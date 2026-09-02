using System;
using System.Text.RegularExpressions;

namespace STS2Mobile.Launcher.Sections;

// Converts a Steam announcement body into the BBCode subset RichTextLabel
// understands.
//
// Steam mixes two dialects in the same feed: first-party announcements come as
// BBCode, while syndicated press items come as HTML. Both are handled here so
// the reader sees prose either way rather than a wall of markup.
public static class NewsMarkup
{
    public static string ToGodotBbcode(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var text = source.Replace("\r\n", "\n");

        // Steam's headings have no RichTextLabel equivalent; bold at a larger
        // size reads the same way.
        text = Regex.Replace(
            text,
            @"\[h([1-6])\](.*?)\[/h\1\]",
            "\n[b][font_size=26]$2[/font_size][/b]\n",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );

        // Lists: RichTextLabel has [ul] but not Steam's bare [*] items.
        text = Regex.Replace(text, @"\[/?list\]", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[\*\]", "\n  •  ", RegexOptions.IgnoreCase);

        // Images would need fetching and sizing; the link text carries enough.
        text = Regex.Replace(
            text,
            @"\[img\].*?\[/img\]",
            "",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        text = Regex.Replace(text, @"<img[^>]*>", "", RegexOptions.IgnoreCase);

        text = ConvertHtml(text);

        // Anything left that RichTextLabel does not know would render as literal
        // brackets, so drop the tags but keep their contents.
        text = Regex.Replace(
            text,
            @"\[/?(?!b\]|/b\]|i\]|/i\]|u\]|/u\]|url|/url\]|font_size|/font_size\])[^\]]*\]",
            "",
            RegexOptions.IgnoreCase
        );

        // Steam's HTML leaves lines holding nothing but spaces, and a "blank"
        // line like that is not blank to the run collapse below — which is why
        // paragraph gaps came out at arbitrary heights. Empty them first.
        text = Regex.Replace(text, @"[^\S\n]+(?=\n)", "");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string ConvertHtml(string text)
    {
        if (!text.Contains('<'))
            return text;

        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(
            text,
            @"<(strong|b)>(.*?)</\1>",
            "[b]$2[/b]",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        text = Regex.Replace(
            text,
            @"<(em|i)>(.*?)</\1>",
            "[i]$2[/i]",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        text = Regex.Replace(
            text,
            @"<a[^>]*href=""([^""]*)""[^>]*>(.*?)</a>",
            "[url=$1]$2[/url]",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        text = Regex.Replace(text, @"<li[^>]*>", "\n  •  ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", "");

        return text.Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">");
    }
}
