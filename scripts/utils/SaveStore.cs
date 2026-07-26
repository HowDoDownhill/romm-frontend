using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

public static class SaveStore
{
    private const string SystemSlugMacro = "{system_slug}";

    private const string WindowsNonInterpretedPathPrefix = @"\??\";

    public static void LinkEmulatorSaveDirectories(AppInstance appInstance, string emulatorSlug, EmulatorMeta emulatorMetadata, string currentOperatingSystem, string systemSlug)
    {
        if (appInstance?.configManager == null || string.IsNullOrEmpty(emulatorSlug) || emulatorMetadata == null)
        {
            return;
        }

        if (emulatorMetadata.EmulatorDirName == null || !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
        {
            return;
        }

        string emulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
        string emulatorStoreDirectory = Path.Combine(appInstance.configManager.SavesPath, emulatorSlug);

        foreach (string relativeSavePath in emulatorMetadata.GetSaveRelativePaths())
        {
            string resolvedRelativePath = ResolveSaveRelativePath(relativeSavePath, systemSlug);

            if (resolvedRelativePath == null)
            {
                continue;
            }

            string savesDirectoryInsideInstall = Path.GetFullPath(Path.Combine(emulatorInstallDirectory, resolvedRelativePath));
            string savesDirectoryInsideStore = Path.GetFullPath(Path.Combine(emulatorStoreDirectory, resolvedRelativePath));

            ReplaceLinkedAncestorsWithRealDirectories(emulatorInstallDirectory, savesDirectoryInsideInstall);
            EnsureDirectoryLinkedToStore(savesDirectoryInsideInstall, savesDirectoryInsideStore);
        }
    }

    public static bool IsDirectoryLink(string directoryPath)
    {
        try
        {
            return new DirectoryInfo(directoryPath).LinkTarget != null;
        }

        catch
        {
            return false;
        }
    }

    public static void DeleteDirectoryTreeWithoutFollowingLinks(string directoryPath)
    {
        if (IsDirectoryLink(directoryPath))
        {
            Directory.Delete(directoryPath);
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (string subdirectoryPath in Directory.GetDirectories(directoryPath))
        {
            DeleteDirectoryTreeWithoutFollowingLinks(subdirectoryPath);
        }

        Directory.Delete(directoryPath, true);
    }

    private static string ResolveSaveRelativePath(string relativeSavePath, string systemSlug)
    {
        if (string.IsNullOrWhiteSpace(relativeSavePath))
        {
            return null;
        }

        string normalizedRelativePath = relativeSavePath
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);

        if (normalizedRelativePath.Length == 0)
        {
            return null;
        }

        if (!normalizedRelativePath.Contains(SystemSlugMacro))
        {
            return normalizedRelativePath;
        }

        if (string.IsNullOrEmpty(systemSlug))
        {
            GD.Print($"Save store: deferring \"{relativeSavePath}\" until a system slug is known.");
            return null;
        }

        return normalizedRelativePath.Replace(SystemSlugMacro, systemSlug);
    }

    private static void ReplaceLinkedAncestorsWithRealDirectories(string emulatorInstallDirectory, string savesDirectoryInsideInstall)
    {
        string installRootPath = Path.GetFullPath(emulatorInstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var ancestorPaths = new List<string>();
        string ancestorPath = Path.GetDirectoryName(savesDirectoryInsideInstall);

        while (!string.IsNullOrEmpty(ancestorPath) && !PathsAreEqual(ancestorPath, installRootPath)
               && ancestorPath.StartsWith(installRootPath, StringComparison.OrdinalIgnoreCase))
        {
            ancestorPaths.Insert(0, ancestorPath);
            ancestorPath = Path.GetDirectoryName(ancestorPath);
        }

        foreach (string linkedAncestorPath in ancestorPaths)
        {
            if (!IsDirectoryLink(linkedAncestorPath))
            {
                continue;
            }

            try
            {
                Directory.Delete(linkedAncestorPath);
                Directory.CreateDirectory(linkedAncestorPath);
                GD.Print($"Save store: {linkedAncestorPath} was a link covering several save directories. Replaced it with a real directory so each one links separately; the saves stay in the store.");
            }

            catch (Exception exception)
            {
                GD.PrintErr($"Save store: could not replace the link at {linkedAncestorPath}: {exception.Message}");
                return;
            }
        }
    }

    private static bool EnsureDirectoryLinkedToStore(string savesDirectoryInsideInstall, string savesDirectoryInsideStore)
    {
        if (PathsAreEqual(savesDirectoryInsideInstall, savesDirectoryInsideStore))
        {
            return true;
        }

        try
        {
            if (IsDirectoryLink(savesDirectoryInsideInstall))
            {
                if (LinkPointsAt(savesDirectoryInsideInstall, savesDirectoryInsideStore))
                {
                    Directory.CreateDirectory(savesDirectoryInsideStore);
                    return true;
                }

                GD.Print($"Save store: {savesDirectoryInsideInstall} pointed at {new DirectoryInfo(savesDirectoryInsideInstall).LinkTarget} instead of {savesDirectoryInsideStore}. Repointing it; anything at the old target is left where it is.");
                Directory.Delete(savesDirectoryInsideInstall);
            }

            else if (File.Exists(savesDirectoryInsideInstall))
            {
                GD.PrintErr($"Save store: {savesDirectoryInsideInstall} is a file where a save directory was expected. Leaving it alone.");
                return false;
            }

            else if (Directory.Exists(savesDirectoryInsideInstall))
            {
                if (!MoveSaveDirectoryIntoStore(savesDirectoryInsideInstall, savesDirectoryInsideStore))
                {
                    return false;
                }
            }

            Directory.CreateDirectory(savesDirectoryInsideStore);
            Directory.CreateDirectory(Path.GetDirectoryName(savesDirectoryInsideInstall));

            return CreateDirectoryLink(savesDirectoryInsideInstall, savesDirectoryInsideStore);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Save store: could not link {savesDirectoryInsideInstall} to {savesDirectoryInsideStore}: {exception.Message}. Saves stay in the emulator directory.");
            return false;
        }
    }

    private static bool PathsAreEqual(string firstPath, string secondPath)
    {
        var pathComparison = OS.GetName().ToLower() == "windows" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar),
            pathComparison);
    }

