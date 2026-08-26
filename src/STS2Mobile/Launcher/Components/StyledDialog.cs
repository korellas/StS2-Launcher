using System;
using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// Modal confirmation, drawn as the game draws its own: the stone popup panel, a
// gold title, and the red/green ribbon buttons at the foot. Falls back to a plain
// rounded panel when the game pack isn't mounted.
public class StyledDialog : ColorRect
{
    public event Action Confirmed;
    public event Action Cancelled;

    private static readonly Color TitleGold = new(0.96f, 0.76f, 0.31f);

    public StyledDialog(string message, float scale, string title = null)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Color = new Color(0, 0, 0, 0.62f);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        // The panel and the ribbons are siblings inside a fixed-size frame, so the
        // ribbons can hang over the panel's bottom edge the way the game draws
        // them. Kept inside the panel they sat in a tidy row well above it.
        var size = new Vector2((int)(560 * scale), (int)(380 * scale));
        var frame = new Control { CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Ignore };
        center.AddChild(frame);

        var body = BuildPanel(frame, scale);

        var heading = new StyledLabel(title ?? Localization.Tr("DIALOG_CONFIRM_TITLE"), scale, fontSize: 28);
        heading.AddThemeColorOverride("font_color", TitleGold);
        body.AddChild(heading);

        var text = new StyledLabel(message, scale, fontSize: 20);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsVertical = SizeFlags.ExpandFill;
        text.VerticalAlignment = VerticalAlignment.Center;
        body.AddChild(text);

        // Decline sits left of accept, matching the game's own prompts.
        AddRibbon(frame, scale, confirm: false, Localization.Tr("DIALOG_NO"), () =>
        {
            Cancelled?.Invoke();
            QueueFree();
        });
        AddRibbon(frame, scale, confirm: true, Localization.Tr("DIALOG_YES"), () =>
        {
            Confirmed?.Invoke();
            QueueFree();
        });
    }

    private static void AddRibbon(Control frame, float scale, bool confirm, string label, Action onPressed)
    {
        var button = new GameRibbonButton(label, scale, confirm)
        {
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowVertical = GrowDirection.Both,
            // Straddles the panel's lower edge rather than sitting inside it.
            OffsetTop = (int)(-84 * scale),
            OffsetBottom = (int)(-26 * scale),
        };

        if (confirm)
        {
            button.AnchorLeft = 1f;
            button.AnchorRight = 1f;
            button.GrowHorizontal = GrowDirection.Begin;
            button.OffsetRight = (int)(-24 * scale);
            button.OffsetLeft = (int)(-214 * scale);
        }
        else
        {
            button.OffsetLeft = (int)(24 * scale);
            button.OffsetRight = (int)(214 * scale);
        }

        button.Pressed += onPressed;
        frame.AddChild(button);
    }

    private static VBoxContainer BuildPanel(Control parent, float scale)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);

        var texture = GameAssets.Load<Texture2D>(GameAssets.PopupPanel);
        if (texture != null)
        {
            var style = new StyleBoxTexture { Texture = texture };
            float inset = Math.Min(texture.GetWidth(), texture.GetHeight()) / 3f;
            style.SetTextureMarginAll(inset);
            style.SetContentMarginAll(36 * scale);
            style.ContentMarginBottom = 96 * scale;
            panel.AddThemeStyleboxOverride("panel", style);
        }
        else
        {
            var style = LauncherTheme.Panel(scale);
            style.SetContentMarginAll((int)(30 * scale));
            style.ContentMarginBottom = (int)(78 * scale);
            panel.AddThemeStyleboxOverride("panel", style);
        }

        parent.AddChild(panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", (int)(18 * scale));
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.AddChild(body);
        return body;
    }
}
