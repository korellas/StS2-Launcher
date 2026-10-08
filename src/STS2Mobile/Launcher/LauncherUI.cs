using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Patches;

namespace STS2Mobile.Launcher;

// Thin wrapper Control that initializes the MVC launcher components and
// processes a main-thread action queue so SteamKit callbacks can update the UI.
public class LauncherUI : Control
{
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    private LauncherModel _model;
    private LauncherView _view;
    private LauncherController _controller;
    private bool _inGameMode;
    private bool _exiting;

    public void Initialize()
    {
        ZIndex = 100;

        try
        {
            var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            SetAnchorsPreset(LayoutPreset.FullRect);
            Size = vpSize;
            var scale = Math.Max(vpSize.X, vpSize.Y) / 960f;

            _model = new LauncherModel(OS.GetDataDir());
            GraphicsPatches.Initialize();
            _model.InGameMode = _inGameMode;
            _view = new LauncherView(this, scale);
            _controller = new LauncherController(_model, _view, QueueMainThreadAction);

            PatchHelper.Log($"LauncherUI initialized. Viewport={vpSize}");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"BuildUI FAILED: {ex}");
            return;
        }

        LauncherPatches.CloudSyncEnabled = LauncherModel.LoadCloudSyncPref();

        // Prevent Android back button from quitting while the launcher is active.
        GetTree().AutoAcceptQuit = false;

        GetTree().ProcessFrame += OnProcessFrame;
        TreeExiting += OnExitTree;
        // The game calls Initialize from _EnterTree, while the root is still
        // visiting its children. Benchmark recovery adds a sibling to that root.
        Callable
            .From(() =>
            {
                if (!_exiting && !RenderBenchmarkScreen.RecoverBoot(this))
                    _controller.Start();
            })
            .CallDeferred();
    }

    public void SetGameMode(bool inGameMode) => _inGameMode = inGameMode;

    public Task WaitForLaunch() => _model.WaitForLaunch();

    private void OnProcessFrame()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"UI update error: {ex.Message}");
            }
        }

        _view?.UpdateKeyboardOffset();
    }

    private void OnExitTree()
    {
        lock (_mainThreadQueue)
        {
            _exiting = true;
            _mainThreadQueue.Clear();
        }
        _controller?.Dispose();
        GetTree().ProcessFrame -= OnProcessFrame;
        GetTree().AutoAcceptQuit = true;
        _model?.Dispose();
    }

    private void QueueMainThreadAction(Action action)
    {
        lock (_mainThreadQueue)
        {
            if (!_exiting)
                _mainThreadQueue.Enqueue(action);
        }
    }
}
