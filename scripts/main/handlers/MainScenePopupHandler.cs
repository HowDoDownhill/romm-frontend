using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public class MainScenePopupHandler
{
    private MainScene _mainScene;
    private AppInstance _appInstance;

    public MainScenePopupHandler(MainScene mainScene, AppInstance appInstance)
    {
        _mainScene = mainScene;
        _appInstance = appInstance;
    }

    public void OnLaunchEmulatorPressed()
    {
        if (_mainScene.GameListHandler.gameSystems == null || _mainScene.GameListHandler.currentGameSystemIndex < 0 || _mainScene.GameListHandler.currentGameSystemIndex >= _mainScene.GameListHandler.gameSystems.Count)
        {
            return;
        }

        var system = _mainScene.GameListHandler.gameSystems[_mainScene.GameListHandler.currentGameSystemIndex];
        string mappedEmulator = _appInstance.emulatorManager.GetMappedEmulator(system.Slug);

        if (!string.IsNullOrEmpty(mappedEmulator))
        {
            _appInstance.emulatorManager.LaunchEmulatorWithoutGame(mappedEmulator, system);
        }

        if (_mainScene.startMenuRoot != null)
        {
            _mainScene.startMenuRoot.Visible = false;
        }

        _mainScene.gameList?.GrabFocus();
    }

    // Resolves the emulator mapped to the system currently shown in the carousel, matching how
    // the other start-menu emulator actions pick their target.
    private string GetCurrentSystemEmulator()
    {
        if (_mainScene.GameListHandler.gameSystems == null || _mainScene.GameListHandler.currentGameSystemIndex < 0 || _mainScene.GameListHandler.currentGameSystemIndex >= _mainScene.GameListHandler.gameSystems.Count)
        {
            return null;
        }

        var system = _mainScene.GameListHandler.gameSystems[_mainScene.GameListHandler.currentGameSystemIndex];
        return _appInstance.emulatorManager.GetMappedEmulator(system.Slug);
    }

    // Called when the start menu opens: Update/Uninstall only apply to an emulator that is
    // actually installed, and neither should be available mid-install.
    public void RefreshEmulatorMenuOptions()
    {
        string mappedEmulator = GetCurrentSystemEmulator();
        bool isInstalled = !string.IsNullOrEmpty(mappedEmulator) && _appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator);
        bool isInstalling = !string.IsNullOrEmpty(mappedEmulator) && _appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator);

        if (_mainScene.UpdateEmulatorPopupOption is Button updateBtn)
        {
            updateBtn.Disabled = !isInstalled || isInstalling;
            updateBtn.Text = isInstalling ? "Installing Emulator..." : "Update Emulator";
        }

        if (_mainScene.UninstallEmulatorPopupOption is Button uninstallBtn)
        {
            uninstallBtn.Disabled = !isInstalled || isInstalling;
        }
    }

    // Opens the release picker so the user can install a different version over the current one.
    public void OnUpdateEmulatorPressed()
    {
        string mappedEmulator = GetCurrentSystemEmulator();

        if (string.IsNullOrEmpty(mappedEmulator) || _appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator))
        {
            return;
        }

        if (_mainScene.startMenuRoot != null)
        {
            _mainScene.startMenuRoot.Visible = false;
        }

        _mainScene.OpenReleasePicker(mappedEmulator);
    }

    // Removes the installed emulator's files. Save data inside the emulator directory is kept.
    public void OnUninstallEmulatorPressed()
    {
        string mappedEmulator = GetCurrentSystemEmulator();

        if (string.IsNullOrEmpty(mappedEmulator) || !_appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator))
        {
            return;
        }

        _appInstance.emulatorManager.UninstallEmulator(mappedEmulator);

        if (_mainScene.startMenuRoot != null)
        {
            _mainScene.startMenuRoot.Visible = false;
        }

        if (_mainScene.GameListHandler.currentlySelectedGame != null)
        {
            _mainScene.GameListHandler.UpdateDetailsPanelButtons(_mainScene.GameListHandler.currentlySelectedGame);
        }

        _mainScene.gameList?.GrabFocus();
    }

    public void OnSelectBiosMenuPressed()
    {
        if (_mainScene.startMenuContainer != null)
        {
            _mainScene.startMenuContainer.Visible = false;
        }

        if (_mainScene.biosSelectorContainer != null)
        {
            _mainScene.biosSelectorContainer.Visible = true;
        }

        PopulateBiosSelector();
    }

    public void PopulateBiosSelector()
    {
        if (_mainScene.biosSelector == null)
        {
            return;
        }

        foreach (Node child in _mainScene.biosSelector.GetChildren())
        {
            _mainScene.biosSelector.RemoveChild(child);
            child.QueueFree();
        }

        if (_mainScene.GameListHandler.gameSystems == null || _mainScene.GameListHandler.currentGameSystemIndex < 0 || _mainScene.GameListHandler.currentGameSystemIndex >= _mainScene.GameListHandler.gameSystems.Count)
        {
            return;
        }

        var system = _mainScene.GameListHandler.gameSystems[_mainScene.GameListHandler.currentGameSystemIndex];

        var firmwareDir = _appInstance.configManager.BiosPath.PathJoin(system.Slug);
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

                    if (_mainScene.biosSelectorContainer != null)
                    {
                        _mainScene.biosSelectorContainer.Visible = false;
                    }

                    if (_mainScene.startMenuContainer != null)
                    {
                        _mainScene.startMenuContainer.Visible = true;
                    } 
                    (_mainScene.SelectBiosPopupOption as Control)?.GrabFocus();
                };
                _mainScene.biosSelector.AddChild(btn);
            }
        }
        else
        {
            Label lbl = new Label();
            lbl.Text = "No bios/firmware found.";
            _mainScene.biosSelector.AddChild(lbl);
        }

        if (_mainScene.biosSelector.GetChildCount() > 0 && _mainScene.biosSelector.GetChild(0) is Control firstChild)
        {
            firstChild.GrabFocus();
        }
    }

    public void OnSettingsMenuPressed()
    {
        if (_mainScene.startMenuRoot != null)
        {
            _mainScene.startMenuRoot.Visible = false;
        }

        _mainScene.SettingsHandler.ToggleSettingsMenu();
    }

    public void OnRefreshGamesPressed()
    {
        _appInstance.cacheManager?.RebuildGameCache();
    }

    public async void OnRefreshCurrentSystemGamesPressed()
    {
        if (_mainScene.GameListHandler.gameSystems == null || _mainScene.GameListHandler.currentGameSystemIndex < 0 || _mainScene.GameListHandler.currentGameSystemIndex >= _mainScene.GameListHandler.gameSystems.Count)
        {
            return;
        }

        if (_mainScene.startMenuRoot != null)
        {
            _mainScene.startMenuRoot.Visible = false;
        }

        if (_mainScene.downloadProgressPopup != null) 
        {
            _mainScene.downloadProgressPopup.Visible = true;

            if (_mainScene.downloadProgressLabel != null)
            {
                _mainScene.downloadProgressLabel.Text = "Refreshing games...";
            }

            if (_mainScene.downloadProgressBar != null)
            {
                _mainScene.downloadProgressBar.Value = 0;
            }
        }
        
        GameSystem currentSystem = _mainScene.GameListHandler.gameSystems[_mainScene.GameListHandler.currentGameSystemIndex];
        List<Game> allGamesForSystem = new List<Game>();
        int currentPage = 1;
        const int chunkSize = 100;
        bool hasMoreGames = true;

        while (hasMoreGames)
        {
            var response = await _appInstance.rommApi.GetGamesAsync(currentSystem, currentPage, chunkSize);
            
            if (response?.Games != null && response.Games.Any())
            {
                foreach (var game in response.Games)
                {
                    game.System = currentSystem;
                }

                allGamesForSystem.AddRange(response.Games);

                if (_mainScene.downloadProgressLabel != null)
                {
                    _mainScene.downloadProgressLabel.Text = $"Found {allGamesForSystem.Count} games...";
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
        
        _appInstance.dataBus.gameCache[currentSystem.Id] = allGamesForSystem;
        _appInstance.cacheManager?.SaveCache(_appInstance.dataBus.systems, _appInstance.dataBus.gameCache);
        
        _mainScene.GameListHandler.games = _appInstance.dataBus.gameCache;
        _mainScene.GameListHandler.ApplyFiltersAndRefresh();
        
        if (_mainScene.downloadProgressBar != null)
        {
            _mainScene.downloadProgressBar.Value = 100;
        }

        if (_mainScene.downloadProgressLabel != null)
        {
            _mainScene.downloadProgressLabel.Text = "Refresh complete!";
        }

        await _mainScene.ToSignal(_mainScene.GetTree().CreateTimer(1.5f), "timeout");

        if (_mainScene.downloadProgressPopup != null)
        {
            _mainScene.downloadProgressPopup.Visible = false;
        }

        _mainScene.gameList?.GrabFocus();
    }

    public void OnQuitPressed()
    {
        _mainScene.GetTree().Quit();
    }

    public void OnRandomGamePressed()
    {
        if (_mainScene.GameListHandler.currentlyShownGames.Count > 0)
        {
            int randomIndex = new Random().Next(_mainScene.GameListHandler.currentlyShownGames.Count);
            _mainScene.GameListHandler.OnGameSelected(randomIndex);

            if (_mainScene.gameList != null && _mainScene.gameList.HasMethod("Refresh"))
            {
                _mainScene.gameList.Set("SelectedIndex", randomIndex);
                _mainScene.gameList.Call("Refresh");
            }

            if (_mainScene.startMenuRoot != null)
            {
                _mainScene.startMenuRoot.Visible = false;
            }
            
            _mainScene.gameList?.GrabFocus();
        }
    }
}
