using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using STS2Mobile.Patches;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var harmony = new Harmony("asset-preload.test");
AssetPreloadPatches.Apply(harmony);
try
{
    var session = new AssetLoadingSession("a", "b", "c", "d");
    session.Process();
    Check(ResourceLoader.Requests.SequenceEqual(new[] { "a", "b" }), "Initial loading limit changed");
    session.Process();
    Check(ResourceLoader.Requests.Count == 2, "In-progress requests must retain their slots");

    ResourceLoader.Statuses["a"] = ResourceLoader.ThreadLoadStatus.Loaded;
    session.Process();
    Check(ResourceLoader.Requests.SequenceEqual(new[] { "a", "b", "c" }),
        "Completed slot was left idle until the next frame");
    Check(ResourceLoader.Gets.Count == 0, "Resource finalization must remain owned by the game");

    ResourceLoader.Statuses["b"] = ResourceLoader.ThreadLoadStatus.Failed;
    ResourceLoader.Statuses["c"] = ResourceLoader.ThreadLoadStatus.Loaded;
    session.Process();
    Check(ResourceLoader.Requests.SequenceEqual(new[] { "a", "b", "c", "d" }), "Freed slots were not refilled");
    Check(ResourceLoader.SyncLoads.SequenceEqual(new[] { "b" }), "Original failure fallback was bypassed");
    ResourceLoader.Statuses["d"] = ResourceLoader.ThreadLoadStatus.Loaded;
    session.Process();
    session.Process();
    Check(session.IsCompleted && session.CachedCount == 4, "Session did not finalize all resources");
    Check(ResourceLoader.Gets.SequenceEqual(new[] { "a", "c", "d" }), "Resources finalized more than once");
    Check(ResourceLoader.PeakInProgress == 2, "Refilling increased concurrent loading");

    ResourceLoader.Reset();
    session = new AssetLoadingSession("cached", "request-error", "a", "b", "c");
    session.Cache("cached");
    ResourceLoader.RequestFailures.Add("request-error");
    session.Process();
    Check(ResourceLoader.Requests.SequenceEqual(new[] { "request-error", "a", "b" }),
        "Cached assets or rejected requests consumed a loading slot");
    ResourceLoader.Statuses["a"] = ResourceLoader.ThreadLoadStatus.Loaded;
    ResourceLoader.Statuses["b"] = ResourceLoader.ThreadLoadStatus.Loaded;
    session.Process();
    Check(ResourceLoader.Requests.Last() == "c", "Queue did not continue after cached/rejected assets");
    Check(ResourceLoader.PeakInProgress == 2, "Loading limit changed after a rejected request");
    Console.WriteLine("Asset preload refill, concurrency, finalization and failure tests passed");
}
finally
{
    harmony.UnpatchAll(harmony.Id);
}

namespace Godot
{
    public enum Error { Ok, Failed }
    public sealed class Resource { }
    public static class ResourceLoader
    {
        public enum CacheMode { Reuse }
        public enum ThreadLoadStatus { InvalidResource, InProgress, Failed, Loaded }
        public static readonly List<string> Requests = new(), Gets = new(), SyncLoads = new();
        public static readonly Dictionary<string, ThreadLoadStatus> Statuses = new();
        public static readonly HashSet<string> RequestFailures = new();
        public static int PeakInProgress;

        public static Error LoadThreadedRequest(string path, string hint, bool useSubThreads, CacheMode mode)
        {
            if (useSubThreads || mode != CacheMode.Reuse) throw new Exception("Loader options changed");
            Requests.Add(path);
            if (RequestFailures.Contains(path)) return Error.Failed;
            Statuses[path] = ThreadLoadStatus.InProgress;
            PeakInProgress = Math.Max(PeakInProgress, Statuses.Values.Count(s => s == ThreadLoadStatus.InProgress));
            return Error.Ok;
        }
        public static ThreadLoadStatus LoadThreadedGetStatus(string path) => Statuses[path];
        public static Resource LoadThreadedGet(string path)
        {
            if (Statuses[path] != ThreadLoadStatus.Loaded) throw new Exception("Blocked on an unfinished load");
            Gets.Add(path);
            return new Resource();
        }
        public static Resource Load(string path) { SyncLoads.Add(path); return new Resource(); }
        public static void Reset()
        {
            Requests.Clear(); Gets.Clear(); SyncLoads.Clear(); Statuses.Clear(); RequestFailures.Clear();
            PeakInProgress = 0;
        }
    }
}

namespace MegaCrit.Sts2.Core.Assets
{
    // Retains AssetLoadingSession.Process ordering and its loaded/failed handling
    // so the production Harmony hooks exercise the game's frame boundary.
    public sealed class AssetLoadingSession
    {
        private readonly Queue<string> _loading = new(), _toLoad = new(), _finalizing = new();
        private readonly ConcurrentDictionary<string, Resource> _cache = new();
        public bool IsCompleted => _loading.Count == 0 && _toLoad.Count == 0 && _finalizing.Count == 0;
        public int CachedCount => _cache.Count;
        public AssetLoadingSession(params string[] paths) { foreach (string path in paths) _toLoad.Enqueue(path); }
        public void Cache(string path) => _cache[path] = new Resource();

        public void Process()
        {
            while (_finalizing.TryDequeue(out string path))
                _cache[path] = ResourceLoader.LoadThreadedGet(path);
            ProcessLoadingQueue();
            CheckLoadingStatus();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ProcessLoadingQueue() => throw new Exception("Original unbounded queue must be replaced");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CheckLoadingStatus()
        {
            int count = _loading.Count;
            for (int i = 0; i < count; i++)
            {
                string path = _loading.Dequeue();
                switch (ResourceLoader.LoadThreadedGetStatus(path))
                {
                    case ResourceLoader.ThreadLoadStatus.Loaded: _finalizing.Enqueue(path); break;
                    case ResourceLoader.ThreadLoadStatus.InProgress: _loading.Enqueue(path); break;
                    case ResourceLoader.ThreadLoadStatus.Failed:
                    case ResourceLoader.ThreadLoadStatus.InvalidResource: _cache[path] = ResourceLoader.Load(path); break;
                }
            }
        }
    }
}
