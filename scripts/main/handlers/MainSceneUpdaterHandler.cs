using Godot;
using System;

public class MainSceneUpdaterHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;
    private AppUpdater appUpdater;
    private string pendingUpdateVersion;

    public MainSceneUpdaterHandler(MainScene mainScene, AppInstance appInstance)
    {
        this.mainScene = mainScene;
        this.appInstance = appInstance;
    }

    public void InitUpdater()
    {
        appUpdater = mainScene.GetNodeOrNull<AppUpdater>("/root/AppUpdater");

        if (appUpdater != null)
        {
            appUpdater.UpdateAvailable += OnUpdateAvailable;
            appUpdater.UpdateDownloadProgress += OnUpdateDownloadProgress;
            appUpdater.UpdateDownloadCompleted += OnUpdateDownloadCompleted;

            _ = appUpdater.CheckForUpdatesAsync();
        }
    }

    private void OnUpdateAvailable(string version, string releaseNotes)
    {
        pendingUpdateVersion = version;

        if (mainScene.changelogPopup != null && mainScene.changelogRichTextLabel != null)
        {
            string sanitizedNotes = releaseNotes.Replace("\r", "").Replace("\b", "");
            mainScene.changelogRichTextLabel.Text = $"[b]A new version ({version}) of Romm Frontend is available.[/b]\n\nRelease Notes:\n{sanitizedNotes}";
            mainScene.changelogPopup.Visible = true;

            if (mainScene.acceptUpdateBtn != null)
            {
                mainScene.acceptUpdateBtn.Text = "Install";
            }
            if (mainScene.cancelUpdateBtn != null)
            {
                mainScene.cancelUpdateBtn.Text = "Close";
            }
        }
    }

    private void OnUpdateDownloadProgress(float progress)
    {
        if (mainScene.downloadProgressBar != null)
        {
            mainScene.downloadProgressBar.Value = progress * 100.0f;
        }
    }

    private async void OnUpdateDownloadCompleted(bool success)
    {
        if (success)
        {
            if (mainScene.downloadProgressLabel != null)
            {
                mainScene.downloadProgressLabel.Text = "Download complete. Restarting to apply update...";
            }

            await mainScene.ToSignal(mainScene.GetTree().CreateTimer(3.0f), "timeout");
            appUpdater.ApplyUpdateAndRestart();
        }
        else
        {
            if (mainScene.downloadProgressLabel != null)
            {
                mainScene.downloadProgressLabel.Text = "Failed to download update. Please try again later.";
            }

            await mainScene.ToSignal(mainScene.GetTree().CreateTimer(3.0f), "timeout");

            if (mainScene.downloadProgressPopup != null)
            {
                mainScene.downloadProgressPopup.Visible = false;
            }
        }
    }

    public void OnAcceptUpdatePressed()
    {
        if (mainScene.changelogPopup != null) mainScene.changelogPopup.Visible = false;

        if (!string.IsNullOrEmpty(pendingUpdateVersion))
        {
            if (mainScene.downloadProgressPopup != null)
            {
                mainScene.downloadProgressPopup.Visible = true;
            }

            if (mainScene.downloadProgressLabel != null)
            {
                mainScene.downloadProgressLabel.Text = "Downloading...";
            }

            if (mainScene.downloadProgressBar != null)
            {
                mainScene.downloadProgressBar.Value = 0;
            }

            _ = appUpdater.DownloadUpdateAsync(pendingUpdateVersion);
        }
    }

    public void OnCancelUpdatePressed()
    {
        if (mainScene.changelogPopup != null) mainScene.changelogPopup.Visible = false;
    }
}
