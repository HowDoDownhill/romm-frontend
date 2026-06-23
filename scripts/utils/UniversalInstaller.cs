using Godot;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

public static class UniversalInstaller
{
    private static readonly System.Net.Http.HttpClient _httpClient;

    static UniversalInstaller()
    {
        _httpClient = new System.Net.Http.HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RomM-Frontend/1.0");
    }

    public static async Task<bool> Install(AppInstance appInstance, string emulatorName, EmulatorMeta meta, string osName)
    {
        if (meta.InstallRecipe == null || !meta.InstallRecipe.ContainsKey(osName))
        {
            GD.PrintErr($"No install recipe found for {emulatorName} on {osName}.");
            return false;
        }

        var recipe = meta.InstallRecipe[osName];
        string targetDir = Path.Combine(appInstance.configManager.EmulatorsPath, meta.EmulatorDirName[osName]);
        
        string downloadUrl = recipe.Url;

        if (recipe.Type == "github_release")
        {
            downloadUrl = await GetGithubReleaseAssetUrl(recipe.Repo, recipe.AssetRegex);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                GD.PrintErr("Failed to fetch Github release URL.");
                return false;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            GD.PrintErr("No valid download URL found.");
            return false;
        }

        string tempPath = Path.Combine(appInstance.configManager.DownloadsPath, $"{emulatorName}_download.archive");
        GD.Print($"Downloading {downloadUrl} to {tempPath}");

        bool downloaded = await DownloadFileAsync(downloadUrl, tempPath);
        if (!downloaded) return false;

        if (recipe.Extract)
        {
            GD.Print("Extracting archive...");
            
            string extractDest = string.IsNullOrEmpty(recipe.ExtractFolderRegex) 
                ? targetDir 
                : appInstance.configManager.EmulatorsPath;

            if (extractDest == targetDir && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            bool extracted = await ExtractArchiveAsync(appInstance, tempPath, extractDest);
            if (!extracted) return false;

            // Handle renaming the extracted folder if necessary
            if (!string.IsNullOrEmpty(recipe.ExtractFolderRegex))
            {
                var dirs = Directory.GetDirectories(appInstance.configManager.EmulatorsPath);
                var regex = new Regex("^" + recipe.ExtractFolderRegex.Replace("*", ".*") + "$", RegexOptions.IgnoreCase);
                string foundDir = dirs.FirstOrDefault(d => regex.IsMatch(new DirectoryInfo(d).Name));

                if (foundDir != null)
                {
                    // If target dir already exists, clear it out for update
                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, true);
                    }
                    Directory.Move(foundDir, targetDir);
                }
            }

            File.Delete(tempPath);
        }
        else
        {
            // Just move the file
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            
            // Assume it's the executable (like an AppImage)
            string dest = Path.Combine(targetDir, meta.ExecutableName[osName]);
            if (File.Exists(dest))
            {
                File.Delete(dest);
            }
            File.Move(tempPath, dest);
            
            // On Linux/Mac, make it executable
            if (osName != "windows")
            {
                try { Process.Start("chmod", $"+x \"{dest}\""); } catch { }
            }
        }

        return true;
    }

    private static async Task<string> GetGithubReleaseAssetUrl(string repo, string assetRegex)
    {
        string apiUrl = $"https://api.github.com/repos/{repo}/releases/latest";
        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("assets", out var assets))
            {
                var regex = new Regex(assetRegex, RegexOptions.IgnoreCase);
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameProp) && asset.TryGetProperty("browser_download_url", out var urlProp))
                    {
                        if (regex.IsMatch(nameProp.GetString()))
                        {
                            return urlProp.GetString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Github API error: {ex.Message}");
        }
        return null;
    }

    private static async Task<bool> DownloadFileAsync(string url, string destPath)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fs = new FileStream(destPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Download error: {ex.Message}");
            return false;
        }
    }

    private static Task<bool> ExtractArchiveAsync(AppInstance appInstance, string archivePath, string destDir)
    {
        var tcs = new TaskCompletionSource<bool>();
        string osName = OS.GetName().ToLower();

        string toolPath = "";
        string args = "";

        if (osName == "windows")
        {
            toolPath = Path.Combine(appInstance.configManager.rootDir, "tools", "7zip", "windows", "7za.exe");
            args = $"x \"{archivePath}\" -o\"{destDir}\" -y";
        }
        else
        {
            toolPath = "7z";
            args = $"x \"{archivePath}\" -o\"{destDir}\" -y";
        }

        try
        {
            var process = new Process();
            process.StartInfo.FileName = toolPath;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.EnableRaisingEvents = true;

            process.Exited += (sender, e) =>
            {
                if (process.ExitCode == 0) tcs.SetResult(true);
                else tcs.SetResult(false);
                process.Dispose();
            };

            process.Start();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Extraction error: {ex.Message}");
            tcs.SetResult(false);
        }

        return tcs.Task;
    }
}
