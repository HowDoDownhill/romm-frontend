using System.Collections.Generic;
using System.Linq;

public static class SdlDeviceAllowlist
{
    public const string AllowlistEnvironmentVariable = "SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT";

    public static string BuildAllowedDeviceList(IEnumerable<(ushort Vendor, ushort Product)> allowedDevices)
    {
        return string.Join(",", allowedDevices.Select(device => $"0x{device.Vendor:X4}/0x{device.Product:X4}"));
    }

    public static Dictionary<string, string> BuildEnvironment(IEnumerable<(ushort Vendor, ushort Product)> allowedDevices)
    {
        return new Dictionary<string, string>
        {
            { AllowlistEnvironmentVariable, BuildAllowedDeviceList(allowedDevices) }
        };
    }
}
