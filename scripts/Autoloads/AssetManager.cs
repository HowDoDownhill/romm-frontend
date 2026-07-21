using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public partial class AssetManager : Node
{
    [Signal]
    public delegate void AssetDownloadedEventHandler(int gameId, string assetType);

    private readonly struct AssetDownloadItem
    {
        public readonly int GameId;
        public readonly string AssetType;
        public readonly string DownloadUrl;
        public readonly string LocalFilePath;

        public AssetDownloadItem(int gameId, string assetType, string downloadUrl, string localFilePath)
        {
            GameId = gameId;
            AssetType = assetType;
            DownloadUrl = downloadUrl;
            LocalFilePath = localFilePath;
        }
    }

    private AppInstance appInstance;

    // Priority queue: newly requested (on-screen) games are pushed to the FRONT so the game the
    // user is currently on is fetched before games they merely scrolled past. A plain FIFO would
    // put the current selection behind everything already queued (priority inversion).
    private readonly LinkedList<AssetDownloadItem> pendingAssetDownloadQueue = new();
    // Number of still-queued items per game, used to dedupe requests and to cancel on scroll-out.
    private readonly Dictionary<int, int> pendingItemCountByGameId = new();
    private readonly object queueLock = new();

    // Only the on-screen window is ever queued now (off-screen games are pruned), so we can afford
    // more workers and no inter-download delay to get visible art up quickly.
    private int maximumConcurrentDownloadWorkers = 4;
    private int activeDownloadWorkerCount = 0;
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

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

        var items = BuildAssetDownloadItems(game);

        if (items.Count == 0)
        {
            return;
        }

        lock (queueLock)
        {
            // Already queued (or partially in-flight) for this game; don't duplicate.
            if (pendingItemCountByGameId.ContainsKey(game.Id))
            {
                return;
            }

            // Push to the front, reversed, so items[0] (the 3D cover) ends up frontmost.
            for (int i = items.Count - 1; i >= 0; i--)
            {
                pendingAssetDownloadQueue.AddFirst(items[i]);
            }

            pendingItemCountByGameId[game.Id] = items.Count;
        }

        EnsureDownloadWorkersAreRunning();
    }

    // Removes a game's not-yet-started downloads from the queue when it scrolls off screen, so a
    // fast scrub doesn't rack up a huge backlog of art for games the user is no longer looking at.
    // Any item already handed to a worker finishes; only pending items are dropped.
    public void CancelGameAssets(int gameId)
    {
        lock (queueLock)
        {
            if (!pendingItemCountByGameId.ContainsKey(gameId))
            {
                return;
            }

            var node = pendingAssetDownloadQueue.First;

            while (node != null)
            {
                var next = node.Next;

                if (node.Value.GameId == gameId)
                {
                    pendingAssetDownloadQueue.Remove(node);
                }

                node = next;
            }

            pendingItemCountByGameId.Remove(gameId);
        }
    }

    // Drops the entire pending backlog (e.g. when the game list is rebuilt for a new system/filter).
    // Visible entries re-request themselves via their VisibilityChanged handlers after the rebuild.
    public void ClearPendingAssetDownloads()
    {
        lock (queueLock)
        {
            pendingAssetDownloadQueue.Clear();
            pendingItemCountByGameId.Clear();
        }
    }

    private List<AssetDownloadItem> BuildAssetDownloadItems(Game game)
    {
        var items = new List<AssetDownloadItem>();
        string assetsDirectoryPath = appInstance.configManager.AssetsPath;

        if (string.IsNullOrEmpty(assetsDirectoryPath))
        {
            return items;
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

            items.Add(new AssetDownloadItem(game.Id, "box3d", threeDimensionalCoverUrl, threeDimensionalCoverPath));
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

            items.Add(new AssetDownloadItem(game.Id, "box2d", twoDimensionalCoverUrl, twoDimensionalCoverPath));
        }

        string marqueeImagePath = Path.Combine(assetsDirectoryPath, "marquees", $"{game.Id}.png");

        if (!File.Exists(marqueeImagePath))
        {
            string marqueeImageUrl = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/marquee/marquee.png";
            items.Add(new AssetDownloadItem(game.Id, "marquee", marqueeImageUrl, marqueeImagePath));
        }

        for (int i = 0; i < 5; i++)
        {
            string screenshotImagePath = Path.Combine(assetsDirectoryPath, "screenshots", $"{game.Id}_{i}.jpg");

            if (!File.Exists(screenshotImagePath))
            {
                string screenshotImageUrl = $"{appInstance.rommApi.ApiHost}/assets/romm/resources/roms/{game.PlatformId}/{game.Id}/screenshots/{i}.jpg";
                items.Add(new AssetDownloadItem(game.Id, "screenshot", screenshotImageUrl, screenshotImagePath));
            }
        }

        return items;
    }

    private bool TryDequeueNextDownload(out AssetDownloadItem item)
    {
        lock (queueLock)
        {
            var first = pendingAssetDownloadQueue.First;

            if (first == null)
            {
                item = default;
                return false;
            }

            item = first.Value;
            pendingAssetDownloadQueue.RemoveFirst();

            // The item is now in-flight rather than pending; drop the per-game count so the game can
            // be re-requested once its whole set has left the queue.
            if (pendingItemCountByGameId.TryGetValue(item.GameId, out int remaining))
            {
                if (remaining <= 1)
                {
                    pendingItemCountByGameId.Remove(item.GameId);
                }
                else
                {
                    pendingItemCountByGameId[item.GameId] = remaining - 1;
                }
            }

            return true;
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
                if (TryDequeueNextDownload(out var downloadTask))
                {
                    bool downloadSucceeded = await appInstance.rommApi.DownloadAssetAsync(downloadTask.DownloadUrl, downloadTask.LocalFilePath);

                    if (downloadSucceeded)
                    {
                        CallDeferred(MethodName.EmitAssetDownloaded, downloadTask.GameId, downloadTask.AssetType);
                    }
                    // A false result is usually just a 404 (the server has no art for this game), which is
                    // normal and not worth logging. Genuine failures (real HTTP errors, exceptions) are
                    // already logged with URL/status detail inside DownloadAssetAsync.
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
