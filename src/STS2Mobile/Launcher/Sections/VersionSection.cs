using System;
using System.IO;
using System.Text;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher.Sections;

public class VersionSection : VBoxContainer
{
    private readonly VBoxContainer _rows = new();
    private readonly float _scale;
    private string _details;

    public VersionSection(float scale)
    {
        _scale = scale;
        AddChild(SettingsRow.Separator(scale));
        var title = new StyledLabel(
            Localization.Tr("VERSION_TITLE"),
            scale,
            20,
            HorizontalAlignment.Left
        );
        title.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        AddChild(title);
        AddChild(_rows);
        var copy = new GameMenuButton(Localization.Tr("VERSION_COPY"), scale, fontSize: 16);
        copy.Pressed += () => DisplayServer.ClipboardSet(_details);
        AddChild(copy);
        Refresh();
    }

    public void Refresh()
    {
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        var details = new StringBuilder();
        Add("VERSION_LAUNCHER", AppUpdateChecker.GetInstalledVersion());
        var game = LibraryVersions.ReadGameRelease(
            Path.Combine(OS.GetDataDir(), "game", "release_info.json")
        );
        Add("VERSION_GAME_FILES", game == null ? null : $"{game.Version} · {game.Commit}");
        AddLibrary("VERSION_GAME_DLL", "sts2");
        Add("VERSION_ENGINE", Engine.GetVersionInfo()["string"].AsString());
        Add("VERSION_RUNTIME", System.Environment.Version.ToString());
        AddLibrary("GodotSharp", "GodotSharp");
        AddLibrary("SteamKit2", "SteamKit2");
        AddLibrary("Harmony", "0Harmony");
        _details = details.ToString();

        void AddLibrary(string label, string name)
        {
            var info = LibraryVersions.ReadLoaded(name);
            Add(
                label,
                info == null ? null : $"{info.Version} · {info.ModuleId[..8]}",
                info == null ? null : $"{info.Version} · MVID {info.ModuleId}"
            );
        }
        void Add(string key, string value, string full = null)
        {
            var caption = Localization.Tr(key);
            value ??= Localization.Tr("VERSION_UNAVAILABLE");
            var row = new SettingsRow(caption, _scale, fontSize: 16);
            var text = new StyledLabel(value, _scale, 14, HorizontalAlignment.Right)
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddControl(text);
            _rows.AddChild(row);
            details.AppendLine($"{caption}: {full ?? value}");
        }
    }
}
