using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class MainScene : Control
{
    [ExportGroup("System Carousel")]
    [Export] private ScrollContainer _systemCarouselScroll;
    [Export] private HBoxContainer _systemCarouselContainer;

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

    private BackendManager _backendManager;
    private ConfigManager _configManager;
    private IBackend _activeBackend;

    private List<GameSystem> _systems = new List<GameSystem>();
    private List<Game> _games = new List<Game>();
    private Game _currentlySelectedGame;

    public override void _Ready()
    {
        _backendManager = GetNode<BackendManager>("/root/BackendManager");
        _configManager = GetNode<ConfigManager>("/root/ConfigManager");
        _activeBackend = _backendManager.ActiveBackend;

        if (_activeBackend == null)
        {
            GetTree().ChangeSceneToFile("res://Scenes/login_screen.tscn");
            return;
        }

        if (_gameList != null)
        {
            _gameList.ItemSelected += OnGameSelected;
        }

        if (_playDownloadButton != null)
        {
            _playDownloadButton.Pressed += OnPlayDownloadButtonPressed;
        }

        if (_deleteButton != null)
        {
            _deleteButton.Pressed += OnDeleteButtonPressed;
        }

        if (_uploadButton != null)
        {
            _uploadButton.Pressed += OnUploadButtonPressed;
        }

        LoadSystems();
    }

    private async void LoadSystems()
    {
        _systems = await _activeBackend.GetSystemsAsync();
        
        PopulateSystemCarousel();
    }

    private void PopulateSystemCarousel()
    {
        if (_systemCarouselContainer == null) return;

        foreach (Node child in _systemCarouselContainer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var system in _systems)
        {
            Button systemButton = new Button();
            systemButton.Text = system.Name;
            
            GameSystem capturedSystem = system;
            systemButton.Pressed += () => OnSystemSelected(capturedSystem);
            
            _systemCarouselContainer.AddChild(systemButton);
        }
    }

    private async void OnSystemSelected(GameSystem system)
    {
        await LoadGames(system.Id);
    }

    private async Task LoadGames(string systemId)
    {
        _games = await _activeBackend.GetGamesAsync(systemId);
        
        if (_gameList == null) return;
        
        _gameList.Clear();
        foreach (var game in _games)
        {
            _gameList.AddItem(game.Name);
        }

        if (_gameDetailsPanel != null)
        {
            _gameDetailsPanel.Visible = false;
        }
        
        _currentlySelectedGame = null;
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

    private bool CheckIfGameIsDownloaded(Game game)
    {
        if (string.IsNullOrEmpty(game.LocalFilename))
        {
            return false;
        }
        
        string fullPath = _configManager.LocalRomsPath.PathJoin(game.LocalFilename);
        return FileAccess.FileExists(fullPath);
    }

    private bool CheckIfGameIsOnServer(Game game)
    {
        return true;
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

    private void LaunchGame(Game game)
    {
    }

    private void DownloadGame(Game game)
    {
    }

    private void DeleteLocalGame(Game game)
    {
    }

    private void UploadLocalGame(Game game)
    {
    }
}
