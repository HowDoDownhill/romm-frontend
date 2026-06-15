using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

public partial class MainScene : Control
{
    //Header
    [ExportGroup("Header")] 
    [Export] private MarginContainer headerContainer;
    [Export] private Label platformLabel;
    [Export] private TextureRect platformIcon;

    private List<GameSystem> gameSystems = new List<GameSystem>();
    public Dictionary<int, List<Game>> games { get; set; } = new Dictionary<int, List<Game>>();
    private List<Game> currentlyShownGames = new List<Game>();
    private int currentGameSystemIndex;
    public Game currentlySelectedGame; 
    
    //Game list / Details Panel 
    [ExportGroup("GameList")] 
    [Export] private MarginContainer gameListContainer;
    [Export] private ItemList gameList;
    
    [ExportGroup("DetailsPanel")]
    [Export] private VBoxContainer detailsPanelContainer;
    [Export] private TextureRect gameCover;
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
    [Export] private Button downloadsPageToggle;
    [Export] private Button refreshGamesButton;

    //Global access to other systems
    private AppInstance appInstance;
    

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.downloadManager.DownloadCompleted += OnDownloadCompleted; 
        
        appInstance.emulatorManager.mainScene = this;
        
        GetCache();
        SelectSystemByIndex(0);
        SetupGameList();
        SetupDownloadsList();
        SetupButtonBindings();
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
            gameList.ItemSelected += OnGameSelected;
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
        if (downloadsListContainer != null && gameListContainer != null)
        {
            downloadsListContainer.Visible = !downloadsListContainer.Visible;
            gameListContainer.Visible = !gameListContainer.Visible;
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
                var texture = FindPlatformIcon(selectedSystem.IgdbSlug, "res://assets/platforms/", new[] { ".svg", ".png" });
                platformIcon.Texture = texture;
            }
            
            else if (platformIcon.Texture == null)
            {
                var texture = FindPlatformIcon(selectedSystem.Slug, "res://assets/platforms/", new[] { ".svg", ".png" });
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

        gameList.Clear();
        currentlySelectedGame = null;

        if (games.TryGetValue(system.Id, out List<Game> cachedGames))
        {
            currentlyShownGames = cachedGames;
            RefreshGameList();

            if (currentlyShownGames.Any())
            {
                gameList.Select(0);
                OnGameSelected(0);
                if (downloadsListContainer is not { Visible: true })
                {
                    gameList.GrabFocus();
                }
            }
        }
        else
        {
            currentlyShownGames = new List<Game>();
            GD.Print($"No games found in cache for system {system.Name}");
        }
    }

    public void RefreshGameList()
    {
        if (gameList == null) return;

        gameList.Clear();

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
            gameList.AddItem(game.Name);
            if (CheckIfGameIsDownloaded(game))
            {
                if (systemControllerIcon != null)
                {
                    gameList.SetItemIcon(i, systemControllerIcon);
                }
            }
            else
            {
                gameList.SetItemIcon(i, null);
            }
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
        
        if (gameTitle != null) gameTitle.Text = game.Name;
        if (gameDescription != null) gameDescription.Text = game.Description;
        
        if (gameCover != null)
        {
            string coverUrl = null;

            if (appInstance.rommApi != null && !string.IsNullOrEmpty(game.PathCoverLarge))
            {
                string cleanPath = game.PathCoverLarge.StartsWith("/") ? game.PathCoverLarge.Substring(1) : game.PathCoverLarge;
                coverUrl = $"{appInstance.rommApi.ApiHost}/{cleanPath}".Replace(" ", "%20");
            }
            else if (!string.IsNullOrEmpty(game.CoverArtUrl))
            {
                coverUrl = game.CoverArtUrl.Replace(" ", "%20");
            }

            if (!string.IsNullOrEmpty(coverUrl))
            {
                DownloadAndSetTexture(coverUrl, gameCover);
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
    
    private void DownloadAndSetTexture(string url, TextureRect textureRect)
    {
        HttpRequest httpRequest = new HttpRequest();
        AddChild(httpRequest);
        
        httpRequest.RequestCompleted += (long result, long responseCode, string[] headers, byte[] body) => 
        {
            if (result == (long)HttpRequest.Result.Success && responseCode == 200 && body != null && body.Length > 0)
            {
                string contentType = "";
                foreach (string header in headers)
                {
                    if (header.StartsWith("Content-Type", System.StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = header.Split(':')[1].Trim().ToLower();
                        break;
                    }
                }

                Image image = new Image();
                Error error = Error.Failed;

                if (contentType.Contains("jpeg") || contentType.Contains("jpg"))
                {
                    error = image.LoadJpgFromBuffer(body);
                }
                else if (contentType.Contains("png"))
                {
                    error = image.LoadPngFromBuffer(body);
                }
                else if (contentType.Contains("webp"))
                {
                    error = image.LoadWebpFromBuffer(body);
                }
                else
                {
                    error = image.LoadJpgFromBuffer(body);
                    if (error != Error.Ok) error = image.LoadPngFromBuffer(body);
                    if (error != Error.Ok) error = image.LoadWebpFromBuffer(body);
                }

                if (error == Error.Ok)
                {
                    ImageTexture texture = ImageTexture.CreateFromImage(image);
                    textureRect.Texture = texture;
                }
                else
                {
                    GD.PrintErr($"Failed to create image from downloaded data. URL: {url}, Error: {error}");
                }
            }
            else
            {
                GD.PrintErr($"Failed to download image. URL: {url}, Response Code: {responseCode}, Result: {result}, Body Length: {(body != null ? body.Length.ToString() : "null")}");
            }
            httpRequest.QueueFree();
        };

        httpRequest.Request(url);
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
            appInstance.emulatorManager.LaunchEmulator(currentlySelectedGame);
        }
        else
        {
            DownloadGame(currentlySelectedGame);
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
