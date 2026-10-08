using System.Collections.Concurrent;
using System.Reflection;
using Godot;
using STS2Mobile.Launcher;

void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}
object Invoke(object target, string method, params object[] arguments) =>
    typeof(ShaderWarmupScreen)
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
        .Invoke(target, arguments)!;

var context = new MainThreadContext();
SynchronizationContext.SetSynchronizationContext(context);
TestEngine.MainThread = Environment.CurrentManagedThreadId;
var mode = args.FirstOrDefault() ?? "all";
void Pump(Task task)
{
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (!task.IsCompleted && DateTime.UtcNow < deadline)
    {
        context.Flush();
        TestEngine.Frame();
        Thread.Yield();
    }
    Check(task.IsCompleted, "Warmup did not complete");
    task.GetAwaiter().GetResult();
}

if (mode is "all" or "identity")
{
    var first = new ShaderMaterial { Shader = new Shader() };
    var second = new ShaderMaterial { Shader = new Shader() };
    Check(
        !Equals(Invoke(null, "GetShaderKey", first), Invoke(null, "GetShaderKey", second)),
        "Distinct shaders without resource paths were collapsed"
    );
    Check(
        Equals(
            Invoke(null, "GetShaderKey", first),
            Invoke(null, "GetShaderKey", new ShaderMaterial { Shader = first.Shader })
        ),
        "Materials using the same shader were not deduplicated"
    );
    var particleShader = new ShaderMaterial
    {
        Shader = new Shader { ModeValue = Shader.Mode.Particles },
    };
    Check(
        Invoke(null, "CreateWarmupNode", particleShader, new ImageTexture()) is GpuParticles2D,
        "Particle shaders must run through particle processing, rather than a sprite"
    );
    Check(
        !Equals(
            Invoke(
                null,
                "GetShaderKey",
                new ParticleProcessMaterial { ResourcePath = "res://shared.tres" }
            ),
            Invoke(
                null,
                "GetShaderKey",
                new ParticleProcessMaterial { ResourcePath = "res://shared.tres" }
            )
        ),
        "Distinct particle materials may generate different shaders even when their resource paths match"
    );
    Console.WriteLine("PASS shader identity and particle shader submission");
}

if (mode is "all" or "scan")
{
    ResourceLoader.Reset();
    var shared = new Shader { ResourcePath = "res://shaders/shared.gdshader" };
    var embedded = new ParticleProcessMaterial();
    DirAccess.Directories = new()
    {
        ["res://"] = [("shaders", true), ("scenes", true), ("debug", true)],
        ["res://shaders"] =
        [
            ("shared.gdshader", false),
            ("shared.gdshader.remap", false),
            ("theme.tres", false),
        ],
        ["res://scenes"] =
        [
            ("first.tscn", false),
            ("first.tscn.remap", false),
            ("second.tscn", false),
        ],
        ["res://debug"] = [("ignored.gdshader", false)],
    };
    ResourceLoader.Resources = new()
    {
        [shared.ResourcePath] = shared,
        ["res://shaders/theme.tres"] = new Resource(),
        ["res://scenes/first.tscn"] = new PackedScene(
            new ShaderMaterial { Shader = shared },
            embedded
        ),
        ["res://scenes/second.tscn"] = new PackedScene(
            new ShaderMaterial { Shader = shared },
            embedded
        ),
    };
    var screen = new ShaderWarmupScreen();
    screen.Initialize();
    Callable.Pending.Clear();
    var scan = (Task<List<(string path, Material mat)>>)Invoke(screen, "CollectMaterialsAsync");
    Pump(scan);
    Check(
        ResourceLoader.Threads.All(x => x != TestEngine.MainThread),
        "Resource loading blocked the main thread"
    );
    Check(
        ResourceLoader.MaxConcurrent == 1,
        "Resource parsers ran concurrently despite the shared-resource race"
    );
    Check(
        ResourceLoader.Loads.Count == 4,
        "Duplicate/remapped paths or non-material resources were loaded repeatedly"
    );
    Check(
        scan.Result.Count == 2,
        "Scanning lost embedded materials or retained shared shader duplicates"
    );
    Check(!ResourceLoader.Loads.Any(x => x.Contains("debug")), "Debug resources entered the scan");
    Console.WriteLine(
        "PASS serial background scan, remap deduplication, non-material single load and embedded materials"
    );
}

