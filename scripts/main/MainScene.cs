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
    [Export] public VBoxContainer detailsPanelContainer;
    [Export] public TextureRect gameCover;
    [Export] public TextureRect gameMarquee;
    [Export] public Label gameTitle;
    [Export] public RichTextLabel gameDescription;
    [Export] public TextureRect installedIcon;
    [Export] public ScrollContainer gameScreenshotsScroll;
    [Export] public GridContainer gameScreenshotsFlow;
    [Export] public ProgressBar gameDownloadProgressBar;

    [ExportGroup("DowloadsList")]
    [Export] public MarginContainer downloadsListContainer;
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

    [ExportGroup("PopupOptions")]
    [Export] public Node LaunchEmulatorPopupOption;
    [Export] public Node SelectBiosPopupOption;
    [Export] public Node SettingsPopupOption;
    [Export] public Node RefreshGamesPopupOption;
    [Export] public Node RefreshCurrentSystemGamesPopupOption;
    [Export] public Node QuitPopupOption;
    [Export] public Node RandomGamePopupOption;

    [ExportGroup("Update & Refresh UI")]
    [Export] public Control downloadProgressPopup;
    [Export] public Label downloadProgressLabel;
    [Export] public ProgressBar downloadProgressBar;
    
    [Export] public Control changelogPopup;
    [Export] public RichTextLabel changelogRichTextLabel;
    [Export] public Button acceptUpdateBtn;
    [Export] public Button cancelUpdateBtn;

    public AppInstance appInstance;
    public ImageTexture placeholderTexture;
    [Export] public ColorRect backgroundRect;
    [Export] public VBoxContainer mainVBoxContainer;

    [Export] public Control startMenu;
    [Export] public Control startMenuContainer;
    [Export] public Control biosSelectorContainer;
    [Export] public VBoxContainer biosSelector;
    public Control startMenuRoot;

    [Export] public MarginContainer settingsMenuContainer;
    [Export] public VBoxContainer settingsSectionsTree;
    [Export] public VBoxContainer sectionOptionsContainer;

    public Button emulatorCloseHotkeysBtn;

    public MainSceneSettingsHandler SettingsHandler { get; private set; }
    public MainSceneInputHandler InputHandler { get; private set; }
    public MainSceneGameListHandler GameListHandler { get; private set; }
    public MainSceneDownloadHandler DownloadHandler { get; private set; }
    public MainSceneUpdaterHandler UpdaterHandler { get; private set; }
    public MainScenePopupHandler PopupHandler { get; private set; }
    
    public HoverPopupOverlay HoverOverlay { get; private set; }

    public PanelContainer fuzzySearchPopup;
    public Label fuzzySearchLabel;

    public SystemJumpPopup systemJumpPopup;
    private ulong leftBumperPressedTime = 0;
    private ulong rightBumperPressedTime = 0;

    public override void _Ready()
    {
        var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        whiteImage.Fill(Colors.White);
        placeholderTexture = ImageTexture.CreateFromImage(whiteImage);
        appInstance = GetNode<AppInstance>("/root/AppInstance");

        SettingsHandler = new MainSceneSettingsHandler(this, appInstance);
        PopupHandler = new MainScenePopupHandler(this, appInstance);
        
        fuzzySearchPopup = new PanelContainer();
        fuzzySearchPopup.Visible = false;
        // Shared mica frosted-glass material (same as the panels). In the normal draw flow, so
        // Godot auto-copies the back buffer for the shader.
        fuzzySearchPopup.Material = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
        var fuzzyStyle = new StyleBoxFlat
        {
            // The mica shader replaces the fill; this stylebox only defines the rounded shape.
            BgColor = new Color(0, 0, 0, 1f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        };
        fuzzySearchPopup.AddThemeStyleboxOverride("panel", fuzzyStyle);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        fuzzySearchLabel = new Label();
        fuzzySearchLabel.AddThemeFontSizeOverride("font_size", 24);
        margin.AddChild(fuzzySearchLabel);
        fuzzySearchPopup.AddChild(margin);
        // Center on screen; grow both ways from the center anchor so it stays centered as the
        // content-sized panel resizes with the search text.
        fuzzySearchPopup.SetAnchorsPreset(Control.LayoutPreset.Center);
        fuzzySearchPopup.GrowHorizontal = Control.GrowDirection.Both;
        fuzzySearchPopup.GrowVertical = Control.GrowDirection.Both;
        AddChild(fuzzySearchPopup);

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

            // Clicking the platform logo opens the system jump grid (same as holding a bumper).
            systemCarousel.JumpRequested += OpenSystemJumpPopup;

            // Clicking the arrows fades the current system out immediately, like the bumpers do.
            systemCarousel.Cycled += GameListHandler.BeginQuickSwitchFade;
        }

        HoverOverlay = new HoverPopupOverlay();
        AddChild(HoverOverlay);

        systemJumpPopup = new SystemJumpPopup();
        AddChild(systemJumpPopup);
        systemJumpPopup.SystemSelected += (index) => 
        {
            systemJumpPopup.Visible = false;
            if (systemCarousel != null)
            {
                systemCarousel.SetSelectionSilently(index);
            }
            GameListHandler.SelectSystemByIndex(index);
        };

        appInstance.downloadManager.DownloadCompleted += DownloadHandler.OnDownloadCompleted;
        appInstance.emulatorManager.EmulatorInstallationCompleted += OnEmulatorInstallationCompleted;

        if (startMenuContainer != null)
        {
            startMenuRoot = startMenuContainer.GetParent()?.GetParent() as Control;

            if (startMenuRoot != null)
            {
                startMenuRoot.Visible = false;
            }

            if (LaunchEmulatorPopupOption is Button launchBtn) launchBtn.Pressed += PopupHandler.OnLaunchEmulatorPressed;
            if (SelectBiosPopupOption is Button biosBtn) biosBtn.Pressed += PopupHandler.OnSelectBiosMenuPressed;
            if (SettingsPopupOption is Button settingsBtn) settingsBtn.Pressed += PopupHandler.OnSettingsMenuPressed;
            if (RefreshGamesPopupOption is Button refreshBtn) refreshBtn.Pressed += PopupHandler.OnRefreshGamesPressed;
            if (RefreshCurrentSystemGamesPopupOption is Button refreshSysBtn) refreshSysBtn.Pressed += PopupHandler.OnRefreshCurrentSystemGamesPressed;
            if (QuitPopupOption is Button quitBtn) quitBtn.Pressed += PopupHandler.OnQuitPressed;
            if (RandomGamePopupOption is Button randomGameBtn) randomGameBtn.Pressed += PopupHandler.OnRandomGamePressed;
        }

        GetCache();
        
        if (settingsMenuContainer != null)
        {
            settingsMenuContainer.Visible = false;
            SettingsHandler.SetupSettingsTree();
        }

        if (acceptUpdateBtn != null)
        {
            acceptUpdateBtn.Pressed += UpdaterHandler.OnAcceptUpdatePressed;
            acceptUpdateBtn.Icon = MakeControllerGlyph("Select"); // A = Install
        }
        if (cancelUpdateBtn != null)
        {
            cancelUpdateBtn.Pressed += UpdaterHandler.OnCancelUpdatePressed;
            cancelUpdateBtn.Icon = MakeControllerGlyph("Back"); // B = Close
        }

        GameListHandler.SelectSystemByIndex(0);
        
        if (gameList != null)
        {
            gameList.Connect("ItemSelected", Callable.From<long>(GameListHandler.OnGameSelected));
            gameList.Connect("ItemFocused", Callable.From<long>(GameListHandler.OnGameSelected));
            gameList.Connect("JumpSectionRequested", Callable.From<int>(GameListHandler.OnJumpSectionRequested));
            // gameList.FocusExited += () => HoverOverlay.ForceCancelPopup();
        }

        DownloadHandler.SetupDownloadsList();
        SetupFooterUI();
        UpdaterHandler.InitUpdater();
        ApplyTheme();

        // Give every frosted panel a native StyleBoxFlat outline so overlapping panels are separable.
        var micaMaterial = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
        MicaBorder.AttachToAll(this, micaMaterial, MicaBorder.DefaultColor);
    }

    public void ApplyTheme()
    {
        string currentTheme = appInstance.configManager.AppTheme;
        if (ConfigManager.Themes.TryGetValue(currentTheme, out var colors))
        {
            var bgMaterial = GD.Load<ShaderMaterial>("res://assets/materials/moving_background.tres");
            if (bgMaterial != null)
            {
                bgMaterial.SetShaderParameter("bg_color", colors.Bg);
                bgMaterial.SetShaderParameter("primary_color", colors.Primary);
                bgMaterial.SetShaderParameter("secondary_color", colors.Secondary);
            }

            var panelMaterial = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
            if (panelMaterial != null)
            {
                // Tint the mica frost with the theme's darkest palette color so panels feel themed
                // rather than generic black. The Panel color still supplies the tint strength (alpha).
                Color tint = GetDarkestColor(colors.Bg, colors.Primary, colors.Secondary);
                tint.A = colors.Panel.A;
                panelMaterial.SetShaderParameter("mix_color", tint);
            }

            HoverOverlay?.ApplyThemeColor(colors.Panel, colors.Secondary);
            systemJumpPopup?.ApplyTheme(colors.Secondary);
        }
    }

    // Returns the darkest of the given colors by perceived luminance (alpha ignored).
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

    private void OnEmulatorInstallationCompleted(string emulatorName, bool wasSuccessful)
    {
        if (GameListHandler.currentlySelectedGame != null)
        {
            GameListHandler.UpdateDetailsPanelButtons(GameListHandler.currentlySelectedGame);
        }
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
        btn.Icon = MakeControllerGlyph(iconPath);
    }

    // Builds a controller-glyph texture for the given input action. It only renders when a
    // controller is the active input, so in keyboard/mouse mode buttons show just their label.
    private ControllerIconTexture MakeControllerGlyph(string actionPath)
    {
        var iconTex = new ControllerIconTexture();
        iconTex.path = actionPath;
        iconTex.show_mode = ControllerIcons.EShowMode.CONTROLLER;
        return iconTex;
    }

    // Opens/closes the start menu (or closes the settings menu if it's open). Shared by the
    // ToggleSettings action and the footer "Options" button so both behave identically.
    private void ToggleStartMenu()
    {
        if (settingsMenuContainer != null && settingsMenuContainer.Visible)
        {
            SettingsHandler.ToggleSettingsMenu();
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
                if (startMenuContainer != null) startMenuContainer.Visible = true;
                if (biosSelectorContainer != null) biosSelectorContainer.Visible = false;
                if (LaunchEmulatorPopupOption is Control launchBtn) launchBtn.GrabFocus();
            }
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

        string emulatorName = appInstance.emulatorManager.GetMappedEmulator(GameListHandler.currentlySelectedGame.PlatformSlug);

        if (actionBtn.Text == "Install Emulator")
        {
            actionBtn.Disabled = true; 
            _ = appInstance.emulatorManager.InstallEmulator(emulatorName); 
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

        if (settingsMenuContainer != null && settingsMenuContainer.Visible)
        {
            systemCarousel.SetOverrideText("Settings");
        }
        else if (downloadsListContainer != null && downloadsListContainer.Visible)
        {
            systemCarousel.SetOverrideText("Downloads");
        }
        else
        {
            systemCarousel.ClearOverride();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
        {
            UpdateMouseFocus();
        }

        // Mouse wheel scrolls the game selection wheel when the cursor is over it.
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

        if (changelogPopup != null && changelogPopup.Visible)
        {
            // Let mouse events through so the Install/Close buttons can be clicked; handle A/B
            // (and keyboard equivalents) for controller/keyboard users.
            if (@event is InputEventMouse) return;

            if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("Select"))
            {
                UpdaterHandler.OnAcceptUpdatePressed();
            }
            else if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                UpdaterHandler.OnCancelUpdatePressed();
            }

            GetViewport().SetInputAsHandled();
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

        if (systemJumpPopup != null && systemJumpPopup.Visible)
        {
            // Let mouse events reach the popup's buttons; consume everything else so no
            // input leaks through to the UI behind the popup.
            if (@event is InputEventMouse) return;
            systemJumpPopup.HandleInput(@event);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ToggleSettings"))
        {
            ToggleStartMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
        
        bool isAnyPopupVisible = (startMenuRoot != null && startMenuRoot.Visible) || 
                                 (settingsMenuContainer != null && settingsMenuContainer.Visible) ||
                                 (downloadsListContainer != null && downloadsListContainer.Visible);

        if (!isAnyPopupVisible && @event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            ulong currentTime = Time.GetTicksMsec();

            if (keyEvent.Keycode == Key.Backspace)
            {
                // Let the user fix typos by removing the last character.
                if (GameListHandler.fuzzySearchBuffer.Length > 0)
                {
                    GameListHandler.fuzzySearchBuffer = GameListHandler.fuzzySearchBuffer.Substring(0, GameListHandler.fuzzySearchBuffer.Length - 1);
                }
                GameListHandler.lastKeystrokeTime = currentTime;
                GameListHandler.isFuzzySearchDirty = true;
            }
            else if (keyEvent.Unicode >= 32) // printable characters only; skip control keys
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

        if (startMenuRoot != null && startMenuRoot.Visible)
        {
            if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                if (biosSelectorContainer != null && biosSelectorContainer.Visible)
                {
                    biosSelectorContainer.Visible = false;
                    if (startMenuContainer != null) startMenuContainer.Visible = true;
                    (SelectBiosPopupOption as Control)?.GrabFocus();
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
                SettingsHandler.CycleFocusInContainer(biosSelectorContainer != null && biosSelectorContainer.Visible ? biosSelectorContainer : startMenu, -1);
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (@event.IsActionPressed("ui_down", true) || @event.IsActionPressed("MoveDown"))
            {
                SettingsHandler.CycleFocusInContainer(biosSelectorContainer != null && biosSelectorContainer.Visible ? biosSelectorContainer : startMenu, 1);
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

            bool isFocusInTree = (focusOwner != null && settingsSectionsTree.IsAncestorOf(focusOwner));
            bool isFocusInOptions = (focusOwner != null && sectionOptionsContainer.IsAncestorOf(focusOwner));

            if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                if (isFocusInOptions)
                {
                    SettingsHandler.CycleFocusInContainer(settingsSectionsTree, 0);
                }
                else
                {
                    SettingsHandler.ToggleSettingsMenu();
                }
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("Select"))
            {
                if (isFocusInTree)
                {
                    var visibleForm = SettingsHandler.GetVisibleSettingsForm();
                    if (visibleForm != null)
                    {
                        var firstFocusable = SettingsHandler.FindFirstFocusable(visibleForm);
                        if (firstFocusable != null)
                        {
                            firstFocusable.GrabFocus();
                            GetViewport().SetInputAsHandled();
                            return;
                        }
                    }
                }
                else if (isFocusInOptions)
                {
                    if (focusOwner is SettingsListEntry entry)
                    {
                        entry.InteractWithWidget();
                    }
                    else if (focusOwner is BaseButton btn)
                    {
                        if (btn is CheckButton checkBtn)
                        {
                            checkBtn.ButtonPressed = !checkBtn.ButtonPressed;
                            checkBtn.EmitSignal(BaseButton.SignalName.Toggled, checkBtn.ButtonPressed);
                        }
                        else if (btn is OptionButton optBtn)
                        {
                            optBtn.ShowPopup();
                        }
                        else
                        {
                            btn.EmitSignal(BaseButton.SignalName.Pressed);
                        }
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            else if (@event.IsActionPressed("ui_up", true) || @event.IsActionPressed("MoveUp") || 
                     @event.IsActionPressed("ui_down", true) || @event.IsActionPressed("MoveDown"))
            {
                int direction = (@event.IsActionPressed("ui_down", true) || @event.IsActionPressed("MoveDown")) ? 1 : -1;
                
                if (isFocusInTree)
                {
                    SettingsHandler.CycleFocusInContainer(settingsSectionsTree, direction);
                }
                else if (isFocusInOptions)
                {
                    var visibleForm = SettingsHandler.GetVisibleSettingsForm();
                    if (visibleForm != null)
                    {
                        SettingsHandler.CycleFocusInContainer(visibleForm, direction);
                    }
                }
                
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (@event.IsActionPressed("ui_left", true) || @event.IsActionPressed("ui_right", true))
            {
                if (isFocusInOptions)
                {
                    int direction = @event.IsAction("ui_right") ? 1 : -1;
                    if (focusOwner is SettingsListEntry entry)
                    {
                        entry.CycleWidget(direction);
                    }
                    else
                    {
                        SettingsHandler.CycleFocusedOption(direction);
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            return; 
        }

        if(@event.IsActionPressed("CylceSystemUp") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            if (systemJumpPopup != null && systemJumpPopup.Visible) return;
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
        
        if (@event.IsActionPressed("CycleSystemDown") && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            if (systemJumpPopup != null && systemJumpPopup.Visible) return;
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

        // Footer-button actions are only consumed when driven by a controller. In keyboard/mouse
        // mode the footer buttons are clicked instead, which keeps their bound keys (e.g. Backspace
        // for ToggleDownloadsPage, Enter for Select) free for fuzzy search and normal typing.
        if (IsControllerEvent(@event))
        {
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
                DownloadHandler.SwapLists();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("CancelDownload"))
            {
                if (downloadsListContainer != null && downloadsListContainer.Visible)
                {
                    DownloadHandler.OnCancelDownloadPressed();
                    GetViewport().SetInputAsHandled();
                }
                return;
            }
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

    // True when the input originated from a controller (button or stick/trigger), as opposed to
    // keyboard or mouse. Used to gate footer-button shortcuts to controller-only.
    private static bool IsControllerEvent(InputEvent @event)
    {
        return @event is InputEventJoypadButton || @event is InputEventJoypadMotion;
    }

    // Focus-follows-mouse for keyboard/mouse users: whichever focusable widget the cursor moves
    // over takes focus. The nearest focusable ancestor is used, so hovering a settings entry's
    // inner widget still focuses the entry. The game carousel is handled at the list level since
    // it drives its own index-based selection rather than per-entry focus.
    private void UpdateMouseFocus()
    {
        var viewport = GetViewport();
        var hovered = viewport.GuiGetHoveredControl();
        if (hovered == null) return;

        Control focusable = hovered;
        while (focusable != null && focusable.FocusMode != Control.FocusModeEnum.All)
        {
            focusable = focusable.GetParentOrNull<Control>();
        }

        if (focusable == null || !focusable.IsVisibleInTree()) return;

        if (gameList != null && (focusable == gameList || gameList.IsAncestorOf(focusable)))
        {
            // Don't pull focus onto the game list while a menu/popup owns the screen.
            if (IsAnyMenuOpen()) return;

            focusable = gameList;
        }

        if (focusable != viewport.GuiGetFocusOwner())
        {
            focusable.GrabFocus();
        }
    }

    // True when the start menu, settings, downloads page, or system-jump popup is showing.
    private bool IsAnyMenuOpen()
    {
        return (startMenuRoot != null && startMenuRoot.Visible)
            || (settingsMenuContainer != null && settingsMenuContainer.Visible)
            || (downloadsListContainer != null && downloadsListContainer.Visible)
            || (systemJumpPopup != null && systemJumpPopup.Visible);
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
                if (fuzzySearchPopup != null) fuzzySearchPopup.Visible = false;
            }
            
            if (GameListHandler.isFuzzySearchDirty && currentTime - GameListHandler.lastKeystrokeTime > 400)
            {
                GameListHandler.isFuzzySearchDirty = false;

                // Skip searching on an empty buffer; Contains("") matches everything and would
                // jump to the first game when the user backspaces the query away.
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
                    if (!fuzzySearchPopup.Visible) fuzzySearchPopup.Visible = true;
                    if (fuzzySearchLabel != null) fuzzySearchLabel.Text = "Search: " + GameListHandler.fuzzySearchBuffer;
                }
                else
                {
                    fuzzySearchPopup.Visible = false;
                }
            }
        }
    }



    private void OpenSystemJumpPopup()
    {
        if (systemJumpPopup != null && !systemJumpPopup.Visible && (downloadsListContainer == null || !downloadsListContainer.Visible))
        {
            systemJumpPopup.Populate(GameListHandler.gameSystems, GameListHandler.currentGameSystemIndex);
            systemJumpPopup.Visible = true;
            systemJumpPopup.FocusSystem(GameListHandler.currentGameSystemIndex >= 0 ? GameListHandler.currentGameSystemIndex : 0);
        }
    }
}
