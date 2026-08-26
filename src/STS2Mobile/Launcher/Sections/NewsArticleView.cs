using System;
using Godot;
using STS2Mobile.Launcher;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher.Sections;

// An announcement rendered inside the launcher. The Steam news endpoint returns
// the body alongside the title, so there is no reason to hand the reader to a
// browser just to read it.
public class NewsArticleView : VBoxContainer
{
    public event Action BackRequested;
    public event Action<string> OpenOriginalRequested;

    private readonly StyledLabel _title;
    private readonly StyledLabel _date;
    private readonly RichTextLabel _body;
    private readonly float _scale;

    private string _url = "";

    public NewsArticleView(float scale)
    {
        _scale = scale;
        AddThemeConstantOverride("separation", (int)(8 * scale));
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", (int)(12 * scale));
        AddChild(header);

        var back = new GameMenuButton(Localization.Tr("NEWS_BACK_TO_LIST"), scale, fontSize: 17);
        back.Pressed += () => BackRequested?.Invoke();
        header.AddChild(back);

        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var original = new GameMenuButton(Localization.Tr("NEWS_OPEN_ORIGINAL"), scale, fontSize: 17);
        original.Pressed += () => OpenOriginalRequested?.Invoke(_url);
        header.AddChild(original);

        _title = new StyledLabel("", scale, fontSize: 24, HorizontalAlignment.Left);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_title);

        _date = new StyledLabel("", scale, fontSize: 14, HorizontalAlignment.Left);
        _date.Modulate = new Color(1f, 1f, 1f, 0.6f);
        AddChild(_date);

        _body = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        LauncherTheme.ApplyGameFont(_body, 18, scale);
        _body.AddThemeColorOverride("default_color", LauncherTheme.Cream);
        _body.MetaClicked += meta => OpenOriginalRequested?.Invoke(meta.AsString());
        AddChild(_body);
    }

    public void Show(SteamNewsItem item, string formattedDate)
    {
        _url = item.Url;
        _title.Text = item.Title;
        _date.Text = formattedDate;

        var body = NewsMarkup.ToGodotBbcode(item.Contents);
        _body.Text = string.IsNullOrWhiteSpace(body) ? Localization.Tr("NEWS_NO_BODY") : body;
        Visible = true;
    }
}
