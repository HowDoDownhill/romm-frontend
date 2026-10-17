using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class SdlProbeDevice
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("instance")] public long Instance { get; set; }
    [JsonPropertyName("player")] public int PlayerIndex { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("vendor")] public int Vendor { get; set; }
    [JsonPropertyName("product")] public int Product { get; set; }
    [JsonPropertyName("guid")] public string Guid { get; set; }
    [JsonPropertyName("gamepad")] public bool IsGamepad { get; set; }
}

public class SdlProbeXInputSlot
{
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("vendor")] public int Vendor { get; set; }
    [JsonPropertyName("product")] public int Product { get; set; }
}

public class SdlProbeResult
{
    [JsonPropertyName("sdl")] public string SdlVersion { get; set; }
    [JsonPropertyName("hooked")] public bool SteamHooked { get; set; }
    [JsonPropertyName("devices")] public List<SdlProbeDevice> Devices { get; set; } = new List<SdlProbeDevice>();
    [JsonPropertyName("all_devices")] public List<SdlProbeDevice> AllDevices { get; set; } = new List<SdlProbeDevice>();
    [JsonPropertyName("xinput")] public List<SdlProbeXInputSlot> XInputSlots { get; set; } = new List<SdlProbeXInputSlot>();
    [JsonPropertyName("error")] public string Error { get; set; }
}

public static class SdlEnumerationProbe
{
    private const int ProbeTimeoutMilliseconds = 30000;

