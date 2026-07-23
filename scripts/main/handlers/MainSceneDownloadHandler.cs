using Godot;
using System;
using System.Linq;

public class MainSceneDownloadHandler
{
    private MainScene _mainScene;
    private AppInstance _appInstance;

    public MainSceneDownloadHandler(MainScene mainScene, AppInstance appInstance)
    {
        _mainScene = mainScene;
        _appInstance = appInstance;
    }

    public void SetupDownloadsList()
    {
        if (_mainScene.downloadsListContainer != null)
        {
            _mainScene.downloadsListContainer.Visible = false;
        }
    }

    // Container, footer and focus bookkeeping now lives in MainSceneSectionHandler.
    public void SwapLists()
    {
        _mainScene.SectionHandler.ToggleDownloads();
    }

    public void OnCancelDownloadPressed()
    {
        if (_mainScene.downloadProgressUI != null)
        {
            _mainScene.downloadProgressUI.CancelSelectedDownload();
        }
    }

    public void DownloadGame(Game game)
    {
        if (game == null || game.Files == null || !game.Files.Any())
        {
            GD.PrintErr($"No files found for game: {game?.Name}");
            return;
        }
        
        string downloadUrl = _appInstance.rommApi.GetRomDownloadUrl(game);
        
        if (string.IsNullOrEmpty(downloadUrl))
        {
            GD.PrintErr($"Could not get download URL for game: {game.Name}");
            return;
        }

        string tempZipName = game.Files[0].FileName + ".zip";
        string tempZipPath = _appInstance.configManager.DownloadsPath.PathJoin(tempZipName);
        
        string baseDir = tempZipPath.GetBaseDir();

        if (!DirAccess.DirExistsAbsolute(baseDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(baseDir);
        }
        
        GD.Print($"Starting download for {game.Name} from {downloadUrl} to temporary file {tempZipPath}");
        
        _appInstance.downloadManager.DownloadFile(
            downloadUrl, 
            tempZipPath, 
            _appInstance.rommApi.GetAuthHeaders(),
            (path) => HandleRomDownloadCompletion(path, game),
            game.Id.ToString());
            
        _mainScene.GameListHandler.UpdateDetailsPanelButtons(game);
    }
    
    private void HandleRomDownloadCompletion(string tempZipPath, Game game)
    {
        string fileName = tempZipPath.GetFile();

        if (_mainScene.downloadProgressUI != null)
        {
            _mainScene.downloadProgressUI.SetDownloadStatus(fileName, "Extracting...");
        }

        GD.Print($"Download complete. Starting extraction for: {tempZipPath}");

        string finalDir = _appInstance.configManager.RomsPath.PathJoin(game.System.Slug);
        
        if (!DirAccess.DirExistsAbsolute(finalDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(finalDir);
        }

        string toolsDir = _appInstance.configManager.ToolsPath;
        string sevenZipPath = OS.HasFeature("windows") 
            ? System.IO.Path.Combine(toolsDir, "7zip", "windows", "7za.exe")
            : System.IO.Path.Combine(toolsDir, "7zip", "linux", "7zz");

        try
        {
            if (OS.HasFeature("linux") || OS.GetName() == "Linux" || OS.GetName() == "X11" || OS.GetName() == "Wayland")
            {
                OS.Execute("chmod", new string[] { "+x", sevenZipPath }, new Godot.Collections.Array());
            }
            
            string globalTempZip = ProjectSettings.GlobalizePath(tempZipPath);
            string globalFinalDir = ProjectSettings.GlobalizePath(finalDir);
            
            string[] arguments = { "e", globalTempZip, $"-o{globalFinalDir}", "roms/*", "-r", "-y" };
            
            int exitCode = OS.Execute(sevenZipPath, arguments, new Godot.Collections.Array());
            
            if (exitCode == 0)
            {
                GD.Print($"Successfully extracted {tempZipPath} to {finalDir}");
                
                if (OS.HasFeature("linux") || OS.GetName() == "Linux" || OS.GetName() == "X11" || OS.GetName() == "Wayland")
                {
                    OS.Execute("chmod", new string[] { "-R", "a+rwx", globalFinalDir }, new Godot.Collections.Array());
                }
            }
            else
            {
                GD.PrintErr($"Failed to extract zip file. 7zip exit code: {exitCode}");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Error extracting zip file: {e.Message}");
        }
        finally
        {
            if (Godot.FileAccess.FileExists(tempZipPath))
            {
                DirAccess.RemoveAbsolute(tempZipPath);
                GD.Print($"Deleted temporary file: {tempZipPath}");
            }
        }

        _mainScene.GameListHandler.UpdateDetailsPanelButtons(game);
        _mainScene.GameListHandler.RefreshGameList();
    }
    
    public void OnDownloadCompleted(string fileName, bool success)
    {
        _mainScene.GameListHandler.RefreshGameList();
    }
}
