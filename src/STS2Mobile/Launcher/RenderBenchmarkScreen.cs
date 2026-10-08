using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher;

public sealed class RenderBenchmarkScreen : Control
{
    private const double WarmupSeconds = 2;
    private const double SampleSeconds = 5;
    private static string DataPath => Path.Combine(OS.GetDataDir(), "render-benchmark.json");
    private static string CaptureDirectory =>
        Path.Combine(OS.GetDataDir(), "render-benchmark-captures");
    private readonly LauncherUI _owner;
    private readonly float _scale;
    private readonly VBoxContainer _panel;
    private readonly StyledLabel _status;
    private readonly TextEdit _report;
    private readonly GameMenuButton _start;
    private readonly GameMenuButton _cancel;
    private readonly GameMenuButton _close;
    private readonly TextureRect _capture;
    private readonly StyledLabel _captureLabel;
    private RenderBenchmarkData _data;
    private SubViewport _viewport;
    private TextureRect _preview;
    private RenderBenchmarkFixture _fixture;
    private bool _running;
    private readonly GodotObject _app;
    private int _pauseCount;
    private bool _cancelled;
    private bool _restarting;
    private int _savedFps;
    private bool _savedKeepOn;
    private DisplayServer.VSyncMode _savedVSync;
    private int _captureIndex;

    public static bool RecoverBoot(LauncherUI owner)
    {
        var app = LauncherModel.GetGodotApp();
        int mode = app?.Call("getBenchmarkPacing").AsInt32() ?? -99;
        if (app != null && !app.Call("restoreBenchmarkConfig").AsBool())
            throw new IOException("Could not restore benchmark boot configuration");
        var data = LoadData();
        if (data == null)
        {
            if (mode != -99)
            {
                app.Call("restartApp");
                return true;
            }
            return false;
        }
        if (data.CanResume(mode))
        {
            Open(owner, data, resume: true);
            return true;
        }
        if (data.Running)
        {
            data.Running = false;
            data.Status = "Interrupted; completed cases preserved";
            data.ShowResults = true;
            data.Save(DataPath);
        }
        if (mode != -99)
        {
            app.Call("restartApp");
            return true;
        }
        if (data.ShowResults)
        {
            data.ShowResults = false;
            data.Save(DataPath);
            Open(owner, data);
        }
        return false;
    }

    public static void Open(LauncherUI owner) => Open(owner, LoadData());

