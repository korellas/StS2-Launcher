using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2Mobile;
using STS2Mobile.Launcher;

namespace BenchmarkTraceTests
{
    internal static class Program
    {
        [DllImport("trace-test")] private static extern int trace_open();
        [DllImport("trace-test")] private static extern int trace_sync_open();
        [DllImport("trace-test")] private static extern void trace_enabled([MarshalAs(UnmanagedType.I1)] bool enabled);
        private static int _existingCalls;
        private static void ExistingPrefix() => _existingCalls++;
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
        }

        private static async Task Main(string[] args)
        {
            var library = NativeLibrary.Load(args[0]);
            NativeLibrary.SetDllImportResolver(typeof(BenchmarkTrace).Assembly, (name, assembly, path) =>
                name is "libandroid.so" or "trace-test" ? library : IntPtr.Zero);
            var app = new GodotObject();
            var tree = new SceneTree();
            app.Supported = false;
            Check(
                BenchmarkTrace.TryStart(app, tree, out string supportFailure, recordDeath: true) == null
                    && supportFailure.Contains("support check returned false"),
                "Unsupported Android must explain why diagnostics could not start"
            );
            Check(BenchmarkTrace.TryStart(app, tree) == null, "Unsupported Android must skip hooks");
            app.Supported = true;
            trace_enabled(false);
            Check(BenchmarkTrace.TryStart(app, tree) == null, "Inactive tracing must skip hooks");
            trace_enabled(true);

            var existing = new Harmony("benchmark-test.existing");
            var start = AccessTools.Method(typeof(CombatManager), nameof(CombatManager.StartCombatInternal));
            existing.Patch(start, prefix: new HarmonyMethod(typeof(Program), nameof(ExistingPrefix)));
            using (var trace = BenchmarkTrace.TryStart(app, tree))
            {
                Check(trace != null && tree.Listeners == 1, "Active trace must attach");
                var combat = new CombatManager();
                foreach (string outcome in new[] { "complete", "fail", "cancel" })
                {
                    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    combat.NextTask = completion.Task;
                    var returned = combat.StartCombatInternal();
                    Check(ReferenceEquals(returned, completion.Task), "Original Task identity changed");
                    Check(trace_open() == 1 && trace_sync_open() == 0, "Async span must outlive synchronous entry");
                    var error = new InvalidOperationException("original game failure");
                    if (outcome == "complete") completion.SetResult();
                    if (outcome == "fail") completion.SetException(error);
                    if (outcome == "cancel") completion.SetCanceled();
                    try { await returned; Check(outcome == "complete", "Failure swallowed"); }
                    catch (InvalidOperationException ex) { Check(ReferenceEquals(ex, error), "Exception changed"); }
                    catch (OperationCanceledException) { Check(outcome == "cancel", "Cancellation changed"); }
                    for (int i = 0; i < 100 && trace_open() != 0; i++) await Task.Delay(10);
                    Check(trace_open() == 0 && trace_sync_open() == 0, "Task span not closed");
                }
                combat.ThrowOnEntry = true;
                try { _ = combat.StartCombatInternal(); throw new Exception("Synchronous failure swallowed"); }
                catch (InvalidOperationException) { }
                Check(trace_open() == 0 && trace_sync_open() == 0, "Throwing entry left a span open");
                new NCombatRoom()._Ready();
                Check(trace_sync_open() == 0, "Scene creation span unbalanced");
                tree.Frame();
            }
            Check(tree.Listeners == 0, "Frame callback leaked");
            int before = _existingCalls;
            _ = new CombatManager().StartCombatInternal();
            Check(_existingCalls == before + 1, "Disposing removed another owner's hook");
            Check(!Harmony.GetPatchInfo(start).Owners.Contains("sts2mobile.benchmark.trace"), "Trace hook leaked");
            existing.UnpatchAll(existing.Id);
            trace_enabled(false);
            using (var standalone = BenchmarkTrace.TryStart(app, tree, recordDeath: true))
            {
                Check(
                    standalone != null && tree.Listeners == 1,
                    "Death diagnostics must work without an ADB trace"
                );
                standalone.BeginDeathSample();
                var run = new MegaCrit.Sts2.Core.Runs.RunManager();
                var result = run.OnEnded(false);
                Check(
                    ReferenceEquals(result, run.Saved),
                    "Timing hooks changed the game's returned value"
                );
                Godot.RenderingServer.Canvas = 2;
                tree.Frame();
                string report = standalone.DeathReport();
                Check(
                    report.Contains("RunManager.OnEnded") && report.Contains("RunManager.ToSave"),
                    "Nested death timings were not collected"
                );
                Check(
                    report.Contains("ProcessFrame gap") && report.Contains("canvasPipelines=+2"),
                    "Long frame and pipeline correlation missing"
                );
                Check(
                    trace_open() == 0 && trace_sync_open() == 0,
                    "Standalone diagnostics wrote native trace spans"
                );
                standalone.BeginDeathSample();
                Check(
                    !standalone.DeathReport().Contains("RunManager.OnEnded"),
                    "Diagnostics leaked across benchmark cases"
                );
            }
            Check(tree.Listeners == 0, "Standalone death diagnostics leaked its frame callback");
            RenderingServer.FailNext = true;
            Check(
                BenchmarkTrace.TryStart(app, tree, out string setupFailure, recordDeath: true) == null,
                "Counter setup failure should disable diagnostics"
            );
            Check(
                PatchHelper.Messages.Last().Contains("ProcessFrame counters")
                    && PatchHelper.Messages.Last().Contains("native counter detail"),
                "Setup failure must retain its stage and inner exception for copied reports"
            );
            Check(
                setupFailure.Contains("ProcessFrame counters")
                    && setupFailure.Contains("native counter detail"),
                "Caller must receive the complete failure for the benchmark result"
            );
            Check(tree.Listeners == 0, "Failed setup leaked its frame callback");
            Console.WriteLine("Benchmark trace task, exception, gating and cleanup tests passed");
        }
    }
}

