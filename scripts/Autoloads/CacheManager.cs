using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FileAccess = Godot.FileAccess;

public partial class CacheManager : Node
{
    private string systemsCacheFilePath;
    private string gamesCacheFilePath;

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.cacheManager = this;

        InitializeCacheFilePaths();
    }

    public void InitializeCacheFilePaths()
    {
        if (OS.HasFeature("editor"))
        {
            systemsCacheFilePath = ProjectSettings.GlobalizePath("res://systems.cache");
            gamesCacheFilePath = ProjectSettings.GlobalizePath("res://games.cache");
        }
        else
        {
            systemsCacheFilePath = OS.GetExecutablePath().GetBaseDir() + "/systems.cache";
            gamesCacheFilePath = OS.GetExecutablePath().GetBaseDir() + "/games.cache";
        }
    }

    public void SaveCache(List<GameSystem> gameSystems, Dictionary<int, List<Game>> gameCacheBySystemId)
    {
        WriteJsonToFile(systemsCacheFilePath, gameSystems);
        WriteJsonToFile(gamesCacheFilePath, gameCacheBySystemId);
    }

    public void RebuildGameCache()
    {
        File.Delete(systemsCacheFilePath);
        File.Delete(gamesCacheFilePath);

        GetTree().ChangeSceneToFile("res://scenes/login/loading_screen.tscn");
    }

    public (List<GameSystem> systems, Dictionary<int, List<Game>> games) LoadCache()
    {
        var cachedSystems = ReadJsonFromFile<List<GameSystem>>(systemsCacheFilePath);
        var cachedGames = ReadJsonFromFile<Dictionary<int, List<Game>>>(gamesCacheFilePath);

        if (cachedSystems != null && cachedGames != null)
        {
            return (cachedSystems, cachedGames);
        }

        return (null, null);
    }

    private void WriteJsonToFile<T>(string filePath, T dataToSerialize)
    {
        using var fileHandle = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (fileHandle == null)
        {
            GD.PrintErr($"Failed to open file for writing: {filePath}");
            return;
        }

        string serializedJsonContent = JsonSerializer.Serialize(dataToSerialize);
        fileHandle.StoreString(serializedJsonContent);
    }

    private T ReadJsonFromFile<T>(string filePath) where T : class
    {
        if (!FileAccess.FileExists(filePath))
        {
            return null;
        }

        using var fileHandle = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (fileHandle == null)
        {
            GD.PrintErr($"Failed to open file for reading: {filePath}");
            return null;
        }

        string fileJsonContent = fileHandle.GetAsText();
        return JsonSerializer.Deserialize<T>(fileJsonContent);
    }
}
