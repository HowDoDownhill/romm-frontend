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

public partial class EmulatorManager : Node
{
    private const string EmulatorsBasePath = "user://emulators/";
    private const string InstallScriptsBasePath = "user://install_scripts/";
    private const string EmulatorMapPath = "user://install_scripts/emulator_map.json";

    private Dictionary<string, string> _emulatorMap = new Dictionary<string, string>();

    public override void _Ready()
    {
        EnsureDirectoriesExist();
        LoadOrGenerateEmulatorMap();
    }

    private void EnsureDirectoriesExist()
    {
        if (!DirAccess.DirExistsAbsolute(EmulatorsBasePath))
        {
            DirAccess.MakeDirRecursiveAbsolute(EmulatorsBasePath);
        }

        if (!DirAccess.DirExistsAbsolute(InstallScriptsBasePath))
        {
            DirAccess.MakeDirRecursiveAbsolute(InstallScriptsBasePath);
        }
    }

    private void LoadOrGenerateEmulatorMap()
    {
        if (!FileAccess.FileExists(EmulatorMapPath))
        {
            GenerateDefaultEmulatorMap();
        }

        try
        {
            string jsonString = FileAccess.GetFileAsString(EmulatorMapPath);
            _emulatorMap = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            GD.Print("Loaded emulator map from user directory.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load emulator map: {e.Message}");
        }
    }

    private void GenerateDefaultEmulatorMap()
    {
        var defaultMap = new Dictionary<string, string>
        {
            { "nes,fds", "mesen" },
            { "snes,sfam", "snes9x" },
            { "n64", "rmg" },
            { "ngc,wii", "dolphin" },
            { "gb,gbc,gba", "mgba" },
            { "nds", "melonDS" },
            { "psx", "duckstation" },
            { "ps2", "pcsx2" },
            { "psp", "ppsspp" },
            { "genesis,megadrive,sega32x,segacd", "picodrive" }
        };

        try
        {
            string jsonString = JsonSerializer.Serialize(defaultMap, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(EmulatorMapPath, FileAccess.ModeFlags.Write);
            file.StoreString(jsonString);
            GD.Print("Generated default emulator map.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to generate default emulator map: {e.Message}");
        }
    }

    public string GetMappedEmulator(string systemSlug)
    {
        if (string.IsNullOrEmpty(systemSlug)) return null;

        foreach (var kvp in _emulatorMap)
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
        return null;
    }

    public bool IsEmulatorInstalled(string emulatorName)
    {
        if (string.IsNullOrEmpty(emulatorName)) return false;

        string metaPath = InstallScriptsBasePath.PathJoin(emulatorName).PathJoin("meta.json");
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
            
            string installDir = ProjectSettings.GlobalizePath(EmulatorsBasePath.PathJoin(meta.EmulatorDirName[osName]));
            string executableRelativePath = meta.ExecutableName[osName];
            string fullExecutablePath = Path.Combine(installDir, executableRelativePath);
            
            return System.IO.File.Exists(fullExecutablePath);
        }
        catch (Exception e)
        {
             GD.PrintErr($"Error checking if emulator is installed: {e.Message}");
             return false;
        }
    }

    public async Task InstallEmulator(string emulatorName)
    {
        string emulatorScriptDir = InstallScriptsBasePath.PathJoin(emulatorName);
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

        await RunInstallScript(scriptPath, EmulatorsBasePath, osName);
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

        string metaPath = InstallScriptsBasePath.PathJoin(mappedEmulator).PathJoin("meta.json");
        
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

            string installDir = ProjectSettings.GlobalizePath(EmulatorsBasePath.PathJoin(meta.EmulatorDirName[osName]));
            string executableRelativePath = meta.ExecutableName[osName];
            string fullExecutablePath = Path.Combine(installDir, executableRelativePath);
            
            string romPath = ProjectSettings.GlobalizePath(game.Path);
            string arguments = meta.LaunchArgs.Replace("{rom_path}", $"\"{romPath}\"");

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
}

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
