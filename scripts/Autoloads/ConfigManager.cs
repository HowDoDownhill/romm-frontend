using Godot;

public partial class ConfigManager : Node
{

    public string rootDir;
    private string configDir;
    private ConfigFile config;
    
    
    public string RomsPath { get; private set; }
    public string BiosPath { get; private set; }
    public string EmulatorsPath { get; private set; }
    public string DownloadsPath { get; private set; }
    public string InstallScriptsPath { get; private set; }
    public string ToolsPath { get; private set; }
    public string AssetsPath { get; private set; }
    public string RomMHost { get; private set; }
    public string RomMUsername { get; private set; }
    public string RomMPassword { get; private set; }
    public string RomMApiKey { get; private set; }
    public bool RomMValidLoginLastUsed { get; private set; }

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.configManager = this; 
        
        ChooseRootDir();
        CheckFilesystem();
        LoadConfig();
    }
    
    public void ChooseRootDir()
    {
        if (OS.HasFeature("editor"))
        {
            rootDir = ProjectSettings.GlobalizePath("res://");
            rootDir = rootDir.Remove(rootDir.Length-1);
            GD.Print(rootDir); 

        }
        else
        {
            rootDir = OS.GetExecutablePath().GetBaseDir();
        }
        configDir = rootDir + "/config.cfg";
        config = new ConfigFile();
    }
    
    private void CheckFilesystem()
    {
        if(!DirAccess.DirExistsAbsolute(rootDir + "/roms/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/roms/");
        }

        if (!DirAccess.DirExistsAbsolute(rootDir + "/bios/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/bios/");
        }
        
        if(!DirAccess.DirExistsAbsolute(rootDir + "/emulators/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/emulators/");
        }

        if (!DirAccess.DirExistsAbsolute(rootDir + "/downloads/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/downloads/");
        }

        if (!DirAccess.DirExistsAbsolute(rootDir + "/install_scripts/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/install_scripts/");
        }
        
        if (!DirAccess.DirExistsAbsolute(rootDir + "/tools/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/tools/");
        }

        if (!DirAccess.DirExistsAbsolute(rootDir + "/assets/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/assets/");
        }
        if (!DirAccess.DirExistsAbsolute(rootDir + "/assets/covers_3d/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/assets/covers_3d/");
        }
        if (!DirAccess.DirExistsAbsolute(rootDir + "/assets/covers_2d/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/assets/covers_2d/");
        }
        if (!DirAccess.DirExistsAbsolute(rootDir + "/assets/marquees/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/assets/marquees/");
        }
        if (!DirAccess.DirExistsAbsolute(rootDir + "/assets/covers_fallback/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/assets/covers_fallback/");
        }
        if (!DirAccess.DirExistsAbsolute(rootDir + "/assets/screenshots/"))
        {
            DirAccess.MakeDirAbsolute(rootDir + "/assets/screenshots/");
        }
    }

    private void LoadConfig()
    {
        Error err = config.Load(configDir);
        
        if (err != Error.Ok)
        {
            SetDefaultConfig();
            return;
        }

        RomsPath = (string)config.GetValue("Paths", "RomsRomsPath", $"{rootDir}/roms/");
        BiosPath = (string)config.GetValue("Paths", "BiosPath", $"{rootDir}/bios/");
        EmulatorsPath = (string)config.GetValue("Paths", "EmulatorsPath", $"{rootDir}/emulators/");
        DownloadsPath = (string)config.GetValue("Paths", "DownloadsPath", $"{rootDir}/downloads/");
        InstallScriptsPath = (string)config.GetValue("Paths", "InstallScriptsPath", $"{rootDir}/install_scripts/");
        ToolsPath = (string)config.GetValue("Paths", "ToolsPath", $"{rootDir}/tools/");
        AssetsPath = (string)config.GetValue("Paths", "AssetsPath", $"{rootDir}/assets/");
        RomMHost = (string)config.GetValue("RomM", "Host", "");
        RomMUsername = (string)config.GetValue("RomM", "Username", "");
        RomMPassword = (string)config.GetValue("RomM", "Password", "");
        RomMApiKey = (string)config.GetValue("RomM", "ApiKey", "");
        RomMValidLoginLastUsed = (bool)config.GetValue("RomM", "ValidLoginLastUsed", "");
        
    }

    private void SetDefaultConfig()
    {
        RomsPath = $"{rootDir}/roms/";
        BiosPath = $"{rootDir}/bios/";
        EmulatorsPath = $"{rootDir}/emulators/";
        DownloadsPath = $"{rootDir}/downloads/";
        InstallScriptsPath = $"{rootDir}/install_scripts/";
        ToolsPath = $"{rootDir}/tools/";
        AssetsPath = $"{rootDir}/assets/";
        RomMHost = "";
        RomMUsername = "";
        RomMPassword = "";
        RomMApiKey = "";
        RomMValidLoginLastUsed = false;
        
        config.SetValue("Paths", "RomsPath", RomsPath);
        config.SetValue("Paths", "BiosPath", BiosPath);
        config.SetValue("Paths", "EmulatorsPath", EmulatorsPath);
        config.SetValue("Paths", "DownloadsPath", DownloadsPath);
        config.SetValue("Paths", "InstallScriptsPath", InstallScriptsPath);
        config.SetValue("Paths", "ToolsPath", ToolsPath);
        config.SetValue("Paths", "AssetsPath", AssetsPath);
        config.SetValue("RomM", "Host", RomMHost);
        config.SetValue("RomM", "Username", RomMUsername);
        config.SetValue("RomM", "Password", RomMPassword);
        config.SetValue("RomM", "ApiKey", RomMApiKey);
        config.SetValue("RomM", "ValidLoginLastUsed", RomMValidLoginLastUsed);
        config.Save(configDir);
    }
    
    public void SaveConfig()
    {
        config.SetValue("Paths", "RomsPath", RomsPath);
        config.SetValue("Paths", "BiosPath", BiosPath);
        config.SetValue("Paths", "EmulatorsPath", EmulatorsPath);
        config.SetValue("Paths", "DownloadsPath", DownloadsPath);
        config.SetValue("Paths", "InstallScriptsPath", InstallScriptsPath);
        config.SetValue("Paths", "ToolsPath", ToolsPath);
        config.SetValue("Paths", "AssetsPath", AssetsPath);
        config.SetValue("RomM", "Host", RomMHost);
        config.SetValue("RomM", "Username", RomMUsername);
        config.SetValue("RomM", "Password", RomMPassword);
        config.SetValue("RomM", "ApiKey", RomMApiKey);
        config.SetValue("RomM", "ValidLoginLastUsed", RomMValidLoginLastUsed);
        config.Save(configDir);
    }

    public void SaveValidLoginLastUsed(bool value)
    {
        RomMValidLoginLastUsed = value;
        SaveConfig();
    }

    public void SaveRomMCredentials(string host, string username, string password, string apiKey)
    {
        RomMHost = host;
        RomMUsername = username;
        RomMPassword = password;
        RomMApiKey = apiKey;
        RomMValidLoginLastUsed = true;
        SaveConfig();
    }
}
