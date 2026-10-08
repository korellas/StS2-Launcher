using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Mobile.Launcher;

// Diagnostic builds can also collect death timings without an external Android trace.
internal sealed class BenchmarkTrace : IDisposable
{
    private static BenchmarkTrace _active;
    private static int _cookie;
    private readonly Harmony _harmony = new("sts2mobile.benchmark.trace");
    private readonly SceneTree _tree;
    private readonly bool _androidTracing;
    private readonly bool _recordDeath;
    private readonly object _diagnosticLock = new();
    private readonly List<(string Name, long Start, long End)> _timings = new();
    private readonly List<(
        double AtMs,
        double GapMs,
        long Canvas,
        long Specialized,
        long[] Gc
    )> _stalls = new();
    private long _sampleStart;
    private long _previousFrame;
    private const double StallThresholdMs = 50;
    private readonly long[] _lastCounters = new long[GC.MaxGeneration + 3];
    private ulong _lastFrame;
    private string _setupStage;

    private BenchmarkTrace(SceneTree tree, bool androidTracing, bool recordDeath)
    {
        _tree = tree;
        _androidTracing = androidTracing;
        _recordDeath = recordDeath;
        Array.Fill(_lastCounters, -1);
    }

    public static BenchmarkTrace TryStart(
        GodotObject app,
        SceneTree tree,
        bool recordDeath = false
    ) => TryStart(app, tree, out _, recordDeath);

    public static BenchmarkTrace TryStart(
        GodotObject app,
        SceneTree tree,
        out string failure,
        bool recordDeath = false
    )
    {
        failure = null;
        string stage = "Android tracing support";
        BenchmarkTrace trace = null;
        try
        {
            if (!app.Call("supportsBenchmarkTracing").AsBool())
            {
                if (recordDeath)
                    failure =
                        "Death diagnostics unavailable: Android tracing support check returned false.";
                return null;
            }
            stage = "ATrace_isEnabled";
            bool androidTracing = ATrace_isEnabled();
            if (!androidTracing && !recordDeath)
                return null;
            trace = new BenchmarkTrace(tree, androidTracing, recordDeath);
            _active = trace;
            using var setup = trace.Sync("TraceSetup");
            if (recordDeath)
            {
                trace.PatchTask(
                    typeof(CreatureCmd),
                    "Kill",
                    new[] { typeof(IReadOnlyCollection<Creature>), typeof(bool) }
                );
                trace.PatchSync(typeof(NCreature), "StartDeathAnim");
                trace.PatchSync(typeof(RunManager), "ToSave");
                trace.PatchSync(typeof(RunManager), "OnEnded");
                trace.PatchSync(typeof(NRun), "ShowGameOverScreen");
                trace.PatchSync(typeof(NGameOverScreen), "Create");
                trace.PatchSync(typeof(NGameOverScreen), "_Ready");
                trace.PatchSync(typeof(NGameOverScreen), "AfterOverlayOpened");
                trace.PatchSync(
                    typeof(NGameOverScreen),
                    "MoveCreaturesToDifferentLayerAndDisableUi"
                );
            }
            if (androidTracing)
            {
                trace.PatchTask(typeof(PreloadManager), "LoadRoomAssets");
                trace.PatchTask(typeof(CombatManager), "StartCombatInternal");
                trace.PatchTask(typeof(CombatManager), "StartTurn");
                trace.PatchTask(typeof(CombatManager), "SetupPlayerTurn");
                trace.PatchSync(typeof(NCombatRoom), "_Ready");
                trace.PatchSync(typeof(NCombatRoom), "OnCombatSetUp");
            }
            trace._setupStage = "ProcessFrame counters";
            tree.ProcessFrame += trace.SampleCounters;
            trace.SampleCounters();
            PatchHelper.Log(
                $"[BenchmarkTrace] Android trace={androidTracing}, death diagnostics={recordDeath}"
            );
            return trace;
        }
        catch (Exception error)
        {
            failure =
                $"Death diagnostics unavailable: setup stage={trace?._setupStage ?? stage}\n{error}";
            trace?.Dispose();
            PatchHelper.Log($"[BenchmarkTrace] Cannot enable markers: {failure}");
            return null;
        }
    }

