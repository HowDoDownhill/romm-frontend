using Godot;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

public partial class MainScene : Control
{
    [ExportGroup("Main Layout")]
    [Export] private MarginContainer _mainContentContainer;

    [ExportGroup("Platform Header")]
    [Export] private TextureRect _platformControllerIcon;
    [Export] private Label _platformNameLabel;

    [ExportGroup("Game List")]
    [Export] private ItemList _gameList;

    [ExportGroup("Game Details")]
    [Export] private Control _gameDetailsPanel;
    [Export] private TextureRect _gameCoverArt;
    [Export] private Label _gameTitleLabel;
    [Export] private RichTextLabel _gameDescriptionLabel;
    
    [ExportGroup("Action Buttons")]
    [Export] private Button _playDownloadButton;
    [Export] private Button _deleteButton;
    [Export] private Button _uploadButton;

    [ExportGroup("Downloads")]
    [Export] private Button _downloadsButton;
    [Export] private MarginContainer _downloadProgressContainer;
    private DownloadProgressUI _downloadProgressUi;

    [ExportGroup("Icons")]
    [Export] private Texture2D _downloadsIcon;

    private DataBus _dataBus;
    private ConfigManager _configManager;
    private DownloadManager _downloadManager;
    private IBackend _activeBackend;

    private List<GameSystem> _systems = new List<GameSystem>();
    private List<Game> _games = new List<Game>();
    private Dictionary<int, List<Game>> _gameCache = new Dictionary<int, List<Game>>();
    private Game _currentlySelectedGame;
    private int _currentlySelectedSystemIndex = -1;
    
    private enum FocusState
    {
        GameList,
        ActionButtons
    }
    
    private FocusState _currentFocusState = FocusState.GameList;

