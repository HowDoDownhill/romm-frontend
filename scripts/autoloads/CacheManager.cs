using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using FileAccess = Godot.FileAccess;

public partial class CacheManager : Node
{
    private string systemsCacheFilePath;
    private string gamesCacheFilePath;
    private string romHashCacheFilePath;

    private Dictionary<string, RomHashEntry> romHashesByPath;

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.cacheManager = this;

        InitializeCacheFilePaths();
    }

    public void InitializeCacheFilePaths()
    {
        string applicationRootDirectory = GetNode<ConfigManager>("/root/ConfigManager").ApplicationRootDirectory;
        systemsCacheFilePath = applicationRootDirectory + "/systems.cache";
        gamesCacheFilePath = applicationRootDirectory + "/games.cache";
        romHashCacheFilePath = applicationRootDirectory + "/romhashes.cache";
    }

    public string GetStoredRomHash(string romFilePath)
    {
        var currentStamp = ResolveRomFileStamp(romFilePath);

        if (currentStamp == null)
        {
            return null;
        }

        EnsureRomHashesLoaded();

        if (!romHashesByPath.TryGetValue(romFilePath, out RomHashEntry storedEntry))
        {
            return null;
        }

        bool stampMatches = storedEntry.SizeBytes == currentStamp.SizeBytes
            && storedEntry.ModifiedUnix == currentStamp.ModifiedUnix;

        return stampMatches ? storedEntry.Md5 : null;
    }

    public void StoreRomHash(string romFilePath, string romHash)
    {
        var currentStamp = ResolveRomFileStamp(romFilePath);

        if (currentStamp == null || string.IsNullOrEmpty(romHash))
        {
            return;
        }

        EnsureRomHashesLoaded();

        currentStamp.Md5 = romHash;
        romHashesByPath[romFilePath] = currentStamp;

        WriteJsonToFile(romHashCacheFilePath, romHashesByPath, RommJsonContext.Default.DictionaryStringRomHashEntry);
    }

    private static RomHashEntry ResolveRomFileStamp(string romFilePath)
    {
        if (string.IsNullOrEmpty(romFilePath) || !File.Exists(romFilePath))
        {
            return null;
        }

        var romFileInfo = new FileInfo(romFilePath);

        return new RomHashEntry
        {
            SizeBytes = romFileInfo.Length,
            ModifiedUnix = new System.DateTimeOffset(romFileInfo.LastWriteTimeUtc).ToUnixTimeSeconds()
        };
    }

    private void EnsureRomHashesLoaded()
    {
        romHashesByPath ??= ReadJsonFromFile(romHashCacheFilePath, RommJsonContext.Default.DictionaryStringRomHashEntry)
            ?? new Dictionary<string, RomHashEntry>();
    }

    public void SaveCache(List<GameSystem> gameSystems, Dictionary<int, List<Game>> gameCacheBySystemId)
    {
        WriteJsonToFile(systemsCacheFilePath, gameSystems, CacheJsonContext.Default.ListGameSystem);
        WriteJsonToFile(gamesCacheFilePath, gameCacheBySystemId, CacheJsonContext.Default.DictionaryInt32ListGame);
    }

    public void RebuildGameCache()
    {
        File.Delete(systemsCacheFilePath);
        File.Delete(gamesCacheFilePath);

        GetTree().ChangeSceneToFile("res://scenes/login/loading_screen.tscn");
    }

    public (List<GameSystem> systems, Dictionary<int, List<Game>> games) LoadCache()
    {
        var cachedSystems = ReadJsonFromFile(systemsCacheFilePath, CacheJsonContext.Default.ListGameSystem);
        var cachedGames = ReadJsonFromFile(gamesCacheFilePath, CacheJsonContext.Default.DictionaryInt32ListGame);

        if (cachedSystems != null && cachedGames != null)
        {


            bool isCacheValid = true;
            bool foundAnyGameWithFiles = false;

            foreach (var gamesList in cachedGames.Values)
            {
                foreach(var game in gamesList)
                {
                    if (game.Files != null && game.Files.Count > 0)
                    {
                        foundAnyGameWithFiles = true;
                        break;
                    }
                }


                if (foundAnyGameWithFiles)
                {
                    break;
                }
            }

            if (cachedGames.Values.Any(list => list.Count > 0) && !foundAnyGameWithFiles)
            {
                isCacheValid = false;
            }

            if (isCacheValid)
            {
                if (IsIndentedJsonFile(gamesCacheFilePath))
                {
                    long indentedBytes = (long)FileAccess.GetFileAsBytes(gamesCacheFilePath).Length;
                    SaveCache(cachedSystems, cachedGames);
                    GD.Print($"Compacted the game cache from {indentedBytes / 1024} KB to {(long)FileAccess.GetFileAsBytes(gamesCacheFilePath).Length / 1024} KB.");
                }

                return (cachedSystems, cachedGames);
            }

            else 
            {
                GD.Print("Cache invalid (missing Files). Rebuilding cache...");
            }
        }

        return (null, null);
    }

    private void WriteJsonToFile<T>(string filePath, T dataToSerialize, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        using var fileHandle = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);

        if (fileHandle == null)
        {
            GD.PrintErr($"Failed to open file for writing: {filePath}");
            return;
        }

        string serializedJsonContent = JsonSerializer.Serialize(dataToSerialize, typeInfo);
        fileHandle.StoreString(serializedJsonContent);
    }

    private static bool IsIndentedJsonFile(string filePath)
    {
        using var fileHandle = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);

        if (fileHandle == null || fileHandle.GetLength() < 2)
        {
            return false;
        }

        byte[] leadingBytes = fileHandle.GetBuffer(System.Math.Min(16, (long)fileHandle.GetLength()));
        return System.Array.IndexOf(leadingBytes, (byte)'\n') >= 0;
    }

    private T ReadJsonFromFile<T>(string filePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) where T : class
    {
        if (!FileAccess.FileExists(filePath))
        {
            return null;
        }

        byte[] fileJsonBytes = FileAccess.GetFileAsBytes(filePath);

        if (fileJsonBytes == null || fileJsonBytes.Length == 0)
        {
            GD.PrintErr($"Failed to read file: {filePath}");
            return null;
        }

        return JsonSerializer.Deserialize(fileJsonBytes, typeInfo);
    }
}
