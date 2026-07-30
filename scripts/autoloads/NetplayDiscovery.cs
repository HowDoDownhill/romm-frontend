using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

public partial class NetplayDiscovery : Node
{
    private const int DiscoveryPort = 55441;
    private const double AdvertiseIntervalSeconds = 2.0;
    private const double SessionExpirySeconds = 8.0;

    private AppInstance appInstance;
    private PacketPeerUdp discoverySocket;
    private string localInstanceId;
    private double secondsSinceAdvertise;
    private bool isAdvertising;
    private NetplayAdvertisement localAdvertisement;

    private readonly Dictionary<string, DiscoveredSession> sessionsByInstanceId = new Dictionary<string, DiscoveredSession>();

    public class DiscoveredSession
    {
        public string InstanceId;
        public string HostAddress;
        public string Username;
        public string RommHost;
        public int RomId;
        public string GameName;
        public int LobbyPort;
        public int MemberCount;
        public ulong LastSeenMilliseconds;
    }

    [Signal]
    public delegate void SessionsChangedEventHandler();

    public bool IsListening => discoverySocket != null;

    public IReadOnlyList<DiscoveredSession> Sessions => sessionsByInstanceId.Values
        .OrderBy(session => session.Username)
        .ToList();

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.netplayDiscovery = this;
        localInstanceId = Guid.NewGuid().ToString("N");
    }

    public bool StartListening()
    {
        if (discoverySocket != null)
        {
            return true;
        }

        var createdSocket = new PacketPeerUdp();
        createdSocket.SetBroadcastEnabled(true);

        Error bindResult = createdSocket.Bind(DiscoveryPort);

        if (bindResult != Error.Ok)
        {
            GD.PrintErr($"Could not bind netplay discovery port {DiscoveryPort}: {bindResult}. Another instance on this machine is probably already listening.");
            return false;
        }

        createdSocket.SetDestAddress("255.255.255.255", DiscoveryPort);
        discoverySocket = createdSocket;

        return true;
    }

    public void StopListening()
    {
        StopAdvertising();

        discoverySocket?.Close();
        discoverySocket = null;

        sessionsByInstanceId.Clear();
        EmitSignal(SignalName.SessionsChanged);
    }

    public void StartAdvertising(int romId, string gameName, int lobbyPort, int memberCount)
    {
        if (!StartListening())
        {
            return;
        }

        localAdvertisement = new NetplayAdvertisement
        {
            InstanceId = localInstanceId,
            RommHost = appInstance?.rommApi?.ApiHost ?? "",
            Username = appInstance?.netplayManager?.ResolvePlayerName() ?? "Player",
            RomId = romId,
            GameName = gameName ?? "",
            LobbyPort = lobbyPort,
            MemberCount = memberCount
        };

        isAdvertising = true;
        secondsSinceAdvertise = AdvertiseIntervalSeconds;
    }

    public void UpdateAdvertisement(int romId, string gameName, int memberCount)
    {
        if (localAdvertisement == null)
        {
            return;
        }

        localAdvertisement.RomId = romId;
        localAdvertisement.GameName = gameName ?? "";
        localAdvertisement.MemberCount = memberCount;
    }

    public void StopAdvertising()
    {
        isAdvertising = false;
        localAdvertisement = null;
    }

    public override void _Process(double delta)
    {
        if (discoverySocket == null)
        {
            return;
        }

        ReceiveAdvertisements();
        ExpireStaleSessions();
        BroadcastLocalAdvertisement(delta);
    }

    private void BroadcastLocalAdvertisement(double delta)
    {
        if (!isAdvertising || localAdvertisement == null)
        {
            return;
        }

        secondsSinceAdvertise += delta;

        if (secondsSinceAdvertise < AdvertiseIntervalSeconds)
        {
            return;
        }

        secondsSinceAdvertise = 0.0;

        string serialisedAdvertisement = JsonSerializer.Serialize(localAdvertisement, RommJsonContext.Default.NetplayAdvertisement);
        discoverySocket.PutPacket(Encoding.UTF8.GetBytes(serialisedAdvertisement));
    }

    private void ReceiveAdvertisements()
    {
        bool sessionsChanged = false;

        while (discoverySocket.GetAvailablePacketCount() > 0)
        {
            byte[] packet = discoverySocket.GetPacket();
            string senderAddress = discoverySocket.GetPacketIP();

            NetplayAdvertisement advertisement = ParseAdvertisement(packet);

            if (advertisement == null || advertisement.InstanceId == localInstanceId)
            {
                continue;
            }

            if (!IsSameRommServer(advertisement.RommHost))
            {
                continue;
            }

            sessionsByInstanceId[advertisement.InstanceId] = new DiscoveredSession
            {
                InstanceId = advertisement.InstanceId,
                HostAddress = senderAddress,
                Username = advertisement.Username,
                RommHost = advertisement.RommHost,
                RomId = advertisement.RomId,
                GameName = advertisement.GameName,
                LobbyPort = advertisement.LobbyPort,
                MemberCount = advertisement.MemberCount,
                LastSeenMilliseconds = Time.GetTicksMsec()
            };

            sessionsChanged = true;
        }

        if (sessionsChanged)
        {
            EmitSignal(SignalName.SessionsChanged);
        }
    }

    private static NetplayAdvertisement ParseAdvertisement(byte[] packet)
    {
        try
        {
            return JsonSerializer.Deserialize(Encoding.UTF8.GetString(packet), RommJsonContext.Default.NetplayAdvertisement);
        }

        catch (Exception)
        {
            return null;
        }
    }

    private bool IsSameRommServer(string rommHost)
    {
        string localRommHost = appInstance?.rommApi?.ApiHost;

        if (string.IsNullOrEmpty(localRommHost) || string.IsNullOrEmpty(rommHost))
        {
            return false;
        }

        return string.Equals(localRommHost.TrimEnd('/'), rommHost.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private void ExpireStaleSessions()
    {
        ulong nowMilliseconds = Time.GetTicksMsec();
        var expiredInstanceIds = sessionsByInstanceId
            .Where(entry => nowMilliseconds - entry.Value.LastSeenMilliseconds > SessionExpirySeconds * 1000.0)
            .Select(entry => entry.Key)
            .ToList();

        if (expiredInstanceIds.Count == 0)
        {
            return;
        }

        foreach (string expiredInstanceId in expiredInstanceIds)
        {
            sessionsByInstanceId.Remove(expiredInstanceId);
        }

        EmitSignal(SignalName.SessionsChanged);
    }
}
