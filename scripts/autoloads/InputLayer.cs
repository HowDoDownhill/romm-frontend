using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public enum InputLayerConsent
{
    Unasked,
    Accepted,
    Declined
}

public class SessionPlayer
{
    public int PhysicalDeviceId;
    public bool IsPassthrough;
    public int VirtualPadIndex = -1;
    public ushort Vendor;
    public ushort Product;
}

public partial class InputLayer : Node
{
    public const int MaximumPlayers = 4;

    private AppInstance appInstance;
    private ControllerManager controllerManager;

    private IPhysicalPadReader physicalPadReader;
    private IVirtualPadBackend virtualPadBackend;
    private IDeviceHider deviceHider;

    private readonly List<SessionPlayer> sessionPlayers = new List<SessionPlayer>();
    private readonly List<SessionPlayer> virtualPadPlayers = new List<SessionPlayer>();
    private readonly List<PadMappingTable> sessionMappingTables = new List<PadMappingTable>();
    private readonly List<PadState> sessionPhysicalStates = new List<PadState>();
    private readonly List<PadState> sessionVirtualStates = new List<PadState>();

    private const int VirtualPadEnumerationPollMilliseconds = 50;
    private const int VirtualPadEnumerationTimeoutMilliseconds = 3000;

    private HashSet<string> joypadsPresentBeforeSession = new HashSet<string>();
    private readonly HashSet<int> ownVirtualDeviceIds = new HashSet<int>();
    private readonly List<int> ownVirtualDeviceIdsInPlayerOrder = new List<int>();

    public bool IsSessionActive { get; private set; }
    public string LastSessionFailureReason { get; private set; } = "";

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.inputLayer = this;
        controllerManager = GetNode<ControllerManager>("/root/ControllerManager");

        physicalPadReader = new GodotPhysicalPadReader();
        virtualPadBackend = OperatingSystem.IsWindows() ? new ViGEmPadBackend() : new UnsupportedPlatformPadBackend();
        deviceHider = new NullDeviceHider();

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
    public int ActivePlayerCount => sessionPlayers.Count;
    public int VirtualPadCount => virtualPadPlayers.Count;
    public IReadOnlyList<SessionPlayer> SessionPlayers => sessionPlayers;

    public bool IsOwnVirtualDevice(int godotDeviceId)
    {
        return ownVirtualDeviceIds.Contains(godotDeviceId);
    }

    public IReadOnlyList<int> SessionPhysicalDeviceIds => sessionPlayers.Select(player => player.PhysicalDeviceId).ToList();

    public int ResolvePlayerOneDeviceId()
    {
        if (sessionPlayers.Count > 0)
        {
            return sessionPlayers[0].PhysicalDeviceId;
        }

        List<ConnectedController> availablePads = GetAssignablePhysicalPads();
        return availablePads.Count > 0 ? availablePads[0].GodotDeviceId : -1;
    }

    private readonly List<int> playerAssignmentDeviceIds = new List<int>();

    public IReadOnlyList<int> PlayerAssignmentDeviceIds => playerAssignmentDeviceIds;

    public void SetPlayerAssignment(IEnumerable<int> orderedDeviceIds)
    {
        playerAssignmentDeviceIds.Clear();
        playerAssignmentDeviceIds.AddRange(orderedDeviceIds);
        GD.Print($"[InputLayer] player order set to devices [{string.Join(", ", playerAssignmentDeviceIds)}].");
    }

    public void ClearPlayerAssignment()
    {
        if (playerAssignmentDeviceIds.Count == 0)
        {
            return;
        }

        playerAssignmentDeviceIds.Clear();
        GD.Print("[InputLayer] player order cleared; falling back to connection order.");
    }

    private List<ConnectedController> GetAssignablePhysicalPads()
    {
        var availablePads = controllerManager
            .GetConnectedControllers()
            .Where(candidate => !IsOwnVirtualDevice(candidate.GodotDeviceId))
            .ToList();

        return availablePads.OrderBy(ResolveAssignedPlayerPosition).ToList();
    }

    private int ResolveAssignedPlayerPosition(ConnectedController pad)
    {
        int assignedPosition = playerAssignmentDeviceIds.IndexOf(pad.GodotDeviceId);
        return assignedPosition >= 0 ? assignedPosition : playerAssignmentDeviceIds.Count + pad.ConnectionOrder;
    }

