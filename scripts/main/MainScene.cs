using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FileAccess = Godot.FileAccess;

public partial class MainScene : Control
{

    [ExportGroup("Header")] 
    [Export] private MarginContainer headerContainer;
    [Export] private Label platformLabel;
    [Export] private TextureRect platformIcon;

    public List<GameSystem> gameSystems = new List<GameSystem>();
    public Dictionary<int, List<Game>> games { get; set; } = new Dictionary<int, List<Game>>();
    private List<Game> currentlyShownGames = new List<Game>();
    private bool showOnlyInstalledGames = false;
    public int currentGameSystemIndex;
    public Game currentlySelectedGame; 

    [ExportGroup("GameList")] 
    [Export] private Control gameList;
    [Export] private PackedScene gameListEntryScene;
    
    [ExportGroup("DetailsPanel")]
    [Export] private VBoxContainer detailsPanelContainer;
    [Export] private TextureRect gameCover;
    [Export] private TextureRect gameMarquee;
    [Export] private Label gameTitle;
    [Export] private RichTextLabel gameDescription;
    [Export] private TextureRect installedIcon;

    [ExportGroup("DowloadsList")]
    [Export] private MarginContainer downloadsListContainer;
    [Export] private DownloadProgressUI downloadProgressUI;

    [ExportGroup("Footer Buttons & Containers")]
    [Export] private Control gameListFooter;
    [Export] private Control downloadsFooter;
    [Export] private Control settingsFooter;
    
    [Export] private Button actionBtn;
    [Export] private Button deleteBtn;
    [Export] private Button optionsBtn;
    [Export] private Button filterInstalledGamesBtn;
    [Export] private Button toggleDownloadsBtn;
    [Export] private Button downloadsToggleDownloadsBtn;
    [Export] private Button navHintBtn;
    [Export] private Button cancelDownloadBtn;
    [Export] private Button settingsSelectBtn;
    [Export] private Button settingsBackBtn;

    [ExportGroup("PopupOptions")]
    [Export] public Node LaunchEmulatorPopupOption;
    [Export] public Node SelectBiosPopupOption;
    [Export] public Node SettingsPopupOption;
    [Export] public Node RefreshGamesPopupOption;
    [Export] public Node RefreshCurrentSystemGamesPopupOption;
    [Export] public Node QuitPopupOption;
    [Export] public Node RandomGamePopupOption;

    [ExportGroup("Update & Refresh UI")]
    [Export] private Control downloadProgressPopup;
    [Export] private Label downloadProgressLabel;
    [Export] private ProgressBar downloadProgressBar;
    
    [Export] private Control changelogPopup;
    [Export] private RichTextLabel changelogRichTextLabel;
    [Export] private Button acceptUpdateBtn;
    [Export] private Button cancelUpdateBtn;

    private AppInstance appInstance;
    private ImageTexture placeholderTexture;
    [Export] private TextureRect backgroundRect;
    [Export] private VBoxContainer mainVBoxContainer;

    [Export] private Control startMenu;
    [Export] private Control startMenuContainer;
    [Export] private Control biosSelectorContainer;
    [Export] private VBoxContainer biosSelector;
    private Control startMenuRoot;

    [Export] private MarginContainer settingsMenuContainer;
    [Export] private Tree settingsSectionsTree;
    [Export] private VBoxContainer sectionOptionsContainer;
    
    public override void _Ready()
    {
        var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        whiteImage.Fill(Colors.White);
        placeholderTexture = ImageTexture.CreateFromImage(whiteImage);
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        
        appInstance.downloadManager.DownloadCompleted += OnDownloadCompleted; 
        
        appInstance.emulatorManager.EmulatorInstallationCompleted += OnEmulatorInstallationCompleted;
        appInstance.assetManager.AssetDownloaded += OnAssetDownloaded;

        if (startMenuContainer != null)
        {
            startMenuRoot = startMenuContainer.GetParent()?.GetParent() as Control;

            if (startMenuRoot != null)
            {
                startMenuRoot.Visible = false;
            }

            if (LaunchEmulatorPopupOption is Button launchBtn)
            {
                launchBtn.Pressed += OnLaunchEmulatorPressed;
            }

            if (SelectBiosPopupOption is Button biosBtn)
            {
                biosBtn.Pressed += OnSelectBiosMenuPressed;
            }

            if (SettingsPopupOption is Button settingsBtn)
            {
                settingsBtn.Pressed += OnSettingsMenuPressed;
            }

            if (RefreshGamesPopupOption is Button refreshBtn)
            {
                refreshBtn.Pressed += OnRefreshGamesPressed;
            }

            if (RefreshCurrentSystemGamesPopupOption is Button refreshSysBtn)
            {
                refreshSysBtn.Pressed += OnRefreshCurrentSystemGamesPressed;
            }

            if (QuitPopupOption is Button quitBtn)
            {
                quitBtn.Pressed += OnQuitPressed;
            }

            if (RandomGamePopupOption is Button randomGameBtn)
            {
                randomGameBtn.Pressed += OnRandomGamePressed;
            }
        }

        if (settingsMenuContainer != null)
        {
            settingsMenuContainer.Visible = false;
            settingsSectionsTree?.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            SetupSettingsTree();
        }

        if (acceptUpdateBtn != null)
        {
            acceptUpdateBtn.Pressed += OnAcceptUpdatePressed;
        }
        
        if (cancelUpdateBtn != null)
        {
            cancelUpdateBtn.Pressed += OnCancelUpdatePressed;
        }

        GetCache();
        SelectSystemByIndex(0);
        SetupGameList();
        SetupDownloadsList();
        SetupFooterUI();
        
        InitUpdater();
    }

    private AppUpdater appUpdater;
    private string pendingUpdateVersion;

    private void InitUpdater()
    {
        appUpdater = GetNodeOrNull<AppUpdater>("/root/AppUpdater");

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

        if (changelogPopup != null && changelogRichTextLabel != null)
        {
            string sanitizedNotes = releaseNotes.Replace("\r", "").Replace("\b", "");
            changelogRichTextLabel.Text = $"[b]A new version ({version}) of Romm Frontend is available.[/b]\n\nRelease Notes:\n{sanitizedNotes}";
            changelogPopup.Visible = true;

            if (acceptUpdateBtn != null)
            {
                acceptUpdateBtn.Text = "Select";
            }
            if (cancelUpdateBtn != null)
            {
                cancelUpdateBtn.Text = "Close";
            }
        }
    }

    private void OnUpdateDownloadProgress(float progress)
    {
        if (downloadProgressBar != null)
        {
            downloadProgressBar.Value = progress * 100.0f;
        }
    }

    private async void OnUpdateDownloadCompleted(bool success)
    {
        if (success)
        {
            if (downloadProgressLabel != null)
            {
                downloadProgressLabel.Text = "Download complete. Restarting to apply update...";
            }

            await ToSignal(GetTree().CreateTimer(3.0f), "timeout");
            appUpdater.ApplyUpdateAndRestart();
        }

        else
        {
            if (downloadProgressLabel != null)
            {
                downloadProgressLabel.Text = "Failed to download update. Please try again later.";
            }

            await ToSignal(GetTree().CreateTimer(3.0f), "timeout");

            if (downloadProgressPopup != null)
            {
                downloadProgressPopup.Visible = false;
            }
        }
    }

    private void ToggleSettingsMenu()
    {
        if (settingsMenuContainer != null)
        {
            var gamesListContainer = gameList?.GetParent()?.GetParent<Control>();

            if (settingsMenuContainer.Visible)
            {
                settingsMenuContainer.Visible = false;

                if (settingsFooter != null)
                {
                    settingsFooter.Visible = false;
                }

                if (gamesListContainer != null)
                {
                    gamesListContainer.Visible = true;
                }

                if (gameListFooter != null)
                {
                    gameListFooter.Visible = (downloadsListContainer == null || !downloadsListContainer.Visible);
                }

                if (downloadsFooter != null)
                {
                    downloadsFooter.Visible = (downloadsListContainer != null && downloadsListContainer.Visible);
                }

                gameList?.GrabFocus();
            }

            else
            {
                settingsMenuContainer.Visible = true;

                if (settingsFooter != null)
                {
                    settingsFooter.Visible = true;
                }

                if (gamesListContainer != null)
                {
                    gamesListContainer.Visible = false;
                }

                if (gameListFooter != null)
                {
                    gameListFooter.Visible = false;
                }

                if (downloadsFooter != null)
                {
                    downloadsFooter.Visible = false;
                }

                settingsSectionsTree?.GrabFocus();
            }

            UpdateHeaderLabel();
        }
    }

