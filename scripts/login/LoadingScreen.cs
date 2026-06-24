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

            await SyncFirmwareAsync();
            await PopulateAvailableFirmwareAsync();


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
            GD.Print($"Found {allGamesForSystem.Count} games for {system.Name}");
            
            systemsProcessed++;
            if (_progressBar != null)
            {
                _progressBar.Value = ((float)systemsProcessed / systems.Count) * 100;
            }
        }
        
        GD.Print($"Saving {appInstance.dataBus.gameCache.Sum(x => x.Value.Count)} games to cache.");
        appInstance.cacheManager.SaveCache(appInstance.dataBus.systems, appInstance.dataBus.gameCache);
        
        await SyncFirmwareAsync();
        await PopulateAvailableFirmwareAsync();
        
        
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

        var firmwareToDownload = new List<(Firmware fw, string slug, string systemName)>();
        
        foreach (var system in appInstance.dataBus.systems)
        {
            List<Firmware> systemFirmware = await appInstance.rommApi.GetFirmwareAsync(system.Id);
            
            if (systemFirmware != null && systemFirmware.Any())
            {
                foreach (var fw in systemFirmware)
                {
                    firmwareToDownload.Add((fw, system.Slug, system.Name));
                }
            }
        }

        if (!firmwareToDownload.Any()) return;

        int processed = 0;
        var authHeaders = appInstance.rommApi.GetAuthHeaders();

        foreach (var item in firmwareToDownload)
        {
            Firmware fw = item.fw;
            string slug = item.slug;
            
            string systemBiosDir = Path.Combine(appInstance.configManager.BiosPath, slug);
            if (!Directory.Exists(systemBiosDir))
            {
                Directory.CreateDirectory(systemBiosDir);
            }

            string savePath = Path.Combine(systemBiosDir, fw.FileName);

            if (!File.Exists(savePath))
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = $"Downloading BIOS: {fw.FileName} ({item.systemName})...";
                }

                string downloadUrl = appInstance.rommApi.GetFirmwareDownloadUrl(fw);
                await DownloadFirmwareWrapperAsync(downloadUrl, savePath, authHeaders);

                string configPath = Path.Combine(systemBiosDir, $"{Path.GetFileNameWithoutExtension(fw.FileName)}.config.json");
                string localConfig = "{ \"loaded\": true, \"path\": \"./" + fw.FileName + "\" }";
                await File.WriteAllTextAsync(configPath, localConfig);
            }

            processed++;
            if (_progressBar != null)
            {
                _progressBar.Value = ((float)processed / firmwareToDownload.Count) * 100;
            }
        }
    }
    
    private async Task PopulateAvailableFirmwareAsync()
    {
        foreach (var system in appInstance.dataBus.systems)
        {
            var firmwareDir = appInstance.configManager.BiosPath.PathJoin(system.Slug);
            if (DirAccess.DirExistsAbsolute(firmwareDir))
            {
                var firmwaresFromApi = await appInstance.rommApi.GetFirmwareAsync(system.Id);
                var localFiles = DirAccess.GetFilesAt(firmwareDir);

                var availableFirmwares = new List<Firmware>();
                foreach (var fw in firmwaresFromApi)
                {
                    if (localFiles.Contains(fw.FileName))
                    {
                        fw.FullPath = firmwareDir.PathJoin(fw.FileName);
                        availableFirmwares.Add(fw);
                    }
                }
                system.AvailableFirmwares = availableFirmwares;

                if (string.IsNullOrEmpty(system.PrefferedFirmware) && system.AvailableFirmwares.Any())
                {
                    system.PrefferedFirmware = system.AvailableFirmwares.First().FullPath;
                }
            }
        }
    }
    
    private Task<string> DownloadFirmwareWrapperAsync(string url, string destinationPath, string[] headers)
    {
        var tcs = new TaskCompletionSource<string>();
        
        appInstance.downloadManager.DownloadFile(
            url, 
            destinationPath, 
            headers, 
            (path) => 
            {
                tcs.SetResult(path);
            }
        );
        
        return tcs.Task;
    }
}