    public static int ReadXInputSlot(int godotDeviceId)
    {
        Godot.Collections.Dictionary joypadInfo = Input.GetJoyInfo(godotDeviceId);
        return joypadInfo.TryGetValue("xinput_index", out Variant slot) && int.TryParse(slot.AsString(), out int slotIndex) ? slotIndex : -1;
    }

    public static (ushort Vendor, ushort Product) ReadVendorAndProduct(int godotDeviceId)
    {
        Godot.Collections.Dictionary joypadInfo = Input.GetJoyInfo(godotDeviceId);
        ushort ReadId(string key) => joypadInfo.TryGetValue(key, out Variant value) && ushort.TryParse(value.AsString(), out ushort id) ? id : (ushort)0;
        return (ReadId("vendor_id"), ReadId("product_id"));
    }

    public bool BeginSession(string systemSlug, EmulatorMeta emulatorMetadata)
    {
        EndSession();

        if (ResolveConsent() != InputLayerConsent.Accepted)
        {
            LastSessionFailureReason = "automatic controller mapping has not been enabled";
            return false;
        }

        List<ConnectedController> physicalPads = GetAssignablePhysicalPads();

        if (physicalPads.Count == 0)
        {
            LastSessionFailureReason = "no physical controller is connected";
            GD.Print($"[InputLayer] not starting a session: {LastSessionFailureReason}.");
            return false;
        }

        List<SessionPlayer> candidatePlayers = physicalPads
            .Take(Math.Min(physicalPads.Count, ResolveMaximumPlayers(emulatorMetadata)))
            .Select(DescribePlayer)
            .ToList();

        if (candidatePlayers.Any(player => !player.IsPassthrough) && !virtualPadBackend.IsAvailable)
        {
            GD.Print($"[InputLayer] leaving out non-XInput pad(s): {virtualPadBackend.UnavailableReason}.");
            candidatePlayers.RemoveAll(player => !player.IsPassthrough);
        }

        if (candidatePlayers.Count == 0)
        {
            LastSessionFailureReason = virtualPadBackend.UnavailableReason;
            GD.Print($"[InputLayer] not starting a session: {LastSessionFailureReason}.");
            return false;
        }

        List<SessionPlayer> playersNeedingVirtualPads = candidatePlayers.Where(player => !player.IsPassthrough).ToList();
        joypadsPresentBeforeSession = Input.GetConnectedJoypads().Select(deviceId => DescribeJoypadIdentity((int)deviceId)).ToHashSet();
        xinputSlotsBeforeCreatingPads = XInputSlots.ReadConnectedSlots();

        GD.Print($"[InputLayer] XInput slot(s) [{string.Join(", ", xinputSlotsBeforeCreatingPads)}] in use; players: {string.Join("; ", candidatePlayers.Select(DescribePlayerForLog))}.");

        if (playersNeedingVirtualPads.Count > 0)
        {
            HidePhysicalPadsBeforeCreatingVirtualOnes(physicalPads);

            if (!virtualPadBackend.TryCreatePads(playersNeedingVirtualPads.Count))
            {
                LastSessionFailureReason = virtualPadBackend.UnavailableReason;
                GD.PrintErr($"[InputLayer] not starting a session: {LastSessionFailureReason}.");
                return false;
            }
        }

        sessionPlayers.AddRange(candidatePlayers);
        BuildVirtualPadMappings(systemSlug, emulatorMetadata, playersNeedingVirtualPads);
        RecordVirtualPadXInputSlots();

        IsSessionActive = true;
        LastSessionFailureReason = "";
        SetProcess(virtualPadPlayers.Count > 0);

        GD.Print($"[InputLayer] session started for '{systemSlug}' with {sessionPlayers.Count} player(s): {sessionPlayers.Count - virtualPadPlayers.Count} passed through, {virtualPadPlayers.Count} on virtual pads.");
        return true;
    }

    private static SessionPlayer DescribePlayer(ConnectedController pad)
    {
        (ushort vendor, ushort product) = ReadVendorAndProduct(pad.GodotDeviceId);

        return new SessionPlayer
        {
            PhysicalDeviceId = pad.GodotDeviceId,
            IsPassthrough = ReadXInputSlot(pad.GodotDeviceId) >= 0,
            Vendor = vendor,
            Product = product
        };
    }

