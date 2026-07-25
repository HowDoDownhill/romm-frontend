using Godot;
using System;
using System.Collections.Generic;

public partial class MainScene : Control
{
    [ExportGroup("Header")]
    [Export] public MarginContainer headerContainer;
    [Export] public SystemCarousel systemCarousel;

    [ExportGroup("GameList")]
    [Export] public Control gameList;
    [Export] public PackedScene gameListEntryScene;

    [ExportGroup("DetailsPanel")]
    [Export] public Control detailsPanel;
    [Export] public VBoxContainer detailsPanelContainer;
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
        fuzzySearchLabel.AddThemeFontSizeOverride("font_size", 24);
        margin.AddChild(fuzzySearchLabel);
        fuzzySearchPopup.ContentRoot.AddChild(margin);

        GameListHandler = new MainSceneGameListHandler(this, appInstance);
        DownloadHandler = new MainSceneDownloadHandler(this, appInstance);
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

        if (startMenuPanel != null)
        {
            panelStack.Register(startMenuPanel);
            startMenuPanel.BiosViewRequested += PopupHandler.PopulateBiosSelector;

            if (startMenuPanel.launchEmulatorButton != null) startMenuPanel.launchEmulatorButton.Pressed += PopupHandler.OnLaunchEmulatorPressed;
            if (startMenuPanel.updateEmulatorButton != null) startMenuPanel.updateEmulatorButton.Pressed += PopupHandler.OnUpdateEmulatorPressed;
            if (startMenuPanel.uninstallEmulatorButton != null) startMenuPanel.uninstallEmulatorButton.Pressed += PopupHandler.OnUninstallEmulatorPressed;
            if (startMenuPanel.selectBiosButton != null) startMenuPanel.selectBiosButton.Pressed += PopupHandler.OnSelectBiosMenuPressed;
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
            changelogPanel.Accepted += UpdaterHandler.OnAcceptUpdatePressed;
            changelogPanel.Dismissed += UpdaterHandler.OnCancelUpdatePressed;
        }

        GameListHandler.SelectSystemByIndex(0);

        if (gameList != null)
        {
            gameList.Connect("ItemSelected", Callable.From<long>(GameListHandler.OnGameSelected));
            gameList.Connect("ItemFocused", Callable.From<long>(GameListHandler.OnGameSelected));
            gameList.Connect("JumpSectionRequested", Callable.From<int>(GameListHandler.OnJumpSectionRequested));
        }

        DownloadHandler.SetupDownloadsList();
        SetupFooterUI();
        UpdaterHandler.InitUpdater();
        ApplyTheme();

        var micaMaterial = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
        MicaShadow.AttachToAll(this, micaMaterial, panelShadowColor, panelShadowSize, panelShadowOffset);
    }

    public void ApplyTheme()
    {
        if (TryResolveThemeColors(out var colors))
        {
            var bgMaterial = GD.Load<ShaderMaterial>("res://assets/materials/moving_background.tres");
            if (bgMaterial != null)
            {
                var bgShader = GD.Load<Shader>(ConfigManager.BackgroundShaderPath(appInstance.configManager.AppBackground));
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

            systemJumpPopup?.ApplyTheme(colors.Secondary);
            releasePickerPopup?.ApplyTheme(colors.Secondary);
        }
    }

    private bool TryResolveThemeColors(out (Color Bg, Color Primary, Color Secondary, Color Panel) colors)
    {
        string currentTheme = appInstance.configManager.AppTheme;

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

        releasePickerPopup.Populate(emulatorName, releases);
    }

    private void OnEmulatorReleaseChosen(int index)
    {
        if (releasePickerPopup == null || index < 0 || index >= releasePickerPopup.Releases.Count) return;

        var chosenRelease = releasePickerPopup.Releases[index];
        string emulatorName = releasePickerPopup.EmulatorName;
        releasePickerPopup.Close();

        _ = appInstance.emulatorManager.InstallEmulator(emulatorName, chosenRelease);
    }

    public void GetCache()
    {
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
        GameListHandler.games = appInstance.dataBus.gameCache;
        if (systemCarousel != null)
        {
            systemCarousel.Populate(GameListHandler.gameSystems, GameListHandler.currentGameSystemIndex >= 0 ? GameListHandler.currentGameSystemIndex : 0);
        }
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
        if (GameListHandler.IsFilterTransitioning) return;

        GameListHandler.showOnlyInstalledGames = !GameListHandler.showOnlyInstalledGames;
        if (filterInstalledGamesBtn != null)
        {
            filterInstalledGamesBtn.Text = GameListHandler.showOnlyInstalledGames ? "Installed" : "All Games";
        }
        GameListHandler.ApplyFiltersWithFade();
    }

    private void OnPlayDownloadButtonPressed()
    {
        if (GameListHandler.currentlySelectedGame == null) return;

        if (actionBtn != null && actionBtn.Disabled) return;

        string emulatorName = appInstance.emulatorManager.GetMappedEmulator(GameListHandler.currentlySelectedGame.PlatformSlug);

        if (actionBtn.Text == "Install Emulator")
        {
            actionBtn.Disabled = true;
            OpenReleasePicker(emulatorName);
            return;
        }

        bool isGameDownloadedLocally = GameListHandler.CheckIfGameIsDownloaded(GameListHandler.currentlySelectedGame);

        if (isGameDownloadedLocally)
        {
            appInstance.emulatorManager.LaunchEmulatorWithGame(GameListHandler.currentlySelectedGame);
        }
        else
        {
            DownloadHandler.DownloadGame(GameListHandler.currentlySelectedGame);
        }
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
            && gameList is VerticalCarousel gameCarousel)
        {
            if (wheelEvent.ButtonIndex == MouseButton.WheelDown) gameCarousel.SelectNext();
            else gameCarousel.SelectPrevious();
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
                if (systemCarousel != null)
                {
                    systemCarousel.Next();
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
                if (systemCarousel != null)
                {
                    systemCarousel.Previous();
                    GameListHandler.BeginQuickSwitchFade();
                }
            }
            leftBumperPressedTime = 0;
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
        GameListHandler?.ProcessPendingImageLoads();

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
                        if (gameList != null && gameList.HasMethod("Refresh"))
                        {
                            gameList.Set("SelectedIndex", matchIndex);
                            gameList.Call("Refresh");
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
