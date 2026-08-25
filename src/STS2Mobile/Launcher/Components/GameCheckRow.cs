using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// A settings row drawn the way the game draws them: its checkbox sprite beside
// the label, the whole row tappable.
//
// Built as a plain Button with Icon + Text rather than a Button wrapping its own
// layout. A child container anchored to the button's rect reports no width of
// its own, so inside a GridContainer the cells collapsed to zero and adjacent
// rows drew on top of each other.
public class GameCheckRow : Button
{
    private readonly Texture2D _ticked;
    private readonly Texture2D _unticked;
    private readonly string _caption;
    private readonly bool _hasSprites;

    public GameCheckRow(string caption, float scale, int fontSize = 22)
    {
        _caption = caption;
        ToggleMode = true;
        Flat = true;
        Alignment = HorizontalAlignment.Left;
        CustomMinimumSize = new Vector2(0, (int)(48 * scale));

        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        _ticked = GameAssets.Load<Texture2D>(GameAssets.CheckboxTicked);
        _unticked = GameAssets.Load<Texture2D>(GameAssets.CheckboxUnticked);
        _hasSprites = _ticked != null || _unticked != null;

        if (_hasSprites)
        {
            ExpandIcon = false;
            AddThemeConstantOverride("icon_max_width", (int)(32 * scale));
            AddThemeConstantOverride("h_separation", (int)(14 * scale));
        }

        LauncherTheme.ApplyGameFont(this, fontSize, scale);
        AddThemeColorOverride("font_color", LauncherTheme.Ink);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Ink);
        AddThemeColorOverride("font_pressed_color", LauncherTheme.Ink);

        Toggled += _ => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        if (_hasSprites)
        {
            Icon = ButtonPressed ? _ticked : _unticked;
            Text = _caption;
            return;
        }

        // Without the sprites there is no visual state, so the text carries it.
        Text = $"{_caption}: {(ButtonPressed ? Localization.Tr("STATE_ON") : Localization.Tr("STATE_OFF"))}";
    }
}
