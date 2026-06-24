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
    //Header
    [ExportGroup("Header")] 
    [Export] private MarginContainer headerContainer;
    [Export] private Label platformLabel;
    [Export] private TextureRect platformIcon;
    [Export] private OptionButton firmwareSelector;
    
    

    public List<GameSystem> gameSystems = new List<GameSystem>();
    public Dictionary<int, List<Game>> games { get; set; } = new Dictionary<int, List<Game>>();
    private List<Game> currentlyShownGames = new List<Game>();
    public int currentGameSystemIndex;
    public Game currentlySelectedGame; 
    
    //Game list / Details Panel 
    [ExportGroup("GameList")] 
    [Export] private Control gameList;
    [Export] private PackedScene gameListEntryScene;
    
    [ExportGroup("DetailsPanel")]
    [Export] private VBoxContainer detailsPanelContainer;
    [Export] private TextureRect gameCover;
    [Export] private TextureRect gameMarquee;
    [Export] private Label gameTitle;
    [Export] private RichTextLabel gameDescription;
    [Export] private Button playDownloadButton;
    [Export] private Button deleteButton;

    
    //Downloads List
    [ExportGroup("DowloadsList")]
    [Export] private MarginContainer downloadsListContainer;
    [Export] private DownloadProgressUI downloadProgressUI;
        
    //Footer
    [ExportGroup("FooterButtons")]
    [Export] private MarginContainer footerButtonsContainer;
    [Export] private Button LaunchEmulatorButton;
    [Export] private Button downloadsPageToggle;
    [Export] private Button refreshGamesButton;
    

    //Global access to other systems
    private AppInstance appInstance;
    private ImageTexture _placeholderTexture;
    private VBoxContainer _mainVBoxContainer;
    
    // Settings Menu
    private MarginContainer settingsMenuContainer;
    private Tree settingsSectionsTree;
    private VBoxContainer sectionOptionsContainer;
    
    public override void _Ready()
    {
        var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        whiteImage.Fill(Colors.White);
        _placeholderTexture = ImageTexture.CreateFromImage(whiteImage);
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        _mainVBoxContainer = GetNode<VBoxContainer>("Background/VBoxContainer");
        appInstance.downloadManager.DownloadCompleted += OnDownloadCompleted; 
        
        appInstance.emulatorManager.mainScene = this;
        appInstance.assetManager.AssetDownloaded += OnAssetDownloaded;
        
        GetCache();
        SelectSystemByIndex(0);
        SetupGameList();
        SetupDownloadsList();
        SetupButtonBindings();
        SetupFirmwareSelector();
        
        // Setup Settings
        settingsMenuContainer = GetNodeOrNull<MarginContainer>("Background/VBoxContainer/SettingsMenuContainer");
        if (settingsMenuContainer != null)
        {
            settingsSectionsTree = settingsMenuContainer.GetNodeOrNull<Tree>("HBoxContainer/SettingsSections");
            sectionOptionsContainer = settingsMenuContainer.GetNodeOrNull<VBoxContainer>("HBoxContainer/SectionOptions");
            settingsMenuContainer.Visible = false;
            SetupSettingsTree();
        }
    }

    private void OnAssetDownloaded(int gameId, string assetType)
    {
        if (currentlySelectedGame != null && currentlySelectedGame.Id == gameId)
        {
            ShowGameDetails(currentlySelectedGame);
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ToggleSettings"))
        {
            if (settingsMenuContainer != null)
            {
                var gamesListContainer = gameList?.GetParent()?.GetParent<Control>();
                if (settingsMenuContainer.Visible)
                {
                    settingsMenuContainer.Visible = false;
                    if (gamesListContainer != null) gamesListContainer.Visible = true;
                    gameList?.GrabFocus();
                }
                else
                {
                    settingsMenuContainer.Visible = true;
                    if (gamesListContainer != null) gamesListContainer.Visible = false;
                    settingsSectionsTree?.GrabFocus();
                }
                UpdateHeaderLabel();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (settingsMenuContainer != null && settingsMenuContainer.Visible)
        {
            if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("Back"))
            {
                var gamesListContainer = gameList?.GetParent()?.GetParent<Control>();
                settingsMenuContainer.Visible = false;
                if (gamesListContainer != null) gamesListContainer.Visible = true;
                UpdateHeaderLabel();
                gameList?.GrabFocus();
                GetViewport().SetInputAsHandled();
            }
            // Block other custom input processing while settings is open
            return; 
        }

        if(@event.IsActionPressed("CylceSystemUp"))
        {
            if (downloadsListContainer != null && downloadsListContainer.Visible) return;
            CycleSelectedSystemNext();
            return;
        }
        
        if (@event.IsActionPressed("CycleSystemDown"))
        {
            if (downloadsListContainer != null && downloadsListContainer.Visible) return;
            CycleSelectedSystemLast();
            return;
        }
    }

    public void SetupButtonBindings()
    {
        if (playDownloadButton != null)
        {
            playDownloadButton.Pressed += OnPlayDownloadButtonPressed; 
        }

        if (downloadsPageToggle != null)
        {
            downloadsPageToggle.Pressed += SwapLists;
        }

        if (deleteButton != null)
        {
            deleteButton.Pressed += OnDeleteButtonPressed;
        }
        
        if (refreshGamesButton != null)
        {
            refreshGamesButton.Pressed += appInstance.cacheManager.rebuildGameCache;
        }

        if (LaunchEmulatorButton != null)
        {
            LaunchEmulatorButton.Pressed += OnLaunchEmulatorButtonPressed;
        }
    }
    
    public void GetCache()
    {
        gameSystems = appInstance.dataBus.systems;
        games = appInstance.dataBus.gameCache;
        
    }
    
    public void SetupGameList()
    {
        if (gameList != null)
        {
            gameList.Connect("ItemSelected", Callable.From<long>(OnGameSelected));
            gameList.Connect("ItemFocused", Callable.From<long>(OnGameSelected));
        }
    }
    
    private void SetupDownloadsList()
    {
        if (downloadsListContainer != null)
        {
            downloadsListContainer.Visible = false;
        }
    }
    
    private void SetupFirmwareSelector()
    {
        if (firmwareSelector != null)
        {
            firmwareSelector.ItemSelected += OnFirmwareSelected;
        }
    }

    private void SwapLists()
    {
        if (downloadsListContainer != null && gameList != null)
        {
            downloadsListContainer.Visible = !downloadsListContainer.Visible;
            
            // Hide the entire games list container (game list + details panel)
            var gamesListContainer = gameList.GetParent().GetParent<Control>();
            if (gamesListContainer != null)
            {
                gamesListContainer.Visible = !downloadsListContainer.Visible;
            }
            
            // Restore focus to the game list when returning to games view
            if (!downloadsListContainer.Visible)
            {
                gameList.GrabFocus();
            }
            
            UpdateHeaderLabel();
        }
    }

    private bool _isTransitioningSystem = false;
    
    private async void TransitionToSystem(int targetIndex)
    {
        if (_isTransitioningSystem) return;
        _isTransitioningSystem = true;
        
        float duration = 0.2f;

        // Fade out
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

        // Hard enforce 0 opacity before loading
        var glModOut = gameList.Modulate; glModOut.A = 0.0f; gameList.Modulate = glModOut;
        if (platformIcon != null) { var piMod = platformIcon.Modulate; piMod.A = 0.0f; platformIcon.Modulate = piMod; }
        if (platformLabel != null) { var plMod = platformLabel.Modulate; plMod.A = 0.0f; platformLabel.Modulate = plMod; }
        if (detailsPanelContainer != null) { var dpcMod = detailsPanelContainer.Modulate; dpcMod.A = 0.0f; detailsPanelContainer.Modulate = dpcMod; }

        // Load the next system
        SelectSystemByIndex(targetIndex);

        // Give the UI one frame to actually layout the new list and covers before fading in
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame"); // Two frames for good measure

        // Fade in
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
        
        // Failsafe to guarantee 100% opacity
        var glModIn = gameList.Modulate; glModIn.A = 1.0f; gameList.Modulate = glModIn;
        if (platformIcon != null) { var piMod = platformIcon.Modulate; piMod.A = 1.0f; platformIcon.Modulate = piMod; }
        if (platformLabel != null) { var plMod = platformLabel.Modulate; plMod.A = 1.0f; platformLabel.Modulate = plMod; }
        if (detailsPanelContainer != null) { var dpcMod = detailsPanelContainer.Modulate; dpcMod.A = 1.0f; detailsPanelContainer.Modulate = dpcMod; }
        
        _isTransitioningSystem = false;
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
        if (index < 0 || index >= gameSystems.Count) return;

        currentGameSystemIndex = index;
        var selectedSystem = gameSystems[index];
        
        if (platformLabel!= null)
        {
            platformLabel.Text = selectedSystem.Name;
        }

        if (platformIcon != null)
        {
            if (!string.IsNullOrEmpty(selectedSystem.IgdbSlug))
            {
                var texture = FindPlatformIcon(selectedSystem.IgdbSlug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
                platformIcon.Texture = texture;
            }
            else
            {
                var texture = FindPlatformIcon(selectedSystem.Slug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
                platformIcon.Texture = texture;
            }
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
        if (gameList == null) return;

        currentlySelectedGame = null;

        if (games.TryGetValue(system.Id, out List<Game> cachedGames))
        {
            currentlyShownGames = cachedGames;
            RefreshGameList();

            if (currentlyShownGames.Any())
            {
                OnGameSelected(0L);
                // Restore focus to the game list when returning to games view
                if (!downloadsListContainer.Visible)
                {
                    gameList.GrabFocus();
                }
            
                UpdateHeaderLabel();
            }
        }
        else
        {
            currentlyShownGames = new List<Game>();
            GD.Print($"No games found in cache for system {system.Name}");
        }
        
        UpdateFooterButtons();
        PopulateFirmwareSelector(system);
    }
    
    private void PopulateFirmwareSelector(GameSystem system)
    {
        if (firmwareSelector == null)
        {
            GD.Print("Firmware selector is null");
            return;
        }

        firmwareSelector.Clear();
        GD.Print($"Populating firmware selector for {system.Name}");
        var hasFirmwares = system.AvailableFirmwares != null && system.AvailableFirmwares.Any();
        firmwareSelector.Visible = hasFirmwares;
        GD.Print($"Firmware selector visible: {hasFirmwares}");

        if (hasFirmwares)
        {
            GD.Print($"{system.AvailableFirmwares.Count} available firmwares found.");
            foreach (var firmware in system.AvailableFirmwares)
            {
                firmwareSelector.AddItem(firmware.FileName);
            }

            var preferredFirmwareIndex = system.AvailableFirmwares.FindIndex(f => f.FullPath == system.PrefferedFirmware);
            if (preferredFirmwareIndex != -1)
            {
                firmwareSelector.Select(preferredFirmwareIndex);
                GD.Print($"Selected preferred firmware: {Path.GetFullPath(system.PrefferedFirmware)}");
            }
        }
    }
    
    private void OnFirmwareSelected(long index)
    {
        var system = gameSystems[currentGameSystemIndex];
        if (index >= 0 && index < system.AvailableFirmwares.Count)
        {
            system.PrefferedFirmware = system.AvailableFirmwares[(int)index].FullPath;
            GD.Print($"Selected preferred firmware: {system.PrefferedFirmware}");
        }
    }

    public void RefreshGameList()
    {
        if (gameList == null) return;

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
            
            if (gameListEntryScene == null) continue;

            TextureRect entry = gameListEntryScene.Instantiate<TextureRect>();
            entry.FocusMode = FocusModeEnum.All;

            // Set up focus highlight
            Panel focusPanel = new Panel();
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

            // Input and Focus are now handled entirely by the parent VerticalCarousel.

            // Set up fallback covers for the list item
            entry.Texture = _placeholderTexture;
            Label titleLabel = entry.GetNode<Label>("TitleLabel");
            titleLabel.Text = game.Name;
            titleLabel.AddThemeColorOverride("font_color", Colors.Black);

            bool textureLoaded = false;

            void TryLoadImage()
            {
                if (textureLoaded) return;
                
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
                if (!string.IsNullOrEmpty(path2d)) loadedTex = SafeLoadTexture(path2d);
                if (loadedTex == null && !string.IsNullOrEmpty(path3d)) loadedTex = SafeLoadTexture(path3d);
                if (loadedTex == null && !string.IsNullOrEmpty(pathFallback)) loadedTex = SafeLoadTexture(pathFallback);

                if (loadedTex != null)
                {
                    entry.Texture = loadedTex;
                    titleLabel.Visible = false;
                    textureLoaded = true;
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
                        gameList.Call("UpdateLayout", false);
                    }
                }
            }

            void TryUnloadImage()
            {
                if (!textureLoaded) return;

                entry.Texture = _placeholderTexture;
                titleLabel.Visible = true;
                textureLoaded = false;
            }

            VisibleOnScreenNotifier2D visibilityNotifier = new VisibleOnScreenNotifier2D();
            visibilityNotifier.Rect = new Rect2(0, -600, 200, 1400); // Expanded rect to load items before they become visible
            visibilityNotifier.ScreenEntered += () => 
            {
                TryLoadImage();
            };
            visibilityNotifier.ScreenExited += () =>
            {
                TryUnloadImage();
            };
            entry.AddChild(visibilityNotifier);

            // Subscribe to asset downloads in background
            AssetManager.AssetDownloadedEventHandler onAssetDownloaded = null;
            onAssetDownloaded = (downloadedGameId, assetType) =>
            {
                if (downloadedGameId == game.Id && (assetType == "box3d" || assetType == "box2d"))
                {
                    textureLoaded = false;
                    if (visibilityNotifier.IsOnScreen())
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
            gameList.Call("Refresh");
        }
    }

    private bool CheckIfGameIsDownloaded(Game game)
    {
        if (game.Files == null || !game.Files.Any()) return false;
        
        string fileName = game.Files[0].FileName;
        string fullPath = appInstance.configManager.RomsPath.PathJoin(game.System.Slug).PathJoin(fileName);
        return Godot.FileAccess.FileExists(fullPath);
        
    }

    private void OnGameSelected(long index)
    {
        if (index < 0 || index >= currentlyShownGames.Count) return;
        
        currentlySelectedGame = currentlyShownGames[(int)index];
        ShowGameDetails(currentlySelectedGame);
    }

    private void ShowGameDetails(Game game)
    {
        if (detailsPanelContainer == null) return;
        
        if (gameTitle != null) 
        {
            gameTitle.Text = game.Name;
            gameTitle.Visible = true;
        }
        if (gameDescription != null) gameDescription.Text = game.Description;
        
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
                if (gameTitle != null) gameTitle.Visible = false;
            }
            else
            {
                if (gameTitle != null) gameTitle.Visible = true;
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
            if (!string.IsNullOrEmpty(path2d)) loadedTex = SafeLoadTexture(path2d);
            if (loadedTex == null && !string.IsNullOrEmpty(path3d)) loadedTex = SafeLoadTexture(path3d);
            if (loadedTex == null && !string.IsNullOrEmpty(pathFallback)) loadedTex = SafeLoadTexture(pathFallback);

            gameCover.Texture = loadedTex;
        }

        TextureRect backgroundRect = GetNodeOrNull<TextureRect>("Background");
        if (backgroundRect != null)
        {
            string assetsPath = appInstance.configManager.AssetsPath;
            string pathScreenshot = System.IO.Path.Combine(assetsPath, "screenshots", $"{game.Id}.jpg");
            
            if (Godot.FileAccess.FileExists(pathScreenshot))
            {
                backgroundRect.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(pathScreenshot));
                // Optional: dim the screenshot slightly so the UI remains readable
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

        if (playDownloadButton != null)
        {
            playDownloadButton.Visible = true;
        }

        else
        {
            return;
        }

        if (isGameDownloadedLocally)
        {
            if (appInstance.emulatorManager.IsEmulatorInstalled(appInstance.emulatorManager.GetMappedEmulator(game.PlatformSlug)))
            {
                playDownloadButton.Text = "Play";
                playDownloadButton.Disabled = false; 
            }

            else
            {
                playDownloadButton.Text = "Install Emulator";
            }
        }

        else
        {
            playDownloadButton.Text = "Download";
            playDownloadButton.Disabled = false;
        }

        if (deleteButton != null)
        {
            deleteButton.Visible = isGameDownloadedLocally;
        }
    }

    public void UpdateFooterButtons()
    {
        if (LaunchEmulatorButton != null)
        {
            string emulatorName = gameSystems[currentGameSystemIndex].MappedEmulator;
            
            if (appInstance.emulatorManager.IsEmulatorInstalled(emulatorName))
            {
                LaunchEmulatorButton.Text = "Launch Emulator";
            }
            else
            {
                LaunchEmulatorButton.Text = "Install Emulator";
            }
        }
    }
    

    
    private void OnPlayDownloadButtonPressed()
    {
        if (currentlySelectedGame == null) return;
        
        string emulatorName = appInstance.emulatorManager.GetMappedEmulator(currentlySelectedGame.PlatformSlug);

        if (playDownloadButton.Text == "Install Emulator")
        {
            playDownloadButton.Disabled = true; 
            appInstance.emulatorManager.InstallEmulator(emulatorName); 
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

    private void OnLaunchEmulatorButtonPressed()
    {
        if (LaunchEmulatorButton != null)
        {
            if (LaunchEmulatorButton.Text == "Install Emulator")
            {
                appInstance.emulatorManager.InstallEmulator(gameSystems[currentGameSystemIndex].MappedEmulator);
            }

            else
            {
                appInstance.emulatorManager.LaunchEmulatorWithoutGame(gameSystems[currentGameSystemIndex].MappedEmulator);
            }
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

        string tempZipName = game.Files[0].FileName;
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
        
    }
    
    private void HandleRomDownloadCompletion(string tempZipPath, Game game)
    {
        string fileName = tempZipPath.GetFile();
        if (downloadProgressUI != null)
        {
            downloadProgressUI.SetDownloadStatus(fileName, "Extracting...");
        }

        GD.Print($"Download complete. Starting extraction for: {tempZipPath}");
        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(ProjectSettings.GlobalizePath(tempZipPath)))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("roms/") && !entry.FullName.EndsWith("/"))
                    {
                        string finalFileName = entry.Name;
                        string finalDir = appInstance.configManager.RomsPath.PathJoin(game.System.Slug);
                        string finalPath = finalDir.PathJoin(finalFileName);

                        if (!DirAccess.DirExistsAbsolute(finalDir))
                        {
                            DirAccess.MakeDirRecursiveAbsolute(finalDir);
                        }
                        
                        entry.ExtractToFile(ProjectSettings.GlobalizePath(finalPath), true);
                        GD.Print($"Extracted {entry.FullName} to {finalPath}");
                        
                        UpdateDetailsPanelButtons(game);
                        RefreshGameList();
                        break; 
                    }
                }
            }
        }
        catch (Exception e)
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
        UpdateFooterButtons();
    }
    
    private void OnDownloadCompleted(string fileName, bool success)
    {
        RefreshGameList();
    }
    
    private void OnDeleteButtonPressed()
    {
        if (currentlySelectedGame == null) return;
        DeleteLocalGame(currentlySelectedGame);
        RefreshGameList();
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
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
        try
        {
            byte[] fileData = System.IO.File.ReadAllBytes(path);
            var img = new Image();
            
            Error err = img.LoadPngFromBuffer(fileData);
            if (err != Error.Ok) err = img.LoadWebpFromBuffer(fileData);
            if (err != Error.Ok) err = img.LoadJpgFromBuffer(fileData);
            
            if (err == Error.Ok && img != null && !img.IsEmpty())
            {
                return ImageTexture.CreateFromImage(img);
            }
            else
            {
                GD.PrintErr($"SafeLoadTexture failed for {path}: Not a valid PNG, WebP, or JPG. Error code: {err}");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"SafeLoadTexture failed for {path}: {e.Message}");
        }
        return null;
    }

    private void SetupSettingsTree()
    {
        if (settingsSectionsTree == null) return;
        
        settingsSectionsTree.Clear();
        TreeItem root = settingsSectionsTree.CreateItem();
        settingsSectionsTree.HideRoot = true;

        TreeItem general = settingsSectionsTree.CreateItem(root);
        general.SetText(0, "General Settings");
        
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

            // Generate UI Form for this emulator
            GenerateEmulatorSettingsForm(slug, meta);
        }

        settingsSectionsTree.ItemSelected += OnSettingsTreeItemSelected;
    }

    private void GenerateEmulatorSettingsForm(string slug, EmulatorMeta meta)
    {
        if (sectionOptionsContainer == null || meta.SettingsFields == null) return;

        // Check if form already exists
        string nodeName = meta.Name.Replace(" ", "");
        if (sectionOptionsContainer.HasNode(nodeName)) return;

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        
        VBoxContainer vbox = new VBoxContainer();
        formContainer.AddChild(vbox);
        sectionOptionsContainer.AddChild(formContainer);

        var userSettings = appInstance.emulatorManager.LoadEmulatorSettings(slug);

        foreach (var field in meta.SettingsFields)
        {
            if (string.IsNullOrEmpty(field.Id)) continue;
            
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
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.True) val = true;
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.False) val = false;
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
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String) val = element.GetString();
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
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String) val = element.GetString();

                int idx = 0;
                int selectedIdx = 0;
                if (field.Options != null)
                {
                    foreach (var option in field.Options)
                    {
                        optionButton.AddItem(option.Key, idx);
                        optionButton.SetItemMetadata(idx, option.Value);
                        if (option.Value == val) selectedIdx = idx;
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
        if (settingsSectionsTree == null || sectionOptionsContainer == null) return;
        
        TreeItem selected = settingsSectionsTree.GetSelected();
        if (selected == null) return;

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
        if (platformLabel == null) return;

        if (settingsMenuContainer != null && settingsMenuContainer.Visible)
        {
            platformLabel.Text = "Settings";
            if (platformIcon != null) platformIcon.Visible = false;
        }
        else if (downloadsListContainer != null && downloadsListContainer.Visible)
        {
            platformLabel.Text = "Downloads";
            if (platformIcon != null) platformIcon.Visible = false;
        }
        else if (gameSystems != null && currentGameSystemIndex >= 0 && currentGameSystemIndex < gameSystems.Count)
        {
            platformLabel.Text = gameSystems[currentGameSystemIndex].Name;
            if (platformIcon != null) platformIcon.Visible = true;
        }
    }
}
