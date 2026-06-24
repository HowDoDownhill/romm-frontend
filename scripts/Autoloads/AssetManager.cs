using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private int _activeWorkers = 0;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    // Track which game IDs have already been queued to avoid duplicate requests
    private readonly HashSet<int> _requestedGameIds = new();

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.assetManager = this; 
    }

    /// <summary>
    /// Request assets for a single game. Call this when a game entry becomes
    /// near-visible in the carousel. Already-cached and in-flight games are skipped.
    /// </summary>
    public void RequestGameAssets(Game game)
    {
        if (game == null) return;

        // Skip if we've already queued this game's assets
        lock (_requestedGameIds)
        {
            if (!_requestedGameIds.Add(game.Id)) return;
        }

        QueueGameAssets(game);
        EnsureWorkersRunning();
    }

    private void QueueGameAssets(Game game)
    {
        string assetsPath = appInstance.configManager.AssetsPath;
        if (string.IsNullOrEmpty(assetsPath)) return;

        // 3D Cover
        string path3d = Path.Combine(assetsPath, "covers_3d", $"{game.Id}.png");
        if (!File.Exists(path3d))
        {
            string url3d;
            if (!string.IsNullOrEmpty(game.PathCover3d))
            {
                string path = game.PathCover3d.TrimStart('/');
                url3d = game.PathCover3d.StartsWith("http") ? game.PathCover3d : $"{appInstance.rommApi.ApiHost}/{path}";
            }
            else
            {
                url3d = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/box3d/box3d.png";
            }
            _downloadQueue.Enqueue((game.Id, "box3d", url3d, path3d));
        }

        // 2D Cover (box2d, using large cover as requested)
        string path2d = Path.Combine(assetsPath, "covers_2d", $"{game.Id}.png");
        if (!File.Exists(path2d))
        {
            string url2d;
            if (!string.IsNullOrEmpty(game.PathCoverLarge))
            {
                string path = game.PathCoverLarge.TrimStart('/');
                url2d = game.PathCoverLarge.StartsWith("http") ? game.PathCoverLarge : $"{appInstance.rommApi.ApiHost}/{path}";
            }
            else
            {
                url2d = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/cover/big.png";
            }
            _downloadQueue.Enqueue((game.Id, "box2d", url2d, path2d));
        } 
        
        // Marquee
        string pathMarquee = Path.Combine(assetsPath, "marquees", $"{game.Id}.png");
        if (!File.Exists(pathMarquee))
        {
            string urlMarquee = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/marquee/marquee.png";
            _downloadQueue.Enqueue((game.Id, "marquee", urlMarquee, pathMarquee));
        }

        // Screenshot
        string pathScreenshot = Path.Combine(assetsPath, "screenshots", $"{game.Id}.jpg");
        if (!File.Exists(pathScreenshot))
        {
            string urlScreenshot = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/screenshot/0.jpg";
            _downloadQueue.Enqueue((game.Id, "screenshot", urlScreenshot, pathScreenshot));
        }
    }

    /// <summary>
    /// Spin up download workers if none are currently running.
    /// </summary>
    private void EnsureWorkersRunning()
    {
        if (_activeWorkers >= _concurrentDownloads) return;

        int workersToStart = _concurrentDownloads - _activeWorkers;
        for (int i = 0; i < workersToStart; i++)
        {
            Interlocked.Increment(ref _activeWorkers);
            _ = DownloadWorkerAsync(_cts.Token);
        }
    }

    private async Task DownloadWorkerAsync(CancellationToken token)
    {
        try
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
        finally
        {
            Interlocked.Decrement(ref _activeWorkers);
        }
    }

    private void EmitAssetDownloaded(int gameId, string assetType)
    {
        EmitSignal(SignalName.AssetDownloaded, gameId, assetType);
    }
}
