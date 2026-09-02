using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;
using STS2Mobile.Steam;

namespace STS2Mobile.Patches;

// Cloud-sync scheduling for save files. Split out of LauncherPatches deliberately:
// the queue is typed with sts2.dll types, and a class holding such a field cannot be
// loaded at all when the game files haven't been downloaded yet. Keeping it here lets
// LauncherPatches stay loadable in launcher-only mode (LauncherUI reads its statics),
// while this class is only ever touched from the patched game path.
internal static class DeferredHistorySync
{
    // Per-file timeout for cloud sync. On a fresh install the game can request >100
    // history files; without a bound a stuck Steam RPC would freeze startup forever.
    private static readonly TimeSpan CloudSyncTimeout = TimeSpan.FromSeconds(10);

    // Bounded so a large history backlog can't saturate the Steam connection.
    private const int MaxConcurrentDrains = 4;

    // History files are purely for the "past runs" UI and are not needed to show the
    // main menu. We defer them to a background queue that drains after the game is
    // done starting up, so their cloud pulls don't block the critical path.
    private static readonly ConcurrentQueue<(
        ISaveStore local,
        ICloudSaveStore cloud,
        string path
    )> _queue = new();
    private static int _drainStarted;

    internal static void Enqueue(ISaveStore local, ICloudSaveStore cloud, string path) =>
        _queue.Enqueue((local, cloud, path));

    internal static async Task AutoSyncWithTimeout(
        ISaveStore local,
        ICloudSaveStore cloud,
        string path
    )
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

        PatchHelper.Log(
            $"[Cloud] Sync timed out after {CloudSyncTimeout.TotalSeconds:F0}s: {path}"
        );
    }

    // Drains the deferred history queue with bounded concurrency. Called once after
    // the game finishes starting so the UI is already interactive while these pull.
    internal static void StartDrain()
    {
        if (Interlocked.Exchange(ref _drainStarted, 1) == 1)
            return;
        _ = Task.Run(DrainAsync);
    }

    private static async Task DrainAsync()
    {
        var initialCount = _queue.Count;
        if (initialCount == 0)
            return;

        PatchHelper.Log($"[Cloud] Draining {initialCount} deferred history files in background");

        using var throttle = new SemaphoreSlim(MaxConcurrentDrains);
        var tasks = new List<Task>();

        while (_queue.TryDequeue(out var item))
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
}
