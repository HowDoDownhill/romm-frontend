using Godot;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

public static class DiscreteGpuPreference
{
    private const string WindowsGpuPreferenceKeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string WindowsHighPerformanceValue = "GpuPreference=2;";

    private const string OffloadAttemptedVariable = "ROMM_GPU_OFFLOAD_ATTEMPTED";

    private static readonly string[] IntegratedAdapterMarkers = { "Intel", "llvmpipe", "softpipe", "swrast" };

    private static bool IsRenderingOnIntegratedAdapter()
    {
        string adapterName = RenderingServer.GetVideoAdapterName() ?? "";

        foreach (string marker in IntegratedAdapterMarkers)
        {
            if (adapterName.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOffloadableDiscreteGpu()
    {
        return System.IO.Directory.Exists("/proc/driver/nvidia")
            || System.IO.File.Exists("/usr/share/vulkan/icd.d/nvidia_icd.json");
    }

    public static bool ShouldRelaunchOnDiscreteGpu(bool preferDiscreteGpu)
    {
        if (!preferDiscreteGpu || OS.GetName().ToLower() != "linux")
        {
            return false;
        }

        if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(OffloadAttemptedVariable))
            || !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("__NV_PRIME_RENDER_OFFLOAD")))
        {
            return false;
        }

        return IsRenderingOnIntegratedAdapter() && HasOffloadableDiscreteGpu();
    }

    public static bool RelaunchOnDiscreteGpu()
    {
        try
        {
            var relaunchStartInfo = new ProcessStartInfo
            {
                FileName = OS.GetExecutablePath(),
                UseShellExecute = false,
                WorkingDirectory = System.IO.Directory.GetCurrentDirectory()
            };

            foreach (string commandLineArgument in OS.GetCmdlineArgs())
            {
                relaunchStartInfo.ArgumentList.Add(commandLineArgument);
            }

            ApplyToProcess(relaunchStartInfo, true);
            relaunchStartInfo.Environment[OffloadAttemptedVariable] = "1";

            Process.Start(relaunchStartInfo);
            GD.Print($"Relaunching on the discrete GPU (was {RenderingServer.GetVideoAdapterName()}).");

            return true;
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Could not relaunch on the discrete GPU: {exception.Message}");
            return false;
        }
    }

    public static void ApplyToProcess(ProcessStartInfo processStartInfo, bool preferDiscreteGpu)
    {
        if (processStartInfo == null || !preferDiscreteGpu || OS.GetName().ToLower() == "windows")
        {
            return;
        }

        processStartInfo.Environment["__NV_PRIME_RENDER_OFFLOAD"] = "1";
        processStartInfo.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
        processStartInfo.Environment["__VK_LAYER_NV_optimus"] = "NVIDIA_only";
        processStartInfo.Environment["DRI_PRIME"] = "1";
    }

    public static void RegisterWindowsGpuPreference(string executablePath, bool preferDiscreteGpu)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(executablePath))
        {
            return;
        }

        WriteWindowsGpuPreference(executablePath, preferDiscreteGpu);
    }

    [SupportedOSPlatform("windows")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteWindowsGpuPreference(string executablePath, bool preferDiscreteGpu)
    {
        try
        {
            string fullExecutablePath = System.IO.Path.GetFullPath(executablePath);

            if (!System.IO.File.Exists(fullExecutablePath))
            {
                return;
            }

            using var gpuPreferenceKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(WindowsGpuPreferenceKeyPath);

            if (gpuPreferenceKey == null)
            {
                return;
            }

            string existingPreference = gpuPreferenceKey.GetValue(fullExecutablePath) as string;

            if (!preferDiscreteGpu)
            {
                if (existingPreference == WindowsHighPerformanceValue)
                {
                    gpuPreferenceKey.DeleteValue(fullExecutablePath, false);
                }

                return;
            }

            if (existingPreference != WindowsHighPerformanceValue)
            {
                gpuPreferenceKey.SetValue(fullExecutablePath, WindowsHighPerformanceValue, Microsoft.Win32.RegistryValueKind.String);
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Could not register GPU preference for {executablePath}: {exception.Message}");
        }
    }
}
