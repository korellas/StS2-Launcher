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

        var body = BuildPanel(center, scale);

        var heading = new StyledLabel(title ?? Localization.Tr("DIALOG_CONFIRM_TITLE"), scale, fontSize: 28);
        heading.AddThemeColorOverride("font_color", TitleGold);
        body.AddChild(heading);

        var text = new StyledLabel(message, scale, fontSize: 20);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsVertical = SizeFlags.ExpandFill;
        text.VerticalAlignment = VerticalAlignment.Center;
        body.AddChild(text);

        var buttons = new HBoxContainer();
        body.AddChild(buttons);

        // Decline sits left of accept, matching the game's own prompts.
        var cancel = new GameRibbonButton(Localization.Tr("DIALOG_NO"), scale, confirm: false);
        cancel.Pressed += () =>
        {
            Cancelled?.Invoke();
            QueueFree();
        };
        buttons.AddChild(cancel);

        // A spacer rather than a separation constant: the game pushes its two
        // ribbons to opposite corners of the panel.
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var confirm = new GameRibbonButton(Localization.Tr("DIALOG_YES"), scale, confirm: true);
        confirm.Pressed += () =>
        {
            Confirmed?.Invoke();
            QueueFree();
        };
        buttons.AddChild(confirm);
    }

    private static VBoxContainer BuildPanel(Control parent, float scale)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2((int)(520 * scale), (int)(460 * scale)),
        };

        var texture = GameAssets.Load<Texture2D>(GameAssets.PopupPanel);
        if (texture != null)
        {
            var style = new StyleBoxTexture { Texture = texture };
            float inset = Math.Min(texture.GetWidth(), texture.GetHeight()) / 3f;
            style.SetTextureMarginAll(inset);
            style.SetContentMarginAll(36 * scale);
            panel.AddThemeStyleboxOverride("panel", style);
        }
        else
        {
            var style = LauncherTheme.Panel(scale);
            style.SetContentMarginAll((int)(30 * scale));
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
