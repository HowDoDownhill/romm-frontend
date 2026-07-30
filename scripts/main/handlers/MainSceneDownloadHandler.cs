using Godot;
using System;
using System.Linq;

public class MainSceneDownloadHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;

    public MainSceneDownloadHandler(MainScene mainScene, AppInstance appInstance)
    {
        this.mainScene = mainScene;
        this.appInstance = appInstance;
    }

    public void SetupDownloadsList()
    {
        mainScene.downloadsListContainer?.Close(false);
    }

    public void SwapLists()
    {
        mainScene.SectionHandler.ToggleDownloads();
    }

    public void OnCancelDownloadPressed()
    {
        if (mainScene.downloadProgressUI != null)
        {
            mainScene.downloadProgressUI.CancelSelectedDownload();
        }
    }

    public void DownloadGame(Game game)
    {
        if (game == null || game.Files == null || !game.Files.Any())
        {
            GD.PrintErr($"No files found for game: {game?.Name}");
            return;
        }

        string downloadUrl = appInstance.rommApi.GetRomDownloadUrl(game);

        if (string.IsNullOrEmpty(downloadUrl))
        {
            GD.PrintErr($"Could not get download URL for game: {game.Name}");
            return;
        }

        string tempZipName = game.Files[0].FileName + ".zip";
        string tempZipPath = appInstance.configManager.DownloadsPath.PathJoin(tempZipName);

        string baseDir = tempZipPath.GetBaseDir();

        if (!DirAccess.DirExistsAbsolute(baseDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(baseDir);
        }

        GD.Print($"Starting download for {game.Name} from {downloadUrl} to temporary file {tempZipPath}");

        appInstance.downloadManager.DownloadFile(
            downloadUrl,
            tempZipPath,
            appInstance.rommApi.GetAuthHeaders(),
            (path) => HandleRomDownloadCompletion(path, game),
            game.Id.ToString(),
            game.FileSizeBytes);

        mainScene.GameListHandler.UpdateDetailsPanelButtons(game);
    }

    private void HandleRomDownloadCompletion(string tempZipPath, Game game)
    {
        string fileName = tempZipPath.GetFile();

        if (mainScene.downloadProgressUI != null)
        {
            mainScene.downloadProgressUI.SetDownloadStatus(fileName, "Extracting...");
        }

        GD.Print($"Download complete. Starting extraction for: {tempZipPath}");

        string finalDir = appInstance.configManager.RomsPath.PathJoin(game.System.Slug);

        if (!DirAccess.DirExistsAbsolute(finalDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(finalDir);
        }

        string toolsDir = appInstance.configManager.ToolsPath;
        string sevenZipPath = OS.HasFeature("windows")
            ? System.IO.Path.Combine(toolsDir, "7zip", "windows", "7za.exe")
            : System.IO.Path.Combine(toolsDir, "7zip", "linux", "7zz");

        string globalTempZip = ProjectSettings.GlobalizePath(tempZipPath);
        string globalFinalDir = ProjectSettings.GlobalizePath(finalDir);
        bool isLinux = OS.HasFeature("linux") || OS.GetName() == "Linux" || OS.GetName() == "X11" || OS.GetName() == "Wayland";

        System.Threading.Tasks.Task.Run(() =>
        {
            ExtractDownloadedRom(sevenZipPath, globalTempZip, globalFinalDir, isLinux);

            Callable.From(() => HandleExtractionFinished(tempZipPath, game)).CallDeferred();
        });
    }

    private void ExtractDownloadedRom(string sevenZipPath, string globalTempZip, string globalFinalDir, bool isLinux)
    {
        try
        {
            if (isLinux)
            {
                RunProcessToCompletion("chmod", new string[] { "+x", sevenZipPath });
            }

            string[] arguments = { "e", globalTempZip, $"-o{globalFinalDir}", "roms/*", "-r", "-y" };

            int exitCode = RunProcessToCompletion(sevenZipPath, arguments);

            if (exitCode != 0)
            {
                GD.PrintErr($"Failed to extract zip file. 7zip exit code: {exitCode}");
                return;
            }

            GD.Print($"Successfully extracted {globalTempZip} to {globalFinalDir}");

            if (isLinux)
            {
                RunProcessToCompletion("chmod", new string[] { "-R", "a+rwx", globalFinalDir });
            }
        }
        catch (System.Exception extractionException)
        {
            GD.PrintErr($"Error extracting zip file: {extractionException.Message}");
        }
    }

    private static int RunProcessToCompletion(string executablePath, string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo);

        process.WaitForExit();

        return process.ExitCode;
    }

    private void HandleExtractionFinished(string tempZipPath, Game game)
    {
        if (Godot.FileAccess.FileExists(tempZipPath))
        {
            DirAccess.RemoveAbsolute(tempZipPath);
            GD.Print($"Deleted temporary file: {tempZipPath}");
        }

        mainScene.GameListHandler.UpdateDetailsPanelButtons(game);
        mainScene.GameListHandler.RefreshGameList();
        mainScene.NetplayHandler?.OnLocalRomLibraryChanged();
    }

    public void OnDownloadCompleted(string fileName, bool success)
    {
        mainScene.GameListHandler.RefreshGameList();
    }

}
