using System;
using System.Collections.Generic;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher.Sections;

// Renders a small "Steam News" panel — recent Slay the Spire 2 community
// announcements with their date. Tapping a row opens the announcement in
// the system browser. Lazy/non-blocking: starts in a "Loading…" state and
// is updated by the controller once SteamNewsClient.FetchAsync resolves.
public class NewsSection : VBoxContainer
{
    public event Action<SteamNewsItem> ArticleSelected;

    private static readonly Color StatusColor = new(0.72f, 0.74f, 0.80f);
    private static readonly Color TitleColor = LauncherTheme.Cream;
    private static readonly Color DateColor = new(0.62f, 0.64f, 0.70f);
    private static readonly Color HoverColor = LauncherTheme.Gold;

    private readonly float _scale;
    private readonly StyledLabel _statusLabel;
    private readonly VBoxContainer _itemsContainer;

    public NewsSection(float scale)
    {
        _scale = scale;
        AddThemeConstantOverride("separation", (int)(4 * scale));

        var header = new StyledLabel(Localization.Tr("NEWS_HEADER"), scale, fontSize: 19, align: HorizontalAlignment.Left);
        header.AddThemeColorOverride("font_color", StatusColor);
        AddChild(header);

        _statusLabel = new StyledLabel(Localization.Tr("NEWS_LOADING"), scale, fontSize: 17);
        _statusLabel.AddThemeColorOverride("font_color", StatusColor);
        AddChild(_statusLabel);

        _itemsContainer = new VBoxContainer();
        _itemsContainer.AddThemeConstantOverride("separation", (int)(2 * scale));
        AddChild(_itemsContainer);
    }

    // Replaces the in-progress placeholder with rendered news items.
    public void SetItems(IReadOnlyList<SteamNewsItem> items)
    {
        ClearItems();

        if (items == null || items.Count == 0)
        {
            _statusLabel.Text = Localization.Tr("NEWS_EMPTY");
            _statusLabel.Visible = true;
            return;
        }

        _statusLabel.Visible = false;

        foreach (var item in items)
            _itemsContainer.AddChild(BuildRow(item));
    }

    public void SetFailed()
    {
        ClearItems();
        _statusLabel.Text = Localization.Tr("NEWS_UNAVAILABLE");
        _statusLabel.Visible = true;
    }

    private void ClearItems()
    {
        foreach (var child in _itemsContainer.GetChildren())
            child.QueueFree();
    }

    private Control BuildRow(SteamNewsItem item)
    {
        // Button (with the inner VBox anchored to FullRect inside it) gives
        // us hover/pressed/focus styles for free. Single-line title +
        // ellipsis on overflow keeps the row's height fixed and predictable
        // — the previous WordSmart-wrap version inside a PanelContainer
        // produced a layout cycle that exploded the news column tall enough
        // to push the rest of the panel out of view on real devices. With
        // the wider news column from the 3-column rework, almost every
        // STS2 announcement title fits on one line; the rare press-article
        // headline truncates with an ellipsis.
        var btn = new Button();
        btn.Flat = true;
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.CustomMinimumSize = new Vector2(0, (int)(54 * _scale));
        btn.Text = "";

        var transparent = StyledButton.MakeFilled(Colors.Transparent, 0);
        var hoverStyle = StyledButton.MakeFilled(
            new Color(1f, 1f, 1f, 0.07f),
            (int)(3 * _scale)
        );
        btn.AddThemeStyleboxOverride("normal", transparent);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.AddThemeStyleboxOverride("focus", transparent);

        // Button is not a Container in Godot 4 — children stay at (0,0)
        // unless explicitly anchored. Anchor inner to FullRect so the
        // VBoxContainer fills the button's rect; then the VBox lays out
        // the title and date labels normally.
        var inner = new VBoxContainer();
        btn.AddChild(inner);
        inner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        inner.OffsetLeft = (int)(8 * _scale);
        inner.OffsetRight = -(int)(8 * _scale);
        inner.OffsetTop = (int)(6 * _scale);
        inner.OffsetBottom = -(int)(6 * _scale);
        inner.AddThemeConstantOverride("separation", (int)(2 * _scale));
        inner.MouseFilter = MouseFilterEnum.Ignore;

        var title = new StyledLabel(item.Title, _scale, fontSize: 21, align: HorizontalAlignment.Left);
        title.AddThemeColorOverride("font_color", TitleColor);
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.MouseFilter = MouseFilterEnum.Ignore;
        inner.AddChild(title);

        var date = new StyledLabel(FormatDate(item.Date), _scale, fontSize: 15, align: HorizontalAlignment.Left);
        date.AddThemeColorOverride("font_color", DateColor);
        date.MouseFilter = MouseFilterEnum.Ignore;
        inner.AddChild(date);

        btn.MouseEntered += () => title.AddThemeColorOverride("font_color", HoverColor);
        btn.MouseExited += () => title.AddThemeColorOverride("font_color", TitleColor);
        btn.Pressed += () => ArticleSelected?.Invoke(item);

        return btn;
    }

    // Tries to open the article inside the launcher via the GodotApp
    // WebView overlay; falls back to the system browser if the JNI bridge
    // isn't available (e.g. running on desktop for dev).
    public static void OpenInBrowser(string url)
    {
        try
        {
            var jcw = Engine.GetSingleton("JavaClassWrapper");
            var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
            var godotApp = (GodotObject)wrapper.Call("getInstance");
            godotApp.Call("showWebView", url);
        }
        catch (Exception ex)
        {
            STS2Mobile.PatchHelper.Log(
                $"[News] In-app WebView unavailable ({ex.Message}); falling back to OS shell"
            );
            try
            {
                OS.ShellOpen(url);
            }
            catch (Exception ex2)
            {
                STS2Mobile.PatchHelper.Log($"[News] Failed to open {url}: {ex2.Message}");
            }
        }
    }

    // "3d ago", "2w ago", "Mar 5" — keeps the row to a single short line.
    public static string FormatDate(DateTimeOffset date)
    {
        var delta = DateTimeOffset.UtcNow - date;
        if (delta.TotalHours < 1)
            return "just now";
        if (delta.TotalHours < 24)
            return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 7)
            return $"{(int)delta.TotalDays}d ago";
        if (delta.TotalDays < 30)
            return $"{(int)(delta.TotalDays / 7)}w ago";
        return date.LocalDateTime.ToString("MMM d");
    }
}
