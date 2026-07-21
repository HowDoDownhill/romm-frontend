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
    public string AppTheme { get; private set; }

    public static readonly System.Collections.Generic.Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)> Themes = new System.Collections.Generic.Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)>
    {
        { "Default", (new Color(0.05f, 0.02f, 0.1f, 1f).Darkened(0.3f), new Color(0.3f, 0f, 0.4f, 1f).Darkened(0.4f), new Color(0f, 0.2f, 0.4f, 1f).Darkened(0.4f), new Color(0f, 0f, 0f, 0.55f)) },
        { "Rose-pine", (new Color("#191724").Darkened(0.5f), new Color("#c4a7e7").Darkened(0.65f), new Color("#eb6f92").Darkened(0.65f), new Color("#1f1d2e8c")) },
        { "Gruvbox", (new Color("#282828").Darkened(0.5f), new Color("#cc241d").Darkened(0.65f), new Color("#458588").Darkened(0.65f), new Color("#1d20218c")) },
        { "catppuccin", (new Color("#1e1e2e").Darkened(0.5f), new Color("#cba6f7").Darkened(0.65f), new Color("#89b4fa").Darkened(0.65f), new Color("#1818258c")) },
        { "Solarized Dark", (new Color("#002b36").Darkened(0.5f), new Color("#cb4b16").Darkened(0.65f), new Color("#268bd2").Darkened(0.65f), new Color("#00212b8c")) },
        { "Solarized Light", (new Color("#fdf6e3").Darkened(0.7f), new Color("#d33682").Darkened(0.65f), new Color("#2aa198").Darkened(0.65f), new Color("#eee8d58c")) },
        { "monokai", (new Color("#272822").Darkened(0.5f), new Color("#f92672").Darkened(0.65f), new Color("#66d9ef").Darkened(0.65f), new Color("#1e1f1c8c")) },
        { "Nord", (new Color("#2e3440").Darkened(0.5f), new Color("#81a1c1").Darkened(0.65f), new Color("#b48ead").Darkened(0.65f), new Color("#2429338c")) },
        { "Dracula", (new Color("#282a36").Darkened(0.5f), new Color("#bd93f9").Darkened(0.65f), new Color("#ff79c6").Darkened(0.65f), new Color("#1e1f298c")) }
    };

    public int EmulatorCloseHotkeyCount { get; private set; }
    public Godot.Collections.Array EmulatorCloseHotkeys { get; private set; }
    
    public System.Collections.Generic.Dictionary<string, string> PreferredEmulators { get; private set; } = new System.Collections.Generic.Dictionary<string, string>();

    public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, string>>> PlatformInputMappings { get; private set; } = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, string>>>();

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

    // A path that is always resolved relative to the executable's own folder. Used for
    // directories that ship with the app or are ephemeral caches, so a build stays
    // relocatable: move it to another PC/OS and it recomputes its own paths.
    private string DeriveDefaultPath(string subdirectory) => $"{ApplicationRootDirectory}/{subdirectory}/";

    // User-relocatable directory: honor a stored override only if it actually exists on
    // disk, otherwise fall back to the derived default. This makes stale absolute paths
    // (from a moved folder or a different machine/OS) self-heal instead of breaking.
    private string ResolveRelocatablePath(string key, string subdirectory)
    {
        string derivedDefault = DeriveDefaultPath(subdirectory);
        if (configurationFile.HasSectionKey("Paths", key))
        {
            string storedPath = (string)configurationFile.GetValue("Paths", key, derivedDefault);
            if (!string.IsNullOrWhiteSpace(storedPath) && DirAccess.DirExistsAbsolute(storedPath))
            {
                return storedPath;
            }
        }
        return derivedDefault;
    }

    // Resolves every path property. App-layout dirs are always derived from the executable
    // location; user-relocatable dirs use a valid stored override or fall back to the default.
    private void ResolveAllPaths()
    {
        // App layout — shipped with the app or ephemeral cache; always next to the exe.
        DownloadsPath = DeriveDefaultPath("downloads");
        InstallScriptsPath = DeriveDefaultPath("install_scripts");
        ToolsPath = DeriveDefaultPath("tools");
        AssetsPath = DeriveDefaultPath("assets");

        // User-relocatable — a user may point these at a separate/large drive.
        RomsPath = ResolveRelocatablePath("RomsPath", "roms");
        BiosPath = ResolveRelocatablePath("BiosPath", "bios");
        EmulatorsPath = ResolveRelocatablePath("EmulatorsPath", "emulators");
    }

    // Persist a relocatable path only when it's an actual override (differs from the
    // executable-relative default). Otherwise remove any stale key so defaults stay derived.
    private void WriteRelocatablePathValue(string key, string currentValue, string subdirectory)
    {
        if (currentValue != DeriveDefaultPath(subdirectory))
        {
            configurationFile.SetValue("Paths", key, currentValue);
        }
        else if (configurationFile.HasSectionKey("Paths", key))
        {
            configurationFile.EraseSectionKey("Paths", key);
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

        ResolveAllPaths();
        RomMHost = (string)configurationFile.GetValue("RomM", "Host", "");
        RomMUsername = (string)configurationFile.GetValue("RomM", "Username", "");
        RomMPassword = (string)configurationFile.GetValue("RomM", "Password", "");
        RomMApiKey = (string)configurationFile.GetValue("RomM", "ApiKey", "");
        RomMValidLoginLastUsed = (bool)configurationFile.GetValue("RomM", "ValidLoginLastUsed", "");
        HideGamesWithoutBoxArt = (bool)configurationFile.GetValue("UI", "HideGamesWithoutBoxArt", false);
        ShowAllSystems = (bool)configurationFile.GetValue("UI", "ShowAllSystems", false);
        AppTheme = (string)configurationFile.GetValue("UI", "AppTheme", "Default");

        EmulatorCloseHotkeyCount = (int)configurationFile.GetValue("Input", "EmulatorCloseHotkeyCount", 4);
        var defaultHotkeyButtons = new Godot.Collections.Array { (int)JoyButton.LeftShoulder, (int)JoyButton.RightShoulder, (int)JoyButton.Back, (int)JoyButton.Start };
        EmulatorCloseHotkeys = (Godot.Collections.Array)configurationFile.GetValue("Input", "EmulatorCloseHotkeys", defaultHotkeyButtons);
        
        if (configurationFile.HasSection("PreferredEmulators"))
        {
            foreach (string key in configurationFile.GetSectionKeys("PreferredEmulators"))
            {
                PreferredEmulators[key] = (string)configurationFile.GetValue("PreferredEmulators", key);
            }
        }

        if (configurationFile.HasSection("PlatformInputMappings"))
        {
            foreach (string systemSlug in configurationFile.GetSectionKeys("PlatformInputMappings"))
            {
                var mappingsForSystem = (Godot.Collections.Dictionary)configurationFile.GetValue("PlatformInputMappings", systemSlug);
                var dict = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, string>>();
                foreach (var playerKey in mappingsForSystem.Keys)
                {
                    if (int.TryParse(playerKey.ToString(), out int playerIndex))
                    {
                        var playerMappings = (Godot.Collections.Dictionary)mappingsForSystem[playerKey];
                        var playerDict = new System.Collections.Generic.Dictionary<string, string>();
                        foreach (var key in playerMappings.Keys)
                        {
                            playerDict[key.ToString()] = playerMappings[key].ToString();
                        }
                        dict[playerIndex] = playerDict;
                    }
                }
                PlatformInputMappings[systemSlug] = dict;
            }
        }

        ApplyInputMap();
    }

    private void SetDefaultConfiguration()
    {
        ResolveAllPaths();
        RomMHost = "";
        RomMUsername = "";
        RomMPassword = "";
        RomMApiKey = "";
        RomMValidLoginLastUsed = false;
        HideGamesWithoutBoxArt = false;
        ShowAllSystems = false;
        AppTheme = "Default";

        EmulatorCloseHotkeyCount = 4;
        EmulatorCloseHotkeys = new Godot.Collections.Array { (int)JoyButton.LeftShoulder, (int)JoyButton.RightShoulder, (int)JoyButton.Back, (int)JoyButton.Start };

        WriteAllConfigurationValues();
        configurationFile.Save(configurationFilePath);
        ApplyInputMap();
    }

    private void WriteAllConfigurationValues()
    {
        // Only user-relocatable paths are persisted, and only when overridden. App-layout
        // dirs (downloads/install_scripts/tools/assets) are derived from the executable
        // location at load time and intentionally not stored, so builds stay relocatable.
        WriteRelocatablePathValue("RomsPath", RomsPath, "roms");
        WriteRelocatablePathValue("BiosPath", BiosPath, "bios");
        WriteRelocatablePathValue("EmulatorsPath", EmulatorsPath, "emulators");
        // Scrub app-layout keys that older versions baked in, so stale absolute paths don't linger.
        foreach (string deprecatedPathKey in new[] { "DownloadsPath", "InstallScriptsPath", "ToolsPath", "AssetsPath" })
        {
            if (configurationFile.HasSectionKey("Paths", deprecatedPathKey))
            {
                configurationFile.EraseSectionKey("Paths", deprecatedPathKey);
            }
        }
        configurationFile.SetValue("RomM", "Host", RomMHost);
        configurationFile.SetValue("RomM", "Username", RomMUsername);
        configurationFile.SetValue("RomM", "Password", RomMPassword);
        configurationFile.SetValue("RomM", "ApiKey", RomMApiKey);
        configurationFile.SetValue("RomM", "ValidLoginLastUsed", RomMValidLoginLastUsed);
        configurationFile.SetValue("UI", "HideGamesWithoutBoxArt", HideGamesWithoutBoxArt);
        configurationFile.SetValue("UI", "ShowAllSystems", ShowAllSystems);
        configurationFile.SetValue("UI", "AppTheme", AppTheme);
        configurationFile.SetValue("Input", "EmulatorCloseHotkeyCount", EmulatorCloseHotkeyCount);
        configurationFile.SetValue("Input", "EmulatorCloseHotkeys", EmulatorCloseHotkeys);

        if (PreferredEmulators != null)
        {
            foreach (var kvp in PreferredEmulators)
            {
                configurationFile.SetValue("PreferredEmulators", kvp.Key, kvp.Value);
            }
        }

        if (PlatformInputMappings != null)
        {
            foreach (var kvp in PlatformInputMappings)
            {
                var godotDict = new Godot.Collections.Dictionary();
                foreach (var playerMapping in kvp.Value)
                {
                    var playerGodotDict = new Godot.Collections.Dictionary();
                    foreach (var mapping in playerMapping.Value)
                    {
                        playerGodotDict[mapping.Key] = mapping.Value;
                    }
                    godotDict[playerMapping.Key] = playerGodotDict;
                }
                configurationFile.SetValue("PlatformInputMappings", kvp.Key, godotDict);
            }
        }
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

    public void SaveAppTheme(string themeName)
    {
        AppTheme = themeName;
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

    public void SavePreferredEmulator(string systemSlug, string emulatorSlug)
    {
        PreferredEmulators[systemSlug] = emulatorSlug;
        SaveConfig();
    }

    public void SavePlatformInputMapping(string systemSlug, int playerIndex, string platformButton, string standardSdlInput)
    {
        if (!PlatformInputMappings.ContainsKey(systemSlug))
        {
            PlatformInputMappings[systemSlug] = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, string>>();
        }
        if (!PlatformInputMappings[systemSlug].ContainsKey(playerIndex))
        {
            PlatformInputMappings[systemSlug][playerIndex] = new System.Collections.Generic.Dictionary<string, string>();
        }
        PlatformInputMappings[systemSlug][playerIndex][platformButton] = standardSdlInput;
        SaveConfig();
    }
}
