using System;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Launcher.Sections;

namespace STS2Mobile.Launcher;

// Builds the launcher UI layout programmatically with a split panel:
// left side has login/download/action controls, right side has a console log.
public class LauncherView
{
    public LoginSection Login { get; }
    public CodeSection Code { get; }
    public DownloadSection Download { get; }
    public ActionSection Actions { get; }
    public NewsSection News { get; }
    public LogView Log { get; }

    private readonly StyledLabel _statusLabel;
    private readonly StyledLabel _versionLabel;
    private readonly Control _parent;

    public LauncherView(Control parent, float scale)
    {
        _parent = parent;
        _scale = scale;
        parent.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var vpSize = parent.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

        Localization.Install();
        GameAssets.LogAvailability();
        PatchHelper.Log($"[Translate] {new TranslationBridge().Capabilities()}");
        GameAssets.DescribeTheme(GameAssets.MenuButtonTheme);
        GameAssets.DescribeTheme(GameAssets.SettingsRowTheme);

        var bg = new ScreenBackground();
        bg.GuiInput += DismissKeyboard;
        parent.AddChild(bg);

        // The old layout was one screen-filling panel split into three columns,
        // which hid the artwork and read as a utility dialog rather than a game
        // menu. Now the entries sit directly on the art like the game's own menu,
        // and the dense parts (news, settings, console) open as submenus.
        var menu = new VBoxContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.52f,
            AnchorBottom = 0.52f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };
        menu.CustomMinimumSize = new Vector2((int)(520 * scale), 0);
        menu.AddThemeConstantOverride("separation", (int)(2 * scale));
        parent.AddChild(menu);
        _menu = menu;

        // Sits above the entries where the text title used to be.
        var logo = LauncherTheme.LoadLogo();
        if (logo != null)
        {
            var mark = new TextureRect
            {
                Texture = logo,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0, (int)(210 * scale)),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            menu.AddChild(mark);
            menu.AddChild(new Control { CustomMinimumSize = new Vector2(0, (int)(28 * scale)) });
        }

        _statusLabel = new StyledLabel(Localization.Tr("STATUS_INITIALIZING"), scale, fontSize: 15);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _statusLabel.Modulate = new Color(1f, 1f, 1f, 0.72f);
        menu.AddChild(_statusLabel);

        // Breathing room between the greeting and the entries, so PLAY reads as
        // the first item of a menu rather than the next line of a paragraph.
        menu.AddChild(new Control { CustomMinimumSize = new Vector2(0, (int)(26 * scale)) });

        Login = new LoginSection(scale);
        menu.AddChild(Login);

        Code = new CodeSection(scale);
        menu.AddChild(Code);

        Download = new DownloadSection(scale);
        menu.AddChild(Download);

        Actions = new ActionSection(scale);
        menu.AddChild(Actions);

        // Settings controls are built by ActionSection but belong in a submenu,
        // so they are reparented rather than duplicated: every signal the
        // controller already connected keeps working untouched.
        var settingsOverlay = new SubmenuOverlay(Localization.Tr("MENU_SETTINGS"), scale, widthRatio: 0.58f, heightRatio: 0.66f);
        Actions.RemoveChild(Actions.SettingsGroup);
        settingsOverlay.Content.AddChild(Actions.SettingsGroup);
        parent.AddChild(settingsOverlay);

        var newsOverlay = new SubmenuOverlay(Localization.Tr("MENU_NEWS"), scale, heightRatio: 0.78f);
        News = new NewsSection(scale);
        News.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        newsOverlay.Content.AddChild(News);

        // The announcement body arrives with the list, so reading happens here
        // rather than in a browser. The list and the article share the panel,
        // swapping places.
        var article = new NewsArticleView(scale) { Visible = false };
        newsOverlay.Content.AddChild(article);

        News.ArticleSelected += item =>
        {
            News.Visible = false;
            article.Show(item, NewsSection.FormatDate(item.Date));
        };
        article.BackRequested += () =>
        {
            article.Visible = false;
            News.Visible = true;
        };
        article.OpenOriginalRequested += NewsSection.OpenInBrowser;

        parent.AddChild(newsOverlay);

        var consoleOverlay = new SubmenuOverlay(Localization.Tr("MENU_CONSOLE"), scale, heightRatio: 0.78f);
        Log = new LogView(scale);
        Log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Log.GuiInput += DismissKeyboard;
        consoleOverlay.Content.AddChild(Log);

        // Copy dumps the whole log to the clipboard: selecting text in a
        // RichTextLabel by touch is fiddly enough that one tap is worth keeping.
        var copyLogsButton = new GameMenuButton(Localization.Tr("ACTION_COPY_LOG"), scale, fontSize: 15);
        copyLogsButton.Pressed += () =>
        {
            DisplayServer.ClipboardSet(Log.GetParsedText());
            Log.AppendLog("[copied console contents to clipboard]");
        };
        consoleOverlay.Content.AddChild(copyLogsButton);
        parent.AddChild(consoleOverlay);

        // Secondary entries sit closer together than PLAY does, giving the menu
        // an obvious primary action instead of four equal-weight lines.
        menu.AddChild(new Control { CustomMinimumSize = new Vector2(0, (int)(14 * scale)) });

