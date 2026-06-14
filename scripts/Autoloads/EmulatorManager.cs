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

    [JsonPropertyName("launch_args")]
    public string LaunchArgs { get; set; }
    
    [JsonPropertyName("emulator_dir_name")]
    public Dictionary<string, string> EmulatorDirName { get; set; }
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

        string scriptPath = "";
        string osName = OS.GetName();

        if (osName == "Windows")
        {
            scriptPath = emulatorScriptDir.PathJoin("install_windows.ps1");
        }
        else if (osName == "Linux")
        {
            scriptPath = emulatorScriptDir.PathJoin("install_linux.sh");
        }
        else if (osName == "macOS")
        {
            scriptPath = emulatorScriptDir.PathJoin("install_macos.sh");
        }
        else
        {
            GD.PrintErr($"Unsupported OS: {osName}");
            return;
        }

        if (!FileAccess.FileExists(scriptPath))
        {
            GD.PrintErr($"Installation script not found: {scriptPath}");
            return;
        }

        await RunInstallScript(scriptPath, appInstance.configManager.EmulatorsPath, osName);
        mainScene.UpdateDetailsPanelButtons(mainScene.currentlySelectedGame);  

    }

    private Task RunInstallScript(string scriptPath, string installDir, string osName)
    {
        var tcs = new TaskCompletionSource<bool>();
        var process = new Process();
        
        try
        {
            if (osName == "Windows")
            {
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{ProjectSettings.GlobalizePath(scriptPath)}\" -InstallDirectory \"{ProjectSettings.GlobalizePath(installDir)}\"";
            }
            else
            {
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"\"{ProjectSettings.GlobalizePath(scriptPath)}\" \"{ProjectSettings.GlobalizePath(installDir)}\"";
            }

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (sender, args) => {
                if (args.Data != null) GD.Print(args.Data);
            };
            process.ErrorDataReceived += (sender, args) => {
                if (args.Data != null) GD.PrintErr(args.Data);
            };

            process.Exited += (sender, args) =>
            {
                if (process.ExitCode == 0)
                {
                    GD.Print("Installation script finished successfully.");
                    tcs.SetResult(true);
                }
                else
                {
                    GD.PrintErr($"Installation script failed with exit code: {process.ExitCode}");
                    tcs.SetResult(false);
                }
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to start installation process: {e.Message}");
            tcs.SetResult(false);
        }

        return tcs.Task;
    }

    public void LaunchEmulator(Game game)
    {
        string mappedEmulator = GetMappedEmulator(game.System.Slug);
        
        if (string.IsNullOrEmpty(mappedEmulator))
        {
            GD.PrintErr($"No emulator mapped for system: {game.System.Name} ({game.System.Slug})");
            return;
        }

        string metaPath = appInstance.configManager.InstallScriptsPath.PathJoin(mappedEmulator).PathJoin("meta.json");
        
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
                 GD.PrintErr($"Incomplete meta.json for {mappedEmulator} on OS: {osName}");
                 return;
            }

            string installDir = appInstance.configManager.EmulatorsPath + meta.EmulatorDirName[osName];
            string executableRelativePath = meta.ExecutableName[osName];
            string fullExecutablePath = Path.Combine(installDir, executableRelativePath);

            string romPath = ProjectSettings.GlobalizePath(Path.Combine(
                appInstance.configManager.RomsPath, 
                game.System.Slug, 
                game.Files[0].FileName));
            
            string arguments = meta.LaunchArgs;
            arguments = arguments.Replace("{rom_path}", romPath);
            

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
            {"n64", "mupen64plus"},
            {"nes", "nestopia"},
            {"gb", "mGBA"},
            {"gba", "mGBA"},
            {"nds", "melonDS"},
            {"psx", "DuckStation"},
            {"ps2", "PCSX2"},
            {"ps3", "RPCS3"},
            {"ps4", "shadPS4"},
            {"psp", "ppsspp"},
            {"sega32", "ares"},
            {"segacd", "ares"},
            {"sms", "ares"},
            {"genesis", "ares"}
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

        var platformExecutableMap = new Dictionary<string, string>
        {
            { "dolphin", "Dolphin.exe" },
            { "snes9x", "snes9x-x64.exe" },
            { "mupen64plus", "mupen64plus.exe" },
            { "nestopia", "nestopia.exe" },
            { "mGBA", "mgba.exe" },
            { "melonDS", "melonDS.exe" },
            { "DuckStation", "duckstation-qt.exe" },
            { "PCSX2", "pcsx2-qt.exe" },
            { "RPCS3", "rpcs3.exe" },
            { "shadPS4", "shadps4.exe" },
            { "ppsspp", "PPSSPPWindows64.exe" },
            { "ares", "ares.exe" }
        };
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


