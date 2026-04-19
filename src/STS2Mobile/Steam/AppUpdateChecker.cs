using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace STS2Mobile.Steam;

// Checks for launcher updates by comparing the installed version against the
// latest GitHub release. Returns the download URL if an update is available.
public static class AppUpdateChecker
{
    // Points at this fork's releases so sideloaded builds prompt for updates from
    // here rather than upstream Ekyso. Change the repo segment if forking further.
    //
    // Uses `/releases` (list) instead of `/releases/latest` because the latter
    // excludes prereleases and returns 404 when the repo only has prereleases.
    // We include prereleases here so this fork's rapid-iteration snapshots show
    // up as update prompts.
    private const string ReleasesUrl =
        "https://api.github.com/repos/korellas/StS2-Launcher/releases?per_page=10";

    public static async Task<AppUpdateResult> CheckAsync()
    {
        var currentVersion = GetInstalledVersion();
        if (currentVersion == null)
            return AppUpdateResult.None;

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Add("User-Agent", "StS2-Launcher");

        var response = await http.GetStringAsync(ReleasesUrl).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return AppUpdateResult.None;

        // GitHub returns releases sorted newest-first. Take the first non-draft.
        JsonElement latest = default;
        bool found = false;
        foreach (var rel in root.EnumerateArray())
        {
            var isDraft = rel.TryGetProperty("draft", out var d) && d.GetBoolean();
            if (isDraft)
                continue;
            latest = rel;
            found = true;
            break;
        }

        if (!found)
            return AppUpdateResult.None;

        var releaseName = latest.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString()
            : null;
        if (string.IsNullOrEmpty(releaseName))
        {
            releaseName = latest.TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString()
                : null;
        }

        if (releaseName == null)
            return AppUpdateResult.None;

        var latestVersion = NormalizeVersion(releaseName);
        var installedVersion = NormalizeVersion(currentVersion);

        if (latestVersion == null || installedVersion == null)
            return AppUpdateResult.None;

        if (CompareVersions(latestVersion, installedVersion) <= 0)
            return AppUpdateResult.None;

        string downloadUrl = null;
        if (latest.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name != null && name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var url)
                        ? url.GetString()
                        : null;
                    break;
                }
            }
        }

        return new AppUpdateResult(latestVersion, downloadUrl);
    }

    internal static string GetInstalledVersion()
    {
        try
        {
            var jcw = Engine.GetSingleton("JavaClassWrapper");
            var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
            var godotApp = (GodotObject)wrapper.Call("getInstance");
            return (string)godotApp.Call("getVersionName");
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return null;
        version = version.TrimStart('v', 'V').Trim();
        // Drop semver pre-release / build-metadata suffix so "0.3.3-corner" and
        // "0.3.3+build42" both normalise to "0.3.3" for comparison. CompareVersions
        // only handles numeric segments, and string suffixes would otherwise be
        // silently parsed as 0 and defeat the check.
        var cut = version.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0)
            version = version.Substring(0, cut);
        return version;
    }

    private static int CompareVersions(string a, string b)
    {
        var aParts = a.Split('.');
        var bParts = b.Split('.');
        var len = Math.Max(aParts.Length, bParts.Length);

        for (int i = 0; i < len; i++)
        {
            int aVal = i < aParts.Length && int.TryParse(aParts[i], out var av) ? av : 0;
            int bVal = i < bParts.Length && int.TryParse(bParts[i], out var bv) ? bv : 0;
            if (aVal != bVal)
                return aVal - bVal;
        }

        return 0;
    }
}

public class AppUpdateResult
{
    public static readonly AppUpdateResult None = new(null, null);

    public string LatestVersion { get; }
    public string DownloadUrl { get; }
    public bool HasUpdate => LatestVersion != null;

    public AppUpdateResult(string latestVersion, string downloadUrl)
    {
        LatestVersion = latestVersion;
        DownloadUrl = downloadUrl;
    }
}