if (mode is "all" or "compile")
{
    ResourceLoader.Reset();
    DirAccess.Directories = new() { ["res://"] = [("shaders", true)], ["res://shaders"] = [] };
    for (int i = 0; i < 65; i++)
    {
        var name = $"shader{i}.gdshader";
        DirAccess.Directories["res://shaders"].Add((name, false));
        var path = "res://shaders/" + name;
        ResourceLoader.Resources[path] = new Shader
        {
            ResourcePath = path,
            ModeValue = i == 64 ? Shader.Mode.Particles : Shader.Mode.CanvasItem,
        };
    }
    TestEngine.ResetFrames();
    var screen = new ShaderWarmupScreen();
    var completion = screen.WaitForCompletion();
    screen.Initialize();
    Callable.Flush();
    Pump(completion);
    Check(TestEngine.DrawnMaterials.Count == 65, "A warmup material was freed before it rendered");
    Check(
        TestEngine
            .DrawCounts.Where(x => x.Key.Shader.GetMode() == Shader.Mode.Particles)
            .All(x => x.Value >= 2),
        "Particle materials were removed before their subsequent simulation draw"
    );
    Check(TestEngine.WarmupDraws <= 4, "Compilation still waits for small batches of materials");
    Check(!ShaderWarmupScreen.NeedsWarmup(), "Completed warmup did not save its marker");
    Check(screen.Children.OfType<SubViewport>().All(x => x.Freed), "Warmup viewport leaked");
    Console.WriteLine(
        $"PASS all materials rendered in {TestEngine.WarmupDraws} warmup frames and completion was persisted"
    );
}

sealed class MainThreadContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback callback, object state)> _callbacks =
        new();

    public override void Post(SendOrPostCallback callback, object state) =>
        _callbacks.Enqueue((callback, state));

    public void Flush()
    {
        while (_callbacks.TryDequeue(out var item))
            item.callback(item.state);
    }
}

static class TestEngine
{
    public static int MainThread;
    public static List<(string signal, TaskCompletionSource<bool> task)> Waiters = [];
    public static List<SubViewport> Viewports = [];
    public static HashSet<Material> DrawnMaterials = [];
    public static Dictionary<ShaderMaterial, int> DrawCounts = [];
    public static int WarmupDraws;

    public static void AssertMainThread()
    {
        if (Environment.CurrentManagedThreadId != MainThread)
            throw new Exception("Active scene tree was touched from a worker thread");
    }

    public static Task Wait(string signal)
    {
        AssertMainThread();
        var task = new TaskCompletionSource<bool>();
        Waiters.Add((signal, task));
        return task.Task;
    }

    private static void Emit(string signal)
    {
        var pending = Waiters.Where(x => x.signal == signal).ToArray();
        Waiters.RemoveAll(x => x.signal == signal);
        foreach (var item in pending)
            item.task.SetResult(true);
    }

    public static void Frame()
    {
        Emit(SceneTree.SignalName.ProcessFrame);
        var materials = Viewports
            .Where(x => !x.Freed)
            .SelectMany(x => x.Children)
            .Where(x => !x.Freed)
            .Select(x =>
                x is Sprite2D sprite ? sprite.Material : (x as GpuParticles2D)?.ProcessMaterial
            )
            .Where(x => x != null)
            .ToArray();
        if (materials.Length > 0)
            WarmupDraws++;
        foreach (var material in materials)
        {
            DrawnMaterials.Add(material);
            if (material is ShaderMaterial shaderMaterial)
                DrawCounts[shaderMaterial] = DrawCounts.GetValueOrDefault(shaderMaterial) + 1;
        }
        Emit(RenderingServer.SignalName.FramePostDraw);
        Emit(SceneTreeTimer.SignalName.Timeout);
    }

    public static void ResetFrames()
    {
        Waiters.Clear();
        Viewports.Clear();
        DrawnMaterials.Clear();
        DrawCounts.Clear();
        WarmupDraws = 0;
    }
}