    private static bool LinkPointsAt(string linkPath, string expectedTargetPath)
    {
        string linkTarget = new DirectoryInfo(linkPath).LinkTarget;

        if (string.IsNullOrEmpty(linkTarget))
        {
            return false;
        }

        if (linkTarget.StartsWith(WindowsNonInterpretedPathPrefix, StringComparison.Ordinal))
        {
            linkTarget = linkTarget.Substring(WindowsNonInterpretedPathPrefix.Length);
        }

        if (!Path.IsPathRooted(linkTarget))
        {
            linkTarget = Path.Combine(Path.GetDirectoryName(linkPath), linkTarget);
        }

        return PathsAreEqual(linkTarget, expectedTargetPath);
    }

    private static bool MoveSaveDirectoryIntoStore(string savesDirectoryInsideInstall, string savesDirectoryInsideStore)
    {
        if (Directory.Exists(savesDirectoryInsideStore) && Directory.EnumerateFileSystemEntries(savesDirectoryInsideStore).Any())
        {
            GD.PrintErr($"Save store: {savesDirectoryInsideStore} already holds data and {savesDirectoryInsideInstall} holds its own. Merge them by hand, then relaunch. Nothing was moved.");
            return false;
        }

        if (Directory.Exists(savesDirectoryInsideStore))
        {
            Directory.Delete(savesDirectoryInsideStore);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(savesDirectoryInsideStore));

        try
        {
            Directory.Move(savesDirectoryInsideInstall, savesDirectoryInsideStore);
            GD.Print($"Save store: moved {savesDirectoryInsideInstall} into {savesDirectoryInsideStore}.");
            return true;
        }

        catch (IOException)
        {
            return CopySaveDirectoryIntoStoreThenRemoveOriginal(savesDirectoryInsideInstall, savesDirectoryInsideStore);
        }
    }

