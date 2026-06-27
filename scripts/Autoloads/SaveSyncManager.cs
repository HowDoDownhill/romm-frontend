using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DirAccess = Godot.DirAccess;

public partial class SaveSyncManager : Node
{
    private AppInstance appInstance;
    private Dictionary<string, DateTime> preLaunchSnapshot;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.saveSyncManager = this;
    }

    public async Task SyncBeforeLaunch(Game game)
    {
        if (appInstance.rommApi == null) return;
        string[] savesDirs = GetSavesDirsForGame(game);
        if (savesDirs.Length == 0) return;
        
        foreach (var savesDir in savesDirs)
        {
            if (!DirAccess.DirExistsAbsolute(savesDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(savesDir);
            }
        }

        // 1. Identify local saves for this game by checking what the server knows about it
        var clientSaves = new List<ClientSaveState>();
        var serverSavesJson = await appInstance.rommApi.GetSavesAsync(game.Id);
        
        if (serverSavesJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var serverSave in serverSavesJson.EnumerateArray())
            {
                if (serverSave.TryGetProperty("file_name", out var fileNameProp) &&
                    serverSave.TryGetProperty("id", out var serverSaveIdProp))
                {
                    string fileName = fileNameProp.GetString();
                    int serverSaveId = serverSaveIdProp.GetInt32();
                    
                    bool fileExistsLocally = false;
                    long fileSizeBytes = 0;
                    DateTime updatedAt = DateTime.MinValue;
                    
                    foreach (var savesDir in savesDirs)
                    {
                        string checkPath = Path.Combine(savesDir, fileName);
                        if (System.IO.File.Exists(checkPath))
                        {
                            fileExistsLocally = true;
                            var fileInfo = new FileInfo(checkPath);
                            fileSizeBytes = fileInfo.Length;
                            updatedAt = fileInfo.LastWriteTimeUtc;
                            break;
                        }
                    }

                    if (!fileExistsLocally)
                    {
                        GD.Print($"Downloading missing save for {game.Name}: {fileName}");
                        string downloadUrl = appInstance.rommApi.GetSaveDownloadUrl(serverSaveId, fileName);
                        
                        string tempDownloadPath = Path.Combine(savesDirs[0], "temp_" + fileName);
                        await appInstance.rommApi.DownloadAssetAsync(downloadUrl, tempDownloadPath);
                        
                        // Copy to all watched directories
                        foreach (var savesDir in savesDirs)
                        {
                            string targetPath = Path.Combine(savesDir, fileName);
                            System.IO.File.Copy(tempDownloadPath, targetPath, true);
                        }
                        System.IO.File.Delete(tempDownloadPath);
                        
                        // Register as downloaded client save
                        var fileInfo = new FileInfo(Path.Combine(savesDirs[0], fileName));
                        clientSaves.Add(new ClientSaveState
                        {
                            RomId = game.Id,
                            FileName = fileName,
                            UpdatedAt = fileInfo.LastWriteTimeUtc,
                            FileSizeBytes = fileInfo.Length
                        });
                    }
                    else
                    {
                        clientSaves.Add(new ClientSaveState
                        {
                            RomId = game.Id,
                            FileName = fileName,
                            UpdatedAt = updatedAt,
                            FileSizeBytes = fileSizeBytes
                        });
                    }
                }
            }
        }

        string deviceId = await appInstance.rommApi.GetOrCreateDeviceAsync();
        if (string.IsNullOrEmpty(deviceId))
        {
            GD.PrintErr("Failed to get or create device ID. Aborting sync.");
            return;
        }

        // 2. Negotiate Sync
        var payload = new SyncNegotiatePayload
        {
            DeviceId = deviceId,
            Saves = clientSaves
        };

        var response = await appInstance.rommApi.NegotiateSyncAsync(payload);
        if (response != null && response.Operations != null)
        {
            var gameOps = response.Operations.Where(o => o.RomId == game.Id).ToList();
            
            foreach (var op in gameOps)
            {
                if (op.Action == "download" && op.ServerSaveId.HasValue)
                {
                    string downloadUrl = appInstance.rommApi.GetSaveDownloadUrl(op.ServerSaveId.Value, op.FileName);
                    string tempDownloadPath = Path.Combine(savesDirs[0], "temp_op_" + op.FileName);
                    
                    if (op.FileName.EndsWith(".folder.zip"))
                    {
                        GD.Print($"Downloading zipped folder save for {game.Name}: {op.FileName}");
                        await appInstance.rommApi.DownloadAssetAsync(downloadUrl, tempDownloadPath);
                        
                        string folderName = op.FileName.Substring(0, op.FileName.Length - ".folder.zip".Length);
                        foreach (var savesDir in savesDirs)
                        {
                            string extractPath = Path.Combine(savesDir, folderName);
                            ZipFile.ExtractToDirectory(tempDownloadPath, extractPath, true);
                        }
                        System.IO.File.Delete(tempDownloadPath);
                    }
                    else
                    {
                        GD.Print($"Downloading save for {game.Name}: {op.FileName}");
                        await appInstance.rommApi.DownloadAssetAsync(downloadUrl, tempDownloadPath);
                        foreach (var savesDir in savesDirs)
                        {
                            string destPath = Path.Combine(savesDir, op.FileName);
                            System.IO.File.Copy(tempDownloadPath, destPath, true);
                        }
                        System.IO.File.Delete(tempDownloadPath);
                    }
                }
            }

            // Immediately complete the pre-launch sync session
            var preLaunchCompletePayload = new SyncCompletePayload
            {
                OperationsCompleted = gameOps.Count,
                OperationsFailed = 0,
                PlaySessions = new List<SyncPlaySessionEntry>()
            };
            await appInstance.rommApi.CompleteSyncAsync(preLaunchCompletePayload, response.SessionId);
        }

        // 3. Snapshot directories
        preLaunchSnapshot = new Dictionary<string, DateTime>();
        foreach (var savesDir in savesDirs)
        {
            string[] files = System.IO.Directory.GetFiles(savesDir, "*", System.IO.SearchOption.AllDirectories);
            if (files != null)
            {
                foreach (string fullPath in files)
                {
                    preLaunchSnapshot[fullPath] = System.IO.File.GetLastWriteTimeUtc(fullPath);
                }
            }
        }
    }

    public async Task SyncAfterExit(Game game, DateTime sessionStart, DateTime sessionEnd)
    {
        if (appInstance.rommApi == null) return;
        string[] savesDirs = GetSavesDirsForGame(game);
        if (savesDirs.Length == 0) return;

        int opsCompleted = 0;

        // 1. Find modified or new files per savesDir
        var modifiedTopLevelItemsPerDir = new Dictionary<string, HashSet<string>>();
        foreach (var savesDir in savesDirs)
        {
            if (!DirAccess.DirExistsAbsolute(savesDir)) continue;
            modifiedTopLevelItemsPerDir[savesDir] = new HashSet<string>();

            string[] files = System.IO.Directory.GetFiles(savesDir, "*", System.IO.SearchOption.AllDirectories);
            if (files != null)
            {
                foreach (string fullPath in files)
                {
                    DateTime currentWriteTime = System.IO.File.GetLastWriteTimeUtc(fullPath);

                    bool isModifiedOrNew = true;
                    if (preLaunchSnapshot != null && preLaunchSnapshot.TryGetValue(fullPath, out DateTime previousWriteTime))
                    {
                        if (currentWriteTime <= previousWriteTime)
                        {
                            isModifiedOrNew = false;
                        }
                    }

                    if (isModifiedOrNew)
                    {
                        string relativePath = Path.GetRelativePath(savesDir, fullPath);
                        string[] parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                        if (parts.Length > 0)
                        {
                            modifiedTopLevelItemsPerDir[savesDir].Add(parts[0]);
                        }
                    }
                }
            }
        }

        foreach (var kvp in modifiedTopLevelItemsPerDir)
        {
            string savesDir = kvp.Key;
            foreach (string topLevelItem in kvp.Value)
            {
                string itemFullPath = Path.Combine(savesDir, topLevelItem);
                if (System.IO.Directory.Exists(itemFullPath))
                {
                    string zipFileName = topLevelItem + ".folder.zip";
                    string tempZipPath = Path.Combine(savesDir, zipFileName);
                    if (System.IO.File.Exists(tempZipPath)) System.IO.File.Delete(tempZipPath);
                    
                    ZipFile.CreateFromDirectory(itemFullPath, tempZipPath);
                    
                    GD.Print($"Uploading zipped folder save for {game.Name}: {zipFileName}");
                    bool success = await appInstance.rommApi.UploadSaveAsync(game.Id, tempZipPath);
                    if (success) opsCompleted++;
                    
                    System.IO.File.Delete(tempZipPath);
                }
                else if (System.IO.File.Exists(itemFullPath))
                {
                    GD.Print($"Uploading modified save for {game.Name}: {topLevelItem}");
                    bool success = await appInstance.rommApi.UploadSaveAsync(game.Id, itemFullPath);
                    if (success) opsCompleted++;
                }
            }
        }

        // 2. Negotiate post-exit session
        string deviceId = await appInstance.rommApi.GetOrCreateDeviceAsync();
        var postExitPayload = new SyncNegotiatePayload
        {
            DeviceId = deviceId,
            Saves = new List<ClientSaveState>()
        };
        var response = await appInstance.rommApi.NegotiateSyncAsync(postExitPayload);

        // 3. Complete Sync with play session time
        if (response != null)
        {
            var playSession = new SyncPlaySessionEntry
            {
                RomId = game.Id,
                StartTime = sessionStart,
                EndTime = sessionEnd,
                DurationMs = (long)(sessionEnd - sessionStart).TotalMilliseconds
            };

            var completePayload = new SyncCompletePayload
            {
                OperationsCompleted = opsCompleted,
                OperationsFailed = 0,
                PlaySessions = new List<SyncPlaySessionEntry> { playSession }
            };

            await appInstance.rommApi.CompleteSyncAsync(completePayload, response.SessionId);
        }

        preLaunchSnapshot = null;
    }

    private string[] GetSavesDirsForGame(Game game)
    {
        string platformSlug = game.System.Slug;
        string defaultSavesDir = Path.Combine(appInstance.configManager.SavesPath, platformSlug);
        var savesDirs = new List<string> { defaultSavesDir };

        string mappedEmulatorName = appInstance.emulatorManager.GetMappedEmulator(platformSlug);
        if (!string.IsNullOrEmpty(mappedEmulatorName))
        {
            var emulatorMetadata = appInstance.emulatorManager.LoadEmulatorMetadataFromDisk(mappedEmulatorName);
            if (emulatorMetadata != null && emulatorMetadata.RelativeSavePath != null)
            {
                if (emulatorMetadata.RelativeSavePath.TryGetValue(platformSlug, out JsonElement relativePathElement))
                {
                    string currentOperatingSystem = OS.GetName().ToLower();
                    if (emulatorMetadata.EmulatorDirName != null && emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
                    {
                        string emulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
                        
                        if (relativePathElement.ValueKind == JsonValueKind.String)
                        {
                            savesDirs.Clear();
                            savesDirs.Add(Path.Combine(emulatorInstallDirectory, relativePathElement.GetString()));
                        }
                        else if (relativePathElement.ValueKind == JsonValueKind.Array)
                        {
                            savesDirs.Clear();
                            foreach (var pathElement in relativePathElement.EnumerateArray())
                            {
                                savesDirs.Add(Path.Combine(emulatorInstallDirectory, pathElement.GetString()));
                            }
                        }
                    }
                }
            }
        }
        
        return savesDirs.ToArray();
    }
}
