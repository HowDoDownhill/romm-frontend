using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

public class HidHideGamingDevice
{
    [JsonPropertyName("present")]
    public bool Present { get; set; }

    [JsonPropertyName("gamingDevice")]
    public bool GamingDevice { get; set; }

    [JsonPropertyName("deviceInstancePath")]
    public string DeviceInstancePath { get; set; }

    [JsonPropertyName("product")]
    public string Product { get; set; }

    [JsonPropertyName("usage")]
    public string Usage { get; set; }
}

public class HidHideDeviceContainer
{
    [JsonPropertyName("friendlyName")]
    public string FriendlyName { get; set; }

    [JsonPropertyName("devices")]
    public List<HidHideGamingDevice> Devices { get; set; }
}

public class HidHideDeviceHider : IDeviceHider
{
    private const string CloakEnabledResponse = "--cloak-on";
    private const int CommandTimeoutMilliseconds = 15000;

    private readonly ConfigManager configManager;
    private string resolvedCommandLineToolPath;

    public HidHideDeviceHider(ConfigManager configManager)
    {
        this.configManager = configManager;
    }

    public bool IsAvailable => ResolveCommandLineToolPath() != null;

    public string UnavailableReason => IsAvailable
        ? ""
        : "HidHide is not installed, so physical controllers stay visible to emulators";

    private string ResolveCommandLineToolPath()
    {
        if (resolvedCommandLineToolPath != null)
        {
            return resolvedCommandLineToolPath;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (string candidatePath in BuildCommandLineToolCandidatePaths())
        {
            if (File.Exists(candidatePath))
            {
                resolvedCommandLineToolPath = candidatePath;
                return resolvedCommandLineToolPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildCommandLineToolCandidatePaths()
    {
        foreach (string programFilesVariable in new[] { "ProgramFiles", "ProgramW6432", "ProgramFiles(x86)" })
        {
            string programFilesPath = System.Environment.GetEnvironmentVariable(programFilesVariable);

            if (string.IsNullOrEmpty(programFilesPath))
            {
                continue;
            }

            string installDirectory = Path.Combine(programFilesPath, "Nefarius Software Solutions", "HidHide");

            yield return Path.Combine(installDirectory, "x64", "HidHideCLI.exe");
            yield return Path.Combine(installDirectory, "HidHideCLI.exe");
        }
    }

    public bool HidePhysicalPads(IReadOnlyList<ConnectedController> physicalPads)
    {
        if (!IsAvailable)
        {
            return false;
        }

        List<string> devicePathsToHide = ReadPresentGamingDevicePaths();

        if (devicePathsToHide.Count == 0)
        {
            GD.Print("[InputLayer] HidHide reported no present gaming devices; nothing to hide.");
            return false;
        }

        bool cloakWasAlreadyEnabled = IsCloakEnabled();
        var hiddenDevicePaths = new Godot.Collections.Array();

        foreach (string devicePath in devicePathsToHide)
        {
            if (RunCommandLineTool("--dev-hide", devicePath).ExitCode != 0)
            {
                GD.PrintErr($"[InputLayer] HidHide refused to hide {devicePath}.");
                continue;
            }

            hiddenDevicePaths.Add(devicePath);
        }

        RunCommandLineTool("--app-reg", OS.GetExecutablePath());

        if (!cloakWasAlreadyEnabled)
        {
            RunCommandLineTool("--cloak-on");
        }

        configManager.SaveHiddenInputDevices(hiddenDevicePaths, !cloakWasAlreadyEnabled);
        GD.Print($"[InputLayer] hid {hiddenDevicePaths.Count} physical controller(s) from other applications.");
        return hiddenDevicePaths.Count > 0;
    }

    private List<string> ReadPresentGamingDevicePaths()
    {
        var devicePaths = new List<string>();
        CommandLineToolResult enumerationResult = RunCommandLineTool("--dev-gaming");

        if (enumerationResult.ExitCode != 0 || string.IsNullOrWhiteSpace(enumerationResult.StandardOutput))
        {
            GD.PrintErr("[InputLayer] HidHide could not enumerate gaming devices.");
            return devicePaths;
        }

        List<HidHideDeviceContainer> deviceContainers;

        try
        {
            deviceContainers = JsonSerializer.Deserialize<List<HidHideDeviceContainer>>(enumerationResult.StandardOutput);
        }
        catch (JsonException parsingFailure)
        {
            GD.PrintErr($"[InputLayer] HidHide device listing could not be parsed: {parsingFailure.Message}");
            return devicePaths;
        }

        foreach (HidHideDeviceContainer deviceContainer in deviceContainers ?? new List<HidHideDeviceContainer>())
        {
            foreach (HidHideGamingDevice gamingDevice in deviceContainer.Devices ?? new List<HidHideGamingDevice>())
            {
                if (gamingDevice.Present && gamingDevice.GamingDevice && !string.IsNullOrEmpty(gamingDevice.DeviceInstancePath))
                {
                    devicePaths.Add(gamingDevice.DeviceInstancePath);
                }
            }
        }

        return devicePaths;
    }

    private bool IsCloakEnabled()
    {
        return RunCommandLineTool("--cloak-state").StandardOutput.Contains(CloakEnabledResponse);
    }

    public void UnhideAll()
    {
        if (!IsAvailable)
        {
            return;
        }

        var hiddenDevicePaths = configManager.HiddenInputDevicePaths;

        if (hiddenDevicePaths == null || hiddenDevicePaths.Count == 0)
        {
            return;
        }

        foreach (var hiddenDevicePath in hiddenDevicePaths)
        {
            RunCommandLineTool("--dev-unhide", hiddenDevicePath.AsString());
        }

        RunCommandLineTool("--app-unreg", OS.GetExecutablePath());

        if (configManager.HidHideCloakEnabledByFrontend)
        {
            RunCommandLineTool("--cloak-off");
        }

        GD.Print($"[InputLayer] restored visibility of {hiddenDevicePaths.Count} controller(s).");
        configManager.SaveHiddenInputDevices(new Godot.Collections.Array(), false);
    }

    private struct CommandLineToolResult
    {
        public int ExitCode;
        public string StandardOutput;
    }

    private CommandLineToolResult RunCommandLineTool(params string[] arguments)
    {
        string commandLineToolPath = ResolveCommandLineToolPath();

        if (commandLineToolPath == null)
        {
            return new CommandLineToolResult { ExitCode = -1, StandardOutput = "" };
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = commandLineToolPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process commandLineToolProcess = Process.Start(processStartInfo);
            string standardOutput = commandLineToolProcess.StandardOutput.ReadToEnd();
            commandLineToolProcess.WaitForExit(CommandTimeoutMilliseconds);

            return new CommandLineToolResult
            {
                ExitCode = commandLineToolProcess.HasExited ? commandLineToolProcess.ExitCode : -1,
                StandardOutput = standardOutput
            };
        }
        catch (Exception invocationFailure)
        {
            GD.PrintErr($"[InputLayer] HidHideCLI {string.Join(' ', arguments)} failed: {invocationFailure.Message}");
            return new CommandLineToolResult { ExitCode = -1, StandardOutput = "" };
        }
    }
}
