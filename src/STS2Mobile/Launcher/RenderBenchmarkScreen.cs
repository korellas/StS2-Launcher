using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Patches;

namespace STS2Mobile.Launcher;

public sealed class RenderBenchmarkScreen : Control
{
    private const double WarmupSeconds = 2;
    private const double SampleSeconds = 6;
    private static string BenchmarkDataPath =>
        Path.Combine(OS.GetDataDir(), "render-benchmark.json");
    private static string ComparisonDataPath =>
        Path.Combine(OS.GetDataDir(), "render-comparison.json");
    private static string EngineDataPath => Path.Combine(OS.GetDataDir(), "engine-benchmark.json");
    private readonly bool _comparison;
    private readonly bool _engine;

    private static string DataPathFor(RenderBenchmarkData data) =>
        data.EngineOnly ? EngineDataPath
        : data.CaptureOnly ? ComparisonDataPath
        : BenchmarkDataPath;

    private string DataPath =>
        _engine ? EngineDataPath
        : _comparison ? ComparisonDataPath
        : BenchmarkDataPath;
    private string CaptureDirectory =>
        Path.Combine(
            OS.GetDataDir(),
            _comparison ? "render-comparison-captures" : "render-benchmark-captures"
        );
    private readonly LauncherUI _owner;
    private readonly float _scale;
    private readonly VBoxContainer _panel;
    private readonly Control _background;
    private readonly Control _frame;
    private readonly ColorRect _scrim;
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
    private Viewport _viewport;
    private GraphicsSettings _savedGraphics;
    private RenderBenchmarkFixture _fixture;
    private BenchmarkTrace _trace;
    private bool _running;
    private readonly GodotObject _app;
    private int _pauseCount;
    private bool _cancelled;
    private bool _restarting;
    private bool _openingComparison;
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
        var engine = LoadData(EngineDataPath);
        var comparison = LoadData(ComparisonDataPath);
        var data =
            engine != null && (engine.Running || engine.ShowResults) ? engine
            : comparison != null && (comparison.Running || comparison.ShowResults) ? comparison
            : LoadData(BenchmarkDataPath);
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
            Open(owner, data, resume: true, comparison: data.CaptureOnly, engine: data.EngineOnly);
            return true;
        }
        if (data.Running)
        {
            data.Running = false;
            data.Status = "Interrupted; completed cases preserved";
            data.ShowResults = true;
            data.Save(DataPathFor(data));
        }
        if (mode != -99)
        {
            app.Call("restartApp");
            return true;
        }
        if (data.ShowResults)
        {
            data.ShowResults = false;
            data.Save(DataPathFor(data));
            Open(owner, data, comparison: data.CaptureOnly, engine: data.EngineOnly);
        }
        return false;
    }

    public static void Open(LauncherUI owner) => Open(owner, LoadData(BenchmarkDataPath));

    public static void OpenComparison(LauncherUI owner) =>
        Open(owner, LoadData(ComparisonDataPath), comparison: true);

    public static void OpenEngine(LauncherUI owner) =>
        Open(owner, LoadData(EngineDataPath), engine: true);

    private static RenderBenchmarkData LoadData(string path)
    {
        try
        {
            return RenderBenchmarkData.Load(path);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Benchmark] Cannot read saved results: {ex.Message}");
            return null;
        }
    }

    private static void Open(
        LauncherUI owner,
        RenderBenchmarkData data,
        bool resume = false,
        bool comparison = false,
        bool engine = false
    )
    {
        var screen = new RenderBenchmarkScreen(owner, data, comparison, engine);
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
        else if (comparison && data?.Status == "Completed")
            Callable.From(screen.OpenComparisonPage).CallDeferred();
    }

    private RenderBenchmarkScreen(
        LauncherUI owner,
        RenderBenchmarkData data,
        bool comparison,
        bool engine
    )
    {
        _comparison = comparison;
        _engine = engine;
        _owner = owner;
        _app = LauncherModel.GetGodotApp();
        _data = data;
        ZIndex = 200;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _scale = Math.Max(.65f, Math.Min(owner.Size.X / 960f, owner.Size.Y / 600f));
        _background = new ScreenBackground();
        AddChild(_background);
        _scrim = new ColorRect
        {
            Color = new Color(0, 0, 0, .55f),
            Size = owner.Size,
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(_scrim);
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
        var title = new StyledLabel(
            Tr(
                _engine ? "ENGINE_BENCH_TITLE"
                : _comparison ? "COMPARE_TITLE"
                : "BENCH_TITLE"
            ),
            _scale,
            24,
            HorizontalAlignment.Left
        );
        title.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _panel.AddChild(title);
        var description = new StyledLabel(
            Tr(
                _engine ? "ENGINE_BENCH_INFO"
                : _comparison ? "COMPARE_INFO"
                : "BENCH_INFO"
            ),
            _scale,
            13,
            HorizontalAlignment.Left
        )
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
        tabs.Visible = !_engine;
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
        _start = Button(
            buttons,
            _engine ? "ENGINE_BENCH_START"
                : _comparison ? "COMPARE_START"
                : "BENCH_START",
            StartSuite
        );
        _start.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _cancel = Button(buttons, "BENCH_CANCEL", Cancel);
        if (_comparison)
            Button(buttons, "COMPARE_OPEN", OpenComparisonPage);
        if (!_comparison)
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
        if (_openingComparison)
            return;
        try
        {
            RenderBenchmarkFixture.ValidateEntry();
            if (LauncherModel.GetGodotApp() == null)
                throw new InvalidOperationException("Android benchmark entry unavailable");
            if (RenderingServer.GetCurrentRenderingDriverName() != "vulkan")
                throw new InvalidOperationException("Frame-pacing comparison requires Vulkan");
            var size = DisplayServer.WindowGetSize();
            _data = new RenderBenchmarkData
            {
                Running = true,
                CaptureOnly = _comparison,
                EngineOnly = _engine,
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
        _savedGraphics = GraphicsPatches.Settings.Copy();
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
            DisplayServer.ScreenSetKeepOn(true);
            _frame.Visible = false;
            _background.Visible = false;
            _scrim.Color = Colors.Transparent;
            _cancel.Visible = true;
            // Move progress outside the panel; engine sampling hides these controls too.
            _panel.RemoveChild(_status);
            _cancel.GetParent().RemoveChild(_cancel);
            _status.Position = new Vector2(20 * _scale, 15 * _scale);
            _cancel.Position = new Vector2(20 * _scale, 45 * _scale);
            AddChild(_status);
            AddChild(_cancel);
            _status.Text = Tr("BENCH_LOAD_GAME");
            ulong initializationStart = Time.GetTicksUsec();
#if BENCHMARK_DEATH_DIAGNOSTICS
            _trace = BenchmarkTrace.TryStart(_app, GetTree(), recordDeath: _engine);
#else
            _trace = _engine ? null : BenchmarkTrace.TryStart(_app, GetTree());
#endif
            if (_engine)
                Godot.Engine.MaxFps = 0;
            _fixture = new RenderBenchmarkFixture(CheckTestState, _trace);
            await _fixture.Initialize();
            double initializationMs = (Time.GetTicksUsec() - initializationStart) / 1000d;
            CheckForeground();
            _viewport = GetTree().Root;
            RenderingServer.ViewportSetMeasureRenderTime(_viewport.GetViewportRid(), true);
            var benchmarkVsync = _engine
                ? DisplayServer.VSyncMode.Mailbox
                : DisplayServer.VSyncMode.Disabled;
            DisplayServer.WindowSetVsyncMode(benchmarkVsync);
            var tests = new List<RenderBenchmarkCase>();
            if (_engine)
                tests.AddRange(RenderBenchmarkCase.EngineCases());
            else if (_comparison)
                tests.AddRange(RenderBenchmarkCase.ComparisonCases());
            else if (_data.Phase == 0)
                tests.AddRange(RenderBenchmarkCase.QualityCases());
            if (!_comparison && !_engine)
                tests.AddRange(
                    RenderBenchmarkCase.PacingCases(expected, RenderBenchmarkCase.FrameLimits)
                );
            for (int i = 0; i < tests.Count; i++)
            {
                CheckCancellation();
                var test = tests[i];
                Godot.Engine.MaxFps = test.Fps;
                _status.Text = $"{Tr("BENCH_LOAD_GAME")} · {SceneName(test.Scene)}";
                await _fixture.Build(test);
                // Game scene initialization can reapply its display preferences.
                DisplayServer.WindowSetVsyncMode(benchmarkVsync);
                Godot.Engine.MaxFps = test.Fps;
                CheckCancellation();
                CheckForeground();
                if (i == 0)
                {
                    _data.Boots.Add(
                        new BenchmarkBootTiming
                        {
                            Phase = _data.Phase,
                            GameInitializationMs = initializationMs,
                            LaunchToSceneMs = _app.Call("getProcessElapsedMs").AsInt64(),
                        }
                    );
                    SaveData();
                }
                _status.Text =
                    $"{Tr("BENCH_WARM")} {_data.Phase + 1}/{_data.PhaseCount} · {i + 1}/{tests.Count} · {test.Scene}: {test.Name}";
                await Sample(WarmupSeconds, record: false);
                string screenshot = null;
                if (!_engine)
                {
                    if (_comparison)
                    {
                        _fixture.Animate();
                        await Sample(.1, record: false);
                    }
                    else
                        // Capture during the game's beam sequence, outside the timed window.
                        await Sample(.75, record: false);
                    await ToSignal(
                        RenderingServer.Singleton,
                        RenderingServer.SignalName.FramePostDraw
                    );
                    Directory.CreateDirectory(CaptureDirectory);
                    screenshot = Path.Combine(
                        CaptureDirectory,
                        $"case-{_data.Results.Count:D3}.png"
                    );
                    _status.Visible = false;
                    _cancel.Visible = false;
                    try
                    {
                        await ToSignal(
                            RenderingServer.Singleton,
                            RenderingServer.SignalName.FramePostDraw
                        );
                        using var captureTrace = _trace?.Sync("Capture.ReadbackAndPng");
                        using var image = GetViewport().GetTexture().GetImage();
                        if (image.GetFormat() != Image.Format.Rgba8)
                            image.Convert(Image.Format.Rgba8);
                        if (_viewport.UseHdr2D)
                            image.LinearToSrgb();
                        if (_comparison && image.GetSize() != physical)
                            // Match RendererCompositorRD's nearest-sampled screen blit.
                            image.Resize(physical.X, physical.Y, Image.Interpolation.Nearest);
                        if (image.SavePng(screenshot) != Error.Ok)
                            throw new IOException("Could not save comparison screenshot");
                    }
                    finally
                    {
                        _status.Visible = true;
                        _cancel.Visible = true;
                    }
                    if (!_comparison)
                        await Sample(.5, record: false);
                }
                _status.Text =
                    $"{Tr(_comparison ? "COMPARE_CAPTURE" : "BENCH_MEASURE")} {_data.Phase + 1}/{_data.PhaseCount} · {i + 1}/{tests.Count} · {test.Scene}: {test.Name}";
                string thermalStart = Thermal();
                if (_engine)
                    _status.Visible = _cancel.Visible = false;
                BenchmarkMetrics metrics;
                if (_comparison)
                    metrics = new BenchmarkMetrics();
                else if (_engine || test.Scene == "Transitions")
                {
                    _trace?.BeginDeathSample();
                    var route = _engine ? _fixture.RunCombatTurns() : _fixture.RunTransitions();
                    try
                    {
                        metrics = await Sample(0, record: true, until: route);
                    }
                    finally
                    {
                        await route;
                    }
                }
                else
                    metrics = await Sample(SampleSeconds, record: true);
                if (_engine)
                    _status.Visible = _cancel.Visible = true;
                CheckCancellation();
                if (!_comparison && metrics.Frames == 0)
                    throw new InvalidOperationException("No rendered frames measured");
                _data.Results.Add(
                    new BenchmarkResult
                    {
                        Case = test.Scene,
                        Variant = test.Name,
                        Applied = FormattableString.Invariant(
                            $"root viewport, scale={GraphicsPatches.Settings.RenderScale}%, HDR={_viewport.UseHdr2D}, MSAA={_viewport.Msaa2D}, filter={GraphicsPatches.Settings.TextureFilter}, direct={GraphicsPatches.Settings.DirectCardPortraits}, blur={GraphicsPatches.Settings.RadialBlurSamples}, distortion={GraphicsPatches.Settings.ScreenDistortion}, backgroundParticles={GraphicsPatches.Settings.BackgroundParticles}%, cap={Godot.Engine.MaxFps}, nativePacing={expected}, vsyncRequested={benchmarkVsync}, vsyncActual={DisplayServer.WindowGetVsyncMode()}, refreshHz={DisplayServer.ScreenGetRefreshRate():F2}"
                        ),
                        Metrics = metrics,
                        Sequence = _engine ? _fixture.SequenceSummary : null,
#if BENCHMARK_DEATH_DIAGNOSTICS
                        Diagnostics =
                            _trace?.DeathReport()
                            ?? (
                                _engine
                                    ? "Death diagnostics unavailable: hook setup failed; see launcher log."
                                    : null
                            ),
#else
                        Diagnostics = _trace?.DeathReport(),
#endif
                        ThermalStart = thermalStart,
                        ThermalEnd = Thermal(),
                        Screenshot = screenshot,
                        LoadTimings = _fixture.LoadTimings.ToList(),
                    }
                );
                SaveData();
            }
            _data.Phase++;
            if (_data.Phase >= _data.PhaseCount)
            {
                _data.Running = false;
                _data.ShowResults = true;
                _data.Status = "Completed";
            }
            SaveData();
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
                SaveData();
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
            }
            try
            {
                try
                {
                    if (_fixture != null)
                        await _fixture.FinishActions();
                }
                finally
                {
                    _fixture?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _data.Running = false;
                _data.ShowResults = true;
                _data.Status = $"Failed during combat cleanup: {ex.Message}";
                try
                {
                    SaveData();
                }
                catch (Exception saveError)
                {
                    PatchHelper.Log($"[Benchmark] Saving cleanup failure: {saveError}");
                }
                PatchHelper.Log($"[Benchmark] {_data.Status}");
            }
            GraphicsPatches.Settings.CopyVisualsFrom(_savedGraphics);
            GraphicsPatches.GraphicsPreferencesPostfix();
            Godot.Engine.MaxFps = _savedFps;
            DisplayServer.ScreenSetKeepOn(_savedKeepOn);
            DisplayServer.WindowSetVsyncMode(_savedVSync);
            _trace?.Dispose();
            _trace = null;
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

    private async Task<BenchmarkMetrics> Sample(double seconds, bool record, Task until = null)
    {
        using var trace = _trace?.Span(record ? "Measure" : "Warmup");
        CheckForeground();
        var samples = new BenchmarkSamples();
        ulong start = Time.GetTicksUsec();
        ulong last = start;
        while (
            until != null
                ? !until.IsCompleted
                : (Time.GetTicksUsec() - start) / 1_000_000d < seconds
        )
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckCancellation();
            if (!_comparison)
                _fixture.Animate();
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

    private void SaveData()
    {
        using var trace = _trace?.Sync("SaveResults");
        _data.Save(DataPath);
    }

    private void CheckTestState()
    {
        CheckCancellation();
        CheckForeground();
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
        if (_running || _restarting || _openingComparison)
            return;
        _owner.Show();
        QueueFree();
    }

    private async void OpenComparisonPage()
    {
        if (_running || _restarting || _openingComparison)
            return;
        _openingComparison = true;
        _start.Disabled = _close.Disabled = true;
        try
        {
            if (_data == null || _data.Results.Count == 0)
                throw new InvalidOperationException(Tr("BENCH_NO_CAPTURE"));
            _status.Text = Tr("COMPARE_DIFF");
            string path = await RenderComparisonPage.Write(_data, GetTree());
            _status.Text = Tr("BENCH_COMPLETED");
            _app.Call("showRenderComparison", path);
        }
        catch (Exception ex)
        {
            _status.Text = Tr("COMPARE_OPEN_FAILED") + " " + ex.Message;
        }
        finally
        {
            _openingComparison = false;
            _start.Disabled = _close.Disabled = false;
        }
    }

    private void ShowResults()
    {
        _status.Text = _data?.Status switch
        {
            "Completed" => Tr("BENCH_COMPLETED"),
            "Running" => Tr(_comparison ? "COMPARE_CAPTURE" : "BENCH_MEASURE"),
            "Cancelled; completed cases preserved" => Tr("BENCH_CANCELLED"),
            "Interrupted; completed cases preserved" => Tr("BENCH_INTERRUPTED"),
            null or "Ready" => Tr(_engine ? "ENGINE_BENCH_INFO" : "BENCH_READY"),
            var status => status,
        };
        if (_data != null && _data.Version < RenderBenchmarkData.FormatVersion)
            AddResultText(Tr("BENCH_LEGACY_RESULT"), 14, LauncherTheme.Gold);
        if (_comparison)
        {
            AddResultText(Tr("COMPARE_INFO"), 14, LauncherTheme.Cream);
            ChangeCapture(0);
            return;
        }
        AddResultText(
            Tr(_engine ? "ENGINE_BENCH_INFO" : "BENCH_RESULTS_HELP"),
            12,
            LauncherTheme.Dim
        );
        foreach (var boot in _data?.Boots ?? new())
            AddResultText(
                $"{Tr("BENCH_APP_LOAD")} #{boot.Phase + 1}: {boot.LaunchToSceneMs:F0} ms · {Tr("BENCH_GAME_INIT")} {boot.GameInitializationMs:F0} ms",
                13,
                LauncherTheme.Cream
            );
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
                if (_engine)
                    AddResultText(
                        $"{Tr("ENGINE_BENCH_MINIMUM")} {metrics.MinimumFps:F1} FPS · {metrics.FrameMaxMs:F2} ms",
                        13,
                        LauncherTheme.Cream
                    );
                foreach (var load in result.LoadTimings)
                    AddResultText(
                        $"{SceneName(load.From)} → {SceneName(load.To)}: {load.DurationMs:F1} ms · {Tr(load.FirstVisit ? "BENCH_FIRST_VISIT" : "BENCH_REPEAT_VISIT")}",
                        13,
                        LauncherTheme.Dim
                    );
                _resultRows.AddChild(SettingsRow.Separator(_scale));
            }
        }
        if (!_engine)
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
            "CombatIdle" => Tr("BENCH_SCENE_COMBAT_IDLE"),
            "CombatTurns" => Tr("BENCH_SCENE_COMBAT_ACTIONS"),
            "CombatCards" => Tr("BENCH_SCENE_COMBAT_CARDS"),
            "CombatEffects" => Tr("BENCH_SCENE_COMBAT_EFFECTS"),
            "KaiserCrabTurns" => Tr("BENCH_SCENE_BOSS_EFFECTS"),
            "WaterfallGiantTurns" => Tr("BENCH_SCENE_WATERFALL_GIANT"),
            "Merchant" => Tr("BENCH_SCENE_MERCHANT"),
            "Map" => Tr("BENCH_SCENE_MAP"),
            "Deck" => Tr("BENCH_SCENE_DECK"),
            "Transitions" => Tr("BENCH_SCENE_TRANSITIONS"),
            "Run" => Tr("BENCH_SCENE_RUN"),
            "Reset" => Tr("BENCH_SCENE_RESET"),
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
