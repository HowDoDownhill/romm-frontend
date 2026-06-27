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
    private static readonly System.Net.Http.HttpClient sharedHttpClient;

    static UniversalInstaller()
    {
        sharedHttpClient = new System.Net.Http.HttpClient();
        sharedHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RomM-Frontend/1.0");
    }

    public static async Task<bool> Install(AppInstance appInstance, string emulatorName, EmulatorMeta emulatorMetadata, string currentOperatingSystem)
    {
        if (emulatorMetadata.InstallRecipe == null || !emulatorMetadata.InstallRecipe.ContainsKey(currentOperatingSystem))
        {
            GD.PrintErr($"No install recipe found for {emulatorName} on {currentOperatingSystem}.");
            return false;
        }

        var installRecipe = emulatorMetadata.InstallRecipe[currentOperatingSystem];
        string emulatorTargetDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);

        string resolvedDownloadUrl = await ResolveDownloadUrl(installRecipe);
        if (string.IsNullOrEmpty(resolvedDownloadUrl))
        {
            GD.PrintErr("No valid download URL found.");
            return false;
        }

        string temporaryArchiveFilePath = Path.Combine(appInstance.configManager.DownloadsPath, $"{emulatorName}_download.archive");

        bool downloadSucceeded = await DownloadFileAsync(resolvedDownloadUrl, temporaryArchiveFilePath);
        if (!downloadSucceeded) return false;

        if (installRecipe.Extract)
        {
            string extractionDestinationPath = string.IsNullOrEmpty(installRecipe.ExtractFolderRegex)
                ? emulatorTargetDirectory
                : appInstance.configManager.EmulatorsPath;

            if (extractionDestinationPath == emulatorTargetDirectory && !Directory.Exists(emulatorTargetDirectory))
            {
                Directory.CreateDirectory(emulatorTargetDirectory);
            }

            bool extractionSucceeded = await ExtractArchiveAsync(appInstance, temporaryArchiveFilePath, extractionDestinationPath);
            if (!extractionSucceeded) return false;

            if (!string.IsNullOrEmpty(installRecipe.ExtractFolderRegex))
            {
                var directoriesInEmulatorsPath = Directory.GetDirectories(appInstance.configManager.EmulatorsPath);
                var extractFolderPattern = new Regex("^" + installRecipe.ExtractFolderRegex.Replace("*", ".*") + "$", RegexOptions.IgnoreCase);
                string matchingExtractedDirectory = directoriesInEmulatorsPath.FirstOrDefault(directoryPath => extractFolderPattern.IsMatch(new DirectoryInfo(directoryPath).Name));

                if (matchingExtractedDirectory != null)
                {
                    if (Directory.Exists(emulatorTargetDirectory))
                    {
                        Directory.Delete(emulatorTargetDirectory, true);
                    }
                    Directory.Move(matchingExtractedDirectory, emulatorTargetDirectory);
                }
            }

            File.Delete(temporaryArchiveFilePath);
        }
        else
        {
            if (!Directory.Exists(emulatorTargetDirectory))
            {
                Directory.CreateDirectory(emulatorTargetDirectory);
            }

            string destinationExecutablePath = Path.Combine(emulatorTargetDirectory, emulatorMetadata.ExecutableName[currentOperatingSystem]);
            if (File.Exists(destinationExecutablePath))
            {
                File.Delete(destinationExecutablePath);
            }
            File.Move(temporaryArchiveFilePath, destinationExecutablePath);

            if (currentOperatingSystem != "windows")
            {
                try { Process.Start("chmod", $"+x \"{destinationExecutablePath}\""); } catch { }
            }
        }

        CopyDefaultConfigurations(appInstance, emulatorName, emulatorTargetDirectory);

        return true;
    }

    private static async Task<string> ResolveDownloadUrl(InstallRecipe installRecipe)
    {
        switch (installRecipe.Type)
        {
            case "github_release":
                string githubAssetUrl = await FetchGithubReleaseAssetUrl(installRecipe.Repo, installRecipe.AssetRegex);
                if (string.IsNullOrEmpty(githubAssetUrl))
                {
                    GD.PrintErr("Failed to fetch Github release URL.");
                }
                return githubAssetUrl;

            case "direct_url":
                return installRecipe.Url;

            default:
                GD.PrintErr($"Unknown install recipe type: {installRecipe.Type}");
                return null;
        }
    }

    private static async Task<string> FetchGithubReleaseAssetUrl(string repositorySlug, string assetNameRegexPattern)
    {
        string githubApiUrl = $"https://api.github.com/repos/{repositorySlug}/releases/latest";
        try
        {
            var githubApiResponse = await sharedHttpClient.GetStringAsync(githubApiUrl);
            using var githubApiResponseDocument = JsonDocument.Parse(githubApiResponse);
            var responseRootElement = githubApiResponseDocument.RootElement;

            if (responseRootElement.TryGetProperty("assets", out var releaseAssets))
            {
                var assetNamePattern = new Regex(assetNameRegexPattern, RegexOptions.IgnoreCase);
                foreach (var releaseAsset in releaseAssets.EnumerateArray())
                {
                    if (releaseAsset.TryGetProperty("name", out var assetNameProperty) && releaseAsset.TryGetProperty("browser_download_url", out var downloadUrlProperty))
                    {
                        if (assetNamePattern.IsMatch(assetNameProperty.GetString()))
                        {
                            return downloadUrlProperty.GetString();
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Github API error: {exception.Message}");
        }
        return null;
    }

    private static async Task<bool> DownloadFileAsync(string downloadUrl, string destinationFilePath)
    {
        try
        {
            var httpResponse = await sharedHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            httpResponse.EnsureSuccessStatusCode();

            using var destinationFileStream = new FileStream(destinationFilePath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
            await httpResponse.Content.CopyToAsync(destinationFileStream);
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Download error: {exception.Message}");
            return false;
        }
    }

    private static Task<bool> ExtractArchiveAsync(AppInstance appInstance, string archiveFilePath, string extractionDestinationDirectory)
    {
        var extractionTaskCompletionSource = new TaskCompletionSource<bool>();
        string currentOperatingSystem = OS.GetName().ToLower();

        string archiveToolExecutablePath;
        string archiveToolArguments;

        if (currentOperatingSystem == "windows")
        {
            archiveToolExecutablePath = Path.Combine(appInstance.configManager.ApplicationRootDirectory, "tools", "7zip", "windows", "7za.exe");
            archiveToolArguments = $"x \"{archiveFilePath}\" -o\"{extractionDestinationDirectory}\" -y";
        }
        else
        {
            archiveToolExecutablePath = "7z";
            archiveToolArguments = $"x \"{archiveFilePath}\" -o\"{extractionDestinationDirectory}\" -y";
        }

        try
        {
            var extractionProcess = new Process();
            extractionProcess.StartInfo.FileName = archiveToolExecutablePath;
            extractionProcess.StartInfo.Arguments = archiveToolArguments;
            extractionProcess.StartInfo.UseShellExecute = false;
            extractionProcess.StartInfo.CreateNoWindow = true;
            extractionProcess.EnableRaisingEvents = true;

            extractionProcess.Exited += (sender, exitEventArgs) =>
            {
                if (extractionProcess.ExitCode == 0) extractionTaskCompletionSource.SetResult(true);
                else extractionTaskCompletionSource.SetResult(false);
                extractionProcess.Dispose();
            };

            extractionProcess.Start();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Extraction error: {exception.Message}");
            extractionTaskCompletionSource.SetResult(false);
        }

        return extractionTaskCompletionSource.Task;
    }

    private static void CopyDefaultConfigurations(AppInstance appInstance, string emulatorName, string emulatorTargetDirectory)
    {
        string defaultConfigDirectory = Path.Combine(appInstance.configManager.InstallScriptsPath, emulatorName, "default_config");
        if (Directory.Exists(defaultConfigDirectory))
        {
            try
            {
                CopyDirectoryRecursively(defaultConfigDirectory, emulatorTargetDirectory);
                GD.Print($"Copied default configurations for {emulatorName}");
            }
            catch (Exception exception)
            {
                GD.PrintErr($"Failed to copy default configurations for {emulatorName}: {exception.Message}");
            }
        }
    }

    private static void CopyDirectoryRecursively(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        foreach (string directoryPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directoryPath.Replace(sourceDirectory, targetDirectory));
        }

        foreach (string filePath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(filePath, filePath.Replace(sourceDirectory, targetDirectory), true);
        }
    }
}
