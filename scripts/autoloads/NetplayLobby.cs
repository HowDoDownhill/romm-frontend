using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class NetplayLobby : Node
{
    private const int DefaultLobbyPort = 55440;
    private const int MaximumLobbyMembers = 8;
    private const int HostPeerId = 1;

    private AppInstance appInstance;
    private ENetMultiplayerPeer lobbyPeer;

    public bool IsHosting { get; private set; }
    public bool IsInLobby => lobbyPeer != null;
    public int SelectedRomId { get; private set; }
    public string SelectedGameName { get; private set; }
    public string RequiredEmulatorName { get; private set; }
    public string RequiredEmulatorVersion { get; private set; }
    public string RequiredRomHash { get; private set; }

    private const double IdentityTimeoutSeconds = 10.0;

    private readonly Dictionary<long, LobbyMember> membersByPeerId = new Dictionary<long, LobbyMember>();
    private readonly Dictionary<long, double> secondsAwaitingIdentityByPeerId = new Dictionary<long, double>();

    public class LobbyMember
    {
        public long PeerId;
        public string Username;
        public string RommHost;
        public bool HasGame;
        public bool IsReady;
        public string Status;
        public string EmulatorVersion;
    }

    [Signal]
    public delegate void MembersChangedEventHandler();

    [Signal]
    public delegate void GameSelectionChangedEventHandler(int romId);

    [Signal]
    public delegate void HostBrowsingGameChangedEventHandler(int romId);

    [Signal]
    public delegate void StartRequestedEventHandler(string hostAddress, int port);

    [Signal]
    public delegate void LobbyClosedEventHandler(string reason);

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.netplayLobby = this;

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public IReadOnlyList<LobbyMember> Members => membersByPeerId.Values.OrderBy(member => member.PeerId).ToList();

    public bool AllMembersReady => membersByPeerId.Count > 1 && membersByPeerId.Values.All(member => member.IsReady);

    public int ResolveLobbyPort() => DefaultLobbyPort;

    public bool HostLobby()
    {
        LeaveLobby();

        var createdPeer = new ENetMultiplayerPeer();
        Error createResult = createdPeer.CreateServer(DefaultLobbyPort, MaximumLobbyMembers);

        if (createResult != Error.Ok)
        {
            GD.PrintErr($"Could not host netplay lobby on port {DefaultLobbyPort}: {createResult}");
            return false;
        }

        lobbyPeer = createdPeer;
        Multiplayer.MultiplayerPeer = lobbyPeer;
        IsHosting = true;

        membersByPeerId.Clear();
        membersByPeerId[HostPeerId] = BuildLocalMember(HostPeerId);

        EmitSignal(SignalName.MembersChanged);
        return true;
    }

    public bool JoinLobby(string hostAddress, int port)
    {
        LeaveLobby();

        var createdPeer = new ENetMultiplayerPeer();
        Error createResult = createdPeer.CreateClient(hostAddress, port > 0 ? port : DefaultLobbyPort);

        if (createResult != Error.Ok)
        {
            GD.PrintErr($"Could not join netplay lobby at {hostAddress}:{port}: {createResult}");
            return false;
        }

        lobbyPeer = createdPeer;
        Multiplayer.MultiplayerPeer = lobbyPeer;
        IsHosting = false;

        membersByPeerId.Clear();
        return true;
    }

    public void LeaveLobby()
    {
        if (lobbyPeer == null)
        {
            return;
        }

        if (IsHosting)
        {
            Rpc(MethodName.ReceiveHostLeaving);
        }

        lobbyPeer.Close();
        Multiplayer.MultiplayerPeer = null;
        lobbyPeer = null;
        IsHosting = false;
        SelectedRomId = 0;
        SelectedGameName = null;
        RequiredEmulatorName = null;
        RequiredEmulatorVersion = null;
        RequiredRomHash = null;
        membersByPeerId.Clear();
        secondsAwaitingIdentityByPeerId.Clear();

        EmitSignal(SignalName.MembersChanged);
    }

    private LobbyMember BuildLocalMember(long peerId)
    {
        return new LobbyMember
        {
            PeerId = peerId,
            Username = appInstance?.netplayManager?.ResolvePlayerName() ?? "Player",
            RommHost = appInstance?.rommApi?.ApiHost ?? "",
            HasGame = false,
            IsReady = false,
            Status = "",
            EmulatorVersion = ""
        };
    }

    public override void _Process(double delta)
    {
        if (!IsHosting || secondsAwaitingIdentityByPeerId.Count == 0)
        {
            return;
        }

        foreach (long peerId in secondsAwaitingIdentityByPeerId.Keys.ToList())
        {
            secondsAwaitingIdentityByPeerId[peerId] += delta;

            if (secondsAwaitingIdentityByPeerId[peerId] < IdentityTimeoutSeconds)
            {
                continue;
            }

            GD.PrintErr($"[Lobby] peer {peerId} never identified itself; disconnecting.");
            secondsAwaitingIdentityByPeerId.Remove(peerId);
            lobbyPeer?.DisconnectPeer((int)peerId);
        }
    }

    private void OnPeerConnected(long peerId)
    {
        if (!IsHosting)
        {
            return;
        }

        secondsAwaitingIdentityByPeerId[peerId] = 0.0;

        BroadcastMembers();
    }

    private void SendGameSelectionTo(long peerId)
    {
        if (SelectedRomId <= 0)
        {
            return;
        }

        GD.Print($"[Lobby] sending selection {SelectedRomId} ({SelectedGameName}) to peer {peerId}.");
        RpcId(peerId, MethodName.ReceiveGameSelection, SelectedRomId, SelectedGameName ?? "", RequiredEmulatorName ?? "", RequiredEmulatorVersion ?? "", RequiredRomHash ?? "");
    }

    private void OnPeerDisconnected(long peerId)
    {
        secondsAwaitingIdentityByPeerId.Remove(peerId);

        if (!membersByPeerId.Remove(peerId))
        {
            return;
        }

        if (IsHosting)
        {
            BroadcastMembers();
        }

        EmitSignal(SignalName.MembersChanged);
    }

    private void OnConnectedToServer()
    {
        var localMember = BuildLocalMember(Multiplayer.GetUniqueId());
        GD.Print($"[Lobby] connected as peer {Multiplayer.GetUniqueId()}, sending identity user={localMember.Username} host={localMember.RommHost}");
        RpcId(HostPeerId, MethodName.ReceiveMemberIdentity, localMember.Username, localMember.RommHost);
    }

    private void OnConnectionFailed()
    {
        LeaveLobby();
        EmitSignal(SignalName.LobbyClosed, "Could not connect to the lobby.");
    }

    private void OnServerDisconnected()
    {
        LeaveLobby();
        EmitSignal(SignalName.LobbyClosed, "The host left the lobby.");
        appInstance?.emulatorManager?.CloseEmulator();
    }

    private void PruneDisconnectedMembers()
    {
        var connectedPeerIds = Multiplayer.GetPeers().Select(peerId => (long)peerId).ToHashSet();

        var stalePeerIds = membersByPeerId.Keys
            .Where(peerId => peerId != HostPeerId && !connectedPeerIds.Contains(peerId))
            .ToList();

        foreach (long stalePeerId in stalePeerIds)
        {
            GD.Print($"[Lobby] dropping member {stalePeerId}: no longer a connected peer.");
            membersByPeerId.Remove(stalePeerId);
            secondsAwaitingIdentityByPeerId.Remove(stalePeerId);
        }
    }

    private void BroadcastMembers()
    {
        if (!IsHosting)
        {
            return;
        }

        PruneDisconnectedMembers();

        var orderedMembers = Members;
        var peerIds = orderedMembers.Select(member => (int)member.PeerId).ToArray();
        var usernames = orderedMembers.Select(member => member.Username ?? "").ToArray();
        var rommHosts = orderedMembers.Select(member => member.RommHost ?? "").ToArray();
        var hasGameFlags = orderedMembers.Select(member => member.HasGame ? 1 : 0).ToArray();
        var readyFlags = orderedMembers.Select(member => member.IsReady ? 1 : 0).ToArray();
        var statuses = orderedMembers.Select(member => member.Status ?? "").ToArray();
        var emulatorVersions = orderedMembers.Select(member => member.EmulatorVersion ?? "").ToArray();

        GD.Print($"[Lobby] host broadcasting roster of {peerIds.Length}: {string.Join(", ", usernames)}");

        Rpc(MethodName.ReceiveMemberRoster, peerIds, usernames, rommHosts, hasGameFlags, readyFlags, statuses, emulatorVersions);
        EmitSignal(SignalName.MembersChanged);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveMemberIdentity(string username, string rommHost)
    {
        if (!IsHosting)
        {
            return;
        }

        long senderPeerId = Multiplayer.GetRemoteSenderId();
        secondsAwaitingIdentityByPeerId.Remove(senderPeerId);

        GD.Print($"[Lobby] identity from peer {senderPeerId}: user={username} host=\"{rommHost}\" localHost=\"{appInstance?.rommApi?.ApiHost}\"");

        if (!IsSameRommServer(rommHost))
        {
            GD.PrintErr($"[Lobby] rejecting peer {senderPeerId} ({username}): different RomM server \"{rommHost}\" vs \"{appInstance?.rommApi?.ApiHost}\".");
            lobbyPeer.DisconnectPeer((int)senderPeerId);
            return;
        }

        membersByPeerId[senderPeerId] = new LobbyMember
        {
            PeerId = senderPeerId,
            Username = username,
            RommHost = rommHost,
            HasGame = false,
            IsReady = false,
            Status = "",
            EmulatorVersion = ""
        };

        BroadcastMembers();
        SendGameSelectionTo(senderPeerId);
    }

    private bool IsSameRommServer(string rommHost)
    {
        string localRommHost = appInstance?.rommApi?.ApiHost;

        if (string.IsNullOrEmpty(localRommHost) || string.IsNullOrEmpty(rommHost))
        {
            return false;
        }

        return string.Equals(localRommHost.TrimEnd('/'), rommHost.TrimEnd('/'), System.StringComparison.OrdinalIgnoreCase);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveMemberRoster(int[] peerIds, string[] usernames, string[] rommHosts, int[] hasGameFlags, int[] readyFlags, string[] statuses, string[] emulatorVersions)
    {
        GD.Print($"[Lobby] client received roster of {peerIds.Length}: {string.Join(", ", usernames)}");

        membersByPeerId.Clear();

        for (int memberIndex = 0; memberIndex < peerIds.Length; memberIndex++)
        {
            membersByPeerId[peerIds[memberIndex]] = new LobbyMember
            {
                PeerId = peerIds[memberIndex],
                Username = usernames[memberIndex],
                RommHost = rommHosts[memberIndex],
                HasGame = hasGameFlags[memberIndex] != 0,
                IsReady = readyFlags[memberIndex] != 0,
                Status = memberIndex < statuses.Length ? statuses[memberIndex] : "",
                EmulatorVersion = memberIndex < emulatorVersions.Length ? emulatorVersions[memberIndex] : ""
            };
        }

        EmitSignal(SignalName.MembersChanged);
    }

    public void BroadcastBrowsingGame(int romId)
    {
        if (!IsHosting || romId <= 0 || membersByPeerId.Count < 2)
        {
            return;
        }

        Rpc(MethodName.ReceiveHostBrowsingGame, romId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHostBrowsingGame(int romId)
    {
        EmitSignal(SignalName.HostBrowsingGameChanged, romId);
    }

    public void SelectGame(int romId, string gameName, string emulatorName, string emulatorVersion, string romHash)
    {
        if (!IsHosting)
        {
            return;
        }

        SelectedRomId = romId;
        SelectedGameName = gameName;
        RequiredEmulatorName = emulatorName;
        RequiredEmulatorVersion = emulatorVersion;
        RequiredRomHash = romHash;

        foreach (var member in membersByPeerId.Values)
        {
            member.HasGame = false;
            member.IsReady = false;
        }

        Rpc(MethodName.ReceiveGameSelection, romId, gameName ?? "", emulatorName ?? "", emulatorVersion ?? "", romHash ?? "");
        BroadcastMembers();
        EmitSignal(SignalName.GameSelectionChanged, romId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveGameSelection(int romId, string gameName, string emulatorName, string emulatorVersion, string romHash)
    {
        SelectedRomId = romId;
        SelectedGameName = gameName;
        RequiredEmulatorName = emulatorName;
        RequiredEmulatorVersion = emulatorVersion;
        RequiredRomHash = romHash;
        EmitSignal(SignalName.GameSelectionChanged, romId);
    }

    public void ReportLocalReadiness(bool hasGame, bool isReady, string status, string emulatorVersion)
    {
        if (IsHosting)
        {
            if (membersByPeerId.TryGetValue(HostPeerId, out LobbyMember hostMember))
            {
                hostMember.HasGame = hasGame;
                hostMember.IsReady = isReady;
                hostMember.Status = status;
                hostMember.EmulatorVersion = emulatorVersion ?? "";
                BroadcastMembers();
            }

            return;
        }

        RpcId(HostPeerId, MethodName.ReceiveMemberReadiness, hasGame, isReady, status ?? "", emulatorVersion ?? "");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveMemberReadiness(bool hasGame, bool isReady, string status, string emulatorVersion)
    {
        if (!IsHosting)
        {
            return;
        }

        long senderPeerId = Multiplayer.GetRemoteSenderId();

        if (!membersByPeerId.TryGetValue(senderPeerId, out LobbyMember member))
        {
            return;
        }

        member.HasGame = hasGame;
        member.IsReady = isReady;
        member.Status = status;
        member.EmulatorVersion = emulatorVersion;

        BroadcastMembers();
    }

    public void ReleaseMembersToStart(string hostAddress, int netplayPort)
    {
        if (!IsHosting)
        {
            return;
        }

        Rpc(MethodName.ReceiveStartRequest, hostAddress ?? "", netplayPort);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveStartRequest(string hostAddress, int netplayPort)
    {
        EmitSignal(SignalName.StartRequested, hostAddress, netplayPort);
    }

    public void ClearMemberReadiness()
    {
        if (!IsHosting)
        {
            return;
        }

        foreach (var member in membersByPeerId.Values)
        {
            member.IsReady = false;
        }

        GD.Print("[Lobby] session over; clearing everyone's readiness.");
        BroadcastMembers();
    }

    public void ReleaseMembersToStop()
    {
        if (!IsHosting)
        {
            return;
        }

        GD.Print("[Lobby] host ended the game, telling members to close.");
        Rpc(MethodName.ReceiveStopRequest);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveStopRequest()
    {
        GD.Print("[Lobby] host ended the game, closing the emulator.");
        appInstance?.emulatorManager?.CloseEmulator();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHostLeaving()
    {
        appInstance?.emulatorManager?.CloseEmulator();
        EmitSignal(SignalName.LobbyClosed, "The host ended the session.");
    }
}
