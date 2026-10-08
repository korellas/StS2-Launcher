using Godot;
using STS2Mobile.Launcher;

void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}

foreach (var (showResults, resume) in new[] { (true, true), (true, false), (false, false) })
{
    Callable.Pending.Clear();
    RenderBenchmarkScreen.ShowResults = showResults;
    RenderBenchmarkScreen.Resume = resume;
    RenderBenchmarkScreen.Calls = 0;
    LauncherController.Starts = 0;
    var tree = new SceneTree();
    var launcher = new LauncherUI { Tree = tree };
    tree.Root.Busy = true;
    launcher.Initialize();
    Check(
        tree.Root.RejectedAdds == 0,
        "Cold boot tried to add benchmark UI while root was entering the tree"
    );
    Check(launcher.Visible, "Launcher disappeared before benchmark UI could attach");
    Check(RenderBenchmarkScreen.Calls == 0, "Recovery ran inside scene initialization");
    tree.Root.Busy = false;
    Callable.Flush();
    Check(RenderBenchmarkScreen.Calls == 1, "Recovery must run once after initialization");
    Check(
        LauncherController.Starts == (resume ? 0 : 1),
        "Benchmark must bypass the Steam session; normal boot must start it"
    );
    Check(
        tree.Root.Attached == (showResults ? 1 : 0),
        "Benchmark screen was not attached after root became available"
    );
    Check(launcher.Visible == !showResults, "Launcher visibility does not match the active entry");
}
Console.WriteLine(
    "PASS resumed benchmark, saved results and normal cold boot defer recovery until scene initialization finishes"
);

Callable.Pending.Clear();
RenderBenchmarkScreen.Calls = 0;
LauncherController.Starts = 0;
var exiting = new LauncherUI { Tree = new SceneTree() };
exiting.Initialize();
exiting.Exit();
Callable.Flush();
Check(
    RenderBenchmarkScreen.Calls == 0 && LauncherController.Starts == 0,
    "An exited launcher ran its queued startup"
);
Console.WriteLine("PASS an exited launcher does not resume a benchmark or start a session");

namespace Godot
{
    public struct Vector2(float x, float y)
    {
        public float X = x;
        public float Y = y;
    }

    public class Rect2
    {
        public Vector2 Size = new(1920, 1080);
    }

    public class Viewport
    {
        public Rect2 GetVisibleRect() => new();
    }

    public class Control
    {
        public enum LayoutPreset
        {
            FullRect,
        }

        public int ZIndex;
        public Vector2 Size;
        public bool Visible = true;
        public SceneTree Tree;
        public event Action TreeExiting;

        public void SetAnchorsPreset(LayoutPreset preset) { }

        public Viewport GetViewport() => new();

        public SceneTree GetTree() => Tree;

        public void Hide() => Visible = false;

        public void Exit() => TreeExiting?.Invoke();
    }

    public class Window
    {
        public bool Busy;
        public int RejectedAdds;
        public int Attached;

        public void AddChild(Control control)
        {
            // Godot Node::add_child refuses additions while _propagate_enter_tree visits children.
            if (Busy)
            {
                RejectedAdds++;
                return;
            }
            Attached++;
        }
    }

    public class SceneTree
    {
        public Window Root = new();
        public bool AutoAcceptQuit = true;
        public event Action ProcessFrame
        {
            add { }
            remove { }
        }
    }

    public static class OS
    {
        public static string GetDataDir() => "/unused";
    }

    public class Callable(Action action)
    {
        public static readonly Queue<Action> Pending = new();

        public static Callable From(Action action) => new(action);

        public void CallDeferred() => Pending.Enqueue(action);

        public static void Flush()
        {
            while (Pending.TryDequeue(out var action))
                action();
        }
    }
}

namespace STS2Mobile.Launcher
{
    public class LauncherModel
    {
        public LauncherModel(string dataDir) { }

        public bool InGameMode;

        public static bool LoadCloudSyncPref() => true;

        public Task WaitForLaunch() => Task.CompletedTask;

        public void Dispose() { }
    }

    public class LauncherView
    {
        public LauncherView(Control owner, float scale) { }

        public void UpdateKeyboardOffset() { }
    }

    public class LauncherController
    {
        public LauncherController(LauncherModel model, LauncherView view, Action<Action> queue) { }

        public static int Starts;

        public void Start() => Starts++;

        public void Dispose() { }
    }

    public static class RenderBenchmarkScreen
    {
        public static bool ShowResults;
        public static bool Resume;
        public static int Calls;

        public static bool RecoverBoot(LauncherUI owner)
        {
            Calls++;
            if (ShowResults)
            {
                owner.Hide();
                owner.GetTree().Root.AddChild(new Control());
            }
            return Resume;
        }
    }
}

namespace STS2Mobile.Patches
{
    public static class LauncherPatches
    {
        public static bool CloudSyncEnabled;
    }

    public static class GraphicsPatches
    {
        public static void Initialize() { }
    }
}

namespace STS2Mobile
{
    public static class PatchHelper
    {
        public static void Log(string message) { }
    }
}
