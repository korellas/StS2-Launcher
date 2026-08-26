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

    // Every figure below is measured off the game's quit prompt rather than
    // guessed, and expressed as a fraction so it holds at any resolution.
    private const float PanelWidthRatio = 0.320f;
    private const float PanelHeightRatio = 0.527f;
    private const float RibbonTop = 0.843f;
    private const float RibbonBottom = 0.960f;
    private const float RibbonInnerEdgeLeft = 0.335f;
    private const float RibbonInnerEdgeRight = 0.695f;


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
        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        // Measured from the game's own quit prompt: 32.0% of the screen wide and
        // 52.7% tall, which is very slightly portrait rather than the landscape
        // box this used to be.
        var size = new Vector2((int)(vp.X * PanelWidthRatio), (int)(vp.Y * PanelHeightRatio));
        var frame = new Control { CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Ignore };
        center.AddChild(frame);

        var body = BuildPanel(frame, scale, size);

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
        // Anchored on both axes: the ribbons sit inside the panel's lower area,
        // each a third of its width, not straddling its border as they were.
        var button = new GameRibbonButton(label, scale, confirm)
        {
            AnchorTop = RibbonTop,
            AnchorBottom = RibbonBottom,
            AnchorLeft = confirm ? RibbonInnerEdgeRight : 0f,
            AnchorRight = confirm ? 1f : RibbonInnerEdgeLeft,
            OffsetLeft = 0,
            OffsetRight = 0,
            OffsetTop = 0,
            OffsetBottom = 0,
        };

        button.Pressed += onPressed;
        frame.AddChild(button);
    }

    private static VBoxContainer BuildPanel(Control parent, float scale, Vector2 size)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);

        var texture = GameAssets.Load<Texture2D>(GameAssets.PopupPanel);
        if (texture != null)
        {
            var style = new StyleBoxTexture { Texture = texture };
            float inset = Math.Min(texture.GetWidth(), texture.GetHeight()) / 3f;
            style.SetTextureMarginAll(inset);
            style.SetContentMarginAll(34 * scale);
            style.ContentMarginBottom = size.Y * (1f - RibbonTop) + 16 * scale;
            style.ModulateColor = LauncherTheme.PanelSlate;
            panel.AddThemeStyleboxOverride("panel", style);
        }
        else
        {
            var style = LauncherTheme.Panel(scale);
            style.SetContentMarginAll((int)(30 * scale));
            style.ContentMarginBottom = (int)(size.Y * (1f - RibbonTop) + 16 * scale);
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
