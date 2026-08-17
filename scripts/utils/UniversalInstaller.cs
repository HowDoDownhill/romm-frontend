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
    private const int TransferBufferSizeBytes = 1024 * 1024;

    private static readonly string[] OperatingSystemConfigFolderNames = { "windows", "linux", "macos" };

    private static readonly System.Net.Http.HttpClient sharedHttpClient;

    static UniversalInstaller()
    {
        sharedHttpClient = new System.Net.Http.HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        sharedHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RomM-Frontend/1.0");
    }

    public static async Task<bool> Install(AppInstance appInstance, string emulatorName, EmulatorMeta emulatorMetadata, string currentOperatingSystem, ReleaseOption selectedRelease = null)
    {
        var installTransfer = appInstance?.downloadManager?.BeginExternalTransfer(emulatorMetadata?.Name ?? emulatorName);

        try
        {
            bool installSucceeded = await RunInstall(appInstance, emulatorName, emulatorMetadata, currentOperatingSystem, selectedRelease, installTransfer);
            appInstance?.downloadManager?.CompleteExternalTransfer(installTransfer, installSucceeded);
            return installSucceeded;
        }

        catch (Exception)
        {
            appInstance?.downloadManager?.CompleteExternalTransfer(installTransfer, false);
            throw;
        }
    }

    private static void ReportStage(AppInstance appInstance, DownloadManager.ExternalTransfer installTransfer, string stageDescription)
    {
        appInstance?.downloadManager?.ReportExternalTransferStage(installTransfer, stageDescription);
    }

    private static async Task<bool> RunInstall(AppInstance appInstance, string emulatorName, EmulatorMeta emulatorMetadata, string currentOperatingSystem, ReleaseOption selectedRelease, DownloadManager.ExternalTransfer installTransfer)
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
            ReportStage(appInstance, installTransfer, "Finding the latest release...");

            var availableReleases = await ListReleases(installRecipe);
            selectedRelease = availableReleases.FirstOrDefault();
        }

        if (selectedRelease == null || string.IsNullOrEmpty(selectedRelease.DownloadUrl))
        {
            GD.PrintErr("No valid download URL found.");
            return false;
        }

        string temporaryArchiveFilePath = Path.Combine(appInstance.configManager.DownloadsPath, $"{emulatorName}download.archive");

        ReportStage(appInstance, installTransfer, null);

        bool downloadSucceeded = await DownloadIntoTransferAsync(appInstance, selectedRelease.DownloadUrl, temporaryArchiveFilePath, installTransfer);

        if (!downloadSucceeded)
        {
            return false;
        }

        if (installRecipe.Extract)
        {
            ReportStage(appInstance, installTransfer, "Extracting...");

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
        }

        if (currentOperatingSystem != "windows")
        {
            EnsureExecutableBit(ResolveInstalledExecutablePath(emulatorMetadata, currentOperatingSystem, emulatorTargetDirectory));
        }

        if (!await DownloadExtraFiles(appInstance, emulatorName, installRecipe, emulatorTargetDirectory, selectedRelease.VersionLabel, installTransfer))
        {
            return false;
        }

        ReportStage(appInstance, installTransfer, "Finishing the install...");

        CopyDefaultConfigurations(appInstance, emulatorName, emulatorTargetDirectory);
        SaveStore.LinkEmulatorSaveDirectories(appInstance, emulatorName, emulatorMetadata, currentOperatingSystem, null);
        WriteInstalledVersion(emulatorTargetDirectory, selectedRelease.VersionLabel);

        return true;
    }

    public const string InstalledVersionFileName = "installed_version.txt";

    private static void WriteInstalledVersion(string emulatorTargetDirectory, string versionLabel)
    {
        if (string.IsNullOrWhiteSpace(versionLabel))
        {
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(emulatorTargetDirectory, InstalledVersionFileName), versionLabel.Trim());
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Could not record the installed version: {exception.Message}");
        }
    }

    public static async Task<bool> EnsureCoreInstalled(AppInstance appInstance, string emulatorName, EmulatorMeta emulatorMetadata, string coreName, string currentOperatingSystem)
    {
        string coreRelativePath = emulatorMetadata.ResolveCoreRelativePath(coreName, currentOperatingSystem);

        if (coreRelativePath == null)
        {
            return true;
        }

        string emulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
        string coreFilePath = Path.GetFullPath(Path.Combine(emulatorInstallDirectory, coreRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (File.Exists(coreFilePath))
        {
            return true;
        }

        string coreDownloadUrl = emulatorMetadata.ResolveCoreDownloadUrl(coreName, currentOperatingSystem);

        if (string.IsNullOrEmpty(coreDownloadUrl))
        {
            GD.PrintErr($"The {coreName} core is missing from {emulatorName} and no per-core download is configured. Reinstall {emulatorName} to restore its cores.");
            return false;
        }

        string coreDirectoryPath = Path.GetDirectoryName(coreFilePath);
        Directory.CreateDirectory(coreDirectoryPath);

        string temporaryArchiveFilePath = Path.Combine(appInstance.configManager.DownloadsPath, $"{emulatorName}core.archive");
        GD.Print($"Downloading the {coreName} core for {emulatorName}...");

        var coreTransfer = appInstance?.downloadManager?.BeginExternalTransfer($"{emulatorName} {coreName} core");

        if (!await DownloadIntoTransferAsync(appInstance, coreDownloadUrl, temporaryArchiveFilePath, coreTransfer))
        {
            GD.PrintErr($"Failed to download the {coreName} core from {coreDownloadUrl}.");
            appInstance?.downloadManager?.CompleteExternalTransfer(coreTransfer, false);
            return false;
        }

        ReportStage(appInstance, coreTransfer, "Extracting...");

        bool extractionSucceeded = await ExtractArchiveAsync(appInstance, temporaryArchiveFilePath, coreDirectoryPath);
        try { File.Delete(temporaryArchiveFilePath); } catch { }

        if (!extractionSucceeded || !File.Exists(coreFilePath))
        {
            GD.PrintErr($"The {coreName} core did not extract to {coreFilePath}.");
            appInstance?.downloadManager?.CompleteExternalTransfer(coreTransfer, false);
            return false;
        }

        appInstance?.downloadManager?.CompleteExternalTransfer(coreTransfer, true);

        GD.Print($"Installed the {coreName} core for {emulatorName}.");
        return true;
    }

    private static string ApplyVersionPlaceholder(string templateUrl, string versionLabel)
    {
        return string.IsNullOrEmpty(templateUrl) || string.IsNullOrEmpty(versionLabel)
            ? templateUrl
            : templateUrl.Replace("{version}", versionLabel);
    }

    private static string FindExtractedFolder(string stagingDirectory, string extractFolderRegex)
    {
        var extractFolderPattern = new Regex("^" + extractFolderRegex.Replace("*", ".*") + "$", RegexOptions.IgnoreCase);

        return Directory.GetDirectories(stagingDirectory).FirstOrDefault(directoryPath => extractFolderPattern.IsMatch(new DirectoryInfo(directoryPath).Name));
    }

    private static async Task<bool> ExtractExtraDownload(AppInstance appInstance, string archiveFilePath, string destinationDirectory, string extractFolderRegex)
    {
        if (string.IsNullOrEmpty(extractFolderRegex))
        {
            return await ExtractArchiveAsync(appInstance, archiveFilePath, destinationDirectory);
        }

        string stagingDirectory = Path.Combine(appInstance.configManager.DownloadsPath, "extradownloadstaging");

        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, true);
        }

        Directory.CreateDirectory(stagingDirectory);

        try
        {
            if (!await ExtractArchiveAsync(appInstance, archiveFilePath, stagingDirectory))
            {
                return false;
            }

            string contentSourceDirectory = FindExtractedFolder(stagingDirectory, extractFolderRegex) ?? stagingDirectory;
            CopyDirectoryRecursively(contentSourceDirectory, destinationDirectory);
            return true;
        }

        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, true);
            }
        }
    }

    private static async Task<bool> DownloadExtraFiles(AppInstance appInstance, string emulatorName, InstallRecipe installRecipe, string emulatorTargetDirectory, string versionLabel, DownloadManager.ExternalTransfer installTransfer)
    {
        if (installRecipe.ExtraDownloads == null || installRecipe.ExtraDownloads.Count == 0)
        {
            return true;
        }

        for (int extraDownloadIndex = 0; extraDownloadIndex < installRecipe.ExtraDownloads.Count; extraDownloadIndex++)
        {
            var extraDownload = installRecipe.ExtraDownloads[extraDownloadIndex];

            if (string.IsNullOrWhiteSpace(extraDownload.Url))
            {
                continue;
            }

            string resolvedDownloadUrl = ApplyVersionPlaceholder(extraDownload.Url, versionLabel);

            string destinationDirectory = string.IsNullOrWhiteSpace(extraDownload.Destination)
                ? emulatorTargetDirectory
                : Path.Combine(emulatorTargetDirectory, extraDownload.Destination.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(destinationDirectory);

            string downloadedFileName = resolvedDownloadUrl.Split('/').LastOrDefault();
            string temporaryFilePath = Path.Combine(appInstance.configManager.DownloadsPath, $"{emulatorName}extra{extraDownloadIndex}.archive");

            ReportStage(appInstance, installTransfer, null);

            if (!await DownloadIntoTransferAsync(appInstance, resolvedDownloadUrl, temporaryFilePath, installTransfer))
            {
                GD.PrintErr($"Failed to download {resolvedDownloadUrl} for {emulatorName}.");
                return false;
            }

            if (extraDownload.Extract)
            {
                ReportStage(appInstance, installTransfer, $"Extracting {downloadedFileName}...");

                bool extractionSucceeded = await ExtractExtraDownload(appInstance, temporaryFilePath, destinationDirectory, extraDownload.ExtractFolderRegex);
                File.Delete(temporaryFilePath);

                if (!extractionSucceeded)
                {
                    GD.PrintErr($"Failed to extract {downloadedFileName} for {emulatorName}.");
                    return false;
                }
            }

            else
            {
                string destinationFilePath = Path.Combine(destinationDirectory, downloadedFileName);

                if (File.Exists(destinationFilePath))
                {
                    File.Delete(destinationFilePath);
                }

                File.Move(temporaryFilePath, destinationFilePath);
            }

            GD.Print($"Installed extra download {downloadedFileName} for {emulatorName}.");
        }

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

    private static List<long> SplitVersionSegments(string versionLabel)
    {
        var versionSegments = new List<long>();

        foreach (Match numberMatch in Regex.Matches(versionLabel ?? "", @"\d+"))
        {
            if (long.TryParse(numberMatch.Value, out long segmentValue))
            {
                versionSegments.Add(segmentValue);
            }
        }

        return versionSegments;
    }

    private static int CompareVersionLabelsDescending(string firstVersionLabel, string secondVersionLabel)
    {
        var firstSegments = SplitVersionSegments(firstVersionLabel);
        var secondSegments = SplitVersionSegments(secondVersionLabel);
        int segmentCount = Math.Max(firstSegments.Count, secondSegments.Count);

        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            long firstSegment = segmentIndex < firstSegments.Count ? firstSegments[segmentIndex] : 0;
            long secondSegment = segmentIndex < secondSegments.Count ? secondSegments[segmentIndex] : 0;

            if (firstSegment != secondSegment)
            {
                return secondSegment.CompareTo(firstSegment);
            }
        }

        return string.Compare(secondVersionLabel, firstVersionLabel, StringComparison.OrdinalIgnoreCase);
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

                releaseOptions.Sort((firstOption, secondOption) => CompareVersionLabelsDescending(firstOption.VersionLabel, secondOption.VersionLabel));
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

    public static async Task<bool> DownloadFileAsync(AppInstance appInstance, string downloadUrl, string destinationFilePath, string displayName)
    {
        var externalTransfer = appInstance?.downloadManager?.BeginExternalTransfer(displayName);
        bool downloadSucceeded = await DownloadIntoTransferAsync(appInstance, downloadUrl, destinationFilePath, externalTransfer);
        appInstance?.downloadManager?.CompleteExternalTransfer(externalTransfer, downloadSucceeded);

        return downloadSucceeded;
    }

    private static async Task<bool> DownloadIntoTransferAsync(AppInstance appInstance, string downloadUrl, string destinationFilePath, DownloadManager.ExternalTransfer externalTransfer)
    {
        appInstance?.downloadManager?.ReportExternalTransferProgress(externalTransfer, 0, 0);

        try
        {
            var httpResponse = await sharedHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            httpResponse.EnsureSuccessStatusCode();

            long totalBytes = httpResponse.Content.Headers.ContentLength ?? 0;

            using (var contentStream = await httpResponse.Content.ReadAsStreamAsync())
            using (var destinationFileStream = new FileStream(destinationFilePath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, TransferBufferSizeBytes, true))
            {
                var transferBuffer = new byte[TransferBufferSizeBytes];
                long bytesTransferred = 0;

                while (true)
                {
                    int bytesRead = await contentStream.ReadAsync(transferBuffer, 0, transferBuffer.Length);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await destinationFileStream.WriteAsync(transferBuffer, 0, bytesRead);
                    bytesTransferred += bytesRead;
                    appInstance?.downloadManager?.ReportExternalTransferProgress(externalTransfer, bytesTransferred, totalBytes);
                }
            }

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
                CopyDirectoryRecursively(defaultConfigDirectory, emulatorTargetDirectory, OperatingSystemConfigFolderNames);

                string operatingSystemConfigDirectory = Path.Combine(defaultConfigDirectory, OS.GetName().ToLower());

                if (Directory.Exists(operatingSystemConfigDirectory))
                {
                    CopyDirectoryRecursively(operatingSystemConfigDirectory, emulatorTargetDirectory);
                }

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

            if (SaveStore.IsDirectoryLink(subdirectoryPath))
            {
                try { Directory.Delete(subdirectoryPath); } catch (Exception exception) { GD.PrintErr($"Failed to delete link {subdirectoryPath}: {exception.Message}"); }
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

            try { SaveStore.DeleteDirectoryTreeWithoutFollowingLinks(subdirectoryPath); } catch (Exception exception) { GD.PrintErr($"Failed to delete {subdirectoryPath}: {exception.Message}"); }
        }
    }

    private static void CopyDirectoryRecursively(string sourceDirectory, string targetDirectory, IEnumerable<string> excludedDirectoryNames = null)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var excludedNames = excludedDirectoryNames == null
            ? null
            : new HashSet<string>(excludedDirectoryNames, StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in Directory.GetFiles(sourceDirectory))
        {
            File.Copy(filePath, Path.Combine(targetDirectory, Path.GetFileName(filePath)), true);
        }

        foreach (string subdirectoryPath in Directory.GetDirectories(sourceDirectory))
        {
            if (SaveStore.IsDirectoryLink(subdirectoryPath))
            {
                continue;
            }

            string subdirectoryName = new DirectoryInfo(subdirectoryPath).Name;

            if (excludedNames != null && excludedNames.Contains(subdirectoryName))
            {
                continue;
            }

            CopyDirectoryRecursively(subdirectoryPath, Path.Combine(targetDirectory, subdirectoryName));
        }
    }

    private static string ResolveInstalledExecutablePath(EmulatorMeta emulatorMetadata, string currentOperatingSystem, string emulatorTargetDirectory)
    {
        if (emulatorMetadata.ExecutableName != null
            && emulatorMetadata.ExecutableName.TryGetValue(currentOperatingSystem, out string declaredExecutableName)
            && !string.IsNullOrEmpty(declaredExecutableName))
        {
            return Path.Combine(emulatorTargetDirectory, declaredExecutableName);
        }

        if (emulatorMetadata.ExecutableRegex != null
            && emulatorMetadata.ExecutableRegex.TryGetValue(currentOperatingSystem, out string executableRegex)
            && Directory.Exists(emulatorTargetDirectory))
        {
            var executablePattern = new Regex(executableRegex, RegexOptions.IgnoreCase);

            return Directory.GetFiles(emulatorTargetDirectory)
                .FirstOrDefault(candidatePath => executablePattern.IsMatch(Path.GetFileName(candidatePath)));
        }

        return null;
    }

    private static void EnsureExecutableBit(string executableFilePath)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrEmpty(executableFilePath) || !File.Exists(executableFilePath))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(executableFilePath, File.GetUnixFileMode(executableFilePath)
                | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Could not mark {executableFilePath} executable: {exception.Message}");
        }
    }
}