    public override void _Ready()
    {
        _dataBus = GetNode<DataBus>("/root/DataBus");
        _configManager = GetNode<ConfigManager>("/root/ConfigManager");
        _downloadManager = GetNode<DownloadManager>("/root/DownloadManager");
        _activeBackend = GetNode<BackendManager>("/root/BackendManager").ActiveBackend;

        if (_activeBackend == null)
        {
            GetTree().ChangeSceneToFile("res://Scenes/login_screen.tscn");
            return;
        }

        _systems = _dataBus.Systems;
        _gameCache = _dataBus.GameCache;

        StyleBoxEmpty emptyFocusStyle = new StyleBoxEmpty();

        if (_gameList != null)
        {
            _gameList.ItemSelected += OnGameSelected;
            _gameList.FocusMode = FocusModeEnum.All;
            _gameList.AddThemeStyleboxOverride("focus", emptyFocusStyle);
            _gameList.IconMode = ItemList.IconModeEnum.Left; 
            _gameList.AddThemeConstantOverride("icon_margin", 10);
            _gameList.FixedIconSize = new Vector2I(24, 24);
        }

        if (_playDownloadButton != null)
        {
            _playDownloadButton.Pressed += OnPlayDownloadButtonPressed;
            _playDownloadButton.FocusMode = FocusModeEnum.All;
        }

        if (_deleteButton != null)
        {
            _deleteButton.Pressed += OnDeleteButtonPressed;
            _deleteButton.FocusMode = FocusModeEnum.All;
        }

        if (_uploadButton != null)
        {
            _uploadButton.Pressed += OnUploadButtonPressed;
            _uploadButton.FocusMode = FocusModeEnum.All;
        }

        if (_downloadsButton != null)
        {
            _downloadsButton.Pressed += OnDownloadsButtonPressed;
        }

        if (_downloadProgressContainer != null)
        {
            _downloadProgressContainer.Visible = false;
            if (_downloadProgressContainer.GetChildCount() > 0)
            {
                _downloadProgressUi = _downloadProgressContainer.GetChild<DownloadProgressUI>(0);
            }
        }

        if (_mainContentContainer != null)
        {
            _mainContentContainer.Visible = true;
        }

        if (_gameDetailsPanel != null)
        {
            _gameDetailsPanel.ClipContents = true;
        }

        if (_gameCoverArt != null)
        {
            _gameCoverArt.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _gameCoverArt.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }

        if (_gameTitleLabel != null)
        {
            _gameTitleLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        }

        if (_systems.Any())
        {
            SelectSystemByIndex(0);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_systems.Count == 0) return;

        if (@event.IsActionPressed("MoveUp"))
        {
            HandleMoveUp();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("MoveDown"))
        {
            HandleMoveDown();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("Select"))
        {
            HandleSelect();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("Back"))
        {
            HandleBack();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("CylceSystemUp"))
        {
            _currentlySelectedSystemIndex = (_currentlySelectedSystemIndex + 1) % _systems.Count;
            SelectSystemByIndex(_currentlySelectedSystemIndex);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("CycleSystemDown"))
        {
            _currentlySelectedSystemIndex = (_currentlySelectedSystemIndex - 1 + _systems.Count) % _systems.Count;
            SelectSystemByIndex(_currentlySelectedSystemIndex);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleMoveUp()
    {
        if (_downloadProgressContainer != null && _downloadProgressContainer.Visible) return;

        if (_currentFocusState == FocusState.ActionButtons)
        {
            if (_uploadButton != null && _uploadButton.HasFocus() && _deleteButton != null && _deleteButton.Visible)
            {
                _deleteButton.GrabFocus();
            }
            else if (_deleteButton != null && _deleteButton.HasFocus() && _playDownloadButton != null && _playDownloadButton.Visible)
            {
                _playDownloadButton.GrabFocus();
            }
            else if (_uploadButton != null && _uploadButton.HasFocus() && _playDownloadButton != null && _playDownloadButton.Visible)
            {
                _playDownloadButton.GrabFocus();
            }
            else
            {
                _currentFocusState = FocusState.GameList;
                _gameList.GrabFocus();
            }
        }
    }

    private void HandleMoveDown()
    {
        if (_downloadProgressContainer != null && _downloadProgressContainer.Visible) return;

        if (_currentFocusState == FocusState.ActionButtons)
        {
            if (_playDownloadButton != null && _playDownloadButton.HasFocus())
            {
                if (_deleteButton != null && _deleteButton.Visible)
                {
                    _deleteButton.GrabFocus();
                }
                else if (_uploadButton != null && _uploadButton.Visible)
                {
                    _uploadButton.GrabFocus();
                }
            }
            else if (_deleteButton != null && _deleteButton.HasFocus() && _uploadButton != null && _uploadButton.Visible)
            {
                _uploadButton.GrabFocus();
            }
            else
            {
                _currentFocusState = FocusState.GameList;
                _gameList.GrabFocus();
            }
        }
    }

    private void HandleSelect()
    {
        if (_downloadProgressContainer != null && _downloadProgressContainer.Visible) return;

        if (_currentFocusState == FocusState.GameList)
        {
            if (_currentlySelectedGame != null)
            {
                _currentFocusState = FocusState.ActionButtons;
                
                if (_playDownloadButton != null && _playDownloadButton.Visible)
                {
                    _playDownloadButton.GrabFocus();
                }
                else if (_deleteButton != null && _deleteButton.Visible)
                {
                    _deleteButton.GrabFocus();
                }
                else if (_uploadButton != null && _uploadButton.Visible)
                {
                    _uploadButton.GrabFocus();
                }
            }
        }
        else if (_currentFocusState == FocusState.ActionButtons)
        {
            if (_playDownloadButton != null && _playDownloadButton.HasFocus())
            {
                OnPlayDownloadButtonPressed();
            }
            else if (_deleteButton != null && _deleteButton.HasFocus())
            {
                OnDeleteButtonPressed();
            }
            else if (_uploadButton != null && _uploadButton.HasFocus())
            {
                OnUploadButtonPressed();
            }
        }
    }

    private void HandleBack()
    {
        if (_downloadProgressContainer != null && _downloadProgressContainer.Visible)
        {
            OnDownloadsButtonPressed();
            return;
        }

        if (_currentFocusState == FocusState.ActionButtons)
        {
            _currentFocusState = FocusState.GameList;
            if (_gameList != null)
            {
                _gameList.GrabFocus();
            }
        }
    }

    private Texture2D FindTextureByStub(string stub, string basePath, string[] extensions)
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

    private void SelectSystemByIndex(int index)
    {
        if (index < 0 || index >= _systems.Count) return;

        _currentlySelectedSystemIndex = index;
        var selectedSystem = _systems[index];
        
        if (_platformNameLabel != null)
        {
            _platformNameLabel.Text = selectedSystem.Name;
        }

        if (_platformControllerIcon != null)
        {
            string searchSlug = !string.IsNullOrEmpty(selectedSystem.IgdbSlug) ? selectedSystem.IgdbSlug : selectedSystem.Slug;
            if (!string.IsNullOrEmpty(searchSlug))
            {
                var texture = FindTextureByStub(searchSlug, "res://assets/platforms/systematic/", new[] { ".svg", ".png" });
                if (texture == null)
                {
                     texture = FindTextureByStub(searchSlug, "res://assets/platforms/", new[] { ".svg", ".png" });
                }
                _platformControllerIcon.Texture = texture;
            }
            else
            {
                _platformControllerIcon.Texture = null;
            }
        }

        OnSystemSelected(selectedSystem);
    }

    private void OnSystemSelected(GameSystem system)
    {
        if (_gameList == null) return;

        _gameList.Clear();
        _gameDetailsPanel.Visible = false;
        _currentlySelectedGame = null;
        _currentFocusState = FocusState.GameList;

        if (_gameCache.TryGetValue(system.Id, out List<Game> cachedGames))
        {
            _games = cachedGames;
            RefreshGameListUi();

            if (_games.Any())
            {
                _gameList.Select(0);
                OnGameSelected(0);
                if (_downloadProgressContainer == null || !_downloadProgressContainer.Visible)
                {
                    _gameList.GrabFocus();
                }
            }
        }
        else
        {
            _games = new List<Game>();
            GD.Print($"No games found in cache for system {system.Name}");
        }
    }

    private void OnGameSelected(long index)
    {
        if (index < 0 || index >= _games.Count) return;
        
        _currentlySelectedGame = _games[(int)index];
        ShowGameDetails(_currentlySelectedGame);
    }

    private void ShowGameDetails(Game game)
    {
        if (_gameDetailsPanel == null) return;
        
        _gameDetailsPanel.Visible = true;
        
        if (_gameTitleLabel != null) _gameTitleLabel.Text = game.Name;
        if (_gameDescriptionLabel != null) _gameDescriptionLabel.Text = game.Description;
        
        if (_gameCoverArt != null)
        {
            string coverUrl = null;

            if (_activeBackend is RomMAPI romMapi && !string.IsNullOrEmpty(game.PathCoverLarge))
            {
                string cleanPath = game.PathCoverLarge.StartsWith("/") ? game.PathCoverLarge.Substring(1) : game.PathCoverLarge;
                coverUrl = $"{romMapi.ApiHost}/{cleanPath}".Replace(" ", "%20");
            }
            else if (!string.IsNullOrEmpty(game.CoverArtUrl))
            {
                coverUrl = game.CoverArtUrl.Replace(" ", "%20");
            }

            if (!string.IsNullOrEmpty(coverUrl))
            {
                DownloadAndSetTexture(coverUrl, _gameCoverArt);
            }
            else
            {
                _gameCoverArt.Texture = null;
            }
        }
        
        UpdateButtonStates(game);
    }

    private void UpdateButtonStates(Game game)
    {
        bool isGameDownloadedLocally = CheckIfGameIsDownloaded(game);
        bool isGameStoredOnServer = CheckIfGameIsOnServer(game);

        if (_playDownloadButton != null)
        {
            _playDownloadButton.Visible = true;
            _playDownloadButton.Text = isGameDownloadedLocally ? "Play" : "Download";
        }

        if (_deleteButton != null)
        {
            _deleteButton.Visible = isGameDownloadedLocally;
        }

        if (_uploadButton != null)
        {
            _uploadButton.Visible = isGameDownloadedLocally && !isGameStoredOnServer;
        }
    }

    private void RefreshGameListUi()
    {
        if (_gameList == null) return;

        _gameList.Clear();

        Texture2D systemControllerIcon = null;
        if (_currentlySelectedSystemIndex >= 0 && _currentlySelectedSystemIndex < _systems.Count)
        {
            var system = _systems[_currentlySelectedSystemIndex];
            string searchSlug = !string.IsNullOrEmpty(system.IgdbSlug) ? system.IgdbSlug : system.Slug;
            if (!string.IsNullOrEmpty(searchSlug))
            {
                systemControllerIcon = FindTextureByStub(searchSlug, "res://assets/platforms/", new[] { ".svg", ".png" });
            }
        }

        for (int i = 0; i < _games.Count; i++)
        {
            var game = _games[i];
            _gameList.AddItem(game.Name);
            if (CheckIfGameIsDownloaded(game))
            {
                if (systemControllerIcon != null)
                {
                    _gameList.SetItemIcon(i, systemControllerIcon);
                }
            }
            else
            {
                _gameList.SetItemIcon(i, null);
            }
        }
    }

    private bool CheckIfGameIsDownloaded(Game game)
    {
        if (game.Files == null || !game.Files.Any()) return false;
        
        string fileName = game.Files[0].FileName;
        string fullPath = _configManager.LocalRomsPath.PathJoin(game.System.Slug).PathJoin(fileName);
        return Godot.FileAccess.FileExists(fullPath);
    }

    private bool CheckIfGameIsOnServer(Game game)
    {
        return true;
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
        if (_currentlySelectedGame == null) return;

        bool isGameDownloadedLocally = CheckIfGameIsDownloaded(_currentlySelectedGame);

        if (isGameDownloadedLocally)
        {
            LaunchGame(_currentlySelectedGame);
        }
        else
        {
            DownloadGame(_currentlySelectedGame);
        }
    }

    private void OnDeleteButtonPressed()
    {
        if (_currentlySelectedGame == null) return;
        DeleteLocalGame(_currentlySelectedGame);
    }

    private void OnUploadButtonPressed()
    {
        if (_currentlySelectedGame == null) return;
        UploadLocalGame(_currentlySelectedGame);
    }

    private void OnDownloadsButtonPressed()
    {
        if (_downloadProgressContainer != null && _mainContentContainer != null)
        {
            bool showDownloads = !_downloadProgressContainer.Visible;
            _downloadProgressContainer.Visible = showDownloads;
            _mainContentContainer.Visible = !showDownloads;

            if (showDownloads)
            {
                if (_platformNameLabel != null) _platformNameLabel.Text = "Downloads";
                if (_platformControllerIcon != null) _platformControllerIcon.Texture = _downloadsIcon;
            }
            else
            {
                SelectSystemByIndex(_currentlySelectedSystemIndex);
            }
        }
    }

    private void LaunchGame(Game game)
    {
    }

    private void DownloadGame(Game game)
    {
        if (game == null || game.Files == null || !game.Files.Any())
        {
            GD.PrintErr($"No files found for game: {game?.Name}");
            return;
        }
        
        string downloadUrl = _activeBackend.GetRomDownloadUrl(game);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            GD.PrintErr($"Could not get download URL for game: {game.Name}");
            return;
        }

        string tempZipName = game.Files[0].FileName;
        string tempZipPath = _configManager.DownloadsPath.PathJoin(tempZipName);
        
        string baseDir = tempZipPath.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(baseDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(baseDir);
        }
        
        GD.Print($"Starting download for {game.Name} from {downloadUrl} to temporary file {tempZipPath}");
        _downloadManager.DownloadFile(downloadUrl, tempZipPath, _activeBackend.GetAuthHeaders(), (path) => HandleRomDownloadCompletion(path, game));
    }

    private void HandleRomDownloadCompletion(string tempZipPath, Game game)
    {
        string fileName = tempZipPath.GetFile();
        if (_downloadProgressUi != null)
        {
            _downloadProgressUi.SetDownloadStatus(fileName, "Extracting...");
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
                        string finalDir = _configManager.LocalRomsPath.PathJoin(game.System.Slug);
                        string finalPath = finalDir.PathJoin(finalFileName);

                        if (!DirAccess.DirExistsAbsolute(finalDir))
                        {
                            DirAccess.MakeDirRecursiveAbsolute(finalDir);
                        }
                        
                        entry.ExtractToFile(ProjectSettings.GlobalizePath(finalPath), true);
                        GD.Print($"Extracted {entry.FullName} to {finalPath}");
                        
                        UpdateButtonStates(game);
                        RefreshGameListUi();
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
    }

    private void DeleteLocalGame(Game game)
    {
    }

    private void UploadLocalGame(Game game)
    {
    }
}
