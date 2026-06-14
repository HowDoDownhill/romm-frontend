using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DownloadManager : Node
{
    [Signal]
    public delegate void DownloadProgressUpdatedEventHandler(string fileName, long current, long total);

    [Signal]
    public delegate void DownloadCompletedEventHandler(string fileName, bool success);

    private class Download
    {
        public HttpRequest Request { get; set; }
        public string FileName { get; set; }
        public string DestinationPath { get; set; }
        public System.Action<string> CompletionCallback { get; set; }
    }

    private List<Download> _activeDownloads = new List<Download>();

    public void DownloadFile(string url, string destinationPath, string[] headers, System.Action<string> onComplete)
    {
        var request = new HttpRequest();
        AddChild(request);

        var download = new Download
        {
            Request = request,
            FileName = destinationPath.GetFile(),
            DestinationPath = destinationPath,
            CompletionCallback = onComplete
        };

        _activeDownloads.Add(download);

        request.DownloadFile = destinationPath;
        request.UseThreads = true;
        
        string[] finalHeaders = headers ?? new string[0];
        if (!finalHeaders.Any(h => h.StartsWith("User-Agent")))
        {
            var headerList = finalHeaders.ToList();
            headerList.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            finalHeaders = headerList.ToArray();
        }

        var error = request.Request(url, finalHeaders);
        if (error != Error.Ok)
        {
            GD.PrintErr($"HttpRequest failed to start for {url}. Error: {error}");
            OnDownloadCompleted(download, (long)HttpRequest.Result.CantConnect, 0);
        }

        request.RequestCompleted += (long result, long responseCode, string[] responseHeaders, byte[] body) =>
        {
            OnDownloadCompleted(download, result, responseCode);
        };
    }

    public override void _Process(double delta)
    {
        foreach (var download in _activeDownloads.ToList())
        {
            if (download.Request.GetHttpClientStatus() == HttpClient.Status.Body)
            {
                EmitSignal(SignalName.DownloadProgressUpdated,
                    download.FileName,
                    download.Request.GetDownloadedBytes(),
                    download.Request.GetBodySize());
            }
        }
    }

    public void CancelDownload(string fileName)
    {
        var downloadToCancel = _activeDownloads.FirstOrDefault(d => d.FileName == fileName);
        if (downloadToCancel != null)
        {
            GD.Print($"Cancelling download: {fileName}");
            downloadToCancel.Request.RequestCompleted -= (long result, long responseCode, string[] responseHeaders, byte[] body) => OnDownloadCompleted(downloadToCancel, result, responseCode);
            OnDownloadCompleted(downloadToCancel, (long)HttpRequest.Result.RequestFailed, 0);
        }
    }

    private void OnDownloadCompleted(Download download, long result, long responseCode)
    {
        bool success = result == (long)HttpRequest.Result.Success && responseCode == 200;

        if (success)
        {
            GD.Print($"Download completed successfully: {download.FileName}");
            download.CompletionCallback?.Invoke(download.DestinationPath);
        }
        else
        {
            GD.PrintErr($"Download failed or was canceled: {download.FileName}, Result: {result}, Response Code: {responseCode}");
            if (FileAccess.FileExists(download.DestinationPath))
            {
                DirAccess.RemoveAbsolute(download.DestinationPath);
            }
        }

        EmitSignal(SignalName.DownloadCompleted, download.FileName, success);
        
        _activeDownloads.Remove(download);
        download.Request.QueueFree();
    }
}
