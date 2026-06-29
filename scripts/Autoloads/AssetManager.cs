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
    private ConcurrentQueue<(int gameId, string assetType, string downloadUrl, string localFilePath)> pendingAssetDownloadQueue = new();
    private int maximumConcurrentDownloadWorkers = 2;
    private int activeDownloadWorkerCount = 0;
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    private readonly HashSet<int> previouslyRequestedGameIds = new();

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.assetManager = this;
    }

    public void RequestGameAssets(Game game)
    {
        if (game == null)
        {
            return;
        }

        lock (previouslyRequestedGameIds)
        {
            if (!previouslyRequestedGameIds.Add(game.Id))
            {
                return;
            }
        }

        EnqueueGameAssetDownloads(game);
        EnsureDownloadWorkersAreRunning();
    }

    private void EnqueueGameAssetDownloads(Game game)
    {
        string assetsDirectoryPath = appInstance.configManager.AssetsPath;

        if (string.IsNullOrEmpty(assetsDirectoryPath))
        {
            return;
        }

        string threeDimensionalCoverPath = Path.Combine(assetsDirectoryPath, "covers_3d", $"{game.Id}.png");

        if (!File.Exists(threeDimensionalCoverPath))
        {
            string threeDimensionalCoverUrl;

            if (!string.IsNullOrEmpty(game.PathCover3d))
            {
                string relativePath = game.PathCover3d.TrimStart('/');
                threeDimensionalCoverUrl = game.PathCover3d.StartsWith("http") ? game.PathCover3d : $"{appInstance.rommApi.ApiHost}/{relativePath}";
            }

            else
            {
                threeDimensionalCoverUrl = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/box3d/box3d.png";
            }

            pendingAssetDownloadQueue.Enqueue((game.Id, "box3d", threeDimensionalCoverUrl, threeDimensionalCoverPath));
        }

        string twoDimensionalCoverPath = Path.Combine(assetsDirectoryPath, "covers_2d", $"{game.Id}.png");

        if (!File.Exists(twoDimensionalCoverPath))
        {
            string twoDimensionalCoverUrl;

            if (!string.IsNullOrEmpty(game.PathCoverLarge))
            {
                string relativePath = game.PathCoverLarge.TrimStart('/');
                twoDimensionalCoverUrl = game.PathCoverLarge.StartsWith("http") ? game.PathCoverLarge : $"{appInstance.rommApi.ApiHost}/{relativePath}";
            }

            else
            {
                twoDimensionalCoverUrl = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/cover/big.png";
            }

            pendingAssetDownloadQueue.Enqueue((game.Id, "box2d", twoDimensionalCoverUrl, twoDimensionalCoverPath));
        }

        string marqueeImagePath = Path.Combine(assetsDirectoryPath, "marquees", $"{game.Id}.png");

        if (!File.Exists(marqueeImagePath))
        {
            string marqueeImageUrl = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/marquee/marquee.png";
            pendingAssetDownloadQueue.Enqueue((game.Id, "marquee", marqueeImageUrl, marqueeImagePath));
        }

        string screenshotImagePath = Path.Combine(assetsDirectoryPath, "screenshots", $"{game.Id}.jpg");

        if (!File.Exists(screenshotImagePath))
        {
            string screenshotImageUrl = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/screenshot/0.jpg";
            pendingAssetDownloadQueue.Enqueue((game.Id, "screenshot", screenshotImageUrl, screenshotImagePath));
        }
    }

    private void EnsureDownloadWorkersAreRunning()
    {
        if (activeDownloadWorkerCount >= maximumConcurrentDownloadWorkers)
        {
            return;
        }

        int workersToSpawn = maximumConcurrentDownloadWorkers - activeDownloadWorkerCount;

        for (int workerIndex = 0; workerIndex < workersToSpawn; workerIndex++)
        {
            Interlocked.Increment(ref activeDownloadWorkerCount);
            _ = RunAssetDownloadWorkerAsync(cancellationTokenSource.Token);
        }
    }

    private async Task RunAssetDownloadWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (pendingAssetDownloadQueue.TryDequeue(out var downloadTask))
                {
                    bool downloadSucceeded = await appInstance.rommApi.DownloadAssetAsync(downloadTask.downloadUrl, downloadTask.localFilePath);

                    if (downloadSucceeded)
                    {
                        CallDeferred(MethodName.EmitAssetDownloaded, downloadTask.gameId, downloadTask.assetType);
                    }

                    await Task.Delay(100, cancellationToken);
                }

                else
                {
                    break;
                }
            }
        }

        finally
        {
            Interlocked.Decrement(ref activeDownloadWorkerCount);
        }
    }

    private void EmitAssetDownloaded(int gameId, string assetType)
    {
        EmitSignal(SignalName.AssetDownloaded, gameId, assetType);
    }
}
