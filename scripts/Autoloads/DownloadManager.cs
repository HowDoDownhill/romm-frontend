using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;

public partial class DownloadManager : Node
{
    [Signal]
    public delegate void DownloadProgressUpdatedEventHandler(string fileName, long currentBytes, long totalBytes, string gameId);

    [Signal]
    public delegate void DownloadCompletedEventHandler(string fileName, bool wasSuccessful);

    private const double DiagnosticsLogIntervalSeconds = 2.0;

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.downloadManager = this;
    }

    private class ActiveDownloadEntry
    {
        public HttpRequest Request { get; set; }
        public string FileName { get; set; }
        public string DestinationPath { get; set; }
        public System.Action<string> CompletionCallback { get; set; }
        public bool IsCancelled { get; set; }
        public string GameId { get; set; }
        public ulong StartedAtMilliseconds { get; set; }
        public double SecondsSinceDiagnosticsLog { get; set; }
        public long BytesAtLastDiagnosticsLog { get; set; }
    }

    private List<ActiveDownloadEntry> activeDownloadEntries = new List<ActiveDownloadEntry>();

    public void DownloadFile(string downloadUrl, string destinationFilePath, string[] requestHeaders, System.Action<string> onDownloadComplete, string gameId = null)
    {
        var httpRequest = new HttpRequest();
        AddChild(httpRequest);

        var downloadEntry = new ActiveDownloadEntry
        {
            Request = httpRequest,
            FileName = destinationFilePath.GetFile(),
            DestinationPath = destinationFilePath,
            CompletionCallback = onDownloadComplete,
            GameId = gameId,
            StartedAtMilliseconds = Time.GetTicksMsec()
        };

        activeDownloadEntries.Add(downloadEntry);

        httpRequest.DownloadFile = destinationFilePath;
        httpRequest.UseThreads = true;

        string[] finalRequestHeaders = requestHeaders ?? new string[0];

        if (!finalRequestHeaders.Any(header => header.StartsWith("User-Agent")))
        {
            var headerList = finalRequestHeaders.ToList();
            headerList.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            finalRequestHeaders = headerList.ToArray();
        }

        httpRequest.RequestCompleted += (long resultCode, long responseCode, string[] responseHeaders, byte[] responseBody) =>
        {
            LogDownloadCompletionDiagnostics(downloadEntry, resultCode, responseCode, responseHeaders, responseBody);
            HandleDownloadCompleted(downloadEntry, resultCode, responseCode);
        };

        var requestError = httpRequest.Request(downloadUrl, finalRequestHeaders);

        GD.Print($"[DownloadDiagnostics] start file={downloadEntry.FileName} url={downloadUrl} requestError={requestError} bodySizeLimit={httpRequest.BodySizeLimit} useThreads={httpRequest.UseThreads} timeoutSeconds={httpRequest.Timeout} maxRedirects={httpRequest.MaxRedirects} concurrentDownloads={activeDownloadEntries.Count}");

        if (requestError != Error.Ok)
        {
            GD.PrintErr($"HttpRequest failed to start for {downloadUrl}. Error: {requestError}");
            HandleDownloadCompleted(downloadEntry, (long)HttpRequest.Result.CantConnect, 0);
        }
    }

    public override void _Process(double deltaTime)
    {
        foreach (var downloadEntry in activeDownloadEntries.ToList())
        {
            if (!GodotObject.IsInstanceValid(downloadEntry.Request))
            {
                continue;
            }

            var clientStatus = downloadEntry.Request.GetHttpClientStatus();

            if (clientStatus == HttpClient.Status.Body)
            {
                EmitSignal(SignalName.DownloadProgressUpdated,
                    downloadEntry.FileName,
                    downloadEntry.Request.GetDownloadedBytes(),
                    downloadEntry.Request.GetBodySize(),
                    downloadEntry.GameId);
            }

            LogDownloadProgressDiagnostics(downloadEntry, clientStatus, deltaTime);
        }
    }

    private void LogDownloadProgressDiagnostics(ActiveDownloadEntry downloadEntry, HttpClient.Status clientStatus, double deltaTime)
    {
        downloadEntry.SecondsSinceDiagnosticsLog += deltaTime;

        if (downloadEntry.SecondsSinceDiagnosticsLog < DiagnosticsLogIntervalSeconds)
        {
            return;
        }

        long downloadedBytes = downloadEntry.Request.GetDownloadedBytes();
        long bytesSinceLastLog = downloadedBytes - downloadEntry.BytesAtLastDiagnosticsLog;
        double megabytesPerSecond = bytesSinceLastLog / downloadEntry.SecondsSinceDiagnosticsLog / (1024.0 * 1024.0);
        double elapsedSeconds = (Time.GetTicksMsec() - downloadEntry.StartedAtMilliseconds) / 1000.0;

        GD.Print($"[DownloadDiagnostics] progress file={downloadEntry.FileName} status={clientStatus} downloaded={downloadedBytes} reportedBodySize={downloadEntry.Request.GetBodySize()} onDisk={MeasureBytesOnDisk(downloadEntry.DestinationPath)} rate={megabytesPerSecond:F2}MB/s elapsed={elapsedSeconds:F0}s");

        downloadEntry.BytesAtLastDiagnosticsLog = downloadedBytes;
        downloadEntry.SecondsSinceDiagnosticsLog = 0.0;
    }

    private void LogDownloadCompletionDiagnostics(ActiveDownloadEntry downloadEntry, long resultCode, long responseCode, string[] responseHeaders, byte[] responseBody)
    {
        double elapsedSeconds = (Time.GetTicksMsec() - downloadEntry.StartedAtMilliseconds) / 1000.0;
        long downloadedBytes = GodotObject.IsInstanceValid(downloadEntry.Request) ? downloadEntry.Request.GetDownloadedBytes() : -1;
        long reportedBodySize = GodotObject.IsInstanceValid(downloadEntry.Request) ? downloadEntry.Request.GetBodySize() : -1;

        GD.Print($"[DownloadDiagnostics] completed file={downloadEntry.FileName} result={(HttpRequest.Result)resultCode} responseCode={responseCode} downloaded={downloadedBytes} reportedBodySize={reportedBodySize} onDisk={MeasureBytesOnDisk(downloadEntry.DestinationPath)} inMemoryBody={responseBody?.Length ?? 0} elapsed={elapsedSeconds:F1}s cancelled={downloadEntry.IsCancelled}");

        foreach (var responseHeader in responseHeaders ?? new string[0])
        {
            GD.Print($"[DownloadDiagnostics] responseHeader {responseHeader}");
        }
    }

    private static long MeasureBytesOnDisk(string filePath)
    {
        using var fileHandle = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);

        if (fileHandle == null)
        {
            return -1;
        }

        return (long)fileHandle.GetLength();
    }

    public bool IsDownloading(string fileName)
    {
        return activeDownloadEntries.Any(entry => entry.FileName == fileName);
    }

    public void CancelDownload(string fileName)
    {
        var downloadEntryToCancel = activeDownloadEntries.FirstOrDefault(entry => entry.FileName == fileName);

        if (downloadEntryToCancel != null)
        {
            downloadEntryToCancel.IsCancelled = true;
            downloadEntryToCancel.Request.CancelRequest();
            
            activeDownloadEntries.Remove(downloadEntryToCancel);
            EmitSignal(SignalName.DownloadCompleted, downloadEntryToCancel.FileName, false);

            var timer = GetTree().CreateTimer(2.0f);
            timer.Timeout += () => 
            {
                if (FileAccess.FileExists(downloadEntryToCancel.DestinationPath))
                {
                    DirAccess.RemoveAbsolute(downloadEntryToCancel.DestinationPath);
                }
                if (GodotObject.IsInstanceValid(downloadEntryToCancel.Request))
                {
                    downloadEntryToCancel.Request.QueueFree();
                }
            };
        }
    }

    private void HandleDownloadCompleted(ActiveDownloadEntry downloadEntry, long resultCode, long responseCode)
    {
        if (downloadEntry.IsCancelled) return;

        bool wasSuccessful = resultCode == (long)HttpRequest.Result.Success && responseCode == 200;

        if (wasSuccessful)
        {
            downloadEntry.CompletionCallback?.Invoke(downloadEntry.DestinationPath);
        }

        else
        {
            GD.PrintErr($"Download failed or was canceled: {downloadEntry.FileName}, Result: {resultCode}, Response Code: {responseCode}");

            if (FileAccess.FileExists(downloadEntry.DestinationPath))
            {
                DirAccess.RemoveAbsolute(downloadEntry.DestinationPath);
            }
        }

        EmitSignal(SignalName.DownloadCompleted, downloadEntry.FileName, wasSuccessful);

        activeDownloadEntries.Remove(downloadEntry);
        downloadEntry.Request.QueueFree();
    }
}
