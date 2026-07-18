using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainSceneGameListHandler
{
    private MainScene _mainScene;
    private AppInstance _appInstance;

    public List<GameSystem> gameSystems = new List<GameSystem>();
    public Dictionary<int, List<Game>> games { get; set; } = new Dictionary<int, List<Game>>();
    public List<Game> currentlyShownGames = null;
    public bool showOnlyInstalledGames = false;
    public int currentGameSystemIndex;
    public Game currentlySelectedGame; 
    public bool isTransitioningSystem = false;
    public bool IsFilterTransitioning { get; private set; } = false;
    private bool preFadedForQuickSwitch = false;
    public string fuzzySearchBuffer = "";
    public ulong lastKeystrokeTime = 0;
    public bool isFuzzySearchDirty = false;

    public MainSceneGameListHandler(MainScene mainScene, AppInstance appInstance)
    {
        _mainScene = mainScene;
        _appInstance = appInstance;
        _appInstance.downloadManager.DownloadProgressUpdated += OnDownloadProgressUpdated;
        _appInstance.assetManager.AssetDownloaded += OnAssetDownloaded;
    }

    private void OnAssetDownloaded(int gameId, string assetType)
    {
        GD.Print($"[MainSceneGameListHandler] OnAssetDownloaded: GameId={gameId}, AssetType={assetType}");
        if (currentlySelectedGame != null && currentlySelectedGame.Id == gameId)
        {
            if (assetType == "screenshot" || assetType == "marquee" || assetType == "box3d" || assetType == "box2d")
            {
                GD.Print($"[MainSceneGameListHandler] Updating Game Details UI for GameId={gameId}");
                UpdateGameDetailsUI(currentlySelectedGame);
            }
        }
    }

    // Immediately fades the current system's content out when the user starts thumbing through
    // systems with the bumpers, so the stale list doesn't linger until the debounce settles.
    // TransitionToSystem then skips its own fade-out and only swaps + fades the new system in.
    public void BeginQuickSwitchFade()
    {
        if (_mainScene.gameList == null) return;
        if (isTransitioningSystem || preFadedForQuickSwitch) return;

        preFadedForQuickSwitch = true;

        float duration = 0.15f;
        if (_mainScene.HoverOverlay != null) _mainScene.HoverOverlay.ForceCancelPopup();

        Tween fadeOutTween = _mainScene.CreateTween();
        Color glColorOut = _mainScene.gameList.Modulate; glColorOut.A = 0.0f;
        fadeOutTween.TweenProperty(_mainScene.gameList, "modulate", glColorOut, duration);
        if (_mainScene.detailsPanelContainer != null)
        {
            Color dpcColorOut = _mainScene.detailsPanelContainer.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(_mainScene.detailsPanelContainer, "modulate", dpcColorOut, duration);
        }
    }

    public async void TransitionToSystem(int targetIndex)
    {
        if (isTransitioningSystem)
        {
            return;
        }

        isTransitioningSystem = true;
        
        float duration = 0.2f;

        // Skip the fade-out when quick-switching already faded the content out.
        if (!preFadedForQuickSwitch)
        {
        Tween fadeOutTween = _mainScene.CreateTween();

        Color glColorOut = _mainScene.gameList.Modulate; glColorOut.A = 0.0f;
        fadeOutTween.TweenProperty(_mainScene.gameList, "modulate", glColorOut, duration);



        if (_mainScene.detailsPanelContainer != null) {
            Color dpcColorOut = _mainScene.detailsPanelContainer.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(_mainScene.detailsPanelContainer, "modulate", dpcColorOut, duration);
        }

        if (_mainScene.HoverOverlay != null) {
            Color hoColorOut = _mainScene.HoverOverlay.Modulate; hoColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(_mainScene.HoverOverlay, "modulate", hoColorOut, duration);
        }
            
        await _mainScene.ToSignal(fadeOutTween, Tween.SignalName.Finished);
        }

        preFadedForQuickSwitch = false;

        var glModOut = _mainScene.gameList.Modulate; glModOut.A = 0.0f; _mainScene.gameList.Modulate = glModOut;



        if (_mainScene.detailsPanelContainer != null) { var dpcMod = _mainScene.detailsPanelContainer.Modulate; dpcMod.A = 0.0f; _mainScene.detailsPanelContainer.Modulate = dpcMod; }

        if (_mainScene.HoverOverlay != null) { 
            _mainScene.HoverOverlay.ForceCancelPopup();
            var hoMod = _mainScene.HoverOverlay.Modulate; hoMod.A = 1.0f; _mainScene.HoverOverlay.Modulate = hoMod;
        }

        DoSelectSystemByIndex(targetIndex);

        await _mainScene.ToSignal(_mainScene.GetTree(), "process_frame");
        await _mainScene.ToSignal(_mainScene.GetTree(), "process_frame"); // Two frames for good measure

        Tween fadeInTween = _mainScene.CreateTween();
        
        Color glColorIn = _mainScene.gameList.Modulate; glColorIn.A = 1.0f;
        fadeInTween.TweenProperty(_mainScene.gameList, "modulate", glColorIn, duration);
        


        if (_mainScene.detailsPanelContainer != null) {
            Color dpcColorIn = _mainScene.detailsPanelContainer.Modulate; dpcColorIn.A = 1.0f;
            fadeInTween.Parallel().TweenProperty(_mainScene.detailsPanelContainer, "modulate", dpcColorIn, duration);
        }

        await _mainScene.ToSignal(fadeInTween, Tween.SignalName.Finished);

        var glModIn = _mainScene.gameList.Modulate; glModIn.A = 1.0f; _mainScene.gameList.Modulate = glModIn;



        if (_mainScene.detailsPanelContainer != null) { var dpcMod = _mainScene.detailsPanelContainer.Modulate; dpcMod.A = 1.0f; _mainScene.detailsPanelContainer.Modulate = dpcMod; }
        
        isTransitioningSystem = false;
    }

    public void SelectSystemByIndex(int index)
    {
        if (index < 0 || index >= gameSystems.Count)
        {
            return;
        }

        if (index == currentGameSystemIndex) 
        {
            // Just initialize if it's the first run and systems aren't populated yet
            if (currentlyShownGames == null)
            {
                DoSelectSystemByIndex(index);
            }
            return;
        }
        
        TransitionToSystem(index);
    }
    
    private void DoSelectSystemByIndex(int index)
    {
        if (index < 0 || index >= gameSystems.Count)
        {
            return;
        }

        currentGameSystemIndex = index;
        var selectedSystem = gameSystems[index];
        
        _mainScene.UpdateHeaderLabel();
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
    
    public void OnSystemSelected(GameSystem system)
    {
        if (_mainScene.gameList == null)
        {
            return;
        }

        currentlySelectedGame = null;

        ApplyFiltersAndRefresh();

        if (currentlyShownGames.Any())
        {
            OnGameSelected(0L);

            if (_mainScene.downloadsListContainer != null && !_mainScene.downloadsListContainer.Visible && 
                (_mainScene.settingsMenuContainer == null || !_mainScene.settingsMenuContainer.Visible))
            {
                _mainScene.gameList.GrabFocus();
            }
        
            _mainScene.UpdateHeaderLabel();
        }
        else
        {
            GD.Print($"No games found in cache for system {system.Name}");
        }
    }
    
    public void ApplyFiltersAndRefresh()
    {
        if (currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
        {
            return;
        }

        var system = gameSystems[currentGameSystemIndex];
        
        if (games.TryGetValue(system.Id, out List<Game> cachedGames))
        {
            if (_appInstance.configManager.HideGamesWithoutBoxArt)
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

    // Fades the game list (and details panel) out, re-applies the filter, then fades back in.
    // Used when toggling between "All Games" and "Installed" so the list swap isn't a hard cut.
    public async void ApplyFiltersWithFade()
    {
        if (_mainScene.gameList == null)
        {
            ApplyFiltersAndRefresh();
            return;
        }

        IsFilterTransitioning = true;
        float duration = 0.15f;

        if (_mainScene.HoverOverlay != null) _mainScene.HoverOverlay.ForceCancelPopup();

        Tween fadeOut = _mainScene.CreateTween();
        Color glOut = _mainScene.gameList.Modulate; glOut.A = 0.0f;
        fadeOut.TweenProperty(_mainScene.gameList, "modulate", glOut, duration);
        if (_mainScene.detailsPanelContainer != null)
        {
            Color dpcOut = _mainScene.detailsPanelContainer.Modulate; dpcOut.A = 0.0f;
            fadeOut.Parallel().TweenProperty(_mainScene.detailsPanelContainer, "modulate", dpcOut, duration);
        }
        await _mainScene.ToSignal(fadeOut, Tween.SignalName.Finished);

        ApplyFiltersAndRefresh();

        await _mainScene.ToSignal(_mainScene.GetTree(), "process_frame");
        await _mainScene.ToSignal(_mainScene.GetTree(), "process_frame");

        Tween fadeIn = _mainScene.CreateTween();
        Color glIn = _mainScene.gameList.Modulate; glIn.A = 1.0f;
        fadeIn.TweenProperty(_mainScene.gameList, "modulate", glIn, duration);
        if (_mainScene.detailsPanelContainer != null)
        {
            Color dpcIn = _mainScene.detailsPanelContainer.Modulate; dpcIn.A = 1.0f;
            fadeIn.Parallel().TweenProperty(_mainScene.detailsPanelContainer, "modulate", dpcIn, duration);
        }
        await _mainScene.ToSignal(fadeIn, Tween.SignalName.Finished);

        _mainScene.gameList.Modulate = new Color(_mainScene.gameList.Modulate, 1.0f);
        if (_mainScene.detailsPanelContainer != null)
        {
            _mainScene.detailsPanelContainer.Modulate = new Color(_mainScene.detailsPanelContainer.Modulate, 1.0f);
        }

        IsFilterTransitioning = false;
    }

    public void RefreshGameList()
    {
        if (_mainScene.gameList == null)
        {
            return;
        }

        foreach (Node child in _mainScene.gameList.GetChildren())
        {
            _mainScene.gameList.RemoveChild(child);
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
            
            if (_mainScene.gameListEntryScene == null)
            {
                continue;
            }

            TextureRect entry = _mainScene.gameListEntryScene.Instantiate<TextureRect>();
            entry.FocusMode = Control.FocusModeEnum.All;

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
                BorderColor = new Color(1f, 1f, 1f, 0.5f),
                DrawCenter = false
            };
            focusPanel.AddThemeStyleboxOverride("panel", focusStyle);
            focusPanel.Visible = false;
            entry.AddChild(focusPanel);

            entry.Texture = _mainScene.placeholderTexture;
            Label titleLabel = entry.GetNode<Label>("TitleLabel");
            titleLabel.Text = game.Name;
            titleLabel.AddThemeColorOverride("font_color", Colors.Black);

            bool textureLoaded = false;

            void TryLoadImage()
            {
                if (textureLoaded) return;

                string assetsPath = _appInstance.configManager.AssetsPath;
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
                else
                {
                    _appInstance.assetManager.RequestGameAssets(game);
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

                if (textureLoaded && _mainScene.gameList.HasMethod("UpdateLayout"))
                {
                    bool isAnimating = (bool)_mainScene.gameList.Get("IsAnimating");
                    if (!isAnimating)
                    {
                        _mainScene.gameList.CallDeferred("UpdateLayout", false);
                    }
                }
            }

            void TryUnloadImage()
            {
                if (!textureLoaded) return;
                entry.Texture = _mainScene.placeholderTexture;
                titleLabel.Visible = true;
                textureLoaded = false;
            }

            entry.Visible = false;
            entry.VisibilityChanged += () => 
            {
                if (entry.Visible) TryLoadImage();
                else TryUnloadImage();
            };

            AssetManager.AssetDownloadedEventHandler onAssetDownloaded = null;
            onAssetDownloaded = (downloadedGameId, assetType) =>
            {
                if (downloadedGameId == game.Id && (assetType == "box3d" || assetType == "box2d"))
                {
                    textureLoaded = false;
                    if (entry.Visible) TryLoadImage();
                }
            };
            _appInstance.assetManager.AssetDownloaded += onAssetDownloaded;

            entry.TreeExiting += () => 
            {
                _appInstance.assetManager.AssetDownloaded -= onAssetDownloaded;
            };

            _mainScene.gameList.AddChild(entry);
        }

        if (_mainScene.gameList.HasMethod("Refresh"))
        {
            int targetIndex = 0;
            if (currentlySelectedGame != null)
            {
                targetIndex = currentlyShownGames.FindIndex(g => g.Id == currentlySelectedGame.Id);
                if (targetIndex == -1) targetIndex = 0;
            }

            _mainScene.gameList.Set("SelectedIndex", targetIndex);
            _mainScene.gameList.Call("Refresh");
        }

        if (_mainScene.detailsPanelContainer != null)
        {
            _mainScene.detailsPanelContainer.Visible = currentlyShownGames.Count > 0;
        }
    }

    public bool CheckIfGameIsDownloaded(Game game)
    {
        if (game.Files == null || !game.Files.Any())
        {
            return false;
        }

        string fileName = game.Files[0].FileName;
        string fullPath = _appInstance.configManager.RomsPath.PathJoin(game.System.Slug).PathJoin(fileName);
        return Godot.FileAccess.FileExists(fullPath);
    }

    public async void OnGameSelected(long index)
    {
        if (index < 0 || index >= currentlyShownGames.Count)
        {
            return;
        }

        currentlySelectedGame = currentlyShownGames[(int)index];
        ShowGameDetails(currentlySelectedGame);

        await _mainScene.ToSignal(_mainScene.GetTree(), "process_frame");

        if (_mainScene.gameList != null)
        {
            var children = _mainScene.gameList.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                var entry = children[i] as TextureRect;
                var focusPanel = entry?.GetNodeOrNull<Panel>("FocusPanel");
                if (focusPanel != null)
                {
                    focusPanel.Visible = (i == (int)index);
                }

                if (i == (int)index && entry != null)
                {
                    var popupItem = new DelegateHoverPopupItem(() => {
                        var vbox = new VBoxContainer();
                        var viewportSize = _mainScene.GetViewport().GetVisibleRect().Size;
                        float targetWidth = viewportSize.X * 0.225f; // 22.5% of screen width
                        float targetHeight = targetWidth;
                        
                        if (entry.Texture != null && entry.Texture.GetSize().X > 0)
                        {
                            targetHeight = targetWidth * (entry.Texture.GetSize().Y / entry.Texture.GetSize().X);
                        }

                        var tex = new TextureRect { 
                            Texture = entry.Texture, 
                            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, 
                            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, 
                            CustomMinimumSize = new Vector2(targetWidth, targetHeight) 
                        };
                        var lbl = new Label {
                            Text = currentlySelectedGame.Name,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            AutowrapMode = TextServer.AutowrapMode.WordSmart,
                            CustomMinimumSize = new Vector2(targetWidth, 0)
                        };

                        int titleFontSize = (int)(viewportSize.Y * 0.022f);
                        lbl.AddThemeFontSizeOverride("font_size", titleFontSize);

                        vbox.AddChild(tex);
                        vbox.AddChild(lbl);
                        return vbox;
                    });
                    _mainScene.HoverOverlay.OnItemHovered(entry, popupItem);
                }
            }
        }
    }

    public void ShowGameDetails(Game game)
    {
        _appInstance.assetManager.RequestGameAssets(game);
        
        if (_mainScene.detailsPanelContainer == null)
        {
            return;
        }

        UpdateGameDetailsUI(game);
    }

    private void UpdateGameDetailsUI(Game game)
    {
        if (_mainScene.gameTitle != null) 
        {
            _mainScene.gameTitle.Text = game.Name;
            _mainScene.gameTitle.Visible = true;
        }

        if (_mainScene.gameDescription != null)
        {
            _mainScene.gameDescription.Text = game.Description;

            var descScroller = _mainScene.gameDescription.GetNodeOrNull<AutoScrollHelper>("AutoScrollHelper");
            if (descScroller == null)
            {
                descScroller = new AutoScrollHelper {
                    RichText = _mainScene.gameDescription,
                    IsVertical = true,
                    ScrollSpeed = 10f,
                    StartDelay = 5f,
                    Name = "AutoScrollHelper"
                };
                _mainScene.gameDescription.AddChild(descScroller);
            }
            // Start each newly selected game from the top and wait before scrolling.
            descScroller.Restart();
        }

        if (_mainScene.gameMarquee != null)
        {
            _mainScene.gameMarquee.Visible = false;
            string assetsPath = _appInstance.configManager.AssetsPath;
            string pathMarquee = System.IO.Path.Combine(assetsPath, "marquees", $"{game.Id}.png");
            
            ImageTexture marqueeTex = SafeLoadTexture(pathMarquee);

            if (marqueeTex != null)
            {
                _mainScene.gameMarquee.Texture = marqueeTex;
                _mainScene.gameMarquee.Visible = true;

                if (_mainScene.gameTitle != null) _mainScene.gameTitle.Visible = false;
            }
            else
            {
                if (_mainScene.gameTitle != null) _mainScene.gameTitle.Visible = true;
            }
        }

        if (_mainScene.gameCover != null)
        {
            string assetsPath = _appInstance.configManager.AssetsPath;
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

            _mainScene.gameCover.Texture = loadedTex;
        }

        if (_mainScene.gameScreenshotsScroll != null && !_mainScene.gameScreenshotsScroll.HasNode("AutoScrollHelper"))
        {
            var autoScroller = new AutoScrollHelper { ScrollContainer = _mainScene.gameScreenshotsScroll, Name = "AutoScrollHelper" };
            _mainScene.gameScreenshotsScroll.AddChild(autoScroller);
        }

        if (_mainScene.gameScreenshotsFlow != null)
        {
            foreach(Node child in _mainScene.gameScreenshotsFlow.GetChildren()) 
            {
                _mainScene.gameScreenshotsFlow.RemoveChild(child);
                child.QueueFree();
            }
        }

        string currentAssetsPath = _appInstance.configManager.AssetsPath;

        void AddToFlow(string path)
        {
            if (Godot.FileAccess.FileExists(path) && _mainScene.gameScreenshotsFlow != null)
            {
                var image = Image.LoadFromFile(path);
                
                // CRITICAL FIX: Ensure the image successfully loaded and has valid dimensions before proceeding!
                if (image == null || image.IsEmpty() || image.GetWidth() == 0 || image.GetHeight() == 0)
                {
                    GD.PrintErr($"[MainSceneGameListHandler] Failed to load image or image is empty: {path}");
                    return;
                }

                float ratio = (float)image.GetWidth() / image.GetHeight();
                var tex = ImageTexture.CreateFromImage(image);
                
                // We manually compute the height to maintain aspect ratio based on a fixed width.
                // This guarantees the GridContainer knows exactly how tall each image should be.
                float targetWidth = 115.0f;
                float targetHeight = targetWidth / ratio;

                var texRect = new TextureRect { 
                    Texture = tex, 
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, 
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    CustomMinimumSize = new Vector2(targetWidth, targetHeight),
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
                };
                
                _mainScene.gameScreenshotsFlow.AddChild(texRect);
            }
        }

        AddToFlow(System.IO.Path.Combine(currentAssetsPath, "covers_3d", $"{game.Id}.png"));
        AddToFlow(System.IO.Path.Combine(currentAssetsPath, "covers_2d", $"{game.Id}.png"));
        AddToFlow(System.IO.Path.Combine(currentAssetsPath, "marquees", $"{game.Id}.png"));

        string[] flowExts = { ".png", ".jpg", ".webp" };
        foreach (var ext in flowExts)
        {
            AddToFlow(System.IO.Path.Combine(currentAssetsPath, "covers_fallback", $"{game.Id}{ext}"));
        }

        string oldScreenshotPath = System.IO.Path.Combine(currentAssetsPath, "screenshots", $"{game.Id}.jpg");
        AddToFlow(oldScreenshotPath);
        
        for (int i = 0; i < 5; i++)
        {
            AddToFlow(System.IO.Path.Combine(currentAssetsPath, "screenshots", $"{game.Id}_{i}.jpg"));
        }

        bool isDownloading = false;
        if (game.Files != null && game.Files.Any())
        {
            isDownloading = _appInstance.downloadManager.IsDownloading(game.Files[0].FileName);
        }
        if (_mainScene.gameDownloadProgressBar != null)
        {
            _mainScene.gameDownloadProgressBar.Visible = isDownloading;
        }

        UpdateDetailsPanelButtons(game);
    }

    private void OnDownloadProgressUpdated(string fileName, long currentBytes, long totalBytes, string gameId)
    {
        if (currentlySelectedGame != null && currentlySelectedGame.Id.ToString() == gameId)
        {
            if (_mainScene.gameDownloadProgressBar != null)
            {
                _mainScene.gameDownloadProgressBar.Visible = true;
                _mainScene.gameDownloadProgressBar.MaxValue = totalBytes;
                _mainScene.gameDownloadProgressBar.Value = currentBytes;
            }
        }
    }
    
    private partial class AutoScrollHelper : Node
    {
        public ScrollContainer ScrollContainer;
        public RichTextLabel RichText;
        public bool IsVertical = false;
        public float ScrollSpeed = 50f;
        public float StartDelay = 0f;
        private float scrollAccumulator;
        private float delayTimer;

        // Reset to the top and re-arm the start delay (call when the content changes).
        public void Restart()
        {
            delayTimer = StartDelay;
            scrollAccumulator = 0f;
            if (RichText != null && RichText.GetVScrollBar() != null)
            {
                RichText.GetVScrollBar().Value = 0;
            }
            else if (ScrollContainer != null)
            {
                ScrollContainer.ScrollVertical = 0;
                ScrollContainer.ScrollHorizontal = 0;
            }
        }

        public override void _Process(double delta)
        {
            if (delayTimer > 0f)
            {
                delayTimer -= (float)delta;
                return;
            }

            if (RichText != null)
            {
                var vbar = RichText.GetVScrollBar();
                if (vbar != null)
                {
                    var maxScroll = vbar.MaxValue - vbar.Page;
                    if (maxScroll > 0)
                    {
                        // A scrollbar's Value snaps to its step (default 1), so add whole
                        // pixels at a time; fractional increments would be rounded away and
                        // the label would never move on its own.
                        scrollAccumulator += ScrollSpeed * (float)delta;
                        if (scrollAccumulator >= 1.0f)
                        {
                            vbar.Value += (int)scrollAccumulator;
                            scrollAccumulator -= (int)scrollAccumulator;
                        }
                        if (vbar.Value >= maxScroll)
                        {
                            vbar.Value = 0;
                        }
                    }
                }
                return;
            }

            if (ScrollContainer != null)
            {
                if (IsVertical && ScrollContainer.GetVScrollBar() != null)
                {
                    var maxScroll = ScrollContainer.GetVScrollBar().MaxValue - ScrollContainer.GetVScrollBar().Page;
                    if (maxScroll > 0)
                    {
                        scrollAccumulator += ScrollSpeed * (float)delta;
                        if (scrollAccumulator >= 1.0f)
                        {
                            ScrollContainer.ScrollVertical += (int)scrollAccumulator;
                            scrollAccumulator -= (int)scrollAccumulator;
                        }
                        if (ScrollContainer.ScrollVertical >= maxScroll)
                        {
                            ScrollContainer.ScrollVertical = 0;
                        }
                    }
                }
                else if (!IsVertical && ScrollContainer.GetHScrollBar() != null)
                {
                    var maxScroll = ScrollContainer.GetHScrollBar().MaxValue - ScrollContainer.GetHScrollBar().Page;
                    if (maxScroll > 0)
                    {
                        scrollAccumulator += ScrollSpeed * (float)delta;
                        if (scrollAccumulator >= 1.0f)
                        {
                            ScrollContainer.ScrollHorizontal += (int)scrollAccumulator;
                            scrollAccumulator -= (int)scrollAccumulator;
                        }
                        if (ScrollContainer.ScrollHorizontal >= maxScroll)
                        {
                            ScrollContainer.ScrollHorizontal = 0;
                        }
                    }
                }
            }
        }
    }
    
    public void UpdateDetailsPanelButtons(Game game)
    {
        bool isGameDownloadedLocally = CheckIfGameIsDownloaded(game);

        if (_mainScene.installedIcon != null)
        {
            _mainScene.installedIcon.Visible = isGameDownloadedLocally;
        }

        if (_mainScene.actionBtn == null)
        {
            return;
        }

        bool isDownloading = false;

        if (game.Files != null && game.Files.Any())
        {
            isDownloading = _appInstance.downloadManager.IsDownloading(game.Files[0].FileName);
        }

        if (isGameDownloadedLocally)
        {
            if (_appInstance.emulatorManager.IsEmulatorInstalled(_appInstance.emulatorManager.GetMappedEmulator(game.PlatformSlug)))
            {
                _mainScene.actionBtn.Text = "Play";
                _mainScene.actionBtn.Disabled = false; 
            }
            else
            {
                _mainScene.actionBtn.Text = "Install Emulator";
                _mainScene.actionBtn.Disabled = false;
            }
        }
        else
        {
            if (isDownloading)
            {
                _mainScene.actionBtn.Text = "Downloading...";
                _mainScene.actionBtn.Disabled = true;
            }
            else
            {
                _mainScene.actionBtn.Text = "Download";
                _mainScene.actionBtn.Disabled = false;
            }
        }

        if (_mainScene.deleteBtn != null)
        {
            _mainScene.deleteBtn.Disabled = !isGameDownloadedLocally;
        }
    }

    public void OnJumpSectionRequested(int direction)
    {
        if (currentlyShownGames == null || currentlyShownGames.Count == 0 || _mainScene.gameList == null)
        {
            return;
        }

        int currentIndex = (int)_mainScene.gameList.Get("SelectedIndex");

        if (currentIndex < 0 || currentIndex >= currentlyShownGames.Count)
        {
            return;
        }

        char GetSectionChar(string name)
        {
            if (string.IsNullOrEmpty(name)) return '#';

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
            
            if (string.IsNullOrEmpty(sortName)) return '#';

            char first = char.ToUpper(sortName[0]);
            return char.IsLetter(first) ? first : '#';
        }
        
        char currentLetter = GetSectionChar(currentlyShownGames[currentIndex].Name);
        int targetIndex = currentIndex;
        
        if (direction > 0)
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
        else
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
            _mainScene.gameList.Set("SelectedIndex", targetIndex);
            _mainScene.gameList.Call("UpdateLayout", true);
            OnGameSelected(targetIndex);
        }
    }

    public void DeleteLocalGame(Game game)
    {
        List<RomFile> romFiles = game.Files;
        foreach (RomFile file in romFiles)
        { 
            System.IO.File.Delete(file.FullPath);
        }
    }

    public ImageTexture SafeLoadTexture(string path)
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
}
