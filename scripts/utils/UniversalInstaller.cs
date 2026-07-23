using Godot;
using System;
using System.Collections.Generic;
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

    public static async Task<bool> Install(AppInstance appInstance, string emulatorName, EmulatorMeta emulatorMetadata, string currentOperatingSystem, ReleaseOption selectedRelease = null)
    {
        if (emulatorMetadata.InstallRecipe == null || !emulatorMetadata.InstallRecipe.ContainsKey(currentOperatingSystem))
        {
            GD.PrintErr($"No install recipe found for {emulatorName} on {currentOperatingSystem}.");
            return false;
        }

        var installRecipe = emulatorMetadata.InstallRecipe[currentOperatingSystem];
        string emulatorTargetDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);

        if (selectedRelease == null)
        {
            var availableReleases = await ListReleases(installRecipe);
            selectedRelease = availableReleases.FirstOrDefault();
        }

        if (selectedRelease == null || string.IsNullOrEmpty(selectedRelease.DownloadUrl))
        {
            GD.PrintErr("No valid download URL found.");
            return false;
        }

        string temporaryArchiveFilePath = Path.Combine(appInstance.configManager.DownloadsPath, $"{emulatorName}download.archive");

        bool downloadSucceeded = await DownloadFileAsync(selectedRelease.DownloadUrl, temporaryArchiveFilePath);

        if (!downloadSucceeded)
        {
            return false;
        }

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

            if (!extractionSucceeded)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(installRecipe.ExtractFolderRegex))
            {
                var directoriesInEmulatorsPath = Directory.GetDirectories(appInstance.configManager.EmulatorsPath);
                var extractFolderPattern = new Regex("^" + installRecipe.ExtractFolderRegex.Replace("*", ".*") + "$", RegexOptions.IgnoreCase);
                string matchingExtractedDirectory = directoriesInEmulatorsPath.FirstOrDefault(directoryPath => extractFolderPattern.IsMatch(new DirectoryInfo(directoryPath).Name));

                if (matchingExtractedDirectory != null)
                {
                    if (Directory.Exists(emulatorTargetDirectory))
                    {
                        ClearDirectoryPreservingPaths(emulatorTargetDirectory, emulatorMetadata.GetPreservePaths());
                        CopyDirectoryRecursively(matchingExtractedDirectory, emulatorTargetDirectory);
                        Directory.Delete(matchingExtractedDirectory, true);
                    }

                    else
                    {
                        Directory.Move(matchingExtractedDirectory, emulatorTargetDirectory);
                    }
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

            string destinationExecutableName = emulatorMetadata.ExecutableName != null && emulatorMetadata.ExecutableName.ContainsKey(currentOperatingSystem)
                ? emulatorMetadata.ExecutableName[currentOperatingSystem]
                : selectedRelease.AssetName;

            if (emulatorMetadata.ExecutableRegex != null && emulatorMetadata.ExecutableRegex.ContainsKey(currentOperatingSystem))
            {
                destinationExecutableName = selectedRelease.AssetName;
                var executablePattern = new Regex(emulatorMetadata.ExecutableRegex[currentOperatingSystem], RegexOptions.IgnoreCase);

                foreach (string existingFilePath in Directory.GetFiles(emulatorTargetDirectory))
                {
                    if (executablePattern.IsMatch(Path.GetFileName(existingFilePath)))
                    {
                        File.Delete(existingFilePath);
                    }
                }
            }

            string destinationExecutablePath = Path.Combine(emulatorTargetDirectory, destinationExecutableName);

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

    public static async Task<List<ReleaseOption>> ListReleases(InstallRecipe installRecipe)
    {
        switch (installRecipe.Type)
        {
            case "github_release":
                return await ListGithubReleases(installRecipe.Repo, installRecipe.AssetRegex);

            case "github_tags":
                return await ListGithubTagReleases(installRecipe.Repo, installRecipe.TagRegex, installRecipe.UrlTemplate);

            case "web_scrape":
                return await ListScrapedReleases(installRecipe);

            case "direct_url":
                return new List<ReleaseOption>
                {
                    new ReleaseOption
                    {
                        VersionLabel = "Latest",
                        AssetName = installRecipe.Url?.Split('/').LastOrDefault(),
                        DownloadUrl = installRecipe.Url
                    }
                };

            default:
                GD.PrintErr($"Unknown install recipe type: {installRecipe.Type}");
                return new List<ReleaseOption>();
        }
    }

    private static async Task<List<ReleaseOption>> ListGithubReleases(string repositorySlug, string assetNameRegexPattern)
    {
        var stableReleaseOptions = new List<ReleaseOption>();
        var prereleaseOptions = new List<ReleaseOption>();
        string githubApiUrl = $"https://api.github.com/repos/{repositorySlug}/releases?per_page=30";

        try
        {
            var githubApiResponse = await sharedHttpClient.GetStringAsync(githubApiUrl);
            using var githubApiResponseDocument = JsonDocument.Parse(githubApiResponse);
            var assetNamePattern = new Regex(assetNameRegexPattern, RegexOptions.IgnoreCase);

            foreach (var release in githubApiResponseDocument.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draftProperty) && draftProperty.GetBoolean())
                {
                    continue;
                }

                if (!release.TryGetProperty("assets", out var releaseAssets))
                {
                    continue;
                }

                bool isPrerelease = release.TryGetProperty("prerelease", out var prereleaseProperty) && prereleaseProperty.GetBoolean();

                foreach (var releaseAsset in releaseAssets.EnumerateArray())
                {
                    if (releaseAsset.TryGetProperty("name", out var assetNameProperty) && releaseAsset.TryGetProperty("browser_download_url", out var downloadUrlProperty))
                    {
                        if (assetNamePattern.IsMatch(assetNameProperty.GetString()))
                        {
                            string versionLabel = release.TryGetProperty("tag_name", out var tagProperty) ? tagProperty.GetString() : assetNameProperty.GetString();
                            string publishedDate = "";

                            if (release.TryGetProperty("published_at", out var publishedProperty) && publishedProperty.ValueKind == JsonValueKind.String)
                            {
                                publishedDate = publishedProperty.GetString().Split('T')[0];
                            }

                            var releaseOption = new ReleaseOption
                            {
                                VersionLabel = versionLabel,
                                AssetName = assetNameProperty.GetString(),
                                DownloadUrl = downloadUrlProperty.GetString(),
                                PublishedDate = publishedDate
                            };

                            (isPrerelease ? prereleaseOptions : stableReleaseOptions).Add(releaseOption);
                            break;
                        }
                    }
                }
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Github API error: {exception.Message}");
        }

        return stableReleaseOptions.Count > 0 ? stableReleaseOptions : prereleaseOptions;
    }

    private static async Task<List<ReleaseOption>> ListGithubTagReleases(string repositorySlug, string tagRegexPattern, string urlTemplate)
    {
        var releaseOptions = new List<ReleaseOption>();
        string githubApiUrl = $"https://api.github.com/repos/{repositorySlug}/tags?per_page=30";

        try
        {
            var githubApiResponse = await sharedHttpClient.GetStringAsync(githubApiUrl);
            using var githubApiResponseDocument = JsonDocument.Parse(githubApiResponse);
            var tagPattern = new Regex(tagRegexPattern, RegexOptions.IgnoreCase);

            foreach (var tag in githubApiResponseDocument.RootElement.EnumerateArray())
            {
                if (!tag.TryGetProperty("name", out var tagNameProperty))
                {
                    continue;
                }

                var tagMatch = tagPattern.Match(tagNameProperty.GetString());

                if (!tagMatch.Success)
                {
                    continue;
                }

                string version = tagMatch.Groups.Count > 1 && tagMatch.Groups[1].Success ? tagMatch.Groups[1].Value : tagMatch.Value;
                string downloadUrl = urlTemplate.Replace("{version}", version);

                releaseOptions.Add(new ReleaseOption
                {
                    VersionLabel = version,
                    AssetName = downloadUrl.Split('/').LastOrDefault(),
                    DownloadUrl = downloadUrl
                });
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Github API error: {exception.Message}");
        }

        return releaseOptions;
    }

    private static async Task<List<ReleaseOption>> ListScrapedReleases(InstallRecipe installRecipe)
    {
        var releaseOptions = new List<ReleaseOption>();

        try
        {
            string pageContent = await sharedHttpClient.GetStringAsync(installRecipe.ListUrl);

            if (!string.IsNullOrEmpty(installRecipe.UrlTemplate) && !string.IsNullOrEmpty(installRecipe.VersionRegex))
            {
                var versionPattern = new Regex(installRecipe.VersionRegex, RegexOptions.IgnoreCase);
                var seenVersions = new HashSet<string>();

                foreach (Match versionMatch in versionPattern.Matches(pageContent))
                {
                    string version = versionMatch.Groups.Count > 1 ? versionMatch.Groups[1].Value : versionMatch.Value;

                    if (!seenVersions.Add(version))
                    {
                        continue;
                    }

                    string downloadUrl = installRecipe.UrlTemplate.Replace("{version}", version);

                    releaseOptions.Add(new ReleaseOption
                    {
                        VersionLabel = version,
                        AssetName = downloadUrl.Split('/').LastOrDefault(),
                        DownloadUrl = downloadUrl
                    });
                }
            }

            else if (!string.IsNullOrEmpty(installRecipe.LinkRegex))
            {
                var linkPattern = new Regex(installRecipe.LinkRegex, RegexOptions.IgnoreCase);
                var versionPattern = string.IsNullOrEmpty(installRecipe.VersionRegex) ? null : new Regex(installRecipe.VersionRegex, RegexOptions.IgnoreCase);
                var seenUrls = new HashSet<string>();

                foreach (Match linkMatch in linkPattern.Matches(pageContent))
                {
                    string downloadUrl = linkMatch.Value;

                    if (!seenUrls.Add(downloadUrl))
                    {
                        continue;
                    }

                    string assetName = downloadUrl.Split('/').LastOrDefault();
                    string versionLabel = assetName;

                    if (versionPattern != null)
                    {
                        var versionMatch = versionPattern.Match(downloadUrl);

                        if (versionMatch.Success)
                        {
                            versionLabel = versionMatch.Groups.Count > 1 ? versionMatch.Groups[1].Value : versionMatch.Value;
                        }
                    }

                    releaseOptions.Add(new ReleaseOption
                    {
                        VersionLabel = versionLabel,
                        AssetName = assetName,
                        DownloadUrl = downloadUrl
                    });
                }
            }

            else
            {
                GD.PrintErr("web_scrape recipe needs link_regex, or version_regex with url_template.");
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Scrape error for {installRecipe.ListUrl}: {exception.Message}");
        }

        return releaseOptions;
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
                if (extractionProcess.ExitCode == 0)
                {
                    extractionTaskCompletionSource.SetResult(true);
                }

                else
                {
                    extractionTaskCompletionSource.SetResult(false);
                }

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

    public static void ClearDirectoryPreservingPaths(string directoryPath, List<string> relativePathsToPreserve)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        string rootFullPath = Path.GetFullPath(directoryPath);

        var exactlyPreservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ancestorsOfPreservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string relativePath in relativePathsToPreserve ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            string preservedFullPath = Path.GetFullPath(Path.Combine(directoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!Directory.Exists(preservedFullPath) && !File.Exists(preservedFullPath))
            {
                continue;
            }

            exactlyPreservedPaths.Add(preservedFullPath);

            string parentPath = Path.GetDirectoryName(preservedFullPath);

            while (!string.IsNullOrEmpty(parentPath) && !string.Equals(parentPath, rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                ancestorsOfPreservedPaths.Add(parentPath);
                parentPath = Path.GetDirectoryName(parentPath);
            }
        }

        foreach (string filePath in Directory.GetFiles(directoryPath))
        {
            if (!exactlyPreservedPaths.Contains(Path.GetFullPath(filePath)))
            {
                try { File.Delete(filePath); } catch (Exception exception) { GD.PrintErr($"Failed to delete {filePath}: {exception.Message}"); }
            }
        }

        foreach (string subdirectoryPath in Directory.GetDirectories(directoryPath))
        {
            string fullSubdirectoryPath = Path.GetFullPath(subdirectoryPath);

            if (exactlyPreservedPaths.Contains(fullSubdirectoryPath))
            {
                continue;
            }

            if (ancestorsOfPreservedPaths.Contains(fullSubdirectoryPath))
            {
                ClearDirectoryPreservingPaths(subdirectoryPath, relativePathsToPreserve
                    .Select(relativePath => Path.GetFullPath(Path.Combine(directoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                    .Where(preservedPath => preservedPath.StartsWith(fullSubdirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    .Select(preservedPath => preservedPath.Substring(fullSubdirectoryPath.Length + 1))
                    .ToList());

                continue;
            }

            try { Directory.Delete(subdirectoryPath, true); } catch (Exception exception) { GD.PrintErr($"Failed to delete {subdirectoryPath}: {exception.Message}"); }
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