    private static bool CopySaveDirectoryIntoStoreThenRemoveOriginal(string savesDirectoryInsideInstall, string savesDirectoryInsideStore)
    {
        try
        {
            CopyDirectoryWithoutFollowingLinks(savesDirectoryInsideInstall, savesDirectoryInsideStore);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Save store: could not copy {savesDirectoryInsideInstall} into {savesDirectoryInsideStore}: {exception.Message}. The original saves were left untouched.");
            DiscardPartialStoreDirectory(savesDirectoryInsideStore);
            return false;
        }

        try
        {
            DeleteDirectoryTreeWithoutFollowingLinks(savesDirectoryInsideInstall);
            GD.Print($"Save store: copied {savesDirectoryInsideInstall} into {savesDirectoryInsideStore}.");
            return true;
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Save store: copied {savesDirectoryInsideInstall} into the store but could not remove the original: {exception.Message}. The original saves were left untouched.");
            DiscardPartialStoreDirectory(savesDirectoryInsideStore);
            return false;
        }
    }

    private static void DiscardPartialStoreDirectory(string savesDirectoryInsideStore)
    {
        try
        {
            if (Directory.Exists(savesDirectoryInsideStore))
            {
                DeleteDirectoryTreeWithoutFollowingLinks(savesDirectoryInsideStore);
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Save store: could not clean up the incomplete copy at {savesDirectoryInsideStore}: {exception.Message}");
        }
    }

    private static void CopyDirectoryWithoutFollowingLinks(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory))
        {
            File.Copy(sourceFilePath, Path.Combine(destinationDirectory, Path.GetFileName(sourceFilePath)), true);
        }

        foreach (string sourceSubdirectoryPath in Directory.GetDirectories(sourceDirectory))
        {
            if (IsDirectoryLink(sourceSubdirectoryPath))
            {
                GD.Print($"Save store: skipping {sourceSubdirectoryPath} while copying because it is already a link.");
                continue;
            }

            CopyDirectoryWithoutFollowingLinks(sourceSubdirectoryPath, Path.Combine(destinationDirectory, new DirectoryInfo(sourceSubdirectoryPath).Name));
        }
    }

    private static bool CreateDirectoryLink(string linkPath, string targetPath)
    {
        if (OS.GetName().ToLower() == "windows")
        {
            return CreateWindowsDirectoryJunction(linkPath, targetPath);
        }

        Directory.CreateSymbolicLink(linkPath, targetPath);
        GD.Print($"Save store: linked {linkPath} to {targetPath}.");
        return true;
    }

    private static bool CreateWindowsDirectoryJunction(string linkPath, string targetPath)
    {
        var junctionProcess = new Process();
        junctionProcess.StartInfo.FileName = "cmd.exe";
        junctionProcess.StartInfo.Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"";
        junctionProcess.StartInfo.UseShellExecute = false;
        junctionProcess.StartInfo.CreateNoWindow = true;
        junctionProcess.StartInfo.RedirectStandardOutput = true;
        junctionProcess.StartInfo.RedirectStandardError = true;

        junctionProcess.Start();

        string junctionStandardOutput = junctionProcess.StandardOutput.ReadToEnd();
        string junctionStandardError = junctionProcess.StandardError.ReadToEnd();
        junctionProcess.WaitForExit();

        int junctionExitCode = junctionProcess.ExitCode;
        junctionProcess.Dispose();

        if (junctionExitCode == 0)
        {
            GD.Print($"Save store: linked {linkPath} to {targetPath}.");
            return true;
        }

        string junctionFailureReason = string.IsNullOrWhiteSpace(junctionStandardError) ? junctionStandardOutput.Trim() : junctionStandardError.Trim();
        GD.PrintErr($"Save store: mklink /J failed for {linkPath}: {junctionFailureReason}. Saves stay in the emulator directory.");
        return false;
    }
}
