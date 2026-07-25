using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainSceneGameListHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;

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
        this.mainScene = mainScene;
        this.appInstance = appInstance;
        appInstance.downloadManager.DownloadProgressUpdated += OnDownloadProgressUpdated;
        appInstance.assetManager.AssetDownloaded += OnAssetDownloaded;
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

    public void BeginQuickSwitchFade()
    {
        if (mainScene.gameList == null) return;
        if (isTransitioningSystem || preFadedForQuickSwitch) return;

        preFadedForQuickSwitch = true;

        float duration = 0.15f;

        Tween fadeOutTween = mainScene.CreateTween();
        Color glColorOut = mainScene.gameList.Modulate; glColorOut.A = 0.0f;
        fadeOutTween.TweenProperty(mainScene.gameList, "modulate", glColorOut, duration);
        if (mainScene.detailsPanelContainer != null)
        {
            Color dpcColorOut = mainScene.detailsPanelContainer.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(mainScene.detailsPanelContainer, "modulate", dpcColorOut, duration);
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

        if (!preFadedForQuickSwitch)
        {
        Tween fadeOutTween = mainScene.CreateTween();

        Color glColorOut = mainScene.gameList.Modulate; glColorOut.A = 0.0f;
        fadeOutTween.TweenProperty(mainScene.gameList, "modulate", glColorOut, duration);

        if (mainScene.detailsPanelContainer != null) {
            Color dpcColorOut = mainScene.detailsPanelContainer.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(mainScene.detailsPanelContainer, "modulate", dpcColorOut, duration);
        }

        await mainScene.ToSignal(fadeOutTween, Tween.SignalName.Finished);
        }

        preFadedForQuickSwitch = false;

        var glModOut = mainScene.gameList.Modulate; glModOut.A = 0.0f; mainScene.gameList.Modulate = glModOut;

        if (mainScene.detailsPanelContainer != null) { var dpcMod = mainScene.detailsPanelContainer.Modulate; dpcMod.A = 0.0f; mainScene.detailsPanelContainer.Modulate = dpcMod; }

        DoSelectSystemByIndex(targetIndex);

        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");
        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");

        Tween fadeInTween = mainScene.CreateTween();

        Color glColorIn = mainScene.gameList.Modulate; glColorIn.A = 1.0f;
        fadeInTween.TweenProperty(mainScene.gameList, "modulate", glColorIn, duration);

        if (mainScene.detailsPanelContainer != null) {
            Color dpcColorIn = mainScene.detailsPanelContainer.Modulate; dpcColorIn.A = 1.0f;
            fadeInTween.Parallel().TweenProperty(mainScene.detailsPanelContainer, "modulate", dpcColorIn, duration);
        }

        await mainScene.ToSignal(fadeInTween, Tween.SignalName.Finished);

        var glModIn = mainScene.gameList.Modulate; glModIn.A = 1.0f; mainScene.gameList.Modulate = glModIn;

        if (mainScene.detailsPanelContainer != null) { var dpcMod = mainScene.detailsPanelContainer.Modulate; dpcMod.A = 1.0f; mainScene.detailsPanelContainer.Modulate = dpcMod; }

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

        mainScene.UpdateHeaderLabel();

        if (ConfigManager.IsSystemTheme(appInstance.configManager.AppTheme))
        {
            mainScene.ApplyTheme();
        }

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
        if (mainScene.gameList == null)
        {
            return;
        }

        currentlySelectedGame = null;

        ApplyFiltersAndRefresh();

        if (currentlyShownGames.Any())
        {
            OnGameSelected(0L);

            if (mainScene.downloadsListContainer != null && !mainScene.downloadsListContainer.Visible &&
                (mainScene.settingsMenuContainer == null || !mainScene.settingsMenuContainer.Visible))
            {
                mainScene.gameList.GrabFocus();
            }

            mainScene.UpdateHeaderLabel();
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

    public async void ApplyFiltersWithFade()
    {
        if (mainScene.gameList == null)
        {
            ApplyFiltersAndRefresh();
            return;
        }

        IsFilterTransitioning = true;
        float duration = 0.15f;

        Tween fadeOut = mainScene.CreateTween();
        Color glOut = mainScene.gameList.Modulate; glOut.A = 0.0f;
        fadeOut.TweenProperty(mainScene.gameList, "modulate", glOut, duration);
        if (mainScene.detailsPanelContainer != null)
        {
            Color dpcOut = mainScene.detailsPanelContainer.Modulate; dpcOut.A = 0.0f;
            fadeOut.Parallel().TweenProperty(mainScene.detailsPanelContainer, "modulate", dpcOut, duration);
        }
        await mainScene.ToSignal(fadeOut, Tween.SignalName.Finished);

        ApplyFiltersAndRefresh();

        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");
        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");

        Tween fadeIn = mainScene.CreateTween();
        Color glIn = mainScene.gameList.Modulate; glIn.A = 1.0f;
        fadeIn.TweenProperty(mainScene.gameList, "modulate", glIn, duration);
        if (mainScene.detailsPanelContainer != null)
        {
            Color dpcIn = mainScene.detailsPanelContainer.Modulate; dpcIn.A = 1.0f;
            fadeIn.Parallel().TweenProperty(mainScene.detailsPanelContainer, "modulate", dpcIn, duration);
        }
        await mainScene.ToSignal(fadeIn, Tween.SignalName.Finished);

        mainScene.gameList.Modulate = new Color(mainScene.gameList.Modulate, 1.0f);
        if (mainScene.detailsPanelContainer != null)
        {
            mainScene.detailsPanelContainer.Modulate = new Color(mainScene.detailsPanelContainer.Modulate, 1.0f);
        }

        IsFilterTransitioning = false;
    }

    private const double ImageLoadBudgetMs = 4.0;
    private readonly List<GameCard> pendingImageLoads = new List<GameCard>();
    private readonly Dictionary<GameCard, Action> imageLoaders = new Dictionary<GameCard, Action>();

    private void RequestImageLoad(GameCard entry)
    {
        if (entry == null || pendingImageLoads.Contains(entry))
        {
            return;
        }
        pendingImageLoads.Add(entry);
    }

    public void ProcessPendingImageLoads()
    {
        var frameBudget = System.Diagnostics.Stopwatch.StartNew();

        while (pendingImageLoads.Count > 0)
        {
            GameCard entry = TakeTopmostPending();
            if (entry == null)
            {
                break;
            }

            if (imageLoaders.TryGetValue(entry, out Action load))
            {
                load();
            }

            if (frameBudget.Elapsed.TotalMilliseconds >= ImageLoadBudgetMs)
            {
                break;
            }
        }
    }

    private GameCard TakeTopmostPending()
    {
        pendingImageLoads.RemoveAll(candidate => !GodotObject.IsInstanceValid(candidate) || !candidate.Visible);

        if (pendingImageLoads.Count == 0)
        {
            return null;
        }

        int topmost = 0;
        for (int i = 1; i < pendingImageLoads.Count; i++)
        {
            if (pendingImageLoads[i].Position.Y < pendingImageLoads[topmost].Position.Y)
            {
                topmost = i;
            }
        }

        GameCard entry = pendingImageLoads[topmost];
        pendingImageLoads.RemoveAt(topmost);
        return entry;
    }

    public void RefreshGameList()
    {
        if (mainScene.gameList == null)
        {
            return;
        }

        appInstance.assetManager.ClearPendingAssetDownloads();

        pendingImageLoads.Clear();
        imageLoaders.Clear();

        foreach (Node child in mainScene.gameList.GetChildren())
        {
            mainScene.gameList.RemoveChild(child);
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

            if (mainScene.gameListEntryScene == null)
            {
                continue;
            }

            GameCard entry = mainScene.gameListEntryScene.Instantiate<GameCard>();
            entry.FocusMode = Control.FocusModeEnum.All;
            entry.Title = game.Name;
            entry.SetCover(mainScene.placeholderTexture, true);

            bool textureLoaded = false;

            void TryLoadImage()
            {
                if (textureLoaded) return;

                string assetsPath = appInstance.configManager.AssetsPath;
                string path3d = System.IO.Path.Combine(assetsPath, "covers_3d", $"{game.Id}.png");
                string path2d = System.IO.Path.Combine(assetsPath, "covers_2d", $"{game.Id}.png");

                ImageTexture loadedTex = SafeLoadTexture(path2d) ?? SafeLoadTexture(path3d);

                if (loadedTex == null)
                {
                    foreach (var ext in new[] { ".png", ".jpg", ".webp" })
                    {
                        string p = System.IO.Path.Combine(assetsPath, "covers_fallback", $"{game.Id}{ext}");
                        if (Godot.FileAccess.FileExists(p))
                        {
                            loadedTex = SafeLoadTexture(p);
                            break;
                        }
                    }
                }

                if (loadedTex != null)
                {
                    entry.SetCover(loadedTex, false);
                    textureLoaded = true;
                }
                else
                {
                    appInstance.assetManager.RequestGameAssets(game);
                }

                entry.SetInstalledIcon(CheckIfGameIsDownloaded(game) ? systemControllerIcon : null);

                entry.Reveal();

                if (textureLoaded && mainScene.gameList.HasMethod("UpdateLayout"))
                {
                    bool isAnimating = (bool)mainScene.gameList.Get("IsAnimating");
                    if (!isAnimating)
                    {
                        mainScene.gameList.CallDeferred("UpdateLayout", false);
                    }
                }
            }

            void TryUnloadImage()
            {
                appInstance.assetManager.CancelGameAssets(game.Id);

                if (!textureLoaded) return;
                entry.SetCover(mainScene.placeholderTexture, true);
                textureLoaded = false;
            }

            entry.Visible = false;
            imageLoaders[entry] = TryLoadImage;
            entry.VisibilityChanged += () =>
            {
                if (entry.Visible) RequestImageLoad(entry);
                else TryUnloadImage();
            };

            AssetManager.AssetDownloadedEventHandler onAssetDownloaded = null;
            onAssetDownloaded = (downloadedGameId, assetType) =>
            {
                if (downloadedGameId == game.Id && (assetType == "box3d" || assetType == "box2d"))
                {
                    textureLoaded = false;
                    if (entry.Visible) RequestImageLoad(entry);
                }
            };
            appInstance.assetManager.AssetDownloaded += onAssetDownloaded;

            entry.TreeExiting += () =>
            {
                appInstance.assetManager.AssetDownloaded -= onAssetDownloaded;
                appInstance.assetManager.CancelGameAssets(game.Id);
                imageLoaders.Remove(entry);
            };

            mainScene.gameList.AddChild(entry);
        }

        if (mainScene.gameList.HasMethod("Refresh"))
        {
            int targetIndex = 0;
            if (currentlySelectedGame != null)
            {
                targetIndex = currentlyShownGames.FindIndex(g => g.Id == currentlySelectedGame.Id);
                if (targetIndex == -1) targetIndex = 0;
            }

            mainScene.gameList.Set("SelectedIndex", targetIndex);
            mainScene.gameList.Call("Refresh");
        }

        if (mainScene.detailsPanelContainer != null)
        {
            mainScene.detailsPanelContainer.Visible = currentlyShownGames.Count > 0;
        }
    }

    public bool CheckIfGameIsDownloaded(Game game)
    {
        if (game.Files == null || !game.Files.Any())
        {
            return false;
        }

        string fileName = game.Files[0].FileName;
        string fullPath = appInstance.configManager.RomsPath.PathJoin(game.System.Slug).PathJoin(fileName);
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

        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");

        if (mainScene.gameList != null)
        {
            var children = mainScene.gameList.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                var entry = children[i] as GameCard;
                if (entry == null)
                {
                    continue;
                }

                entry.Selected = (i == (int)index);
            }
        }
    }

    public void ShowGameDetails(Game game)
    {
        appInstance.assetManager.RequestGameAssets(game);

        if (mainScene.detailsPanelContainer == null)
        {
            return;
        }

        UpdateGameDetailsUI(game);
    }

    private void UpdateGameDetailsUI(Game game)
    {
        if (mainScene.gameTitle != null)
        {
            mainScene.gameTitle.Text = game.Name;
            mainScene.gameTitle.Visible = true;
        }

        if (mainScene.gameDescription != null)
        {
            mainScene.gameDescription.Text = game.Description;

            var descScroller = mainScene.gameDescription.GetNodeOrNull<AutoScrollHelper>("AutoScrollHelper");
            if (descScroller == null)
            {
                descScroller = new AutoScrollHelper {
                    RichText = mainScene.gameDescription,
                    IsVertical = true,
                    ScrollSpeed = 10f,
                    StartDelay = 5f,
                    Name = "AutoScrollHelper"
                };
                mainScene.gameDescription.AddChild(descScroller);
            }
            descScroller.Restart();
        }

        if (mainScene.gameMarquee != null)
        {
            mainScene.gameMarquee.Visible = false;
            string assetsPath = appInstance.configManager.AssetsPath;
            string pathMarquee = System.IO.Path.Combine(assetsPath, "marquees", $"{game.Id}.png");

            ImageTexture marqueeTex = SafeLoadTexture(pathMarquee);

            if (marqueeTex != null)
            {
                mainScene.gameMarquee.Texture = marqueeTex;
                mainScene.gameMarquee.Visible = true;

                if (mainScene.gameTitle != null) mainScene.gameTitle.Visible = false;
            }
            else
            {
                if (mainScene.gameTitle != null) mainScene.gameTitle.Visible = true;
            }
        }

        if (mainScene.gameCover != null)
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

            mainScene.gameCover.Texture = loadedTex;
        }

        if (mainScene.gameScreenshotsScroll != null && !mainScene.gameScreenshotsScroll.HasNode("AutoScrollHelper"))
        {
            var autoScroller = new AutoScrollHelper { ScrollContainer = mainScene.gameScreenshotsScroll, Name = "AutoScrollHelper" };
            mainScene.gameScreenshotsScroll.AddChild(autoScroller);
        }

        if (mainScene.gameScreenshotsFlow != null)
        {
            foreach(Node child in mainScene.gameScreenshotsFlow.GetChildren())
            {
                mainScene.gameScreenshotsFlow.RemoveChild(child);
                child.QueueFree();
            }
        }

        string currentAssetsPath = appInstance.configManager.AssetsPath;

        void AddToFlow(string path)
        {
            if (mainScene.gameScreenshotsFlow != null)
            {
                var image = SafeLoadImage(path);

                if (image == null || image.GetWidth() == 0 || image.GetHeight() == 0)
                {
                    return;
                }

                float ratio = (float)image.GetWidth() / image.GetHeight();
                var tex = ImageTexture.CreateFromImage(image);

                float targetWidth = 115.0f;
                float targetHeight = targetWidth / ratio;

                var texRect = new TextureRect {
                    Texture = tex,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    CustomMinimumSize = new Vector2(targetWidth, targetHeight),
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
                };

                mainScene.gameScreenshotsFlow.AddChild(texRect);
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
            isDownloading = appInstance.downloadManager.IsDownloading(game.Files[0].FileName);
        }
        if (mainScene.gameDownloadProgressBar != null)
        {
            mainScene.gameDownloadProgressBar.Visible = isDownloading;
        }

        UpdateDetailsPanelButtons(game);
    }

    private void OnDownloadProgressUpdated(string fileName, long currentBytes, long totalBytes, string gameId)
    {
        if (currentlySelectedGame != null && currentlySelectedGame.Id.ToString() == gameId)
        {
            if (mainScene.gameDownloadProgressBar != null)
            {
                mainScene.gameDownloadProgressBar.Visible = true;
                DownloadProgressDisplay.ApplyTo(mainScene.gameDownloadProgressBar, currentBytes, totalBytes);
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

        if (mainScene.installedIcon != null)
        {
            mainScene.installedIcon.Visible = isGameDownloadedLocally;
        }

        if (mainScene.actionBtn == null)
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
            string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(game.PlatformSlug);

            if (appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator))
            {
                mainScene.actionBtn.Text = "Play";
                mainScene.actionBtn.Disabled = false;
            }
            else if (appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator))
            {
                mainScene.actionBtn.Text = "Installing Emulator...";
                mainScene.actionBtn.Disabled = true;
            }
            else
            {
                mainScene.actionBtn.Text = "Install Emulator";
                mainScene.actionBtn.Disabled = false;
            }
        }
        else
        {
            if (isDownloading)
            {
                mainScene.actionBtn.Text = "Downloading...";
                mainScene.actionBtn.Disabled = true;
            }
            else
            {
                mainScene.actionBtn.Text = "Download";
                mainScene.actionBtn.Disabled = false;
            }
        }

        if (mainScene.deleteBtn != null)
        {
            mainScene.deleteBtn.Disabled = !isGameDownloadedLocally;
        }
    }

    public void OnJumpSectionRequested(int direction)
    {
        if (currentlyShownGames == null || currentlyShownGames.Count == 0 || mainScene.gameList == null)
        {
            return;
        }

        int currentIndex = (int)mainScene.gameList.Get("SelectedIndex");

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
            mainScene.gameList.Set("SelectedIndex", targetIndex);
            mainScene.gameList.Call("UpdateLayout", true);
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

    public Image SafeLoadImage(string path)
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
                return img;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    public ImageTexture SafeLoadTexture(string path)
    {
        Image img = SafeLoadImage(path);
        return img != null ? ImageTexture.CreateFromImage(img) : null;
    }
}
