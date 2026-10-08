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
    private readonly Control _background;
    private readonly Control _frame;
    private readonly StyledLabel _status;
    private readonly VBoxContainer _resultRows;
    private readonly Control _resultsPage;
    private readonly Control _capturePage;
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
        var screen = new RenderBenchmarkScreen(owner, data);
        owner.GetTree().Root.AddChild(screen);
        owner.Hide();
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
        _scale = Math.Max(.65f, Math.Min(owner.Size.X / 960f, owner.Size.Y / 600f));
        _background = new ScreenBackground();
        AddChild(_background);
        AddChild(
            new ColorRect
            {
                Color = new Color(0, 0, 0, .55f),
                Size = owner.Size,
                MouseFilter = MouseFilterEnum.Stop,
            }
        );
        var frame = new PanelContainer
        {
            AnchorLeft = .04f,
            AnchorRight = .96f,
            AnchorTop = .04f,
            AnchorBottom = .96f,
        };
        StyleBox style = LauncherTheme.Panel(_scale);
        var texture = GameAssets.Load<Texture2D>(GameAssets.PopupPanel);
        if (texture != null)
        {
            var art = new StyleBoxTexture
            {
                Texture = texture,
                ModulateColor = LauncherTheme.PanelSlate,
            };
            art.SetTextureMarginAll(Math.Min(texture.GetWidth(), texture.GetHeight()) / 3f);
            style = art;
        }
        style.SetContentMarginAll(20 * _scale);
        frame.AddThemeStyleboxOverride("panel", style);
        AddChild(frame);
        _frame = frame;
        _panel = new VBoxContainer();
        _panel.AddThemeConstantOverride("separation", (int)(8 * _scale));
        frame.AddChild(_panel);
        var title = new StyledLabel(Tr("BENCH_TITLE"), _scale, 24, HorizontalAlignment.Left);
        title.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _panel.AddChild(title);
        var description = new StyledLabel(Tr("BENCH_INFO"), _scale, 13, HorizontalAlignment.Left)
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        description.AddThemeColorOverride("font_color", LauncherTheme.Dim);
        _panel.AddChild(description);
        _status = new StyledLabel("", _scale, 14, HorizontalAlignment.Left)
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _panel.AddChild(_status);
        var environment = new StyledLabel(
            $"{Tr("VERSION_LAUNCHER")} {_data?.App ?? STS2Mobile.Steam.AppUpdateChecker.GetInstalledVersion()} · Godot {_data?.Engine ?? Engine.GetVersionInfo()["string"].AsString()}",
            _scale,
            12,
            HorizontalAlignment.Left
        )
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        environment.AddThemeColorOverride("font_color", LauncherTheme.Dim);
        _panel.AddChild(environment);
        var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var resultsTab = new SettingsTabButton(Tr("BENCH_RESULTS_TAB"), _scale);
        var captureTab = new SettingsTabButton(Tr("BENCH_CAPTURE_TAB"), _scale);
        tabs.AddChild(resultsTab);
        tabs.AddChild(captureTab);
        _panel.AddChild(tabs);
        var body = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        _panel.AddChild(body);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        LauncherTheme.ApplyGameScrollbar(scroll, _scale);
        body.AddChild(scroll);
        _resultsPage = scroll;
        _resultRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resultRows.AddThemeConstantOverride("separation", (int)(12 * _scale));
        scroll.AddChild(_resultRows);
        var gallery = new VBoxContainer();
        gallery.SetAnchorsPreset(LayoutPreset.FullRect);
        body.AddChild(gallery);
        _capturePage = gallery;
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
        };
        gallery.AddChild(_capture);
        var arrows = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        gallery.AddChild(arrows);
        var previous = new GameMenuButton("◀", _scale, fontSize: 14);
        var next = new GameMenuButton("▶", _scale, fontSize: 14);
        arrows.AddChild(previous);
        arrows.AddChild(next);
        previous.Pressed += () => ChangeCapture(-1);
        next.Pressed += () => ChangeCapture(1);
        resultsTab.Pressed += () => SelectTab(false);
        captureTab.Pressed += () => SelectTab(true);
        SelectTab(false);
        void SelectTab(bool captures)
        {
            _resultsPage.Visible = !captures;
            _capturePage.Visible = captures;
            resultsTab.SetSelected(!captures);
            captureTab.SetSelected(captures);
        }
        _panel.AddChild(SettingsRow.Separator(_scale));
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", (int)(20 * _scale));
        _panel.AddChild(buttons);
        _start = Button(buttons, "BENCH_START", StartSuite);
        _start.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _cancel = Button(buttons, "BENCH_CANCEL", Cancel);
        Button(
            buttons,
            "BENCH_COPY",
            () =>
            {
                DisplayServer.ClipboardSet(_data?.Report() ?? Tr("BENCH_NO_RESULT"));
                _status.Text = Tr("BENCH_COPIED");
            }
        );
        _close = Button(buttons, "BENCH_CLOSE", Close);
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
                GameLibraryVersion = LibraryVersions.ReadLoaded("sts2")?.Version,
                RuntimeVersion = System.Environment.Version.ToString(),
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
            _frame.Visible = false;
            _background.Visible = false;
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
            MoveChild(_preview, _frame.GetIndex());
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
        _status.Text = _data?.Status switch
        {
            "Completed" => Tr("BENCH_COMPLETED"),
            "Running" => Tr("BENCH_MEASURE"),
            "Cancelled; completed cases preserved" => Tr("BENCH_CANCELLED"),
            "Interrupted; completed cases preserved" => Tr("BENCH_INTERRUPTED"),
            null or "Ready" => Tr("BENCH_READY"),
            var status => status,
        };
        AddResultText(Tr("BENCH_RESULTS_HELP"), 12, LauncherTheme.Dim);
        if (_data == null || _data.Results.Count == 0)
            AddResultText(Tr("BENCH_NO_RESULT"), 18, LauncherTheme.Cream);
        else
        {
            foreach (var result in _data.Results)
            {
                var metrics = result.Metrics;
                AddResultText(
                    $"{SceneName(result.Case)} · {result.Variant}",
                    17,
                    LauncherTheme.Gold
                );
                AddResultText(
                    $"FPS {metrics.Fps:F1}   ·   CPU {metrics.CpuMeanMs:F2} ms   ·   GPU {metrics.GpuMeanMs?.ToString("F2") ?? "N/A"} ms",
                    16,
                    LauncherTheme.Cream
                );
                AddResultText(
                    $"{Tr("BENCH_FRAME_TIMES")} {metrics.FrameMeanMs:F2} / {metrics.FrameP95Ms:F2} / {metrics.FrameP99Ms:F2} ms",
                    13,
                    LauncherTheme.Dim
                );
                _resultRows.AddChild(SettingsRow.Separator(_scale));
            }
        }
        ChangeCapture(0);
    }

    private void AddResultText(string text, int fontSize, Color color)
    {
        var label = new StyledLabel(text, _scale, fontSize, HorizontalAlignment.Left)
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeColorOverride("font_color", color);
        _resultRows.AddChild(label);
    }

    private static string SceneName(string scene) =>
        scene switch
        {
            "Cards" => Tr("BENCH_SCENE_CARDS"),
            "Geometry" => Tr("BENCH_SCENE_GEOMETRY"),
            "Effects" => Tr("BENCH_SCENE_EFFECTS"),
            _ => scene,
        };

    private void ChangeCapture(int direction)
    {
        var captures = _data
            ?.Results.Where(x => x.Screenshot != null && File.Exists(x.Screenshot))
            .ToArray();
        if (captures == null || captures.Length == 0)
        {
            _captureLabel.Text = Tr("BENCH_NO_CAPTURE");
            return;
        }
        _captureIndex = (_captureIndex + direction + captures.Length) % captures.Length;
        var result = captures[_captureIndex];
        _captureLabel.Text =
            $"{_captureIndex + 1}/{captures.Length} · {SceneName(result.Case)}: {result.Variant}";
        using var image = Image.LoadFromFile(result.Screenshot);
        _capture.Texture = ImageTexture.CreateFromImage(image);
    }

    private static string Thermal() =>
        LauncherModel.GetGodotApp()?.Call("getThermalStatus").AsString() ?? "N/A";

    private static string Tr(string key) => Localization.Tr(key);
}
