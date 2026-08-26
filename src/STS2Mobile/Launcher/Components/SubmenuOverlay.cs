using System;
using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// A submenu drawn on the game's own submenu_panel texture, opened from a menu
// entry and hidden otherwise. The console, news and settings all live in one of
// these instead of being permanently on screen.
public class SubmenuOverlay : Control
{
    public event Action Opened;

    public VBoxContainer Content { get; }

    private readonly ColorRect _scrim;

    public SubmenuOverlay(string title, float scale, float widthRatio = 0.68f, float heightRatio = 0.72f)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        // Dim whatever is behind so the panel reads as a modal layer, and give
        // tapping outside an obvious way to close.
        _scrim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = MouseFilterEnum.Stop };
        _scrim.SetAnchorsPreset(LayoutPreset.FullRect);
        _scrim.GuiInput += OnScrimInput;
        AddChild(_scrim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        // Assigned before the frame is built: ViewportRelativeSize reads them, and
        // leaving them at zero collapsed every panel down to its header.
        _widthRatio = widthRatio;
        _heightRatio = heightRatio;

        var frame = BuildFrame(center, scale, widthRatio, heightRatio);

        // Anchored to the panel, not the screen. It used to sit 34px from the
        // viewport's left edge at 66% of its height — figures with no relationship
        // to the panel, so the button drifted away from it as the panel resized
        // and ended up stranded in empty background.
        _back = new GameBackButton(scale);
        _back.Pressed += Hide;
        AddChild(_back);
        PositionBack();

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", (int)(12 * scale));
        frame.AddChild(header);

        // Long lists (news, console) need to scroll; without this they simply ran
        // past the panel.
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        frame.AddChild(scroll);
        LauncherTheme.ApplyGameScrollbar(scroll, scale);

        var gutter = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        gutter.AddThemeConstantOverride("margin_right", (int)(22 * scale));
        scroll.AddChild(gutter);

        Content = new VBoxContainer();
        Content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Content.SizeFlagsVertical = SizeFlags.ExpandFill;
        Content.AddThemeConstantOverride("separation", (int)(10 * scale));
        gutter.AddChild(Content);

        var titleLabel = new StyledLabel(title, scale, fontSize: 26, align: HorizontalAlignment.Left);
        titleLabel.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(titleLabel);

    }

    // NinePatchRect over the game's panel art when available; the launcher's own
    // rounded panel otherwise.
    // A PanelContainer with a StyleBoxTexture rather than a NinePatchRect: the
    // rect is not a container, so content anchored inside it contributes nothing
    // to its size and the panel stayed at its minimum while the rows spilled out
    // the bottom. A PanelContainer grows to fit whatever it holds.
    private Container BuildFrame(Control parent, float scale, float widthRatio, float heightRatio)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = ViewportRelativeSize(),
            MouseFilter = MouseFilterEnum.Stop,
        };

        var texture = GameAssets.Load<Texture2D>(GameAssets.PopupPanel);
        if (texture != null)
        {
            var style = new StyleBoxTexture { Texture = texture };
            // Corners of the game's panel must not stretch; only the middle may.
            float inset = Math.Min(texture.GetWidth(), texture.GetHeight()) / 3f;
            style.SetTextureMarginAll(inset);
            style.SetContentMarginAll(34 * scale);
            style.ModulateColor = LauncherTheme.PanelSlate;
            panel.AddThemeStyleboxOverride("panel", style);
        }
        else
        {
            var style = LauncherTheme.Panel(scale);
            style.SetContentMarginAll((int)(28 * scale));
            panel.AddThemeStyleboxOverride("panel", style);
        }

        parent.AddChild(panel);
        _frame = panel;

        var box = new VBoxContainer();
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.AddChild(box);
        return box;
    }

    private void OnScrimInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
            Hide();
    }

    private float _widthRatio;
    private float _heightRatio;
    private Control _frame;
    private GameBackButton _back;

    private const float BackClipRatio = 0.203f;

    // The game puts the flag's centre here, measured on its settings screen.
    // Tying it to the panel's bottom edge instead left it 275px low.
    private const float BackCenterY = 0.6155f;



    private Vector2 ViewportRelativeSize()
    {
        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        return new Vector2((int)(vp.X * _widthRatio), (int)(vp.Y * _heightRatio));
    }

    // Folding the device changes the viewport without recreating the activity, so
    // the panel has to re-read its size rather than keep the one it was built with.
    public void Open()
    {
        if (_frame != null)
            _frame.CustomMinimumSize = ViewportRelativeSize();
        PositionBack();
        Visible = true;
        Opened?.Invoke();
    }

    // The panel is centred, so its edges follow from its size. Re-run on open
    // because folding the device changes the viewport under a live overlay.
    private void PositionBack()
    {
        if (_back == null)
            return;

        var panel = ViewportRelativeSize();
        var button = _back.CustomMinimumSize;

        // Flush against the screen's left edge, not the panel's. Hanging it off
        // the panel put it in mid-air once the panel narrowed.
        _back.AnchorLeft = 0f;
        _back.AnchorRight = 0f;
        // Hung off the left edge, not flush against it. Thresholding both
        // screenshots shows the game runs 20.9% of the flag's width past the
        // screen: its flag measures 217px across where the sprite's proportions
        // put the whole thing at 274. Sitting ours fully on screen is why it read
        // as about twice the size when it is 1.37x.
        _back.OffsetLeft = -button.X * BackClipRatio;
        _back.OffsetRight = _back.OffsetLeft + button.X;

        _back.AnchorTop = BackCenterY;
        _back.AnchorBottom = BackCenterY;
        _back.OffsetTop = -button.Y * 0.5f;
        _back.OffsetBottom = button.Y * 0.5f;
    }

    public new void Hide() => Visible = false;
}
