using System;
using System.IO;
using Godot;

namespace STS2Mobile.Launcher.Components;

// Gives the launcher access to the game's own UI resources.
//
// The launcher boots from a bootstrap PCK holding nothing but project.godot, so
// res:// is empty and everything used to be hand-rolled StyleBoxes that never
// looked like the game. Once the game files are downloaded, its PCK is sitting
// in app storage and can simply be mounted: after that the real menu themes,
// panel textures and fonts load through res:// like any other resource.
//
// Nothing here is required. Before the first download the pack does not exist,
// and a game update could move any of these paths, so every lookup falls back to
// the launcher's own styling and says so in the log rather than failing silently.
public static class GameAssets
{
    public const string MenuButtonTheme = "res://themes/main_menu_text_button.tres";
    public const string SettingsRowTheme = "res://themes/settings_screen_line_header.tres";
    public const string SettingsTabTheme = "res://themes/settings_screen_tab.tres";
    public const string FontRegular = "res://themes/kreon_regular_shared.tres";
    public const string FontBold = "res://themes/kreon_bold_shared.tres";
    public const string FontKorean = "res://themes/fonts/kor/gyeonggi_cheonnyeon_batang_bold_shared.tres";
    public const string SubmenuPanel = "res://images/packed/common_ui/submenu_panel.png";
    public const string SubmenuPanelShort = "res://images/packed/common_ui/submenu_panel_short.png";
    public const string CheckboxTicked = "res://images/atlases/ui_atlas.sprites/checkbox_ticked.tres";
    public const string CheckboxUnticked = "res://images/atlases/ui_atlas.sprites/checkbox_unticked.tres";
    public const string PopupPanel = "res://images/atlases/ui_atlas.sprites/popup_vertical.tres";
    public const string PopupCancelButton = "res://images/atlases/ui_atlas.sprites/popup_cancel_button.tres";
    public const string PopupConfirmButton = "res://images/atlases/ui_atlas.sprites/popup_confirm_button.tres";
    public const string BackButton = "res://images/atlases/ui_atlas.sprites/back_button.tres";

    private static bool _mountAttempted;
    private static bool _mounted;

    public static bool Available
    {
        get
        {
            Mount();
            return _mounted;
        }
    }

    private static void Mount()
    {
        if (_mountAttempted)
            return;
        _mountAttempted = true;

        try
        {
            var pck = Path.Combine(OS.GetDataDir(), "game", "SlayTheSpire2.pck");
            if (!File.Exists(pck))
            {
                PatchHelper.Log("[GameAssets] game pack not downloaded yet, using launcher styling");
                return;
            }

            // replaceFiles: false keeps the bootstrap project.godot in charge, so
            // mounting cannot disturb the engine configuration the launcher runs on.
            _mounted = ProjectSettings.LoadResourcePack(pck, replaceFiles: false);
            PatchHelper.Log(
                _mounted
                    ? "[GameAssets] mounted game pack, using in-game themes"
                    : "[GameAssets] LoadResourcePack refused the game pack"
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[GameAssets] mount failed: {ex.Message}");
        }
    }

    // Logs what actually resolved, so a game update that moves a path shows up as
    // a specific missing resource instead of the UI quietly reverting.
    public static void LogAvailability()
    {
        if (!Available)
            return;

        foreach (var path in new[]
        {
            MenuButtonTheme, SettingsRowTheme, SettingsTabTheme,
            FontRegular, FontBold, FontKorean, SubmenuPanel, SubmenuPanelShort,
        })
        {
            PatchHelper.Log($"[GameAssets] {(ResourceLoader.Exists(path) ? "ok  " : "MISS")} {path}");
        }
    }

    // Dumps what a theme actually defines. Godot resolves theme entries by type
    // name, so guessing the name silently yields an unstyled control — the log is
    // the only way to know what a game theme is keyed on.
    public static void DescribeTheme(string path)
    {
        var theme = Load<Theme>(path);
        if (theme == null)
            return;

        foreach (var type in theme.GetTypeList())
        {
            PatchHelper.Log(
                $"[GameAssets] {path} type='{type}' "
                    + $"fonts=[{string.Join(",", theme.GetFontList(type))}] "
                    + $"colors=[{string.Join(",", theme.GetColorList(type))}] "
                    + $"styles=[{string.Join(",", theme.GetStyleboxList(type))}] "
                    + $"consts=[{string.Join(",", theme.GetConstantList(type))}]"
            );
        }
    }

    public static T Load<T>(string path)
        where T : Resource
    {
        if (!Available)
            return null;

        try
        {
            if (!ResourceLoader.Exists(path))
            {
                PatchHelper.Log($"[GameAssets] missing after update: {path}");
                return null;
            }

            return ResourceLoader.Load<T>(path);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[GameAssets] load failed for {path}: {ex.Message}");
            return null;
        }
    }
}
