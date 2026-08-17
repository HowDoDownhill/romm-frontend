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

    public void Detach()
    {
        if (appUpdater == null)
        {
            return;
        }

        appUpdater.UpdateAvailable -= OnUpdateAvailable;
        appUpdater.UpdateDownloadProgress -= OnUpdateDownloadProgress;
        appUpdater.UpdateDownloadCompleted -= OnUpdateDownloadCompleted;
    }

    private void OnUpdateAvailable(string version, string releaseNotes)
    {
        pendingUpdateVersion = version;

        mainScene.changelogPanel?.ShowUpdate(version, releaseNotes);
    }

    private void OnUpdateDownloadProgress(float progress)
    {
        mainScene.progressPanel?.SetProgress(progress * 100.0f);
    }

    private async void OnUpdateDownloadCompleted(bool success)
    {
        if (success)
        {
            mainScene.progressPanel?.SetStatus("Download complete. Restarting to apply update...");

            await mainScene.ToSignal(mainScene.GetTree().CreateTimer(3.0f), "timeout");
            appUpdater.ApplyUpdateAndRestart();
        }
        else
        {
            mainScene.progressPanel?.SetStatus("Failed to download update. Please try again later.");

            await mainScene.ToSignal(mainScene.GetTree().CreateTimer(3.0f), "timeout");

            mainScene.progressPanel?.Close();
        }
    }

    public void OnAcceptUpdatePressed()
    {
        mainScene.changelogPanel?.Close();

        if (!string.IsNullOrEmpty(pendingUpdateVersion))
        {
            mainScene.progressPanel?.ShowStatus("Downloading...");

            _ = appUpdater.DownloadUpdateAsync(pendingUpdateVersion);
        }
    }

    public void OnCancelUpdatePressed()
    {
        mainScene.changelogPanel?.Close();
    }
}
