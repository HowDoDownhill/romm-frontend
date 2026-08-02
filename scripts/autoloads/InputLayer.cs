using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum InputLayerConsent
{
    Unasked,
    Accepted,
    Declined
}

public partial class InputLayer : Node
{
    public const int MaximumPlayers = 4;

    private AppInstance appInstance;
    private ControllerManager controllerManager;

    private IPhysicalPadReader physicalPadReader;
    private IVirtualPadBackend virtualPadBackend;
    private IDeviceHider deviceHider;

    private readonly List<int> sessionPhysicalDeviceIds = new List<int>();
    private readonly List<PadMappingTable> sessionMappingTables = new List<PadMappingTable>();
    private readonly List<PadState> sessionPhysicalStates = new List<PadState>();
    private readonly List<PadState> sessionVirtualStates = new List<PadState>();

    private HashSet<int> joypadsPresentBeforeSession = new HashSet<int>();
    private readonly HashSet<int> ownVirtualDeviceIds = new HashSet<int>();

    public bool IsSessionActive { get; private set; }
    public string LastSessionFailureReason { get; private set; } = "";

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.inputLayer = this;
        controllerManager = GetNode<ControllerManager>("/root/ControllerManager");

        physicalPadReader = new GodotPhysicalPadReader();
        virtualPadBackend = new ViGEmPadBackend();
        deviceHider = OperatingSystem.IsWindows()
            ? new HidHideDeviceHider(appInstance.configManager)
            : new NullDeviceHider();

        Input.JoyConnectionChanged += OnJoyConnectionChanged;
        SetProcess(false);

