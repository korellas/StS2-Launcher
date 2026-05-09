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
    private static readonly Color StatusColor = new(0.55f, 0.55f, 0.6f);
    private static readonly Color TitleColor = new(0.85f, 0.85f, 0.9f);
    private static readonly Color DateColor = new(0.5f, 0.5f, 0.55f);
    private static readonly Color HoverColor = new(1.0f, 0.8f, 0.3f);

    private readonly float _scale;
    private readonly StyledLabel _statusLabel;
    private readonly VBoxContainer _itemsContainer;

    public NewsSection(float scale)
    {
        _scale = scale;
        AddThemeConstantOverride("separation", (int)(4 * scale));

        var header = new StyledLabel("Steam News", scale, fontSize: 14);
        header.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        AddChild(header);

        _statusLabel = new StyledLabel("Loading…", scale, fontSize: 11);
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
            _statusLabel.Text = "(no recent announcements)";
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
        _statusLabel.Text = "(news unavailable)";
        _statusLabel.Visible = true;
    }

    private void ClearItems()
    {
        foreach (var child in _itemsContainer.GetChildren())
            child.QueueFree();
    }

    private Control BuildRow(SteamNewsItem item)
    {
        // Use a PanelContainer + gui_input handler instead of a Button.
        // Button is not a Container in Godot 4 — adding child Controls
        // works visually but the children aren't laid out, so the title
        // and date end up stacked at (0,0) at their minimum size. The
        // result is overlapping squashed text in a narrow strip.
        // PanelContainer is a real Container that sizes its single child
        // to fill the panel area.
        var row = new PanelContainer();
        row.MouseFilter = MouseFilterEnum.Stop;
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.CustomMinimumSize = new Vector2(0, (int)(56 * _scale));

        var normalStyle = StyledButton.MakeFilled(Colors.Transparent, (int)(3 * _scale));
        normalStyle.ContentMarginLeft = (int)(8 * _scale);
        normalStyle.ContentMarginRight = (int)(8 * _scale);
        normalStyle.ContentMarginTop = (int)(6 * _scale);
        normalStyle.ContentMarginBottom = (int)(6 * _scale);

        var hoverStyle = StyledButton.MakeFilled(
            new Color(1f, 1f, 1f, 0.06f),
            (int)(3 * _scale)
        );
        hoverStyle.ContentMarginLeft = normalStyle.ContentMarginLeft;
        hoverStyle.ContentMarginRight = normalStyle.ContentMarginRight;
        hoverStyle.ContentMarginTop = normalStyle.ContentMarginTop;
        hoverStyle.ContentMarginBottom = normalStyle.ContentMarginBottom;

        row.AddThemeStyleboxOverride("panel", normalStyle);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", (int)(2 * _scale));
        inner.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(inner);

        var title = new StyledLabel(item.Title, _scale, fontSize: 14);
        title.AddThemeColorOverride("font_color", TitleColor);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.MouseFilter = MouseFilterEnum.Ignore;
        inner.AddChild(title);

        var date = new StyledLabel(FormatDate(item.Date), _scale, fontSize: 10);
        date.AddThemeColorOverride("font_color", DateColor);
        date.MouseFilter = MouseFilterEnum.Ignore;
        inner.AddChild(date);

        row.MouseEntered += () =>
        {
            row.AddThemeStyleboxOverride("panel", hoverStyle);
            title.AddThemeColorOverride("font_color", HoverColor);
        };
        row.MouseExited += () =>
        {
            row.AddThemeStyleboxOverride("panel", normalStyle);
            title.AddThemeColorOverride("font_color", TitleColor);
        };
        row.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                OpenInAppWebView(item.Url);
                row.AcceptEvent();
            }
            else if (ev is InputEventScreenTouch t && t.Pressed)
            {
                OpenInAppWebView(item.Url);
                row.AcceptEvent();
            }
        };

        return row;
    }

    // Tries to open the article inside the launcher via the GodotApp
    // WebView overlay; falls back to the system browser if the JNI bridge
    // isn't available (e.g. running on desktop for dev).
    private static void OpenInAppWebView(string url)
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
    private static string FormatDate(DateTimeOffset date)
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