    private const string ProbeScript = """
$ErrorActionPreference = 'Stop'
$result = [ordered]@{ sdl = ''; hooked = $false; devices = @(); all_devices = @(); xinput = @(); error = $null }
try {
    $result.hooked = @([System.Diagnostics.Process]::GetCurrentProcess().Modules | Where-Object { $_.ModuleName -like 'gameoverlayrenderer*' }).Count -gt 0
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)] public struct ProbeGuid { public ulong Low; public ulong High; }
public static class ProbeNative {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr LoadLibraryW(string path);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] public static extern uint XInputGetState(uint slot, byte[] state);
    [DllImport("xinput1_4.dll", EntryPoint = "#108")] public static extern uint XInputGetCapabilitiesEx(uint reserved, uint slot, uint flags, byte[] capabilities);
}
public static class Sdl3 {
    [DllImport("SDL3.dll")] [return: MarshalAs(UnmanagedType.I1)] public static extern bool SDL_Init(uint flags);
    [DllImport("SDL3.dll")] public static extern void SDL_PumpEvents();
    [DllImport("SDL3.dll")] public static extern int SDL_GetVersion();
    [DllImport("SDL3.dll")] public static extern IntPtr SDL_GetJoysticks(out int count);
    [DllImport("SDL3.dll")] public static extern IntPtr SDL_GetJoystickNameForID(uint id);
    [DllImport("SDL3.dll")] public static extern ushort SDL_GetJoystickVendorForID(uint id);
    [DllImport("SDL3.dll")] public static extern ushort SDL_GetJoystickProductForID(uint id);
    [DllImport("SDL3.dll")] public static extern int SDL_GetJoystickPlayerIndexForID(uint id);
    [DllImport("SDL3.dll")] public static extern ProbeGuid SDL_GetJoystickGUIDForID(uint id);
    [DllImport("SDL3.dll")] [return: MarshalAs(UnmanagedType.I1)] public static extern bool SDL_IsGamepad(uint id);
    [DllImport("SDL3.dll")] public static extern void SDL_Quit();
    [DllImport("SDL3.dll")] [return: MarshalAs(UnmanagedType.I1)] public static extern bool SDL_SetHintWithPriority(string name, string value, int priority);
}
public static class Sdl2 {
    [DllImport("SDL2.dll")] public static extern int SDL_Init(uint flags);
    [DllImport("SDL2.dll")] public static extern void SDL_PumpEvents();
    [DllImport("SDL2.dll")] public static extern void SDL_GetVersion(byte[] version);
    [DllImport("SDL2.dll")] public static extern int SDL_NumJoysticks();
    [DllImport("SDL2.dll")] public static extern IntPtr SDL_JoystickNameForIndex(int index);
    [DllImport("SDL2.dll")] public static extern ushort SDL_JoystickGetDeviceVendor(int index);
    [DllImport("SDL2.dll")] public static extern ushort SDL_JoystickGetDeviceProduct(int index);
    [DllImport("SDL2.dll")] public static extern int SDL_JoystickGetDevicePlayerIndex(int index);
    [DllImport("SDL2.dll")] public static extern int SDL_JoystickGetDeviceInstanceID(int index);
    [DllImport("SDL2.dll")] public static extern ProbeGuid SDL_JoystickGetDeviceGUID(int index);
    [DllImport("SDL2.dll")] public static extern int SDL_IsGameController(int index);
    [DllImport("SDL2.dll")] public static extern void SDL_Quit();
    [DllImport("SDL2.dll")] public static extern int SDL_SetHintWithPriority(string name, string value, int priority);
}
'@
    function Format-Guid([ProbeGuid]$guid) {
        $bytes = [BitConverter]::GetBytes($guid.Low) + [BitConverter]::GetBytes($guid.High)
        ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
    }
    function Read-Utf8([IntPtr]$pointer) {
        if ($pointer -eq [IntPtr]::Zero) { return '' }
        $length = 0
        while ([System.Runtime.InteropServices.Marshal]::ReadByte($pointer, $length) -ne 0) { $length++ }
        $buffer = New-Object byte[] $length
        [System.Runtime.InteropServices.Marshal]::Copy($pointer, $buffer, 0, $length)
        [System.Text.Encoding]::UTF8.GetString($buffer)
    }
    for ($slot = 0; $slot -lt 4; $slot++) {
        if ([ProbeNative]::XInputGetState($slot, (New-Object byte[] 16)) -eq 0) {
            $capabilities = New-Object byte[] 32
            $vendor = 0; $product = 0
            if ([ProbeNative]::XInputGetCapabilitiesEx(1, $slot, 0, $capabilities) -eq 0) {
                $vendor = [BitConverter]::ToUInt16($capabilities, 20); $product = [BitConverter]::ToUInt16($capabilities, 22)
            }
            $result.xinput += [ordered]@{ slot = $slot; vendor = [int]$vendor; product = [int]$product }
        }
    }
    if ($SdlPath) {
        if ([ProbeNative]::LoadLibraryW($SdlPath) -eq [IntPtr]::Zero) { throw "could not load $SdlPath" }
        $gamepadSubsystem = 0x2000
        $isSdl3 = [System.IO.Path]::GetFileName($SdlPath) -ieq 'SDL3.dll'
        function Read-SdlDevices {
            $devices = @()
            if ($isSdl3) {
                if (-not [Sdl3]::SDL_Init($gamepadSubsystem)) { throw 'SDL_Init failed' }
                Start-Sleep -Milliseconds $SettleMilliseconds
                [Sdl3]::SDL_PumpEvents()
                $count = 0
                $ids = [Sdl3]::SDL_GetJoysticks([ref]$count)
                for ($index = 0; $index -lt $count; $index++) {
                    $id = [uint32][System.Runtime.InteropServices.Marshal]::ReadInt32($ids, $index * 4)
                    $devices += [ordered]@{ index = $index; instance = [long]$id; player = [Sdl3]::SDL_GetJoystickPlayerIndexForID($id); name = (Read-Utf8 ([Sdl3]::SDL_GetJoystickNameForID($id))); vendor = [int][Sdl3]::SDL_GetJoystickVendorForID($id); product = [int][Sdl3]::SDL_GetJoystickProductForID($id); guid = (Format-Guid ([Sdl3]::SDL_GetJoystickGUIDForID($id))); gamepad = [Sdl3]::SDL_IsGamepad($id) }
                }
                [Sdl3]::SDL_Quit()
            } else {
                if ([Sdl2]::SDL_Init($gamepadSubsystem) -ne 0) { throw 'SDL_Init failed' }
                Start-Sleep -Milliseconds $SettleMilliseconds
                [Sdl2]::SDL_PumpEvents()
                $count = [Sdl2]::SDL_NumJoysticks()
                for ($index = 0; $index -lt $count; $index++) {
                    $devices += [ordered]@{ index = $index; instance = [long][Sdl2]::SDL_JoystickGetDeviceInstanceID($index); player = [Sdl2]::SDL_JoystickGetDevicePlayerIndex($index); name = (Read-Utf8 ([Sdl2]::SDL_JoystickNameForIndex($index))); vendor = [int][Sdl2]::SDL_JoystickGetDeviceVendor($index); product = [int][Sdl2]::SDL_JoystickGetDeviceProduct($index); guid = (Format-Guid ([Sdl2]::SDL_JoystickGetDeviceGUID($index))); gamepad = ([Sdl2]::SDL_IsGameController($index) -ne 0) }
                }
                [Sdl2]::SDL_Quit()
            }
            ,$devices
        }
        if ($isSdl3) {
            $version = [Sdl3]::SDL_GetVersion()
            $result.sdl = '{0}.{1}.{2}' -f [math]::Floor($version / 1000000), ([math]::Floor($version / 1000) % 1000), ($version % 1000)
        } else {
            $version = New-Object byte[] 3
            [Sdl2]::SDL_GetVersion($version)
            $result.sdl = '{0}.{1}.{2}' -f $version[0], $version[1], $version[2]
        }
        $result.devices = Read-SdlDevices
        foreach ($hint in @('SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT', 'SDL_GAMECONTROLLER_IGNORE_DEVICES')) {
            if ($isSdl3) { [void][Sdl3]::SDL_SetHintWithPriority($hint, '', 2) } else { [void][Sdl2]::SDL_SetHintWithPriority($hint, '', 2) }
        }
        $result.all_devices = Read-SdlDevices
    }
} catch {
    $result.error = $_.Exception.Message
}
[Console]::Out.Write((ConvertTo-Json $result -Depth 4 -Compress))
""";

