using Godot;

public partial class ConfigManager : Node
{
    private const string ConfigFilePath = "user://config.cfg";
    private ConfigFile _config;

    public string LocalRomsPath { get; private set; }

    public override void _Ready()
    {
        _config = new ConfigFile();
        LoadConfig();
    }

    private void LoadConfig()
    {
        Error err = _config.Load(ConfigFilePath);
        
        if (err != Error.Ok)
        {
            SetDefaultConfig();
            SaveConfig();
            return;
        }

        LocalRomsPath = (string)_config.GetValue("Paths", "LocalRomsPath", "user://roms/");
    }

    private void SetDefaultConfig()
    {
        LocalRomsPath = "user://roms/";
        _config.SetValue("Paths", "LocalRomsPath", LocalRomsPath);
    }

    public void SaveConfig()
    {
        _config.SetValue("Paths", "LocalRomsPath", LocalRomsPath);
        _config.Save(ConfigFilePath);
    }
    
    public void UpdateLocalRomsPath(string newPath)
    {
        LocalRomsPath = newPath;
        SaveConfig();
    }
}