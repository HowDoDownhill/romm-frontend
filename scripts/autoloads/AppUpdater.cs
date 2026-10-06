using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

public partial class AppUpdater : Node
{
    public const string CurrentVersion = "v1.0.16";
    private const string RepoOwner = "HowDoDownhill";
    private const string RepoName = "romm-frontend";
    private const int TransferBufferSizeBytes = 1024 * 1024;

    private readonly System.Net.Http.HttpClient httpClient = new System.Net.Http.HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    private AppInstance appInstance;

    private long updateBytesDownloaded;
    private long updateTotalBytes;
    private bool isDownloadingUpdate;
    private float lastEmittedDownloadProgress = -1.0f;

    [Signal]
    public delegate void UpdateAvailableEventHandler(string version, string releaseNotes);

    [Signal]
    public delegate void UpdateDownloadProgressEventHandler(float progress);

    [Signal]
    public delegate void UpdateDownloadCompletedEventHandler(bool success);

    public override void _Ready()
    {
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RommFrontendUpdater", "1.0"));

        appInstance = GetNode<AppInstance>("/root/AppInstance");

        string downloadsPath = appInstance.configManager.DownloadsPath;
        if (!DirAccess.DirExistsAbsolute(downloadsPath))
        {
            DirAccess.MakeDirRecursiveAbsolute(downloadsPath);
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                GD.PrintErr($"Failed to check for updates: {response.StatusCode}");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            var releaseInfo = JsonSerializer.Deserialize(json, RommJsonContext.Default.GithubReleaseInfo);
            
            if (releaseInfo != null && !string.IsNullOrEmpty(releaseInfo.TagName))
            {
                if (IsNewerVersion(releaseInfo.TagName, CurrentVersion))
                {
                    CallDeferred(MethodName.EmitSignal, SignalName.UpdateAvailable, releaseInfo.TagName, releaseInfo.Body ?? "");
                }
            }
        }

        catch (Exception ex)
        {
            GD.PrintErr($"Exception checking for updates: {ex.Message}");
        }
    }

    private bool IsNewerVersion(string remoteVersion, string localVersion)
    {
       
        string cleanRemote = remoteVersion.TrimStart('v', 'V');
        string cleanLocal = localVersion.TrimStart('v', 'V');

        if (Version.TryParse(cleanRemote, out Version vRemote) && Version.TryParse(cleanLocal, out Version vLocal))
        {
            return vRemote > vLocal;
        }
        
        return cleanRemote != cleanLocal;
    }

    public async Task DownloadUpdateAsync(string remoteVersion)
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/tags/{remoteVersion}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                CallDeferred(MethodName.EmitSignal, SignalName.UpdateDownloadCompleted, false);
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            var releaseInfo = JsonSerializer.Deserialize(json, RommJsonContext.Default.GithubReleaseInfo);

            
            string targetZipName = OS.HasFeature("windows") ? "romm-frontend-windows.zip" : "romm-frontend-linux.zip";
            
            var asset = releaseInfo?.Assets?.FirstOrDefault(a => a.Name.Contains(targetZipName, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                
                asset = releaseInfo?.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            }

            if (asset == null)
            {
                GD.PrintErr("No suitable zip asset found in the release.");
                CallDeferred(MethodName.EmitSignal, SignalName.UpdateDownloadCompleted, false);
                return;
            }

            string downloadsPath = appInstance.configManager.DownloadsPath;
            string downloadPath = System.IO.Path.Combine(downloadsPath, "update.zip");
            
            using (var downloadResponse = await httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                downloadResponse.EnsureSuccessStatusCode();
                var totalBytes = downloadResponse.Content.Headers.ContentLength;
                
                Interlocked.Exchange(ref updateBytesDownloaded, 0);
                Interlocked.Exchange(ref updateTotalBytes, totalBytes ?? 0);
                Volatile.Write(ref isDownloadingUpdate, true);

                var downloadManager = appInstance?.downloadManager;
                var externalTransfer = downloadManager?.BeginExternalTransfer($"Update {remoteVersion}");

                using (var contentStream = await downloadResponse.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(downloadPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, TransferBufferSizeBytes, true))
                {
                    var totalRead = 0L;
                    var buffer = new byte[TransferBufferSizeBytes];

                    while (true)
                    {
                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read == 0)
                        {
                            break;
                        }

                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        Interlocked.Exchange(ref updateBytesDownloaded, totalRead);
                        downloadManager?.ReportExternalTransferProgress(externalTransfer, totalRead, totalBytes ?? 0);
                    }
                }

                downloadManager?.CompleteExternalTransfer(externalTransfer, true);
                Volatile.Write(ref isDownloadingUpdate, false);
            }

            CallDeferred(MethodName.EmitSignal, SignalName.UpdateDownloadCompleted, true);
        }

        catch (Exception ex)
        {
            Volatile.Write(ref isDownloadingUpdate, false);
            GD.PrintErr($"Exception downloading update: {ex.Message}");
            CallDeferred(MethodName.EmitSignal, SignalName.UpdateDownloadCompleted, false);
        }
    }

    public override void _Process(double delta)
    {
        if (!Volatile.Read(ref isDownloadingUpdate))
        {
            return;
        }

        long totalBytes = Interlocked.Read(ref updateTotalBytes);

        if (totalBytes <= 0)
        {
            return;
        }

        float downloadProgress = (float)Interlocked.Read(ref updateBytesDownloaded) / totalBytes;

        if (downloadProgress == lastEmittedDownloadProgress)
        {
            return;
        }

        lastEmittedDownloadProgress = downloadProgress;
        EmitSignal(SignalName.UpdateDownloadProgress, downloadProgress);
    }

    public void ApplyUpdateAndRestart()
    {
        string downloadsPath = appInstance.configManager.DownloadsPath;
        string downloadPath = System.IO.Path.Combine(downloadsPath, "update.zip");

        if (!File.Exists(downloadPath))
        {
            GD.PrintErr("Update zip not found.");
            return;
        }

        string appPath = OS.GetExecutablePath();
        string appDir = System.IO.Path.GetDirectoryName(appPath);
        string toolsDir = appInstance.configManager.ToolsPath;
        
        if (OS.HasFeature("windows"))
        {
            string sevenZipPath = System.IO.Path.Combine(toolsDir, "7zip", "windows", "7za.exe");
            string batPath = System.IO.Path.Combine(downloadsPath, "update.bat");
            string batContent = $@"
@echo off
echo Waiting for application to close...
timeout /t 3 /nobreak >nul
echo Extracting update...
""{sevenZipPath}"" x ""{downloadPath}"" -o""{appDir}"" -y
echo Restarting application...
start """" ""{appPath}""
del ""{downloadPath}""
del ""%~f0""
";
            File.WriteAllText(batPath, batContent);
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            });
        }
        else if (OS.HasFeature("linux"))
        {
            string sevenZipPath = System.IO.Path.Combine(toolsDir, "7zip", "linux", "7zz");
            string shPath = System.IO.Path.Combine(downloadsPath, "update.sh");
            string shContent = $@"#!/bin/bash
sleep 3
chmod +x ""{sevenZipPath}""
""{sevenZipPath}"" x ""{downloadPath}"" -o""{appDir}"" -y
chmod +x ""{appPath}""
""{appPath}"" &
rm ""{downloadPath}""
rm ""$0""
".Replace("\r", "");
            File.WriteAllText(shPath, shContent);
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"chmod +x '{shPath}' && '{shPath}' &\"",
                UseShellExecute = false
            });
        }
        
        GetTree().Quit();
    }
}
