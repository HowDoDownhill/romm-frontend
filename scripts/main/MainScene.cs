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
    [Export] private Container gameList;
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
    

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.downloadManager.DownloadCompleted += OnDownloadCompleted; 
        
        appInstance.emulatorManager.mainScene = this;
        appInstance.assetManager.AssetDownloaded += OnAssetDownloaded;
        
        GetCache();
        SelectSystemByIndex(0);
        SetupGameList();
        SetupDownloadsList();
        SetupButtonBindings();
        SetupFirmwareSelector();
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
        if(@event.IsActionPressed("CylceSystemUp"))
        {
            CycleSelectedSystemNext();
            return;
        }
        
        if (@event.IsActionPressed("CycleSystemDown"))
        {
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
        // Removed ItemList signals since we are using a standard container
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
            gameList.Visible = !gameList.Visible;
        }
    }

    public void CycleSelectedSystemNext()
    {
        if (currentGameSystemIndex == gameSystems.Count - 1)
        {
            SelectSystemByIndex(0);
        }
        else
        {
            SelectSystemByIndex(currentGameSystemIndex + 1);
        }
    }

    public void CycleSelectedSystemLast()
    {
        if (currentGameSystemIndex == 0)
        {
            SelectSystemByIndex(gameSystems.Count - 1);
        }
        
        else
        {
            SelectSystemByIndex(currentGameSystemIndex - 1);
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
            
            else if (platformIcon.Texture == null)
            {
                var texture = FindPlatformIcon(selectedSystem.Slug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
                platformIcon.Texture = texture;
            }
            
            else
            {
                platformIcon.Texture = null;
            }
        }

        GD.Print(selectedSystem.Slug);
        GD.Print(selectedSystem.IgdbSlug);
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

            if (currentlyShownGames.Any() && gameList.GetChildCount() > 0)
            {
                OnGameSelected(0L);
                if (downloadsListContainer is not { Visible: true })
                {
                    if (gameList.GetChild(0) is Control firstChild)
                    {
                        firstChild.GrabFocus();
                    }
                }
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

            // Handle selection and focus
            int index = i;
            entry.GuiInput += (InputEvent @event) => 
            {
                if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
                {
                    entry.GrabFocus();
                    OnGameSelected((long)index);
                }
                else if (@event.IsActionPressed("ui_accept"))
                {
                    OnGameSelected((long)index);
                    if (playDownloadButton != null && playDownloadButton.Visible && !playDownloadButton.Disabled)
                    {
                        playDownloadButton.GrabFocus();
                    }
                    entry.AcceptEvent();
                }
                else if (@event.IsActionPressed("ui_up") || @event.IsActionPressed("ui_down") || @event.IsActionPressed("ui_left") || @event.IsActionPressed("ui_right"))
                {
                    Side side = Side.Bottom;
                    if (@event.IsActionPressed("ui_up")) side = Side.Top;
                    if (@event.IsActionPressed("ui_down")) side = Side.Bottom;
                    if (@event.IsActionPressed("ui_left")) side = Side.Left;
                    if (@event.IsActionPressed("ui_right")) side = Side.Right;

                    Control nextFocus = entry.FindValidFocusNeighbor(side);
                    
                    if (nextFocus != null && !gameList.IsAncestorOf(nextFocus))
                    {
                        entry.AcceptEvent();
                    }
                    else if (nextFocus == null)
                    {
                        entry.AcceptEvent();
                    }
                }
            };

            entry.FocusEntered += () => 
            {
                focusPanel.Visible = true;
                entry.ZIndex = 1;
                OnGameSelected((long)index);
            };

            entry.FocusExited += () => 
            {
                focusPanel.Visible = false;
                entry.ZIndex = 0;
            };

            // Set up fallback covers for the list item
            Label titleLabel = entry.GetNode<Label>("TitleLabel");
            titleLabel.Text = game.Name;

            void TryLoadImage()
            {
                string assetsPath = appInstance.configManager.AssetsPath;
                string path3d = System.IO.Path.Combine(assetsPath, "covers_3d", $"{game.Id}.png");
                string path2d = System.IO.Path.Combine(assetsPath, "covers_2d", $"{game.Id}.png");

                if (Godot.FileAccess.FileExists(path3d))
                {
                    entry.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(path3d));
                    titleLabel.Visible = false;
                }
                else if (Godot.FileAccess.FileExists(path2d))
                {
                    entry.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(path2d));
                    titleLabel.Visible = false;
                }
                else if (CheckIfGameIsDownloaded(game) && systemControllerIcon != null)
                {
                    entry.Texture = systemControllerIcon;
                }
            }

            TryLoadImage();

            // Subscribe to asset downloads in background
            AssetManager.AssetDownloadedEventHandler onAssetDownloaded = null;
            onAssetDownloaded = (downloadedGameId, assetType) =>
            {
                if (downloadedGameId == game.Id && (assetType == "box3d" || assetType == "box2d"))
                {
                    TryLoadImage();
                }
            };
            appInstance.assetManager.AssetDownloaded += onAssetDownloaded;

            entry.TreeExiting += () => 
            {
                appInstance.assetManager.AssetDownloaded -= onAssetDownloaded;
            };

            gameList.AddChild(entry);
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
            
            if (Godot.FileAccess.FileExists(pathMarquee))
            {
                gameMarquee.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(pathMarquee));
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

            if (Godot.FileAccess.FileExists(path3d))
            {
                gameCover.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(path3d));
            }
            else if (Godot.FileAccess.FileExists(path2d))
            {
                gameCover.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(path2d));
            }
            else
            {
                gameCover.Texture = null;
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
            File.Delete(file.FullPath);
        }
    }
}
