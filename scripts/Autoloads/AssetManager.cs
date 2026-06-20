using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public partial class AssetManager : Node
{
    [Signal]
    public delegate void AssetDownloadedEventHandler(int gameId, string assetType);

    private AppInstance appInstance;
    private ConcurrentQueue<(int gameId, string assetType, string url, string localPath)> _downloadQueue = new();
    private int _concurrentDownloads = 2;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.assetManager = this; 
    }

    public void StartBackgroundSync()
    {
        _cts.Cancel(); // cancel previous
        _cts = new CancellationTokenSource();
        _downloadQueue.Clear();

        // Queue all missing assets
        foreach (var kvp in appInstance.dataBus.gameCache)
        {
            foreach (var game in kvp.Value)
            {
                QueueGameAssets(game);
            }
        }

        GD.Print($"AssetManager queued {_downloadQueue.Count} assets for download.");

        // Start workers
        for (int i = 0; i < _concurrentDownloads; i++)
        {
            _ = DownloadWorkerAsync(_cts.Token);
        }
    }

    private void QueueGameAssets(Game game)
    {
        string assetsPath = appInstance.configManager.AssetsPath;
        if (string.IsNullOrEmpty(assetsPath)) return;

        // 3D Cover
        string path3d = Path.Combine(assetsPath, "covers_3d", $"{game.Id}.png");
        if (!File.Exists(path3d))
        {
            string url3d = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/box3d/box3d.png";
            _downloadQueue.Enqueue((game.Id, "box3d", url3d, path3d));
        }

        // 2D Cover
        string path2d = Path.Combine(assetsPath, "covers_2d", $"{game.Id}.png");
        if (!File.Exists(path2d))
        {
            if (!string.IsNullOrEmpty(game.PathCoverLarge))
            {
                string cleanPath = game.PathCoverLarge.StartsWith("/") ? game.PathCoverLarge.Substring(1) : game.PathCoverLarge;
                string url2d = $"{appInstance.rommApi.ApiHost}/{cleanPath}".Replace(" ", "%20");
                _downloadQueue.Enqueue((game.Id, "box2d", url2d, path2d));
            }
            else if (!string.IsNullOrEmpty(game.CoverArtUrl))
            {
                string url2d = game.CoverArtUrl.Replace(" ", "%20");
                _downloadQueue.Enqueue((game.Id, "box2d", url2d, path2d));
            }
        }

        // Marquee
        string pathMarquee = Path.Combine(assetsPath, "marquees", $"{game.Id}.png");
        if (!File.Exists(pathMarquee))
        {
            string urlMarquee = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/marquee/marquee.png";
            _downloadQueue.Enqueue((game.Id, "marquee", urlMarquee, pathMarquee));
        }
    }

    private async Task DownloadWorkerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_downloadQueue.TryDequeue(out var task))
            {
                bool success = await appInstance.rommApi.DownloadAssetAsync(task.url, task.localPath);
                if (success)
                {
                    // Emit signal on main thread
                    CallDeferred(MethodName.EmitAssetDownloaded, task.gameId, task.assetType);
                }
                
                // Add a small delay to prevent flooding the router
                await Task.Delay(100, token);
            }
            else
            {
                // Queue is empty, exit worker
                break;
            }
        }
    }

    private void EmitAssetDownloaded(int gameId, string assetType)
    {
        EmitSignal(SignalName.AssetDownloaded, gameId, assetType);
    }
}
