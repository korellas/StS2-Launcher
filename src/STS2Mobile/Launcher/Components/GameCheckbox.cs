using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// Just the box, for use at the right-hand end of a settings row. The label lives
// in the row, so this carries no text of its own.
public class GameCheckbox : Button
{
    private readonly Texture2D _ticked;
    private readonly Texture2D _unticked;

    public GameCheckbox(float scale)
    {
        ToggleMode = true;
        Flat = true;
        CustomMinimumSize = new Vector2((int)(38 * scale), (int)(38 * scale));

        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        _ticked = GameAssets.Load<Texture2D>(GameAssets.CheckboxTicked);
        _unticked = GameAssets.Load<Texture2D>(GameAssets.CheckboxUnticked);

        if (_ticked != null || _unticked != null)
        {
            ExpandIcon = false;
            AddThemeConstantOverride("icon_max_width", (int)(34 * scale));
        }
        else
        {
            // No sprites: fall back to text, since an empty square button would
            // give no indication of state at all.
            LauncherTheme.ApplyGameFont(this, 18, scale);
            AddThemeColorOverride("font_color", LauncherTheme.Cream);
        }

        Toggled += _ => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        if (_ticked != null || _unticked != null)
        {
            Icon = ButtonPressed ? _ticked : _unticked;
            return;
        }

        Text = ButtonPressed ? Localization.Tr("STATE_ON") : Localization.Tr("STATE_OFF");
    }
}
