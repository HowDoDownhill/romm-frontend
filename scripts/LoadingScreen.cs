using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class LoadingScreen : Control
{
    [Export] private ProgressBar _progressBar;
    [Export] private Label _statusLabel;

    private BackendManager _backendManager;
    private DataBus _dataBus;

    public override void _Ready()
    {
        _backendManager = GetNode<BackendManager>("/root/BackendManager");
        _dataBus = GetNode<DataBus>("/root/DataBus");
        
        if (_backendManager.ActiveBackend == null)
        {
            GetTree().ChangeSceneToFile("res://Scenes/login_screen.tscn");
            return;
        }

        PreloadDataAsync();
    }

    private async void PreloadDataAsync()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Loading systems...";
        }

        IBackend activeBackend = _backendManager.ActiveBackend;

        List<GameSystem> systems = await activeBackend.GetSystemsAsync();
        _dataBus.Systems = systems;

        if (systems == null || !systems.Any())
        {
            if (_statusLabel != null) _statusLabel.Text = "No systems found.";
            await Task.Delay(1000);
            GetTree().ChangeSceneToFile("res://Scenes/main_scene.tscn");
            return;
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = "Loading games...";
        }

        _dataBus.GameCache.Clear();
        int systemsProcessed = 0;

        foreach (var system in systems)
        {
            List<Game> allGamesForSystem = new List<Game>();
            int currentPage = 1;
            const int chunkSize = 100;
            bool hasMoreGames = true;

            while (hasMoreGames)
            {
                GameResponse gameResponse = await activeBackend.GetGamesAsync(system, currentPage, chunkSize);
                
                if (gameResponse != null && gameResponse.Games != null && gameResponse.Games.Any())
                {
                    foreach(var game in gameResponse.Games)
                    {
                        game.System = system;
                        allGamesForSystem.Add(game);
                    }
                    
                    hasMoreGames = allGamesForSystem.Count < gameResponse.Total;
                    currentPage++;
                }
                else
                {
                    hasMoreGames = false;
                }
            }
            
            _dataBus.GameCache[system.Id] = allGamesForSystem;
            
            systemsProcessed++;
            if (_progressBar != null)
            {
                _progressBar.Value = ((float)systemsProcessed / systems.Count) * 100;
            }
        }
        
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Finished!";
        }
        
        await Task.Delay(200);
        GetTree().ChangeSceneToFile("res://Scenes/main_scene.tscn");
    }
}
