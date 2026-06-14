using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FileAccess = Godot.FileAccess;

public partial class CacheManager : Node
{
    private string SystemsCachePath;
    private string GamesCachePath;

    private AppInstance appInstance;


    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.cacheManager = this; 
        
        SetCacheLocations();
    }

    public void SetCacheLocations()
    {
        if(OS.HasFeature("editor"))
        {
            SystemsCachePath = ProjectSettings.GlobalizePath("res://systems.cache");
            GamesCachePath = ProjectSettings.GlobalizePath("res://games.cache");
        }

        else
        {
            SystemsCachePath = OS.GetExecutablePath().GetBaseDir() + "/systems.cache";
            GamesCachePath = OS.GetExecutablePath().GetBaseDir() + "/games.cache";
        }
    }
    public void SaveCache(List<GameSystem> systems, Dictionary<int, List<Game>> gameCache)
    {
        SaveJson(SystemsCachePath, systems);
        SaveJson(GamesCachePath, gameCache);
        GD.Print("Saved systems and games to cache.");
    }

    public void rebuildGameCache()
    {
        File.Delete(SystemsCachePath);
        File.Delete(GamesCachePath);
        
        GetTree().ChangeSceneToFile("res://scenes/login/loading_screen.tscn");
    }

    public (List<GameSystem> systems, Dictionary<int, List<Game>> games) LoadCache()
    {
        var systems = LoadJson<List<GameSystem>>(SystemsCachePath);
        var games = LoadJson<Dictionary<int, List<Game>>>(GamesCachePath);

        if (systems != null && games != null)
        {
            GD.Print("Loaded systems and games from cache.");
            return (systems, games);
        }

        return (null, null);
    }

    private void SaveJson<T>(string path, T data)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"Failed to open file for writing: {path}");
            return;
        }
        
        string jsonString = JsonSerializer.Serialize(data);
        file.StoreString(jsonString);
    }

    private T LoadJson<T>(string path) where T : class
    {
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"Failed to open file for reading: {path}");
            return null;
        }

        string jsonString = file.GetAsText();
        return JsonSerializer.Deserialize<T>(jsonString);
    }
}
