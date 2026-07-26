using Godot;

public partial class ConfigManager : Node
{
    public string ApplicationRootDirectory;
    private string configurationFilePath;
    private ConfigFile configurationFile;

    public string RomsPath { get; private set; }
    public string BiosPath { get; private set; }
    public string EmulatorsPath { get; private set; }
    public string SavesPath { get; private set; }
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
    public string AppBackground { get; private set; }

    public static readonly (string Name, string ShaderPath)[] BackgroundStyles = new (string, string)[]
    {
        ("Flow", "res://assets/shaders/backgrounds/bg_flow.gdshader"),
        ("Blobs", "res://assets/shaders/backgrounds/bg_blobs.gdshader"),
        ("Waves", "res://assets/shaders/backgrounds/bg_waves.gdshader"),
        ("Aurora", "res://assets/shaders/backgrounds/bg_aurora.gdshader"),
        ("Bokeh", "res://assets/shaders/backgrounds/bg_bokeh.gdshader"),
        ("Grid", "res://assets/shaders/backgrounds/bg_grid.gdshader"),
        ("Gradient", "res://assets/shaders/backgrounds/bg_gradient.gdshader"),
        ("Solid", "res://assets/shaders/backgrounds/bg_solid.gdshader")
    };

    public static string BackgroundShaderPath(string styleName)
    {
        foreach (var style in BackgroundStyles)
        {
            if (style.Name == styleName) return style.ShaderPath;
        }
        return BackgroundStyles[0].ShaderPath;
    }

