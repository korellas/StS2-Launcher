using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using STS2Mobile;
using STS2Mobile.Launcher;
using STS2Mobile.Patches;

void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}
Directory.CreateDirectory(Path.GetDirectoryName(GraphicsSettings.Path)!);
using (var legacy = new ConfigFile())
{
    legacy.SetValue("mobile_graphics", "msaa", 8);
    legacy.SetValue("mobile_graphics", "render_scale", 85);
    legacy.SetValue("mobile_graphics", "frame_pacing", 2);
    legacy.SetValue("other", "keep", 42);
    Check(legacy.Save(GraphicsSettings.Path) == Error.Ok, "Write legacy configuration");
}
var settings = GraphicsSettings.Load();
Check(settings.RenderScale == 85 && settings.FramePacing == 2, "Retained settings load normally");
settings.ApplyPreset(GraphicsPreset.Quality);
Check(settings.Save(), "Save revised mobile preferences");
using (var saved = new ConfigFile())
{
    saved.Load(GraphicsSettings.Path);
    Check(!saved.HasSectionKey("mobile_graphics", "msaa"), "Remove legacy launcher MSAA on save");
    Check(saved.GetValue("other", "keep", 0).AsInt32() == 42, "Preserve unrelated configuration");
    Check(
        saved.GetValue("display", "window/frame_pacing/android/swappy_mode", 0).AsInt32() == 2,
        "Mobile presets preserve native pacing"
    );
}
SettingsPatches.Apply(new HarmonyLib.Harmony());
Check(
    !PatchHelper.Calls.Any(call =>
        call.Method == "InitializeGraphicsPreferences" || call.Type.Name == "NFpsPaginator"
    ),
    "Normal game FPS initialization and UI changes must not install a launcher override"
);
Check(
    PatchHelper.Calls.Any(call => call.Method == "InitSettingsData"),
    "Retain mobile first-launch initialization"
);
Console.WriteLine("PASS legacy MSAA cleanup, retained preferences and native FPS ownership");

var markerPath = Path.Combine(OS.GetUserDataDir(), ".mobile_defaults_applied");
SaveManager.Instance = new SaveManager(new MegaCrit.Sts2.Core.Saves.Test.MockGodotFileIo());
SettingsPatches.InitSettingsDataPostfix();
Check(!File.Exists(markerPath), "A benchmark must not mark real mobile defaults as initialized");
Check(SaveManager.Instance.SaveCount == 0, "A benchmark must not run real first-launch setup");

SaveManager.Instance = new SaveManager();
SettingsPatches.InitSettingsDataPostfix();
Check(File.Exists(markerPath), "Normal PLAY still applies mobile defaults after a benchmark");
Check(
    SaveManager.Instance.SettingsSave.AspectRatioSetting == AspectRatioSetting.Auto
        && SaveManager.Instance.SaveCount == 1,
    "Normal PLAY persists the automatic aspect ratio"
);

var markerBytes = File.ReadAllBytes(markerPath);
typeof(SettingsPatches)
    .GetField(
        "_mobileDefaultsChecked",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
    )!
    .SetValue(null, false);
SaveManager.Instance = new SaveManager(new MegaCrit.Sts2.Core.Saves.Test.MockGodotFileIo());
SettingsPatches.InitSettingsDataPostfix();
Check(
    File.ReadAllBytes(markerPath).SequenceEqual(markerBytes),
    "A benchmark preserves an existing marker"
);
SaveManager.Instance = new SaveManager();
SaveManager.Instance.SettingsSave.AspectRatioSetting = AspectRatioSetting.SixteenByNine;
SaveManager.Instance.SettingsSave.Msaa = 4;
SettingsPatches.InitSettingsDataPostfix();
Check(
    SaveManager.Instance.SettingsSave.AspectRatioSetting == AspectRatioSetting.SixteenByNine
        && SaveManager.Instance.SettingsSave.Msaa == 4
        && SaveManager.Instance.SaveCount == 0,
    "A benchmark followed by PLAY preserves existing user preferences"
);
Console.WriteLine(
    "PASS benchmark-first mobile defaults isolation and existing preference preservation"
);

