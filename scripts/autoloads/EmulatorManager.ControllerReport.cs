using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class EmulatorManager
{
    private const int ControllerProbeSettleMilliseconds = 1500;
    private const ushort SteamVirtualPadVendorId = 0x28DE;
    private const ushort SteamVirtualPadProductId = 0x11FF;

    private enum DeviceReferenceKind
    {
        None,
        XInputSlot,
        PlayerIndex,
        ProbedDevice
    }

    public async Task<string> WriteControllerReport()
    {
        var report = new StringBuilder();
        InputLayer inputLayer = appInstance.inputLayer;
        ControllerManager controllerManager = GetNode<ControllerManager>("/root/ControllerManager");
        string currentOperatingSystem = OS.GetName().ToLower();

        report.AppendLine("# Controller report");
        report.AppendLine();
        report.AppendLine($"- Written: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"- App: {AppUpdater.CurrentVersion} on {OS.GetName()} {OS.GetVersion()}");
        report.AppendLine($"- Steam: {DescribeSteamLaunch()}");
        report.AppendLine($"- Automatic controller mapping: {inputLayer?.ResolveConsent().ToString() ?? "unavailable"}; virtual pads {(inputLayer?.IsVirtualPadBackendAvailable == true ? "available" : "unavailable: " + inputLayer?.VirtualPadBackendUnavailableReason)}");
        report.AppendLine();

        AppendGodotPads(report, controllerManager, inputLayer);
        AppendXInputSlots(report, "XInput slots seen by the app", XInputSlots.ReadSlotIdentities().Select(slot => (slot.Slot, (int)slot.Vendor, (int)slot.Product)));

        foreach ((string emulatorName, string systemSlug) in ResolveInstalledEmulatorsForReport())
        {
            await AppendEmulatorReport(report, emulatorName, systemSlug, currentOperatingSystem, inputLayer, controllerManager);
        }

        string reportDirectory = ProjectSettings.GlobalizePath("user://logs");
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, $"controller-report-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(reportPath, report.ToString());
        GD.Print($"[Controllers] controller report written to {reportPath}");
        return reportPath;
    }

    private static string DescribeSteamLaunch()
    {
        string steamAppId = System.Environment.GetEnvironmentVariable("SteamAppId");
        bool hooked = OperatingSystem.IsWindows() && System.Diagnostics.Process.GetCurrentProcess().Modules
            .Cast<System.Diagnostics.ProcessModule>()
            .Any(module => module.ModuleName.StartsWith("gameoverlayrenderer", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(steamAppId))
        {
            return hooked ? "overlay loaded, but no SteamAppId" : "not launched by Steam";
        }

        return $"launched by Steam (app {steamAppId}), overlay {(hooked ? "loaded" : "not loaded")}, virtual gamepads {(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SDL_GAMECONTROLLER_ALLOW_STEAM_VIRTUAL_GAMEPAD")) ? "not offered" : "offered")}";
    }

    private static void AppendGodotPads(StringBuilder report, ControllerManager controllerManager, InputLayer inputLayer)
    {
        report.AppendLine("## Pads seen by the app");
        report.AppendLine();
        report.AppendLine("| Device | Name | Bus | VID/PID | Backend | XInput slot | Role |");
        report.AppendLine("|---|---|---|---|---|---|---|");

        foreach (ConnectedController controller in controllerManager.GetAllControllersIncludingVirtual())
        {
            (string bus, int vendor, int product, string backend) = DecodeSdlGuid(controller.Guid);
            string role = inputLayer != null && inputLayer.IsOwnVirtualDevice(controller.GodotDeviceId) ? "our virtual pad" : DescribePhysicalIdentity(vendor, product);
            int xinputSlot = InputLayer.ReadXInputSlot(controller.GodotDeviceId);
            report.AppendLine($"| {controller.GodotDeviceId} | {controller.ControllerName} | {bus} | {vendor:X4}/{product:X4} | {backend} | {(xinputSlot >= 0 ? xinputSlot.ToString() : "none")} | {role} |");
        }

        report.AppendLine();
    }

    private static string DescribePhysicalIdentity(int vendor, int product)
    {
        if (vendor == SteamVirtualPadVendorId && product == SteamVirtualPadProductId)
        {
            return "Steam Input virtual pad";
        }

        if (vendor == ViGEmPadBackend.VirtualPadVendorId && product == ViGEmPadBackend.VirtualPadProductId)
        {
            return "physical, but shares our virtual pad's identity";
        }

        return "physical";
    }

    private static (string Bus, int Vendor, int Product, string Backend) DecodeSdlGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid) || guid.Length < 32)
        {
            return ("?", 0, 0, "?");
        }

        int ReadWord(int byteOffset) => Convert.ToInt32(guid.Substring(byteOffset * 2 + 2, 2) + guid.Substring(byteOffset * 2, 2), 16);
        int busCode = ReadWord(0);
        string bus = busCode switch { 0x03 => "USB", 0x05 => "Bluetooth", 0x00 => "unknown", _ => $"0x{busCode:X2}" };
        char backendCode = (char)Convert.ToInt32(guid.Substring(28, 2), 16);
        string backend = backendCode switch { 'x' => "XInput", 'r' => "RawInput", 'w' => "WGI", 'h' => "HIDAPI", 'v' => "virtual", _ => "other" };
        return (bus, ReadWord(4), ReadWord(8), backend);
    }

    private static void AppendXInputSlots(StringBuilder report, string heading, IEnumerable<(int Slot, int Vendor, int Product)> slots)
    {
        var slotList = slots.ToList();
        report.AppendLine($"**{heading}:** {(slotList.Count == 0 ? "none" : string.Join(", ", slotList.Select(slot => $"{slot.Slot} = {slot.Vendor:X4}/{slot.Product:X4}")))}");
        report.AppendLine();
    }

    private List<(string EmulatorName, string SystemSlug)> ResolveInstalledEmulatorsForReport()
    {
        var emulators = new List<(string, string)>();

        foreach (var mapping in systemToEmulatorMap)
        {
            foreach (string emulatorName in mapping.Value)
            {
                if (!emulators.Any(entry => entry.Item1 == emulatorName) && IsEmulatorInstalled(emulatorName))
                {
                    emulators.Add((emulatorName, mapping.Key));
                }
            }
        }

        return emulators;
    }

    private async Task AppendEmulatorReport(StringBuilder report, string emulatorName, string systemSlug, string currentOperatingSystem, InputLayer inputLayer, ControllerManager controllerManager)
    {
        report.AppendLine($"## {emulatorName} ({systemSlug})");
        report.AppendLine();

        EmulatorMeta emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);

        if (emulatorMetadata?.EmulatorDirName == null || !emulatorMetadata.EmulatorDirName.TryGetValue(currentOperatingSystem, out string emulatorDirectoryName))
        {
            report.AppendLine("No install for this OS.");
            report.AppendLine();
            return;
        }

        string emulatorInstallDirectory = appInstance.configManager.EmulatorsPath + emulatorDirectoryName;
        string executablePath = ResolveExecutablePath(emulatorMetadata, currentOperatingSystem, emulatorInstallDirectory);
        bool sessionStarted = inputLayer?.BeginSession(systemSlug, emulatorMetadata) ?? false;

        try
        {
            if (sessionStarted)
            {
                await inputLayer.WaitForVirtualPadsToEnumerate();
                AppendSessionPlayers(report, inputLayer);
            }

            else
            {
                report.AppendLine($"Mapping session: not started ({inputLayer?.LastSessionFailureReason ?? "no input layer"}).");
                report.AppendLine();
            }

            SdlProbeResult probe = null;
            EmulatorDeviceView deviceView = null;

            if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(executablePath))
            {
                string sdlLibraryPath = FindEmulatorSdlLibrary(emulatorInstallDirectory, out string sdlSource);
                probe = await SdlEnumerationProbe.RunAsync(BuildEmulatorStartInfo(executablePath, "", emulatorInstallDirectory, emulatorMetadata), sdlLibraryPath, ControllerProbeSettleMilliseconds);
                AppendProbeResult(report, probe, sdlSource, emulatorMetadata.ControllerConfig);

                if (sessionStarted && string.IsNullOrEmpty(probe.Error) && !string.IsNullOrEmpty(probe.SdlVersion) && emulatorMetadata.ControllerConfig != null)
                {
                    deviceView = BuildDeviceView(probe, emulatorMetadata.ControllerConfig, inputLayer);
                }
            }

            if (sessionStarted)
            {
                AppendDeviceReferenceChecks(report, emulatorMetadata.ControllerConfig, inputLayer, probe, deviceView);
            }
        }
        finally
        {
            if (sessionStarted)
            {
                inputLayer.EndSession();
            }
        }
    }

    private static void AppendSessionPlayers(StringBuilder report, InputLayer inputLayer)
    {
        report.AppendLine("| Player | Device | Name | VID/PID | Route | XInput slot |");
        report.AppendLine("|---|---|---|---|---|---|");

        for (int playerIndex = 0; playerIndex < inputLayer.ActivePlayerCount; playerIndex++)
        {
            SessionPlayer player = inputLayer.SessionPlayers[playerIndex];
            (ushort vendor, ushort product) = inputLayer.ResolvePlayerIdentity(playerIndex);
            int xinputSlot = inputLayer.ResolvePlayerXInputSlot(playerIndex);
            report.AppendLine($"| {playerIndex + 1} | {player.PhysicalDeviceId} | {Input.GetJoyName(player.PhysicalDeviceId)} | {vendor:X4}/{product:X4} | {(player.IsPassthrough ? "passthrough" : "virtual pad")} | {(xinputSlot >= 0 ? xinputSlot.ToString() : "none")} |");
        }

        report.AppendLine();
    }

    private string FindEmulatorSdlLibrary(string emulatorInstallDirectory, out string source)
    {
        foreach (string libraryName in new[] { "SDL3.dll", "SDL2.dll" })
        {
            string ownLibrary = Directory.Exists(emulatorInstallDirectory)
                ? Directory.EnumerateFiles(emulatorInstallDirectory, libraryName, SearchOption.AllDirectories).FirstOrDefault()
                : null;

            if (ownLibrary != null)
            {
                source = "its own " + libraryName;
                return ownLibrary;
            }
        }

        string fallbackLibrary = Directory.Exists(appInstance.configManager.EmulatorsPath)
            ? Directory.EnumerateFiles(appInstance.configManager.EmulatorsPath, "SDL3.dll", SearchOption.AllDirectories).FirstOrDefault()
            : null;

        source = fallbackLibrary != null
            ? $"another emulator's SDL3.dll ({Path.GetFileName(Path.GetDirectoryName(fallbackLibrary))}), since this one links SDL statically"
            : "no SDL library found; XInput only";
        return fallbackLibrary;
    }

    private static void AppendProbeResult(StringBuilder report, SdlProbeResult probe, string sdlSource, ControllerConfig controllerConfig)
    {
        bool numbersAllDevices = controllerConfig?.SdlEnumeration == "all";

        report.AppendLine($"Probe in this emulator's launch environment, using {sdlSource}{(string.IsNullOrEmpty(probe.SdlVersion) ? "" : $" (SDL {probe.SdlVersion})")}. Steam overlay in the probe: {(probe.SteamHooked ? "loaded" : "not loaded")}.");
        report.AppendLine();

        if (!string.IsNullOrEmpty(probe.Error))
        {
            report.AppendLine($"Probe error: {probe.Error}");
            report.AppendLine();
        }

        AppendXInputSlots(report, "XInput slots seen by the emulator", probe.XInputSlots.Select(slot => (slot.Slot, slot.Vendor, slot.Product)));

        List<SdlProbeDevice> numberedDevices = numbersAllDevices ? probe.AllDevices : probe.Devices;

        if (numberedDevices.Count == 0)
        {
            report.AppendLine($"SDL devices ({(numbersAllDevices ? "all" : "filtered")}): none. Unfiltered count: {probe.AllDevices.Count}.");
            report.AppendLine();
            return;
        }

        report.AppendLine($"SDL devices this emulator numbers from ({(numbersAllDevices ? "all, ignoring the filter" : "after the filter")}; {probe.Devices.Count} filtered, {probe.AllDevices.Count} unfiltered):");
        report.AppendLine();
        report.AppendLine("| Index | Player | Name | VID/PID | Gamepad | GUID |");
        report.AppendLine("|---|---|---|---|---|---|");

        foreach (SdlProbeDevice device in numberedDevices)
        {
            report.AppendLine($"| {device.Index} | {device.PlayerIndex} | {device.Name} | {device.Vendor:X4}/{device.Product:X4} | {(device.IsGamepad ? "yes" : "no")} | {device.Guid} |");
        }

        report.AppendLine();
    }

    private static void AppendDeviceReferenceChecks(StringBuilder report, ControllerConfig controllerConfig, InputLayer inputLayer, SdlProbeResult probe, EmulatorDeviceView deviceView)
    {
        if (controllerConfig?.ControllerSections == null || controllerConfig.ControllerSections.Count == 0)
        {
            report.AppendLine("No device references to check; this emulator's bindings are global.");
            report.AppendLine();
            return;
        }

        report.AppendLine("| Section | Player | We write | Resolves to | Verdict |");
        report.AppendLine("|---|---|---|---|---|");

        foreach (ControllerSection controllerSection in controllerConfig.ControllerSections)
        {
            if (controllerSection.OtherDevices)
            {
                AppendOtherDeviceRows(report, controllerSection, deviceView);
                continue;
            }

            string referenceTemplate = SelectDeviceReferenceTemplate(controllerSection);
            DeviceReferenceKind referenceKind = ClassifyDeviceReference(referenceTemplate);

            if (referenceKind == DeviceReferenceKind.None)
            {
                continue;
            }

            for (int playerIndex = 0; playerIndex < inputLayer.ActivePlayerCount; playerIndex++)
            {
                string portNumber = (controllerSection.PortStart + playerIndex).ToString();
                string sectionName = (controllerSection.SectionTemplate ?? "").Replace("{port}", portNumber);
                string written = ResolveDeviceBindingMacros(referenceTemplate, playerIndex, inputLayer, deviceView).Replace("{port}", portNumber);
                (string resolved, string verdict) = CheckDeviceReference(referenceKind, playerIndex, inputLayer, probe, deviceView);
                report.AppendLine($"| {sectionName} | {playerIndex + 1} | `{(string.IsNullOrEmpty(written) ? "(nothing)" : written)}` | {resolved} | {verdict} |");
            }
        }

        report.AppendLine();
    }

    private static void AppendOtherDeviceRows(StringBuilder report, ControllerSection controllerSection, EmulatorDeviceView deviceView)
    {
        if (deviceView == null)
        {
            return;
        }

        int nextPort = controllerSection.PortStart;

        foreach (SdlProbeDevice otherDevice in deviceView.Devices.Where(candidate => !deviceView.PlayerDevices.Contains(candidate)))
        {
            string deviceKey = DeviceMacroPattern.Replace(controllerSection.DeviceKey, otherDevice.Index.ToString());
            report.AppendLine($"| {controllerSection.SectionTemplate} | - | `{deviceKey} = {controllerSection.DeviceTemplate.Replace("{port}", nextPort.ToString())}` | {otherDevice.Name} {otherDevice.Vendor:X4}/{otherDevice.Product:X4} | OK (not a player) |");
            nextPort++;
        }
    }

    private static string SelectDeviceReferenceTemplate(ControllerSection controllerSection)
    {
        if (!string.IsNullOrEmpty(controllerSection.DeviceBindingTemplate))
        {
            return controllerSection.DeviceBindingTemplate;
        }

        return ClassifyDeviceReference(controllerSection.DeviceKey) != DeviceReferenceKind.None ? controllerSection.DeviceKey : controllerSection.DeviceTemplate;
    }

    private static DeviceReferenceKind ClassifyDeviceReference(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return DeviceReferenceKind.None;
        }

        if (template.Contains("{xinput_index}"))
        {
            return DeviceReferenceKind.XInputSlot;
        }

        if (template.Contains("{sdl_player_index}") || template.Contains("{sdl_index_after_hidden}"))
        {
            return DeviceReferenceKind.PlayerIndex;
        }

        return ProbeDependentMacros.Any(template.Contains) ? DeviceReferenceKind.ProbedDevice : DeviceReferenceKind.None;
    }

    private static (string Resolved, string Verdict) CheckDeviceReference(DeviceReferenceKind referenceKind, int playerIndex, InputLayer inputLayer, SdlProbeResult probe, EmulatorDeviceView deviceView)
    {
        if (probe == null || !string.IsNullOrEmpty(probe.Error))
        {
            return ("not probed", "UNCHECKED");
        }

        (ushort vendor, ushort product) = inputLayer.ResolvePlayerIdentity(playerIndex);
        int xinputSlot = inputLayer.ResolvePlayerXInputSlot(playerIndex);

        if (referenceKind == DeviceReferenceKind.XInputSlot)
        {
            SdlProbeXInputSlot slotSeen = probe.XInputSlots.FirstOrDefault(candidate => candidate.Slot == xinputSlot);
            return slotSeen == null
                ? ($"slot {xinputSlot}: empty", "MISSING")
                : ($"slot {xinputSlot}: {slotSeen.Vendor:X4}/{slotSeen.Product:X4}", JudgeIdentity(slotSeen.Vendor, slotSeen.Product, vendor, product));
        }

        if (string.IsNullOrEmpty(probe.SdlVersion))
        {
            return ("no SDL library to probe with", "UNCHECKED");
        }

        SdlProbeDevice device = referenceKind == DeviceReferenceKind.PlayerIndex
            ? (deviceView?.Devices ?? probe.Devices).FirstOrDefault(candidate => candidate.PlayerIndex == xinputSlot)
            : deviceView != null && playerIndex < deviceView.PlayerDevices.Count ? deviceView.PlayerDevices[playerIndex] : null;

        return device == null
            ? ("no such device", "MISSING")
            : ($"{device.Name} {device.Vendor:X4}/{device.Product:X4}", JudgeIdentity(device.Vendor, device.Product, vendor, product));
    }

    private static string JudgeIdentity(int seenVendor, int seenProduct, ushort expectedVendor, ushort expectedProduct)
    {
        return seenVendor == expectedVendor && seenProduct == expectedProduct ? "OK" : $"WRONG DEVICE (expected {expectedVendor:X4}/{expectedProduct:X4})";
    }
}
