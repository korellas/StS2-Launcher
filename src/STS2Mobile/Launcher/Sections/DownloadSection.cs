using System;
using Godot;
using STS2Mobile.Launcher;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

// Download state for the game files. Deliberately loud: this is a 1.9 GB
// transfer, and the previous version reported it as a thin bar with a 12pt grey
// caption, which left no way to tell a stalled download from a slow one.
public class DownloadSection : VBoxContainer
{
    public event Action DownloadRequested;

    private readonly Button _downloadButton;
    private readonly StyledLabel _heading;
    private readonly StyledLabel _percentLabel;
    private readonly ProgressBar _progressBar;
    private readonly StyledLabel _sizeLabel;
    private readonly StyledLabel _fileLabel;

    private ulong _lastSampleMsec;
    private long _lastSampleBytes;
    private string _speedText = "";

    public DownloadSection(float scale)
    {
        AddThemeConstantOverride("separation", (int)(6 * scale));
        Visible = false;

        _downloadButton = new GameMenuButton(
            Localization.Tr("DOWNLOAD_GAME_FILES"),
            scale,
            fontSize: 28,
            primary: true
        );
        _downloadButton.Pressed += () => DownloadRequested?.Invoke();
        AddChild(_downloadButton);

        _heading = new StyledLabel(Localization.Tr("DOWNLOAD_IN_PROGRESS"), scale, fontSize: 18);
        _heading.Visible = false;
        AddChild(_heading);

        _percentLabel = new StyledLabel("", scale, fontSize: 40);
        _percentLabel.AddThemeColorOverride("font_color", LauncherTheme.Gold);
        _percentLabel.Visible = false;
        AddChild(_percentLabel);

        _progressBar = new StyledProgressBar(scale);
        _progressBar.CustomMinimumSize = new Vector2(0, (int)(18 * scale));
        _progressBar.Visible = false;
        AddChild(_progressBar);

        _sizeLabel = new StyledLabel("", scale, fontSize: 17);
        _sizeLabel.Visible = false;
        AddChild(_sizeLabel);

        // The file name changes constantly even while the percentage crawls, so it
        // is the clearest evidence that the transfer is alive.
        _fileLabel = new StyledLabel("", scale, fontSize: 13);
        _fileLabel.Modulate = new Color(1f, 1f, 1f, 0.6f);
        _fileLabel.ClipText = true;
        _fileLabel.Visible = false;
        AddChild(_fileLabel);
    }

    public void SetProgress(double pct, string text) => SetProgress(pct, text, 0);

    public void SetProgress(double pct, string text, long downloadedBytes)
    {
        ShowProgressRows();
        _progressBar.Value = pct;
        _percentLabel.Text = $"{pct:F1} %";

        UpdateSpeed(downloadedBytes);
        _sizeLabel.Text = string.IsNullOrEmpty(_speedText) ? text : $"{text}   ·   {_speedText}";
    }

    public void SetCurrentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _fileLabel.Visible = true;
        var name = path.Replace('\\', '/');
        int slash = name.LastIndexOf('/');
        _fileLabel.Text = slash >= 0 ? name[(slash + 1)..] : name;
    }

    // Derived here rather than plumbed through the model: the section already
    // receives every progress update, and a byte delta over a time delta is all
    // a speed readout is.
    private void UpdateSpeed(long downloadedBytes)
    {
        if (downloadedBytes <= 0)
            return;

        ulong now = Time.GetTicksMsec();
        if (_lastSampleMsec == 0)
        {
            _lastSampleMsec = now;
            _lastSampleBytes = downloadedBytes;
            return;
        }

        double elapsed = (now - _lastSampleMsec) / 1000.0;
        if (elapsed < 1.0)
            return;

        long delta = downloadedBytes - _lastSampleBytes;
        _lastSampleMsec = now;
        _lastSampleBytes = downloadedBytes;

        _speedText = delta > 0 ? $"{LauncherModel.FormatSize(delta / (long)elapsed)}/s" : "";
    }

    private void ShowProgressRows()
    {
        _heading.Visible = true;
        _percentLabel.Visible = true;
        _progressBar.Visible = true;
        _sizeLabel.Visible = true;
    }

    public void ShowProgress(string text)
    {
        _downloadButton.Disabled = true;
        ShowProgressRows();
        _progressBar.Value = 0;
        _percentLabel.Text = "";
        _sizeLabel.Text = text;
    }

    public void HideProgress()
    {
        _heading.Visible = false;
        _percentLabel.Visible = false;
        _progressBar.Visible = false;
        _sizeLabel.Visible = false;
        _fileLabel.Visible = false;
    }

    public void SetButtonDisabled(bool disabled) => _downloadButton.Disabled = disabled;

    public void SetButtonText(string text) => _downloadButton.Text = text;

    public void Reset(string buttonText = null)
    {
        _downloadButton.Disabled = false;
        _downloadButton.Text = buttonText ?? Localization.Tr("DOWNLOAD_GAME_FILES");
        _progressBar.Value = 0;
        _lastSampleMsec = 0;
        _lastSampleBytes = 0;
        _speedText = "";
        HideProgress();
    }
}
