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
        var system = GetCurrentPlatformSystem();

        if (system == null)
        {
            return;
        }

        string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(system.Slug);

        if (string.IsNullOrEmpty(mappedEmulator))
        {
            return;
        }

        if (!appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator))
        {
            mainScene.startMenuPanel?.Close();
            mainScene.OpenReleasePicker(mappedEmulator);
            return;
        }

        appInstance.emulatorManager.LaunchEmulatorWithoutGame(mappedEmulator, system);

        mainScene.startMenuPanel?.Close();

        mainScene.gameList?.GrabFocus();
    }

    private GameSystem GetCurrentPlatformSystem()
    {
        var selectedGame = mainScene.GameListHandler.currentlySelectedGame;

        if (selectedGame?.System != null)
        {
            return selectedGame.System;
        }

        if (mainScene.GameListHandler.gameSystems == null || mainScene.GameListHandler.currentGameSystemIndex < 0 || mainScene.GameListHandler.currentGameSystemIndex >= mainScene.GameListHandler.gameSystems.Count)
        {
            return null;
        }

        var system = mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex];

        return system.IsCollection ? null : system;
    }

    private string GetCurrentEmulator()
    {
        var platformSystem = GetCurrentPlatformSystem();

        if (platformSystem != null)
        {
            return appInstance.emulatorManager.GetMappedEmulator(platformSystem.Slug);
        }

        string platformSlug = mainScene.GameListHandler.currentlySelectedGame?.PlatformSlug;

        return string.IsNullOrEmpty(platformSlug) ? null : appInstance.emulatorManager.GetMappedEmulator(platformSlug);
    }

    public void RefreshEmulatorMenuOptions()
    {
        string mappedEmulator = GetCurrentEmulator();
        bool isInstalled = !string.IsNullOrEmpty(mappedEmulator) && appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator);
        bool isInstalling = !string.IsNullOrEmpty(mappedEmulator) && appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator);

        Button launchBtn = mainScene.startMenuPanel?.launchEmulatorButton;
        Button updateBtn = mainScene.startMenuPanel?.updateEmulatorButton;
        Button uninstallBtn = mainScene.startMenuPanel?.uninstallEmulatorButton;

        bool isLaunching = appInstance.emulatorManager.IsEmulatorLaunching;
        bool isRunning = appInstance.emulatorManager.IsEmulatorRunning;
        bool isBusy = isInstalling || isLaunching || isRunning;

        if (launchBtn != null)
        {
            string emulatorDisplayName = string.IsNullOrEmpty(mappedEmulator) ? null : appInstance.emulatorManager.GetEmulatorDisplayName(mappedEmulator);

            launchBtn.Disabled = string.IsNullOrEmpty(mappedEmulator) || isBusy;

            if (isInstalling)
            {
                launchBtn.Text = $"Installing {emulatorDisplayName}...";
            }

            else if (isLaunching)
            {
                launchBtn.Text = "Starting...";
            }

            else if (isRunning)
            {
                launchBtn.Text = "Running";
            }

            else if (string.IsNullOrEmpty(mappedEmulator))
            {
                launchBtn.Text = "No Emulator For This System";
            }

            else
            {
                launchBtn.Text = isInstalled ? $"Launch {emulatorDisplayName}" : $"Install {emulatorDisplayName}";
            }
        }

        string emulatorLabelName = string.IsNullOrEmpty(mappedEmulator) ? null : appInstance.emulatorManager.GetEmulatorDisplayName(mappedEmulator);

        if (updateBtn != null)
        {
            updateBtn.Disabled = !isInstalled || isBusy;

            if (isInstalling)
            {
                updateBtn.Text = $"Installing {emulatorLabelName}...";
            }

            else
            {
                updateBtn.Text = string.IsNullOrEmpty(emulatorLabelName) ? "Update Emulator" : $"Update {emulatorLabelName}";
            }
        }

        if (uninstallBtn != null)
        {
            uninstallBtn.Disabled = !isInstalled || isBusy;
            uninstallBtn.Text = string.IsNullOrEmpty(emulatorLabelName) ? "Uninstall Emulator" : $"Uninstall {emulatorLabelName}";
        }

        RefreshFavoriteMenuOption();
        RefreshNetplayMenuOption();
    }

    public void RefreshNetplayMenuOption()
    {
        Button hostNetplayBtn = mainScene.startMenuPanel?.hostNetplayButton;

        if (hostNetplayBtn == null)
        {
            return;
        }

        bool isAlreadyInSession = appInstance.netplayLobby != null && appInstance.netplayLobby.IsInLobby;

        hostNetplayBtn.Text = "Host Session";
        hostNetplayBtn.Disabled = isAlreadyInSession;

        Button joinNetplayBtn = mainScene.startMenuPanel?.joinNetplayButton;

        if (joinNetplayBtn != null)
        {
            joinNetplayBtn.Text = "Join Session";
            joinNetplayBtn.Disabled = isAlreadyInSession;
        }
    }

    public async void OnHostNetplayPressed()
    {
        if (appInstance.netplayManager == null || appInstance.netplayLobby == null || appInstance.netplayLobby.IsInLobby)
        {
            return;
        }

        int hostPort = appInstance.netplayManager.ResolveDefaultPort(null);

        string localAddress = appInstance.netplayManager.ResolveLocalHostAddress();

        if (string.IsNullOrEmpty(localAddress))
        {
            mainScene.startMenuPanel?.ShowNetplayView("Failed", "No local network address was found, so a join code cannot be generated.\nPress Back to return.");
            return;
        }

        appInstance.netplayManager.BeginHosting(hostPort);

        mainScene.startMenuPanel?.ShowNetplayView("Hosting", "Starting the session and asking the router to open a port...");

        int lobbyPort = appInstance.netplayLobby.ResolveLobbyPort();

        bool portsMapped = appInstance.netplayPortMapper != null && await appInstance.netplayPortMapper.TryMapPortsAsync(hostPort, lobbyPort);
        string externalAddress = portsMapped ? appInstance.netplayPortMapper.ExternalAddress : null;
        bool isInternetReachable = portsMapped
            && !string.IsNullOrEmpty(externalAddress)
            && appInstance.netplayPortMapper.IsPortMapped(hostPort)
            && appInstance.netplayPortMapper.IsPortMapped(lobbyPort);

        string codeAddress = isInternetReachable ? externalAddress : localAddress;
        string joinCode = appInstance.netplayManager.BuildJoinCode(codeAddress, hostPort);

        if (string.IsNullOrEmpty(joinCode))
        {
            appInstance.netplayManager.EndSession();
            appInstance.netplayPortMapper?.ReleasePorts();
            mainScene.startMenuPanel?.ShowNetplayView("Failed", $"Could not build a join code for {codeAddress}.\nPress Back to return.");
            return;
        }

        string reachabilityNote = isInternetReachable
            ? $"Reachable from the internet at {externalAddress}:{hostPort}."
            : $"Local network only. For internet play, forward ports {lobbyPort} and {hostPort} (UDP and TCP) to {localAddress}. {appInstance.netplayPortMapper?.LastFailureReason}";

        if (mainScene.NetplayHandler == null || !mainScene.NetplayHandler.HostLobby(joinCode))
        {
            appInstance.netplayManager.EndSession();
            appInstance.netplayPortMapper?.ReleasePorts();
            GD.PrintErr($"[Netplay] Could not open the lobby on port {lobbyPort}; not handing out a join code.");
            mainScene.startMenuPanel?.ShowNetplayView("Failed", $"Could not open the lobby on port {lobbyPort}.\nAnother copy of this app may already be hosting.\nPress Back to return.");
            return;
        }

        mainScene.startMenuPanel?.ShowMenuView();
        mainScene.startMenuPanel?.Close();

        GD.Print($"[Netplay] Lobby open with code {joinCode}. {reachabilityNote}");
    }

    public void OnJoinNetplayPressed()
    {
        if (appInstance.netplayManager == null || appInstance.netplayLobby == null)
        {
            return;
        }

        var discoveredSession = appInstance.netplayDiscovery?.Sessions.FirstOrDefault();

        if (discoveredSession != null)
        {
            JoinResolvedLobby(discoveredSession.HostAddress, discoveredSession.LobbyPort, $"{discoveredSession.Username} on this network");
            return;
        }

        string clipboardText = DisplayServer.ClipboardGet();

        if (!appInstance.netplayManager.TryParseJoinCode(clipboardText, out string hostAddress, out int netplayPort))
        {
            mainScene.startMenuPanel?.ShowNetplayView("----------", "No lobby found on this network, and the clipboard does not contain a join code.\nCopy the host's code, then choose Join again.");
            return;
        }

        appInstance.netplayManager.BeginJoining(hostAddress, netplayPort);
        JoinResolvedLobby(hostAddress, appInstance.netplayLobby.ResolveLobbyPort(), $"{hostAddress}:{netplayPort}");
    }

    private void JoinResolvedLobby(string hostAddress, int lobbyPort, string description)
    {
        if (mainScene.NetplayHandler == null || !mainScene.NetplayHandler.JoinLobby(hostAddress, lobbyPort))
        {
            mainScene.startMenuPanel?.ShowNetplayView("----------", $"Could not connect to {description}.");
            return;
        }

        mainScene.startMenuPanel?.ShowMenuView();
        mainScene.startMenuPanel?.Close();

        GD.Print($"[Netplay] Joining lobby: {description}");
    }

    public void OnNetplayCancelPressed()
    {
        appInstance.netplayManager?.EndSession();
        appInstance.netplayPortMapper?.ReleasePorts();
        mainScene.startMenuPanel?.ShowMenuView();
    }

    public void RefreshFavoriteMenuOption()
    {
        Button favoriteBtn = mainScene.startMenuPanel?.favoriteGameButton;

        if (favoriteBtn == null)
        {
            return;
        }

        var selectedGame = mainScene.GameListHandler.currentlySelectedGame;

        if (!appInstance.dataBus.HasFavoriteCollection)
        {
            favoriteBtn.Disabled = true;
            favoriteBtn.Text = "No Favorites Collection On Server";
            return;
        }

        favoriteBtn.Disabled = selectedGame == null;

        if (selectedGame == null)
        {
            favoriteBtn.Text = "Add to Favorites";
            return;
        }

        favoriteBtn.Text = appInstance.dataBus.IsFavorite(selectedGame.Id) ? "Remove from Favorites" : "Add to Favorites";
    }

    public async void OnFavoriteGamePressed()
    {
        var selectedGame = mainScene.GameListHandler.currentlySelectedGame;

        if (selectedGame == null || !appInstance.dataBus.HasFavoriteCollection)
        {
            return;
        }

        Button favoriteBtn = mainScene.startMenuPanel?.favoriteGameButton;
        bool shouldBecomeFavorite = !appInstance.dataBus.IsFavorite(selectedGame.Id);

        if (favoriteBtn != null)
        {
            favoriteBtn.Disabled = true;
            favoriteBtn.Text = shouldBecomeFavorite ? "Adding..." : "Removing...";
        }

        bool changeSucceeded = await appInstance.rommApi.SetRomInCollectionAsync(appInstance.dataBus.favoriteCollectionId, selectedGame.Id, shouldBecomeFavorite);

        if (changeSucceeded)
        {
            appInstance.dataBus.ApplyFavoriteChange(selectedGame, shouldBecomeFavorite);
        }

        RefreshFavoriteMenuOption();
    }

    public void OnUpdateEmulatorPressed()
    {
        string mappedEmulator = GetCurrentEmulator();

        if (string.IsNullOrEmpty(mappedEmulator) || appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator))
        {
            return;
        }

        mainScene.startMenuPanel?.Close();

        mainScene.OpenReleasePicker(mappedEmulator);
    }

    public void OnUninstallEmulatorPressed()
    {
        string mappedEmulator = GetCurrentEmulator();

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
        if (mainScene.GameListHandler.CurrentSystemIsCollection)
        {
            return;
        }

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

        GameSystem currentSystem = mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex];

        if (currentSystem.IsCollection)
        {
            return;
        }

        mainScene.startMenuPanel?.Close();

        mainScene.progressPanel?.ShowStatus("Refreshing games...");
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
