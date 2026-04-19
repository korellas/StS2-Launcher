using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using STS2Mobile.Launcher;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Steam;

namespace STS2Mobile.Patches;

// Core patches for the mobile launcher flow. Intercepts GameStartupWrapper to show
// the Steam login UI before the game starts, injects cloud save support via SteamKit2,
// and delegates sync logic to CloudSyncCoordinator.
public static class LauncherPatches
{
    internal static bool CloudSyncEnabled = true;
    internal static string SavedAccountName;
    internal static string SavedRefreshToken;

    // Per-file timeout for cloud sync. On a fresh install the game can request >100
    // history files; without a bound a stuck Steam RPC would freeze startup forever.
    private static readonly TimeSpan CloudSyncTimeout = TimeSpan.FromSeconds(10);

    // History files are purely for the "past runs" UI and are not needed to show the
    // main menu. We defer them to a background queue that drains after the game is
    // done starting up, so their cloud pulls don't block the critical path.
    private static readonly ConcurrentQueue<(
        ISaveStore local,
        ICloudSaveStore cloud,
        string path
    )> _deferredHistorySyncs = new();
    private static int _deferredSyncDrainStarted;

    private static bool IsHistoryPath(string path) =>
        path != null && (path.Contains("/history/") || path.Contains("\\history\\"));

    public static void Apply(Harmony harmony)
    {
        PatchHelper.PatchCritical(
            harmony,
            typeof(NGame),
            "GameStartupWrapper",
            prefix: PatchHelper.Method(typeof(LauncherPatches), nameof(GameStartupWrapperPrefix))
        );

        PatchHelper.Patch(
            harmony,
            typeof(SaveManager),
            "ConstructDefault",
            prefix: PatchHelper.Method(typeof(LauncherPatches), nameof(ConstructDefaultPrefix))
        );

        PatchHelper.PatchCritical(
            harmony,
            typeof(CloudSaveStore),
            "SyncCloudToLocal",
            prefix: PatchHelper.Method(typeof(LauncherPatches), nameof(SyncCloudToLocalPrefix))
        );
    }

    public static bool GameStartupWrapperPrefix(object __instance, ref Task __result)
    {
        __result = RunLauncherThenGame(__instance);
        return false;
    }

    public static bool ConstructDefaultPrefix(ref SaveManager __result)
    {
        PatchHelper.Log(
            $"[Cloud] ConstructDefaultPrefix called. HasToken={SavedRefreshToken != null}, CloudSync={CloudSyncEnabled}"
        );

        if (!CloudSyncEnabled)
        {
            PatchHelper.Log("[Cloud] Cloud sync disabled by user — using local-only SaveManager");
            return true;
        }

        if (SavedAccountName == null || SavedRefreshToken == null)
        {
            PatchHelper.Log("[Cloud] No saved credentials — using local-only SaveManager");
            return true;
        }

        try
        {
            var localStore = new GodotFileIo(UserDataPathProvider.GetAccountScopedBasePath(null));
            var cloudStore = new SteamKit2CloudSaveStore(SavedAccountName, SavedRefreshToken);
            var wrappedStore = new CloudSaveStore(localStore, cloudStore);

            __result = new SaveManager(wrappedStore);
            PatchHelper.Log("[Cloud] Created SaveManager with SteamKit2 cloud store");
            return false;
        }
        catch (Exception ex)
        {
            PatchHelper.Log(
                $"[Cloud] Cloud store injection failed, falling back to local: {ex.Message}"
            );
            return true;
        }
    }

    public static bool SyncCloudToLocalPrefix(
        CloudSaveStore __instance,
        string path,
        ref Task __result
    )
    {
        // History files (`profile*/saves/history/*.run[.backup]`) only drive the
        // "past runs" UI. Defer them to a background queue so startup isn't gated
        // on pulling dozens/hundreds of them synchronously. Active state files
        // (progress/profile/current_run/prefs/settings) still sync on the critical
        // path for correctness.
        if (IsHistoryPath(path))
        {
            _deferredHistorySyncs.Enqueue((__instance.LocalStore, __instance.CloudStore, path));
            __result = Task.CompletedTask;
            return false;
        }

        __result = AutoSyncWithTimeout(__instance.LocalStore, __instance.CloudStore, path);
        return false;
    }

