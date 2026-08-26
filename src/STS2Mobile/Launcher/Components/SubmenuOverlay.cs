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

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", (int)(-18 * scale));
        center.AddChild(row);

        var back = new GameBackButton(scale) { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        back.Pressed += Hide;
        row.AddChild(back);

        var frame = BuildFrame(row, scale, widthRatio, heightRatio);

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

        Content = new VBoxContainer();
        Content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Content.SizeFlagsVertical = SizeFlags.ExpandFill;
        Content.AddThemeConstantOverride("separation", (int)(10 * scale));
        scroll.AddChild(Content);

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
