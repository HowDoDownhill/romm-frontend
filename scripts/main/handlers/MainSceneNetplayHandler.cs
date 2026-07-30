using Godot;
using System.Linq;
using System.Net.NetworkInformation;

public class MainSceneNetplayHandler
{
    private readonly MainScene mainScene;
    private readonly AppInstance appInstance;

    private string activeJoinCode;
    private bool isLocallyReady;
    private int lastPushedRomId;

    public MainSceneNetplayHandler(MainScene mainScene, AppInstance appInstance)
    {
        this.mainScene = mainScene;
        this.appInstance = appInstance;
    }

    public bool IsLobbyVisible => appInstance.netplayLobby != null && appInstance.netplayLobby.IsInLobby;

    public void Initialise()
    {
        if (appInstance.netplayLobby != null)
        {
            appInstance.netplayLobby.MembersChanged += RefreshLobbyPanel;
            appInstance.netplayLobby.MembersChanged += ReportPreparednessIfInLobby;
            appInstance.netplayLobby.MembersChanged += AutoStartHostedGameIfRequested;
            appInstance.netplayLobby.GameSelectionChanged += OnGameSelectionChanged;
            appInstance.netplayLobby.HostBrowsingGameChanged += OnHostBrowsingGameChanged;
            appInstance.netplayLobby.StartRequested += OnStartRequested;
            appInstance.netplayLobby.LobbyClosed += OnLobbyClosed;
        }

        if (mainScene.lobbyActionButton != null)
        {
            mainScene.lobbyActionButton.Pressed += OnLobbyActionPressed;
        }

        if (mainScene.lobbyLeaveButton != null)
        {
            mainScene.lobbyLeaveButton.Pressed += LeaveLobby;
        }

        if (mainScene.lobbyCopyCodeButton != null)
        {
            mainScene.lobbyCopyCodeButton.Pressed += CopyJoinCodeToClipboard;
        }

        if (appInstance.downloadManager != null)
        {
            appInstance.downloadManager.DownloadCompleted += OnAnyDownloadCompleted;
        }

        if (appInstance.emulatorManager != null)
        {
            appInstance.emulatorManager.EmulatorInstallationCompleted += OnEmulatorInstallationCompleted;
            appInstance.emulatorManager.EmulatorLaunchStateChanged += OnEmulatorLaunchStateChanged;
        }

        appInstance.netplayDiscovery?.StartListening();

        ApplyLobbyVisibility();
    }

    private const string HostSessionArgument = "--netplay-host";
    private const string JoinSessionArgumentPrefix = "--netplay-join=";
    private const string AutoReadyArgument = "--netplay-auto-ready";
    private const string AutoStartArgument = "--netplay-auto-start";
    private const string AutoDownloadArgument = "--netplay-auto-download";
    private const string StartupGameArgumentPrefix = "--netplay-game=";

    private bool readiesAutomatically;
    private bool startsAutomatically;
    private bool downloadsAutomatically;
    private int startupRomId;

    public void ApplyStartupSessionArguments()
    {
        readiesAutomatically = OS.GetCmdlineUserArgs().Contains(AutoReadyArgument);
        startsAutomatically = OS.GetCmdlineUserArgs().Contains(AutoStartArgument);
        downloadsAutomatically = OS.GetCmdlineUserArgs().Contains(AutoDownloadArgument);
        startupRomId = ResolveStartupRomId();

        foreach (string startupArgument in OS.GetCmdlineUserArgs())
        {
            if (startupArgument == HostSessionArgument)
            {
                GD.Print("[Netplay] Hosting a session because of --netplay-host.");
                mainScene.PopupHandler.OnHostNetplayPressed();
                return;
            }

            if (startupArgument.StartsWith(JoinSessionArgumentPrefix))
            {
                string requestedHostAddress = startupArgument[JoinSessionArgumentPrefix.Length..];
                int lobbyPort = appInstance.netplayLobby?.ResolveLobbyPort() ?? 0;

                GD.Print($"[Netplay] Joining {requestedHostAddress}:{lobbyPort} because of --netplay-join.");

                if (!JoinLobby(requestedHostAddress, lobbyPort))
                {
                    GD.PrintErr($"[Netplay] Could not join {requestedHostAddress}:{lobbyPort}.");
                }

                return;
            }
        }
    }

    private static int ResolveStartupRomId()
    {
        string romArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith(StartupGameArgumentPrefix));