    private static async Task AutoSyncWithTimeout(ISaveStore local, ICloudSaveStore cloud, string path)
    {
        using var cts = new CancellationTokenSource(CloudSyncTimeout);
        var syncTask = CloudSyncCoordinator.AutoSyncFileAsync(local, cloud, path);
        var completed = await Task.WhenAny(syncTask, Task.Delay(CloudSyncTimeout, cts.Token));
        if (completed == syncTask)
        {
            cts.Cancel();
            await syncTask;
            return;
        }

        PatchHelper.Log($"[Cloud] Sync timed out after {CloudSyncTimeout.TotalSeconds:F0}s: {path}");
    }

    // Drains the deferred history queue with bounded concurrency. Called once after
    // the game finishes starting so the UI is already interactive while these pull.
    internal static void StartDeferredHistoryDrain()
    {
        if (Interlocked.Exchange(ref _deferredSyncDrainStarted, 1) == 1)
            return;
        _ = Task.Run(DrainDeferredHistoryAsync);
    }

    private static async Task DrainDeferredHistoryAsync()
    {
        var initialCount = _deferredHistorySyncs.Count;
        if (initialCount == 0)
            return;

        PatchHelper.Log($"[Cloud] Draining {initialCount} deferred history files in background");

        using var throttle = new SemaphoreSlim(4);
        var tasks = new System.Collections.Generic.List<Task>();

        while (_deferredHistorySyncs.TryDequeue(out var item))
        {
            await throttle.WaitAsync();
            tasks.Add(
                Task.Run(async () =>
                {
                    try
                    {
                        await AutoSyncWithTimeout(item.local, item.cloud, item.path);
                    }
                    catch (Exception ex)
                    {
                        PatchHelper.Log($"[Cloud] Deferred sync failed {item.path}: {ex.Message}");
                    }
                    finally
                    {
                        throttle.Release();
                    }
                })
            );
        }

        await Task.WhenAll(tasks);
        PatchHelper.Log($"[Cloud] Deferred history drain complete ({initialCount} files)");
    }

    private static async Task RunLauncherThenGame(object game)
    {
        var gameNode = (Node)game;
        var tree = gameNode.GetTree();

        var launcher = new LauncherUI();
        gameNode.AddChild(launcher);
        launcher.SetGameMode(true);
        launcher.Initialize();
        PatchHelper.Log("Launcher UI displayed");

        await launcher.WaitForLaunch();
        PatchHelper.Log("User launched game, proceeding to startup...");

        // Non-blocking "Loading..." indicator in the corner — doesn't cover the
        // game's intro logo or scenes, just tells the user something is still
        // happening during the 25s game boot. Parented to tree.Root so it
        // survives the scene transition the game performs in GameStartup.
        var overlay = LoadingOverlay.Show(tree, "Loading");

        var instanceField = typeof(SaveManager).GetField(
            "_instance",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (instanceField != null)
        {
            instanceField.SetValue(null, null);
            PatchHelper.Log("[Cloud] Reset SaveManager._instance for cloud store re-injection");
        }

        launcher.QueueFree();

        if (ShaderWarmupScreen.NeedsWarmup())
        {
            // Shader warmup has its own full-screen UI with progress.
            var warmup = new ShaderWarmupScreen();
            gameNode.AddChild(warmup);
            warmup.Initialize();
            await warmup.WaitForCompletion();
            warmup.QueueFree();
        }

        SaveManager.Instance.InitSettingsData();

        var gameStartup = game.GetType()
            .GetMethod("GameStartup", BindingFlags.NonPublic | BindingFlags.Instance);

        // Start the background history sync drain so `profile*/saves/history/*.run*`
        // files pull in the background while the game initialises.
        StartDeferredHistoryDrain();

        try
        {
            await (Task)gameStartup.Invoke(game, null);
        }
        catch (TargetInvocationException ex)
        {
            PatchHelper.Log($"Game startup failed: {ex.InnerException?.Message}");
            if (GodotObject.IsInstanceValid(overlay))
                overlay.FadeOutAndFree(0.2f);
            throw ex.InnerException ?? ex;
        }

        if (GodotObject.IsInstanceValid(overlay))
            overlay.FadeOutAndFree(0.35f);
    }
}