namespace Godot
{
    public record struct Vector2(float X, float Y);

    public record struct Vector2I(int X, int Y);

    public record struct Rect2(Vector2 Size);

    public static class Colors
    {
        public static object White = new();
    }

    public class GodotObject
    {
        private static long _next;
        private readonly ulong _id = (ulong)Interlocked.Increment(ref _next);

        public ulong GetInstanceId() => _id;

        public ulong GetRid() => _id;
    }

    public class Resource : GodotObject
    {
        public string ResourcePath { get; set; } = "";
    }

    public class Material : Resource { }

    public class Shader : Resource
    {
        public enum Mode
        {
            CanvasItem,
            Particles,
        }

        public Mode ModeValue;

        public Mode GetMode() => ModeValue;
    }

    public class ShaderMaterial : Material
    {
        public Shader Shader { get; set; }
    }

    public class ParticleProcessMaterial : Material { }

    public class PackedScene(params Material[] materials) : Resource
    {
        public SceneState GetState() => new(materials);
    }

    public class SceneState(Material[] materials)
    {
        public int GetNodeCount() => materials.Length;

        public int GetNodePropertyCount(int n) => 1;

        public string GetNodePropertyName(int n, int p) =>
            materials[n] is ParticleProcessMaterial ? "process_material" : "material";

        public Variant GetNodePropertyValue(int n, int p) => new(materials[n]);
    }

    public record Variant(object Obj);

    public static class ResourceLoader
    {
        public enum CacheMode
        {
            Reuse,
        }

        public static Dictionary<string, Resource> Resources = new();
        public static List<string> Loads = [];
        public static List<int> Threads = [];
        public static int Concurrent;
        public static int MaxConcurrent;

        private static string Normalize(string path) => path.Replace("res:///", "res://");

        public static bool Exists(string path) => Resources.ContainsKey(Normalize(path));

        public static Resource Load(
            string path,
            string typeHint = null,
            CacheMode cacheMode = CacheMode.Reuse
        )
        {
            var active = Interlocked.Increment(ref Concurrent);
            lock (Loads)
            {
                MaxConcurrent = Math.Max(MaxConcurrent, active);
                Loads.Add(Normalize(path));
                Threads.Add(Environment.CurrentManagedThreadId);
            }
            try
            {
                return Resources[Normalize(path)];
            }
            finally
            {
                Interlocked.Decrement(ref Concurrent);
            }
        }

        public static T Load<T>(
            string path,
            string typeHint = null,
            CacheMode cacheMode = CacheMode.Reuse
        )
            where T : Resource => Load(path, typeHint, cacheMode) as T;

        public static void Reset()
        {
            Resources.Clear();
            Loads.Clear();
            Threads.Clear();
            MaxConcurrent = 0;
        }
    }

    public class DirAccess(List<(string name, bool directory)> files) : IDisposable
    {
        public static Dictionary<string, List<(string name, bool directory)>> Directories = new();
        private int _index = -1;

        public static DirAccess Open(string path) =>
            Directories.TryGetValue(path.Replace("res:///", "res://"), out var files)
                ? new(files)
                : null;

        public void ListDirBegin() { }

        public string GetNext() => ++_index < files.Count ? files[_index].name : "";

        public bool CurrentIsDir() => files[_index].directory;

        public void ListDirEnd() { }

        public void Dispose() { }
    }

    public class Node : GodotObject
    {
        public List<Node> Children = [];
        public bool Freed;

        public void AddChild(Node node)
        {
            TestEngine.AssertMainThread();
            Children.Add(node);
        }

        public void QueueFree()
        {
            TestEngine.AssertMainThread();
            Freed = true;
        }

        public SceneTree GetTree()
        {
            TestEngine.AssertMainThread();
            return new();
        }

        public Task ToSignal(object target, string signal) => TestEngine.Wait(signal);
    }

    public class Viewport : Node
    {
        public Rect2 GetVisibleRect() => new(new(1920, 1080));
    }

    public class SubViewport : Viewport
    {
        public SubViewport() => TestEngine.Viewports.Add(this);