        return romArgument != null && int.TryParse(romArgument[StartupGameArgumentPrefix.Length..], out int parsedRomId)
            ? parsedRomId
            : 0;
    }

    private void ApplyStartupGameSelection()
    {
        if (startupRomId <= 0)
        {
            return;
        }

        var startupGame = ResolveGameByRomId(startupRomId);

        if (startupGame == null)
        {
            GD.PrintErr($"[Netplay] --netplay-game={startupRomId} is not in this library's cache.");
            return;
        }

        GD.Print($"[Netplay] Setting {startupGame.Name} because of --netplay-game.");
        PushHostGameSelection(startupGame);
        ReturnFocusToLobby();
    }

    public bool HostLobby(string joinCode)
    {
        if (appInstance.netplayLobby == null || !appInstance.netplayLobby.HostLobby())
        {
            return false;
        }

        activeJoinCode = joinCode;
        isLocallyReady = false;
        lastPushedRomId = 0;
        lastReportedReadinessSignature = null;

        appInstance.netplayDiscovery?.StartAdvertising(0, "", appInstance.netplayLobby.ResolveLobbyPort(), 1);

        CopyJoinCodeToClipboard();

        ApplyLobbyVisibility();
        mainScene.RefreshBrowseSourceForLobby();
        BeginBrowsingForLobbyGame();
        ApplyStartupGameSelection();

        return true;
    }

    public bool JoinLobby(string hostAddress, int lobbyPort)
    {
        if (appInstance.netplayLobby == null || !appInstance.netplayLobby.JoinLobby(hostAddress, lobbyPort))
        {
            return false;
        }

        activeJoinCode = null;
        isLocallyReady = false;
        lastReportedReadinessSignature = null;
        lobbyHostAddress = hostAddress;

        ApplyLobbyVisibility();
        mainScene.RefreshBrowseSourceForLobby();
        ReturnFocusToLobby();

        return true;
    }

    private string lobbyHostAddress;

    public void LeaveLobby()
    {
        appInstance.netplayLobby?.LeaveLobby();
        appInstance.netplayDiscovery?.StopAdvertising();
        appInstance.netplayManager?.EndSession();
        appInstance.netplayPortMapper?.ReleasePorts();

        activeJoinCode = null;
        isLocallyReady = false;
        lastReportedReadinessSignature = null;
        lobbyHostAddress = null;
        IsBrowsingForLobbyGame = false;

        ApplyLobbyVisibility();
        mainScene.RefreshBrowseSourceForLobby();
        mainScene.gameList?.GrabFocus();
    }

    public void PushHostGameSelection(Game selectedGame)
    {
        if (appInstance.netplayLobby == null || !appInstance.netplayLobby.IsHosting || selectedGame == null)
        {
            return;
        }

        if (selectedGame.Id == lastPushedRomId)
        {
            return;
        }

        if (appInstance.netplayManager != null && !appInstance.netplayManager.SupportsNetplayForGame(selectedGame))
        {
            return;
        }

        lastPushedRomId = selectedGame.Id;
        isLocallyReady = false;

        string hostEmulatorName = appInstance.emulatorManager.GetMappedEmulator(selectedGame.System?.Slug ?? selectedGame.PlatformSlug);
        string hostEmulatorVersion = appInstance.emulatorManager.GetInstalledVersion(hostEmulatorName);

        appInstance.netplayLobby.SelectGame(selectedGame.Id, selectedGame.Name, hostEmulatorName, hostEmulatorVersion, ResolvePublishedRomHash(selectedGame));
        appInstance.netplayDiscovery?.UpdateAdvertisement(selectedGame.Id, selectedGame.Name, appInstance.netplayLobby.Members.Count);

        RefreshLobbyPanel();
    }

    private const double BrowsingBroadcastSettleSeconds = 0.2;

    private int pendingBrowsingRomId;
    private bool isBrowsingBroadcastScheduled;

    public void PushHostBrowsingGame(Game highlightedGame)
    {
        if (!IsLobbyVisible || !appInstance.netplayLobby.IsHosting || highlightedGame == null)
        {
            return;
        }

        if (highlightedGame.Id == pendingBrowsingRomId)
        {
            return;
        }

        pendingBrowsingRomId = highlightedGame.Id;

        if (isBrowsingBroadcastScheduled)
        {
            return;
        }

        isBrowsingBroadcastScheduled = true;
        BroadcastBrowsingGameOnceScrollingSettles();
    }

    private async void BroadcastBrowsingGameOnceScrollingSettles()
    {
        await mainScene.ToSignal(mainScene.GetTree().CreateTimer(BrowsingBroadcastSettleSeconds), SceneTreeTimer.SignalName.Timeout);

        isBrowsingBroadcastScheduled = false;

        if (IsLobbyVisible && appInstance.netplayLobby.IsHosting)
        {
            appInstance.netplayLobby.BroadcastBrowsingGame(pendingBrowsingRomId);
        }
    }

    private void ApplyLobbyVisibility()
    {
        bool showLobby = IsLobbyVisible;

        if (mainScene.lobbyPanel != null)
        {
            mainScene.lobbyPanel.Visible = showLobby;
        }

        if (mainScene.detailsPanelContainer != null)
        {
            mainScene.detailsPanelContainer.Visible = !showLobby;
        }
    }

    private Game ResolveSelectedLobbyGame()
    {
        return ResolveGameByRomId(appInstance.netplayLobby?.SelectedRomId ?? 0);
    }

    private Game ResolveGameByRomId(int selectedRomId)
    {
        if (selectedRomId <= 0)
        {
            return null;
        }

        foreach (var cachedGames in appInstance.dataBus.gameCache)
        {
            if (cachedGames.Key < 0 || cachedGames.Value == null)
            {
                continue;
            }

            var matchingGame = cachedGames.Value.FirstOrDefault(game => game.Id == selectedRomId);

            if (matchingGame != null)
            {
                return matchingGame;
            }
        }

        return null;
    }

    public bool IsBrowsingForLobbyGame { get; private set; }

    public void BeginBrowsingForLobbyGame()
    {
        if (!IsLobbyVisible || !appInstance.netplayLobby.IsHosting)
        {
            return;
        }

        IsBrowsingForLobbyGame = true;
        mainScene.gameList?.GrabFocus();
        RefreshLobbyPanel();
        mainScene.GameListHandler.UpdateDetailsPanelButtons(mainScene.GameListHandler.currentlySelectedGame);
    }

    public bool HandleGameConfirmedInLobby()
    {
        if (!IsLobbyVisible)
        {
            return false;
        }

        if (!appInstance.netplayLobby.IsHosting || !IsBrowsingForLobbyGame)
        {
            PressFocusedLobbyButton();
            return true;
        }

        var selectedGame = mainScene.GameListHandler.currentlySelectedGame;

        if (selectedGame != null)
        {
            PushHostGameSelection(selectedGame);
        }

        ReturnFocusToLobby();

        return true;
    }

    private void PressFocusedLobbyButton()
    {
        if (mainScene.GetViewport().GuiGetFocusOwner() is BaseButton focusedButton && !focusedButton.Disabled)
        {
            focusedButton.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    public bool HandleLobbyBackPressed()
    {
        if (!IsLobbyVisible || !appInstance.netplayLobby.IsHosting || IsBrowsingForLobbyGame)
        {
            return false;
        }

        BeginBrowsingForLobbyGame();
        return true;
    }

    private void ReturnFocusToLobby()
    {
        IsBrowsingForLobbyGame = false;
        RefreshLobbyPanel();
        mainScene.GameListHandler.UpdateDetailsPanelButtons(mainScene.GameListHandler.currentlySelectedGame);
        FocusFirstAvailableLobbyControl();
    }

    private void FocusFirstAvailableLobbyControl()
    {
        foreach (var lobbyButton in LobbyButtonsInOrder())
        {
            if (IsLobbyButtonUsable(lobbyButton))
            {
                lobbyButton.GrabFocus();
                return;
            }
        }
    }

    private bool IsLobbyControlFocused()
    {
        var focusOwner = mainScene.GetViewport().GuiGetFocusOwner();

        return focusOwner != null
            && mainScene.lobbyPanel != null
            && (focusOwner == mainScene.lobbyPanel || mainScene.lobbyPanel.IsAncestorOf(focusOwner));
    }

    private void CopyJoinCodeToClipboard()
    {
        if (string.IsNullOrEmpty(activeJoinCode))
        {
            return;
        }

        DisplayServer.ClipboardSet(activeJoinCode);
        GD.Print($"[Netplay] Join code {activeJoinCode} copied to the clipboard.");

        if (mainScene.lobbyCopyCodeButton != null)
        {
            mainScene.lobbyCopyCodeButton.Text = "Copied";
        }
    }

    private GameActionState ResolveLobbyGameAction()
    {
        var selectedGame = ResolveSelectedLobbyGame();
        return selectedGame == null ? null : mainScene.GameListHandler.ResolveGameAction(selectedGame);
    }

    private bool IsLocallyReadyOrHosting => isLocallyReady || (appInstance.netplayLobby?.IsHosting ?? false);

    private bool LocalPlayerHasGame()
    {
        var lobbyGameAction = ResolveLobbyGameAction();

        return lobbyGameAction != null
            && lobbyGameAction.Kind == GameActionKind.LaunchGame
            && !NeedsEmulatorVersionChange()
            && isLocalRomMatchingHost
            && !isVerifyingLocalRom;
    }

    private static string ResolvePublishedRomHash(Game game)
    {
        return game?.Files == null || game.Files.Count == 0 ? null : game.Files[0].Md5Hash;
    }

    private string ResolveLocalRomPath(Game game)
    {
        string systemSlug = game?.System?.Slug ?? game?.PlatformSlug;

        if (game?.Files == null || game.Files.Count == 0 || string.IsNullOrEmpty(systemSlug))
        {
            return null;
        }

        return System.IO.Path.GetFullPath(System.IO.Path.Combine(appInstance.configManager.RomsPath, systemSlug, game.Files[0].FileName));
    }

    private string verifiedRomFilePath;
    private bool isLocalRomMatchingHost = true;
    private bool isVerifyingLocalRom;

    private void ResetLocalRomVerification()
    {
        verifiedRomFilePath = null;
        isLocalRomMatchingHost = true;
        isVerifyingLocalRom = false;
    }

    private async void VerifyLocalRomAgainstHost()
    {
        string expectedRomHash = appInstance.netplayLobby?.RequiredRomHash;
        string localRomPath = ResolveLocalRomPath(ResolveSelectedLobbyGame());

        if (string.IsNullOrEmpty(expectedRomHash) || string.IsNullOrEmpty(localRomPath) || !System.IO.File.Exists(localRomPath))
        {
            return;
        }

        if (isVerifyingLocalRom || verifiedRomFilePath == localRomPath)
        {
            return;
        }

        isVerifyingLocalRom = true;
        RefreshLobbyPanel();

        string computedRomHash = await System.Threading.Tasks.Task.Run(() => ComputeFileMd5(localRomPath));

        isVerifyingLocalRom = false;
        verifiedRomFilePath = localRomPath;
        isLocalRomMatchingHost = computedRomHash == null || string.Equals(computedRomHash, expectedRomHash, System.StringComparison.OrdinalIgnoreCase);

        if (!isLocalRomMatchingHost)
        {
            GD.PrintErr($"[Netplay] {System.IO.Path.GetFileName(localRomPath)} does not match the host: expected {expectedRomHash}, computed {computedRomHash}.");
        }

        ReportPreparednessIfInLobby();
    }

    private static string ComputeFileMd5(string filePath)
    {
        try
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var romStream = System.IO.File.OpenRead(filePath);

            return System.Convert.ToHexString(md5.ComputeHash(romStream)).ToLowerInvariant();
        }

        catch (System.Exception hashFailure)
        {
            GD.PrintErr($"[Netplay] could not hash {filePath}: {hashFailure.Message}");
            return null;
        }
    }

    private string ResolveRequiredEmulatorName()
    {
        string requiredEmulatorName = appInstance.netplayLobby?.RequiredEmulatorName;

        if (!string.IsNullOrEmpty(requiredEmulatorName))
        {
            return requiredEmulatorName;
        }

        var selectedGame = ResolveSelectedLobbyGame();
        return selectedGame == null ? null : appInstance.emulatorManager.GetMappedEmulator(selectedGame.System?.Slug ?? selectedGame.PlatformSlug);
    }

    private string ResolveLocalEmulatorVersion()
    {
        string requiredEmulatorName = ResolveRequiredEmulatorName();

        return string.IsNullOrEmpty(requiredEmulatorName) || !appInstance.emulatorManager.IsEmulatorInstalled(requiredEmulatorName)
            ? ""
            : appInstance.emulatorManager.GetInstalledVersion(requiredEmulatorName) ?? "";
    }

    private string ResolveTargetEmulatorVersion()
    {
        string targetVersion = appInstance.netplayLobby?.RequiredEmulatorVersion;
        string localVersion = ResolveLocalEmulatorVersion();

        if (!string.IsNullOrEmpty(localVersion) && (string.IsNullOrEmpty(targetVersion) || EmulatorVersions.Compare(localVersion, targetVersion) > 0))
        {
            targetVersion = localVersion;
        }

        foreach (var member in appInstance.netplayLobby?.Members ?? new System.Collections.Generic.List<NetplayLobby.LobbyMember>())
        {
            if (string.IsNullOrEmpty(member.EmulatorVersion))
            {
                continue;
            }

            if (string.IsNullOrEmpty(targetVersion) || EmulatorVersions.Compare(member.EmulatorVersion, targetVersion) > 0)
            {
                targetVersion = member.EmulatorVersion;
            }
        }

        return targetVersion;
    }

    private string DescribeVersionChange()
    {
        return EmulatorVersions.IsDowngrade(ResolveTargetEmulatorVersion(), ResolveLocalEmulatorVersion())
            ? "Downgrade"
            : "Update";
    }

    private bool NeedsEmulatorVersionChange()
    {
        string targetVersion = ResolveTargetEmulatorVersion();
        string requiredEmulatorName = ResolveRequiredEmulatorName();

        if (string.IsNullOrEmpty(targetVersion) || string.IsNullOrEmpty(requiredEmulatorName))
        {
            return false;
        }

        if (!appInstance.emulatorManager.IsEmulatorInstalled(requiredEmulatorName))
        {
            return false;
        }

        string installedVersion = ResolveLocalEmulatorVersion();

        if (string.IsNullOrEmpty(installedVersion))
        {
            return true;
        }

        return !string.Equals(installedVersion, targetVersion, System.StringComparison.OrdinalIgnoreCase);
    }

    private async void InstallRequiredEmulatorVersion()
    {
        string requiredEmulatorName = ResolveRequiredEmulatorName();
        string requiredVersion = ResolveTargetEmulatorVersion();

        if (string.IsNullOrEmpty(requiredEmulatorName) || string.IsNullOrEmpty(requiredVersion))
        {
            return;
        }

        var availableReleases = await appInstance.emulatorManager.GetAvailableReleases(requiredEmulatorName);
        var matchingRelease = availableReleases?.FirstOrDefault(release => string.Equals(release.VersionLabel, requiredVersion, System.StringComparison.OrdinalIgnoreCase));

        if (matchingRelease == null)
        {
            GD.PrintErr($"[Netplay] {requiredEmulatorName} {requiredVersion} is not available to install; the host may be on a build this client cannot reach.");
            return;
        }

        await appInstance.emulatorManager.InstallEmulator(requiredEmulatorName, matchingRelease);
    }

    private void OnAnyDownloadCompleted(string fileName, bool wasSuccessful)
    {
        OnLocalRomLibraryChanged();
    }

    public void OnLocalRomLibraryChanged()
    {
        ResetLocalRomVerification();
        VerifyLocalRomAgainstHost();
        ReportPreparednessIfInLobby();
    }

    private void OnEmulatorInstallationCompleted(string emulatorName, bool wasSuccessful)
    {
        ReportPreparednessIfInLobby();
    }

    private void ReportPreparednessIfInLobby()
    {
        if (!IsLobbyVisible)
        {
            return;
        }

        bool isPrepared = LocalPlayerHasGame();

        if (isPrepared && readiesAutomatically && !isLocallyReady)
        {
            isLocallyReady = true;
            GD.Print("[Netplay] Readying up because of --netplay-auto-ready.");
        }

        bool isReady = isPrepared && IsLocallyReadyOrHosting;
        string preparednessText = DescribeLocalPreparedness();
        string localVersion = ResolveLocalEmulatorVersion();
        string readinessSignature = $"{isPrepared}|{isReady}|{preparednessText}|{localVersion}";

        if (readinessSignature != lastReportedReadinessSignature)
        {
            lastReportedReadinessSignature = readinessSignature;
            GD.Print($"[Netplay] preparedness now \"{preparednessText}\" (prepared={isPrepared} ready={isReady}).");
            appInstance.netplayLobby.ReportLocalReadiness(isPrepared, isReady, preparednessText, localVersion);
        }

        RefreshLobbyPanel();
        AutoDownloadSelectedGameIfRequested();
        AutoStartHostedGameIfRequested();
    }

    private string lastReportedReadinessSignature;

    private void OnGameSelectionChanged(int romId)
    {
        isLocallyReady = false;
        lastReportedReadinessSignature = null;
        hasAutoStartedCurrentSelection = false;
        hasAutoDownloadedCurrentSelection = false;
        ResetLocalRomVerification();
        VerifyLocalRomAgainstHost();

        if (appInstance.netplayLobby != null && !appInstance.netplayLobby.IsHosting)
        {
            FollowHostGameSelection(romId);
        }

        ReportPreparednessIfInLobby();
    }

    private void FollowHostGameSelection(int romId)
    {
        lastPushedRomId = romId;
        MoveBrowsingToRom(romId);
    }

    private void OnHostBrowsingGameChanged(int romId)
    {
        if (appInstance.netplayLobby == null || appInstance.netplayLobby.IsHosting)
        {
            return;
        }

        MoveBrowsingToRom(romId);
    }

    private void MoveBrowsingToRom(int romId)
    {
        var browsedGame = ResolveGameByRomId(romId);

        if (browsedGame?.System == null)
        {
            return;
        }

        int systemIndex = mainScene.GameListHandler.gameSystems.FindIndex(system => system.Id == browsedGame.System.Id);

        if (systemIndex < 0)
        {
            return;
        }

        if (systemIndex != mainScene.GameListHandler.currentGameSystemIndex)
        {
            mainScene.GameListHandler.SelectGameOnceSystemSettles(romId);
            mainScene.SelectSystemFromCarousel(systemIndex);
            return;
        }

        mainScene.GameListHandler.SelectGameById(romId);
    }

    public void RefreshLobbyPanel()
    {
        ApplyLobbyVisibility();

        if (!IsLobbyVisible)
        {
            return;
        }

        var lobby = appInstance.netplayLobby;

        if (mainScene.lobbyCodeLabel != null)
        {
            mainScene.lobbyCodeLabel.Text = string.IsNullOrEmpty(activeJoinCode) ? "Joined Lobby" : activeJoinCode;
        }

        if (mainScene.lobbyCopyCodeButton != null)
        {
            mainScene.lobbyCopyCodeButton.Visible = !string.IsNullOrEmpty(activeJoinCode);
            mainScene.lobbyCopyCodeButton.Text = "Copy Code";
        }

        if (mainScene.lobbyStatusLabel != null)
        {
            mainScene.lobbyStatusLabel.Text = DescribeLobbyState(lobby);
        }

        var focusedBeforeRefresh = mainScene.GetViewport().GuiGetFocusOwner() as Button;

        RebuildPlayerRows();
        RefreshLobbyActionButton();

        if (IsBrowsingForLobbyGame || IsLobbyControlFocused())
        {
            return;
        }

        if (IsLobbyButtonUsable(focusedBeforeRefresh))
        {
            focusedBeforeRefresh.GrabFocus();
            return;
        }

        FocusFirstAvailableLobbyControl();
    }

    private bool hasAutoStartedCurrentSelection;

    private void AutoStartHostedGameIfRequested()
    {
        if (!startsAutomatically || hasAutoStartedCurrentSelection || hasHostedGameRunning || !IsLobbyVisible || !appInstance.netplayLobby.IsHosting)
        {
            return;
        }

        if (!appInstance.netplayLobby.AllMembersReady)
        {
            return;
        }

        var selectedGame = ResolveSelectedLobbyGame();

        if (selectedGame == null || !LocalPlayerHasGame())
        {
            return;
        }

        GD.Print("[Netplay] Starting the game because of --netplay-auto-start.");
        hasAutoStartedCurrentSelection = true;
        StartHostedGame(selectedGame);
    }

    private bool hasAutoDownloadedCurrentSelection;

    private void AutoDownloadSelectedGameIfRequested()
    {
        if (!downloadsAutomatically || hasAutoDownloadedCurrentSelection || !IsLobbyVisible)
        {
            return;
        }

        var selectedGame = ResolveSelectedLobbyGame();

        if (selectedGame == null || appInstance.downloadManager.IsDownloadingGame(selectedGame.Id.ToString()))
        {
            return;
        }

        if (ResolveLobbyGameAction()?.Kind != GameActionKind.DownloadGame)
        {
            return;
        }

        GD.Print($"[Netplay] Downloading {selectedGame.Name} because of --netplay-auto-download.");
        hasAutoDownloadedCurrentSelection = true;
        mainScene.DownloadHandler.DownloadGame(selectedGame);
    }

    private bool IsLobbyButtonUsable(Button lobbyButton)
    {
        return lobbyButton != null
            && !lobbyButton.Disabled
            && lobbyButton.Visible
            && mainScene.lobbyPanel != null
            && mainScene.lobbyPanel.IsAncestorOf(lobbyButton);
    }

    public bool HandleLobbyNavigation(int direction)
    {
        if (!IsLobbyVisible || IsBrowsingForLobbyGame || !IsLobbyControlFocused())
        {
            return false;
        }

        var navigableButtons = LobbyButtonsInOrder().Where(IsLobbyButtonUsable).ToList();

        if (navigableButtons.Count == 0)
        {
            return false;
        }

        int focusedIndex = navigableButtons.IndexOf(mainScene.GetViewport().GuiGetFocusOwner() as Button);

        if (focusedIndex < 0)
        {
            navigableButtons[0].GrabFocus();
            return true;
        }

        int targetIndex = (focusedIndex + direction + navigableButtons.Count) % navigableButtons.Count;
        navigableButtons[targetIndex].GrabFocus();

        return true;
    }

    private System.Collections.Generic.List<Button> LobbyButtonsInOrder()
    {
        var orderedButtons = new System.Collections.Generic.List<Button>();
        CollectButtonsInTreeOrder(mainScene.lobbyPanel, orderedButtons);

        return orderedButtons;
    }

    private static void CollectButtonsInTreeOrder(Node parentNode, System.Collections.Generic.List<Button> orderedButtons)
    {
        if (parentNode == null)
        {
            return;
        }

        foreach (Node childNode in parentNode.GetChildren())
        {
            if (childNode is Button childButton)
            {
                orderedButtons.Add(childButton);
            }

            CollectButtonsInTreeOrder(childNode, orderedButtons);
        }
    }

    private static string DescribeLobbyState(NetplayLobby lobby)
    {
        if (!lobby.IsHosting && lobby.Members.Count == 0)
        {
            return "Connecting to the host...";
        }

        if (!string.IsNullOrEmpty(lobby.SelectedGameName))
        {
            return lobby.SelectedGameName;
        }

        return lobby.IsHosting
            ? "Pick a game from the list to set it for everyone."
            : "Waiting for the host to pick a game.";
    }

    private void RebuildPlayerRows()
    {
        if (mainScene.lobbyPlayerList == null)
        {
            return;
        }

        foreach (Node existingRow in mainScene.lobbyPlayerList.GetChildren())
        {
            mainScene.lobbyPlayerList.RemoveChild(existingRow);
            existingRow.QueueFree();
        }

        foreach (var member in appInstance.netplayLobby.Members)
        {
            var memberRow = new Label
            {
                Text = $"{member.Username}   {DescribeMemberState(member)}",
                HorizontalAlignment = HorizontalAlignment.Center
            };

            memberRow.AddThemeFontSizeOverride("font_size", MemberRowFontSize);

            if (boldMemberFont != null)
            {
                memberRow.AddThemeFontOverride("font", boldMemberFont);
            }

            mainScene.lobbyPlayerList.AddChild(memberRow);
        }
    }

    private const int MemberRowFontSize = 24;

    private static readonly Font boldMemberFont = ResourceLoader.Exists(BoldFontPath)
        ? ResourceLoader.Load<Font>(BoldFontPath)
        : null;

    private const string BoldFontPath = "res://assets/fonts/Inter-Bold.woff2";

    private static string DescribeMemberState(NetplayLobby.LobbyMember member)
    {
        if (member.IsReady)
        {
            return "Ready";
        }

        return string.IsNullOrEmpty(member.Status) ? "Waiting" : member.Status;
    }

    private string DescribeLocalPreparedness()
    {
        var lobbyGameAction = ResolveLobbyGameAction();

        if (lobbyGameAction == null)
        {
            return "No game selected";
        }

        if (NeedsEmulatorVersionChange())
        {
            return $"Needs to {DescribeVersionChange().ToLowerInvariant()} {ResolveEmulatorDisplayName(ResolveRequiredEmulatorName())}";
        }

        if (isVerifyingLocalRom)
        {
            return "Checking game file";
        }

        if (!isLocalRomMatchingHost)
        {
            return "Game file does not match";
        }

        switch (lobbyGameAction.Kind)
        {
            case GameActionKind.InstallEmulator:
                return $"Needs {ResolveEmulatorDisplayName(lobbyGameAction.EmulatorName)}";

            case GameActionKind.DownloadGame:
                return "Needs game";

            case GameActionKind.Unavailable:
                return lobbyGameAction.Label;

            default:
                return IsLocallyReadyOrHosting ? "Ready" : "Not ready";
        }
    }

    private string ResolveEmulatorDisplayName(string emulatorName)
    {
        if (string.IsNullOrEmpty(emulatorName))
        {
            return "emulator";
        }

        string displayName = appInstance.emulatorManager.GetEmulatorDisplayName(emulatorName);
        return string.IsNullOrEmpty(displayName) ? emulatorName : displayName;
    }

    private void RefreshLobbyActionButton()
    {
        if (mainScene.lobbyActionButton == null)
        {
            return;
        }

        var lobby = appInstance.netplayLobby;
        var selectedGame = ResolveSelectedLobbyGame();

        if (selectedGame == null)
        {
            mainScene.lobbyActionButton.Disabled = true;
            mainScene.lobbyActionButton.Text = lobby.IsHosting ? "Select A Game" : "Waiting For Host";
            return;
        }

        var lobbyGameAction = ResolveLobbyGameAction();

        if (NeedsEmulatorVersionChange())
        {
            mainScene.lobbyActionButton.Disabled = false;
            mainScene.lobbyActionButton.Text = $"{DescribeVersionChange()} {ResolveEmulatorDisplayName(ResolveRequiredEmulatorName())}";
            return;
        }

        if (isVerifyingLocalRom)
        {
            mainScene.lobbyActionButton.Disabled = true;
            mainScene.lobbyActionButton.Text = "Checking Game File";
            return;
        }

        if (!isLocalRomMatchingHost)
        {
            mainScene.lobbyActionButton.Disabled = false;
            mainScene.lobbyActionButton.Text = $"Redownload {selectedGame.Name}";
            return;
        }

        if (lobbyGameAction.Kind == GameActionKind.InstallEmulator)
        {
            mainScene.lobbyActionButton.Disabled = false;
            mainScene.lobbyActionButton.Text = lobbyGameAction.Label;
            return;
        }

        if (lobbyGameAction.Kind == GameActionKind.DownloadGame)
        {
            mainScene.lobbyActionButton.Disabled = false;
            mainScene.lobbyActionButton.Text = $"Download {selectedGame.Name}";
            return;
        }

        if (lobbyGameAction.Kind == GameActionKind.Unavailable)
        {
            mainScene.lobbyActionButton.Disabled = true;
            mainScene.lobbyActionButton.Text = lobbyGameAction.Label;
            return;
        }

        if (!lobby.IsHosting)
        {
            mainScene.lobbyActionButton.Disabled = false;
            mainScene.lobbyActionButton.Text = isLocallyReady ? "Not Ready" : "Ready";
            return;
        }

        mainScene.lobbyActionButton.Disabled = !lobby.AllMembersReady;
        mainScene.lobbyActionButton.Text = lobby.AllMembersReady ? "Start Game" : "Waiting For Players";
    }

    private void OnLobbyActionPressed()
    {
        var lobby = appInstance.netplayLobby;
        var selectedGame = ResolveSelectedLobbyGame();

        if (lobby == null || selectedGame == null)
        {
            return;
        }

        var lobbyGameAction = ResolveLobbyGameAction();

        if (NeedsEmulatorVersionChange())
        {
            InstallRequiredEmulatorVersion();
            RefreshLobbyPanel();
            return;
        }

        if (!isLocalRomMatchingHost)
        {
            ResetLocalRomVerification();
            mainScene.DownloadHandler.DownloadGame(selectedGame);
            RefreshLobbyPanel();
            return;
        }

        if (lobbyGameAction.Kind == GameActionKind.InstallEmulator)
        {
            _ = appInstance.emulatorManager.InstallEmulator(lobbyGameAction.EmulatorName);
            RefreshLobbyPanel();
            return;
        }

        if (lobbyGameAction.Kind == GameActionKind.DownloadGame)
        {
            mainScene.DownloadHandler.DownloadGame(selectedGame);
            RefreshLobbyPanel();
            return;
        }

        if (lobbyGameAction.Kind != GameActionKind.LaunchGame)
        {
            return;
        }

        if (!lobby.IsHosting)
        {
            isLocallyReady = !isLocallyReady;
            lobby.ReportLocalReadiness(true, isLocallyReady, DescribeLocalPreparedness(), ResolveLocalEmulatorVersion());
            RefreshLobbyPanel();
            return;
        }

        StartHostedGame(selectedGame);
    }

    private bool hasHostedGameRunning;

    private void OnEmulatorLaunchStateChanged()
    {
        if (!IsLobbyVisible || appInstance.emulatorManager.IsEmulatorRunning || appInstance.emulatorManager.IsEmulatorLaunching)
        {
            return;
        }

        isLocallyReady = false;
        lastReportedReadinessSignature = null;

        if (hasHostedGameRunning)
        {
            hasHostedGameRunning = false;
            appInstance.netplayLobby?.ReleaseMembersToStop();
            appInstance.netplayLobby?.ClearMemberReadiness();
        }

        ReportPreparednessIfInLobby();
    }

    private void StartHostedGame(Game selectedGame)
    {
        var lobby = appInstance.netplayLobby;

        appInstance.netplayManager?.BeginHosting(appInstance.netplayManager.ResolveDefaultPort(null));

        int netplayPort = appInstance.netplayManager?.Port ?? 0;

        hasHostedGameRunning = true;
        appInstance.emulatorManager.LaunchEmulatorWithGame(selectedGame);

        string hostAddress = appInstance.netplayPortMapper?.ExternalAddress
            ?? appInstance.netplayManager?.ResolveLocalHostAddress();

        ReleaseMembersOnceHostIsListening(lobby, hostAddress, netplayPort);
    }

    private const double HostListeningPollSeconds = 0.25;
    private const double HostListeningTimeoutSeconds = 60.0;

    private async void ReleaseMembersOnceHostIsListening(NetplayLobby lobby, string hostAddress, int netplayPort)
    {
        if (netplayPort <= 0)
        {
            GD.PrintErr("[Netplay] no netplay port was resolved for this session; releasing the other players immediately.");
            lobby.ReleaseMembersToStart(hostAddress, netplayPort);
            return;
        }

        double secondsWaited = 0.0;

        while (secondsWaited < HostListeningTimeoutSeconds && !IsAnythingListeningOn(netplayPort))
        {
            await mainScene.ToSignal(mainScene.GetTree().CreateTimer(HostListeningPollSeconds), SceneTreeTimer.SignalName.Timeout);
            secondsWaited += HostListeningPollSeconds;
        }

        if (IsAnythingListeningOn(netplayPort))
        {
            GD.Print($"[Netplay] the emulator is listening on {netplayPort} after {secondsWaited:0.0}s; releasing the other players.");
        }

        else
        {
            GD.PrintErr($"[Netplay] nothing started listening on {netplayPort}; releasing the other players anyway.");
        }

        lobby.ReleaseMembersToStart(hostAddress, netplayPort);
    }

    private static bool IsAnythingListeningOn(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(listeningEndPoint => listeningEndPoint.Port == port);
        }

        catch (System.Exception)
        {
            return false;
        }
    }

    private async void OnStartRequested(string hostAddress, int netplayPort)
    {
        var selectedGame = ResolveSelectedLobbyGame();

        if (selectedGame == null || appInstance.netplayManager == null)
        {
            return;
        }

        string reachableHostAddress = string.IsNullOrEmpty(lobbyHostAddress) ? hostAddress : lobbyHostAddress;

        GD.Print($"[Netplay] Connecting to {reachableHostAddress}:{netplayPort} (host advertised {hostAddress}).");

        appInstance.netplayManager.BeginJoining(reachableHostAddress, netplayPort);
        appInstance.emulatorManager.LaunchEmulatorWithGame(selectedGame);
    }

    private void OnLobbyClosed(string reason)
    {
        activeJoinCode = null;
        isLocallyReady = false;
        lastReportedReadinessSignature = null;
        lobbyHostAddress = null;
        IsBrowsingForLobbyGame = false;

        appInstance.netplayDiscovery?.StopAdvertising();
        appInstance.netplayManager?.EndSession();
        appInstance.netplayPortMapper?.ReleasePorts();

        ApplyLobbyVisibility();
        mainScene.RefreshBrowseSourceForLobby();
        mainScene.gameList?.GrabFocus();

        GD.Print($"[Netplay] Lobby closed: {reason}");
    }
}
