using Godot;

public partial class ConfigManager : Node
{
    public string ApplicationRootDirectory;
    private string configurationFilePath;
    private ConfigFile configurationFile;

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
    public bool HideGamesWithoutBoxArt { get; private set; }
    public bool ShowAllSystems { get; private set; }

    public int EmulatorCloseHotkeyCount { get; private set; }
    public Godot.Collections.Array EmulatorCloseHotkeys { get; private set; }

    private AppInstance appInstance;

    private static readonly string[] requiredSubdirectories = new string[]
    {
        "roms",
        "bios",
        "emulators",
        "downloads",
        "install_scripts",
        "tools",
        "assets",
        "assets/covers_3d",
        "assets/covers_2d",
        "assets/marquees",
        "assets/covers_fallback",
        "assets/screenshots"
    };

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.configManager = this;

        DetermineApplicationRootDirectory();
        EnsureRequiredDirectoriesExist();
        LoadConfiguration();
    }

    public void DetermineApplicationRootDirectory()
    {
        if (OS.HasFeature("editor"))
        {
            ApplicationRootDirectory = ProjectSettings.GlobalizePath("res://");
            ApplicationRootDirectory = ApplicationRootDirectory.Remove(ApplicationRootDirectory.Length - 1);
        }
        else
        {
            ApplicationRootDirectory = OS.GetExecutablePath().GetBaseDir();
        }
        configurationFilePath = ApplicationRootDirectory + "/config.cfg";
        configurationFile = new ConfigFile();
    }

    private void EnsureRequiredDirectoriesExist()
    {
        foreach (string subdirectoryRelativePath in requiredSubdirectories)
        {
            string fullDirectoryPath = ApplicationRootDirectory + "/" + subdirectoryRelativePath + "/";
            if (!DirAccess.DirExistsAbsolute(fullDirectoryPath))
            {
                DirAccess.MakeDirAbsolute(fullDirectoryPath);
            }
        }
    }

    private void LoadConfiguration()
    {
        Error loadError = configurationFile.Load(configurationFilePath);

        if (loadError != Error.Ok)
        {
            SetDefaultConfiguration();
            return;
        }

        RomsPath = (string)configurationFile.GetValue("Paths", "RomsRomsPath", $"{ApplicationRootDirectory}/roms/");
        BiosPath = (string)configurationFile.GetValue("Paths", "BiosPath", $"{ApplicationRootDirectory}/bios/");
        EmulatorsPath = (string)configurationFile.GetValue("Paths", "EmulatorsPath", $"{ApplicationRootDirectory}/emulators/");
        DownloadsPath = (string)configurationFile.GetValue("Paths", "DownloadsPath", $"{ApplicationRootDirectory}/downloads/");
        InstallScriptsPath = (string)configurationFile.GetValue("Paths", "InstallScriptsPath", $"{ApplicationRootDirectory}/install_scripts/");
        ToolsPath = (string)configurationFile.GetValue("Paths", "ToolsPath", $"{ApplicationRootDirectory}/tools/");
        AssetsPath = (string)configurationFile.GetValue("Paths", "AssetsPath", $"{ApplicationRootDirectory}/assets/");
        RomMHost = (string)configurationFile.GetValue("RomM", "Host", "");
        RomMUsername = (string)configurationFile.GetValue("RomM", "Username", "");
        RomMPassword = (string)configurationFile.GetValue("RomM", "Password", "");
        RomMApiKey = (string)configurationFile.GetValue("RomM", "ApiKey", "");
        RomMValidLoginLastUsed = (bool)configurationFile.GetValue("RomM", "ValidLoginLastUsed", "");
        HideGamesWithoutBoxArt = (bool)configurationFile.GetValue("UI", "HideGamesWithoutBoxArt", false);
        ShowAllSystems = (bool)configurationFile.GetValue("UI", "ShowAllSystems", false);

        EmulatorCloseHotkeyCount = (int)configurationFile.GetValue("Input", "EmulatorCloseHotkeyCount", 4);
        var defaultHotkeyButtons = new Godot.Collections.Array { (int)JoyButton.LeftShoulder, (int)JoyButton.RightShoulder, (int)JoyButton.Back, (int)JoyButton.Start };
        EmulatorCloseHotkeys = (Godot.Collections.Array)configurationFile.GetValue("Input", "EmulatorCloseHotkeys", defaultHotkeyButtons);
        ApplyInputMap();
    }

    private void SetDefaultConfiguration()
    {
        RomsPath = $"{ApplicationRootDirectory}/roms/";
        BiosPath = $"{ApplicationRootDirectory}/bios/";
        EmulatorsPath = $"{ApplicationRootDirectory}/emulators/";
        DownloadsPath = $"{ApplicationRootDirectory}/downloads/";
        InstallScriptsPath = $"{ApplicationRootDirectory}/install_scripts/";
        ToolsPath = $"{ApplicationRootDirectory}/tools/";
        AssetsPath = $"{ApplicationRootDirectory}/assets/";
        RomMHost = "";
        RomMUsername = "";
        RomMPassword = "";
        RomMApiKey = "";
        RomMValidLoginLastUsed = false;
        HideGamesWithoutBoxArt = false;
        ShowAllSystems = false;

        EmulatorCloseHotkeyCount = 4;
        EmulatorCloseHotkeys = new Godot.Collections.Array { (int)JoyButton.LeftShoulder, (int)JoyButton.RightShoulder, (int)JoyButton.Back, (int)JoyButton.Start };

        WriteAllConfigurationValues();
        configurationFile.Save(configurationFilePath);
        ApplyInputMap();
    }

    private void WriteAllConfigurationValues()
    {
        configurationFile.SetValue("Paths", "RomsPath", RomsPath);
        configurationFile.SetValue("Paths", "BiosPath", BiosPath);
        configurationFile.SetValue("Paths", "EmulatorsPath", EmulatorsPath);
        configurationFile.SetValue("Paths", "DownloadsPath", DownloadsPath);
        configurationFile.SetValue("Paths", "InstallScriptsPath", InstallScriptsPath);
        configurationFile.SetValue("Paths", "ToolsPath", ToolsPath);
        configurationFile.SetValue("Paths", "AssetsPath", AssetsPath);
        configurationFile.SetValue("RomM", "Host", RomMHost);
        configurationFile.SetValue("RomM", "Username", RomMUsername);
        configurationFile.SetValue("RomM", "Password", RomMPassword);
        configurationFile.SetValue("RomM", "ApiKey", RomMApiKey);
        configurationFile.SetValue("RomM", "ValidLoginLastUsed", RomMValidLoginLastUsed);
        configurationFile.SetValue("UI", "HideGamesWithoutBoxArt", HideGamesWithoutBoxArt);
        configurationFile.SetValue("UI", "ShowAllSystems", ShowAllSystems);
        configurationFile.SetValue("Input", "EmulatorCloseHotkeyCount", EmulatorCloseHotkeyCount);
        configurationFile.SetValue("Input", "EmulatorCloseHotkeys", EmulatorCloseHotkeys);
    }

    public void SaveConfig()
    {
        WriteAllConfigurationValues();
        configurationFile.Save(configurationFilePath);
        ApplyInputMap();
    }

    public void SaveValidLoginLastUsed(bool isValidLogin)
    {
        RomMValidLoginLastUsed = isValidLogin;
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

    public void SaveGameListSettings(bool shouldHideGamesWithoutBoxArt, bool showAllSystems)
    {
        HideGamesWithoutBoxArt = shouldHideGamesWithoutBoxArt;
        ShowAllSystems = showAllSystems;
        SaveConfig();
    }

    public void SaveInputSettings(int hotkeyCount, Godot.Collections.Array hotkeyButtons)
    {
        EmulatorCloseHotkeyCount = hotkeyCount;
        EmulatorCloseHotkeys = hotkeyButtons;
        SaveConfig();
    }

    public void ApplyInputMap()
    {
        for (int actionIndex = 1; actionIndex <= 10; actionIndex++)
        {
            if (InputMap.HasAction($"CloseKey{actionIndex}"))
            {
                InputMap.EraseAction($"CloseKey{actionIndex}");
            }
        }
        for (int hotkeyIndex = 0; hotkeyIndex < EmulatorCloseHotkeyCount; hotkeyIndex++)
        {
            string inputActionName = $"CloseKey{hotkeyIndex + 1}";
            InputMap.AddAction(inputActionName);
            var joypadButtonEvent = new InputEventJoypadButton();
            if (hotkeyIndex < EmulatorCloseHotkeys.Count)
            {
                joypadButtonEvent.ButtonIndex = (JoyButton)EmulatorCloseHotkeys[hotkeyIndex].AsInt32();
            }
            else
            {
                joypadButtonEvent.ButtonIndex = JoyButton.Invalid;
            }
            InputMap.ActionAddEvent(inputActionName, joypadButtonEvent);
        }
    }
}
