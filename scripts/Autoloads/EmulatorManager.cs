using System;
using Godot;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FileAccess = Godot.FileAccess;
using DirAccess = Godot.DirAccess;

public class EmulatorMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("executable_name")]
    public Dictionary<string, string> ExecutableName { get; set; }
    
    [JsonPropertyName("emulator_dir_name")]
    public Dictionary<string, string> EmulatorDirName { get; set; }

    [JsonPropertyName("launch_args_with_game")]
    public string LaunchArgsWithGame { get; set; }
    
    [JsonPropertyName("launch_args_without_game")]
    public string LaunchArgsWithoutGame { get; set; }
    
    [JsonPropertyName("install_recipe")]
    public Dictionary<string, InstallRecipe> InstallRecipe { get; set; }
}

public class InstallRecipe
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("repo")]
    public string Repo { get; set; }

    [JsonPropertyName("asset_regex")]
    public string AssetRegex { get; set; }

    [JsonPropertyName("extract")]
    public bool Extract { get; set; } = true;

    [JsonPropertyName("extract_folder_regex")]
    public string ExtractFolderRegex { get; set; }
}

public partial class EmulatorManager : Node
{
    private string emulatorMapPath;
    private string executableMapPath;

    private Dictionary<string, string> emulatorMap = new Dictionary<string, string>();

    private AppInstance appInstance;

    public MainScene mainScene;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.emulatorManager = this; 