        ClearStaleHidingRulesFromAPreviousRun();
    }

    private void ClearStaleHidingRulesFromAPreviousRun()
    {
        deviceHider.UnhideAll();
    }

    public bool IsVirtualPadBackendAvailable => virtualPadBackend.IsAvailable;
    public string VirtualPadBackendUnavailableReason => virtualPadBackend.UnavailableReason;
    public string VirtualPadSdlDeviceName => virtualPadBackend.SdlDeviceName;
    public int ActivePlayerCount => sessionPhysicalDeviceIds.Count;

    public bool IsOwnVirtualDevice(int godotDeviceId)
    {
        return ownVirtualDeviceIds.Contains(godotDeviceId);
    }

    public IReadOnlyList<int> SessionPhysicalDeviceIds => sessionPhysicalDeviceIds;

    public int ResolvePlayerOneDeviceId()
    {
        if (sessionPhysicalDeviceIds.Count > 0)
        {
            return sessionPhysicalDeviceIds[0];
        }

        List<ConnectedController> availablePads = GetAssignablePhysicalPads();
        return availablePads.Count > 0 ? availablePads[0].GodotDeviceId : -1;
    }

    private List<ConnectedController> GetAssignablePhysicalPads()
    {
        return controllerManager
            .GetConnectedControllers()
            .Where(candidate => !IsOwnVirtualDevice(candidate.GodotDeviceId))
            .ToList();
    }

    public bool BeginSession(string systemSlug, EmulatorMeta emulatorMetadata)
    {
        EndSession();

        if (!ShouldRunLayerForSession())
        {
            return false;
        }

        List<ConnectedController> physicalPads = GetAssignablePhysicalPads();

        if (physicalPads.Count == 0)
        {
            LastSessionFailureReason = "no physical controller is connected";
            GD.Print($"[InputLayer] not starting a session: {LastSessionFailureReason}.");
            return false;
        }

        int playerCount = Math.Min(physicalPads.Count, ResolveMaximumPlayers(emulatorMetadata));

        joypadsPresentBeforeSession = Input.GetConnectedJoypads().Select(deviceId => (int)deviceId).ToHashSet();
        HidePhysicalPadsBeforeCreatingVirtualOnes(physicalPads);

        if (!virtualPadBackend.TryCreatePads(playerCount))
        {
            LastSessionFailureReason = virtualPadBackend.UnavailableReason;
            GD.PrintErr($"[InputLayer] not starting a session: {LastSessionFailureReason}.");
            return false;
        }

        BuildSessionMappings(systemSlug, emulatorMetadata, physicalPads, playerCount);

        IsSessionActive = true;
        LastSessionFailureReason = "";
        SetProcess(true);

        GD.Print($"[InputLayer] session started for '{systemSlug}' with {playerCount} virtual pad(s).");
        return true;
    }

    private void HidePhysicalPadsBeforeCreatingVirtualOnes(List<ConnectedController> physicalPads)
    {
        if (!deviceHider.IsAvailable)
        {
            GD.Print($"[InputLayer] {deviceHider.UnavailableReason}.");
            return;
        }

        deviceHider.HidePhysicalPads(physicalPads);
    }

    private bool ShouldRunLayerForSession()
    {
        if (ResolveConsent() != InputLayerConsent.Accepted)
        {
            LastSessionFailureReason = "automatic controller mapping has not been enabled";
            return false;
        }

        if (!virtualPadBackend.IsAvailable)
        {
            LastSessionFailureReason = virtualPadBackend.UnavailableReason;
            GD.Print($"[InputLayer] not starting a session: {LastSessionFailureReason}.");
            return false;
        }

        return true;
    }

    private int ResolveMaximumPlayers(EmulatorMeta emulatorMetadata)
    {
        int metadataMaximum = emulatorMetadata?.ControllerConfig?.MaxControllers ?? 0;
        return metadataMaximum > 0 ? Math.Min(metadataMaximum, MaximumPlayers) : MaximumPlayers;
    }

    private void BuildSessionMappings(string systemSlug, EmulatorMeta emulatorMetadata, List<ConnectedController> physicalPads, int playerCount)
    {
        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            sessionPhysicalDeviceIds.Add(physicalPads[playerIndex].GodotDeviceId);
            sessionMappingTables.Add(BuildMappingTableForPlayer(systemSlug, emulatorMetadata, playerIndex));
            sessionPhysicalStates.Add(new PadState());
            sessionVirtualStates.Add(new PadState());
        }
    }

    private PadMappingTable BuildMappingTableForPlayer(string systemSlug, EmulatorMeta emulatorMetadata, int playerIndex)
    {
        if (emulatorMetadata?.ControllerConfig == null)
        {
            return PadMappingTable.BuildIdentity();
        }

        Dictionary<string, string> playerMappings = ResolvePlayerMappings(systemSlug, playerIndex);
        return PadMappingTable.Build(emulatorMetadata.ControllerConfig, playerMappings);
    }

    private Dictionary<string, string> ResolvePlayerMappings(string systemSlug, int playerIndex)
    {
        var platformMappings = appInstance?.configManager?.PlatformInputMappings;

        if (platformMappings == null
            || string.IsNullOrEmpty(systemSlug)
            || !platformMappings.TryGetValue(systemSlug, out var mappingsByPlayer)
            || !mappingsByPlayer.TryGetValue(playerIndex, out var playerMappings))
        {
            return null;
        }

        return playerMappings;
    }

    public void EndSession()
    {
        if (!IsSessionActive && sessionPhysicalDeviceIds.Count == 0)
        {
            return;
        }

        SetProcess(false);
        IsSessionActive = false;

        virtualPadBackend.DestroyPads();
        deviceHider.UnhideAll();

        sessionPhysicalDeviceIds.Clear();
        sessionMappingTables.Clear();
        sessionPhysicalStates.Clear();
        sessionVirtualStates.Clear();
        ownVirtualDeviceIds.Clear();
        joypadsPresentBeforeSession.Clear();

        GD.Print("[InputLayer] session ended.");
    }

    public override void _Process(double delta)
    {
        if (!IsSessionActive)
        {
            return;
        }

        for (int playerIndex = 0; playerIndex < sessionPhysicalDeviceIds.Count; playerIndex++)
        {
            PadState physicalState = sessionPhysicalStates[playerIndex];
            PadState virtualState = sessionVirtualStates[playerIndex];

            physicalPadReader.ReadInto(sessionPhysicalDeviceIds[playerIndex], physicalState);
            sessionMappingTables[playerIndex].Apply(physicalState, virtualState);
            virtualPadBackend.Submit(playerIndex, virtualState);
        }
    }

    private void OnJoyConnectionChanged(long deviceId, bool connected)
    {
        int joypadId = (int)deviceId;

        if (!connected)
        {
            ownVirtualDeviceIds.Remove(joypadId);
            return;
        }

        if (virtualPadBackend.CreatedPadCount > 0 && !joypadsPresentBeforeSession.Contains(joypadId))
        {
            ownVirtualDeviceIds.Add(joypadId);
            GD.Print($"[InputLayer] our virtual pad appeared as Godot device {joypadId} \"{Input.GetJoyName(joypadId)}\"; excluding it from input.");
        }
    }

    public Dictionary<string, string> BuildLaunchEnvironment(EmulatorMeta emulatorMetadata)
    {
        if (!IsSessionActive || ResolveInputLayerMode(emulatorMetadata) == "none")
        {
            return new Dictionary<string, string>();
        }

        return SdlDeviceAllowlist.BuildEnvironment();
    }

    private static string ResolveInputLayerMode(EmulatorMeta emulatorMetadata)
    {
        return string.IsNullOrEmpty(emulatorMetadata?.InputLayer) ? "virtual_pad" : emulatorMetadata.InputLayer;
    }

    public InputLayerConsent ResolveConsent()
    {
        string storedConsent = appInstance?.configManager?.ControllerMappingConsent;

        switch (storedConsent)
        {
            case "accepted": return InputLayerConsent.Accepted;
            case "declined": return InputLayerConsent.Declined;
            default: return InputLayerConsent.Unasked;
        }
    }

    public override void _ExitTree()
    {
        Input.JoyConnectionChanged -= OnJoyConnectionChanged;
        EndSession();
        (virtualPadBackend as ViGEmPadBackend)?.Dispose();
    }
}
