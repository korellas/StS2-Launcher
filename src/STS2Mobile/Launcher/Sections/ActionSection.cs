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
    private readonly StyledButton _localBackupToggle;
    private readonly StyledButton _cloudSyncToggle;
    private readonly StyledButton _betaChannelToggle;
    private readonly StyledButton _fpsOverlayToggle;
    private readonly Button _pushButton;
    private readonly Button _pullButton;
    private readonly Button _updateButton;
    private readonly StyledButton _appUpdateButton;
    private string _appUpdateBaseText = "UPDATE LAUNCHER";
    private readonly StyleBoxFlat _offStyle;
    private readonly StyleBoxFlat _onStyle;

    public ActionSection(float scale)
    {
        _retryButton = new StyledButton("RETRY", scale);
        _retryButton.Visible = false;
        _retryButton.Pressed += () => RetryPressed?.Invoke();
        AddChild(_retryButton);

        var r = (int)(4 * scale);
        var bw = System.Math.Max(1, (int)(2 * scale));
        _offStyle = StyledButton.MakeOutline(new Color(0.7f, 0.25f, 0.25f), r, bw);
        _onStyle = StyledButton.MakeOutline(new Color(0.25f, 0.65f, 0.3f), r, bw);

        _localBackupToggle = new StyledButton("Local Backup: OFF", scale, fontSize: 14, height: 44);
        _localBackupToggle.ToggleMode = true;
        _localBackupToggle.Visible = false;
        ApplyToggleStyle(_localBackupToggle, false);
        _localBackupToggle.Toggled += pressed =>
        {
            _localBackupToggle.Text = pressed ? "Local Backup: ON" : "Local Backup: OFF";
            ApplyToggleStyle(_localBackupToggle, pressed);
            LocalBackupToggled?.Invoke(pressed);
        };
        AddChild(_localBackupToggle);

        _cloudSyncToggle = new StyledButton("Auto Sync: OFF", scale, fontSize: 14, height: 44);
        _cloudSyncToggle.ToggleMode = true;
        _cloudSyncToggle.Visible = false;
        ApplyToggleStyle(_cloudSyncToggle, false);
        _cloudSyncToggle.Toggled += pressed =>
        {
            _cloudSyncToggle.Text = pressed ? "Auto Sync: ON" : "Auto Sync: OFF";
            ApplyToggleStyle(_cloudSyncToggle, pressed);
            CloudSyncToggled?.Invoke(pressed);
        };
        AddChild(_cloudSyncToggle);

        // Channel toggle. Off (default) → follow Steam's `public` branch.
        // On → prefer any beta-named branch (e.g. STS2's `public-beta`).
        // The user MUST opt into the same beta channel inside the Steam
        // client first; otherwise GetManifestRequestCode fails with
        // "Ensure the account owns this app" on protected branches.
        _betaChannelToggle = new StyledButton(
            "Beta Channel: OFF",
            scale,
            fontSize: 14,
            height: 44
        );
        _betaChannelToggle.ToggleMode = true;
        _betaChannelToggle.Visible = false;
        ApplyToggleStyle(_betaChannelToggle, false);
        _betaChannelToggle.Toggled += pressed =>
        {
            _betaChannelToggle.Text = pressed ? "Beta Channel: ON" : "Beta Channel: OFF";
            ApplyToggleStyle(_betaChannelToggle, pressed);
            BetaChannelToggled?.Invoke(pressed);
        };
        AddChild(_betaChannelToggle);

        // Debug aid, so it stays available whether or not Steam is connected.
        _fpsOverlayToggle = new StyledButton("FPS Overlay: OFF", scale, fontSize: 14, height: 44);
        _fpsOverlayToggle.ToggleMode = true;
        _fpsOverlayToggle.Visible = false;
        ApplyToggleStyle(_fpsOverlayToggle, false);
        _fpsOverlayToggle.Toggled += pressed =>
        {
            _fpsOverlayToggle.Text = pressed ? "FPS Overlay: ON" : "FPS Overlay: OFF";
            ApplyToggleStyle(_fpsOverlayToggle, pressed);
            FpsOverlayToggled?.Invoke(pressed);
        };
        AddChild(_fpsOverlayToggle);

        var pushPullRow = new HBoxContainer();
        pushPullRow.Visible = false;
        pushPullRow.AddThemeConstantOverride("separation", (int)(6 * scale));

        _pushButton = new StyledButton("Push to Cloud", scale, fontSize: 14, height: 44);
        _pushButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pushButton.Pressed += () => CloudPushPressed?.Invoke();
        pushPullRow.AddChild(_pushButton);

        _pullButton = new StyledButton("Pull from Cloud", scale, fontSize: 14, height: 44);
        _pullButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pullButton.Pressed += () => CloudPullPressed?.Invoke();
        pushPullRow.AddChild(_pullButton);

        AddChild(pushPullRow);

        _updateButton = new StyledButton("CHECK FOR UPDATES", scale, fontSize: 16, height: 48);
        _updateButton.Visible = false;
        _updateButton.Pressed += () => CheckForUpdatesPressed?.Invoke();
        AddChild(_updateButton);

        // No banner above the orange UPDATE LAUNCHER button. Earlier versions
        // had a single-line yellow prompt here, but the user found it noisy
        // and asked us to remove it — the button itself already changes label
        // (e.g. "UPDATE LAUNCHER → v0.3.19", "Downloading… 42%",
        // "TAP TO INSTALL") so a separate prompt was redundant.
        _appUpdateButton = new StyledButton("UPDATE LAUNCHER", scale, fontSize: 16, height: 48);
        _appUpdateButton.Visible = false;
        var appUpdateStyle = StyledButton.MakeFilled(
            new Color(0.85f, 0.6f, 0.15f),
            (int)(4 * scale)
        );
        var appUpdateHover = StyledButton.MakeFilled(
            new Color(0.95f, 0.7f, 0.2f),
            (int)(4 * scale)
        );
        var appUpdatePressed = StyledButton.MakeFilled(
            new Color(0.7f, 0.5f, 0.1f),
            (int)(4 * scale)
        );
        _appUpdateButton.AddThemeStyleboxOverride("normal", appUpdateStyle);
        _appUpdateButton.AddThemeStyleboxOverride("hover", appUpdateHover);
        _appUpdateButton.AddThemeStyleboxOverride("pressed", appUpdatePressed);
        _appUpdateButton.Pressed += () => AppUpdatePressed?.Invoke();
        AddChild(_appUpdateButton);

        _launchButton = new StyledButton("LAUNCH", scale, fontSize: 16, height: 48);
        _launchButton.Visible = false;
        _launchButton.Pressed += () => LaunchPressed?.Invoke();
        AddChild(_launchButton);
    }

    public void SetLocalBackupChecked(bool value)
    {
        _localBackupToggle.ButtonPressed = value;
        _localBackupToggle.Text = value ? "Local Backup: ON" : "Local Backup: OFF";
        ApplyToggleStyle(_localBackupToggle, value);
    }

    public void SetCloudSyncChecked(bool value)
    {
        _cloudSyncToggle.ButtonPressed = value;
        _cloudSyncToggle.Text = value ? "Auto Sync: ON" : "Auto Sync: OFF";
        ApplyToggleStyle(_cloudSyncToggle, value);
    }

    public void SetFpsOverlayChecked(bool value)
    {
        _fpsOverlayToggle.ButtonPressed = value;
        _fpsOverlayToggle.Text = value ? "FPS Overlay: ON" : "FPS Overlay: OFF";
        ApplyToggleStyle(_fpsOverlayToggle, value);
    }

    public void SetBetaChannelChecked(bool value)
    {
        _betaChannelToggle.ButtonPressed = value;
        _betaChannelToggle.Text = value ? "Beta Channel: ON" : "Beta Channel: OFF";
        ApplyToggleStyle(_betaChannelToggle, value);
    }

    private void ApplyToggleStyle(Button button, bool on)
    {
        var style = on ? _onStyle : _offStyle;
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("disabled", style);
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
