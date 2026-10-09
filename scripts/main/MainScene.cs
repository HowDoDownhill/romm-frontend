using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainScene : Control
{
    [ExportGroup("Header")]
    [Export] public MarginContainer headerContainer;
    [Export] public SystemCarousel systemCarousel;

    [ExportGroup("GameList")]
    [Export] public Control gameList;
    [Export] public PackedScene gameListEntryScene;

    public readonly List<GameListView> gameListViews = new List<GameListView>();
    public GameListView ActiveGameList => gameList as GameListView;

    private const float ControllerLayerOfferDelaySeconds = 1.5f;

    public Button assignControllersButton => startMenuPanel?.assignControllersButton;

    [ExportGroup("DetailsPanel")]
    [Export] public Control detailsPanel;
    [Export] public VBoxContainer detailsPanelContainer;
    [Export] public Control lobbyPanel;
    [Export] public Label lobbyCodeLabel;
    [Export] public Label lobbyStatusLabel;
    [Export] public VBoxContainer lobbyPlayerList;
    [Export] public Button lobbyActionButton;
    [Export] public Button lobbyLeaveButton;
    [Export] public Button lobbyCopyCodeButton;
    [Export] public TextureRect gameCover;
    [Export] public TextureRect gameMarquee;
    [Export] public Label gameTitle;
    [Export] public RichTextLabel gameDescription;
    [Export] public TextureRect installedIcon;
    [Export] public ScrollContainer gameScreenshotsScroll;
    [Export] public GridContainer gameScreenshotsFlow;
    [Export] public ProgressBar gameDownloadProgressBar;

    [ExportGroup("Sections")]
    [Export] public UiPanel gameListSection;
    [Export] public UiPanel downloadsListContainer;
    [Export] public DownloadProgressUI downloadProgressUI;

    [ExportGroup("Footer Buttons & Containers")]
    [Export] public Control gameListFooter;
    [Export] public Control downloadsFooter;
    [Export] public Control settingsFooter;

    [Export] public Button actionBtn;
    [Export] public Button deleteBtn;
    [Export] public Button optionsBtn;
    [Export] public Button filterInstalledGamesBtn;
    [Export] public Button toggleDownloadsBtn;
    [Export] public Button downloadsToggleDownloadsBtn;
    [Export] public Button navHintBtn;
    [Export] public Button cancelDownloadBtn;
    [Export] public Button settingsSelectBtn;
    [Export] public Button settingsBackBtn;

    [ExportGroup("Update & Refresh UI")]
    [Export] public ProgressPanel progressPanel;
    [Export] public ChangelogPanel changelogPanel;

    [ExportGroup("Panel Frost")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float panelLuminosityFloor = 0.14f;

    [ExportGroup("Panel Shadow")]
    [Export(PropertyHint.Range, "0,120,1")] public int panelShadowSize = MicaShadow.DefaultSize;
    [Export] public Color panelShadowColor = MicaShadow.DefaultColor;
    [Export] public Vector2 panelShadowOffset = MicaShadow.DefaultOffset;

    public AppInstance appInstance;
    public ImageTexture placeholderTexture;
    [Export] public ColorRect backgroundRect;
    [Export] public VBoxContainer mainVBoxContainer;

    [Export] public StartMenuPanel startMenuPanel;

    [Export] public SettingsPanel settingsMenuContainer;
    [Export] public VBoxContainer settingsSectionsTree;
    [Export] public VBoxContainer sectionOptionsContainer;

    public Button emulatorCloseHotkeysBtn;

    public MainSceneSettingsHandler SettingsHandler { get; private set; }
    public MainSceneSectionHandler SectionHandler { get; private set; }
    public MainSceneInputHandler InputHandler { get; private set; }
    public MainSceneGameListHandler GameListHandler { get; private set; }
    public MainSceneDownloadHandler DownloadHandler { get; private set; }
    public MainSceneNetplayHandler NetplayHandler { get; private set; }
    public MainSceneUpdaterHandler UpdaterHandler { get; private set; }
    public MainScenePopupHandler PopupHandler { get; private set; }

    public UiPanel fuzzySearchPopup;
    public Label fuzzySearchLabel;

    public SystemJumpPopup systemJumpPopup;
    public ReleasePickerPopup releasePickerPopup;
    public UiPanelStack panelStack = new UiPanelStack();
    private ulong leftBumperPressedTime = 0;
    private ulong rightBumperPressedTime = 0;

    public override void _Ready()
    {
        var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        whiteImage.Fill(Colors.White);
        placeholderTexture = ImageTexture.CreateFromImage(whiteImage);
        appInstance = GetNode<AppInstance>("/root/AppInstance");

        SettingsHandler = new MainSceneSettingsHandler(this, appInstance);
        SectionHandler = new MainSceneSectionHandler(this);
        PopupHandler = new MainScenePopupHandler(this, appInstance);

        fuzzySearchPopup = new UiPanel();
        AddChild(fuzzySearchPopup);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        fuzzySearchLabel = new Label();
        margin.AddChild(fuzzySearchLabel);
        fuzzySearchPopup.ContentRoot.AddChild(margin);

        CreateGameListViews();
        GameListHandler = new MainSceneGameListHandler(this, appInstance);
        SetGameListView(System.Array.IndexOf(ConfigManager.GameListViews, appInstance.configManager.GameListView));
        DownloadHandler = new MainSceneDownloadHandler(this, appInstance);
        NetplayHandler = new MainSceneNetplayHandler(this, appInstance);
        NetplayHandler.Initialise();
        UpdaterHandler = new MainSceneUpdaterHandler(this, appInstance);
        InputHandler = new MainSceneInputHandler(this, appInstance);

        if (systemCarousel != null)
        {
            systemCarousel.SystemSelected += (index) =>
            {
                if (GameListHandler.gameSystems != null && index >= 0 && index < GameListHandler.gameSystems.Count)
                {
                    GameListHandler.SelectSystemByIndex(index);
                }
            };

            systemCarousel.JumpRequested += OpenSystemJumpPopup;

            systemCarousel.Cycled += GameListHandler.BeginQuickSwitchFade;
        }

        releasePickerPopup = new ReleasePickerPopup();
        AddChild(releasePickerPopup);
        releasePickerPopup.ReleaseChosen += OnEmulatorReleaseChosen;
        releasePickerPopup.Closed += OnReleasePickerClosed;

        panelStack.Register(releasePickerPopup);

        systemJumpPopup = new SystemJumpPopup();
        AddChild(systemJumpPopup);
        panelStack.Register(systemJumpPopup);
        systemJumpPopup.SystemSelected += (index) =>
        {
            systemJumpPopup.Close();
            if (systemCarousel != null)
            {
                systemCarousel.SetSelectionSilently(index, true);
            }
            GameListHandler.SelectSystemByIndex(index);
        };

        appInstance.downloadManager.DownloadCompleted += DownloadHandler.OnDownloadCompleted;
        appInstance.emulatorManager.EmulatorInstallationCompleted += OnEmulatorInstallationCompleted;
        appInstance.emulatorManager.EmulatorLaunchStateChanged += OnEmulatorLaunchStateChanged;

        if (startMenuPanel != null)
        {
            panelStack.Register(startMenuPanel);
            startMenuPanel.BiosViewRequested += PopupHandler.PopulateBiosSelector;
            startMenuPanel.NetplayCancelRequested += PopupHandler.OnNetplayCancelPressed;
            startMenuPanel.Closed += ReturnFocusToGameList;

            if (startMenuPanel.launchEmulatorButton != null) startMenuPanel.launchEmulatorButton.Pressed += PopupHandler.OnLaunchEmulatorPressed;
            if (startMenuPanel.updateEmulatorButton != null) startMenuPanel.updateEmulatorButton.Pressed += PopupHandler.OnUpdateEmulatorPressed;
            if (startMenuPanel.uninstallEmulatorButton != null) startMenuPanel.uninstallEmulatorButton.Pressed += PopupHandler.OnUninstallEmulatorPressed;
            if (startMenuPanel.favoriteGameButton != null) startMenuPanel.favoriteGameButton.Pressed += PopupHandler.OnFavoriteGamePressed;
            if (startMenuPanel.hostNetplayButton != null) startMenuPanel.hostNetplayButton.Pressed += PopupHandler.OnHostNetplayPressed;
            if (startMenuPanel.joinNetplayButton != null) startMenuPanel.joinNetplayButton.Pressed += PopupHandler.OnJoinNetplayPressed;
            if (startMenuPanel.selectBiosButton != null) startMenuPanel.selectBiosButton.Pressed += PopupHandler.OnSelectBiosMenuPressed;
            if (startMenuPanel.assignControllersButton != null) startMenuPanel.assignControllersButton.Pressed += InputHandler.BeginControllerAssignment;
            if (startMenuPanel.settingsButton != null) startMenuPanel.settingsButton.Pressed += PopupHandler.OnSettingsMenuPressed;
            if (startMenuPanel.refreshAllGamesButton != null) startMenuPanel.refreshAllGamesButton.Pressed += PopupHandler.OnRefreshGamesPressed;
            if (startMenuPanel.refreshCurrentSystemButton != null) startMenuPanel.refreshCurrentSystemButton.Pressed += PopupHandler.OnRefreshCurrentSystemGamesPressed;
            if (startMenuPanel.quitButton != null) startMenuPanel.quitButton.Pressed += PopupHandler.OnQuitPressed;
            if (startMenuPanel.randomGameButton != null) startMenuPanel.randomGameButton.Pressed += PopupHandler.OnRandomGamePressed;
        }

        GetCache();

        if (settingsMenuContainer != null)
        {
            settingsMenuContainer.Handler = SettingsHandler;
            SettingsHandler.SetupSettingsTree();
        }

        SectionHandler.Initialise();

        if (changelogPanel != null)
        {
            panelStack.Register(changelogPanel);
            changelogPanel.Accepted += OnChangelogPanelAccepted;
            changelogPanel.Dismissed += OnChangelogPanelDismissed;
        }

        GameListHandler.SelectSystemByIndex(0);

        foreach (GameListView view in gameListViews)
        {
            view.ItemSelected += GameListHandler.OnGameSelected;
            view.ItemFocused += GameListHandler.OnGameSelected;
            view.ItemActivated += index => OnPlayDownloadButtonPressed();
            view.JumpSectionRequested += GameListHandler.OnJumpSectionRequested;
        }

        DownloadHandler.SetupDownloadsList();
        SetupFooterUI();
        UpdaterHandler.InitUpdater();
        ApplyTheme();

        var micaMaterial = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
        MicaShadow.AttachToAll(this, micaMaterial, panelShadowColor, panelShadowSize, panelShadowOffset);

        NetplayHandler.ApplyStartupSessionArguments();
        OfferControllerLayerOnceTheInterfaceHasSettled();
        CaptureLayoutIfRequested();
        RunBenchmarkIfRequested();
    }

    private const string BenchmarkArgumentPrefix = "--ui-bench=";
    private const double BenchmarkSettleSeconds = 4.0;

    private async void RunBenchmarkIfRequested()
    {
        string[] userArguments = OS.GetCmdlineUserArgs();
        string benchmarkArgument = userArguments.FirstOrDefault(argument => argument.StartsWith(BenchmarkArgumentPrefix));

        if (benchmarkArgument == null)
        {
            return;
        }

        ApplyCaptureWindowSize(userArguments);

        while (ActiveGameList == null || ActiveGameList.ItemCount == 0)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        ulong libraryReadyMilliseconds = Time.GetTicksMsec();
        await ToSignal(GetTree().CreateTimer(BenchmarkSettleSeconds), SceneTreeTimer.SignalName.Timeout);

        var benchmark = new UiBenchmark();
        AddChild(benchmark);
        benchmark.Begin(this, benchmarkArgument.Substring(BenchmarkArgumentPrefix.Length), libraryReadyMilliseconds);
    }

    private static void ApplyCaptureWindowSize(string[] userArguments)
    {
        string sizeArgument = userArguments.FirstOrDefault(argument => argument.StartsWith(LayoutCaptureSizeArgumentPrefix));
        string[] sizeParts = sizeArgument?.Substring(LayoutCaptureSizeArgumentPrefix.Length).Split('x');

        if (sizeParts != null && sizeParts.Length == 2 && int.TryParse(sizeParts[0], out int width) && int.TryParse(sizeParts[1], out int height))
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(width, height));
            DisplayServer.WindowSetPosition(Vector2I.Zero);
        }
    }

    private const string LayoutCaptureArgumentPrefix = "--ui-capture=";
    private const string LayoutCaptureSizeArgumentPrefix = "--ui-capture-size=";
    private const string LayoutCaptureViewArgumentPrefix = "--ui-capture-view=";
    private const string LayoutCaptureSystemArgumentPrefix = "--ui-capture-system=";
    private const string LayoutCaptureThemeArgumentPrefix = "--ui-capture-theme=";
    private const string LayoutCaptureBackgroundArgumentPrefix = "--ui-capture-background=";

    private string ActiveThemeName => CaptureArgument(LayoutCaptureThemeArgumentPrefix) ?? appInstance.configManager.AppTheme;

    private string ActiveBackgroundName => CaptureArgument(LayoutCaptureBackgroundArgumentPrefix) ?? appInstance.configManager.AppBackground;

    private static string CaptureArgument(string prefix)
    {
        string argument = OS.GetCmdlineUserArgs().FirstOrDefault(candidate => candidate.StartsWith(prefix));
        return argument?.Substring(prefix.Length);
    }
    private const double LayoutCaptureSettleSeconds = 8.0;
    private const double LayoutCaptureViewDelaySeconds = 4.0;

    private async void CaptureLayoutIfRequested()
    {
        string[] userArguments = OS.GetCmdlineUserArgs();
        string captureArgument = userArguments.FirstOrDefault(argument => argument.StartsWith(LayoutCaptureArgumentPrefix));

        if (captureArgument == null)
        {
            return;
        }

        ApplyCaptureWindowSize(userArguments);

        string capturePath = captureArgument.Substring(LayoutCaptureArgumentPrefix.Length);
        string viewArgument = userArguments.FirstOrDefault(argument => argument.StartsWith(LayoutCaptureViewArgumentPrefix));
        string systemArgument = userArguments.FirstOrDefault(argument => argument.StartsWith(LayoutCaptureSystemArgumentPrefix));
        await ToSignal(GetTree().CreateTimer(LayoutCaptureViewDelaySeconds), SceneTreeTimer.SignalName.Timeout);

        if (systemArgument != null)
        {
            string systemSlug = systemArgument.Substring(LayoutCaptureSystemArgumentPrefix.Length);
            int systemIndex = GameListHandler.gameSystems.FindIndex(system => system.Slug == systemSlug);

            if (systemIndex >= 0)
            {
                systemCarousel?.SetSelectionSilently(systemIndex);
                GameListHandler.SelectSystemByIndex(systemIndex);
                await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            }
        }

        switch (viewArgument?.Substring(LayoutCaptureViewArgumentPrefix.Length))
        {
            case "settings": SectionHandler.ShowSection(MainSceneSectionHandler.Section.Settings, false); break;
            case "downloads": SectionHandler.ShowSection(MainSceneSectionHandler.Section.Downloads, false); break;
            case "start": ToggleStartMenu(); break;
            case "jump": OpenSystemJumpPopup(); break;
            case "carousel": CaptureGameListView(0, 3); break;
            case "grid": CaptureGameListView(1, 9); break;
            case "list": CaptureGameListView(2, 6); break;
        }

        await ToSignal(GetTree().CreateTimer(LayoutCaptureSettleSeconds - LayoutCaptureViewDelaySeconds), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        Image capturedFrame = GetViewport().GetTexture().GetImage();
        Error saveResult = capturedFrame.SavePng(capturePath);
        string openPanel = panelStack.TopPanel?.Name ?? "none";
        GD.Print($"[Layout] captured {capturedFrame.GetWidth()}x{capturedFrame.GetHeight()} (canvas {GetViewportRect().Size}, carousel {gameList?.Size}, details {detailsPanel?.Size}, open panel {openPanel}, section {SectionHandler?.CurrentSection}) to {capturePath}: {saveResult}");
        GetTree().Quit();
    }

    private void CaptureGameListView(int viewIndex, int selectedIndex)
    {
        SetGameListView(viewIndex);
        ActiveGameList?.GrabFocus();
        ActiveGameList?.SelectIndex(selectedIndex, true);
    }

    public override void _ExitTree()
    {
        if (appInstance == null)
        {
            return;
        }

        if (appInstance.downloadManager != null && DownloadHandler != null)
        {
            appInstance.downloadManager.DownloadCompleted -= DownloadHandler.OnDownloadCompleted;
        }

        if (appInstance.emulatorManager != null)
        {
            appInstance.emulatorManager.EmulatorInstallationCompleted -= OnEmulatorInstallationCompleted;
            appInstance.emulatorManager.EmulatorLaunchStateChanged -= OnEmulatorLaunchStateChanged;
        }

        GameListHandler?.Detach();
        NetplayHandler?.Detach();
        UpdaterHandler?.Detach();
    }

    private async void OfferControllerLayerOnceTheInterfaceHasSettled()
    {
        await ToSignal(GetTree().CreateTimer(ControllerLayerOfferDelaySeconds), "timeout");
        InputHandler.OfferControllerLayerIfNotYetAsked();
    }

    public void ApplyTheme()
    {
        if (TryResolveThemeColors(out var colors))
        {
            var bgMaterial = GD.Load<ShaderMaterial>("res://assets/materials/moving_background.tres");
            if (bgMaterial != null)
            {
                var bgShader = GD.Load<Shader>(ConfigManager.BackgroundShaderPath(ActiveBackgroundName));
                if (bgShader != null && bgMaterial.Shader != bgShader)
                {
                    bgMaterial.Shader = bgShader;
                }

                bgMaterial.SetShaderParameter("bg_color", colors.Bg);
                bgMaterial.SetShaderParameter("primary_color", colors.Primary);
                bgMaterial.SetShaderParameter("secondary_color", colors.Secondary);
            }

            var panelMaterial = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
            if (panelMaterial != null)
            {
                Color tint = GetDarkestColor(colors.Bg, colors.Primary, colors.Secondary);
                tint.A = colors.Panel.A;
                panelMaterial.SetShaderParameter("mix_color", tint);
                panelMaterial.SetShaderParameter("luminosity_floor", panelLuminosityFloor);
            }

            ApplyProgressBarAccent(colors.Primary, colors.Secondary);
        }
    }

    private const float ProgressAccentSaturation = 0.5f;
    private const float ProgressAccentValue = 1.0f;
    private const float ProgressGlowAlpha = 0.35f;

    private static void ApplyProgressBarAccent(Color primary, Color secondary)
    {
        if (ThemeDB.GetProjectTheme()?.GetStylebox("fill", "ProgressBar") is not StyleBoxFlat fill)
        {
            return;
        }

        Color source = primary.S * primary.V >= secondary.S * secondary.V ? primary : secondary;
        Color accent = Color.FromHsv(source.H, ProgressAccentSaturation, ProgressAccentValue);

        fill.BgColor = accent;
        fill.ShadowColor = new Color(accent, ProgressGlowAlpha);
    }

    private bool TryResolveThemeColors(out (Color Bg, Color Primary, Color Secondary, Color Panel) colors)
    {
        string currentTheme = ActiveThemeName;

        if (ConfigManager.IsSystemTheme(currentTheme))
        {
            var derived = SystemPalette.FromSystem(CurrentGameSystem);
            if (derived.HasValue)
            {
                colors = derived.Value;
                return true;
            }
            return ConfigManager.Themes.TryGetValue("Default", out colors);
        }

        return ConfigManager.Themes.TryGetValue(currentTheme, out colors);
    }

    private GameSystem CurrentGameSystem
    {
        get
        {
            var systems = GameListHandler?.gameSystems;
            int index = GameListHandler?.currentGameSystemIndex ?? -1;
            if (systems == null || index < 0 || index >= systems.Count) return null;
            return systems[index];
        }
    }

    private static Color GetDarkestColor(params Color[] candidates)
    {
        Color darkest = candidates[0];
        float min = float.MaxValue;
        foreach (var c in candidates)
        {
            float luminance = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
            if (luminance < min)
            {
                min = luminance;
                darkest = c;
            }
        }
        return darkest;
    }

    private void OnReleasePickerClosed()
    {
        if (GameListHandler.currentlySelectedGame != null)
        {
            GameListHandler.UpdateDetailsPanelButtons(GameListHandler.currentlySelectedGame);
        }

        gameList?.GrabFocus();
    }

    private void OnEmulatorInstallationCompleted(string emulatorName, bool wasSuccessful)
    {
        if (GameListHandler.currentlySelectedGame != null)
        {
            GameListHandler.UpdateDetailsPanelButtons(GameListHandler.currentlySelectedGame);
        }
    }

    public async void OpenReleasePicker(string emulatorName)
    {
        if (releasePickerPopup == null) return;

        releasePickerPopup.ShowLoading(emulatorName);
        releasePickerPopup.Open();

        var releases = await appInstance.emulatorManager.GetAvailableReleases(emulatorName);

        if (!releasePickerPopup.IsOpen) return;

        if (releases.Count == 0)
        {
            releasePickerPopup.ShowError("No releases found. Check your connection and try again.");
            return;
        }

        releasePickerPopup.Populate(emulatorName, releases, appInstance.emulatorManager.GetInstalledVersion(emulatorName));
    }

    private void OnEmulatorReleaseChosen(int index)
    {
        if (releasePickerPopup == null || index < 0 || index >= releasePickerPopup.Releases.Count) return;

        var chosenRelease = releasePickerPopup.Releases[index];
        string emulatorName = releasePickerPopup.EmulatorName;
        releasePickerPopup.Close();

        _ = appInstance.emulatorManager.InstallEmulator(emulatorName, chosenRelease);
    }

    public bool IsBrowsingCollections { get; private set; }

    public bool HasCollections => appInstance.dataBus.collectionSystems != null && appInstance.dataBus.collectionSystems.Count > 0;

    public void ToggleBrowseMode()
    {
        if (!HasCollections && !IsBrowsingCollections)
        {
            return;
        }

        IsBrowsingCollections = !IsBrowsingCollections;
        GameListHandler.currentGameSystemIndex = -1;
        GetCache();
        GameListHandler.SelectSystemByIndex(0);

        GD.Print($"[Browse] mode={(IsBrowsingCollections ? "collections" : "systems")} entries={GameListHandler.gameSystems?.Count ?? -1} index={GameListHandler.currentGameSystemIndex}");
    }

    public void GetCache()
    {
        if (IsBrowsingCollections)
        {
            GameListHandler.gameSystems = appInstance.dataBus.collectionSystems;
            GameListHandler.games = appInstance.dataBus.gameCache;

            if (systemCarousel != null)
            {
                systemCarousel.Populate(GameListHandler.gameSystems, GameListHandler.currentGameSystemIndex >= 0 ? GameListHandler.currentGameSystemIndex : 0);
            }

            return;
        }

        if (appInstance.configManager.ShowAllSystems)
        {
            GameListHandler.gameSystems = appInstance.dataBus.systems;
        }
        else
        {
            GameListHandler.gameSystems = new List<GameSystem>();
            foreach(var sys in appInstance.dataBus.systems)
            {
                string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(sys.Slug);
                if (!string.IsNullOrEmpty(mappedEmulator) && appInstance.emulatorManager.LoadEmulatorMetadataFromDisk(mappedEmulator) != null)
                {
                    GameListHandler.gameSystems.Add(sys);
                }
            }

            if (GameListHandler.gameSystems.Count == 0)
            {
                GameListHandler.gameSystems = appInstance.dataBus.systems;
            }
        }
        GameListHandler.gameSystems = FilterSystemsForNetplayHost(GameListHandler.gameSystems);

        GameListHandler.games = appInstance.dataBus.gameCache;
        if (systemCarousel != null)
        {
            systemCarousel.Populate(GameListHandler.gameSystems, GameListHandler.currentGameSystemIndex >= 0 ? GameListHandler.currentGameSystemIndex : 0);
        }
    }

    private List<GameSystem> FilterSystemsForNetplayHost(List<GameSystem> candidateSystems)
    {
        if (appInstance.netplayLobby == null || !appInstance.netplayLobby.IsInLobby || appInstance.netplayManager == null)
        {
            return candidateSystems;
        }

        var netplaySystems = candidateSystems
            .Where(system => appInstance.netplayManager.SupportsNetplay(appInstance.emulatorManager.GetMappedEmulator(system.Slug), system.Slug))
            .ToList();

        return netplaySystems.Count > 0 ? netplaySystems : candidateSystems;
    }

    public void SelectSystemFromCarousel(int systemIndex)
    {
        systemCarousel?.SetSelectionSilently(systemIndex);
        GameListHandler.SelectSystemByIndex(systemIndex);
    }

    public void RefreshBrowseSourceForLobby()
    {
        GameListHandler.currentGameSystemIndex = -1;
        GetCache();
        GameListHandler.SelectSystemByIndex(0);
    }

    public void SetupFooterUI()
    {
        SetupButton(actionBtn, "Select", "Play");
        if (actionBtn != null) actionBtn.Pressed += OnPlayDownloadButtonPressed;

        SetupButton(deleteBtn, "DeleteGame", "Delete");
        if (deleteBtn != null) deleteBtn.Pressed += OnDeleteButtonPressed;

        SetupButton(settingsSelectBtn, "Select", "Select");
        SetupButton(settingsBackBtn, "Back", "Back");

        SetupButton(optionsBtn, "ToggleSettings", "Options");
        if (optionsBtn != null) optionsBtn.Pressed += ToggleStartMenu;

        SetupButton(filterInstalledGamesBtn, "ToggleInstalled", "All Games");
        if (filterInstalledGamesBtn != null) filterInstalledGamesBtn.Pressed += OnFilterInstalledGamesPressed;

        SetupButton(toggleDownloadsBtn, "ToggleDownloadsPage", "Downloads");
        if (toggleDownloadsBtn != null) toggleDownloadsBtn.Pressed += DownloadHandler.SwapLists;

        SetupButton(downloadsToggleDownloadsBtn, "ToggleDownloadsPage", "Games");
        if (downloadsToggleDownloadsBtn != null) downloadsToggleDownloadsBtn.Pressed += DownloadHandler.SwapLists;

        SetupButton(navHintBtn, "MoveUp", "Navigate");
        if (navHintBtn != null) navHintBtn.Disabled = true;

        SetupButton(cancelDownloadBtn, "CancelDownload", "Cancel");
        if (cancelDownloadBtn != null) cancelDownloadBtn.Pressed += DownloadHandler.OnCancelDownloadPressed;
    }

    private void SetupButton(Button btn, string iconPath, string defaultText)
    {
        if (btn == null) return;
        btn.Text = defaultText;
        btn.ThemeTypeVariation = "FlatButton";
        btn.Icon = ControllerGlyph.For(iconPath);
    }

    private void ToggleStartMenu()
    {
        if (settingsMenuContainer != null && settingsMenuContainer.IsOpen)
        {
            SettingsHandler.ToggleSettingsMenu();
            return;
        }

        if (startMenuPanel == null) return;

        if (startMenuPanel.IsOpen)
        {
            startMenuPanel.Close();
        }
        else if (downloadsListContainer == null || !downloadsListContainer.IsOpen)
        {
            startMenuPanel.ShowMenuView();
            PopupHandler.RefreshEmulatorMenuOptions();
            startMenuPanel.Open();
        }
    }

    private void OnFilterInstalledGamesPressed()
    {
        if (NetplayHandler != null && NetplayHandler.HandleLobbyBackPressed())
        {
            return;
        }

        if (GameListHandler.IsFilterTransitioning) return;

        GameListHandler.showOnlyInstalledGames = !GameListHandler.showOnlyInstalledGames;
        if (filterInstalledGamesBtn != null)
        {
            filterInstalledGamesBtn.Text = GameListHandler.showOnlyInstalledGames ? "Installed" : "All Games";
        }
        GameListHandler.ApplyFiltersWithFade();
    }

    private void CreateGameListViews()
    {
        if (gameList is not GameListView carouselView)
        {
            return;
        }

        gameListViews.Add(carouselView);

        foreach (GameListView extraView in new GameListView[] { new GameGridView { Name = "GameGrid" }, new GameTextListView { Name = "GameTextList" } })
        {
            extraView.SizeFlagsHorizontal = carouselView.SizeFlagsHorizontal;
            extraView.SizeFlagsVertical = carouselView.SizeFlagsVertical;
            extraView.Visible = false;
            carouselView.AddSibling(extraView);
            gameListViews.Add(extraView);
        }
    }

    public void SetGameListView(int viewIndex)
    {
        if (viewIndex < 0 || viewIndex >= gameListViews.Count || gameListViews[viewIndex] == gameList)
        {
            return;
        }

        GameListView previousView = ActiveGameList;
        bool hadFocus = previousView != null && previousView.HasFocus();

        if (previousView != null)
        {
            previousView.ReloadItems(0, 0);
            previousView.Visible = false;
        }

        GameListView nextView = gameListViews[viewIndex];
        nextView.Visible = true;
        gameList = nextView;

        if (GameListHandler.currentlyShownGames != null)
        {
            GameListHandler.RefreshGameList();
        }

        if (hadFocus)
        {
            nextView.GrabFocus();
        }
    }

    private void OnPlayDownloadButtonPressed()
    {
        if (NetplayHandler != null && NetplayHandler.HandleGameConfirmedInLobby())
        {
            return;
        }

        if (GameListHandler.currentlySelectedGame == null) return;

        var gameAction = GameListHandler.ResolveGameAction(GameListHandler.currentlySelectedGame);

        if (gameAction.Disabled) return;

        switch (gameAction.Kind)
        {
            case GameActionKind.InstallEmulator:
                actionBtn.Disabled = true;
                OpenReleasePicker(gameAction.EmulatorName);
                return;

            case GameActionKind.DownloadGame:
                DownloadHandler.DownloadGame(GameListHandler.currentlySelectedGame);
                return;

            case GameActionKind.LaunchGame:
                appInstance.emulatorManager.LaunchEmulatorWithGame(GameListHandler.currentlySelectedGame);
                return;
        }
    }

    private void OnEmulatorLaunchStateChanged()
    {
        if (GameListHandler.currentlySelectedGame != null)
        {
            GameListHandler.UpdateDetailsPanelButtons(GameListHandler.currentlySelectedGame);
        }

        PopupHandler.RefreshEmulatorMenuOptions();
    }

    private void OnDeleteButtonPressed()
    {
        if (GameListHandler.currentlySelectedGame == null) return;
        GameListHandler.DeleteLocalGame(GameListHandler.currentlySelectedGame);
        GameListHandler.ApplyFiltersAndRefresh();
        GameListHandler.UpdateDetailsPanelButtons(GameListHandler.currentlySelectedGame);
    }

    public void UpdateHeaderLabel()
    {
        if (systemCarousel == null) return;

        switch (SectionHandler.CurrentSection)
        {
            case MainSceneSectionHandler.Section.Settings:
                systemCarousel.SetOverrideText("Settings");
                break;
            case MainSceneSectionHandler.Section.Downloads:
                systemCarousel.SetOverrideText("Downloads");
                break;
            default:
                systemCarousel.ClearOverride();
                break;
        }
    }

    private void OnChangelogPanelAccepted()
    {
        if (changelogPanel.ActiveSubject == ChangelogPanel.PromptSubject.ControllerLayer)
        {
            changelogPanel.Close();
            InputHandler.OnControllerLayerOfferAccepted();
            return;
        }

        UpdaterHandler.OnAcceptUpdatePressed();
    }

    private void OnChangelogPanelDismissed()
    {
        if (changelogPanel.ActiveSubject == ChangelogPanel.PromptSubject.ControllerLayer)
        {
            changelogPanel.Close();
            InputHandler.OnControllerLayerOfferDeclined();
            return;
        }

        UpdaterHandler.OnCancelUpdatePressed();
    }

    private bool ShouldFrontendIgnoreInput()
    {
        if (appInstance.emulatorManager != null && appInstance.emulatorManager.IsEmulatorRunning)
        {
            return true;
        }

        if (appInstance.inputLayer != null && appInstance.inputLayer.IsSessionActive)
        {
            return true;
        }

        return !IsAutomatedInputRunning && GetWindow() != null && !GetWindow().HasFocus();
    }

    public bool IsAutomatedInputRunning { get; set; }

    public override void _Input(InputEvent @event)
    {
        if (SectionHandler.IsTransitioning)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            UpdateMouseFocus();
        }

        if (@event is InputEventMouseButton wheelEvent && wheelEvent.Pressed
            && (wheelEvent.ButtonIndex == MouseButton.WheelUp || wheelEvent.ButtonIndex == MouseButton.WheelDown)
            && IsMouseOverGameList()
            && ActiveGameList is GameListView wheelView)
        {
            wheelView.ScrollStep(wheelEvent.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (InputHandler.isListeningForControllerAssignment)
        {
            if (@event is InputEventJoypadButton assignmentButton && assignmentButton.Pressed)
            {
                InputHandler.RecordControllerAssignment(assignmentButton.Device);
            }

            else if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                InputHandler.CancelControllerAssignment();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (InputHandler.isListeningForEmulatorCloseHotkeys)
        {
            if (@event is InputEventJoypadButton listenJoyBtn && listenJoyBtn.Pressed)
            {
                int btnVal = (int)listenJoyBtn.ButtonIndex;
                if (!InputHandler.collectedEmulatorCloseHotkeys.Contains(btnVal))
                {
                    InputHandler.collectedEmulatorCloseHotkeys.Add(btnVal);
                    int currentCount = InputHandler.collectedEmulatorCloseHotkeys.Count;
                    if (emulatorCloseHotkeysBtn != null)
                    {
                        emulatorCloseHotkeysBtn.Text = $"Listening... ({currentCount}/{InputHandler.expectedEmulatorCloseHotkeysCount})";
                    }

                    if (currentCount >= InputHandler.expectedEmulatorCloseHotkeysCount)
                    {
                        InputHandler.isListeningForEmulatorCloseHotkeys = false;
                        appInstance.configManager.SaveInputSettings(InputHandler.expectedEmulatorCloseHotkeysCount, InputHandler.collectedEmulatorCloseHotkeys);
                        InputHandler.UpdateEmulatorCloseHotkeysBtnText();
                    }
                }
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventKey || @event is InputEventJoypadButton || @event is InputEventJoypadMotion)
            {
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (InputHandler.isListeningForInput)
        {
            if ((@event is InputEventKey listenKeyEvent && listenKeyEvent.Pressed) ||
                (@event is InputEventJoypadButton listenJoyBtn && listenJoyBtn.Pressed) ||
                (@event is InputEventJoypadMotion listenJoyMotion && Mathf.Abs(listenJoyMotion.AxisValue) > 0.5f))
            {
                string mappedInput = InputHandler.ConvertInputEventToStandardString(@event);
                if (mappedInput != null)
                {
                    InputHandler.isListeningForInput = false;
                    GetViewport().SetInputAsHandled();
                    InputHandler.inputListenCallback?.Invoke(mappedInput);
                }
                else
                {
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventKey || @event is InputEventJoypadButton || @event is InputEventJoypadMotion)
            {
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (ShouldFrontendIgnoreInput())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (panelStack.HasOpenPanel)
        {
            if (@event is InputEventMouse) return;
            panelStack.HandleInput(@event);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ToggleSettings"))
        {
            ToggleStartMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        bool isAnyPopupVisible = panelStack.HasOpenPanel ||
                                 (settingsMenuContainer != null && settingsMenuContainer.IsOpen) ||
                                 (downloadsListContainer != null && downloadsListContainer.IsOpen);

        if (!isAnyPopupVisible && @event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            ulong currentTime = Time.GetTicksMsec();

            if (keyEvent.Keycode == Key.Backspace)
            {
                if (GameListHandler.fuzzySearchBuffer.Length > 0)
                {
                    GameListHandler.fuzzySearchBuffer = GameListHandler.fuzzySearchBuffer.Substring(0, GameListHandler.fuzzySearchBuffer.Length - 1);
                }
                GameListHandler.lastKeystrokeTime = currentTime;
                GameListHandler.isFuzzySearchDirty = true;
            }
            else if (keyEvent.Unicode >= 32)
            {
                if (currentTime - GameListHandler.lastKeystrokeTime > 1500)
                {
                    GameListHandler.fuzzySearchBuffer = "";
                }

                GameListHandler.fuzzySearchBuffer += (char)keyEvent.Unicode;
                GameListHandler.fuzzySearchBuffer = GameListHandler.fuzzySearchBuffer.ToLower();
                GameListHandler.lastKeystrokeTime = currentTime;
                GameListHandler.isFuzzySearchDirty = true;
            }
        }

        if (settingsMenuContainer != null && settingsMenuContainer.IsOpen)
        {
            if (settingsMenuContainer.HandleInput(@event)) GetViewport().SetInputAsHandled();
            return;
        }

        if(@event.IsActionPressed("CylceSystemUp") && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
        {
            rightBumperPressedTime = Time.GetTicksMsec();
            return;
        }
        else if (@event.IsActionReleased("CylceSystemUp"))
        {
            if (rightBumperPressedTime > 0 && Time.GetTicksMsec() - rightBumperPressedTime < 250)
            {
                if (systemCarousel != null && systemCarousel.Next())
                {
                    GameListHandler.BeginQuickSwitchFade();
                }
            }
            rightBumperPressedTime = 0;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("CycleSystemDown") && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
        {
            leftBumperPressedTime = Time.GetTicksMsec();
            return;
        }
        else if (@event.IsActionReleased("CycleSystemDown"))
        {
            if (leftBumperPressedTime > 0 && Time.GetTicksMsec() - leftBumperPressedTime < 250)
            {
                if (systemCarousel != null && systemCarousel.Previous())
                {
                    GameListHandler.BeginQuickSwitchFade();
                }
            }
            leftBumperPressedTime = 0;
            GetViewport().SetInputAsHandled();
            return;
        }

        if ((@event.IsActionPressed("Back") || @event.IsActionPressed("ui_cancel"))
            && (downloadsListContainer == null || !downloadsListContainer.IsOpen)
            && NetplayHandler != null
            && NetplayHandler.HandleLobbyBackPressed())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (IsControllerEvent(@event))
        {
            if (@event.IsActionPressed("Select") && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
            {
                OnPlayDownloadButtonPressed();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("ToggleInstalled") && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
            {
                OnFilterInstalledGamesPressed();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("ToggleCollections") && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
            {
                ToggleBrowseMode();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("DeleteGame") && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
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
                DownloadHandler.SwapLists();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("CancelDownload"))
            {
                if (downloadsListContainer != null && downloadsListContainer.IsOpen)
                {
                    DownloadHandler.OnCancelDownloadPressed();
                    GetViewport().SetInputAsHandled();
                }
                return;
            }
        }

        if (@event.IsActionPressed("ui_up", true) || @event.IsActionPressed("MoveUp"))
        {
            if ((downloadsListContainer == null || !downloadsListContainer.IsOpen)
                && NetplayHandler != null
                && NetplayHandler.HandleLobbyNavigation(-1))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (downloadsListContainer != null && downloadsListContainer.IsOpen)
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
            if ((downloadsListContainer == null || !downloadsListContainer.IsOpen)
                && NetplayHandler != null
                && NetplayHandler.HandleLobbyNavigation(1))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (downloadsListContainer != null && downloadsListContainer.IsOpen)
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

    private static bool IsControllerEvent(InputEvent @event)
    {
        return @event is InputEventJoypadButton || @event is InputEventJoypadMotion;
    }

    private void UpdateMouseFocus()
    {
        var viewport = GetViewport();
        var hovered = viewport.GuiGetHoveredControl();
        if (hovered == null) return;

        UiPanel topPanel = panelStack.TopPanel;
        if (topPanel != null && !topPanel.IsAncestorOf(hovered)) return;

        Control focusable = hovered;
        while (focusable != null && focusable.FocusMode != Control.FocusModeEnum.All)
        {
            focusable = focusable.GetParentOrNull<Control>();
        }

        if (focusable == null || !focusable.IsVisibleInTree()) return;

        if (focusable is BaseButton { Disabled: true }) return;

        if (gameList != null && (focusable == gameList || gameList.IsAncestorOf(focusable)))
        {
            if (IsAnyMenuOpen()) return;

            focusable = gameList;
        }

        if (focusable != viewport.GuiGetFocusOwner())
        {
            focusable.GrabFocus();
        }
    }

    private void ReturnFocusToGameList()
    {
        if (IsAnyMenuOpen())
        {
            return;
        }

        gameList?.GrabFocus();
    }

    private bool IsAnyMenuOpen()
    {
        return panelStack.HasOpenPanel
            || (settingsMenuContainer != null && settingsMenuContainer.IsOpen)
            || (downloadsListContainer != null && downloadsListContainer.IsOpen);
    }

    private bool IsMouseOverGameList()
    {
        if (gameList == null || !gameList.IsVisibleInTree() || IsAnyMenuOpen())
        {
            return false;
        }

        var hovered = GetViewport().GuiGetHoveredControl();
        return hovered != null && (hovered == gameList || gameList.IsAncestorOf(hovered));
    }

    public override void _Process(double delta)
    {
        GameListHandler?.PumpDecodedImages();
        GameListHandler?.ProcessPendingDetailsRefresh();
        GameListHandler?.ProcessPendingScreenshotLoads();
        GameListHandler?.ProcessPendingImageLoads();
        InputHandler?.UpdateEmulatorCloseHold();

        ulong currentTime = Time.GetTicksMsec();

        if (rightBumperPressedTime > 0 && currentTime - rightBumperPressedTime >= 250)
        {
            rightBumperPressedTime = 0;
            leftBumperPressedTime = 0;
            OpenSystemJumpPopup();
        }
        else if (leftBumperPressedTime > 0 && currentTime - leftBumperPressedTime >= 250)
        {
            leftBumperPressedTime = 0;
            rightBumperPressedTime = 0;
            OpenSystemJumpPopup();
        }

        if (GameListHandler != null)
        {
            currentTime = Time.GetTicksMsec();
            if (GameListHandler.fuzzySearchBuffer.Length > 0 && currentTime - GameListHandler.lastKeystrokeTime > 1500)
            {
                GameListHandler.fuzzySearchBuffer = "";
                fuzzySearchPopup?.Close();
            }

            if (GameListHandler.isFuzzySearchDirty && currentTime - GameListHandler.lastKeystrokeTime > 400)
            {
                GameListHandler.isFuzzySearchDirty = false;

                if (!string.IsNullOrEmpty(GameListHandler.fuzzySearchBuffer))
                {
                    int matchIndex = GameListHandler.currentlyShownGames.FindIndex(g => g.Name.ToLower().Contains(GameListHandler.fuzzySearchBuffer));

                    if (matchIndex != -1)
                    {
                        GameListHandler.OnGameSelected(matchIndex);
                        if (ActiveGameList != null)
                        {
                            ActiveGameList.SelectIndex(matchIndex, false);
                        }
                    }
                }
            }
            if (fuzzySearchPopup != null)
            {
                if (!string.IsNullOrEmpty(GameListHandler.fuzzySearchBuffer))
                {
                    if (fuzzySearchLabel != null) fuzzySearchLabel.Text = "Search: " + GameListHandler.fuzzySearchBuffer;
                    fuzzySearchPopup.Open();
                }
                else
                {
                    fuzzySearchPopup.Close();
                }
            }
        }
    }

    private void OpenSystemJumpPopup()
    {
        if (systemJumpPopup != null && !systemJumpPopup.IsOpen && (downloadsListContainer == null || !downloadsListContainer.IsOpen))
        {
            systemJumpPopup.Populate(GameListHandler.gameSystems, GameListHandler.currentGameSystemIndex);
            systemJumpPopup.Open();
            systemJumpPopup.FocusSystem(GameListHandler.currentGameSystemIndex >= 0 ? GameListHandler.currentGameSystemIndex : 0);
        }
    }
}
