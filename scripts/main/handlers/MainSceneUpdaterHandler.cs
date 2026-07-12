using Godot;
using System;

public class MainSceneUpdaterHandler
{
    private MainScene _mainScene;
    private AppInstance _appInstance;
    private AppUpdater _appUpdater;
    private string _pendingUpdateVersion;

    public MainSceneUpdaterHandler(MainScene mainScene, AppInstance appInstance)
    {
        _mainScene = mainScene;
        _appInstance = appInstance;
    }

    public void InitUpdater()
    {
        _appUpdater = _mainScene.GetNodeOrNull<AppUpdater>("/root/AppUpdater");

        if (_appUpdater != null)
        {
            _appUpdater.UpdateAvailable += OnUpdateAvailable;
            _appUpdater.UpdateDownloadProgress += OnUpdateDownloadProgress;
            _appUpdater.UpdateDownloadCompleted += OnUpdateDownloadCompleted;

            _ = _appUpdater.CheckForUpdatesAsync();
        }
    }

    private void OnUpdateAvailable(string version, string releaseNotes)
    {
        _pendingUpdateVersion = version;

        if (_mainScene.changelogPopup != null && _mainScene.changelogRichTextLabel != null)
        {
            string sanitizedNotes = releaseNotes.Replace("\r", "").Replace("\b", "");
            _mainScene.changelogRichTextLabel.Text = $"[b]A new version ({version}) of Romm Frontend is available.[/b]\n\nRelease Notes:\n{sanitizedNotes}";
            _mainScene.changelogPopup.Visible = true;

            if (_mainScene.acceptUpdateBtn != null)
            {
                _mainScene.acceptUpdateBtn.Text = "Select";
            }
            if (_mainScene.cancelUpdateBtn != null)
            {
                _mainScene.cancelUpdateBtn.Text = "Close";
            }
        }
    }

    private void OnUpdateDownloadProgress(float progress)
    {
        if (_mainScene.downloadProgressBar != null)
        {
            _mainScene.downloadProgressBar.Value = progress * 100.0f;
        }
    }

    private async void OnUpdateDownloadCompleted(bool success)
    {
        if (success)
        {
            if (_mainScene.downloadProgressLabel != null)
            {
                _mainScene.downloadProgressLabel.Text = "Download complete. Restarting to apply update...";
            }

            await _mainScene.ToSignal(_mainScene.GetTree().CreateTimer(3.0f), "timeout");
            _appUpdater.ApplyUpdateAndRestart();
        }
        else
        {
            if (_mainScene.downloadProgressLabel != null)
            {
                _mainScene.downloadProgressLabel.Text = "Failed to download update. Please try again later.";
            }

            await _mainScene.ToSignal(_mainScene.GetTree().CreateTimer(3.0f), "timeout");

            if (_mainScene.downloadProgressPopup != null)
            {
                _mainScene.downloadProgressPopup.Visible = false;
            }
        }
    }

    public void OnAcceptUpdatePressed()
    {
        if (_mainScene.changelogPopup != null) _mainScene.changelogPopup.Visible = false;

        if (!string.IsNullOrEmpty(_pendingUpdateVersion))
        {
            if (_mainScene.downloadProgressPopup != null)
            {
                _mainScene.downloadProgressPopup.Visible = true;
            }

            if (_mainScene.downloadProgressLabel != null)
            {
                _mainScene.downloadProgressLabel.Text = "Downloading...";
            }

            if (_mainScene.downloadProgressBar != null)
            {
                _mainScene.downloadProgressBar.Value = 0;
            }

            _ = _appUpdater.DownloadUpdateAsync(_pendingUpdateVersion);
        }
    }

    public void OnCancelUpdatePressed()
    {
        if (_mainScene.changelogPopup != null) _mainScene.changelogPopup.Visible = false;
    }
}
