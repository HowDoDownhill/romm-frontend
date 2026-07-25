using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public class MainScenePopupHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;

    public MainScenePopupHandler(MainScene mainScene, AppInstance appInstance)
    {
        this.mainScene = mainScene;
        this.appInstance = appInstance;
    }

    public void OnLaunchEmulatorPressed()
    {
        if (mainScene.GameListHandler.gameSystems == null || mainScene.GameListHandler.currentGameSystemIndex < 0 || mainScene.GameListHandler.currentGameSystemIndex >= mainScene.GameListHandler.gameSystems.Count)
        {
            return;
        }

        var system = mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex];
        string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(system.Slug);

        if (!string.IsNullOrEmpty(mappedEmulator))
        {
            appInstance.emulatorManager.LaunchEmulatorWithoutGame(mappedEmulator, system);
        }

        if (mainScene.startMenuRoot != null)
        {
            mainScene.startMenuRoot.Visible = false;
        }

        mainScene.gameList?.GrabFocus();
    }

    private string GetCurrentSystemEmulator()
    {
        if (mainScene.GameListHandler.gameSystems == null || mainScene.GameListHandler.currentGameSystemIndex < 0 || mainScene.GameListHandler.currentGameSystemIndex >= mainScene.GameListHandler.gameSystems.Count)
        {
            return null;
        }

        var system = mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex];
        return appInstance.emulatorManager.GetMappedEmulator(system.Slug);
    }

    public void RefreshEmulatorMenuOptions()
    {
        string mappedEmulator = GetCurrentSystemEmulator();
        bool isInstalled = !string.IsNullOrEmpty(mappedEmulator) && appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator);
        bool isInstalling = !string.IsNullOrEmpty(mappedEmulator) && appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator);

        if (mainScene.UpdateEmulatorPopupOption is Button updateBtn)
        {
            updateBtn.Disabled = !isInstalled || isInstalling;
            updateBtn.Text = isInstalling ? "Installing Emulator..." : "Update Emulator";
        }

        if (mainScene.UninstallEmulatorPopupOption is Button uninstallBtn)
        {
            uninstallBtn.Disabled = !isInstalled || isInstalling;
        }
    }

    public void OnUpdateEmulatorPressed()
    {
        string mappedEmulator = GetCurrentSystemEmulator();

        if (string.IsNullOrEmpty(mappedEmulator) || appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator))
        {
            return;
        }

        if (mainScene.startMenuRoot != null)
        {
            mainScene.startMenuRoot.Visible = false;
        }

        mainScene.OpenReleasePicker(mappedEmulator);
    }

    public void OnUninstallEmulatorPressed()
    {
        string mappedEmulator = GetCurrentSystemEmulator();

        if (string.IsNullOrEmpty(mappedEmulator) || !appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator))
        {
            return;
        }

        appInstance.emulatorManager.UninstallEmulator(mappedEmulator);

        if (mainScene.startMenuRoot != null)
        {
            mainScene.startMenuRoot.Visible = false;
        }

        if (mainScene.GameListHandler.currentlySelectedGame != null)
        {
            mainScene.GameListHandler.UpdateDetailsPanelButtons(mainScene.GameListHandler.currentlySelectedGame);
        }

        mainScene.gameList?.GrabFocus();
    }

    public void OnSelectBiosMenuPressed()
    {
        if (mainScene.startMenuContainer != null)
        {
            mainScene.startMenuContainer.Visible = false;
        }

        if (mainScene.biosSelectorContainer != null)
        {
            mainScene.biosSelectorContainer.Visible = true;
        }

        PopulateBiosSelector();
    }

    public void PopulateBiosSelector()
    {
        if (mainScene.biosSelector == null)
        {
            return;
        }

        foreach (Node child in mainScene.biosSelector.GetChildren())
        {
            mainScene.biosSelector.RemoveChild(child);
            child.QueueFree();
        }

        if (mainScene.GameListHandler.gameSystems == null || mainScene.GameListHandler.currentGameSystemIndex < 0 || mainScene.GameListHandler.currentGameSystemIndex >= mainScene.GameListHandler.gameSystems.Count)
        {
            return;
        }

        var system = mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex];

        var firmwareDir = appInstance.configManager.BiosPath.PathJoin(system.Slug);
        var localFiles = new string[0];

        if (Godot.FileAccess.FileExists(firmwareDir) || Godot.DirAccess.DirExistsAbsolute(firmwareDir))
        {
            if (Godot.DirAccess.DirExistsAbsolute(firmwareDir))
            {
                localFiles = Godot.DirAccess.GetFilesAt(firmwareDir);
            }
        }

        if (localFiles.Length > 0)
        {
            foreach (var fileName in localFiles)
            {
                Button btn = new Button();
                btn.Text = fileName;
                btn.Alignment = HorizontalAlignment.Left;
                btn.Pressed += () =>
                {
                    system.PrefferedFirmware = firmwareDir.PathJoin(fileName);

                    if (mainScene.biosSelectorContainer != null)
                    {
                        mainScene.biosSelectorContainer.Visible = false;
                    }

                    if (mainScene.startMenuContainer != null)
                    {
                        mainScene.startMenuContainer.Visible = true;
                    }
                    (mainScene.SelectBiosPopupOption as Control)?.GrabFocus();
                };
                mainScene.biosSelector.AddChild(btn);
            }
        }
        else
        {
            Label lbl = new Label();
            lbl.Text = "No bios/firmware found.";
            mainScene.biosSelector.AddChild(lbl);
        }

        if (mainScene.biosSelector.GetChildCount() > 0 && mainScene.biosSelector.GetChild(0) is Control firstChild)
        {
            firstChild.GrabFocus();
        }
    }

    public void OnSettingsMenuPressed()
    {
        if (mainScene.startMenuRoot != null)
        {
            mainScene.startMenuRoot.Visible = false;
        }

        mainScene.SettingsHandler.ToggleSettingsMenu();
    }

    public void OnRefreshGamesPressed()
    {
        appInstance.cacheManager?.RebuildGameCache();
    }

    public async void OnRefreshCurrentSystemGamesPressed()
    {
        if (mainScene.GameListHandler.gameSystems == null || mainScene.GameListHandler.currentGameSystemIndex < 0 || mainScene.GameListHandler.currentGameSystemIndex >= mainScene.GameListHandler.gameSystems.Count)
        {
            return;
        }

        if (mainScene.startMenuRoot != null)
        {
            mainScene.startMenuRoot.Visible = false;
        }

        if (mainScene.downloadProgressPopup != null)
        {
            mainScene.downloadProgressPopup.Open();

            if (mainScene.downloadProgressLabel != null)
            {
                mainScene.downloadProgressLabel.Text = "Refreshing games...";
            }

            if (mainScene.downloadProgressBar != null)
            {
                mainScene.downloadProgressBar.Value = 0;
            }
        }

        GameSystem currentSystem = mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex];
        List<Game> allGamesForSystem = new List<Game>();
        int currentPage = 1;
        const int chunkSize = 100;
        bool hasMoreGames = true;

        while (hasMoreGames)
        {
            var response = await appInstance.rommApi.GetGamesAsync(currentSystem, currentPage, chunkSize);

            if (response?.Games != null && response.Games.Any())
            {
                foreach (var game in response.Games)
                {
                    game.System = currentSystem;
                }

                allGamesForSystem.AddRange(response.Games);

                if (mainScene.downloadProgressLabel != null)
                {
                    mainScene.downloadProgressLabel.Text = $"Found {allGamesForSystem.Count} games...";
                }

                if (response.Games.Count < chunkSize)
                {
                    hasMoreGames = false;
                }
                else
                {
                    currentPage++;
                }
            }
            else
            {
                hasMoreGames = false;
            }
        }

        appInstance.dataBus.gameCache[currentSystem.Id] = allGamesForSystem;
        appInstance.cacheManager?.SaveCache(appInstance.dataBus.systems, appInstance.dataBus.gameCache);

        mainScene.GameListHandler.games = appInstance.dataBus.gameCache;
        mainScene.GameListHandler.ApplyFiltersAndRefresh();

        if (mainScene.downloadProgressBar != null)
        {
            mainScene.downloadProgressBar.Value = 100;
        }

        if (mainScene.downloadProgressLabel != null)
        {
            mainScene.downloadProgressLabel.Text = "Refresh complete!";
        }

        await mainScene.ToSignal(mainScene.GetTree().CreateTimer(1.5f), "timeout");

        if (mainScene.downloadProgressPopup != null)
        {
            mainScene.downloadProgressPopup.Close();
        }

        mainScene.gameList?.GrabFocus();
    }

    public void OnQuitPressed()
    {
        mainScene.GetTree().Quit();
    }

    public void OnRandomGamePressed()
    {
        if (mainScene.GameListHandler.currentlyShownGames.Count > 0)
        {
            int randomIndex = new Random().Next(mainScene.GameListHandler.currentlyShownGames.Count);
            mainScene.GameListHandler.OnGameSelected(randomIndex);

            if (mainScene.gameList != null && mainScene.gameList.HasMethod("Refresh"))
            {
                mainScene.gameList.Set("SelectedIndex", randomIndex);
                mainScene.gameList.Call("Refresh");
            }

            if (mainScene.startMenuRoot != null)
            {
                mainScene.startMenuRoot.Visible = false;
            }

            mainScene.gameList?.GrabFocus();
        }
    }
}