    private void OnLaunchEmulatorPressed()
    {
        if (gameSystems == null || currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
        {
            return;
        }

        var system = gameSystems[currentGameSystemIndex];
        string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(system.Slug);

        if (!string.IsNullOrEmpty(mappedEmulator))
        {
            appInstance.emulatorManager.LaunchEmulatorWithoutGame(mappedEmulator, system);
        }


        if (startMenuRoot != null)
        {
            startMenuRoot.Visible = false;
        }

        gameList?.GrabFocus();
    }

    private void OnSelectBiosMenuPressed()
    {
        if (startMenuContainer != null)
        {
            startMenuContainer.Visible = false;
        }

        if (biosSelectorContainer != null)
        {
            biosSelectorContainer.Visible = true;
        }

        PopulateBiosSelector();
    }

    private void PopulateBiosSelector()
    {
        if (biosSelector == null)
        {
            return;
        }

        foreach (Node child in biosSelector.GetChildren())
        {
            biosSelector.RemoveChild(child);
            child.QueueFree();
        }

        if (gameSystems == null || currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
        {
            return;
        }

        var system = gameSystems[currentGameSystemIndex];

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

                    if (biosSelectorContainer != null)
                    {
                        biosSelectorContainer.Visible = false;
                    }

                    if (startMenuContainer != null)
                    {
                        startMenuContainer.Visible = true;
                    } (SelectBiosPopupOption as Control)?.GrabFocus();
                };
                biosSelector.AddChild(btn);
            }
        }

        else
        {
            Label lbl = new Label();
            lbl.Text = "No bios/firmware found.";
            biosSelector.AddChild(lbl);
        }

