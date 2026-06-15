using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileAccess = System.IO.FileAccess;

public partial class LoadingScreen : Control
{
    [Export] private ProgressBar _progressBar;
    [Export] private Label _statusLabel;

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        
        AttemptLoadFromCacheAsync();
    }

    private async void AttemptLoadFromCacheAsync()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Checking cache...";
        }

        var (cachedSystems, cachedGames) = appInstance.cacheManager.LoadCache();

        if (cachedSystems != null && cachedSystems.Any() && cachedGames != null && cachedGames.Any())
        {
            foreach (var system in cachedSystems)
            {
                if (cachedGames.TryGetValue(system.Id, out var games))
                {
                    foreach (var game in games)
                    {
                        game.System = system;
                    }
                }
            }
            
            appInstance.dataBus.systems = cachedSystems;
            appInstance.dataBus.gameCache = cachedGames;
            
            if (_statusLabel != null)
            {
                _statusLabel.Text = "Loaded from cache!";
            }
            if (_progressBar != null)
            {
                _progressBar.Value = 100;
            }

            // Sync BIOS files before changing scenes
            await SyncFirmwareAsync();

            await Task.Delay(200);
            GetTree().ChangeSceneToFile("res://scenes/main_scene.tscn");
        }
        else
        {
            PreloadDataAsync();
        }
    }

    private async void PreloadDataAsync()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Loading systems...";
        }

        List<GameSystem> systems = await appInstance.rommApi.GetSystemsAsync();
        appInstance.dataBus.systems = systems;

        if (systems == null || !systems.Any())
        {
            if (_statusLabel != null) _statusLabel.Text = "No systems found.";
            await Task.Delay(1000);
            GetTree().ChangeSceneToFile("res://scenes/main_scene.tscn");
            return;
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = "Loading games...";
        }

        appInstance.dataBus.gameCache.Clear();
        int systemsProcessed = 0;

        foreach (var system in systems)
        {
            List<Game> allGamesForSystem = new List<Game>();
            int currentPage = 1;
            const int chunkSize = 100;
            bool hasMoreGames = true;

            while (hasMoreGames)
            {
                GameResponse gameResponse = await appInstance.rommApi.GetGamesAsync(system, currentPage, chunkSize);
                
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
            
            appInstance.dataBus.gameCache[system.Id] = allGamesForSystem;
            
            systemsProcessed++;
            if (_progressBar != null)
            {
                _progressBar.Value = ((float)systemsProcessed / systems.Count) * 100;
            }
        }
        
        appInstance.cacheManager.SaveCache(appInstance.dataBus.systems, appInstance.dataBus.gameCache);
        
        // Sync BIOS files before changing scenes
        await SyncFirmwareAsync();
        
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Finished!";
        }
        
        await Task.Delay(200);
        GetTree().ChangeSceneToFile("res://scenes/main_scene.tscn");
    }

    private async Task SyncFirmwareAsync()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Checking for BIOS updates...";
        }

        // Fetch the metadata list of all firmware
        List<Firmware> firmwares = await appInstance.rommApi.GetFirmwareAsync();

        if (firmwares == null || !firmwares.Any())
        {
            return;
        }

        int processed = 0;
        foreach (var fw in firmwares)
        {
            string savePath = Path.Combine(appInstance.configManager.BiosPath, fw.FileName);

            // If we don't have this BIOS file locally, download it
            if (!File.Exists(savePath))
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = $"Downloading BIOS: {fw.FileName}...";
                }

                string downloadUrl = appInstance.rommApi.GetFirmwareDownloadUrl(fw);
                await DownloadFileAsync(downloadUrl, savePath);
            }

            processed++;
            if (_progressBar != null)
            {
                _progressBar.Value = ((float)processed / firmwares.Count) * 100;
            }
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            
            // Re-apply the authentication headers to the new HttpClient
            var authHeaders = appInstance.rommApi.GetAuthHeaders();
            foreach (var header in authHeaders)
            {
                var split = header.Split(": ", 2);
                if (split.Length == 2)
                {
                    client.DefaultRequestHeaders.Add(split[0], split[1]);
                }
            }

            var response = await client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            
            if (response.IsSuccessStatusCode)
            {
                using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
            else
            {
                GD.PrintErr($"Failed to download firmware from {url}. Status code: {response.StatusCode}");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Exception during firmware download: {e.Message}");
        }
    }
}