namespace Godot
{
    public sealed class Variant { public bool Value; public bool AsBool() => Value; }
    public sealed class GodotObject
    {
        public bool Supported = true;
        public Variant Call(string method) => new() { Value = Supported };
    }
    public sealed class SceneTree
    {
        public event Action ProcessFrame;
        public int Listeners => ProcessFrame?.GetInvocationList().Length ?? 0;
        public void Frame() => ProcessFrame?.Invoke();
    }
    public static class Time { public static ulong GetTicksUsec() => (ulong)Environment.TickCount64 * 1000; }
    public static class RenderingServer
    {
        public enum RenderingInfo { PipelineCompilationsCanvas, PipelineCompilationsSpecialization }
        public static ulong Canvas;
        public static bool FailNext;
        public static ulong GetRenderingInfo(RenderingInfo value)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("counter unavailable", new NotSupportedException("native counter detail"));
            }
            return value == RenderingInfo.PipelineCompilationsCanvas ? Canvas : 0;
        }
    }
}

namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public sealed class Creature { }
}

namespace MegaCrit.Sts2.Core.Commands
{
    public static class CreatureCmd
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Task Kill(
            IReadOnlyCollection<MegaCrit.Sts2.Core.Entities.Creatures.Creature> creatures,
            bool force
        ) => Task.CompletedTask;
    }
}

namespace MegaCrit.Sts2.Core.Runs
{
    public sealed class RunManager
    {
        public readonly object Saved = new();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public object ToSave() => Saved;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public object OnEnded(bool victory)
        {
            Thread.Sleep(80);
            return ToSave();
        }
    }
}

namespace MegaCrit.Sts2.Core.Nodes
{
    public sealed class NRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowGameOverScreen(object save) { }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Combat
{
    public sealed class NCreature
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public float StartDeathAnim(bool remove) => 0;
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen
{
    public sealed class NGameOverScreen
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static NGameOverScreen Create(object run, object save) => new();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void _Ready() { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AfterOverlayOpened() { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void MoveCreaturesToDifferentLayerAndDisableUi() { }
    }
}

namespace STS2Mobile
{
    public static class PatchHelper
    {
        public static readonly List<string> Messages = new();
        public static void Log(string text)
        {
            Messages.Add(text);
            Console.WriteLine(text);
        }
    }
}

namespace MegaCrit.Sts2.Core.Assets
{
    public static class PreloadManager
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task LoadRoomAssets() => Task.CompletedTask;
    }
}

namespace MegaCrit.Sts2.Core.Combat
{
    public sealed class CombatManager
    {
        public Task NextTask = Task.CompletedTask;
        public bool ThrowOnEntry;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Task StartCombatInternal()
        {
            if (ThrowOnEntry) throw new InvalidOperationException();
            return NextTask;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private Task StartTurn() => Task.CompletedTask;
        [MethodImpl(MethodImplOptions.NoInlining)] private Task SetupPlayerTurn() => Task.CompletedTask;
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public sealed class NCombatRoom
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public void _Ready() { }
        [MethodImpl(MethodImplOptions.NoInlining)] private void OnCombatSetUp() { }
    }
}