        SetPaths();
        LoadOrGenerateEmulatorMap();
    }

    private void SetPaths()
    {
        emulatorMapPath = Path.Combine(appInstance.configManager.EmulatorsPath, "EmulatorMap.json");
        GD.Print(emulatorMapPath);
        executableMapPath = Path.Combine(appInstance.configManager.EmulatorsPath, "ExecutableMap.json");
        GD.Print(executableMapPath);
    }

    private void LoadOrGenerateEmulatorMap()
    {
        if (!FileAccess.FileExists(emulatorMapPath) || !FileAccess.FileExists(executableMapPath))
        {
            GenerateDefaultMaps();
        }

        try
        {
            string jsonString = FileAccess.GetFileAsString(emulatorMapPath);
            emulatorMap = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            GD.Print("Loaded emulator map from user directory.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load emulator map: {e.Message}");
        }
    }

    public string GetMappedEmulator(string systemSlug)
    {
        if (string.IsNullOrEmpty(systemSlug)) return null;

        if (emulatorMap.ContainsKey(systemSlug))
        {
            return emulatorMap[systemSlug]; 
        }
        
        /*
        foreach (var kvp in emulatorMap)
        {
            var slugs = kvp.Key.Split(',');
            foreach (var slug in slugs)
            {
                if (slug.Trim().Equals(systemSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }
        */
        return null;
    }

    public bool IsEmulatorInstalled(string emulatorName)
    {
        if (string.IsNullOrEmpty(emulatorName)) return false;

        string metaPath = appInstance.configManager.InstallScriptsPath.PathJoin(emulatorName).PathJoin("meta.json");
        if (!FileAccess.FileExists(metaPath))
        {
            return false;
        }

        try
        {
            var metaJson = FileAccess.GetFileAsString(metaPath);
            var meta = JsonSerializer.Deserialize<EmulatorMeta>(metaJson);
            
            string osName = OS.GetName().ToLower();
            
            if (meta == null || meta.EmulatorDirName == null || meta.ExecutableName == null ||
                !meta.EmulatorDirName.ContainsKey(osName) || !meta.ExecutableName.ContainsKey(osName))
            {
                 return false;
            }

            string installDir = appInstance.configManager.EmulatorsPath.PathJoin(emulatorName); 
            
            string executableRelativePath = meta.ExecutableName[osName];
            string fullExecutablePath = Path.Combine(installDir, executableRelativePath);
            fullExecutablePath = Path.GetFullPath(fullExecutablePath);
            GD.Print(fullExecutablePath);

            return FileAccess.FileExists(fullExecutablePath);
        }
        catch (Exception e)
        {
             GD.PrintErr($"Error checking if emulator is installed: {e.Message}");
             return false;
        }
    }
    
    public async Task InstallEmulator(string emulatorName)
    {
        string emulatorScriptDir = appInstance.configManager.InstallScriptsPath.PathJoin(emulatorName);
        string metaPath = emulatorScriptDir.PathJoin("meta.json");
        
        if (!FileAccess.FileExists(metaPath))
        {
            GD.PrintErr($"Emulator recipe not found at: {metaPath}");
            return;
        }

        string osName = OS.GetName().ToLower();

        try
        {
            var metaJson = FileAccess.GetFileAsString(metaPath);
            var meta = JsonSerializer.Deserialize<EmulatorMeta>(metaJson);

            bool success = await UniversalInstaller.Install(appInstance, emulatorName, meta, osName);

            if (success)
            {
                GD.Print($"Successfully installed {emulatorName}.");
            }
            else
            {
                GD.PrintErr($"Failed to install {emulatorName}.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Exception during install: {ex.Message}");
        }

        mainScene.UpdateDetailsPanelButtons(mainScene.currentlySelectedGame);  
    }

    public void LaunchEmulatorWithGame(Game game)
    {
        GD.Print("--- Launching Emulator with Game ---");

        if (game == null)
        {
            GD.PrintErr("Game object is null.");
            return;
        }
        GD.Print($"Game: {game.Name}");

        if (game.System == null)
        {
            GD.PrintErr("Game.System is null.");
            return;
        }
        GD.Print($"Game System: {game.System.Name}");
        GD.Print($"Game System Slug: {game.System.Slug}");

        string mappedEmulator = GetMappedEmulator(game.System.Slug);
        if (string.IsNullOrEmpty(mappedEmulator))
        {
            GD.PrintErr($"No emulator mapped for system: {game.System.Name} ({game.System.Slug})");
            return;
        }
        GD.Print($"Mapped Emulator: {mappedEmulator}");

        string metaPath = appInstance.configManager.InstallScriptsPath.PathJoin(mappedEmulator).PathJoin("meta.json");
        if (!FileAccess.FileExists(metaPath))
        {
            GD.PrintErr($"Meta file not found: {metaPath}");
            return;
        }
        GD.Print($"Meta Path: {metaPath}");

        try
        {
            string osName = OS.GetName().ToLower();
            GD.Print($"Operating System: {osName}");

            var metaJson = FileAccess.GetFileAsString(metaPath);
            var meta = JsonSerializer.Deserialize<EmulatorMeta>(metaJson);

            if (meta == null)
            {
                GD.PrintErr("meta.json could not be deserialized.");
                return;
            }
            if (meta.EmulatorDirName == null || !meta.EmulatorDirName.ContainsKey(osName))
            {
                GD.PrintErr("EmulatorDirName is missing or does not contain key for the current OS.");
                return;
            }
            if (meta.ExecutableName == null || !meta.ExecutableName.ContainsKey(osName))
            {
                GD.PrintErr("ExecutableName is missing or does not contain key for the current OS.");
                return;
            }

            string installDir = Path.Combine(appInstance.configManager.EmulatorsPath, meta.EmulatorDirName[osName]);
            GD.Print($"Install Directory: {installDir}");

            string executableRelativePath = meta.ExecutableName[osName];
            GD.Print($"Executable Relative Path: {executableRelativePath}");

            string fullExecutablePath = Path.Combine(installDir, executableRelativePath);
            GD.Print($"Full Executable Path: {fullExecutablePath}");

            if (game.Files == null || game.Files.Count == 0)
            {
                GD.PrintErr("Game has no files.");
                return;
            }
            string romFileName = game.Files[0].FileName;
            GD.Print($"ROM File Name: {romFileName}");

            string romPath = Path.GetFullPath(Path.Combine(appInstance.configManager.RomsPath, game.System.Slug, romFileName));
            GD.Print($"ROM Path: {romPath}");

            string arguments = meta.LaunchArgsWithGame;
            if (string.IsNullOrEmpty(arguments))
            {
                GD.PrintErr("LaunchArgsWithGame is not defined in meta.json.");
                return;
            }
            GD.Print($"Raw Arguments: {arguments}");

            arguments = arguments.Replace("{rom_path}", romPath);

            if (!string.IsNullOrEmpty(game.System.PrefferedFirmware))
            {
                string biosPath = Path.GetFullPath(game.System.PrefferedFirmware);
                GD.Print($"Preferred Firmware Path: {biosPath}");
                arguments = arguments.Replace("{bios_path}", biosPath);
            }
            else
            {
                GD.Print("No preferred firmware set.");
            }

            GD.Print($"Launching Command: \"{fullExecutablePath}\" {arguments}");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fullExecutablePath,
                Arguments = arguments,
                WorkingDirectory = installDir,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process emulatorProcess = Process.Start(startInfo);
            if (emulatorProcess != null)
            {
                GD.Print($"Emulator launched with PID: {emulatorProcess.Id}");
                emulatorProcess.EnableRaisingEvents = true;
                emulatorProcess.Exited += (sender, e) =>
                {
                    GD.Print("Emulator was closed.");
                };
            }
            else
            {
                GD.PrintErr("Failed to start emulator process. Process.Start returned null.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"An exception occurred while launching the emulator: {ex.Message}");
            GD.PrintErr($"Stack Trace: {ex.StackTrace}");
        }
    }

    public void LaunchEmulatorWithoutGame(string emulatorName)
    {
        if (string.IsNullOrEmpty(emulatorName))
        {
            GD.PrintErr("No emulator name provided.");
            return;
        }

        string metaPath = appInstance.configManager.InstallScriptsPath.PathJoin(emulatorName).PathJoin("meta.json");
        
        if (!FileAccess.FileExists(metaPath))
        {
            GD.PrintErr($"Meta file not found for mapped emulator: {metaPath}");
            return;
        }
        
        try 
        {
            string osName = OS.GetName().ToLower();
            var metaJson = FileAccess.GetFileAsString(metaPath);
            var meta = JsonSerializer.Deserialize<EmulatorMeta>(metaJson);
            
            if (meta == null || meta.EmulatorDirName == null || meta.ExecutableName == null ||
                !meta.EmulatorDirName.ContainsKey(osName) || !meta.ExecutableName.ContainsKey(osName))
            {
                 GD.PrintErr($"Incomplete meta.json for {emulatorName} on OS: {osName}");
                 return;
            }

            string installDir = appInstance.configManager.EmulatorsPath + meta.EmulatorDirName[osName];
            string executableRelativePath = meta.ExecutableName[osName];
            string fullExecutablePath = Path.Join(installDir, executableRelativePath);
            
            string arguments = meta.LaunchArgsWithoutGame;

            var currentSystem = mainScene.gameSystems[mainScene.currentGameSystemIndex];
            
            if (currentSystem != null && !string.IsNullOrEmpty(currentSystem.PrefferedFirmware))
            {
                arguments = arguments.Replace("{bios_path}", Path.GetFullPath(currentSystem.PrefferedFirmware));
            }
            
            GD.Print($"Launching: {fullExecutablePath} {arguments}"); 
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fullExecutablePath,
                Arguments = arguments,
                WorkingDirectory = installDir, 
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process emulatorProcess = Process.Start(startInfo);
            if (emulatorProcess != null)
            {
                GD.Print($"Emulator launched with PID: {emulatorProcess.Id}");
                emulatorProcess.EnableRaisingEvents = true;
                emulatorProcess.Exited += (sender, e) => 
                {
                    GD.Print("Emulator was closed.");
                };
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to launch emulator: {ex.Message}");
        }
    }
    
    private void GenerateDefaultMaps()
    {
        var platformEmulatorMap = new Dictionary<string, string>
        {
            {"ngc", "dolphin"},
            {"wii", "dolphin"},
            {"snes", "snes9x"},
            {"n64", "gopher64"},
            {"nes", "nestopia"},
            {"gb", "mGBA"},
            {"gba", "mGBA"},
            {"nds", "melonDS"},
            {"psx", "duckstation"},
            {"ps2", "pcsx2"},
            {"ps3", "rpcs3"},
            {"ps4", "shadPS4"},
            {"psp", "ppsspp"},
            {"sega32", "ares"},
            {"segacd", "ares"},
            {"sms", "ares"},
            {"genesis", "ares"},
            {"dc", "flycast"}
        };
        
        try
        {
            string jsonString = JsonSerializer.Serialize(platformEmulatorMap, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(emulatorMapPath, FileAccess.ModeFlags.Write);
            file.StoreString(jsonString);
            GD.Print("Generated default emulator map.");
        }
        
        catch (Exception e)
        {
            GD.PrintErr($"Failed to generate default emulator map: {e.Message}");
        }
        
        try
        {
            string jsonString = JsonSerializer.Serialize(executableMapPath, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(executableMapPath, FileAccess.ModeFlags.Write);
            file.StoreString(jsonString);
            GD.Print("Generated default executable map.");
        }
        
        catch (Exception e)
        {
            GD.PrintErr($"Failed to generate default exectuable map: {e.Message}");
        }
    }
}
