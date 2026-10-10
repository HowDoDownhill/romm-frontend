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

        foreach (GameListView view in mainScene.gameListViews)
        {
            view.ItemFactory = view switch
            {
                GameTextListView => CreateGameRow,
                GameGridView => CreateGridCard,
                _ => CreateGameCard
            };
            view.ItemBound += OnCarouselItemBound;
            view.ItemReleased += OnCarouselItemReleased;
        }
    }

    public void Detach()
    {
        foreach (GameListView view in mainScene.gameListViews)
        {
            view.ItemFactory = null;
            view.ItemBound -= OnCarouselItemBound;
            view.ItemReleased -= OnCarouselItemReleased;
        }

        if (appInstance.downloadManager != null)
        {
            appInstance.downloadManager.DownloadProgressUpdated -= OnDownloadProgressUpdated;
        }

        if (appInstance.assetManager != null)
        {
            appInstance.assetManager.AssetDownloaded -= OnAssetDownloaded;
        }
    }

    private bool marqueeRefreshPending;
    private bool coverRefreshPending;
    private bool screenshotsRefreshPending;

    private void OnAssetDownloaded(int gameId, string assetType)
    {
        InvalidateCachedAssetTextures(gameId);

        if ((assetType == "box3d" || assetType == "box2d") && cardByGameId.TryGetValue(gameId, out Control card))
        {
            cardsWithLoadedCover.Remove(card);

            if (card.Visible)
            {
                RequestImageLoad(card);
            }
        }

        if (currentlySelectedGame == null || currentlySelectedGame.Id != gameId)
        {
            return;
        }

        if (assetType == "marquee")
        {
            marqueeRefreshPending = true;
            screenshotsRefreshPending = true;
        }
        else if (assetType == "box3d" || assetType == "box2d")
        {
            coverRefreshPending = true;
            screenshotsRefreshPending = true;
        }
        else if (assetType == "screenshot")
        {
            screenshotsRefreshPending = true;
        }
    }

    private int pendingRomIdToSelect;

    private bool IsFollowingALobby => appInstance.netplayLobby != null
        && appInstance.netplayLobby.IsInLobby
        && !appInstance.netplayLobby.IsHosting;

    public void SelectGameById(int romId)
    {
        if (currentlyShownGames == null)
        {
            return;
        }

        int gameIndex = currentlyShownGames.FindIndex(game => game.Id == romId);

        if (gameIndex < 0)
        {
            return;
        }

        pendingRomIdToSelect = 0;
        ScrollGameListTo(gameIndex);
        OnGameSelected(gameIndex);
    }

    private void ScrollGameListTo(int gameIndex)
    {
        mainScene.ActiveGameList?.SelectIndex(gameIndex, true);
    }

    public void SelectGameOnceSystemSettles(int romId)
    {
        pendingRomIdToSelect = romId;
        SelectGameById(romId);
    }

    private long ResolveInitialGameIndex()
    {
        if (pendingRomIdToSelect <= 0)
        {
            return 0L;
        }

        int requestedIndex = currentlyShownGames.FindIndex(game => game.Id == pendingRomIdToSelect);
        pendingRomIdToSelect = 0;

        return requestedIndex < 0 ? 0L : requestedIndex;
    }

    public bool CurrentSystemIsCollection
    {
        get
        {
            if (gameSystems == null || currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
            {
                return false;
            }

            return gameSystems[currentGameSystemIndex].IsCollection;
        }
    }

    public void ProcessPendingDetailsRefresh()
    {
        if (!marqueeRefreshPending && !coverRefreshPending && !screenshotsRefreshPending)
        {
            return;
        }

        if (currentlySelectedGame == null)
        {
            marqueeRefreshPending = false;
            coverRefreshPending = false;
            screenshotsRefreshPending = false;
            return;
        }

        if (marqueeRefreshPending)
        {
            marqueeRefreshPending = false;
            UpdateDetailsMarquee(currentlySelectedGame);
        }

        if (coverRefreshPending)
        {
            coverRefreshPending = false;
            UpdateDetailsCover(currentlySelectedGame);
        }

        if (screenshotsRefreshPending)
        {
            screenshotsRefreshPending = false;
            BeginDetailsScreenshotsLoad(currentlySelectedGame);
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
        if (mainScene.detailsPanel != null)
        {
            Color dpcColorOut = mainScene.detailsPanel.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(mainScene.detailsPanel, "modulate", dpcColorOut, duration);
            CentreDetailsPivot();
            if (mainScene.detailsPanel != null) fadeOutTween.Parallel().TweenProperty(mainScene.detailsPanel, "scale", DetailsRestingScale, duration);
        }
    }

    private static readonly Vector2 DetailsRestingScale = new Vector2(0.98f, 0.98f);

    private void CentreDetailsPivot()
    {
        if (mainScene.detailsPanel == null) return;
        mainScene.detailsPanel.PivotOffset = mainScene.detailsPanel.Size / 2.0f;
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

        if (mainScene.detailsPanel != null) {
            Color dpcColorOut = mainScene.detailsPanel.Modulate; dpcColorOut.A = 0.0f;
            fadeOutTween.Parallel().TweenProperty(mainScene.detailsPanel, "modulate", dpcColorOut, duration);
            CentreDetailsPivot();
            if (mainScene.detailsPanel != null) fadeOutTween.Parallel().TweenProperty(mainScene.detailsPanel, "scale", DetailsRestingScale, duration);
        }

        await mainScene.ToSignal(fadeOutTween, Tween.SignalName.Finished);
        }

        preFadedForQuickSwitch = false;

        var glModOut = mainScene.gameList.Modulate; glModOut.A = 0.0f; mainScene.gameList.Modulate = glModOut;

        if (mainScene.detailsPanel != null) { var dpcMod = mainScene.detailsPanel.Modulate; dpcMod.A = 0.0f; mainScene.detailsPanel.Modulate = dpcMod; }

        DoSelectSystemByIndex(targetIndex);

        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");
        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");

        Tween fadeInTween = mainScene.CreateTween();

        Color glColorIn = mainScene.gameList.Modulate; glColorIn.A = 1.0f;
        fadeInTween.TweenProperty(mainScene.gameList, "modulate", glColorIn, duration);

        if (mainScene.detailsPanel != null) {
            Color dpcColorIn = mainScene.detailsPanel.Modulate; dpcColorIn.A = 1.0f;
            fadeInTween.Parallel().TweenProperty(mainScene.detailsPanel, "modulate", dpcColorIn, duration);
            CentreDetailsPivot();
            if (mainScene.detailsPanel != null)
            {
                mainScene.detailsPanel.Scale = DetailsRestingScale;
                fadeInTween.Parallel().TweenProperty(mainScene.detailsPanel, "scale", Vector2.One, duration);
            }
        }

        await mainScene.ToSignal(fadeInTween, Tween.SignalName.Finished);

        var glModIn = mainScene.gameList.Modulate; glModIn.A = 1.0f; mainScene.gameList.Modulate = glModIn;

        if (mainScene.detailsPanel != null) { var dpcMod = mainScene.detailsPanel.Modulate; dpcMod.A = 1.0f; mainScene.detailsPanel.Modulate = dpcMod; }

        isTransitioningSystem = false;
    }

    public void CancelQuickSwitchFade()
    {
        if (!preFadedForQuickSwitch)
        {
            return;
        }

        preFadedForQuickSwitch = false;

        if (mainScene.gameList != null)
        {
            Color restoredListColor = mainScene.gameList.Modulate;
            restoredListColor.A = 1.0f;
            mainScene.gameList.Modulate = restoredListColor;
        }

        if (mainScene.detailsPanel != null)
        {
            Color restoredPanelColor = mainScene.detailsPanel.Modulate;
            restoredPanelColor.A = 1.0f;
            mainScene.detailsPanel.Modulate = restoredPanelColor;
            mainScene.detailsPanel.Scale = Vector2.One;
        }
    }

    public void SelectSystemByIndex(int index)
    {
        if (index < 0 || index >= gameSystems.Count)
        {
            CancelQuickSwitchFade();
            return;
        }

        if (index == currentGameSystemIndex)
        {
            if (currentlyShownGames == null)
            {
                DoSelectSystemByIndex(index);
            }

            CancelQuickSwitchFade();
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
            long initialGameIndex = ResolveInitialGameIndex();
            ScrollGameListTo((int)initialGameIndex);
            OnGameSelected(initialGameIndex);

            if (mainScene.downloadsListContainer != null && !mainScene.downloadsListContainer.IsOpen &&
                (mainScene.settingsMenuContainer == null || !mainScene.settingsMenuContainer.IsOpen))
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

            if (showOnlyInstalledGames && !IsFollowingALobby)
            {
                currentlyShownGames = currentlyShownGames.Where(g => CheckIfGameIsDownloaded(g)).ToList();
            }

            GD.Print($"[Filter] index={currentGameSystemIndex} id={system.Id} name=\"{system.Name}\" cached={cachedGames.Count} shown={currentlyShownGames.Count} hideNoArt={appInstance.configManager.HideGamesWithoutBoxArt} installedOnly={showOnlyInstalledGames}");

            RefreshGameList();
        }
        else
        {
            GD.Print($"[Filter] index={currentGameSystemIndex} id={system.Id} name=\"{system.Name}\" NO CACHE ENTRY");
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
    private readonly List<Control> pendingImageLoads = new List<Control>();

    private void RequestImageLoad(Control entry)
    {
        if (entry == null || pendingImageLoads.Contains(entry))
        {
            return;
        }
        pendingImageLoads.Add(entry);
    }

    public void ProcessPendingImageLoads()
    {
        if (pendingImageLoads.Count == 0)
        {
            return;
        }

        var frameBudget = System.Diagnostics.Stopwatch.StartNew();

        while (pendingImageLoads.Count > 0)
        {
            Control entry = TakeTopmostPending();

            if (entry == null)
            {
                break;
            }

            LoadCoverForCard(entry);

            if (frameBudget.Elapsed.TotalMilliseconds >= ImageLoadBudgetMs)
            {
                break;
            }
        }
    }

    private Control TakeTopmostPending()
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

        Control entry = pendingImageLoads[topmost];
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

        currentSystemControllerIcon = ResolveSystemControllerIcon();

        if (mainScene.ActiveGameList is GameListView gameCarousel)
        {
            int targetIndex = 0;
            if (currentlySelectedGame != null)
            {
                targetIndex = currentlyShownGames.FindIndex(g => g.Id == currentlySelectedGame.Id);
                if (targetIndex == -1) targetIndex = 0;
            }

            gameCarousel.ReloadItems(currentlyShownGames.Count, targetIndex);
        }

        if (mainScene.detailsPanelContainer != null)
        {
            bool lobbyOwnsPanel = mainScene.NetplayHandler != null && mainScene.NetplayHandler.IsLobbyVisible;
            mainScene.detailsPanelContainer.Visible = currentlyShownGames.Count > 0 && !lobbyOwnsPanel;
        }
    }

    private readonly Dictionary<Control, Game> gameByCard = new Dictionary<Control, Game>();
    private readonly Dictionary<int, Control> cardByGameId = new Dictionary<int, Control>();
    private readonly HashSet<Control> cardsWithLoadedCover = new HashSet<Control>();
    private readonly Dictionary<string, Texture2D> platformIconsBySlug = new Dictionary<string, Texture2D>();
    private Texture2D currentSystemControllerIcon;

    private Texture2D ResolveSystemControllerIcon()
    {
        if (currentGameSystemIndex < 0 || currentGameSystemIndex >= gameSystems.Count)
        {
            return null;
        }

        var system = gameSystems[currentGameSystemIndex];
        string searchSlug = !string.IsNullOrEmpty(system.IgdbSlug) ? system.IgdbSlug : system.Slug;

        if (string.IsNullOrEmpty(searchSlug))
        {
            return null;
        }

        if (platformIconsBySlug.TryGetValue(searchSlug, out Texture2D cachedIcon))
        {
            return cachedIcon;
        }

        Texture2D icon = FindPlatformIcon(searchSlug, "res://assets/platforms/", new[] { ".svg", ".png" });
        platformIconsBySlug[searchSlug] = icon;
        return icon;
    }

    private Control CreateGameCard()
    {
        if (mainScene.gameListEntryScene == null)
        {
            return null;
        }

        GameCard card = mainScene.gameListEntryScene.Instantiate<GameCard>();
        card.FocusMode = Control.FocusModeEnum.All;
        card.MouseFilter = Control.MouseFilterEnum.Pass;
        return card;
    }

    private Control CreateGridCard()
    {
        Control card = CreateGameCard();

        if (card is GameCard gameCard)
        {
            GameGridView.ConfigureCard(gameCard);
        }

        return card;
    }

    private Control CreateGameRow()
    {
        return new GameListRow();
    }

    private void OnCarouselItemBound(Control boundControl, int itemIndex)
    {
        if (boundControl is not IGameListItem || currentlyShownGames == null || itemIndex < 0 || itemIndex >= currentlyShownGames.Count)
        {
            return;
        }

        BindCardToGame(boundControl, currentlyShownGames[itemIndex]);
    }

    private void OnCarouselItemReleased(Control releasedControl)
    {
        if (releasedControl is not IGameListItem item)
        {
            return;
        }

        Control card = releasedControl;

        pendingImageLoads.Remove(card);

        if (gameByCard.Remove(card, out Game game))
        {
            appInstance.assetManager.CancelGameAssets(game.Id);

            if (cardByGameId.TryGetValue(game.Id, out Control mappedCard) && mappedCard == card)
            {
                cardByGameId.Remove(game.Id);
            }
        }

        if (cardsWithLoadedCover.Remove(card))
        {
            item.SetCover(mainScene.placeholderTexture, true);
        }
    }

    private void BindCardToGame(Control card, Game game)
    {
        var item = (IGameListItem)card;
        gameByCard[card] = game;
        cardByGameId[game.Id] = card;
        cardsWithLoadedCover.Remove(card);

        item.Title = game.Name;
        item.Selected = currentlySelectedGame != null && currentlySelectedGame.Id == game.Id;
        item.SetCover(mainScene.placeholderTexture, true);
        item.SetInstalledIcon(null);
        item.ResetReveal();

        RequestImageLoad(card);
    }

    private void LoadCoverForCard(Control card)
    {
        if (!GodotObject.IsInstanceValid(card) || cardsWithLoadedCover.Contains(card) || card is not IGameListItem item)
        {
            return;
        }

        if (!gameByCard.TryGetValue(card, out Game game))
        {
            return;
        }

        item.SetInstalledIcon(CheckIfGameIsDownloaded(game) ? currentSystemControllerIcon : null);

        if (!item.ShowsCover)
        {
            cardsWithLoadedCover.Add(card);
            item.Reveal();
            return;
        }

        string coverPath = ResolveExistingCoverPath(game.Id);

        if (coverPath == null)
        {
            appInstance.assetManager.RequestGameAssets(game);
            item.Reveal();
            return;
        }

        RequestAssetTexture(coverPath, CoverMaximumWidth, coverTexture => ApplyCoverToCard(card, game, coverTexture));
    }

    private void ApplyCoverToCard(Control card, Game game, ImageTexture coverTexture)
    {
        if (!GodotObject.IsInstanceValid(card) || card is not IGameListItem item || !gameByCard.TryGetValue(card, out Game boundGame) || boundGame.Id != game.Id)
        {
            return;
        }

        if (coverTexture == null)
        {
            appInstance.assetManager.RequestGameAssets(game);
            item.Reveal();
            return;
        }

        item.SetCover(coverTexture, false);
        cardsWithLoadedCover.Add(card);
        item.Reveal();

        mainScene.ActiveGameList?.RequestCardRefresh();
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
        mainScene.NetplayHandler?.PushHostBrowsingGame(currentlySelectedGame);

        await mainScene.ToSignal(mainScene.GetTree(), "process_frame");

        foreach (var boundCard in gameByCard)
        {
            if (GodotObject.IsInstanceValid(boundCard.Key))
            {
                ((IGameListItem)boundCard.Key).Selected = currentlySelectedGame != null && boundCard.Value.Id == currentlySelectedGame.Id;
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
        UpdateDetailsTitleAndDescription(game);
        UpdateDetailsMarquee(game);
        UpdateDetailsCover(game);
        EnsureScreenshotsAutoScroller();
        BeginDetailsScreenshotsLoad(game);
        UpdateDetailsDownloadIndicator(game);
        UpdateDetailsPanelButtons(game);
    }

    private void UpdateDetailsTitleAndDescription(Game game)
    {
        if (mainScene.gameTitle != null)
        {
            mainScene.gameTitle.Text = game.Name;
            mainScene.gameTitle.Visible = true;
        }

        if (mainScene.gameDescription == null)
        {
            return;
        }

        mainScene.gameDescription.Text = System.Net.WebUtility.HtmlDecode(game.Description ?? "");

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

    private void UpdateDetailsMarquee(Game game)
    {
        if (mainScene.gameMarquee == null)
        {
            return;
        }

        mainScene.gameMarquee.Visible = false;
        if (mainScene.gameTitle != null) mainScene.gameTitle.Visible = true;

        string marqueePath = MarqueeAssetPath(game.Id);

        if (!System.IO.File.Exists(marqueePath))
        {
            return;
        }

        RequestAssetTexture(marqueePath, MarqueeMaximumWidth, marqueeTexture =>
        {
            if (marqueeTexture == null || currentlySelectedGame == null || currentlySelectedGame.Id != game.Id)
            {
                return;
            }

            mainScene.gameMarquee.Texture = marqueeTexture;
            mainScene.gameMarquee.Visible = true;

            if (mainScene.gameTitle != null) mainScene.gameTitle.Visible = false;
        });
    }

    private void UpdateDetailsCover(Game game)
    {
        if (mainScene.gameCover == null)
        {
            return;
        }

        string coverPath = ResolveExistingCoverPath(game.Id);

        if (coverPath == null)
        {
            mainScene.gameCover.Texture = null;
            return;
        }

        if (!TryGetCachedAssetTexture(coverPath, out _))
        {
            mainScene.gameCover.Texture = null;
        }

        RequestAssetTexture(coverPath, CoverMaximumWidth, coverTexture =>
        {
            if (currentlySelectedGame != null && currentlySelectedGame.Id == game.Id)
            {
                mainScene.gameCover.Texture = coverTexture;
            }
        });
    }

    private void EnsureScreenshotsAutoScroller()
    {
        if (mainScene.gameScreenshotsScroll == null || mainScene.gameScreenshotsScroll.HasNode("AutoScrollHelper"))
        {
            return;
        }

        var autoScroller = new AutoScrollHelper { ScrollContainer = mainScene.gameScreenshotsScroll, Name = "AutoScrollHelper" };
        mainScene.gameScreenshotsScroll.AddChild(autoScroller);
    }

    private void UpdateDetailsDownloadIndicator(Game game)
    {
        if (mainScene.gameDownloadProgressBar != null)
        {
            mainScene.gameDownloadProgressBar.Visible = appInstance.downloadManager.IsDownloadingGame(game.Id.ToString());
        }
    }

    private const int MaximumCachedAssetTextures = 64;
    private readonly Dictionary<string, ImageTexture> assetTexturesByPath = new Dictionary<string, ImageTexture>();
    private readonly LinkedList<string> assetTextureUsageOrder = new LinkedList<string>();

    private readonly AsyncImageLoader imageLoader = new AsyncImageLoader();
    private const int CoverMaximumWidth = 1024;
    private const int MarqueeMaximumWidth = 1920;
    private const double DecodedImageUploadBudgetMs = 4.0;

    public void PumpDecodedImages()
    {
        imageLoader.Pump(DecodedImageUploadBudgetMs);
    }

    private bool TryGetCachedAssetTexture(string path, out ImageTexture texture)
    {
        texture = null;

        if (string.IsNullOrEmpty(path) || !assetTexturesByPath.TryGetValue(path, out ImageTexture cachedTexture))
        {
            return false;
        }

        if (!GodotObject.IsInstanceValid(cachedTexture))
        {
            assetTexturesByPath.Remove(path);
            assetTextureUsageOrder.Remove(path);
            return false;
        }

        assetTextureUsageOrder.Remove(path);
        assetTextureUsageOrder.AddLast(path);
        texture = cachedTexture;
        return true;
    }

    private void StoreAssetTexture(string path, ImageTexture texture)
    {
        assetTexturesByPath[path] = texture;
        assetTextureUsageOrder.Remove(path);
        assetTextureUsageOrder.AddLast(path);

        while (assetTextureUsageOrder.Count > MaximumCachedAssetTextures)
        {
            string evictedPath = assetTextureUsageOrder.First.Value;
            assetTextureUsageOrder.RemoveFirst();
            assetTexturesByPath.Remove(evictedPath);
        }
    }

    private void RequestAssetTexture(string path, int maximumWidth, Action<ImageTexture> onReady)
    {
        if (TryGetCachedAssetTexture(path, out ImageTexture cachedTexture))
        {
            onReady(cachedTexture);
            return;
        }

        imageLoader.Request(path, maximumWidth, texture =>
        {
            if (texture != null)
            {
                StoreAssetTexture(path, texture);
            }

            onReady(texture);
        });
    }

    private string ResolveExistingCoverPath(int gameId)
    {
        if (System.IO.File.Exists(CoverTwoDimensionalAssetPath(gameId)))
        {
            return CoverTwoDimensionalAssetPath(gameId);
        }

        if (System.IO.File.Exists(CoverThreeDimensionalAssetPath(gameId)))
        {
            return CoverThreeDimensionalAssetPath(gameId);
        }

        return CoverFallbackAssetPaths(gameId).FirstOrDefault(System.IO.File.Exists);
    }

    private void InvalidateCachedAssetTextures(int gameId)
    {
        foreach (string path in DetailsPanelAssetPaths(gameId))
        {
            if (assetTexturesByPath.Remove(path))
            {
                assetTextureUsageOrder.Remove(path);
            }

            if (screenshotThumbnailsByPath.Remove(path))
            {
                screenshotThumbnailUsageOrder.Remove(path);
            }

            failedScreenshotPaths.Remove(path);
        }
    }

    private const int MaximumCachedScreenshotThumbnails = 320;
    private const float ScreenshotThumbnailWidth = 115.0f;
    private readonly Dictionary<string, ImageTexture> screenshotThumbnailsByPath = new Dictionary<string, ImageTexture>();
    private readonly LinkedList<string> screenshotThumbnailUsageOrder = new LinkedList<string>();

    private ImageTexture GetOrBuildScreenshotThumbnail(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (screenshotThumbnailsByPath.TryGetValue(path, out ImageTexture cachedThumbnail))
        {
            if (GodotObject.IsInstanceValid(cachedThumbnail))
            {
                screenshotThumbnailUsageOrder.Remove(path);
                screenshotThumbnailUsageOrder.AddLast(path);
                return cachedThumbnail;
            }

            screenshotThumbnailsByPath.Remove(path);
            screenshotThumbnailUsageOrder.Remove(path);
        }

        return null;
    }

    private int ScreenshotThumbnailPixelWidth => Mathf.Max(1, Mathf.RoundToInt(ScreenshotThumbnailWidth));

    private void RequestScreenshotThumbnail(string path)
    {
        if (screenshotThumbnailsByPath.ContainsKey(path) || failedScreenshotPaths.Contains(path) || imageLoader.IsPending(path, ScreenshotThumbnailPixelWidth))
        {
            return;
        }

        imageLoader.Request(path, ScreenshotThumbnailPixelWidth, thumbnailTexture =>
        {
            if (thumbnailTexture == null || thumbnailTexture.GetWidth() == 0 || thumbnailTexture.GetHeight() == 0)
            {
                failedScreenshotPaths.Add(path);
                return;
            }

            screenshotThumbnailsByPath[path] = thumbnailTexture;
            screenshotThumbnailUsageOrder.Remove(path);
            screenshotThumbnailUsageOrder.AddLast(path);

            while (screenshotThumbnailUsageOrder.Count > MaximumCachedScreenshotThumbnails)
            {
                string evictedPath = screenshotThumbnailUsageOrder.First.Value;
                screenshotThumbnailUsageOrder.RemoveFirst();
                screenshotThumbnailsByPath.Remove(evictedPath);
            }
        });
    }

    private readonly HashSet<string> failedScreenshotPaths = new HashSet<string>();

    private string MarqueeAssetPath(int gameId)
    {
        return System.IO.Path.Combine(appInstance.configManager.AssetsPath, "marquees", $"{gameId}.png");
    }

    private string CoverTwoDimensionalAssetPath(int gameId)
    {
        return System.IO.Path.Combine(appInstance.configManager.AssetsPath, "covers_2d", $"{gameId}.png");
    }

    private string CoverThreeDimensionalAssetPath(int gameId)
    {
        return System.IO.Path.Combine(appInstance.configManager.AssetsPath, "covers_3d", $"{gameId}.png");
    }

    private List<string> CoverFallbackAssetPaths(int gameId)
    {
        string assetsPath = appInstance.configManager.AssetsPath;
        var paths = new List<string>();

        foreach (string extension in new[] { ".png", ".jpg", ".webp" })
        {
            paths.Add(System.IO.Path.Combine(assetsPath, "covers_fallback", $"{gameId}{extension}"));
        }

        return paths;
    }

    private List<string> DetailsPanelAssetPaths(int gameId)
    {
        string assetsPath = appInstance.configManager.AssetsPath;

        var paths = new List<string>
        {
            CoverThreeDimensionalAssetPath(gameId),
            CoverTwoDimensionalAssetPath(gameId),
            MarqueeAssetPath(gameId)
        };

        paths.AddRange(CoverFallbackAssetPaths(gameId));
        paths.Add(System.IO.Path.Combine(assetsPath, "screenshots", $"{gameId}.jpg"));

        for (int screenshotIndex = 0; screenshotIndex < 5; screenshotIndex++)
        {
            paths.Add(System.IO.Path.Combine(assetsPath, "screenshots", $"{gameId}_{screenshotIndex}.jpg"));
        }

        return paths;
    }

    private readonly List<string> pendingScreenshotPaths = new List<string>();
    private readonly HashSet<string> displayedScreenshotPaths = new HashSet<string>();
    private int pendingScreenshotsGameId = -1;

    private void BeginDetailsScreenshotsLoad(Game game)
    {
        if (pendingScreenshotsGameId != game.Id)
        {
            pendingScreenshotsGameId = game.Id;
            pendingScreenshotPaths.Clear();
            displayedScreenshotPaths.Clear();

            if (mainScene.gameScreenshotsFlow != null)
            {
                foreach (Node child in mainScene.gameScreenshotsFlow.GetChildren())
                {
                    mainScene.gameScreenshotsFlow.RemoveChild(child);
                    child.QueueFree();
                }
            }

            pendingScreenshotPaths.AddRange(DetailsPanelAssetPaths(game.Id));
            return;
        }

        foreach (string path in DetailsPanelAssetPaths(game.Id))
        {
            if (displayedScreenshotPaths.Contains(path) || pendingScreenshotPaths.Contains(path))
            {
                continue;
            }

            pendingScreenshotPaths.Add(path);
        }
    }

    public void ProcessPendingScreenshotLoads()
    {
        if (pendingScreenshotPaths.Count == 0)
        {
            return;
        }

        if (currentlySelectedGame == null || currentlySelectedGame.Id != pendingScreenshotsGameId)
        {
            pendingScreenshotPaths.Clear();
            return;
        }

        foreach (string path in pendingScreenshotPaths)
        {
            RequestScreenshotThumbnail(path);
        }

        while (pendingScreenshotPaths.Count > 0)
        {
            string path = pendingScreenshotPaths[0];
            ImageTexture thumbnail = GetOrBuildScreenshotThumbnail(path);

            if (thumbnail == null && !failedScreenshotPaths.Contains(path))
            {
                break;
            }

            pendingScreenshotPaths.RemoveAt(0);

            if (thumbnail != null)
            {
                AppendScreenshotToFlow(path, thumbnail);
            }
        }
    }

    private void AppendScreenshotToFlow(string path, ImageTexture texture)
    {
        if (mainScene.gameScreenshotsFlow == null)
        {
            return;
        }

        float aspectRatio = (float)texture.GetWidth() / texture.GetHeight();
        float targetWidth = ScreenshotThumbnailWidth;
        float targetHeight = targetWidth / aspectRatio;

        var textureRect = new TextureRect {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = new Vector2(targetWidth, targetHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };

        mainScene.gameScreenshotsFlow.AddChild(textureRect);
        displayedScreenshotPaths.Add(path);
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

    public GameActionState ResolveGameAction(Game game)
    {
        if (game == null)
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = "Play", Disabled = true };
        }

        string mappedEmulator = appInstance.emulatorManager.GetMappedEmulator(game.PlatformSlug);

        if (string.IsNullOrEmpty(mappedEmulator))
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = "No Emulator For This System", Disabled = true };
        }

        string emulatorDisplayName = appInstance.emulatorManager.GetEmulatorDisplayName(mappedEmulator);

        if (appInstance.emulatorManager.IsEmulatorInstalling(mappedEmulator))
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = $"Installing {emulatorDisplayName}...", Disabled = true };
        }

        if (appInstance.emulatorManager.IsEmulatorLaunching)
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = "Starting...", Disabled = true };
        }

        if (appInstance.emulatorManager.IsEmulatorRunning)
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = "Running", Disabled = true };
        }

        if (appInstance.downloadManager.IsDownloadingGame(game.Id.ToString()))
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = "Downloading...", Disabled = true };
        }

        if (!appInstance.emulatorManager.CanLaunchGamesOnThisPlatform(mappedEmulator))
        {
            return new GameActionState { Kind = GameActionKind.Unavailable, Label = "Not on Android Yet", Disabled = true, EmulatorName = mappedEmulator };
        }

        if (!appInstance.emulatorManager.IsEmulatorInstalled(mappedEmulator))
        {
            return new GameActionState { Kind = GameActionKind.InstallEmulator, Label = $"Install {emulatorDisplayName}", Disabled = false, EmulatorName = mappedEmulator };
        }

        if (!CheckIfGameIsDownloaded(game))
        {
            return new GameActionState { Kind = GameActionKind.DownloadGame, Label = "Download", Disabled = false, EmulatorName = mappedEmulator };
        }


        if (!appInstance.emulatorManager.IsSelectedCoreInstalled(mappedEmulator, game.PlatformSlug))
        {
            return new GameActionState { Kind = GameActionKind.InstallEmulator, Label = $"Repair {emulatorDisplayName}", Disabled = false, EmulatorName = mappedEmulator };
        }

        return new GameActionState { Kind = GameActionKind.LaunchGame, Label = "Play", Disabled = false, EmulatorName = mappedEmulator };
    }

    public void UpdateDetailsPanelButtons(Game game)
    {
        bool isGameDownloadedLocally = CheckIfGameIsDownloaded(game);

        if (mainScene.installedIcon != null)
        {
            mainScene.installedIcon.Visible = isGameDownloadedLocally;
        }

        bool isInLobby = mainScene.NetplayHandler != null && mainScene.NetplayHandler.IsLobbyVisible;
        bool isChoosingLobbyGame = isInLobby && mainScene.NetplayHandler.IsBrowsingForLobbyGame;

        if (mainScene.actionBtn != null)
        {
            if (isInLobby)
            {
                mainScene.actionBtn.Text = isChoosingLobbyGame ? "Set Game" : "Select";
                mainScene.actionBtn.Disabled = false;
            }

            else
            {
                var gameAction = ResolveGameAction(game);
                mainScene.actionBtn.Text = gameAction.Label;
                mainScene.actionBtn.Disabled = gameAction.Disabled;
            }
        }

        if (mainScene.filterInstalledGamesBtn != null)
        {
            bool showsLobbyReturn = isInLobby && !isChoosingLobbyGame;

            mainScene.filterInstalledGamesBtn.Text = showsLobbyReturn
                ? "Change Game"
                : (showOnlyInstalledGames ? "Installed" : "All Games");

            mainScene.filterInstalledGamesBtn.Disabled = false;
        }

        if (mainScene.deleteBtn != null)
        {
            mainScene.deleteBtn.Disabled = isInLobby
                || !isGameDownloadedLocally
                || appInstance.emulatorManager.IsEmulatorLaunching
                || appInstance.emulatorManager.IsEmulatorRunning;
        }
    }

    public void OnJumpSectionRequested(int direction)
    {
        if (currentlyShownGames == null || currentlyShownGames.Count == 0 || mainScene.ActiveGameList == null)
        {
            return;
        }

        int currentIndex = mainScene.ActiveGameList.SelectedIndex;

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
            mainScene.ActiveGameList.SelectIndex(targetIndex, true);
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
        try
        {
            return AsyncImageLoader.Decode(path);
        }

        catch (Exception)
        {
            return null;
        }
    }

    public ImageTexture SafeLoadTexture(string path)
    {
        Image img = SafeLoadImage(path);
        return img != null ? ImageTexture.CreateFromImage(img) : null;
    }
}

public enum GameActionKind
{
    Unavailable,
    InstallEmulator,
    DownloadGame,
    LaunchGame
}

public class GameActionState
{
    public GameActionKind Kind { get; set; }

    public string Label { get; set; }

    public bool Disabled { get; set; }

    public string EmulatorName { get; set; }
}