    public static readonly System.Collections.Generic.Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)> Themes = new System.Collections.Generic.Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)>();

    public const string ThemesFileName = "themes.json";

    public const string SystemThemeName = "Match System";

    public static bool IsSystemTheme(string themeName) => themeName == SystemThemeName;

    private static readonly System.Collections.Generic.Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)> BuiltInThemes = new System.Collections.Generic.Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)>
    {
        { "Default", (new Color("#0a0713"), new Color("#341052"), new Color("#123a63"), new Color("#100c1a8c")) },
        { "Rose-pine", (new Color("#16141f"), new Color("#4a3663"), new Color("#8a3f59"), new Color("#1f1d2e8c")) },
        { "Gruvbox", (new Color("#1d2021"), new Color("#7a2f24"), new Color("#2f5f57"), new Color("#2828288c")) },
        { "catppuccin", (new Color("#181825"), new Color("#4c3a6b"), new Color("#33517f"), new Color("#1e1e2e8c")) },
        { "Solarized Dark", (new Color("#00212b"), new Color("#6b3410"), new Color("#10537f"), new Color("#0736428c")) },
        { "Solarized Light", (new Color("#3b3630"), new Color("#6e3a55"), new Color("#2f7a70"), new Color("#4a443a8c")) },
        { "monokai", (new Color("#1e1f1c"), new Color("#7a1436"), new Color("#1f6b7a"), new Color("#2728228c")) },
        { "Nord", (new Color("#232833"), new Color("#35506e"), new Color("#4a7a70"), new Color("#2e34408c")) },
        { "Dracula", (new Color("#21222c"), new Color("#4a3a7d"), new Color("#8a3f6e"), new Color("#282a368c")) },
        { "Tokyo Night", (new Color("#16161e"), new Color("#2f3b6b"), new Color("#5c3a7a"), new Color("#1a1b268c")) },
        { "Synthwave", (new Color("#0d0620"), new Color("#6b1050"), new Color("#0e6b8a"), new Color("#140a2b8c")) },
        { "Ember", (new Color("#140c08"), new Color("#7a2c0c"), new Color("#8a6b14"), new Color("#1c110b8c")) },
        { "Abyss", (new Color("#030d14"), new Color("#0b3a4d"), new Color("#1a2f6b"), new Color("#06141e8c")) },
        { "Verdant", (new Color("#0a120c"), new Color("#1d4a2b"), new Color("#5c7014"), new Color("#0e1a118c")) },
        { "Noir", (new Color("#0f0f11"), new Color("#2b2b30"), new Color("#5a5a66"), new Color("#17171a8c")) }
    };

    public string ThemesFilePath => System.IO.Path.Combine(ApplicationRootDirectory, ThemesFileName);

    private void LoadThemes()
    {
        Themes.Clear();
        foreach (var builtIn in BuiltInThemes)
        {
            Themes[builtIn.Key] = builtIn.Value;
        }

        string path = ThemesFilePath;
        if (!FileAccess.FileExists(path))
        {
            WriteThemesFile();
            return;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushWarning($"Could not read {ThemesFileName}: {FileAccess.GetOpenError()}. Using built-in themes.");
            return;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushWarning($"{ThemesFileName} is not a JSON object. Using built-in themes.");
            return;
        }

        foreach (var entry in (Godot.Collections.Dictionary)parsed)
        {
            string themeName = entry.Key.AsString();
            if (entry.Value.VariantType != Variant.Type.Dictionary)
            {
                GD.PushWarning($"{ThemesFileName}: theme \"{themeName}\" is not an object, skipping.");
                continue;
            }

            var fields = (Godot.Collections.Dictionary)entry.Value;
            if (TryParseColor(fields, "bg", themeName, out Color bg)
                && TryParseColor(fields, "primary", themeName, out Color primary)
                && TryParseColor(fields, "secondary", themeName, out Color secondary)
                && TryParseColor(fields, "panel", themeName, out Color panel))
            {
                Themes[themeName] = (bg, primary, secondary, panel);
            }
        }
    }

    private static bool TryParseColor(Godot.Collections.Dictionary fields, string key, string themeName, out Color color)
    {
        color = default;
        if (!fields.ContainsKey(key))
        {
            GD.PushWarning($"{ThemesFileName}: theme \"{themeName}\" is missing \"{key}\", skipping.");
            return false;
        }

        string raw = fields[key].AsString();
        if (!Color.HtmlIsValid(raw))
        {
            GD.PushWarning($"{ThemesFileName}: theme \"{themeName}\" has invalid color \"{raw}\" for \"{key}\", skipping.");
            return false;
        }

        color = new Color(raw);
        return true;
    }

    private void WriteThemesFile()
    {
        var document = new Godot.Collections.Dictionary();
        foreach (var theme in Themes)
        {
            document[theme.Key] = new Godot.Collections.Dictionary
            {
                { "bg", "#" + theme.Value.Bg.ToHtml(false) },
                { "primary", "#" + theme.Value.Primary.ToHtml(false) },
                { "secondary", "#" + theme.Value.Secondary.ToHtml(false) },
                { "panel", "#" + theme.Value.Panel.ToHtml(true) }
            };
        }

        using var file = FileAccess.Open(ThemesFilePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushWarning($"Could not write {ThemesFileName}: {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(Json.Stringify(document, "    "));
    }

    public int EmulatorCloseHotkeyCount { get; private set; }
    public Godot.Collections.Array EmulatorCloseHotkeys { get; private set; }

    public System.Collections.Generic.Dictionary<string, string> PreferredEmulators { get; private set; } = new System.Collections.Generic.Dictionary<string, string>();

    public System.Collections.Generic.Dictionary<string, string> PreferredCores { get; private set; } = new System.Collections.Generic.Dictionary<string, string>();

    public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, string>>> PlatformInputMappings { get; private set; } = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, string>>>();

    private AppInstance appInstance;

    private static readonly string[] requiredSubdirectories = new string[]
    {
        "roms",
        "bios",
        "emulators",
        "saves",
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

    private static readonly string[] subdirectoriesHoldingNonProjectFiles = new string[]
    {
        "roms",
        "bios",
        "emulators",
        "saves",
        "downloads",
        "install_scripts",
        "tools",
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
        LoadThemes();
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

        foreach (string subdirectoryRelativePath in subdirectoriesHoldingNonProjectFiles)
        {
            string godotIgnoreFilePath = ApplicationRootDirectory + "/" + subdirectoryRelativePath + "/.gdignore";

            if (!FileAccess.FileExists(godotIgnoreFilePath))
            {
                using var godotIgnoreFile = FileAccess.Open(godotIgnoreFilePath, FileAccess.ModeFlags.Write);
            }
        }
    }

    private string DeriveDefaultPath(string subdirectory) => $"{ApplicationRootDirectory}/{subdirectory}/";

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

    private void ResolveAllPaths()
    {
        DownloadsPath = DeriveDefaultPath("downloads");
        InstallScriptsPath = DeriveDefaultPath("install_scripts");
        ToolsPath = DeriveDefaultPath("tools");
        AssetsPath = DeriveDefaultPath("assets");

        RomsPath = ResolveRelocatablePath("RomsPath", "roms");
        BiosPath = ResolveRelocatablePath("BiosPath", "bios");
        EmulatorsPath = ResolveRelocatablePath("EmulatorsPath", "emulators");
        SavesPath = ResolveRelocatablePath("SavesPath", "saves");
    }

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
        AppBackground = (string)configurationFile.GetValue("UI", "AppBackground", "Flow");

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

        if (configurationFile.HasSection("PreferredCores"))
        {
            foreach (string key in configurationFile.GetSectionKeys("PreferredCores"))
            {
                PreferredCores[key] = (string)configurationFile.GetValue("PreferredCores", key);
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
        AppBackground = "Flow";

        EmulatorCloseHotkeyCount = 4;
        EmulatorCloseHotkeys = new Godot.Collections.Array { (int)JoyButton.LeftShoulder, (int)JoyButton.RightShoulder, (int)JoyButton.Back, (int)JoyButton.Start };

        WriteAllConfigurationValues();
        configurationFile.Save(configurationFilePath);
        ApplyInputMap();
    }

    private void WriteAllConfigurationValues()
    {
        WriteRelocatablePathValue("RomsPath", RomsPath, "roms");
        WriteRelocatablePathValue("BiosPath", BiosPath, "bios");
        WriteRelocatablePathValue("EmulatorsPath", EmulatorsPath, "emulators");
        WriteRelocatablePathValue("SavesPath", SavesPath, "saves");
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
        configurationFile.SetValue("UI", "AppBackground", AppBackground);
        configurationFile.SetValue("Input", "EmulatorCloseHotkeyCount", EmulatorCloseHotkeyCount);
        configurationFile.SetValue("Input", "EmulatorCloseHotkeys", EmulatorCloseHotkeys);

        if (PreferredEmulators != null)
        {
            foreach (var kvp in PreferredEmulators)
            {
                configurationFile.SetValue("PreferredEmulators", kvp.Key, kvp.Value);
            }
        }

        if (PreferredCores != null)
        {
            foreach (var kvp in PreferredCores)
            {
                configurationFile.SetValue("PreferredCores", kvp.Key, kvp.Value);
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

    public void SaveAppBackground(string backgroundStyle)
    {
        AppBackground = backgroundStyle;
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

    public void SavePreferredCore(string systemSlug, string coreName)
    {
        PreferredCores[systemSlug] = coreName;
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
