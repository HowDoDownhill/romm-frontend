using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;

public partial class DownloadManager : Node
{
    [Signal]
    public delegate void DownloadProgressUpdatedEventHandler(string fileName, long currentBytes, long totalBytes);

    [Signal]
    public delegate void DownloadCompletedEventHandler(string fileName, bool wasSuccessful);

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
    }

    private List<ActiveDownloadEntry> activeDownloadEntries = new List<ActiveDownloadEntry>();

    public void DownloadFile(string downloadUrl, string destinationFilePath, string[] requestHeaders, System.Action<string> onDownloadComplete)
    {
        var httpRequest = new HttpRequest();
        AddChild(httpRequest);

        var downloadEntry = new ActiveDownloadEntry
        {
            Request = httpRequest,
            FileName = destinationFilePath.GetFile(),
            DestinationPath = destinationFilePath,
            CompletionCallback = onDownloadComplete
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

        var requestError = httpRequest.Request(downloadUrl, finalRequestHeaders);

        if (requestError != Error.Ok)
        {
            GD.PrintErr($"HttpRequest failed to start for {downloadUrl}. Error: {requestError}");
            HandleDownloadCompleted(downloadEntry, (long)HttpRequest.Result.CantConnect, 0);
        }

        httpRequest.RequestCompleted += (long resultCode, long responseCode, string[] responseHeaders, byte[] responseBody) =>
        {
            HandleDownloadCompleted(downloadEntry, resultCode, responseCode);
        };
    }

    public override void _Process(double deltaTime)
    {
        foreach (var downloadEntry in activeDownloadEntries.ToList())
        {
            if (downloadEntry.Request.GetHttpClientStatus() == HttpClient.Status.Body)
            {
                EmitSignal(SignalName.DownloadProgressUpdated,
                    downloadEntry.FileName,
                    downloadEntry.Request.GetDownloadedBytes(),
                    downloadEntry.Request.GetBodySize());
            }
        }
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
            downloadEntryToCancel.Request.RequestCompleted -= (long resultCode, long responseCode, string[] responseHeaders, byte[] responseBody) => HandleDownloadCompleted(downloadEntryToCancel, resultCode, responseCode);
            HandleDownloadCompleted(downloadEntryToCancel, (long)HttpRequest.Result.RequestFailed, 0);
        }
    }

    private void HandleDownloadCompleted(ActiveDownloadEntry downloadEntry, long resultCode, long responseCode)
    {
        bool wasSuccessful = resultCode == (long)HttpRequest.Result.Success && responseCode == 200;

        if (wasSuccessful)
        {
            downloadEntry.CompletionCallback?.Invoke(downloadEntry.DestinationPath);

            if (OS.GetName( ) == "Linux")
            {
                string filePath = downloadEntry.DestinationPath; 
                string[] arguments = {"+r", filePath};

                int exitCode = OS.Execute("chmod", arguments, new Godot.Collections.Array());
                
                if (exitCode == 0)
                {
                    GD.Print($"Successfully changed permissions for {filePath}");
                }
                else
                {
                    GD.PrintErr($"Failed to change permissions. Exit code: {exitCode}");
                }
            }
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
