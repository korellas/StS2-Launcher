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
    // One width for both ribbons. They used to carry an inner edge each — 0.335
    // on the left and 0.695 on the right — which are not mirror images, so the
    // decline ribbon came out 10% wider than the accept one. Deriving both from
    // a single figure makes that class of drift impossible.
    private const float RibbonWidthRatio = 0.320f;


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
        AddRibbon(frame, size, scale, confirm: false, Localization.Tr("DIALOG_NO"), () =>
        {
            Cancelled?.Invoke();
            QueueFree();
        });
        AddRibbon(frame, size, scale, confirm: true, Localization.Tr("DIALOG_YES"), () =>
        {
            Confirmed?.Invoke();
            QueueFree();
        });
    }

    private static void AddRibbon(
        Control frame,
        Vector2 panelSize,
        float scale,
        bool confirm,
        string label,
        Action onPressed
    )
    {
        var button = new GameRibbonButton(label, scale, confirm);

        // Height follows the artwork's own aspect rather than the anchor band.
        // Stretching a ribbon into a box of unrelated proportions is what made
        // these read as subtly wrong even once the widths agreed.
        float width = panelSize.X * RibbonWidthRatio;
        float height = button.SpriteSize.X > 0
            ? width * button.SpriteSize.Y / button.SpriteSize.X
            : panelSize.Y * (RibbonBottom - RibbonTop);

        // Pinned to opposite panel edges at a shared centre line, so the pair is
        // symmetric by construction at any viewport size.
        float centerY = (RibbonTop + RibbonBottom) * 0.5f;
        button.AnchorTop = centerY;
        button.AnchorBottom = centerY;
        button.OffsetTop = -height * 0.5f;
        button.OffsetBottom = height * 0.5f;

        button.AnchorLeft = confirm ? 1f : 0f;
        button.AnchorRight = confirm ? 1f : 0f;
        button.OffsetLeft = confirm ? -width : 0f;
        button.OffsetRight = confirm ? 0f : width;

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
