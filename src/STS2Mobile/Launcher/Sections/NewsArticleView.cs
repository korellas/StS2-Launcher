using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly StyledLabel _translateStatus;
    private readonly RichTextLabel _body;
    private readonly float _scale;
    private readonly Button _translateButton;
    private const string BulletMarker = "\u2022";

    private readonly TranslationBridge _translation = new();
    private readonly Timer _poll = new();

    // Paragraphs are translated one at a time and reassembled, rather than the
    // article going over as a single blob: a translator handed the whole body
    // returns one run-on block with every line break gone, and shorter segments
    // usually come back better anyway.
    private readonly List<string> _sourceLines = new();
    private readonly List<string> _translatedLines = new();
    private int _lineIndex;

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

        // Its own line rather than a prefix on the body: the body is rewritten
        // whenever the view toggles between original and translation, which is
        // what kept swallowing the explanation.
        _translateStatus = new StyledLabel("", scale, fontSize: 15, HorizontalAlignment.Left);
        _translateStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _translateStatus.AddThemeColorOverride("font_color", new Color(0.85f, 0.66f, 0.42f));
        _translateStatus.Text = $"{Localization.Tr("NEWS_TRANSLATE")}: {_translation.Capabilities()}";
        AddChild(_translateStatus);

        _title = new StyledLabel("", scale, fontSize: 30, HorizontalAlignment.Left);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_title);

        _date = new StyledLabel("", scale, fontSize: 17, HorizontalAlignment.Left);
        _date.Modulate = new Color(1f, 1f, 1f, 0.6f);
        AddChild(_date);

        _body = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, (int)(240 * scale)),
            // Pass, not Stop: the label still receives clicks for links,
            // but a drag reaches the ScrollContainer instead of dying here.
            MouseFilter = MouseFilterEnum.Pass,
        };
        var bodyFont = LauncherTheme.GameFont();
        if (bodyFont != null)
        {
            // The game font is drawn tight for short menu labels. At article
            // length that reads as a wall, so open the glyph spacing up a little.
            var spaced = new FontVariation { BaseFont = bodyFont, SpacingGlyph = 1 };
            _body.AddThemeFontOverride("normal_font", spaced);
            _body.AddThemeFontOverride("bold_font", spaced);
        }
        _body.AddThemeFontSizeOverride("normal_font_size", (int)(22 * scale));
        _body.AddThemeFontSizeOverride("bold_font_size", (int)(22 * scale));
        _body.AddThemeColorOverride("default_color", LauncherTheme.Cream);
        // Long prose needs air; the game font is drawn for short labels.
        _body.AddThemeConstantOverride("line_separation", (int)(11 * scale));
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

        // Tags would be translated as words, so the request carries plain text and
        // the result is shown without markup.
        var plain = _body.GetParsedText();
        if (string.IsNullOrWhiteSpace(plain))
        {
            ReportUnavailable();
            return;
        }

        _sourceLines.Clear();
        _translatedLines.Clear();
        _sourceLines.AddRange(GroupIntoParagraphs(plain));
        _lineIndex = 0;

        _translateButton.Disabled = true;
        _translateButton.Text = Localization.Tr("NEWS_TRANSLATING");

        if (!StartNextLine())
            ReportUnavailable();
    }

    // A sentence hard-wrapped across three source lines used to be three separate
    // requests, each translated with no idea what came before it — the single
    // biggest cause of the mangled output. Rejoin those into one unit so the
    // engine sees a whole sentence. Blank lines stay put because they carry the
    // paragraph structure, and bullets stay separate because merging them would
    // run a list together into one paragraph.
    private static List<string> GroupIntoParagraphs(string plain)
    {
        var units = new List<string>();
        var pending = new List<string>();

        void Flush()
        {
            if (pending.Count == 0)
                return;
            units.Add(string.Join(" ", pending));
            pending.Clear();
        }

        foreach (var raw in plain.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                units.Add("");
                continue;
            }

            if (line.TrimStart().StartsWith(BulletMarker, StringComparison.Ordinal))
            {
                Flush();
                units.Add(line);
                continue;
            }

            pending.Add(line.Trim());
        }

        Flush();
        return units;
    }

    // Blank lines and separators carry structure but nothing to translate, so
    // they are copied straight across.
    private bool StartNextLine()
    {
        while (_lineIndex < _sourceLines.Count)
        {
            var line = _sourceLines[_lineIndex];
            if (string.IsNullOrWhiteSpace(line) || !HasLetters(line))
            {
                _translatedLines.Add(line);
                _lineIndex++;
                continue;
            }

            if (!_translation.Start(line))
                return false;

            _poll.Start();
            return true;
        }

        FinishTranslation();
        return true;
    }

    private static bool HasLetters(string text)
    {
        foreach (var c in text)
        {
            if (char.IsLetter(c))
                return true;
        }
        return false;
    }

    private void FinishTranslation()
    {
        _poll.Stop();
        _showingTranslation = true;
        _body.Text = string.Join("\n", _translatedLines.Select(line => line.TrimEnd()));
        _translateButton.Disabled = false;
        _translateButton.Text = Localization.Tr("NEWS_SHOW_ORIGINAL");
    }

    private void ReportUnavailable()
    {
        _translationUnavailable = true;
        _translateButton.Disabled = false;
        _translateButton.Text = Localization.Tr("NEWS_TRANSLATE_UNAVAILABLE");

        var reason = _translation.Capabilities();
        PatchHelper.Log($"[Translate] unavailable — {reason}");
        _translateStatus.Text = $"{Localization.Tr("NEWS_TRANSLATE_UNAVAILABLE")}: {reason}";
    }

    private void OnPoll()
    {
        switch (_translation.Poll())
        {
            case TranslationBridge.State.Running:
                return;

            case TranslationBridge.State.Done:
                _poll.Stop();
                _translatedLines.Add(_translation.Result());
                _lineIndex++;
                if (!StartNextLine())
                    ReportUnavailable();
                return;

            default:
                _poll.Stop();
                ReportUnavailable();
                return;
        }
    }
}
