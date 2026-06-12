using Godot;

public partial class ConfigManager : Node
{
    private const string ConfigFilePath = "user://config.cfg";
    private ConfigFile _config;

    public string LocalRomsPath { get; private set; }
    public string DownloadsPath { get; private set; }
    public string RomMHost { get; private set; }
    public string RomMUsername { get; private set; }
    public string RomMPassword { get; private set; }
    public string RomMApiKey { get; private set; }
    public int LastUsedBackend { get; private set; }

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
        DownloadsPath = (string)_config.GetValue("Paths", "DownloadsPath", "user://downloads/");
        RomMHost = (string)_config.GetValue("RomM", "Host", "");
        RomMUsername = (string)_config.GetValue("RomM", "Username", "");
        RomMPassword = (string)_config.GetValue("RomM", "Password", "");
        RomMApiKey = (string)_config.GetValue("RomM", "ApiKey", "");
        LastUsedBackend = (int)_config.GetValue("Settings", "LastUsedBackend", 0);
    }

    private void SetDefaultConfig()
    {
        LocalRomsPath = "user://roms/";
        DownloadsPath = "user://downloads/";
        RomMHost = "";
        RomMUsername = "";
        RomMPassword = "";
        RomMApiKey = "";
        LastUsedBackend = 0;
        
        _config.SetValue("Paths", "LocalRomsPath", LocalRomsPath);
        _config.SetValue("Paths", "DownloadsPath", DownloadsPath);
        _config.SetValue("RomM", "Host", RomMHost);
        _config.SetValue("RomM", "Username", RomMUsername);
        _config.SetValue("RomM", "Password", RomMPassword);
        _config.SetValue("RomM", "ApiKey", RomMApiKey);
        _config.SetValue("Settings", "LastUsedBackend", LastUsedBackend);
    }

    public void SaveConfig()
    {
        _config.SetValue("Paths", "LocalRomsPath", LocalRomsPath);
        _config.SetValue("Paths", "DownloadsPath", DownloadsPath);
        _config.SetValue("RomM", "Host", RomMHost);
        _config.SetValue("RomM", "Username", RomMUsername);
        _config.SetValue("RomM", "Password", RomMPassword);
        _config.SetValue("RomM", "ApiKey", RomMApiKey);
        _config.SetValue("Settings", "LastUsedBackend", LastUsedBackend);
        _config.Save(ConfigFilePath);
    }
    
    public void UpdateLocalRomsPath(string newPath)
    {
        LocalRomsPath = newPath;
        SaveConfig();
    }

    public void UpdateDownloadsPath(string newPath)
    {
        DownloadsPath = newPath;
        SaveConfig();
    }

    public void SaveRomMCredentials(string host, string username, string password, string apiKey)
    {
        RomMHost = host;
        RomMUsername = username;
        RomMPassword = password;
        RomMApiKey = apiKey;
        SaveConfig();
    }

    public void SaveLastUsedBackend(int backendIndex)
    {
        LastUsedBackend = backendIndex;
        SaveConfig();
    }
}
