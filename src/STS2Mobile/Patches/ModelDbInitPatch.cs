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
//  - The Contains short-circuit is installed as a permanent Harmony prefix in Apply()
//    and gated by the _suppressContains flag; the original code installed and removed
//    a second Harmony instance inside InitPrefix on every call, which is pure overhead.
public static class ModelDbInitPatch
{
    private static bool _suppressContains;

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
        _contentByIdField = modelDbType.GetField("_contentById", BindingFlags.NonPublic | BindingFlags.Static);

        if (_getIdMethod != null)
            _getIdInvoker = HarmonyLib.MethodInvoker.GetHandler(_getIdMethod);

        PatchHelper.Patch(
            harmony,
            modelDbType,
            "Init",
            prefix: PatchHelper.Method(typeof(ModelDbInitPatch), nameof(InitPrefix))
        );

        // Permanent Contains(Type) prefix that short-circuits only while Phase 2 is
        // running. Replaces the old pattern of installing a secondary Harmony patch
        // inside InitPrefix and un-patching it at the end. Contains has overloads so
        // we bind to the specific signature instead of using PatchHelper.Patch.
        var containsMethod = modelDbType.GetMethod(
            "Contains",
            StaticFlags,
            null,
            new[] { typeof(Type) },
            null
        );
        if (containsMethod != null)
        {
            try
            {
                harmony.Patch(
                    containsMethod,
                    new HarmonyMethod(
                        PatchHelper.Method(typeof(ModelDbInitPatch), nameof(ContainsPrefix))
                    )
                );
                PatchHelper.Log("Patched ModelDb.Contains (Type)");
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"FAILED ModelDb.Contains patch: {ex.Message}");
            }
        }
        else
        {
            PatchHelper.Log("ModelDb.Contains(Type) not found; suppressContains disabled");
        }
    }

    public static bool ContainsPrefix(ref bool __result)
    {
        if (_suppressContains)
        {
            __result = false;
            return false;
        }
        return true;
    }

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

        // Phase 2: Run constructors on pre-allocated objects. _suppressContains stops
        // the game's Contains() from reporting "not registered" mid-construction.
        PatchHelper.Log("Phase 2: Running constructors");

        _suppressContains = true;
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
            _suppressContains = false;
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
