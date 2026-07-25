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

        mainScene.changelogPanel?.ShowUpdate(version, releaseNotes);
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
                mainScene.downloadProgressPopup.Close();
            }
        }
    }

    public void OnAcceptUpdatePressed()
    {
        mainScene.changelogPanel?.Close();

        if (!string.IsNullOrEmpty(pendingUpdateVersion))
        {
            if (mainScene.downloadProgressPopup != null)
            {
                mainScene.downloadProgressPopup.Open();
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
        mainScene.changelogPanel?.Close();
    }
}
