using System.Collections.Generic;

public static class SdlDeviceAllowlist
{
    public const string AllowlistEnvironmentVariable = "SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT";

    public static string BuildAllowedDeviceList()
    {
        return $"0x{ViGEmPadBackend.VirtualPadVendorId:X4}/0x{ViGEmPadBackend.VirtualPadProductId:X4}";
    }

    public static Dictionary<string, string> BuildEnvironment()
    {
        return new Dictionary<string, string>
        {
            { AllowlistEnvironmentVariable, BuildAllowedDeviceList() }
        };
    }
}
