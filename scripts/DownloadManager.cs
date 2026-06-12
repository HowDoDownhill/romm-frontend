using Godot;
using System;
using System.Collections.Generic;

public partial class DownloadManager : Node
{
    [Signal]
    public delegate void DownloadProgressUpdatedEventHandler(string fileName, long current, long total);

    [Signal]
    public delegate void DownloadCompletedEventHandler(string fileName);

    private class Download
    {
        public HttpRequest Request { get; set; }
        public string FileName { get; set; }
        public string DestinationPath { get; set; }
        public Action<string> OnSuccessCallback { get; set; }
    }

    private List<Download> _activeDownloads = new List<Download>();

    public void DownloadFile(string url, string destinationPath, string[] headers = null, Action<string> onSuccess = null)
    {
        var request = new HttpRequest();
        AddChild(request);

        var download = new Download
        {
            Request = request,
            FileName = destinationPath.GetFile(),
            DestinationPath = destinationPath,
            OnSuccessCallback = onSuccess
        };

        _activeDownloads.Add(download);

        request.DownloadFile = destinationPath;
        request.UseThreads = true;
        request.Request(url, headers);

        request.RequestCompleted += (long result, long responseCode, string[] responseHeaders, byte[] body) =>
        {
            OnDownloadCompleted(download, result, responseCode);
        };
    }

    public override void _Process(double delta)
    {
        for (int i = _activeDownloads.Count - 1; i >= 0; i--)
        {
            var download = _activeDownloads[i];
            if (download.Request != null && IsInstanceValid(download.Request))
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
    }

    private void OnDownloadCompleted(Download download, long result, long responseCode)
    {
        if (result == (long)HttpRequest.Result.Success && responseCode == 200)
        {
            GD.Print($"Download completed successfully: {download.FileName}");
            EmitSignal(SignalName.DownloadCompleted, download.FileName);
            
            download.OnSuccessCallback?.Invoke(download.DestinationPath);
        }
        else
        {
            GD.PrintErr($"Download failed: {download.FileName}, Result: {result}, Response Code: {responseCode}");
        }

        _activeDownloads.Remove(download);
        if (download.Request != null && IsInstanceValid(download.Request))
        {
            download.Request.QueueFree();
        }
    }
}
