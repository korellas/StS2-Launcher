using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace STS2Mobile.Patches;

// Replaces ModelDb.Init() with a two-phase initialization to avoid circular dependency
// crashes. Phase 1 pre-populates the registry with uninitialized objects so cross-type
// references resolve during construction. Phase 2 runs the actual constructors.
//
// Performance notes:
//  - All reflection targets are resolved once in Apply() and cached in static fields.
//  - GetId is called through a FastInvokeHandler (Harmony MethodInvoker) instead of
//    MethodInfo.Invoke to avoid per-call argument boxing into a new object[].
//  - _contentById is cast to IDictionary so we write entries with dict[id] = model
//    instead of reflecting into the set_Item method on every iteration.
//  - Only the base constructor's duplicate check is bypassed during Phase 2;
//    normal registry lookups remain available to model constructors.
public static class ModelDbInitPatch
{
    private static bool _runningConstructors;

    private static PropertyInfo _allSubtypesProp;
    private static MethodInfo _getIdMethod;
    private static HarmonyLib.FastInvokeHandler _getIdInvoker;
    private static FieldInfo _contentByIdField;

    private static readonly BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    public static void Apply(Harmony harmony)
    {
        var modelDbType = typeof(ModelDb);

        _allSubtypesProp = modelDbType.GetProperty("AllAbstractModelSubtypes", StaticFlags);
        _getIdMethod = modelDbType.GetMethod(
            "GetId",
            StaticFlags,
            null,
            new[] { typeof(Type) },
            null
        );
        _contentByIdField = modelDbType.GetField(
            "_contentById",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        if (_getIdMethod != null)
            _getIdInvoker = HarmonyLib.MethodInvoker.GetHandler(_getIdMethod);

        PatchHelper.Patch(
            harmony,
            modelDbType,
            "Init",
            prefix: PatchHelper.Method(typeof(ModelDbInitPatch), nameof(InitPrefix))
        );

        harmony.Patch(
            AccessTools.Constructor(typeof(AbstractModel), Type.EmptyTypes),
            transpiler: new HarmonyMethod(
                PatchHelper.Method(typeof(ModelDbInitPatch), nameof(ConstructorTranspiler))
            )
        );
        PatchHelper.Log("Patched AbstractModel constructor duplicate check");
    }

    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var contains = AccessTools.Method(
            typeof(ModelDb),
            nameof(ModelDb.Contains),
            new[] { typeof(Type) }
        );
        var lookup = AccessTools
            .Method(typeof(ModelDb), nameof(ModelDb.GetByIdOrNull))
            .MakeGenericMethod(typeof(AbstractModel));
        var codes = new List<CodeInstruction>(instructions);
        bool patched = false;
        foreach (var instruction in codes)
        {
            if (instruction.Calls(contains))
            {
                instruction.operand = PatchHelper.Method(
                    typeof(ModelDbInitPatch),
                    nameof(ContainsForConstructor)
                );
                patched = true;
            }
            else if (instruction.Calls(lookup))
            {
                instruction.operand = PatchHelper.Method(
                    typeof(ModelDbInitPatch),
                    nameof(LookupForConstructor)
                );
                patched = true;
            }
        }
        if (!patched)
            throw new InvalidOperationException("AbstractModel duplicate check not found");
        return codes;
    }

    public static bool ContainsForConstructor(Type type) =>
        !_runningConstructors && ModelDb.Contains(type);

    public static AbstractModel LookupForConstructor(ModelId id) =>
        _runningConstructors ? null : ModelDb.GetByIdOrNull<AbstractModel>(id);

    public static bool InitPrefix()
    {
        PatchHelper.Log("Running patched ModelDb.Init()");

        if (_allSubtypesProp == null || _getIdInvoker == null || _contentByIdField == null)
        {
            PatchHelper.Log(
                "ModelDbInitPatch: cached reflection missing, falling back to original Init()"
            );
            return true;
        }

        var types = (Type[])_allSubtypesProp.GetValue(null);
        var contentById = (IDictionary)_contentByIdField.GetValue(null);

        PatchHelper.Log(
            $"Phase 1: Pre-registering {types.Length} types with uninitialized objects"
        );

        var typeObjects = new Dictionary<Type, object>(types.Length);
        var getIdArgs = new object[1];
        int preRegCount = 0;
        var phase1Stride = Math.Max(1, types.Length / 4);

        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            try
            {
                getIdArgs[0] = type;
                var id = _getIdInvoker(null, getIdArgs);
                var model = RuntimeHelpers.GetUninitializedObject(type);
                contentById[id] = model;
                typeObjects[type] = model;
                preRegCount++;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"Phase 1 - Failed to pre-register {type.Name}: {ex.Message}");
            }

            if ((i + 1) % phase1Stride == 0 && i + 1 < types.Length)
                PatchHelper.Log($"[ModelDb] Phase 1: {i + 1}/{types.Length} types registered");
        }

        PatchHelper.Log($"Phase 1 complete: {preRegCount} types pre-registered");

        // Phase 2: Initialize the registered objects without treating them as duplicates.
        PatchHelper.Log("Phase 2: Running constructors");

        _runningConstructors = true;
        int successCount = 0;
        var failed = new List<Type>();
        var phase2Stride = Math.Max(1, types.Length / 4);

        try
        {
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (!typeObjects.TryGetValue(type, out var model))
                    continue;

                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                    var ctor = type.GetConstructor(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null
                    );
                    ctor?.Invoke(model, null);

                    successCount++;
                }
                catch (Exception ex)
                {
                    failed.Add(type);
                    var inner = ex;
                    while (inner.InnerException != null)
                        inner = inner.InnerException;
                    PatchHelper.Log(
                        $"Phase 2 - Failed {type.Name}: {inner.GetType().Name}: {inner.Message}"
                    );
                }

                if ((i + 1) % phase2Stride == 0 && i + 1 < types.Length)
                    PatchHelper.Log($"[ModelDb] Phase 2: {i + 1}/{types.Length} constructors run");
            }
        }
        finally
        {
            _runningConstructors = false;
        }

        if (failed.Count > 0)
        {
            PatchHelper.Log(
                $"WARNING: {failed.Count}/{types.Length} types had constructor errors:"
            );
            foreach (var type in failed)
                PatchHelper.Log($"  - {type.FullName}");
        }
        else
        {
            PatchHelper.Log($"All {successCount} model types registered successfully");
        }

        return false;
    }
}