    private static RenderBenchmarkData LoadData()
    {
        try
        {
            return RenderBenchmarkData.Load(DataPath);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Benchmark] Cannot read saved results: {ex.Message}");
            return null;
        }
    }

    private static void Open(LauncherUI owner, RenderBenchmarkData data, bool resume = false)
    {
        owner.Hide();
        var screen = new RenderBenchmarkScreen(owner, data);
        owner.GetTree().Root.AddChild(screen);
        var window = screen.GetTree().Root;
        window.FocusExited += screen.OnFocusExited;
        screen.TreeExiting += () =>
        {
            window.FocusExited -= screen.OnFocusExited;
        };
        if (resume)
            Callable.From(screen.RunPhase).CallDeferred();
    }

    private RenderBenchmarkScreen(LauncherUI owner, RenderBenchmarkData data)
    {
        _owner = owner;
        _app = LauncherModel.GetGodotApp();
        _data = data;
        ZIndex = 200;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _scale = Math.Max(owner.Size.X, owner.Size.Y) / 960f;
        AddChild(
            new ColorRect
            {
                Color = new Color(.025f, .03f, .055f),
                Size = owner.Size,
                MouseFilter = MouseFilterEnum.Stop,
            }
        );
        _panel = new VBoxContainer { Position = owner.Size * .04f, Size = owner.Size * .92f };
        _panel.AddThemeConstantOverride("separation", (int)(8 * _scale));
        AddChild(_panel);
        _panel.AddChild(new StyledLabel(Tr("BENCH_TITLE"), _scale, 22, HorizontalAlignment.Left));
        var description = new StyledLabel(Tr("BENCH_INFO"), _scale, 13, HorizontalAlignment.Left)
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _panel.AddChild(description);
        _status = new StyledLabel("", _scale, 14, HorizontalAlignment.Left)
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _panel.AddChild(_status);
        var buttons = new HBoxContainer();
        _panel.AddChild(buttons);
        _start = Button(buttons, "BENCH_START", StartSuite);
        _cancel = Button(buttons, "BENCH_CANCEL", Cancel);
        _close = Button(buttons, "BENCH_CLOSE", Close);
        Button(
            buttons,
            "BENCH_COPY",
            () =>
            {
                DisplayServer.ClipboardSet(_data?.Report() ?? Tr("BENCH_NO_RESULT"));
                _status.Text = Tr("BENCH_COPIED");
            }
        );
        var results = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _panel.AddChild(results);
        _report = new TextEdit
        {
            Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(owner.Size.X * .5f, 0),
        };
        _report.AddThemeFontSizeOverride("font_size", (int)(12 * _scale));
        results.AddChild(_report);
        var gallery = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        results.AddChild(gallery);
        _captureLabel = new StyledLabel("", _scale, 12)
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        gallery.AddChild(_captureLabel);
        _capture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(owner.Size.X * .3f, 0),
        };
        gallery.AddChild(_capture);
        var arrows = new HBoxContainer();
        gallery.AddChild(arrows);
        var previous = new GameMenuButton("◀", _scale, fontSize: 14);
        var next = new GameMenuButton("▶", _scale, fontSize: 14);
        arrows.AddChild(previous);
        arrows.AddChild(next);
        previous.Pressed += () => ChangeCapture(-1);
        next.Pressed += () => ChangeCapture(1);
        _cancel.Visible = false;
        ShowResults();
    }

    private GameMenuButton Button(HBoxContainer parent, string key, Action action)
    {
        var button = new GameMenuButton(Tr(key), _scale, fontSize: 14);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void StartSuite()
    {
        try
        {
            // Validate before starting a chain of restarts, without instantiating game scripts.
            _fixture = new RenderBenchmarkFixture();
            if (LauncherModel.GetGodotApp() == null)
                throw new InvalidOperationException("Android benchmark entry unavailable");
            if (RenderingServer.GetCurrentRenderingDriverName() != "vulkan")
                throw new InvalidOperationException("Frame-pacing comparison requires Vulkan");
            var size = DisplayServer.WindowGetSize();
            _data = new RenderBenchmarkData
            {
                Running = true,
                Device = OS.GetModelName(),
                Engine = Godot.Engine.GetVersionInfo()["string"].AsString(),
                App = LauncherModel.GetGodotApp().Call("getVersionName").AsString(),
                GameAssembly = typeof(NGame).Assembly.ManifestModule.ModuleVersionId.ToString(),
                Renderer =
                    $"{RenderingServer.GetCurrentRenderingMethod()} / {RenderingServer.GetCurrentRenderingDriverName()} / {RenderingServer.GetVideoAdapterName()}",
                Resolution = $"{size.X}x{size.Y}",
                Status = "Running",
            };
            _data.Save(DataPath);
            ScheduleRestart();
        }
        catch (Exception ex)
        {
            _status.Text = Tr("BENCH_ERROR") + " " + ex.Message;
            _restarting = false;
        }
    }

    private void ScheduleRestart()
    {
        _restarting = true;
        _start.Disabled = true;
        _close.Disabled = true;
        _cancel.Visible = false;
        _status.Text = Tr("BENCH_RESTART");
        LauncherModel
            .GetGodotApp()
            .Call("restartForBenchmark", RenderBenchmarkData.PacingModes[_data.Phase]);
    }

    private async void RunPhase()
    {
        _running = true;
        _savedFps = Godot.Engine.MaxFps;
        _savedKeepOn = DisplayServer.ScreenIsKeptOn();
        _savedVSync = DisplayServer.WindowGetVsyncMode();
        try
        {
            _pauseCount = _app.Call("getActivityPauseCount").AsInt32();
            CheckForeground();
            var physical = DisplayServer.WindowGetSize();
            if (_data.Resolution != $"{physical.X}x{physical.Y}")
                throw new InvalidOperationException("Screen size changed during benchmark");
            bool enabled = ProjectSettings
                .GetSettingWithOverride("display/window/frame_pacing/android/enable_frame_pacing")
                .AsBool();
            int mode = ProjectSettings
                .GetSettingWithOverride("display/window/frame_pacing/android/swappy_mode")
                .AsInt32();
            int expected = RenderBenchmarkData.PacingModes[_data.Phase];
            if (enabled != (expected != -2) || (enabled && mode != expected))
                throw new InvalidOperationException(
                    $"Native pacing not applied: enabled={enabled}, mode={mode}"
                );
            _fixture ??= new RenderBenchmarkFixture();
            DisplayServer.ScreenSetKeepOn(true);
            _panel.Visible = false;
            _cancel.Visible = true;
            // Progress is the only launcher control drawn during sampling.
            _panel.RemoveChild(_status);
            _cancel.GetParent().RemoveChild(_cancel);
            _status.Position = new Vector2(20 * _scale, 15 * _scale);
            _cancel.Position = new Vector2(20 * _scale, 45 * _scale);
            AddChild(_status);
            AddChild(_cancel);
            _viewport = new SubViewport
            {
                Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            AddChild(_viewport);
            _preview = new TextureRect
            {
                Texture = _viewport.GetTexture(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _preview.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_preview);
            MoveChild(_preview, 1);
            RenderingServer.ViewportSetMeasureRenderTime(_viewport.GetViewportRid(), true);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            var tests = new List<RenderBenchmarkCase>();
            if (_data.Phase == 0)
                tests.AddRange(RenderBenchmarkCase.QualityCases());
            foreach (int fps in LauncherModel.FrameLimitOptions)
                tests.Add(
                    new RenderBenchmarkCase("Effects", $"pacing {expected}, FPS {fps}")
                    {
                        Fps = fps,
                    }
                );
            for (int i = 0; i < tests.Count; i++)
            {
                CheckCancellation();
                var test = tests[i];
                Godot.Engine.MaxFps = test.Fps;
                _fixture.Build(_viewport, physical, test);
                _status.Text =
                    $"{Tr("BENCH_WARM")} {_data.Phase + 1}/{RenderBenchmarkData.PacingModes.Length} · {i + 1}/{tests.Count} · {test.Scene}: {test.Name}";
                await Sample(WarmupSeconds, record: false);
                // Capture a common animation pose, outside the timed window.
                _fixture.Animate(0);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Directory.CreateDirectory(CaptureDirectory);
                string screenshot = Path.Combine(
                    CaptureDirectory,
                    $"case-{_data.Results.Count:D3}.png"
                );
                using (var image = GetViewport().GetTexture().GetImage())
                {
                    if (image.GetFormat() != Image.Format.Rgba8)
                        image.Convert(Image.Format.Rgba8);
                    if (image.SavePng(screenshot) != Error.Ok)
                        screenshot = null;
                }
                await Sample(.5, record: false);
                _status.Text =
                    $"{Tr("BENCH_MEASURE")} {_data.Phase + 1}/{RenderBenchmarkData.PacingModes.Length} · {i + 1}/{tests.Count} · {test.Scene}: {test.Name}";
                string thermalStart = Thermal();
                var metrics = await Sample(SampleSeconds, record: true);
                CheckCancellation();
                if (metrics.Frames == 0)
                    throw new InvalidOperationException("No rendered frames measured");
                _data.Results.Add(
                    new BenchmarkResult
                    {
                        Case = test.Scene,
                        Variant = test.Name,
                        Applied =
                            $"size={_viewport.Size}, HDR={_viewport.UseHdr2D}, MSAA={_viewport.Msaa2D}, filter={test.Filter}, direct={test.Direct}, blur={test.Blur}, distortion={test.Distortion}, particles={test.Particles}%, cap={Godot.Engine.MaxFps}, nativePacing={expected}",
                        Metrics = metrics,
                        ThermalStart = thermalStart,
                        ThermalEnd = Thermal(),
                        Screenshot = screenshot,
                    }
                );
                _data.Save(DataPath);
            }
            _data.Phase++;
            if (_data.Phase >= RenderBenchmarkData.PacingModes.Length)
            {
                _data.Running = false;
                _data.ShowResults = true;
                _data.Status = "Completed";
            }
            _data.Save(DataPath);
        }
        catch (Exception ex)
        {
            _data.Running = false;
            _data.ShowResults = true;
            _data.Status =
                ex is OperationCanceledException
                    ? "Cancelled; completed cases preserved"
                    : $"Failed: {ex.Message}";
            try
            {
                _data.Save(DataPath);
            }
            catch (Exception saveError)
            {
                PatchHelper.Log($"[Benchmark] Saving failure: {saveError}");
            }
            PatchHelper.Log($"[Benchmark] {_data.Status}");
        }
        finally
        {
            if (_viewport != null)
            {
                RenderingServer.ViewportSetMeasureRenderTime(_viewport.GetViewportRid(), false);
                _preview.QueueFree();
                _viewport.QueueFree();
            }
            Godot.Engine.MaxFps = _savedFps;
            DisplayServer.ScreenSetKeepOn(_savedKeepOn);
            DisplayServer.WindowSetVsyncMode(_savedVSync);
            _running = false;
        }
        // Return through a cold boot so the real Vulkan device also uses the user's pacing.
        // A cancelled background test must not bring the app back to the foreground.
        while (!_app.Call("isActivityResumed").AsBool())
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_data.Running)
            ScheduleRestart();
        else
        {
            _restarting = true;
            LauncherModel.GetGodotApp().Call("restartApp");
        }
    }

    private async Task<BenchmarkMetrics> Sample(double seconds, bool record)
    {
        CheckForeground();
        var samples = new BenchmarkSamples();
        ulong start = Time.GetTicksUsec();
        ulong last = start;
        while ((Time.GetTicksUsec() - start) / 1_000_000d < seconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckCancellation();
            _fixture.Animate((Time.GetTicksUsec() - start) / 1_000_000d);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            CheckCancellation();
            ulong now = Time.GetTicksUsec();
            if (record)
            {
                var rid = _viewport.GetViewportRid();
                samples.Add(
                    (now - last) / 1000d,
                    RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid),
                    RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid),
                    RenderingServer.ViewportGetRenderInfo(
                        rid,
                        RenderingServer.ViewportRenderInfoType.Canvas,
                        RenderingServer.ViewportRenderInfo.DrawCallsInFrame
                    )
                );
            }
            last = now;
        }
        CheckForeground();
        return samples.Summarize();
    }

    private void CheckCancellation()
    {
        if (_cancelled || GetTree().Paused)
            throw new OperationCanceledException();
    }

    private void CheckForeground()
    {
        // Android's DisplayServer focus getter is unconditional. The Activity epoch
        // also catches a pause/resume while Godot stops producing frame signals.
        if (
            !_app.Call("isActivityResumed").AsBool()
            || _app.Call("getActivityPauseCount").AsInt32() != _pauseCount
        )
            throw new OperationCanceledException("Activity paused during benchmark");
        if (
            _data.Resolution
            != $"{DisplayServer.WindowGetSize().X}x{DisplayServer.WindowGetSize().Y}"
        )
            throw new InvalidOperationException("Screen size changed during benchmark");
    }

    private void OnFocusExited()
    {
        if (_running && !_restarting)
            Cancel();
    }

    private void Cancel()
    {
        _cancelled = true;
        _status.Text = Tr("BENCH_CANCEL");
    }

    private void Close()
    {
        if (_running || _restarting)
            return;
        _owner.Show();
        QueueFree();
    }

    private void ShowResults()
    {
        _report.Text = _data?.Report() ?? Tr("BENCH_NO_RESULT");
        _status.Text = _data?.Status ?? "";
        ChangeCapture(0);
    }

    private void ChangeCapture(int direction)
    {
        var captures = _data
            ?.Results.Where(x => x.Screenshot != null && File.Exists(x.Screenshot))
            .ToArray();
        if (captures == null || captures.Length == 0)
            return;
        _captureIndex = (_captureIndex + direction + captures.Length) % captures.Length;
        var result = captures[_captureIndex];
        _captureLabel.Text =
            $"{_captureIndex + 1}/{captures.Length} · {result.Case}: {result.Variant}";
        using var image = Image.LoadFromFile(result.Screenshot);
        _capture.Texture = ImageTexture.CreateFromImage(image);
    }

    private static string Thermal() =>
        LauncherModel.GetGodotApp()?.Call("getThermalStatus").AsString() ?? "N/A";

    private static string Tr(string key) => Localization.Tr(key);
}