        public enum UpdateMode
        {
            Always,
        }

        public Vector2I Size;
        public UpdateMode RenderTargetUpdateMode;
        public bool TransparentBg;
    }

    public class SceneTree
    {
        public static class SignalName
        {
            public const string ProcessFrame = "process";
        }

        public SceneTreeTimer CreateTimer(double seconds) => new();
    }

    public class SceneTreeTimer
    {
        public static class SignalName
        {
            public const string Timeout = "timeout";
        }
    }

    public static class RenderingServer
    {
        public static object Singleton = new();

        public static class SignalName
        {
            public const string FramePostDraw = "draw";
        }
    }

    public class Sprite2D : Node
    {
        public ImageTexture Texture;
        public Material Material;
    }

    public class GpuParticles2D : Node
    {
        public Material ProcessMaterial;
        public int Amount;
        public bool Emitting;
        public bool OneShot;
        public ImageTexture Texture;
    }

    public class Image
    {
        public enum Format
        {
            Rgba8,
        }

        public static Image CreateEmpty(int w, int h, bool mipmaps, Format format) => new();

        public void SetPixel(int x, int y, object color) { }
    }

    public class ImageTexture : Resource
    {
        public static ImageTexture CreateFromImage(Image image) => new();
    }

    public class Control : Node
    {
        public enum LayoutPreset
        {
            FullRect,
        }

        public enum GrowDirection
        {
            Both,
        }

        public enum MouseFilterEnum
        {
            Ignore,
        }

        public enum SizeFlags
        {
            ShrinkCenter,
        }

        public int ZIndex;
        public Vector2 Size;
        public float AnchorLeft,
            AnchorRight,
            AnchorTop,
            AnchorBottom;
        public GrowDirection GrowHorizontal,
            GrowVertical;
        public MouseFilterEnum MouseFilter;
        public Vector2 CustomMinimumSize;
        public SizeFlags SizeFlagsHorizontal;
        public object Modulate;

        public Viewport GetViewport()
        {
            TestEngine.AssertMainThread();
            return new();
        }

        public void SetAnchorsPreset(LayoutPreset preset)
        {
            TestEngine.AssertMainThread();
        }
    }

    public class VBoxContainer : Control
    {
        public void AddThemeConstantOverride(string key, int value) { }
    }

    public class Label : Control
    {
        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                TestEngine.AssertMainThread();
                _text = value;
            }
        }
    }

    public class ProgressBar : Control
    {
        public double MinValue,
            MaxValue;
        private double _value;
        public double Value
        {
            get => _value;
            set
            {
                TestEngine.AssertMainThread();
                _value = value;
            }
        }
        public bool ShowPercentage;
    }

    public class Callable(Action action)
    {
        public static Queue<Action> Pending = new();

        public static Callable From(Action action) => new(action);

        public void CallDeferred() => Pending.Enqueue(action);

        public static void Flush()
        {
            while (Pending.TryDequeue(out var action))
                action();
        }
    }

    public static class OS
    {
        public static readonly string DataDir = Environment.GetEnvironmentVariable(
            "STS2_SHADER_TEST_DATA"
        )!;

        public static string GetUserDataDir()
        {
            Directory.CreateDirectory(DataDir);
            return DataDir;
        }
    }
}

namespace STS2Mobile
{
    public static class PatchHelper
    {
        public static void Log(string message) => Console.WriteLine(message);
    }
}

namespace STS2Mobile.Patches
{
    public static class GraphicsPatches
    {
        public static void ConfigureWarmupViewport(SubViewport viewport) { }
    }
}

namespace STS2Mobile.Launcher
{
    public static class Localization
    {
        public static string Tr(string key) => key;
    }

    public static class LauncherTheme
    {
        public static object Dim = new();
    }
}

namespace STS2Mobile.Launcher.Components
{
    public class ScreenBackground : Control { }

    public class StyledLabel : Label
    {
        public StyledLabel(string text, float scale, int fontSize) => Text = text;
    }

    public class StyledProgressBar : ProgressBar
    {
        public StyledProgressBar(float scale) { }
    }
}
