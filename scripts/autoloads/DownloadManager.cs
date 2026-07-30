using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public partial class DownloadManager : Node
{
    [Signal]
    public delegate void DownloadProgressUpdatedEventHandler(string fileName, long currentBytes, long totalBytes, string gameId);

    [Signal]
    public delegate void DownloadCompletedEventHandler(string fileName, bool wasSuccessful);

    private const int TransferBufferSizeBytes = 1024 * 1024;
    private const double DiagnosticsLogIntervalSeconds = 2.0;

    private static readonly System.Net.Http.HttpClient transferClient = new System.Net.Http.HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.downloadManager = this;
    }

    private class ActiveDownloadEntry
    {
        public string fileName;
        public string destinationPath;
        public System.Action<string> completionCallback;
        public string gameId;
        public CancellationTokenSource cancellationSource;
        public ulong startedAtMilliseconds;
        public long bytesDownloaded;
        public long totalBytes;
        public bool hasFinished;
        public bool wasSuccessful;
        public string failureReason;
        public double secondsSinceDiagnosticsLog;
        public long bytesAtLastDiagnosticsLog;
    }

    private List<ActiveDownloadEntry> activeDownloadEntries = new List<ActiveDownloadEntry>();

    public class ExternalTransfer
    {
        public string displayName;
        public long bytesTransferred;
        public long totalBytes;
        public bool hasFinished;
        public bool wasSuccessful;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<ExternalTransfer, byte> externalTransfers = new System.Collections.Concurrent.ConcurrentDictionary<ExternalTransfer, byte>();

    public ExternalTransfer BeginExternalTransfer(string displayName)
    {
        var externalTransfer = new ExternalTransfer { displayName = displayName };
        externalTransfers.TryAdd(externalTransfer, 0);
        return externalTransfer;
    }

    public void ReportExternalTransferProgress(ExternalTransfer externalTransfer, long bytesTransferred, long totalBytes)
    {
        if (externalTransfer == null)
        {
            return;
        }

        Interlocked.Exchange(ref externalTransfer.bytesTransferred, bytesTransferred);
        Interlocked.Exchange(ref externalTransfer.totalBytes, totalBytes);
    }

    public void CompleteExternalTransfer(ExternalTransfer externalTransfer, bool wasSuccessful)
    {
        if (externalTransfer == null)
        {
            return;
        }

        Volatile.Write(ref externalTransfer.wasSuccessful, wasSuccessful);
        Volatile.Write(ref externalTransfer.hasFinished, true);
    }

    private void ProcessExternalTransfers()
    {
        foreach (var externalTransfer in externalTransfers.Keys)
        {
            long bytesTransferred = Interlocked.Read(ref externalTransfer.bytesTransferred);
            long totalBytes = Interlocked.Read(ref externalTransfer.totalBytes);

            if (Volatile.Read(ref externalTransfer.hasFinished))
            {
                externalTransfers.TryRemove(externalTransfer, out _);
                EmitSignal(SignalName.DownloadCompleted, externalTransfer.displayName, Volatile.Read(ref externalTransfer.wasSuccessful));
                continue;
            }

            EmitSignal(SignalName.DownloadProgressUpdated, externalTransfer.displayName, bytesTransferred, totalBytes, "");
        }
    }

    public void DownloadFile(string downloadUrl, string destinationFilePath, string[] requestHeaders, System.Action<string> onDownloadComplete, string gameId = null, long expectedTotalBytes = 0)
    {
        var downloadEntry = new ActiveDownloadEntry
        {
            fileName = destinationFilePath.GetFile(),
            destinationPath = destinationFilePath,
            completionCallback = onDownloadComplete,
            gameId = gameId,
            cancellationSource = new CancellationTokenSource(),
            startedAtMilliseconds = Time.GetTicksMsec(),
            totalBytes = expectedTotalBytes
        };

        activeDownloadEntries.Add(downloadEntry);

        GD.Print($"[Download] starting file={downloadEntry.fileName} url={downloadUrl} expectedTotalBytes={expectedTotalBytes}");

        _ = TransferToDestinationAsync(downloadEntry, downloadUrl, requestHeaders);
    }

    private async Task TransferToDestinationAsync(ActiveDownloadEntry downloadEntry, string downloadUrl, string[] requestHeaders)
    {
        string globalDestinationPath = ProjectSettings.GlobalizePath(downloadEntry.destinationPath);
        var cancellationToken = downloadEntry.cancellationSource.Token;

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

            ApplyRequestHeaders(requestMessage, requestHeaders);

            using var responseMessage = await transferClient
                .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                MarkDownloadFinished(downloadEntry, false, $"HTTP {(int)responseMessage.StatusCode} {responseMessage.ReasonPhrase}");
                return;
            }

            long declaredContentLength = responseMessage.Content.Headers.ContentLength ?? 0;

            if (declaredContentLength > 0)
            {
                Interlocked.Exchange(ref downloadEntry.totalBytes, declaredContentLength);
            }

            GD.Print($"[Download] responseReceived file={downloadEntry.fileName} status={(int)responseMessage.StatusCode} contentLength={declaredContentLength} contentEncoding={string.Join(",", responseMessage.Content.Headers.ContentEncoding)}");

            using var responseStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var destinationStream = new System.IO.FileStream(globalDestinationPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read, TransferBufferSizeBytes, true);

            var transferBuffer = new byte[TransferBufferSizeBytes];
            long bytesWritten = 0;

            while (true)
            {
                int bytesRead = await responseStream.ReadAsync(transferBuffer, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                await destinationStream.WriteAsync(transferBuffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                bytesWritten += bytesRead;
                Interlocked.Exchange(ref downloadEntry.bytesDownloaded, bytesWritten);
            }

            await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            long expectedBytes = Interlocked.Read(ref downloadEntry.totalBytes);
            bool receivedWholeBody = expectedBytes <= 0 || bytesWritten == expectedBytes;

            MarkDownloadFinished(downloadEntry, receivedWholeBody, receivedWholeBody ? null : $"expected {expectedBytes} bytes but received {bytesWritten}");
        }
        catch (System.OperationCanceledException)
        {
            DeleteIncompleteFile(globalDestinationPath);
            MarkDownloadFinished(downloadEntry, false, "cancelled");
        }
        catch (System.Exception transferException)
        {
            DeleteIncompleteFile(globalDestinationPath);
            MarkDownloadFinished(downloadEntry, false, transferException.Message);
        }
    }

    private static void ApplyRequestHeaders(HttpRequestMessage requestMessage, string[] requestHeaders)
    {
        foreach (var requestHeader in requestHeaders ?? new string[0])
        {
            int separatorIndex = requestHeader.IndexOf(':');

            if (separatorIndex <= 0)
            {
                continue;
            }

            string headerName = requestHeader.Substring(0, separatorIndex).Trim();
            string headerValue = requestHeader.Substring(separatorIndex + 1).Trim();

            requestMessage.Headers.TryAddWithoutValidation(headerName, headerValue);
        }
    }

    private static void MarkDownloadFinished(ActiveDownloadEntry downloadEntry, bool wasSuccessful, string failureReason)
    {
        downloadEntry.failureReason = failureReason;
        downloadEntry.wasSuccessful = wasSuccessful;
        Volatile.Write(ref downloadEntry.hasFinished, true);
    }

    private static void DeleteIncompleteFile(string globalFilePath)
    {
        try
        {
            if (System.IO.File.Exists(globalFilePath))
            {
                System.IO.File.Delete(globalFilePath);
            }
        }
        catch (System.Exception deleteException)
        {
            GD.PrintErr($"Could not remove incomplete download {globalFilePath}: {deleteException.Message}");
        }
    }

    public override void _Process(double deltaTime)
    {
        foreach (var downloadEntry in activeDownloadEntries.ToList())
        {
            long bytesDownloaded = Interlocked.Read(ref downloadEntry.bytesDownloaded);
            long totalBytes = Interlocked.Read(ref downloadEntry.totalBytes);

            if (Volatile.Read(ref downloadEntry.hasFinished))
            {
                HandleDownloadFinished(downloadEntry, bytesDownloaded, totalBytes);
                continue;
            }

            EmitSignal(SignalName.DownloadProgressUpdated, downloadEntry.fileName, bytesDownloaded, totalBytes, downloadEntry.gameId);

            LogDownloadProgress(downloadEntry, bytesDownloaded, totalBytes, deltaTime);
        }

        ProcessExternalTransfers();
    }

    private void LogDownloadProgress(ActiveDownloadEntry downloadEntry, long bytesDownloaded, long totalBytes, double deltaTime)
    {
        downloadEntry.secondsSinceDiagnosticsLog += deltaTime;

        if (downloadEntry.secondsSinceDiagnosticsLog < DiagnosticsLogIntervalSeconds)
        {
            return;
        }

        double megabytesPerSecond = (bytesDownloaded - downloadEntry.bytesAtLastDiagnosticsLog) / downloadEntry.secondsSinceDiagnosticsLog / (1024.0 * 1024.0);
        double elapsedSeconds = (Time.GetTicksMsec() - downloadEntry.startedAtMilliseconds) / 1000.0;

        GD.Print($"[Download] progress file={downloadEntry.fileName} downloaded={bytesDownloaded} total={totalBytes} rate={megabytesPerSecond:F2}MB/s elapsed={elapsedSeconds:F0}s");

        downloadEntry.bytesAtLastDiagnosticsLog = bytesDownloaded;
        downloadEntry.secondsSinceDiagnosticsLog = 0.0;
    }

    private void HandleDownloadFinished(ActiveDownloadEntry downloadEntry, long bytesDownloaded, long totalBytes)
    {
        activeDownloadEntries.Remove(downloadEntry);
        downloadEntry.cancellationSource.Dispose();

        double elapsedSeconds = (Time.GetTicksMsec() - downloadEntry.startedAtMilliseconds) / 1000.0;

        if (downloadEntry.wasSuccessful)
        {
            GD.Print($"[Download] finished file={downloadEntry.fileName} downloaded={bytesDownloaded} total={totalBytes} elapsed={elapsedSeconds:F1}s");
            downloadEntry.completionCallback?.Invoke(downloadEntry.destinationPath);
        }
        else
        {
            GD.PrintErr($"Download failed: {downloadEntry.fileName} ({downloadEntry.failureReason}) after {elapsedSeconds:F1}s");
            DeleteIncompleteFile(ProjectSettings.GlobalizePath(downloadEntry.destinationPath));
        }

        EmitSignal(SignalName.DownloadCompleted, downloadEntry.fileName, downloadEntry.wasSuccessful);
    }

    public bool IsDownloading(string fileName)
    {
        return activeDownloadEntries.Any(entry => entry.fileName == fileName);
    }

    public bool IsDownloadingGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return false;
        }

        return activeDownloadEntries.Any(entry => entry.gameId == gameId);
    }

    public void CancelDownload(string fileName)
    {
        var downloadEntryToCancel = activeDownloadEntries.FirstOrDefault(entry => entry.fileName == fileName);

        if (downloadEntryToCancel == null)
        {
            return;
        }

        activeDownloadEntries.Remove(downloadEntryToCancel);
        downloadEntryToCancel.cancellationSource.Cancel();

        EmitSignal(SignalName.DownloadCompleted, downloadEntryToCancel.fileName, false);
    }
}