namespace Godot
{
    public enum Error
    {
        Ok,
        Failed,
    }

    public readonly struct Variant(object value)
    {
        public enum Type
        {
            Int,
            Bool,
        }

        public Type VariantType => value is bool ? Type.Bool : Type.Int;

        public int AsInt32() => Convert.ToInt32(value);

        public bool AsBool() => Convert.ToBoolean(value);
    }

    public static class OS
    {
        public static string GetDataDir() =>
            Environment.GetEnvironmentVariable("STS2_GRAPHICS_TEST_DATA")!;

        public static string GetUserDataDir() => GetDataDir();
    }

    public sealed class ConfigFile : IDisposable
    {
        private Dictionary<string, Dictionary<string, object>> _values = new();

        public Error Load(string path)
        {
            if (!File.Exists(path))
                return Error.Failed;
            var parsed = System.Text.Json.JsonSerializer.Deserialize<
                Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>
            >(File.ReadAllText(path))!;
            _values = parsed.ToDictionary(
                section => section.Key,
                section =>
                    section.Value.ToDictionary(
                        item => item.Key,
                        item =>
                            item.Value.ValueKind
                                is System.Text.Json.JsonValueKind.True
                                    or System.Text.Json.JsonValueKind.False
                                ? (object)item.Value.GetBoolean()
                                : item.Value.GetInt32()
                    )
            );
            return Error.Ok;
        }

        public Error Save(string path)
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(_values));
            return Error.Ok;
        }

        public void SetValue(string section, string key, object value)
        {
            if (!_values.TryGetValue(section, out var values))
                _values[section] = values = new();
            values[key] = value;
        }

        public Variant GetValue(string section, string key, object fallback) =>
            new(
                _values.TryGetValue(section, out var values)
                && values.TryGetValue(key, out var value)
                    ? value
                    : fallback
            );

        public bool HasSectionKey(string section, string key) =>
            _values.TryGetValue(section, out var values) && values.ContainsKey(key);

        public void EraseSectionKey(string section, string key) => _values[section].Remove(key);

        public void Dispose() { }
    }
}

namespace HarmonyLib
{
    public sealed class Harmony { }

    public static class AccessTools
    {
        public static System.Reflection.FieldInfo Field(Type type, string name) =>
            type.GetField(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            )!;
    }
}

namespace MegaCrit.Sts2.Core.Nodes
{
    public sealed class NGame { }
}

namespace MegaCrit.Sts2.Core.Nodes.Screens.Settings
{
    public sealed class NFpsPaginator { }

    public sealed class NVSyncPaginator { }
}

namespace MegaCrit.Sts2.Core.Settings
{
    public enum VSyncType
    {
        On,
    }

    public enum AspectRatioSetting
    {
        Auto,
        SixteenByNine,
    }
}

namespace MegaCrit.Sts2.Core.Saves
{
    public sealed class SaveManager
    {
        private readonly object _saveStore;

        public SaveManager(object saveStore = null) => _saveStore = saveStore ?? new object();

        public static SaveManager Instance { get; set; } = new();
        public SettingsSave SettingsSave { get; } = new();
        public int SaveCount { get; private set; }

        public void SaveSettings() => SaveCount++;
    }

    public sealed class SettingsSave
    {
        public MegaCrit.Sts2.Core.Settings.VSyncType VSync { get; set; }
        public MegaCrit.Sts2.Core.Settings.AspectRatioSetting AspectRatioSetting { get; set; } =
            MegaCrit.Sts2.Core.Settings.AspectRatioSetting.SixteenByNine;
        public int Msaa { get; set; }
    }
}

namespace MegaCrit.Sts2.Core.Saves.Test
{
    public sealed class MockGodotFileIo { }
}

namespace STS2Mobile
{
    public static class PatchHelper
    {
        public static List<(Type Type, string Method)> Calls { get; } = new();

        public static void Patch(
            HarmonyLib.Harmony harmony,
            Type type,
            string method,
            object prefix = null,
            object postfix = null
        ) => Calls.Add((type, method));

        public static object Method(Type type, string name) => name;

        public static void Log(string message) { }
    }
}