    public static async Task<SdlProbeResult> RunAsync(ProcessStartInfo launchStartInfo, string sdlLibraryPath, int settleMilliseconds)
    {
        string script = $"$SdlPath = '{(sdlLibraryPath ?? "").Replace("'", "''")}'\n$SettleMilliseconds = {settleMilliseconds}\n" + ProbeScript;

        launchStartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        launchStartInfo.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        launchStartInfo.RedirectStandardOutput = true;
        launchStartInfo.RedirectStandardError = true;
        launchStartInfo.UseShellExecute = false;
        launchStartInfo.CreateNoWindow = true;

        try
        {
            using Process probeProcess = Process.Start(launchStartInfo);
            Task<string> output = probeProcess.StandardOutput.ReadToEndAsync();
            Task<string> errors = probeProcess.StandardError.ReadToEndAsync();

            if (await Task.WhenAny(output, Task.Delay(ProbeTimeoutMilliseconds)) != output)
            {
                probeProcess.Kill();
                return new SdlProbeResult { Error = "the probe did not finish within 30 seconds" };
            }

            string json = (await output).Trim();

            if (string.IsNullOrEmpty(json))
            {
                return new SdlProbeResult { Error = "the probe printed nothing: " + (await errors).Trim() };
            }

            return JsonSerializer.Deserialize<SdlProbeResult>(json) ?? new SdlProbeResult { Error = "the probe output was empty" };
        }
        catch (Exception exception)
        {
            return new SdlProbeResult { Error = exception.Message };
        }
    }
}
