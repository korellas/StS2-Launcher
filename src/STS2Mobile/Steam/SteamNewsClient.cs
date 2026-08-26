using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace STS2Mobile.Steam;

// Fetches recent Steam community announcements for the game so the launcher
// can surface them on the right-hand panel. The endpoint is public (no API
// key needed) but rate-limited per IP, so callers should treat failures as
// non-fatal and avoid retrying tightly.
public static class SteamNewsClient
{
    private const uint AppId = 2868840;
    private const int MaxItems = 5;

    // count=10 because Steam often interleaves third-party feeds before the
    // first-party announcements; we filter to feedname="steam_community_..."
    // and trim down to MaxItems on our side.
    private static readonly string NewsUrl =
        $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v0002/?appid={AppId}"
        // maxlength=0 asks for the full body: the launcher renders announcements
        // itself rather than sending the reader to a browser.
        + "&count=10&maxlength=0&format=json";

    public static async Task<IReadOnlyList<SteamNewsItem>> FetchAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Add("User-Agent", "StS2-Launcher");

        var response = await http.GetStringAsync(NewsUrl).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(response);

        if (
            !doc.RootElement.TryGetProperty("appnews", out var appnews)
            || !appnews.TryGetProperty("newsitems", out var newsItems)
            || newsItems.ValueKind != JsonValueKind.Array
        )
        {
            return Array.Empty<SteamNewsItem>();
        }

        var results = new List<SteamNewsItem>(MaxItems);
        foreach (var item in newsItems.EnumerateArray())
        {
            // Filter to first-party Steam community announcements. Press
            // articles and curator posts use other feed names and add noise.
            var feedName = item.TryGetProperty("feedname", out var f) ? f.GetString() : null;
            if (feedName != "steam_community_announcements")
                continue;

            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            var contents = item.TryGetProperty("contents", out var c) ? c.GetString() : null;
            var date = item.TryGetProperty("date", out var d) && d.TryGetInt64(out var ts) ? ts : 0;

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
                continue;

            results.Add(new SteamNewsItem(title, url, date, contents ?? string.Empty));
            if (results.Count >= MaxItems)
                break;
        }

        return results;
    }
}

public sealed record SteamNewsItem(string Title, string Url, long UnixTimestamp, string Contents)
{
    public DateTimeOffset Date => DateTimeOffset.FromUnixTimeSeconds(UnixTimestamp);
}
