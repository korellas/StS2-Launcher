using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher;

// Compiles shaders on first launch by collecting materials from resources and scenes,
// rendering them in a SubViewport, then writing a version marker to skip on future launches.
public class ShaderWarmupScreen : Control
{
    private const int WarmupVersion = 6;
    private const int BatchSize = 64;

    private sealed record ScanProgress(string Detail, double Percent);

    private TaskCompletionSource<bool> _tcs;
    private float _scale;
    private Label _statusLabel;
    private Label _detailLabel;
    private ProgressBar _progressBar;

    public static bool NeedsWarmup()
    {
        try
        {
            var markerPath = Path.Combine(OS.GetUserDataDir(), "shader_warmup_version");
            if (File.Exists(markerPath))
            {
                var content = File.ReadAllText(markerPath).Trim();
                if (content == WarmupVersion.ToString())
                {
                    PatchHelper.Log(
                        $"[ShaderWarmup] NeedsWarmup=false (marker v{content} matches)"
                    );
                    return false;
                }
                PatchHelper.Log(
                    $"[ShaderWarmup] NeedsWarmup=true (marker v{content} != v{WarmupVersion})"
                );
            }
            else
            {
                PatchHelper.Log("[ShaderWarmup] NeedsWarmup=true (no marker file)");
            }

            return true;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] NeedsWarmup check failed: {ex.Message}");
            return true;
        }
    }

    public Task WaitForCompletion()
    {
        _tcs = new TaskCompletionSource<bool>();
        return _tcs.Task;
    }

    public void Initialize()
    {
        ZIndex = 100;

        try
        {
            var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            SetAnchorsPreset(LayoutPreset.FullRect);
            Size = vpSize;
            BuildUI();
            PatchHelper.Log("[ShaderWarmup] Screen initialized");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] BuildUI failed: {ex}");
            _tcs?.TrySetResult(false);
            return;
        }

        Callable.From(RunWarmup).CallDeferred();
    }

    private void BuildUI()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        _scale = Math.Max(vpSize.X, vpSize.Y) / 960f;

        var bg = new ScreenBackground();
        AddChild(bg);

        // No panel. The game's own menus put text straight onto the art, and the
        // half-width, near-full-height slab this used to use made three short
        // lines look like a dialog box with nothing in it.
        var column = new VBoxContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.62f,
            AnchorBottom = 0.62f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        column.AddThemeConstantOverride("separation", (int)(18 * _scale));
        AddChild(column);

        _statusLabel = new StyledLabel(
            Localization.Tr("STATUS_COMPILING_SHADERS"),
            _scale,
            fontSize: 26
        );
        column.AddChild(_statusLabel);

        // Sized here rather than in the component so the bar tracks the viewport
        // instead of stretching edge to edge on a foldable.
        var barWidth = (int)(vpSize.X * 0.42f);
        _progressBar = new StyledProgressBar(_scale);
        _progressBar.MinValue = 0;
        _progressBar.MaxValue = 100;
        _progressBar.Value = 0;
        _progressBar.ShowPercentage = false;
        _progressBar.CustomMinimumSize = new Vector2(barWidth, _progressBar.CustomMinimumSize.Y);
        _progressBar.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(_progressBar);

        _detailLabel = new StyledLabel(Localization.Tr("STATUS_ENUMERATING"), _scale, fontSize: 14);
        _detailLabel.Modulate = LauncherTheme.Dim;
        column.AddChild(_detailLabel);
    }

    private async void RunWarmup()
    {
        var sw = Stopwatch.StartNew();
        SubViewport viewport = null;

        try
        {
            _statusLabel.Text = Localization.Tr("STATUS_SCANNING_SHADERS");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var materials = await CollectMaterialsAsync();
            PatchHelper.Log($"[ShaderWarmup] Collected {materials.Count} materials to warm");

            _statusLabel.Text = Localization.Tr("STATUS_COMPILING_SHADERS");

            if (materials.Count == 0)
            {
                WriteVersionMarker();
                _tcs?.TrySetResult(true);
                return;
            }

            var compileTimer = Stopwatch.StartNew();
            viewport = new SubViewport();
            viewport.Size = new Vector2I(64, 64);
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            viewport.TransparentBg = true;
            STS2Mobile.Patches.GraphicsPatches.ConfigureWarmupViewport(viewport);
            AddChild(viewport);

            var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            whiteImage.SetPixel(0, 0, Colors.White);
            var whiteTex = ImageTexture.CreateFromImage(whiteImage);

            int processed = 0;
            int total = materials.Count;

            for (int i = 0; i < total; i += BatchSize)
            {
                var batchNodes = new List<Node>();
                bool hasParticles = false;
                int batchEnd = Math.Min(i + BatchSize, total);

                for (int j = i; j < batchEnd; j++)
                {
                    var (path, mat) = materials[j];
                    try
                    {
                        Node node = CreateWarmupNode(mat, whiteTex);
                        if (node != null)
                        {
                            viewport.AddChild(node);
                            batchNodes.Add(node);
                            hasParticles |= node is GpuParticles2D;
                        }
                    }
                    catch (Exception ex)
                    {
                        PatchHelper.Log(
                            $"[ShaderWarmup] Failed to create node for {path}: {ex.Message}"
                        );
                    }
                }

                // Keep the batch alive until it is drawn. Particles also need a
                // subsequent draw after their first simulation update.
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (hasParticles)
                    await ToSignal(
                        RenderingServer.Singleton,
                        RenderingServer.SignalName.FramePostDraw
                    );

                foreach (var node in batchNodes)
                    node.QueueFree();

                processed = batchEnd;
                _progressBar.Value = 50 + (double)processed / total * 50;
                _detailLabel.Text = $"Compiling {processed} / {total}";
            }

            _progressBar.Value = 100;
            _statusLabel.Text = Localization.Tr("STATUS_DONE");
            _detailLabel.Text = $"Warmed {total} materials in {sw.ElapsedMilliseconds}ms";
            PatchHelper.Log(
                $"[ShaderWarmup] Completed: {total} materials in {sw.ElapsedMilliseconds}ms (rendering {compileTimer.ElapsedMilliseconds}ms, batch size {BatchSize})"
            );

            WriteVersionMarker();

            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed: {ex}");
        }
        finally
        {
            viewport?.QueueFree();
        }

        _tcs?.TrySetResult(true);
    }

    private static Node CreateWarmupNode(Material mat, ImageTexture whiteTex)
    {
        if (
            mat is ParticleProcessMaterial
            || mat is ShaderMaterial { Shader: not null } sm
                && sm.Shader.GetMode() == Shader.Mode.Particles
        )
        {
            var particles = new GpuParticles2D();
            particles.ProcessMaterial = mat;
            particles.Amount = 1;
            particles.Emitting = true;
            particles.OneShot = false;
            particles.Texture = whiteTex;
            return particles;
        }

        var sprite = new Sprite2D();
        sprite.Texture = whiteTex;
        sprite.Material = mat;
        return sprite;
    }

    private async Task<List<(string path, Material mat)>> CollectMaterialsAsync()
    {
        var progress = new ScanProgress(Localization.Tr("STATUS_ENUMERATING"), 0);
        // A single loader avoids shared-resource parsing races. It can run
        // continuously while the main thread draws UI and Godot compiles shaders.
        var scan = Task.Run(() =>
            CollectMaterials(
                (detail, percent) => Volatile.Write(ref progress, new ScanProgress(detail, percent))
            )
        );
        while (!scan.IsCompleted)
        {
            var current = Volatile.Read(ref progress);
            _detailLabel.Text = current.Detail;
            _progressBar.Value = current.Percent;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var materials = await scan;
        _progressBar.Value = 50;
        return materials;
    }

    private static List<(string path, Material mat)> CollectMaterials(Action<string, double> report)
    {
        var sw = Stopwatch.StartNew();
        var resourcePaths = new List<string>();
        var scenePaths = new List<string>();
        CollectResourcePaths("res://", resourcePaths, scenePaths, new HashSet<string>());
        PatchHelper.Log(
            $"[ShaderWarmup] Found {resourcePaths.Count} resource files and {scenePaths.Count} scenes in {sw.ElapsedMilliseconds}ms"
        );

        var materials = new Dictionary<string, (string path, Material mat)>();
        int total = resourcePaths.Count + scenePaths.Count;
        int processed = 0;
        sw.Restart();
        foreach (var path in resourcePaths)
        {
            try
            {
                // A type hint does not filter .tres resources. Load once, then
                // inspect the result instead of loading non-materials twice.
                var resource = ResourceLoader.Load(path, null, ResourceLoader.CacheMode.Reuse);
                if (resource is Material mat)
                    materials.TryAdd(GetShaderKey(mat), (path, mat));
                else if (resource is Shader shader)
                {
                    var key = GetResourceKey(shader);
                    if (!materials.ContainsKey(key))
                        materials.Add(key, (path, new ShaderMaterial { Shader = shader }));
                }
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[ShaderWarmup] Failed to load {path}: {ex.Message}");
            }

            processed++;
            report(
                $"Scanning resources... {processed} / {resourcePaths.Count}",
                (double)processed / total * 50
            );
        }
        PatchHelper.Log(
            $"[ShaderWarmup] Resource scan: {materials.Count} unique materials in {sw.ElapsedMilliseconds}ms"
        );

        sw.Restart();
        for (int i = 0; i < scenePaths.Count; i++)
        {
            try
            {
                var packed = ResourceLoader.Load<PackedScene>(
                    scenePaths[i],
                    null,
                    ResourceLoader.CacheMode.Reuse
                );
                if (packed != null)
                    ExtractMaterialsFromSceneState(packed, scenePaths[i], materials);
            }
            catch (Exception ex)
            {
                PatchHelper.Log(
                    $"[ShaderWarmup] Failed to extract from {scenePaths[i]}: {ex.Message}"
                );
            }

            processed++;
            report(
                $"Scanning scenes... {i + 1} / {scenePaths.Count}",
                (double)processed / total * 50
            );
        }

        PatchHelper.Log(
            $"[ShaderWarmup] Scene scan: {materials.Count} unique materials in {sw.ElapsedMilliseconds}ms"
        );
        return materials.Values.ToList();
    }

    private static string GetResourceKey(Resource resource) =>
        string.IsNullOrEmpty(resource.ResourcePath)
            ? $"instance#{resource.GetInstanceId()}"
            : resource.ResourcePath;

    private static string GetShaderKey(Material mat)
    {
        if (mat is ShaderMaterial { Shader: not null } sm)
            return GetResourceKey(sm.Shader);
        if (mat is ParticleProcessMaterial)
            return $"particle#{mat.GetInstanceId()}";
        return GetResourceKey(mat);
    }

    private static void CollectResourcePaths(
        string dirPath,
        List<string> resourcePaths,
        List<string> scenePaths,
        HashSet<string> visited
    )
    {
        try
        {
            using var dir = DirAccess.Open(dirPath);
            if (dir == null)
                return;

            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (fileName == "." || fileName == "..")
                    continue;

                var fullPath = dirPath + (dirPath.EndsWith('/') ? "" : "/") + fileName;
                if (dir.CurrentIsDir())
                {
                    if (fileName != "debug")
                        CollectResourcePaths(fullPath, resourcePaths, scenePaths, visited);
                    continue;
                }

                var cleanPath = fullPath.EndsWith(".remap", StringComparison.Ordinal)
                    ? fullPath[..^6]
                    : fullPath;
                bool isScene =
                    cleanPath.StartsWith("res://scenes/", StringComparison.Ordinal)
                    && cleanPath.EndsWith(".tscn", StringComparison.Ordinal);
                bool isResource =
                    cleanPath.EndsWith(".tres", StringComparison.Ordinal)
                    || cleanPath.EndsWith(".gdshader", StringComparison.Ordinal)
                    || cleanPath.EndsWith(".material", StringComparison.Ordinal);
                if ((!isScene && !isResource) || !visited.Add(cleanPath))
                    continue;
                if (!ResourceLoader.Exists(cleanPath))
                    continue;

                if (isScene)
                    scenePaths.Add(cleanPath);
                else
                    resourcePaths.Add(cleanPath);
            }
            dir.ListDirEnd();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed to enumerate {dirPath}: {ex.Message}");
        }
    }

    private static void ExtractMaterialsFromSceneState(
        PackedScene packed,
        string scenePath,
        Dictionary<string, (string path, Material mat)> materials
    )
    {
        var state = packed.GetState();
        int nodeCount = state.GetNodeCount();

        for (int n = 0; n < nodeCount; n++)
        {
            int propCount = state.GetNodePropertyCount(n);
            for (int p = 0; p < propCount; p++)
            {
                var propName = state.GetNodePropertyName(n, p).ToString();
                if (
                    propName != "material"
                    && propName != "process_material"
                    && propName != "surface_material_override/0"
                )
                    continue;

                try
                {
                    var val = state.GetNodePropertyValue(n, p);
                    if (val.Obj is Material mat)
                    {
                        materials.TryAdd(GetShaderKey(mat), (scenePath, mat));
                    }
                    else if (val.Obj is Shader shader)
                    {
                        var key = GetResourceKey(shader);
                        if (!materials.ContainsKey(key))
                            materials.Add(key, (scenePath, new ShaderMaterial { Shader = shader }));
                    }
                }
                catch (Exception ex)
                {
                    PatchHelper.Log(
                        $"[ShaderWarmup] Failed to read property {propName} in {scenePath}: {ex.Message}"
                    );
                }
            }
        }
    }

    private static void WriteVersionMarker()
    {
        try
        {
            var markerPath = Path.Combine(OS.GetUserDataDir(), "shader_warmup_version");
            File.WriteAllText(markerPath, WarmupVersion.ToString());
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed to write version marker: {ex.Message}");
        }
    }
}
