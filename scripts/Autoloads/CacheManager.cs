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
        WriteJsonToFile(systemsCacheFilePath, gameSystems, RommJsonContext.Default.ListGameSystem);
        WriteJsonToFile(gamesCacheFilePath, gameCacheBySystemId, RommJsonContext.Default.DictionaryInt32ListGame);
    }

    public void RebuildGameCache()
    {
        File.Delete(systemsCacheFilePath);
        File.Delete(gamesCacheFilePath);

        GetTree().ChangeSceneToFile("res://scenes/login/loading_screen.tscn");
    }

    public (List<GameSystem> systems, Dictionary<int, List<Game>> games) LoadCache()
    {
        var cachedSystems = ReadJsonFromFile(systemsCacheFilePath, RommJsonContext.Default.ListGameSystem);
        var cachedGames = ReadJsonFromFile(gamesCacheFilePath, RommJsonContext.Default.DictionaryInt32ListGame);

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

    private T ReadJsonFromFile<T>(string filePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) where T : class
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
        return JsonSerializer.Deserialize(fileJsonContent, typeInfo);
    }
}
