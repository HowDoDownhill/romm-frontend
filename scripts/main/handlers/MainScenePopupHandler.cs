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

        mainScene.startMenuPanel?.Close();

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

        Button updateBtn = mainScene.startMenuPanel?.updateEmulatorButton;
        Button uninstallBtn = mainScene.startMenuPanel?.uninstallEmulatorButton;

        if (updateBtn != null)
        {
            updateBtn.Disabled = !isInstalled || isInstalling;
            updateBtn.Text = isInstalling ? "Installing Emulator..." : "Update Emulator";
        }

        if (uninstallBtn != null)
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

        mainScene.startMenuPanel?.Close();

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

        mainScene.startMenuPanel?.Close();

        if (mainScene.GameListHandler.currentlySelectedGame != null)
        {
            mainScene.GameListHandler.UpdateDetailsPanelButtons(mainScene.GameListHandler.currentlySelectedGame);
        }

        mainScene.gameList?.GrabFocus();
    }

    public void OnSelectBiosMenuPressed()
    {
        mainScene.startMenuPanel?.ShowBiosView();
    }

    public void PopulateBiosSelector()
    {
        VBoxContainer biosList = mainScene.startMenuPanel?.biosList;

        if (biosList == null)
        {
            return;
        }

        foreach (Node child in biosList.GetChildren())
        {
            biosList.RemoveChild(child);
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
                    mainScene.startMenuPanel?.ShowMenuView();
                };
                biosList.AddChild(btn);
            }
        }
        else
        {
            Label lbl = new Label();
            lbl.Text = "No bios/firmware found.";
            biosList.AddChild(lbl);
        }

        if (biosList.GetChildCount() > 0 && biosList.GetChild(0) is Control firstChild)
        {
            firstChild.GrabFocus();
        }
    }

    public void OnSettingsMenuPressed()
    {
        mainScene.startMenuPanel?.Close();

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

        mainScene.startMenuPanel?.Close();

        mainScene.progressPanel?.ShowStatus("Refreshing games...");

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

                mainScene.progressPanel?.SetStatus($"Found {allGamesForSystem.Count} games...");

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

        mainScene.progressPanel?.SetProgress(100);
        mainScene.progressPanel?.SetStatus("Refresh complete!");

        await mainScene.ToSignal(mainScene.GetTree().CreateTimer(1.5f), "timeout");

        mainScene.progressPanel?.Close();

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

            mainScene.startMenuPanel?.Close();

            mainScene.gameList?.GrabFocus();
        }
    }
}
