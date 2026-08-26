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
    private readonly Button _translateButton;
    private readonly TranslationBridge _translation = new();
    private readonly Timer _poll = new();

    private string _url = "";
    private string _originalBody = "";
    private bool _showingTranslation;

    // Set once a request fails, so a device without a translation service doesn't
    // spend a spinner on every article that gets opened.
    private static bool _translationUnavailable;

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

        _translateButton = new GameMenuButton(Localization.Tr("NEWS_TRANSLATE"), scale, fontSize: 17);
        _translateButton.Pressed += Translate;
        header.AddChild(_translateButton);

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
            SelectionEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        LauncherTheme.ApplyGameFont(_body, 18, scale);
        _body.AddThemeColorOverride("default_color", LauncherTheme.Cream);
        _body.MetaClicked += meta => OpenOriginalRequested?.Invoke(meta.AsString());
        AddChild(_body);

        _poll.WaitTime = 0.25;
        _poll.Timeout += OnPoll;
        AddChild(_poll);
    }

    public void Show(SteamNewsItem item, string formattedDate)
    {
        _url = item.Url;
        _title.Text = item.Title;
        _date.Text = formattedDate;

        _originalBody = NewsMarkup.ToGodotBbcode(item.Contents);
        _showingTranslation = false;
        _body.Text = string.IsNullOrWhiteSpace(_originalBody)
            ? Localization.Tr("NEWS_NO_BODY")
            : _originalBody;
        _translateButton.Text = Localization.Tr("NEWS_TRANSLATE");
        _translateButton.Disabled = string.IsNullOrWhiteSpace(_originalBody);
        Visible = true;

        if (Localization.IsKorean && !_translationUnavailable && !_translateButton.Disabled)
            Translate();
    }

    private void Translate()
    {
        // Second press returns to the original rather than translating again.
        if (_showingTranslation)
        {
            _showingTranslation = false;
            _body.Text = _originalBody;
            _translateButton.Text = Localization.Tr("NEWS_TRANSLATE");
            return;
        }

        // Tags would be translated as words, so the request carries the plain
        // text and the result is shown without markup.
        var plain = _body.GetParsedText();
        if (string.IsNullOrWhiteSpace(plain) || !_translation.Start(plain))
        {
            _translateButton.Text = Localization.Tr("NEWS_TRANSLATE_UNAVAILABLE");
            return;
        }

        _translateButton.Disabled = true;
        _translateButton.Text = Localization.Tr("NEWS_TRANSLATING");
        _poll.Start();
    }

    private void ReportUnavailable()
    {
        _translationUnavailable = true;
        _translateButton.Disabled = false;
        _translateButton.Text = Localization.Tr("NEWS_TRANSLATE_UNAVAILABLE");

        var reason = _translation.Capabilities();
        PatchHelper.Log($"[Translate] unavailable — {reason}");
        _body.Text = $"[color=#c8a06a]{Localization.Tr("NEWS_TRANSLATE_UNAVAILABLE")}: {reason}[/color]\n\n"
            + _originalBody;
    }

    private void OnPoll()
    {
        switch (_translation.Poll())
        {
            case TranslationBridge.State.Running:
                return;

            case TranslationBridge.State.Done:
                _poll.Stop();
                _showingTranslation = true;
                _body.Text = _translation.Result();
                _translateButton.Disabled = false;
                _translateButton.Text = Localization.Tr("NEWS_SHOW_ORIGINAL");
                return;

            default:
                _poll.Stop();
                _translateButton.Disabled = false;
                _translateButton.Text = Localization.Tr("NEWS_TRANSLATE_UNAVAILABLE");
                PatchHelper.Log($"[Translate] unavailable — {_translation.Capabilities()}");
                return;
        }
    }
}
