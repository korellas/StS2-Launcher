using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;

namespace STS2Mobile.Patches;

// Workaround for a Godot ResourceLoader race.
//
// AssetLoadingSession.ProcessLoadingQueue hard-codes _batchSize = 128,
// so up to 128 ResourceLoader.LoadThreadedRequest calls are in flight at
// once. When two of Godot's internal loader threads parse .tres files
// that share the same ext_resource sub-resource (e.g. a visual-shader
// node), both threads call Resource::set_path for the same path; the
// second one bails with
//   "Another resource is loaded from path '...' (possible cyclic
//   resource inclusion)"  [core/io/resource.cpp:95]
// but the partially-constructed object still gets destructed and
// unrefs static StringNames it never inc'd, producing a cascade of
//   "BUG: Unreferenced static string to 0: markers / _compression /
//    _initialize / Variant / ..."
// which corrupts the GDExtension vtable. The next virtual dispatch
// fires "libc++abi: Pure virtual function called!" and the process
// aborts with SIGABRT.
//
// Repro: entering any Combat Room whose preload set contains two .tres
// files that both reference the same shader. Most reliably hit on the
// decimillipede encounter via decimillipede_suck_shader.tres.
//
// Fix: prefix-replace ProcessLoadingQueue with a serial version that
// only keeps MaxConcurrent requests in flight. With 1 in flight, Godot's
// internal pool runs at most one parser at a time and the sub-resource
// set_path can't race.
public static class AssetPreloadPatches
{
    // 1 is enough to prevent the race and still keeps preload in the
    // background (the outer Process() runs per-frame from NAssetLoader).
    // Bumping this to 2 re-introduces the crash on repro; leave at 1.
    private const int MaxConcurrent = 1;

    private static FieldInfo _loadingField;
    private static FieldInfo _toLoadField;
    private static FieldInfo _cacheField;

    public static void Apply(Harmony harmony)
    {
        var t = typeof(AssetLoadingSession);
        _loadingField = AccessTools.Field(t, "_loading");
        _toLoadField = AccessTools.Field(t, "_toLoad");
        _cacheField = AccessTools.Field(t, "_cache");

        if (_loadingField == null || _toLoadField == null || _cacheField == null)
        {
            PatchHelper.Log(
                "[Preload] FAILED to resolve AssetLoadingSession fields; patch skipped"
            );
            return;
        }

        PatchHelper.Patch(
            harmony,
            t,
            "ProcessLoadingQueue",
            prefix: PatchHelper.Method(
                typeof(AssetPreloadPatches),
                nameof(ProcessLoadingQueuePrefix)
            )
        );

        PatchHelper.Log(
            $"[Preload] Serialised ProcessLoadingQueue (max {MaxConcurrent} in-flight)"
        );
    }

    public static bool ProcessLoadingQueuePrefix(object __instance)
    {
        var loading = (Queue<string>)_loadingField.GetValue(__instance);
        var toLoad = (Queue<string>)_toLoadField.GetValue(__instance);
        var cache = (ConcurrentDictionary<string, Resource>)_cacheField.GetValue(__instance);

        while (loading.Count < MaxConcurrent && toLoad.TryDequeue(out var path))
        {
            if (cache.ContainsKey(path))
                continue;

            if (
                ResourceLoader.LoadThreadedRequest(
                    path,
                    "",
                    useSubThreads: false,
                    ResourceLoader.CacheMode.Reuse
                ) == Error.Ok
            )
            {
                loading.Enqueue(path);
            }
            else
            {
                PatchHelper.Log($"[Preload] Error requesting load for path: {path}");
            }
        }

        return false;
    }
}
