using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS2Mobile.Launcher;

// Installed only for a benchmark phase with an external Android trace already active.
internal sealed class BenchmarkTrace : IDisposable
{
    private static BenchmarkTrace _active;
    private static int _cookie;
    private readonly Harmony _harmony = new("sts2mobile.benchmark.trace");
    private readonly SceneTree _tree;
    private readonly long[] _lastCounters = new long[GC.MaxGeneration + 3];
    private ulong _lastFrame;

    private BenchmarkTrace(SceneTree tree)
    {
        _tree = tree;
        Array.Fill(_lastCounters, -1);
    }

    public static BenchmarkTrace TryStart(GodotObject app, SceneTree tree)
    {
        BenchmarkTrace trace = null;
        try
        {
            if (!app.Call("supportsBenchmarkTracing").AsBool() || !ATrace_isEnabled())
                return null;
            trace = new BenchmarkTrace(tree);
            _active = trace;
            using var setup = trace.Sync("TraceSetup");
            trace.PatchTask(typeof(PreloadManager), "LoadRoomAssets");
            trace.PatchTask(typeof(CombatManager), "StartCombatInternal");
            trace.PatchTask(typeof(CombatManager), "StartTurn");
            trace.PatchTask(typeof(CombatManager), "SetupPlayerTurn");
            trace.PatchSync(typeof(NCombatRoom), "_Ready");
            trace.PatchSync(typeof(NCombatRoom), "OnCombatSetUp");
            tree.ProcessFrame += trace.SampleCounters;
            trace.SampleCounters();
            PatchHelper.Log("[BenchmarkTrace] Perfetto markers active for this benchmark phase");
            return trace;
        }
        catch (Exception error)
        {
            trace?.Dispose();
            PatchHelper.Log($"[BenchmarkTrace] Cannot enable markers: {error.Message}");
            return null;
        }
    }

    private void PatchTask(Type type, string name)
    {
        var method = AccessTools.Method(type, name);
        if (method == null || method.ReturnType != typeof(Task))
            throw new MissingMethodException(type.FullName, name);
        _harmony.Patch(
            method,
            prefix: Hook(nameof(TaskPrefix)),
            postfix: Hook(nameof(TaskPostfix)),
            finalizer: Hook(nameof(TaskFinalizer))
        );
    }

    private void PatchSync(Type type, string name)
    {
        var method = AccessTools.Method(type, name);
        if (method == null || method.ReturnType != typeof(void))
            throw new MissingMethodException(type.FullName, name);
        _harmony.Patch(
            method,
            prefix: Hook(nameof(SyncPrefix)),
            finalizer: Hook(nameof(SyncFinalizer))
        );
    }

    private static HarmonyMethod Hook(string name) => new(typeof(BenchmarkTrace), name);

    public IDisposable Span(string name) => new AsyncSpan("STS2Bench." + name);

    public IDisposable Sync(string name) => new SyncSpan("STS2Bench." + name);

    private static void TaskPrefix(MethodBase __originalMethod, out MethodSpan __state)
    {
        string name = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
        __state = _active == null ? null : new MethodSpan(name);
    }

    private static void TaskPostfix(Task __result, MethodSpan __state)
    {
        if (__state == null)
            return;
        if (__result == null)
            __state.Wall.Dispose();
        else
            _ = ObserveCompletion(__result, __state.Wall);
    }

    private static async Task ObserveCompletion(Task task, IDisposable span)
    {
        try
        {
            // Observe without replacing the original Task or changing its exception ownership.
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The original caller still receives this task's failure.
        }
        finally
        {
            span.Dispose();
        }
    }

    private static void TaskFinalizer(Exception __exception, MethodSpan __state)
    {
        __state?.Entry.Dispose();
        if (__exception != null)
            __state?.Wall.Dispose();
    }

    private static void SyncPrefix(MethodBase __originalMethod, out IDisposable __state) =>
        __state = _active?.Sync(__originalMethod.DeclaringType.Name + "." + __originalMethod.Name);

    private static void SyncFinalizer(IDisposable __state) => __state?.Dispose();

    private void SampleCounters()
    {
        if (!ATrace_isEnabled())
            return;
        ulong now = Time.GetTicksUsec();
        if (_lastFrame != 0)
            ATrace_setCounter("STS2Bench.ProcessFrameIntervalUs", (long)(now - _lastFrame));
        _lastFrame = now;
        Counter(
            0,
            "STS2Bench.Pipelines.Canvas",
            (long)
                RenderingServer.GetRenderingInfo(
                    RenderingServer.RenderingInfo.PipelineCompilationsCanvas
                )
        );
        Counter(
            1,
            "STS2Bench.Pipelines.Specialization",
            (long)
                RenderingServer.GetRenderingInfo(
                    RenderingServer.RenderingInfo.PipelineCompilationsSpecialization
                )
        );
        for (int gen = 0; gen <= GC.MaxGeneration; gen++)
        {
            long count = GC.CollectionCount(gen);
            if (_lastCounters[gen + 2] != count)
                Counter(gen + 2, "STS2Bench.GC.Collections.Gen" + gen, count);
        }
    }

    private void Counter(int index, string name, long value)
    {
        if (_lastCounters[index] == value)
            return;
        _lastCounters[index] = value;
        ATrace_setCounter(name, value);
    }

    public void Dispose()
    {
        _tree.ProcessFrame -= SampleCounters;
        _active = null;
        _harmony.UnpatchAll(_harmony.Id);
    }

    private sealed class MethodSpan
    {
        public readonly IDisposable Wall;
        public readonly IDisposable Entry;

        public MethodSpan(string name)
        {
            Wall = new AsyncSpan("STS2Bench." + name);
            Entry = new SyncSpan("STS2Bench." + name + ".Entry");
        }
    }

    private sealed class AsyncSpan : IDisposable
    {
        private readonly string _name;
        private readonly int _id = Interlocked.Increment(ref _cookie);
        private int _closed;

        public AsyncSpan(string name)
        {
            _name = name;
            ATrace_beginAsyncSection(name, _id);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
                ATrace_endAsyncSection(_name, _id);
        }
    }

    private sealed class SyncSpan : IDisposable
    {
        public SyncSpan(string name) => ATrace_beginSection(name);

        public void Dispose() => ATrace_endSection();
    }

    [DllImport("libandroid.so")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ATrace_isEnabled();

    [DllImport("libandroid.so")]
    private static extern void ATrace_beginSection(string name);

    [DllImport("libandroid.so")]
    private static extern void ATrace_endSection();

    [DllImport("libandroid.so")]
    private static extern void ATrace_beginAsyncSection(string name, int cookie);

    [DllImport("libandroid.so")]
    private static extern void ATrace_endAsyncSection(string name, int cookie);

    [DllImport("libandroid.so")]
    private static extern void ATrace_setCounter(string name, long value);
}
