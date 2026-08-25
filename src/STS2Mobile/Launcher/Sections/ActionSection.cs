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

    public ActionSection(float scale)
    {
        SettingsGroup = new VBoxContainer();
        SettingsGroup.AddThemeConstantOverride("separation", (int)(8 * scale));
        AddChild(SettingsGroup);

        _retryButton = new GameMenuButton("RETRY", scale, fontSize: 34, primary: true);
        _retryButton.Visible = false;
        _retryButton.Pressed += () => RetryPressed?.Invoke();
        AddChild(_retryButton);


        _localBackupToggle = new GameCheckRow("Local Backup", scale);
        _localBackupToggle.ToggleMode = true;
        _localBackupToggle.Visible = false;
        _localBackupToggle.Toggled += pressed =>
        {
            LocalBackupToggled?.Invoke(pressed);
        };
        SettingsGroup.AddChild(_localBackupToggle);

        _cloudSyncToggle = new GameCheckRow("Auto Sync", scale);
        _cloudSyncToggle.ToggleMode = true;
        _cloudSyncToggle.Visible = false;
        _cloudSyncToggle.Toggled += pressed =>
        {
            CloudSyncToggled?.Invoke(pressed);
        };
        SettingsGroup.AddChild(_cloudSyncToggle);

        // Channel toggle. Off (default) → follow Steam's `public` branch.
        // On → prefer any beta-named branch (e.g. STS2's `public-beta`).
        // The user MUST opt into the same beta channel inside the Steam
        // client first; otherwise GetManifestRequestCode fails with
        // "Ensure the account owns this app" on protected branches.
        _betaChannelToggle = new GameCheckRow("Beta Channel", scale);
        _betaChannelToggle.ToggleMode = true;
        _betaChannelToggle.Visible = false;
        _betaChannelToggle.Toggled += pressed =>
        {
            BetaChannelToggled?.Invoke(pressed);
        };
        SettingsGroup.AddChild(_betaChannelToggle);

        // Debug aid, so it stays available whether or not Steam is connected.
        _fpsOverlayToggle = new GameCheckRow("FPS Overlay", scale);
        _fpsOverlayToggle.ToggleMode = true;
        _fpsOverlayToggle.Visible = false;
        _fpsOverlayToggle.Toggled += pressed =>
        {
            FpsOverlayToggled?.Invoke(pressed);
        };
        SettingsGroup.AddChild(_fpsOverlayToggle);

        var pushPullRow = new HBoxContainer();
        pushPullRow.Visible = false;
        pushPullRow.AddThemeConstantOverride("separation", (int)(6 * scale));

        _pushButton = new GameMenuButton("Push to Cloud", scale, fontSize: 22, onParchment: true);
        _pushButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pushButton.Pressed += () => CloudPushPressed?.Invoke();
        pushPullRow.AddChild(_pushButton);

        _pullButton = new GameMenuButton("Pull from Cloud", scale, fontSize: 22, onParchment: true);
        _pullButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pullButton.Pressed += () => CloudPullPressed?.Invoke();
        pushPullRow.AddChild(_pullButton);

        SettingsGroup.AddChild(pushPullRow);

        _updateButton = new GameMenuButton("Check for Updates", scale, fontSize: 22, onParchment: true);
        _updateButton.Visible = false;
        _updateButton.Pressed += () => CheckForUpdatesPressed?.Invoke();
        SettingsGroup.AddChild(_updateButton);

        // No banner above the orange UPDATE LAUNCHER button. Earlier versions
        // had a single-line yellow prompt here, but the user found it noisy
        // and asked us to remove it — the button itself already changes label
        // (e.g. "UPDATE LAUNCHER → v0.3.19", "Downloading… 42%",
        // "TAP TO INSTALL") so a separate prompt was redundant.
        _appUpdateButton = new GameMenuButton("UPDATE LAUNCHER", scale, fontSize: 26);
        _appUpdateButton.Visible = false;
        _appUpdateButton.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _appUpdateButton.Pressed += () => AppUpdatePressed?.Invoke();
        AddChild(_appUpdateButton);

        _launchButton = new GameMenuButton("PLAY", scale, fontSize: 40, primary: true);
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


    private HBoxContainer PushPullRow => (HBoxContainer)_pushButton.GetParent();

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
        _updateButton.Text = "CHECK FOR UPDATES";
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
        _appUpdateButton.Text = "TAP TO INSTALL";
    }

    public void SetAppUpdatePermissionNeeded()
    {
        _appUpdateButton.Disabled = false;
        _appUpdateButton.Text = "ALLOW INSTALL IN SETTINGS";
    }

    public void SetAppUpdateFailed()
    {
        _appUpdateButton.Disabled = false;
        _appUpdateButton.Text = _appUpdateBaseText + " (retry)";
    }
}