    private void PatchTask(Type type, string name, Type[] parameters = null)
    {
        _setupStage = type.FullName + "." + name;
        var method = AccessTools.Method(type, name, parameters);
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
        _setupStage = type.FullName + "." + name;
        var method = AccessTools.Method(type, name);
        if (method == null || typeof(Task).IsAssignableFrom(method.ReturnType))
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
        long frameTick = Stopwatch.GetTimestamp();
        ulong now = Time.GetTicksUsec();
        if (_androidTracing && _lastFrame != 0)
            ATrace_setCounter("STS2Bench.ProcessFrameIntervalUs", (long)(now - _lastFrame));
        _lastFrame = now;
        long canvas = (long)
            RenderingServer.GetRenderingInfo(
                RenderingServer.RenderingInfo.PipelineCompilationsCanvas
            );
        long specialized = (long)
            RenderingServer.GetRenderingInfo(
                RenderingServer.RenderingInfo.PipelineCompilationsSpecialization
            );
        long canvasDelta = _lastCounters[0] < 0 ? 0 : canvas - _lastCounters[0];
        long specializedDelta = _lastCounters[1] < 0 ? 0 : specialized - _lastCounters[1];
        Counter(0, "STS2Bench.Pipelines.Canvas", canvas);
        Counter(1, "STS2Bench.Pipelines.Specialization", specialized);
        double gap =
            _previousFrame == 0
                ? 0
                : Stopwatch.GetElapsedTime(_previousFrame, frameTick).TotalMilliseconds;
        bool stalled = _recordDeath && _sampleStart != 0 && gap >= StallThresholdMs;
        long[] gcDeltas = stalled ? new long[GC.MaxGeneration + 1] : null;
        for (int gen = 0; gen <= GC.MaxGeneration; gen++)
        {
            long count = GC.CollectionCount(gen);
            if (gcDeltas != null && _lastCounters[gen + 2] >= 0)
                gcDeltas[gen] = count - _lastCounters[gen + 2];
            if (_lastCounters[gen + 2] != count)
                Counter(gen + 2, "STS2Bench.GC.Collections.Gen" + gen, count);
        }
        if (stalled)
        {
            _stalls.Add(
                (
                    Stopwatch.GetElapsedTime(_sampleStart, frameTick).TotalMilliseconds,
                    gap,
                    canvasDelta,
                    specializedDelta,
                    gcDeltas
                )
            );
        }
        _previousFrame = frameTick;
    }

    private void Counter(int index, string name, long value)
    {
        if (_lastCounters[index] == value)
            return;
        _lastCounters[index] = value;
        if (_androidTracing)
            ATrace_setCounter(name, value);
    }

    public void BeginDeathSample()
    {
        if (!_recordDeath)
            return;
        lock (_diagnosticLock)
        {
            _timings.Clear();
            _stalls.Clear();
            _sampleStart = Stopwatch.GetTimestamp();
        }
        _previousFrame = 0;
        SampleCounters();
    }

    public string DeathReport()
    {
        if (!_recordDeath)
            return null;
        lock (_diagnosticLock)
        {
            var lines = new List<string>
            {
                "Death diagnostics: inclusive wall times; async spans include animation waits. Hook setup is outside samples and may pre-JIT target methods; do not use this run for engine ranking. GC counts are not pause durations; pipeline deltas do not measure compile time.",
            };
            foreach (var span in _timings.OrderBy(x => x.Start))
                lines.Add(
                    FormattableString.Invariant(
                        $"  {span.Name}: start={Stopwatch.GetElapsedTime(_sampleStart, span.Start).TotalMilliseconds:F3} ms, wall={Stopwatch.GetElapsedTime(span.Start, span.End).TotalMilliseconds:F3} ms"
                    )
                );
            foreach (var frame in _stalls)
                lines.Add(
                    FormattableString.Invariant(
                        $"  ProcessFrame gap: end={frame.AtMs:F3} ms, gap={frame.GapMs:F3} ms, canvasPipelines=+{frame.Canvas}, specializedPipelines=+{frame.Specialized}, gcCollections=[{string.Join(",", frame.Gc.Select((delta, generation) => $"g{generation}=+{delta}"))}]"
                    )
                );
            return string.Join("\n", lines);
        }
    }

    private void RecordTiming(string name, long epoch, long start)
    {
        if (epoch == 0)
            return;
        long end = Stopwatch.GetTimestamp();
        lock (_diagnosticLock)
            if (_sampleStart == epoch)
                _timings.Add((name, start, end));
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
        private readonly BenchmarkTrace _owner = _active;
        private readonly long _epoch;
        private readonly long _started;

        public AsyncSpan(string name)
        {
            _name = name;
            _epoch = _owner?._recordDeath == true ? _owner._sampleStart : 0;
            _started = _epoch == 0 ? 0 : Stopwatch.GetTimestamp();
            if (_owner?._androidTracing == true)
                ATrace_beginAsyncSection(name, _id);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                if (_owner?._androidTracing == true)
                    ATrace_endAsyncSection(_name, _id);
                _owner?.RecordTiming(_name, _epoch, _started);
            }
        }
    }

    private sealed class SyncSpan : IDisposable
    {
        private readonly string _name;
        private readonly BenchmarkTrace _owner = _active;
        private readonly long _epoch;
        private readonly long _started;
        private int _closed;

        public SyncSpan(string name)
        {
            _name = name;
            _epoch = _owner?._recordDeath == true ? _owner._sampleStart : 0;
            _started = _epoch == 0 ? 0 : Stopwatch.GetTimestamp();
            if (_owner?._androidTracing == true)
                ATrace_beginSection(name);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;
            if (_owner?._androidTracing == true)
                ATrace_endSection();
            _owner?.RecordTiming(_name, _epoch, _started);
        }
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
