using System;
using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// A submenu drawn on the game's own submenu_panel texture, opened from a menu
// entry and hidden otherwise. The console, news and settings all live in one of
// these instead of being permanently on screen.
public class SubmenuOverlay : Control
{
    public VBoxContainer Content { get; }

    private readonly ColorRect _scrim;

    public SubmenuOverlay(string title, float scale, float widthRatio = 0.68f, float heightRatio = 0f)
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

        var frame = BuildFrame(center, scale, widthRatio, heightRatio);

        Content = new VBoxContainer();
        Content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Content.SizeFlagsVertical = SizeFlags.ExpandFill;
        Content.AddThemeConstantOverride("separation", (int)(10 * scale));
        frame.AddChild(Content);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", (int)(8 * scale));
        Content.AddChild(header);
        Content.MoveChild(header, 0);

        var titleLabel = new StyledLabel(title, scale, fontSize: 26, align: HorizontalAlignment.Left);
        titleLabel.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(titleLabel);

        var close = new GameBackButton(scale)
        {
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = GrowDirection.End,
            GrowVertical = GrowDirection.Both,
            OffsetLeft = (int)(28 * scale),
        };
        close.Pressed += Hide;
        AddChild(close);
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

    private Vector2 ViewportRelativeSize()
    {
        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        // A zero height ratio means "fit the content": the settings panel had a
        // fixed 86% height and stood mostly empty.
        return new Vector2((int)(vp.X * _widthRatio), (int)(vp.Y * _heightRatio));
    }

    // Folding the device changes the viewport without recreating the activity, so
    // the panel has to re-read its size rather than keep the one it was built with.
    public void Open()
    {
        if (_frame != null)
            _frame.CustomMinimumSize = ViewportRelativeSize();
        Visible = true;
    }

    public new void Hide() => Visible = false;
}
