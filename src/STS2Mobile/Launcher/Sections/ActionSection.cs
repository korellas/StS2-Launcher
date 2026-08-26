using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public class ActionSection : VBoxContainer
{
    public event Action LaunchPressed;
    public event Action RetryPressed;
    public event Action<bool> LocalBackupToggled;
    public event Action<bool> CloudSyncToggled;
    public event Action<bool> BetaChannelToggled;
    public event Action<bool> FpsOverlayToggled;
    public event Action<string, bool> OverlayRowToggled;
    public event Action CloudPushPressed;
    public event Action CloudPullPressed;
    public event Action CheckForUpdatesPressed;
    public event Action AppUpdatePressed;

    private readonly Button _launchButton;
    private readonly Button _retryButton;
    private readonly Button _localBackupToggle;
    private readonly Button _cloudSyncToggle;
    private readonly Button _betaChannelToggle;
    private readonly Button _fpsOverlayToggle;
    private readonly Button _pushButton;
    private readonly Button _pullButton;
    private readonly Button _updateButton;
    private readonly Button _appUpdateButton;
    private string _appUpdateBaseText = "UPDATE LAUNCHER";

    // Toggles and cloud actions live in this group so LauncherView can reparent
    // them into a Settings submenu, leaving only PLAY-level actions on the menu.
    public VBoxContainer SettingsGroup { get; }

    private readonly VBoxContainer _rows;
    private readonly System.Collections.Generic.Dictionary<string, GameCheckbox> _overlayRowToggles = new();
    private readonly StyledLabel _cloudStatus;

    public ActionSection(float scale)
    {
        SettingsGroup = new VBoxContainer();
        SettingsGroup.AddThemeConstantOverride("separation", (int)(14 * scale));
        AddChild(SettingsGroup);

        // Rows rather than a grid: the game's settings screen reads as a list of
        // "name … control" lines, and a two-column grid of captioned boxes does not.
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 0);
        SettingsGroup.AddChild(_rows);

        _retryButton = new GameMenuButton(Localization.Tr("MENU_RETRY"), scale, fontSize: 34, primary: true);
        _retryButton.Visible = false;
        _retryButton.Pressed += () => RetryPressed?.Invoke();
        AddChild(_retryButton);


        _localBackupToggle = new GameCheckbox(scale);
        _localBackupToggle.ToggleMode = true;
        _localBackupToggle.Visible = false;
        _localBackupToggle.Toggled += pressed =>
        {
            LocalBackupToggled?.Invoke(pressed);
        };
        AddSettingRow("SETTING_LOCAL_BACKUP", _localBackupToggle, scale);

        _cloudSyncToggle = new GameCheckbox(scale);
        _cloudSyncToggle.ToggleMode = true;
        _cloudSyncToggle.Visible = false;
        _cloudSyncToggle.Toggled += pressed =>
        {
            CloudSyncToggled?.Invoke(pressed);
        };
        AddSettingRow("SETTING_AUTO_SYNC", _cloudSyncToggle, scale);

        // Channel toggle. Off (default) → follow Steam's `public` branch.
        // On → prefer any beta-named branch (e.g. STS2's `public-beta`).
        // The user MUST opt into the same beta channel inside the Steam
        // client first; otherwise GetManifestRequestCode fails with
        // "Ensure the account owns this app" on protected branches.
        _betaChannelToggle = new GameCheckbox(scale);
        _betaChannelToggle.ToggleMode = true;
        _betaChannelToggle.Visible = false;
        _betaChannelToggle.Toggled += pressed =>
        {
            BetaChannelToggled?.Invoke(pressed);
        };
        AddSettingRow("SETTING_BETA_CHANNEL", _betaChannelToggle, scale);

        // Debug aid, so it stays available whether or not Steam is connected.
        _fpsOverlayToggle = new GameCheckbox(scale);
        _fpsOverlayToggle.ToggleMode = true;
        _fpsOverlayToggle.Visible = false;
        _fpsOverlayToggle.Toggled += pressed =>
        {
            FpsOverlayToggled?.Invoke(pressed);
        };
        AddSettingRow("SETTING_FPS_OVERLAY", _fpsOverlayToggle, scale);

        // One switch per line of the overlay, so an unwanted reading can be
        // dropped without losing the rest.
        foreach (var (row, key) in new[]
        {
            ("cpu", "SETTING_OVERLAY_CPU"),
            ("gpu", "SETTING_OVERLAY_GPU"),
            ("temp", "SETTING_OVERLAY_TEMP"),
        })
        {
            var box = new GameCheckbox(scale);
            var rowName = row;
            box.Toggled += pressed => OverlayRowToggled?.Invoke(rowName, pressed);
            _overlayRowToggles[rowName] = box;
            AddSettingRow(key, box, scale);
        }


        // Cloud transfers used to report only into the console; this line keeps
        // the outcome next to the buttons that started it.
        // Small: this line carries the cloud diagnostic, which ran to two
        // screenshots at the size the rest of the settings text uses.
        _cloudStatus = new StyledLabel("", scale, fontSize: 10, align: HorizontalAlignment.Left);
        _cloudStatus.Modulate = new Color(1f, 1f, 1f, 0.75f);
        _cloudStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _cloudStatus.Visible = false;
        SettingsGroup.AddChild(_cloudStatus);

        var pushPullRow = new SettingsRow(Localization.Tr("SETTING_CLOUD_HEADER"), scale);
        pushPullRow.Visible = false;

        _pushButton = new GameMenuButton(Localization.Tr("SETTING_UPLOAD_SAVES"), scale, fontSize: 21);
        _pushButton.Pressed += () => CloudPushPressed?.Invoke();
        pushPullRow.AddControl(_pushButton);

        _pullButton = new GameMenuButton(Localization.Tr("SETTING_DOWNLOAD_SAVES"), scale, fontSize: 21);
        _pullButton.Pressed += () => CloudPullPressed?.Invoke();
        pushPullRow.AddControl(_pullButton);

        _rows.AddChild(pushPullRow);
        _rows.AddChild(SettingsRow.Separator(scale));

        _updateButton = new GameMenuButton(Localization.Tr("SETTING_CHECK_UPDATES"), scale, fontSize: 22);
        _updateButton.Visible = false;
        _updateButton.Pressed += () => CheckForUpdatesPressed?.Invoke();
        var updateRow = new SettingsRow(Localization.Tr("SETTING_UPDATE_ROW"), scale);
        updateRow.AddControl(_updateButton);
        _rows.AddChild(updateRow);

        // No banner above the orange UPDATE LAUNCHER button. Earlier versions
        // had a single-line yellow prompt here, but the user found it noisy
        // and asked us to remove it — the button itself already changes label
        // (e.g. "UPDATE LAUNCHER → v0.3.19", "Downloading… 42%",
        // "TAP TO INSTALL") so a separate prompt was redundant.
        _appUpdateButton = new GameMenuButton(Localization.Tr("MENU_UPDATE_LAUNCHER"), scale, fontSize: 26);
        _appUpdateButton.Visible = false;
        _appUpdateButton.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _appUpdateButton.Pressed += () => AppUpdatePressed?.Invoke();
        AddChild(_appUpdateButton);

        _launchButton = new GameMenuButton(Localization.Tr("MENU_PLAY"), scale, fontSize: 40, primary: true);
        _launchButton.Visible = false;
        _launchButton.Pressed += () => LaunchPressed?.Invoke();
        AddChild(_launchButton);
    }

    public void SetLocalBackupChecked(bool value)
    {
        _localBackupToggle.ButtonPressed = value;
    }

    public void SetCloudSyncChecked(bool value)
    {
        _cloudSyncToggle.ButtonPressed = value;
    }

    public void SetFpsOverlayChecked(bool value)
    {
        _fpsOverlayToggle.ButtonPressed = value;
    }

    public void SetBetaChannelChecked(bool value)
    {
        _betaChannelToggle.ButtonPressed = value;
    }


    private Control PushPullRow => (Control)_pushButton.GetParent();

    public void ShowLaunch(string text, bool showCloudSync, bool showUpdate)
    {
        _launchButton.Text = text;
        _launchButton.Visible = true;
        _localBackupToggle.Visible = showCloudSync;
        _cloudSyncToggle.Visible = showCloudSync;
        _betaChannelToggle.Visible = showCloudSync;
        _fpsOverlayToggle.Visible = true;
        PushPullRow.Visible = showCloudSync;
        _updateButton.Visible = showUpdate;
        _updateButton.Disabled = false;
        _updateButton.Text = Localization.Tr("SETTING_CHECK_UPDATES");
        _retryButton.Visible = false;
    }

    public void ShowRetry()
    {
        _retryButton.Visible = true;
        _launchButton.Visible = false;
        _localBackupToggle.Visible = false;
        _cloudSyncToggle.Visible = false;
        _betaChannelToggle.Visible = false;
        _fpsOverlayToggle.Visible = false;
        PushPullRow.Visible = false;
        _updateButton.Visible = false;
    }

    public void HideAll()
    {
        _launchButton.Visible = false;
        _retryButton.Visible = false;
        _localBackupToggle.Visible = false;
        _cloudSyncToggle.Visible = false;
        _betaChannelToggle.Visible = false;
        _fpsOverlayToggle.Visible = false;
        PushPullRow.Visible = false;
        _updateButton.Visible = false;
        _appUpdateButton.Visible = false;
    }

    private void AddSettingRow(string labelKey, Control control, float scale)
    {
        var row = new SettingsRow(Localization.Tr(labelKey), scale);
        row.AddControl(control);
        _rows.AddChild(row);
        _rows.AddChild(SettingsRow.Separator(scale));
    }

    public void SetOverlayRowChecked(string row, bool value)
    {
        if (_overlayRowToggles.TryGetValue(row, out var box))
            box.ButtonPressed = value;
    }

    public void SetCloudStatus(string text)
    {
        _cloudStatus.Text = text;
        _cloudStatus.Visible = !string.IsNullOrEmpty(text);
    }

    public void SetPushPullDisabled(bool disabled)
    {
        _pushButton.Disabled = disabled;
        _pullButton.Disabled = disabled;
    }

    public void SetUpdateButtonText(string text) => _updateButton.Text = text;

    public void SetUpdateButtonDisabled(bool disabled) => _updateButton.Disabled = disabled;

    public void ShowAppUpdate(string version)
    {
        _appUpdateBaseText = string.IsNullOrEmpty(version)
            ? "UPDATE LAUNCHER"
            : $"UPDATE LAUNCHER → v{version}";
        _appUpdateButton.Text = _appUpdateBaseText;
        _appUpdateButton.Disabled = false;
        _appUpdateButton.Visible = true;
    }

    public void HideAppUpdate()
    {
        _appUpdateButton.Visible = false;
    }

    public void SetAppUpdateProgress(double fraction)
    {
        _appUpdateButton.Disabled = true;
        var pct = (int)System.Math.Round(System.Math.Clamp(fraction, 0, 1) * 100);
        _appUpdateButton.Text = $"Downloading… {pct}%";
    }

    public void SetAppUpdateReadyToInstall()
    {
        _appUpdateButton.Disabled = false;
        _appUpdateButton.Text = Localization.Tr("UPDATE_TAP_TO_INSTALL");
    }

    public void SetAppUpdatePermissionNeeded()
    {
        _appUpdateButton.Disabled = false;
        _appUpdateButton.Text = Localization.Tr("UPDATE_ALLOW_INSTALL");
    }

    public void SetAppUpdateFailed()
    {
        _appUpdateButton.Disabled = false;
        _appUpdateButton.Text = _appUpdateBaseText + " (retry)";
    }
}