    private string DescribePlayerForLog(SessionPlayer player, int playerIndex)
    {
        string route = player.IsPassthrough ? $"passthrough, XInput slot {ReadXInputSlot(player.PhysicalDeviceId)}" : "virtual pad";
        return $"P{playerIndex + 1} device {player.PhysicalDeviceId} \"{Input.GetJoyName(player.PhysicalDeviceId)}\" {player.Vendor:X4}/{player.Product:X4} ({route})";
    }

    private readonly List<int> virtualPadXInputSlots = new List<int>();
    private List<int> xinputSlotsBeforeCreatingPads = new List<int>();

    private void RecordVirtualPadXInputSlots()
    {
        virtualPadXInputSlots.Clear();

        foreach (int slotIndex in XInputSlots.ReadConnectedSlots())
        {
            if (!xinputSlotsBeforeCreatingPads.Contains(slotIndex))
            {
                virtualPadXInputSlots.Add(slotIndex);
            }
        }
    }

    private void ReportVirtualPadXInputSlots()
    {
        if (virtualPadPlayers.Count == 0)
        {
            return;
        }

        GD.Print(virtualPadXInputSlots.Count >= virtualPadPlayers.Count
            ? $"[InputLayer] virtual pads occupy XInput slot(s) [{string.Join(", ", virtualPadXInputSlots)}]."
            : $"[InputLayer] only {virtualPadXInputSlots.Count} of {virtualPadPlayers.Count} virtual pad(s) got an XInput slot; the rest are invisible to emulators.");
    }

    public bool IsPlayerPassthrough(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < sessionPlayers.Count && sessionPlayers[playerIndex].IsPassthrough;
    }

    public int ResolvePlayerXInputSlot(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= sessionPlayers.Count)
        {
            return -1;
        }

        SessionPlayer player = sessionPlayers[playerIndex];

        if (player.IsPassthrough)
        {
            return ReadXInputSlot(player.PhysicalDeviceId);
        }