        var newsEntry = new GameMenuButton(Localization.Tr("MENU_NEWS"), scale);
        newsEntry.Pressed += newsOverlay.Open;
        menu.AddChild(newsEntry);

        var settingsEntry = new GameMenuButton(Localization.Tr("MENU_SETTINGS"), scale);
        settingsEntry.Pressed += settingsOverlay.Open;
        menu.AddChild(settingsEntry);

        var consoleEntry = new GameMenuButton(Localization.Tr("MENU_CONSOLE"), scale);
        consoleEntry.Pressed += consoleOverlay.Open;
        consoleEntry.Visible = !LauncherModel.GameFilesReady();
        menu.AddChild(consoleEntry);
        _consoleEntry = consoleEntry;

        var quitEntry = new GameMenuButton(Localization.Tr("MENU_QUIT"), scale);
        quitEntry.Pressed += () =>
            ShowConfirmation(Localization.Tr("QUIT_CONFIRM"), QuitApp);
        menu.AddChild(quitEntry);

        // Footer: version on the left, FMOD attribution on the right. The credit
        // is required by the FMOD licence, so it stays on screen even though the
        // rest of the chrome moved into submenus.
        var footer = new HBoxContainer
        {
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = (int)(18 * scale),
            OffsetRight = -(int)(18 * scale),
            OffsetTop = -(int)(46 * scale),
            OffsetBottom = -(int)(8 * scale),
            GrowVertical = Control.GrowDirection.Begin,
        };
        parent.AddChild(footer);

        _versionLabel = new StyledLabel("", scale, fontSize: 11, align: HorizontalAlignment.Left);
        _versionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _versionLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        footer.AddChild(_versionLabel);

        var fmodBox = new VBoxContainer();
        fmodBox.Alignment = BoxContainer.AlignmentMode.End;
        footer.AddChild(fmodBox);

        var fmodLogo = LoadFmodLogo(scale);
        if (fmodLogo != null)
            fmodBox.AddChild(fmodLogo);

        var fmodCredit = new StyledLabel(
            "Made using FMOD Studio by Firelight Technologies Pty Ltd.",
            scale,
            fontSize: 8
        );
        fmodCredit.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
        fmodBox.AddChild(fmodCredit);
    }

    private VBoxContainer _menu;
    private Control _consoleEntry;

    // Kept available while the game files are still being fetched: during a
    // download the console is the only place the per-file detail shows up.
    public void SetConsoleVisible(bool visible)
    {
        if (_consoleEntry != null)
            _consoleEntry.Visible = visible;
    }

    private readonly float _scale;

    public void SetStatus(string text) => _statusLabel.Text = text;

    public void SetVersionStatus(string text) => _versionLabel.Text = text;

    public void AppendLog(string msg) => Log.AppendLog(msg);

    public void AppendColoredLog(string msg, Godot.Color color) => Log.AppendColoredLog(msg, color);

    public void HideAllSections()
    {
        Login.Visible = false;
        Code.Visible = false;
        Download.Visible = false;
        Actions.HideAll();
    }

    public void UpdateKeyboardOffset()
    {
        var kbHeight = DisplayServer.VirtualKeyboardGetHeight();
        if (kbHeight > 0)
        {
            var windowSize = DisplayServer.WindowGetSize();
            var vpSize = _parent.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var scale = vpSize.Y / windowSize.Y;
            var offset = kbHeight * scale * 0.5f;
            // The menu is anchored rather than absolutely positioned now, so
            // nudge it with the anchor offset instead of moving a panel.
            _menu.OffsetTop = -offset;
            _menu.OffsetBottom = -offset;
        }
        else
        {
            _menu.OffsetTop = 0;
            _menu.OffsetBottom = 0;
        }
    }

    // Loads the FMOD logo extracted by GodotApp from internal storage.
    private static TextureRect LoadFmodLogo(float scale)
    {
        try
        {
            var logoPath = System.IO.Path.Combine(OS.GetDataDir(), "fmod_logo.png");
            if (!System.IO.File.Exists(logoPath))
            {
                PatchHelper.Log($"FMOD logo not found at {logoPath}");
                return null;
            }

            var bytes = System.IO.File.ReadAllBytes(logoPath);
            var image = new Image();
            image.LoadPngFromBuffer(bytes);

            var tex = ImageTexture.CreateFromImage(image);
            var rect = new TextureRect();
            rect.Texture = tex;
            rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            rect.CustomMinimumSize = new Vector2((int)(120 * scale), (int)(30 * scale));
            return rect;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Failed to load FMOD logo: {ex.Message}");
            return null;
        }
    }

    // Same exit path the game's own Quit takes, so leaving from the launcher and
    // leaving from the game behave identically.
    private static void QuitApp()
    {
        try
        {
            LauncherModel.GetGodotApp()?.Call("quitApp");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Launcher] quit failed: {ex.Message}");
        }
    }

    public void ShowConfirmation(string message, Action onConfirmed)
    {
        var dialog = new StyledDialog(message, _scale);
        dialog.Confirmed += onConfirmed;
        _parent.AddChild(dialog);
    }

    private void DismissKeyboard(InputEvent ev)
    {
        if (
            ev is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true }
        )
            _parent.GetViewport()?.GuiReleaseFocus();
    }
}
