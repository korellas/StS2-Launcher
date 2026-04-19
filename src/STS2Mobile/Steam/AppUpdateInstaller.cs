using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Patches;

namespace STS2Mobile.Steam;

// Downloads a launcher APK to the app's private updates dir and hands it to
// PackageInstaller via the Java bridge in GodotApp. The system dialog step
// is unavoidable — Android will not silently replace a running app from a
// sideloaded source.
public static class AppUpdateInstaller
{
    public static event Action<double> ProgressChanged;

    public static async Task<bool> DownloadAndInstallAsync(
        string downloadUrl,
        string version,
        IProgress<double> progress = null
    )
    {
        if (string.IsNullOrEmpty(downloadUrl))
            throw new ArgumentException("downloadUrl is required", nameof(downloadUrl));

        var apkPath = await DownloadApkAsync(downloadUrl, version, progress).ConfigureAwait(false);
        return LaunchInstaller(apkPath);
    }

    public static async Task<string> DownloadApkAsync(
        string downloadUrl,
        string version,
        IProgress<double> progress = null
    )
    {
        var updatesDir = GetUpdatesDir();
        Directory.CreateDirectory(updatesDir);

        // Clear any previous partial downloads so we don't mix bytes across
        // versions. An interrupted download from a prior session otherwise
        // sticks around and masks real progress.
        foreach (var stale in Directory.GetFiles(updatesDir, "*.apk"))
        {
            try
            {
                File.Delete(stale);
            }
            catch
            {
            }
        }

        var safeVersion = string.IsNullOrEmpty(version) ? "update" : version;
        var apkPath = Path.Combine(updatesDir, $"StS2Launcher-v{safeVersion}.apk");

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Add("User-Agent", "StS2-Launcher");

        using var response = await http
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        long readBytes = 0;

        await using (var src = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        await using (
            var dst = new FileStream(
                apkPath,
                FileMode.Create,
                System.IO.FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true
            )
        )
        {
            var buffer = new byte[64 * 1024];
            double lastReported = -1;
            int read;
            while ((read = await src.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                readBytes += read;

                if (totalBytes <= 0)
                    continue;

                var fraction = (double)readBytes / totalBytes;
                if (fraction - lastReported >= 0.01 || fraction >= 1.0)
                {
                    lastReported = fraction;
                    progress?.Report(fraction);
                    ProgressChanged?.Invoke(fraction);
                }
            }
        }

        PatchHelper.Log($"[Update] Downloaded {readBytes:N0} bytes to {apkPath}");
        return apkPath;
    }

    public static bool CanInstallPackages()
    {
        try
        {
            var app = GetGodotApp();
            return app != null && (bool)app.Call("canInstallPackages");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Update] canInstallPackages probe failed: {ex.Message}");
            return false;
        }
    }

    public static void RequestInstallPermission()
    {
        try
        {
            GetGodotApp()?.Call("requestInstallPackagesPermission");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Update] requestInstallPackagesPermission failed: {ex.Message}");
        }
    }

    public static bool LaunchInstaller(string apkPath)
    {
        try
        {
            var app = GetGodotApp();
            if (app == null)
                return false;
            return (bool)app.Call("installApk", apkPath);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Update] installApk bridge failed: {ex.Message}");
            return false;
        }
    }

    private static string GetUpdatesDir()
    {
        try
        {
            var app = GetGodotApp();
            if (app != null)
            {
                var dir = (string)app.Call("getUpdatesDir");
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Update] getUpdatesDir bridge failed: {ex.Message}");
        }

        // Fallback: Godot's user:// maps to files/ on Android which is exactly
        // what GodotApp.getFilesDir() returns, so the "updates" subdir below
        // still matches res/xml/file_paths.xml.
        return Path.Combine(OS.GetDataDir(), "updates");
    }

    private static GodotObject GetGodotApp()
    {
        var jcw = Engine.GetSingleton("JavaClassWrapper");
        var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
        return (GodotObject)wrapper.Call("getInstance");
    }
}
