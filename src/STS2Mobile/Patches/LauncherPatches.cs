using System;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    internal static bool FpsOverlayEnabled;
    internal static bool OverlayShowCpu = true;
    internal static bool OverlayShowGpu = true;
    internal static bool OverlayShowTemp = true;
    internal static string SavedAccountName;
    internal static string SavedRefreshToken;

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
            __result = ConstructCloudSaveManager();
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

    // Behind a non-inlined call boundary on purpose. SteamKit2CloudSaveStore implements
    // ICloudSaveStore from sts2.dll, so a game update that adds an interface member makes
    // loading the type fail outright (TypeLoadException: VTable setup failed). Inlined here,
    // the JIT resolves that type while compiling ConstructDefaultPrefix — before execution
    // reaches the try — so the fallback below never runs, SaveManager.Instance stays broken,
    // and the game hangs on the loading screen with the exception swallowed by TaskHelper.
    // Keeping it separate downgrades interface drift to "cloud saves off" instead of a hang.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SaveManager ConstructCloudSaveManager()
    {
        var localStore = new GodotFileIo(UserDataPathProvider.GetAccountScopedBasePath(null));
        var cloudStore = new SteamKit2CloudSaveStore(SavedAccountName, SavedRefreshToken);
        var wrappedStore = new CloudSaveStore(localStore, cloudStore);

        return new SaveManager(wrappedStore);
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
            DeferredHistorySync.Enqueue(__instance.LocalStore, __instance.CloudStore, path);
            __result = Task.CompletedTask;
            return false;
        }

        __result = DeferredHistorySync.AutoSyncWithTimeout(
            __instance.LocalStore,
            __instance.CloudStore,
            path
        );
        return false;
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
        var overlay = LoadingOverlay.Show(tree);


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
        DeferredHistorySync.StartDrain();

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

        // Only once the game is up. During loading the viewport differs from the
        // in-game one, so the card appeared at one size and jumped to another, and
        // a loading screen's frame rate is not worth watching.
        if (FpsOverlayEnabled)
        {
            FpsOverlay.Show(tree);
            PatchHelper.Log("[Overlay] FPS overlay enabled");
        }
    }
}
