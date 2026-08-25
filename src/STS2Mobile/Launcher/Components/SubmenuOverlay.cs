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

    public SubmenuOverlay(string title, float scale, float widthRatio = 0.68f, float heightRatio = 0.86f)
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

        var close = new GameBackButton(scale);
        close.Pressed += Hide;
        header.AddChild(close);
    }

    // NinePatchRect over the game's panel art when available; the launcher's own
    // rounded panel otherwise.
    // Adds the frame to `parent` and returns the container callers should fill;
    // the two are different nodes, so the frame cannot simply be returned.
    private Container BuildFrame(Control parent, float scale, float widthRatio, float heightRatio)
    {
        _widthRatio = widthRatio;
        _heightRatio = heightRatio;

        var size = ViewportRelativeSize();
        int pad = (int)(28 * scale);

        var texture = GameAssets.Load<Texture2D>(GameAssets.PopupPanel);
        if (texture != null)
        {
            var patch = new NinePatchRect
            {
                Texture = texture,
                CustomMinimumSize = size,
                MouseFilter = MouseFilterEnum.Stop,
            };
            int inset = (int)(Math.Min(texture.GetWidth(), texture.GetHeight()) / 3f);
            patch.PatchMarginLeft = inset;
            patch.PatchMarginRight = inset;
            patch.PatchMarginTop = inset;
            patch.PatchMarginBottom = inset;

            var margin = new MarginContainer();
            foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                margin.AddThemeConstantOverride(side, pad);
            patch.AddChild(margin);
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            parent.AddChild(patch);
            _frame = patch;
            parent.AddChild(patch);

            var holder = new VBoxContainer();
            holder.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            holder.SizeFlagsVertical = SizeFlags.ExpandFill;
            margin.AddChild(holder);

            return holder;
        }

        var panel = new PanelContainer { CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Stop };
        parent.AddChild(panel);
        _frame = panel;
        parent.AddChild(panel);
        panel.AddThemeStyleboxOverride("panel", LauncherTheme.Panel(scale));
        var fallbackMargin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            fallbackMargin.AddThemeConstantOverride(side, pad);
        panel.AddChild(fallbackMargin);

        var box = new VBoxContainer();
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.SizeFlagsVertical = SizeFlags.ExpandFill;
        fallbackMargin.AddChild(box);

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
