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
    // Measured off the game's own quit prompt: the panel is 32.6% of the screen
    // wide and 48.3% tall, and each ribbon is 35.3% of the panel's width sitting
    // at 83.8% of its height.
    private const float PanelWidthRatio = 0.326f;
    private const float PanelHeightRatio = 0.483f;
    private const float RibbonCenterY = 0.838f;

    // The label may occupy at most this much of the ribbon before the ribbon is
    // widened to keep it clear of the tapered ends.
    private const float MaxLabelFill = 0.55f;

    // Keeps the body clear of the ribbons: their centre line plus half a ribbon.
    private const float BodyBottomRatio = 0.205f;

    // One width for both ribbons. They used to carry an inner edge each — 0.335
    // on the left and 0.695 on the right — which are not mirror images, so the
    // decline ribbon came out 10% wider than the accept one. Deriving both from
    // a single figure makes that class of drift impossible.
    private const float RibbonWidthRatio = 0.353f;

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

        var heading = new StyledLabel(
            title ?? Localization.Tr("DIALOG_CONFIRM_TITLE"),
            scale,
            fontSize: 28
        );
        heading.AddThemeColorOverride("font_color", TitleGold);
        body.AddChild(heading);

        var text = new StyledLabel(message, scale, fontSize: 20);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsVertical = SizeFlags.ExpandFill;
        text.VerticalAlignment = VerticalAlignment.Center;
        body.AddChild(text);

        // Decline sits left of accept, matching the game's own prompts. Both are
        // built before either is placed so they can share one width.
        var decline = new GameRibbonButton(Localization.Tr("DIALOG_NO"), scale, confirm: false);
        decline.Pressed += () =>
        {
            Cancelled?.Invoke();
            QueueFree();
        };

        var accept = new GameRibbonButton(Localization.Tr("DIALOG_YES"), scale, confirm: true);
        accept.Pressed += () =>
        {
            Confirmed?.Invoke();
            QueueFree();
        };

        PlaceRibbons(frame, size, decline, accept);
    }

    private static void PlaceRibbons(
        Control frame,
        Vector2 panelSize,
        GameRibbonButton decline,
        GameRibbonButton accept
    )
    {
        // One width for both, taken from whichever label needs more room. Sizing
        // each to its own text is what made the pair asymmetric; sizing both to a
        // fixed fraction is what truncated the longer one.
        float needed =
            Mathf.Max(decline.MeasuredTextWidth, accept.MeasuredTextWidth) / MaxLabelFill;
        float width = Mathf.Max(panelSize.X * RibbonWidthRatio, needed);

        foreach (var (button, confirm) in new[] { (decline, false), (accept, true) })
        {
            // Height follows the artwork's own aspect rather than an anchor band,
            // so the ribbon is never stretched into unrelated proportions.
            float height =
                button.SpriteSize.X > 0
                    ? width * button.SpriteSize.Y / button.SpriteSize.X
                    : panelSize.Y * 0.117f;

            button.AnchorTop = RibbonCenterY;
            button.AnchorBottom = RibbonCenterY;
            button.OffsetTop = -height * 0.5f;
            button.OffsetBottom = height * 0.5f;

            button.AnchorLeft = confirm ? 1f : 0f;
            button.AnchorRight = confirm ? 1f : 0f;
            button.OffsetLeft = confirm ? -width : 0f;
            button.OffsetRight = confirm ? 0f : width;

            frame.AddChild(button);
        }
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
            style.ContentMarginBottom = size.Y * BodyBottomRatio + 16 * scale;
            style.ModulateColor = LauncherTheme.PanelSlate;
            panel.AddThemeStyleboxOverride("panel", style);
        }
        else
        {
            var style = LauncherTheme.Panel(scale);
            style.SetContentMarginAll((int)(30 * scale));
            style.ContentMarginBottom = (int)(size.Y * BodyBottomRatio + 16 * scale);
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