        if (biosSelector.GetChildCount() > 0 && biosSelector.GetChild(0) is Control firstChild)
        {
            firstChild.GrabFocus();
        }
    }

    private void OnSettingsMenuPressed()
    {
        if (startMenuRoot != null)
        {
            startMenuRoot.Visible = false;
        }

        ToggleSettingsMenu();
    }

    private void OnRefreshGamesPressed()
    {
        appInstance.cacheManager?.RebuildGameCache();
    }

    private async void OnRefreshCurrentSystemGamesPressed()
    {
        if (gameSystems == null || currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
        {
            return;
        }

        if (startMenuRoot != null)
        {
            startMenuRoot.Visible = false;
        }

        if (downloadProgressPopup != null) 
        {
            downloadProgressPopup.Visible = true;

            if (downloadProgressLabel != null)
            {
                downloadProgressLabel.Text = "Refreshing games...";
            }

            if (downloadProgressBar != null)
            {
                downloadProgressBar.Value = 0;
            }
        }
        
        GameSystem currentSystem = gameSystems[currentGameSystemIndex];
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

                if (downloadProgressLabel != null)
                {
                    downloadProgressLabel.Text = $"Found {allGamesForSystem.Count} games...";
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
        
        games = appInstance.dataBus.gameCache;
        ApplyFiltersAndRefresh();
        
        if (downloadProgressBar != null)
        {
            downloadProgressBar.Value = 100;
        }

        if (downloadProgressLabel != null)
        {
            downloadProgressLabel.Text = "Refresh complete!";
        }

        await ToSignal(GetTree().CreateTimer(1.5f), "timeout");

        if (downloadProgressPopup != null)
        {
            downloadProgressPopup.Visible = false;
        }

        gameList?.GrabFocus();
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnRandomGamePressed()
    {
        if (currentlyShownGames.Count > 0)
        {
            int randomIndex = new Random().Next(currentlyShownGames.Count);
            OnGameSelected(randomIndex);

            if (gameList != null && gameList.HasMethod("Refresh"))
            {
                gameList.Set("SelectedIndex", randomIndex);
                gameList.Call("Refresh");
            }

            if (startMenuRoot != null)
            {
                startMenuRoot.Visible = false;
            }
            
            gameList?.GrabFocus();
        }
    }

    private void OnAssetDownloaded(int gameId, string assetType)
    {
        if (currentlySelectedGame != null && currentlySelectedGame.Id == gameId)
        {
            ShowGameDetails(currentlySelectedGame);
        }
    }
    
    private string fuzzySearchBuffer = "";
    private ulong lastKeystrokeTime = 0;
    
    private void OnAcceptUpdatePressed()
    {
        if (changelogPopup != null) changelogPopup.Visible = false;

        if (!string.IsNullOrEmpty(pendingUpdateVersion))
        {
            if (downloadProgressPopup != null)
            {
                downloadProgressPopup.Visible = true;
            }

            if (downloadProgressLabel != null)
            {
                downloadProgressLabel.Text = "Downloading...";
            }

            if (downloadProgressBar != null)
            {
                downloadProgressBar.Value = 0;
            }

            _ = appUpdater.DownloadUpdateAsync(pendingUpdateVersion);
        }
    }

    private void OnCancelUpdatePressed()
    {
        if (changelogPopup != null) changelogPopup.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (changelogPopup != null && changelogPopup.Visible)
        {
            GetViewport().SetInputAsHandled();

            if (@event.IsActionPressed("ui_accept"))
            {
                OnAcceptUpdatePressed();
            }

            else if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                OnCancelUpdatePressed();
            }

            return;
        }

        if (appInstance.emulatorManager != null && appInstance.emulatorManager.IsEmulatorRunning)
        {
            GetViewport().SetInputAsHandled();

            bool isComboPressed = true;
            int hotkeyCount = appInstance.configManager.EmulatorCloseHotkeyCount;

            if (hotkeyCount > 0)
            {
                for (int i = 1; i <= hotkeyCount; i++)
                {
                    if (!Input.IsActionPressed($"CloseKey{i}"))
                    {
                        isComboPressed = false;
                        break;
                    }
                }
            }

            else
            {
                isComboPressed = false;
            }

            if(isComboPressed)  
            {
                appInstance.emulatorManager.CloseEmulator();
            }

            return;
        }

        if (@event.IsActionPressed("ToggleSettings"))
        {
            if (settingsMenuContainer != null && settingsMenuContainer.Visible)
            {
                ToggleSettingsMenu();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (startMenuRoot != null)
            {
                if (startMenuRoot.Visible)
                {
                    startMenuRoot.Visible = false;
                    gameList?.GrabFocus();
                }

                else if (downloadsListContainer == null || !downloadsListContainer.Visible)
                {
                    startMenuRoot.Visible = true;

                    if (startMenuContainer != null)
                    {
                        startMenuContainer.Visible = true;
                    }

                    if (biosSelectorContainer != null)
                    {
                        biosSelectorContainer.Visible = false;
                    }

                    if (LaunchEmulatorPopupOption is Control launchBtn)
                    {
                        launchBtn.GrabFocus();
                    }
                }
            }

            GetViewport().SetInputAsHandled();
            return;
        }
        
        bool isAnyPopupVisible = (startMenuRoot != null && startMenuRoot.Visible) || 
                                 (settingsMenuContainer != null && settingsMenuContainer.Visible) ||
                                 (downloadsListContainer != null && downloadsListContainer.Visible);

        if (!isAnyPopupVisible && @event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Unicode != 0)
            {
                ulong currentTime = Time.GetTicksMsec();
                if (currentTime - lastKeystrokeTime > 1500)
                {
                    fuzzySearchBuffer = "";
                }
                
                fuzzySearchBuffer += (char)keyEvent.Unicode;
                fuzzySearchBuffer = fuzzySearchBuffer.ToLower();
                lastKeystrokeTime = currentTime;

                int matchIndex = currentlyShownGames.FindIndex(g => g.Name.ToLower().Contains(fuzzySearchBuffer));
                
                if (matchIndex != -1)
                {
                    OnGameSelected(matchIndex);
                    if (gameList != null && gameList.HasMethod("Refresh"))
                    {
                        gameList.Set("SelectedIndex", matchIndex);
                        gameList.Call("Refresh");
                    }
                }
            }
        }

        if (startMenuRoot != null && startMenuRoot.Visible)
        {
            if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                if (biosSelectorContainer != null && biosSelectorContainer.Visible)
                {
                    biosSelectorContainer.Visible = false;

                    if (startMenuContainer != null)
                    {
                        startMenuContainer.Visible = true;
                    } (SelectBiosPopupOption as Control)?.GrabFocus();
                }

                else
                {
                    startMenuRoot.Visible = false;
                    gameList?.GrabFocus();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            else if (@event.IsActionPressed("ui_up", true) || @event.IsActionPressed("MoveUp"))
            {
                CycleFocusInContainer(biosSelectorContainer != null && biosSelectorContainer.Visible ? biosSelectorContainer : startMenu, -1);
                GetViewport().SetInputAsHandled();
                return;
            }

            else if (@event.IsActionPressed("ui_down", true) || @event.IsActionPressed("MoveDown"))
            {
                CycleFocusInContainer(biosSelectorContainer != null && biosSelectorContainer.Visible ? biosSelectorContainer : startMenu, 1);
                GetViewport().SetInputAsHandled();
                return;
            }

            else if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("Select"))
            {
                var focusOwner = GetViewport().GuiGetFocusOwner();

                if (focusOwner is BaseButton btn)
                {
                    btn.EmitSignal(BaseButton.SignalName.Pressed);
                    GetViewport().SetInputAsHandled();
                }

                return;
            }

            return;
        }

        if (settingsMenuContainer != null && settingsMenuContainer.Visible)
        {
            var focusOwner = GetViewport().GuiGetFocusOwner();
            bool isFocusInTree = (focusOwner == settingsSectionsTree);
            bool isFocusInOptions = (focusOwner != null && sectionOptionsContainer.IsAncestorOf(focusOwner));

            if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                if (isFocusInOptions)
                {
                    settingsSectionsTree?.GrabFocus();
                }

                else
                {
                    ToggleSettingsMenu();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            else if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("Select"))
            {
                if (isFocusInTree)
                {
                    var visibleForm = GetVisibleSettingsForm();

                    if (visibleForm != null)
                    {
                        var firstFocusable = FindFirstFocusable(visibleForm);

                        if (firstFocusable != null)
                        {
                            firstFocusable.GrabFocus();
                            GetViewport().SetInputAsHandled();
                            return;
                        }
                    }
                }
            }

            else if (@event.IsActionPressed("ui_left", true) || @event.IsActionPressed("ui_right", true))
            {
                if (isFocusInOptions)
                {
                    int direction = @event.IsAction("ui_right") ? 1 : -1;
                    CycleFocusedOption(direction);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }

            return; 
        }

        if(@event.IsActionPressed("CylceSystemUp") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            CycleSelectedSystemNext();
            GetViewport().SetInputAsHandled();
            return;
        }
        
        if (@event.IsActionPressed("CycleSystemDown") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            CycleSelectedSystemLast();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("Select") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            OnPlayDownloadButtonPressed();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ToggleInstalled") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            OnFilterInstalledGamesPressed();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("DeleteGame") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            if (deleteBtn != null && !deleteBtn.Disabled && deleteBtn.Visible)
            {
                OnDeleteButtonPressed();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ToggleDownloadsPage"))
        {
            SwapLists();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("CancelDownload"))
        {
            if (downloadsListContainer != null && downloadsListContainer.Visible)
            {
                OnCancelDownloadPressed();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("ui_up", true) || @event.IsActionPressed("MoveUp"))
        {
            if (downloadsListContainer != null && downloadsListContainer.Visible)
            {
                if (downloadProgressUI is DownloadProgressUI dpUI)
                {
                    dpUI.CycleSelection(-1);
                }

                GetViewport().SetInputAsHandled();
            }

            return;
        }
        
        if (@event.IsActionPressed("ui_down", true) || @event.IsActionPressed("MoveDown"))
        {
            if (downloadsListContainer != null && downloadsListContainer.Visible)
            {
                if (downloadProgressUI is DownloadProgressUI dpUI)
                {
                    dpUI.CycleSelection(1);
                }

                GetViewport().SetInputAsHandled();
            }

            return;
        }
    }

    public void SetupFooterUI()
    {

        SetupButton(actionBtn, "Select", "Play");

        if (actionBtn != null)
        {
            actionBtn.Pressed += OnPlayDownloadButtonPressed;
        }

        SetupButton(deleteBtn, "DeleteGame", "Delete");

        if (deleteBtn != null)
        {
            deleteBtn.Pressed += OnDeleteButtonPressed;
        }

        SetupButton(settingsSelectBtn, "Select", "Select");
        SetupButton(settingsBackBtn, "Back", "Back");

        SetupButton(optionsBtn, "ToggleSettings", "Options");

        if (optionsBtn != null)
        {
            optionsBtn.Pressed += OnOptionsPressed;
        }

        SetupButton(filterInstalledGamesBtn, "ToggleInstalled", "All Games");

        if (filterInstalledGamesBtn != null)
        {
            filterInstalledGamesBtn.Pressed += OnFilterInstalledGamesPressed;
        }

        SetupButton(toggleDownloadsBtn, "ToggleDownloadsPage", "Downloads");

        if (toggleDownloadsBtn != null)
        {
            toggleDownloadsBtn.Pressed += SwapLists;
        }

        SetupButton(downloadsToggleDownloadsBtn, "ToggleDownloadsPage", "Games");

        if (downloadsToggleDownloadsBtn != null)
        {
            downloadsToggleDownloadsBtn.Pressed += SwapLists;
        }

        SetupButton(navHintBtn, "MoveUp", "Navigate");

        if (navHintBtn != null)
        {
            navHintBtn.Disabled = true;
        }

        SetupButton(cancelDownloadBtn, "CancelDownload", "Cancel");

        if (cancelDownloadBtn != null)
        {
            cancelDownloadBtn.Pressed += OnCancelDownloadPressed;
        }
    }

    private void SetupButton(Button btn, string iconPath, string defaultText)
    {
        if (btn == null)
        {
            return;
        }

        btn.Text = defaultText;
        btn.ThemeTypeVariation = "FlatButton";
        
        var iconTex = new ControllerIconTexture();
        iconTex.path = iconPath;
        btn.Icon = iconTex;
    }



    private void OnOptionsPressed()
    {

    }

    private void OnFilterInstalledGamesPressed()
    {
        showOnlyInstalledGames = !showOnlyInstalledGames;

        if (filterInstalledGamesBtn != null)
        {
            filterInstalledGamesBtn.Text = showOnlyInstalledGames ? "Installed" : "All Games";
        }
        
        ApplyFiltersAndRefresh();
    }

    private void OnCancelDownloadPressed()
    {
        if (downloadProgressUI is DownloadProgressUI dpUI)
        {
            dpUI.CancelSelectedDownload();
        }
    }
    
    public void GetCache()
    {
        if (appInstance.configManager.ShowAllSystems)
        {
            gameSystems = appInstance.dataBus.systems;
        }

        else
        {
            gameSystems = appInstance.dataBus.systems.Where(sys => 
            {
                string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(sys.Slug);
                return !string.IsNullOrEmpty(mappedEmulator) && appInstance.emulatorManager.LoadEmulatorMetadataFromDisk(mappedEmulator) != null;
            }).ToList();

            if (gameSystems.Count == 0)
            {
                gameSystems = appInstance.dataBus.systems;
            }
        }

        games = appInstance.dataBus.gameCache;
        
    }
    
    public void SetupGameList()
    {
        if (gameList != null)
        {
            gameList.Connect("ItemSelected", Callable.From<long>(OnGameSelected));
            gameList.Connect("ItemFocused", Callable.From<long>(OnGameSelected));
            gameList.Connect("JumpSectionRequested", Callable.From<int>(OnJumpSectionRequested));
        }
    }
    
    private void SetupDownloadsList()
    {
        if (downloadsListContainer != null)
        {
            downloadsListContainer.Visible = false;
        }
    }
    


    private void SwapLists()
    {
        if (downloadsListContainer != null && gameList != null)
        {
            downloadsListContainer.Visible = !downloadsListContainer.Visible;

            var gamesListContainer = gameList.GetParent().GetParent<Control>();

            if (gamesListContainer != null)
            {
                gamesListContainer.Visible = !downloadsListContainer.Visible;
            }

            if (!downloadsListContainer.Visible)
            {
                gameList.GrabFocus();
            }

            if (gameListFooter != null)
            {
                gameListFooter.Visible = !downloadsListContainer.Visible;
            }

            if (downloadsFooter != null)
            {
                downloadsFooter.Visible = downloadsListContainer.Visible;
            }

            UpdateHeaderLabel();
        }
    }

    private bool isTransitioningSystem = false;
    
    private async void TransitionToSystem(int targetIndex)
    {
        if (isTransitioningSystem)
        {
            return;
        }

        isTransitioningSystem = true;
        
        float duration = 0.2f;

        Tween fadeOutTween = CreateTween();
        
        Color glColorOut = gameList.Modulate; glColorOut.A = 0.0f;
        fadeOutTween.TweenProperty(gameList, "modulate", glColorOut, duration);
        
        if (platformIcon != null) {
            Color piColorOut = platformIcon.Modulate; piColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(platformIcon, "modulate", piColorOut, duration);
        }


        if (platformLabel != null) {
            Color plColorOut = platformLabel.Modulate; plColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(platformLabel, "modulate", plColorOut, duration);
        }


        if (detailsPanelContainer != null) {
            Color dpcColorOut = detailsPanelContainer.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(detailsPanelContainer, "modulate", dpcColorOut, duration);
        }
            
        await ToSignal(fadeOutTween, Tween.SignalName.Finished);

        var glModOut = gameList.Modulate; glModOut.A = 0.0f; gameList.Modulate = glModOut;

        if (platformIcon != null) { var piMod = platformIcon.Modulate; piMod.A = 0.0f; platformIcon.Modulate = piMod; }

        if (platformLabel != null) { var plMod = platformLabel.Modulate; plMod.A = 0.0f; platformLabel.Modulate = plMod; }

        if (detailsPanelContainer != null) { var dpcMod = detailsPanelContainer.Modulate; dpcMod.A = 0.0f; detailsPanelContainer.Modulate = dpcMod; }

        SelectSystemByIndex(targetIndex);

        await ToSignal(GetTree(), "process_frame");

        await ToSignal(GetTree(), "process_frame"); // Two frames for good measure

        Tween fadeInTween = CreateTween();
        
        Color glColorIn = gameList.Modulate; glColorIn.A = 1.0f;
        fadeInTween.TweenProperty(gameList, "modulate", glColorIn, duration);
        
        if (platformIcon != null) {
            Color piColorIn = platformIcon.Modulate; piColorIn.A = 1.0f;
            fadeInTween.Parallel().TweenProperty(platformIcon, "modulate", piColorIn, duration);
        }


        if (platformLabel != null) {
            Color plColorIn = platformLabel.Modulate; plColorIn.A = 1.0f;
            fadeInTween.Parallel().TweenProperty(platformLabel, "modulate", plColorIn, duration);
        }


        if (detailsPanelContainer != null) {
            Color dpcColorIn = detailsPanelContainer.Modulate; dpcColorIn.A = 1.0f;
            fadeInTween.Parallel().TweenProperty(detailsPanelContainer, "modulate", dpcColorIn, duration);
        }

        await ToSignal(fadeInTween, Tween.SignalName.Finished);

        var glModIn = gameList.Modulate; glModIn.A = 1.0f; gameList.Modulate = glModIn;

        if (platformIcon != null) { var piMod = platformIcon.Modulate; piMod.A = 1.0f; platformIcon.Modulate = piMod; }

        if (platformLabel != null) { var plMod = platformLabel.Modulate; plMod.A = 1.0f; platformLabel.Modulate = plMod; }

        if (detailsPanelContainer != null) { var dpcMod = detailsPanelContainer.Modulate; dpcMod.A = 1.0f; detailsPanelContainer.Modulate = dpcMod; }
        
        isTransitioningSystem = false;
    }

    public void CycleSelectedSystemNext()
    {
        if (currentGameSystemIndex == gameSystems.Count - 1)
        {
            TransitionToSystem(0);
        }

        else
        {
            TransitionToSystem(currentGameSystemIndex + 1);
        }
    }

    public void CycleSelectedSystemLast()
    {
        if (currentGameSystemIndex == 0)
        {
            TransitionToSystem(gameSystems.Count - 1);
        }

        else
        {
            TransitionToSystem(currentGameSystemIndex - 1);
        }
    }
    
    private void SelectSystemByIndex(int index)
    {
        if (index < 0 || index >= gameSystems.Count)
        {
            return;
        }

        currentGameSystemIndex = index;
        var selectedSystem = gameSystems[index];
        
        if (platformLabel!= null)
        {
            platformLabel.Text = selectedSystem.Name;
            platformLabel.Visible = false;
        }

        if (platformIcon != null)
        {
            Texture2D texture = null;

            if (!string.IsNullOrEmpty(selectedSystem.IgdbSlug))
            {
                texture = FindPlatformIcon(selectedSystem.IgdbSlug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
            }
            
            if (texture == null && !string.IsNullOrEmpty(selectedSystem.Slug))
            {
                texture = FindPlatformIcon(selectedSystem.Slug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
            }

            platformIcon.Texture = texture;
        }

        UpdateHeaderLabel();
        OnSystemSelected(selectedSystem);
    }
    
    private Texture2D FindPlatformIcon(string stub, string basePath, string[] extensions)
    {
        foreach (var ext in extensions)
        {
            string path = $"{basePath}{stub}{ext}";

            if (ResourceLoader.Exists(path))
            {
                return (Texture2D)ResourceLoader.Load(path);
            }
        }

        return null;
    }
    
    private void OnSystemSelected(GameSystem system)
    {
        if (gameList == null)
        {
            return;
        }

        currentlySelectedGame = null;

        ApplyFiltersAndRefresh();

        if (currentlyShownGames.Any())
        {
            OnGameSelected(0L);

            if (downloadsListContainer != null && !downloadsListContainer.Visible && 
                (settingsMenuContainer == null || !settingsMenuContainer.Visible))
            {
                gameList.GrabFocus();
            }
        
            UpdateHeaderLabel();
        }

        else
        {
            GD.Print($"No games found in cache for system {system.Name}");
        }
    }
    
    private void ApplyFiltersAndRefresh()
    {
        if (currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
        {
            return;
        }

        var system = gameSystems[currentGameSystemIndex];
        
        if (games.TryGetValue(system.Id, out List<Game> cachedGames))
        {
            if (appInstance.configManager.HideGamesWithoutBoxArt)
            {
                currentlyShownGames = cachedGames.Where(g => !string.IsNullOrEmpty(g.PathCover3d) || !string.IsNullOrEmpty(g.PathCoverLarge) || !string.IsNullOrEmpty(g.CoverArtUrl)).ToList();
            }

            else
            {
                currentlyShownGames = cachedGames;
            }

            if (showOnlyInstalledGames)
            {
                currentlyShownGames = currentlyShownGames.Where(g => CheckIfGameIsDownloaded(g)).ToList();
            }

            RefreshGameList();
        }

        else
        {
            currentlyShownGames = new List<Game>();
            RefreshGameList();
        }
    }
    

    public void RefreshGameList()
    {
        if (gameList == null)
        {
            return;
        }

        foreach (Node child in gameList.GetChildren())
        {
            gameList.RemoveChild(child);
            child.QueueFree();
        }

        Texture2D systemControllerIcon = null;

        if (currentGameSystemIndex >= 0 && currentGameSystemIndex < gameSystems.Count)
        {
            var system = gameSystems[currentGameSystemIndex];
            
            string searchSlug = !string.IsNullOrEmpty(system.IgdbSlug) ? system.IgdbSlug : system.Slug;
            
            if (!string.IsNullOrEmpty(searchSlug))
            {
                systemControllerIcon = FindPlatformIcon(searchSlug, "res://assets/platforms/", new[] { ".svg", ".png" });
            }
        }

        for (int i = 0; i < currentlyShownGames.Count; i++)
        {
            var game = currentlyShownGames[i];
            
            if (gameListEntryScene == null)
            {
                continue;
            }

            TextureRect entry = gameListEntryScene.Instantiate<TextureRect>();
            entry.FocusMode = FocusModeEnum.All;

            Panel focusPanel = new Panel();
            focusPanel.Name = "FocusPanel";
            focusPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            focusPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            StyleBoxFlat focusStyle = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                BorderWidthTop = 4,
                BorderWidthBottom = 4,
                BorderWidthLeft = 4,
                BorderWidthRight = 4,
                BorderColor = Colors.White,
                DrawCenter = false
            };
            focusPanel.AddThemeStyleboxOverride("panel", focusStyle);
            focusPanel.Visible = false;
            entry.AddChild(focusPanel);


            entry.Texture = placeholderTexture;
            Label titleLabel = entry.GetNode<Label>("TitleLabel");
            titleLabel.Text = game.Name;
            titleLabel.AddThemeColorOverride("font_color", Colors.Black);

            bool textureLoaded = false;

            void TryLoadImage()
            {
                if (textureLoaded)
                {
                    return;
                }

                string assetsPath = appInstance.configManager.AssetsPath;
                string path3d = System.IO.Path.Combine(assetsPath, "covers_3d", $"{game.Id}.png");
                string path2d = System.IO.Path.Combine(assetsPath, "covers_2d", $"{game.Id}.png");
                
                string pathFallback = "";
                string[] exts = { ".png", ".jpg", ".webp" };

                foreach (var ext in exts)
                {
                    string p = System.IO.Path.Combine(assetsPath, "covers_fallback", $"{game.Id}{ext}");

                    if (Godot.FileAccess.FileExists(p))
                    {
                        pathFallback = p;
                        break;
                    }
                }

                ImageTexture loadedTex = null;

                if (!string.IsNullOrEmpty(path2d))
                {
                    loadedTex = SafeLoadTexture(path2d);
                }

                if (loadedTex == null && !string.IsNullOrEmpty(path3d))
                {
                    loadedTex = SafeLoadTexture(path3d);
                }

                if (loadedTex == null && !string.IsNullOrEmpty(pathFallback))
                {
                    loadedTex = SafeLoadTexture(pathFallback);
                }

                if (loadedTex != null)
                {
                    entry.Texture = loadedTex;
                    titleLabel.Visible = false;
                    textureLoaded = true;
                }

                else
                {
                    appInstance.assetManager.RequestGameAssets(game);
                }

                TextureRect installedIcon = entry.GetNodeOrNull<TextureRect>("InstalledIcon");

                if (installedIcon != null)
                {
                    if (CheckIfGameIsDownloaded(game) && systemControllerIcon != null)
                    {
                        installedIcon.Texture = systemControllerIcon;
                        installedIcon.Visible = true;
                    }

                    else
                    {
                        installedIcon.Visible = false;
                    }
                }

                if (textureLoaded && gameList.HasMethod("UpdateLayout"))
                {
                    bool isAnimating = (bool)gameList.Get("IsAnimating");

                    if (!isAnimating)
                    {
                        gameList.CallDeferred("UpdateLayout", false);
                    }
                }
            }

            void TryUnloadImage()
            {
                if (!textureLoaded)
                {
                    return;
                }

                entry.Texture = placeholderTexture;
                titleLabel.Visible = true;
                textureLoaded = false;
            }

            entry.Visible = false;
            entry.VisibilityChanged += () => 
            {
                if (entry.Visible)
                {
                    TryLoadImage();
                }

                else
                {
                    TryUnloadImage();
                }

            };

            AssetManager.AssetDownloadedEventHandler onAssetDownloaded = null;
            onAssetDownloaded = (downloadedGameId, assetType) =>
            {
                if (downloadedGameId == game.Id && (assetType == "box3d" || assetType == "box2d"))
                {
                    textureLoaded = false;

                    if (entry.Visible)
                    {
                        TryLoadImage();
                    }
                }

            };
            appInstance.assetManager.AssetDownloaded += onAssetDownloaded;

            entry.TreeExiting += () => 
            {
                appInstance.assetManager.AssetDownloaded -= onAssetDownloaded;
            };

            gameList.AddChild(entry);
        }

        if (gameList.HasMethod("Refresh"))
        {
            int targetIndex = 0;

            if (currentlySelectedGame != null)
            {
                targetIndex = currentlyShownGames.FindIndex(g => g.Id == currentlySelectedGame.Id);

                if (targetIndex == -1)
                {
                    targetIndex = 0;
                }
            }

            gameList.Set("SelectedIndex", targetIndex);
            gameList.Call("Refresh");
        }
    }

    private bool CheckIfGameIsDownloaded(Game game)
    {
        if (game.Files == null || !game.Files.Any())
        {
            return false;
        }

        string fileName = game.Files[0].FileName;
        string fullPath = appInstance.configManager.RomsPath.PathJoin(game.System.Slug).PathJoin(fileName);
        return Godot.FileAccess.FileExists(fullPath);
        
    }

    private void OnGameSelected(long index)
    {
        if (index < 0 || index >= currentlyShownGames.Count)
        {
            return;
        }

        currentlySelectedGame = currentlyShownGames[(int)index];
        ShowGameDetails(currentlySelectedGame);

        if (gameList != null)
        {
            var children = gameList.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                var entry = children[i];
                var focusPanel = entry.GetNodeOrNull<Panel>("FocusPanel");
                if (focusPanel != null)
                {
                    focusPanel.Visible = (i == (int)index);
                }
            }
        }
    }

    private void ShowGameDetails(Game game)
    {
        if (detailsPanelContainer == null)
        {
            return;
        }

        if (gameTitle != null) 
        {
            gameTitle.Text = game.Name;
            gameTitle.Visible = true;
        }


        if (gameDescription != null)
        {
            gameDescription.Text = game.Description;
        }

        if (gameMarquee != null)
        {
            gameMarquee.Visible = false;
            string assetsPath = appInstance.configManager.AssetsPath;
            string pathMarquee = System.IO.Path.Combine(assetsPath, "marquees", $"{game.Id}.png");
            
            ImageTexture marqueeTex = SafeLoadTexture(pathMarquee);

            if (marqueeTex != null)
            {
                gameMarquee.Texture = marqueeTex;
                gameMarquee.Visible = true;

                if (gameTitle != null)
                {
                    gameTitle.Visible = false;
                }
            }

            else
            {
                if (gameTitle != null)
                {
                    gameTitle.Visible = true;
                }
            }
        }

        if (gameCover != null)
        {
            string assetsPath = appInstance.configManager.AssetsPath;
            string path3d = System.IO.Path.Combine(assetsPath, "covers_3d", $"{game.Id}.png");
            string path2d = System.IO.Path.Combine(assetsPath, "covers_2d", $"{game.Id}.png");
            
            string pathFallback = "";
            string[] exts = { ".png", ".jpg", ".webp" };

            foreach (var ext in exts)
            {
                string p = System.IO.Path.Combine(assetsPath, "covers_fallback", $"{game.Id}{ext}");

                if (Godot.FileAccess.FileExists(p))
                {
                    pathFallback = p;
                    break;
                }
            }

            ImageTexture loadedTex = null;

            if (!string.IsNullOrEmpty(path2d))
            {
                loadedTex = SafeLoadTexture(path2d);
            }

            if (loadedTex == null && !string.IsNullOrEmpty(path3d))
            {
                loadedTex = SafeLoadTexture(path3d);
            }

            if (loadedTex == null && !string.IsNullOrEmpty(pathFallback))
            {
                loadedTex = SafeLoadTexture(pathFallback);
            }

            gameCover.Texture = loadedTex;
        }

        
        if (backgroundRect != null)
        {
            string assetsPath = appInstance.configManager.AssetsPath;
            string pathScreenshot = System.IO.Path.Combine(assetsPath, "screenshots", $"{game.Id}.jpg");
            
            if (Godot.FileAccess.FileExists(pathScreenshot))
            {
                backgroundRect.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(pathScreenshot));

                backgroundRect.Modulate = new Color(0.6f, 0.6f, 0.6f, 1.0f);
            }

            else
            {
                var blackImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
                blackImage.Fill(Colors.Black);
                backgroundRect.Texture = ImageTexture.CreateFromImage(blackImage);
                backgroundRect.Modulate = Colors.White;
            }
        }
        
        UpdateDetailsPanelButtons(game);
    }
    
    public void UpdateDetailsPanelButtons(Game game)
    {
        bool isGameDownloadedLocally = CheckIfGameIsDownloaded(game);

        if (installedIcon != null)
        {
            installedIcon.Visible = isGameDownloadedLocally;
        }

        if (actionBtn == null)
        {
            return;
        }

        bool isDownloading = false;

        if (game.Files != null && game.Files.Any())
        {
            isDownloading = appInstance.downloadManager.IsDownloading(game.Files[0].FileName);
        }

        if (isGameDownloadedLocally)
        {
            if (appInstance.emulatorManager.IsEmulatorInstalled(appInstance.emulatorManager.GetMappedEmulator(game.PlatformSlug)))
            {
                actionBtn.Text = "Play";
                actionBtn.Disabled = false; 
            }

            else
            {
                actionBtn.Text = "Install Emulator";
                actionBtn.Disabled = false;
            }
        }

        else
        {
            if (isDownloading)
            {
                actionBtn.Text = "Downloading...";
                actionBtn.Disabled = true;
            }

            else
            {
                actionBtn.Text = "Download";
                actionBtn.Disabled = false;
            }
        }

        if (deleteBtn != null)
        {
            deleteBtn.Disabled = !isGameDownloadedLocally;
        }
    }
    

    private void OnEmulatorInstallationCompleted(string emulatorName, bool wasSuccessful)
    {
        if (currentlySelectedGame != null)
        {
            UpdateDetailsPanelButtons(currentlySelectedGame);
        }
    }

    private void OnPlayDownloadButtonPressed()
    {
        if (currentlySelectedGame == null)
        {
            return;
        }

        string emulatorName = appInstance.emulatorManager.GetMappedEmulator(currentlySelectedGame.PlatformSlug);

        if (actionBtn.Text == "Install Emulator")
        {
            actionBtn.Disabled = true; 
            _ = appInstance.emulatorManager.InstallEmulator(emulatorName); 
            return; 
        }

        bool isGameDownloadedLocally = CheckIfGameIsDownloaded(currentlySelectedGame);

        if (isGameDownloadedLocally)
        {
            appInstance.emulatorManager.LaunchEmulatorWithGame(currentlySelectedGame);
        }

        else
        {
            DownloadGame(currentlySelectedGame);
        }
    }

    private void OnJumpSectionRequested(int direction)
    {
        if (currentlyShownGames == null || currentlyShownGames.Count == 0 || gameList == null)
        {
            return;
        }

        int currentIndex = (int)gameList.Get("SelectedIndex");

        if (currentIndex < 0 || currentIndex >= currentlyShownGames.Count)
        {
            return;
        }

        char GetSectionChar(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return '#';
            }

            string sortName = name.TrimStart();
            string[] articles = { "The ", "A ", "An " };

            foreach (string article in articles)
            {
                if (sortName.StartsWith(article, System.StringComparison.OrdinalIgnoreCase))
                {
                    sortName = sortName.Substring(article.Length).TrimStart();
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(sortName))
            {
                return '#';
            }

            char first = char.ToUpper(sortName[0]);
            return char.IsLetter(first) ? first : '#';
        }
        
        char currentLetter = GetSectionChar(currentlyShownGames[currentIndex].Name);
        int targetIndex = currentIndex;
        
        if (direction > 0) // next section
        {
            for (int i = currentIndex + 1; i < currentlyShownGames.Count; i++)
            {
                char letter = GetSectionChar(currentlyShownGames[i].Name);

                if (letter != currentLetter)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        else // previous section
        {
            char targetLetter = '\0';

            for (int i = currentIndex - 1; i >= 0; i--)
            {
                char letter = GetSectionChar(currentlyShownGames[i].Name);

                if (letter != currentLetter)
                {
                    targetLetter = letter;
                    break;
                }
            }
            
            if (targetLetter != '\0')
            {
                for (int i = 0; i <= currentIndex; i++)
                {
                    if (GetSectionChar(currentlyShownGames[i].Name) == targetLetter)
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            else 
            {
                targetIndex = 0;
            }
        }

        if (targetIndex != currentIndex)
        {
            gameList.Set("SelectedIndex", targetIndex);
            gameList.Call("UpdateLayout", true);
            OnGameSelected(targetIndex);
        }
    }
    
    private void DownloadGame(Game game)
    {
        if (game == null || game.Files == null || !game.Files.Any())
        {
            GD.PrintErr($"No files found for game: {game?.Name}");
            return;
        }
        
        string downloadUrl = appInstance.rommApi.GetRomDownloadUrl(game);
        
        if (string.IsNullOrEmpty(downloadUrl))
        {
            GD.PrintErr($"Could not get download URL for game: {game.Name}");
            return;
        }

        string tempZipName = game.Files[0].FileName + ".zip";
        string tempZipPath = appInstance.configManager.DownloadsPath.PathJoin(tempZipName);
        
        string baseDir = tempZipPath.GetBaseDir();

        if (!DirAccess.DirExistsAbsolute(baseDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(baseDir);
        }
        
        GD.Print($"Starting download for {game.Name} from {downloadUrl} to temporary file {tempZipPath}");
        
        appInstance.downloadManager.DownloadFile(
            downloadUrl, 
            tempZipPath, 
            appInstance.rommApi.GetAuthHeaders(),
            (path) => HandleRomDownloadCompletion(path, game));
            
        UpdateDetailsPanelButtons(game);
    }
    
    private void HandleRomDownloadCompletion(string tempZipPath, Game game)
    {
        string fileName = tempZipPath.GetFile();

        if (downloadProgressUI != null)
        {
            downloadProgressUI.SetDownloadStatus(fileName, "Extracting...");
        }

        GD.Print($"Download complete. Starting extraction for: {tempZipPath}");

        string finalDir = appInstance.configManager.RomsPath.PathJoin(game.System.Slug);
        
        if (!DirAccess.DirExistsAbsolute(finalDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(finalDir);
        }

        string toolsDir = appInstance.configManager.ToolsPath;
        string sevenZipPath = OS.HasFeature("windows") 
            ? System.IO.Path.Combine(toolsDir, "7zip", "windows", "7za.exe")
            : System.IO.Path.Combine(toolsDir, "7zip", "linux", "7zz");

        try
        {
            if (OS.HasFeature("linux") || OS.GetName() == "Linux" || OS.GetName() == "X11" || OS.GetName() == "Wayland")
            {
                OS.Execute("chmod", new string[] { "+x", sevenZipPath }, new Godot.Collections.Array());
            }
            
            string globalTempZip = ProjectSettings.GlobalizePath(tempZipPath);
            string globalFinalDir = ProjectSettings.GlobalizePath(finalDir);
            
            string[] arguments = { "e", globalTempZip, $"-o{globalFinalDir}", "roms/*", "-r", "-y" };
            
            int exitCode = OS.Execute(sevenZipPath, arguments, new Godot.Collections.Array());
            
            if (exitCode == 0)
            {
                GD.Print($"Successfully extracted {tempZipPath} to {finalDir}");
                
                if (OS.HasFeature("linux") || OS.GetName() == "Linux" || OS.GetName() == "X11" || OS.GetName() == "Wayland")
                {
                    OS.Execute("chmod", new string[] { "-R", "a+rwx", globalFinalDir }, new Godot.Collections.Array());
                }
            }
            else
            {
                GD.PrintErr($"Failed to extract zip file. 7zip exit code: {exitCode}");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Error extracting zip file: {e.Message}");
        }
        finally
        {
            if (Godot.FileAccess.FileExists(tempZipPath))
            {
                DirAccess.RemoveAbsolute(tempZipPath);
                GD.Print($"Deleted temporary file: {tempZipPath}");
            }
        }

        UpdateDetailsPanelButtons(game);
        RefreshGameList();
    }
    
    private void OnDownloadCompleted(string fileName, bool success)
    {
        RefreshGameList();
    }
    
    private void OnDeleteButtonPressed()
    {
        if (currentlySelectedGame == null)
        {
            return;
        }

        DeleteLocalGame(currentlySelectedGame);
        ApplyFiltersAndRefresh();
        UpdateDetailsPanelButtons(currentlySelectedGame);
    }

    private void DeleteLocalGame(Game game)
    {
        List<RomFile> romFiles = game.Files;
        
        foreach (RomFile file in romFiles)
        { 
            System.IO.File.Delete(file.FullPath);
        }
    }

    private ImageTexture SafeLoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] fileData = System.IO.File.ReadAllBytes(path);

            if (fileData.Length < 12)
            {
                return null;
            }

            var img = new Image();
            Error err = Error.Failed;
            
            if (fileData[0] == 0x89 && fileData[1] == 0x50 && fileData[2] == 0x4E && fileData[3] == 0x47)
            {
                err = img.LoadPngFromBuffer(fileData);
            }

            else if (fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF)
            {
                err = img.LoadJpgFromBuffer(fileData);
            }

            else if (fileData[0] == 0x52 && fileData[1] == 0x49 && fileData[2] == 0x46 && fileData[3] == 0x46 &&
                     fileData[8] == 0x57 && fileData[9] == 0x45 && fileData[10] == 0x42 && fileData[11] == 0x50)
            {
                err = img.LoadWebpFromBuffer(fileData);
            }
            
            if (err == Error.Ok && img != null && !img.IsEmpty())
            {
                return ImageTexture.CreateFromImage(img);
            }
        }

        catch (Exception)
        {
        }

        return null;
    }

    private void SetupSettingsTree()
    {
        if (settingsSectionsTree == null)
        {
            return;
        }

        settingsSectionsTree.Clear();
        TreeItem root = settingsSectionsTree.CreateItem();
        settingsSectionsTree.HideRoot = true;

        TreeItem general = settingsSectionsTree.CreateItem(root);
        general.SetText(0, "General Settings");
        
        TreeItem gameListSettings = settingsSectionsTree.CreateItem(root);
        gameListSettings.SetText(0, "Game List Settings");
        GenerateGameListSettingsForm();
        
        TreeItem inputSettingsItem = settingsSectionsTree.CreateItem(root);
        inputSettingsItem.SetText(0, "Input Settings");
        GenerateInputSettingsForm();
        
        TreeItem connection = settingsSectionsTree.CreateItem(root);
        connection.SetText(0, "Connection Settings");

        TreeItem emulatorsItem = settingsSectionsTree.CreateItem(root);
        emulatorsItem.SetText(0, "Emulator Settings");

        var allEmulators = appInstance.emulatorManager.GetAllAvailableEmulators();

        foreach (var kvp in allEmulators)
        {
            string slug = kvp.Key;
            EmulatorMeta meta = kvp.Value;
            
            TreeItem emuNode = settingsSectionsTree.CreateItem(emulatorsItem);
            emuNode.SetText(0, meta.Name);
            emuNode.SetMetadata(0, slug);

            GenerateEmulatorSettingsForm(slug, meta);
        }

        settingsSectionsTree.ItemSelected += OnSettingsTreeItemSelected;
    }

    private Control GetVisibleSettingsForm()
    {
        if (sectionOptionsContainer == null)
        {
            return null;
        }

        foreach (Node child in sectionOptionsContainer.GetChildren())
        {
            if (child is Control c && c.Visible)
            {
                return c;
            }
        }

        return null;
    }

    private Control FindFirstFocusable(Node node)
    {
        if (node is Control c && c.FocusMode != Control.FocusModeEnum.None && c.Visible)
        {
            return c;
        }

        foreach (Node child in node.GetChildren())
        {
            Control found = FindFirstFocusable(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void CycleFocusedOption(int direction)
    {
        var focusOwner = GetViewport().GuiGetFocusOwner();

        if (focusOwner == null)
        {
            return;
        }

        if (focusOwner is OptionButton optBtn)
        {
            if (optBtn.ItemCount == 0)
            {
                return;
            }

            int newIdx = optBtn.Selected + direction;

            if (newIdx < 0)
            {
                newIdx = optBtn.ItemCount - 1;
            }

            if (newIdx >= optBtn.ItemCount)
            {
                newIdx = 0;
            }

            optBtn.Select(newIdx);
            optBtn.EmitSignal(OptionButton.SignalName.ItemSelected, newIdx);
        }

        else if (focusOwner is BaseButton btn && btn.ToggleMode)
        {
            bool toggled = !btn.ButtonPressed;

            if (direction == 1 && !btn.ButtonPressed)
            {
                toggled = true;
            }

            else if (direction == -1 && btn.ButtonPressed)
            {
                toggled = false;
            }

            else
            {
                return;
            }

            btn.ButtonPressed = toggled;
            btn.EmitSignal(BaseButton.SignalName.Toggled, btn.ButtonPressed);
        }

        else if (focusOwner is SpinBox spinBox)
        {
            double step = spinBox.Step > 0 ? spinBox.Step : 1;
            double newValue = spinBox.Value + (direction * step);

            if (newValue < spinBox.MinValue)
            {
                newValue = spinBox.MaxValue;
            }

            if (newValue > spinBox.MaxValue)
            {
                newValue = spinBox.MinValue;
            }

            spinBox.Value = newValue;
        }
    }

    private void CycleFocusInContainer(Control container, int direction)
    {
        if (container == null)
        {
            return;
        }

        List<Control> focusableChildren = new List<Control>();
        GatherFocusableControls(container, focusableChildren);
        
        if (focusableChildren.Count == 0)
        {
            return;
        }

        var focusOwner = GetViewport().GuiGetFocusOwner();
        int currentIndex = focusOwner != null ? focusableChildren.IndexOf(focusOwner) : -1;
        
        if (currentIndex == -1)
        {
            focusableChildren[0].GrabFocus();
            return;
        }
        
        int nextIndex = currentIndex + direction;

        if (nextIndex < 0)
        {
            nextIndex = focusableChildren.Count - 1;
        }

        else if (nextIndex >= focusableChildren.Count)
        {
            nextIndex = 0;
        }

        focusableChildren[nextIndex].GrabFocus();
    }
    
    private void GatherFocusableControls(Node parent, List<Control> list)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Control c)
            {
                if (!c.Visible)
                {
                    continue;
                }

                if (c.FocusMode != Control.FocusModeEnum.None)
                {
                    list.Add(c);
                }
            }

            GatherFocusableControls(child, list);
        }
    }

    private void GenerateGameListSettingsForm()
    {
        if (sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "GameListSettings";

        if (sectionOptionsContainer.HasNode(nodeName))
        {
            return;
        }

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        
        VBoxContainer vbox = new VBoxContainer();
        formContainer.AddChild(vbox);
        sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer fieldBox = new HBoxContainer();
        
        Label label = new Label();
        label.Text = "Hide games without box art";
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox.AddChild(label);

        CheckButton checkbox = new CheckButton();
        checkbox.ButtonPressed = appInstance.configManager.HideGamesWithoutBoxArt;
        
        CheckButton showAllCheckbox = new CheckButton();
        showAllCheckbox.ButtonPressed = appInstance.configManager.ShowAllSystems;

        checkbox.Toggled += (bool toggledOn) => 
        {
            appInstance.configManager.SaveGameListSettings(toggledOn, showAllCheckbox.ButtonPressed);

            if (gameSystems != null && currentGameSystemIndex >= 0 && currentGameSystemIndex < gameSystems.Count)
            {
                OnSystemSelected(gameSystems[currentGameSystemIndex]);
            }

        };
        fieldBox.AddChild(checkbox);
        vbox.AddChild(fieldBox);

        HBoxContainer fieldBox2 = new HBoxContainer();
        Label label2 = new Label();
        label2.Text = "Show all systems";
        label2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox2.AddChild(label2);
        
        showAllCheckbox.Toggled += (bool toggledOn) => 
        {
            appInstance.configManager.SaveGameListSettings(checkbox.ButtonPressed, toggledOn);
            GetCache();
            SelectSystemByIndex(0);
        };
        fieldBox2.AddChild(showAllCheckbox);
        vbox.AddChild(fieldBox2);
    }

    private void GenerateInputSettingsForm()
    {
        if (sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "InputSettings";

        if (sectionOptionsContainer.HasNode(nodeName))
        {
            return;
        }

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        
        VBoxContainer vbox = new VBoxContainer();
        formContainer.AddChild(vbox);
        sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer countBox = new HBoxContainer();
        Label countLabel = new Label();
        countLabel.Text = "Number of Emulator Close Hotkeys";
        countLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        countBox.AddChild(countLabel);

        SpinBox countSpin = new SpinBox();
        countSpin.MinValue = 1;
        countSpin.MaxValue = 10;
        countSpin.Value = appInstance.configManager.EmulatorCloseHotkeyCount;
        countBox.AddChild(countSpin);
        vbox.AddChild(countBox);

        VBoxContainer keysBox = new VBoxContainer();
        vbox.AddChild(keysBox);

        void RebuildKeysBox(int count)
        {
            foreach (Node child in keysBox.GetChildren())
            {
                keysBox.RemoveChild(child);
                child.QueueFree();
            }
            
            var currentKeys = appInstance.configManager.EmulatorCloseHotkeys;
            
            for (int i = 0; i < count; i++)
            {
                HBoxContainer keyBox = new HBoxContainer();
                Label keyLabel = new Label();
                keyLabel.Text = $"Close Key {i + 1}";
                keyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                
                OptionButton opt = new OptionButton();

                foreach (JoyButton btn in Enum.GetValues(typeof(JoyButton)))
                {
                    opt.AddItem(btn.ToString(), (int)btn);
                }

                if (i < currentKeys.Count)
                {
                    int btnVal = currentKeys[i].AsInt32();

                    for(int idx = 0; idx < opt.ItemCount; idx++)
                    {
                        if(opt.GetItemId(idx) == btnVal)
                        {
                            opt.Select(idx);
                            break;
                        }
                    }
                }
                
                int localI = i;
                opt.ItemSelected += (long index) => 
                {
                    int selectedBtn = opt.GetItemId((int)index);
                    var newKeys = new Godot.Collections.Array(appInstance.configManager.EmulatorCloseHotkeys);

                    while(newKeys.Count <= localI)
                    {
                        newKeys.Add(0);
                    }

                    newKeys[localI] = selectedBtn;
                    appInstance.configManager.SaveInputSettings(appInstance.configManager.EmulatorCloseHotkeyCount, newKeys);
                };

                keyBox.AddChild(keyLabel);
                keyBox.AddChild(opt);
                keysBox.AddChild(keyBox);
            }
        }

        RebuildKeysBox(appInstance.configManager.EmulatorCloseHotkeyCount);

        countSpin.ValueChanged += (double val) =>
        {
            int newCount = (int)val;
            appInstance.configManager.SaveInputSettings(newCount, appInstance.configManager.EmulatorCloseHotkeys);
            RebuildKeysBox(newCount);
        };
    }

    private void GenerateEmulatorSettingsForm(string slug, EmulatorMeta meta)
    {
        if (sectionOptionsContainer == null || meta.SettingsFields == null)
        {
            return;
        }

        string nodeName = meta.Name.Replace(" ", "");

        if (sectionOptionsContainer.HasNode(nodeName))
        {
            return;
        }

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        
        VBoxContainer vbox = new VBoxContainer();
        formContainer.AddChild(vbox);
        sectionOptionsContainer.AddChild(formContainer);

        var userSettings = appInstance.emulatorManager.LoadEmulatorSettings(slug);

        foreach (var field in meta.SettingsFields)
        {
            if (string.IsNullOrEmpty(field.Id))
            {
                continue;
            }

            bool hasValue = userSettings.TryGetValue(field.Id, out System.Text.Json.JsonElement element);

            HBoxContainer fieldBox = new HBoxContainer();
            
            Label label = new Label();
            label.Text = field.Label;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            fieldBox.AddChild(label);

            if (field.Type == "boolean")
            {
                CheckButton checkbox = new CheckButton();
                bool val = field.DefaultValueBool;

                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.True)
                {
                    val = true;
                }

                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.False)
                {
                    val = false;
                }

                checkbox.ButtonPressed = val;
                
                checkbox.Toggled += (bool toggledOn) => 
                {
                    appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, toggledOn);
                };
                fieldBox.AddChild(checkbox);
            }

            else if (field.Type == "string")
            {
                LineEdit lineEdit = new LineEdit();
                lineEdit.CustomMinimumSize = new Vector2(200, 0);
                string val = field.DefaultValueString;

                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    val = element.GetString();
                }

                lineEdit.Text = val;

                lineEdit.TextChanged += (string newText) => 
                {
                    appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, newText);
                };
                fieldBox.AddChild(lineEdit);
            }

            else if (field.Type == "dropdown")
            {
                OptionButton optionButton = new OptionButton();
                string val = field.DefaultValueString;

                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    val = element.GetString();
                }

                int idx = 0;
                int selectedIdx = 0;

                if (field.Options != null)
                {
                    foreach (var option in field.Options)
                    {
                        optionButton.AddItem(option.Key, idx);
                        optionButton.SetItemMetadata(idx, option.Value);

                        if (option.Value == val)
                        {
                            selectedIdx = idx;
                        }

                        idx++;
                    }
                }

                optionButton.Select(selectedIdx);

                optionButton.ItemSelected += (long index) => 
                {
                    string selectedValue = optionButton.GetItemMetadata((int)index).AsString();
                    appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, selectedValue);
                };
                fieldBox.AddChild(optionButton);
            }

            vbox.AddChild(fieldBox);
        }
    }

    private void OnSettingsTreeItemSelected()
    {
        if (settingsSectionsTree == null || sectionOptionsContainer == null)
        {
            return;
        }

        TreeItem selected = settingsSectionsTree.GetSelected();

        if (selected == null)
        {
            return;
        }

        string sectionName = selected.GetText(0);

        foreach (Node child in sectionOptionsContainer.GetChildren())
        {
            if (child is Control control)
            {
                control.Visible = false;
            }
        }

        string nodeName = sectionName.Replace(" ", "");
        var activePanel = sectionOptionsContainer.GetNodeOrNull<Control>(nodeName);

        if (activePanel != null)
        {
            activePanel.Visible = true;
        }
    }

    private void UpdateHeaderLabel()
    {
        if (platformLabel == null)
        {
            return;
        }

        if (settingsMenuContainer != null && settingsMenuContainer.Visible)
        {
            platformLabel.Text = "Settings";
            platformLabel.Visible = true;

            if (platformIcon != null)
            {
                platformIcon.Visible = false;
            }
        }

        else if (downloadsListContainer != null && downloadsListContainer.Visible)
        {
            platformLabel.Text = "Downloads";
            platformLabel.Visible = true;

            if (platformIcon != null)
            {
                platformIcon.Visible = false;
            }
        }

        else if (gameSystems != null && currentGameSystemIndex >= 0 && currentGameSystemIndex < gameSystems.Count)
        {
            platformLabel.Text = gameSystems[currentGameSystemIndex].Name;
            platformLabel.Visible = false;

            if (platformIcon != null)
            {
                platformIcon.Visible = true;
            }
        }
    }
}