        return player.VirtualPadIndex < virtualPadXInputSlots.Count ? virtualPadXInputSlots[player.VirtualPadIndex] : -1;
    }

    public (ushort Vendor, ushort Product) ResolvePlayerIdentity(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= sessionPlayers.Count)
        {
            return (0, 0);
        }

        SessionPlayer player = sessionPlayers[playerIndex];
        return player.IsPassthrough ? (player.Vendor, player.Product) : (ViGEmPadBackend.VirtualPadVendorId, ViGEmPadBackend.VirtualPadProductId);
    }

    public string ResolvePlayerGuid(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= sessionPlayers.Count)
        {
            return "";
        }

        SessionPlayer player = sessionPlayers[playerIndex];

        if (player.IsPassthrough)
        {
            return Input.GetJoyGuid(player.PhysicalDeviceId);
        }

        return player.VirtualPadIndex < ownVirtualDeviceIdsInPlayerOrder.Count ? Input.GetJoyGuid(ownVirtualDeviceIdsInPlayerOrder[player.VirtualPadIndex]) : "";
    }

    public async Task WaitForVirtualPadsToEnumerate()
    {
        if (!IsSessionActive || virtualPadPlayers.Count == 0)
        {
            return;
        }

        int elapsedMilliseconds = 0;
        GD.Print($"[InputLayer] waiting for virtual pads to enumerate: {ownVirtualDeviceIdsInPlayerOrder.Count} of {virtualPadPlayers.Count} seen.");

        while ((ownVirtualDeviceIdsInPlayerOrder.Count < virtualPadPlayers.Count || virtualPadXInputSlots.Count < virtualPadPlayers.Count)
            && elapsedMilliseconds < VirtualPadEnumerationTimeoutMilliseconds)
        {
            await ToSignal(GetTree().CreateTimer(VirtualPadEnumerationPollMilliseconds / 1000.0f), SceneTreeTimer.SignalName.Timeout);
            elapsedMilliseconds += VirtualPadEnumerationPollMilliseconds;
            RecordVirtualPadXInputSlots();
        }

        GD.Print($"[InputLayer] enumeration wait finished after {elapsedMilliseconds} ms with {ownVirtualDeviceIdsInPlayerOrder.Count} pad(s).");
        ReportVirtualPadXInputSlots();

        if (ownVirtualDeviceIdsInPlayerOrder.Count < virtualPadPlayers.Count)
        {
            GD.PrintErr($"[InputLayer] only {ownVirtualDeviceIdsInPlayerOrder.Count} of {virtualPadPlayers.Count} virtual pad(s) enumerated; emulator config may name the wrong device.");
        }
    }

    private void HidePhysicalPadsBeforeCreatingVirtualOnes(List<ConnectedController> physicalPads)
    {
        if (!deviceHider.IsAvailable)
        {
            return;
        }

        deviceHider.HidePhysicalPads(physicalPads);
    }

    private int ResolveMaximumPlayers(EmulatorMeta emulatorMetadata)
    {
        int metadataMaximum = emulatorMetadata?.ControllerConfig?.MaxControllers ?? 0;
        return metadataMaximum > 0 ? Math.Min(metadataMaximum, MaximumPlayers) : MaximumPlayers;
    }

    private void BuildVirtualPadMappings(string systemSlug, EmulatorMeta emulatorMetadata, List<SessionPlayer> playersNeedingVirtualPads)
    {
        foreach (SessionPlayer player in playersNeedingVirtualPads)
        {
            player.VirtualPadIndex = virtualPadPlayers.Count;
            virtualPadPlayers.Add(player);
            sessionMappingTables.Add(BuildMappingTableForPlayer(systemSlug, emulatorMetadata, sessionPlayers.IndexOf(player)));
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
        if (!IsSessionActive && sessionPlayers.Count == 0)
        {
            return;
        }

        SetProcess(false);
        IsSessionActive = false;

        virtualPadBackend.DestroyPads();
        deviceHider.UnhideAll();

        sessionPlayers.Clear();
        virtualPadPlayers.Clear();
        virtualPadXInputSlots.Clear();
        xinputSlotsBeforeCreatingPads.Clear();
        sessionMappingTables.Clear();
        sessionPhysicalStates.Clear();
        sessionVirtualStates.Clear();
        ownVirtualDeviceIdsInPlayerOrder.Clear();
        joypadsPresentBeforeSession.Clear();

        GD.Print("[InputLayer] session ended.");
    }

    public override void _Process(double delta)
    {
        if (!IsSessionActive)
        {
            return;
        }

        for (int virtualPadIndex = 0; virtualPadIndex < virtualPadPlayers.Count; virtualPadIndex++)
        {
            PadState physicalState = sessionPhysicalStates[virtualPadIndex];
            PadState virtualState = sessionVirtualStates[virtualPadIndex];

            physicalPadReader.ReadInto(virtualPadPlayers[virtualPadIndex].PhysicalDeviceId, physicalState);
            sessionMappingTables[virtualPadIndex].Apply(physicalState, virtualState);
            virtualPadBackend.Submit(virtualPadIndex, virtualState);
        }
    }

    private void OnJoyConnectionChanged(long deviceId, bool connected)
    {
        int joypadId = (int)deviceId;

        if (!connected)
        {
            ownVirtualDeviceIds.Remove(joypadId);

            if (playerAssignmentDeviceIds.Contains(joypadId))
            {
                ClearPlayerAssignment();
            }

            return;
        }

        if (virtualPadBackend.CreatedPadCount == 0 || joypadsPresentBeforeSession.Contains(DescribeJoypadIdentity(joypadId)))
        {
            ownVirtualDeviceIds.Remove(joypadId);
            return;
        }

        if (ownVirtualDeviceIds.Add(joypadId))
        {
            ownVirtualDeviceIdsInPlayerOrder.Add(joypadId);
            GD.Print($"[InputLayer] our virtual pad appeared as Godot device {joypadId} \"{Input.GetJoyName(joypadId)}\"; excluding it from input.");
        }
    }

    private static string DescribeJoypadIdentity(int godotDeviceId)
    {
        return $"{godotDeviceId}:{Input.GetJoyGuid(godotDeviceId)}";
    }

    public Dictionary<string, string> BuildLaunchEnvironment(EmulatorMeta emulatorMetadata)
    {
        if (!IsSessionActive || ResolveInputLayerMode(emulatorMetadata) == "none")
        {
            return new Dictionary<string, string>();
        }

        var allowedDevices = new List<(ushort Vendor, ushort Product)>();

        for (int playerIndex = 0; playerIndex < sessionPlayers.Count; playerIndex++)
        {
            (ushort, ushort) identity = ResolvePlayerIdentity(playerIndex);

            if (!allowedDevices.Contains(identity))
            {
                allowedDevices.Add(identity);
            }
        }

        return SdlDeviceAllowlist.BuildEnvironment(allowedDevices);
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
        (virtualPadBackend as IDisposable)?.Dispose();
    }
}
