using Godot;
using System;
using System.Net;
using System.Text;

public enum NetplayRole
{
    None,
    Host,
    Join
}

public partial class NetplayManager : Node
{
    private const string JoinCodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int JoinCodeLength = 10;
    private const int FallbackPort = 55435;

    private AppInstance appInstance;

    public NetplayRole Role { get; private set; } = NetplayRole.None;
    public string PeerAddress { get; private set; }
    public int Port { get; private set; }
    public string SessionCore { get; private set; }

    public bool HasActiveSession => Role != NetplayRole.None;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.netplayManager = this;
    }

    public string ResolvePlayerName()
    {
        string configuredName = appInstance?.configManager?.RomMUsername;
        return string.IsNullOrWhiteSpace(configuredName) ? "Player" : configuredName;
    }

    public int ResolveDefaultPort(EmulatorMeta emulatorMetadata)
    {
        int declaredPort = emulatorMetadata?.Netplay?.DefaultPort ?? 0;
        return declaredPort > 0 ? declaredPort : FallbackPort;
    }

    public bool SupportsNetplay(string emulatorName, string systemSlug)
    {
        var emulatorMetadata = appInstance?.emulatorManager?.LoadEmulatorMetadataFromDisk(emulatorName);
        return emulatorMetadata != null && emulatorMetadata.SupportsNetplayForSystem(systemSlug);
    }

    public bool SupportsNetplayForGame(Game game)
    {
        if (game?.System == null)
        {
            return false;
        }

        string mappedEmulatorName = appInstance?.emulatorManager?.GetMappedEmulator(game.System.Slug);
        return !string.IsNullOrEmpty(mappedEmulatorName) && SupportsNetplay(mappedEmulatorName, game.System.Slug);
    }

    private static bool IsPrivateIPv4(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Contains(":") || address.StartsWith("127."))
        {
            return false;
        }

        if (address.StartsWith("192.168.") || address.StartsWith("10."))
        {
            return true;
        }

        if (!address.StartsWith("172."))
        {
            return false;
        }

        string[] addressSegments = address.Split('.');

        return addressSegments.Length == 4
            && int.TryParse(addressSegments[1], out int secondSegment)
            && secondSegment >= 16
            && secondSegment <= 31;
    }

    public string ResolveLocalHostAddress()
    {
        string routedAddress = ResolveOutboundAddress();

        if (!string.IsNullOrEmpty(routedAddress))
        {
            return routedAddress;
        }

        return ResolveFirstPrivateAddress();
    }

    private static string ResolveOutboundAddress()
    {
        try
        {
            using var routeProbe = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            routeProbe.Connect("1.1.1.1", 65530);

            return (routeProbe.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString();
        }

        catch (System.Exception probeFailure)
        {
            GD.Print($"[Netplay] could not determine the outbound address ({probeFailure.Message}); falling back to the first private address.");
            return null;
        }
    }

    private string ResolveFirstPrivateAddress()
    {
        string firstUsableAddress = null;

        foreach (string localAddress in IP.GetLocalAddresses())
        {
            if (string.IsNullOrEmpty(localAddress) || localAddress.Contains(":") || localAddress.StartsWith("127."))
            {
                continue;
            }

            if (IsPrivateIPv4(localAddress))
            {
                return localAddress;
            }

            firstUsableAddress ??= localAddress;
        }

        return firstUsableAddress;
    }

    public void BeginHosting(int port)
    {
        Role = NetplayRole.Host;
        PeerAddress = null;
        Port = port > 0 ? port : FallbackPort;
    }

    public void BeginJoining(string peerAddress, int port)
    {
        Role = NetplayRole.Join;
        PeerAddress = peerAddress;
        Port = port > 0 ? port : FallbackPort;
    }

    public void EndSession()
    {
        Role = NetplayRole.None;
        PeerAddress = null;
        Port = 0;
        SessionCore = null;
    }

    public void RememberSessionCore(string selectedCore)
    {
        SessionCore = selectedCore;
    }

    public string BuildLaunchFragment(EmulatorMeta emulatorMetadata, string systemSlug)
    {
        if (!HasActiveSession || emulatorMetadata == null || !emulatorMetadata.SupportsNetplayForSystem(systemSlug))
        {
            return "";
        }

        var netplayConfig = emulatorMetadata.Netplay;
        string argumentTemplate = Role == NetplayRole.Host ? netplayConfig.HostArgs : netplayConfig.JoinArgs;

        if (string.IsNullOrEmpty(argumentTemplate))
        {
            return "";
        }

        int resolvedPort = Port > 0 ? Port : ResolveDefaultPort(emulatorMetadata);

        return argumentTemplate
            .Replace("{local_port}", resolvedPort.ToString())
            .Replace("{peer_port}", resolvedPort.ToString())
            .Replace("{peer_address}", PeerAddress ?? "")
            .Replace("{player_name}", ResolvePlayerName());
    }

    public string BuildJoinCode(string hostAddress, int port)
    {
        if (!IPAddress.TryParse(hostAddress, out IPAddress parsedAddress))
        {
            return null;
        }

        byte[] addressBytes = parsedAddress.GetAddressBytes();

        if (addressBytes.Length != 4)
        {
            return null;
        }

        var payload = new byte[6];
        Array.Copy(addressBytes, payload, 4);
        payload[4] = (byte)((port >> 8) & 0xFF);
        payload[5] = (byte)(port & 0xFF);

        return EncodeBase32(payload);
    }

    public bool TryParseJoinCode(string joinCode, out string hostAddress, out int port)
    {
        hostAddress = null;
        port = 0;

        byte[] payload = DecodeBase32(joinCode);

        if (payload == null || payload.Length != 6)
        {
            return false;
        }

        hostAddress = new IPAddress(new[] { payload[0], payload[1], payload[2], payload[3] }).ToString();
        port = (payload[4] << 8) | payload[5];

        return port > 0;
    }

    private static string EncodeBase32(byte[] payload)
    {
        var encoded = new StringBuilder(JoinCodeLength);
        int bitBuffer = 0;
        int bitCount = 0;

        foreach (byte payloadByte in payload)
        {
            bitBuffer = (bitBuffer << 8) | payloadByte;
            bitCount += 8;

            while (bitCount >= 5)
            {
                encoded.Append(JoinCodeAlphabet[(bitBuffer >> (bitCount - 5)) & 0x1F]);
                bitCount -= 5;
            }
        }

        if (bitCount > 0)
        {
            encoded.Append(JoinCodeAlphabet[(bitBuffer << (5 - bitCount)) & 0x1F]);
        }

        return encoded.ToString();
    }

    private static byte[] DecodeBase32(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return null;
        }

        string normalisedCode = NormaliseJoinCode(joinCode);
        var decoded = new System.Collections.Generic.List<byte>();
        int bitBuffer = 0;
        int bitCount = 0;

        foreach (char codeCharacter in normalisedCode)
        {
            int alphabetIndex = JoinCodeAlphabet.IndexOf(codeCharacter);

            if (alphabetIndex < 0)
            {
                return null;
            }

            bitBuffer = (bitBuffer << 5) | alphabetIndex;
            bitCount += 5;

            if (bitCount >= 8)
            {
                decoded.Add((byte)((bitBuffer >> (bitCount - 8)) & 0xFF));
                bitCount -= 8;
            }
        }

        return decoded.ToArray();
    }

    private static string NormaliseJoinCode(string joinCode)
    {
        var normalised = new StringBuilder(joinCode.Length);

        foreach (char codeCharacter in joinCode.ToUpperInvariant())
        {
            if (char.IsWhiteSpace(codeCharacter) || codeCharacter == '-')
            {
                continue;
            }

            if (codeCharacter == 'I' || codeCharacter == 'L')
            {
                normalised.Append('1');
                continue;
            }

            if (codeCharacter == 'O')
            {
                normalised.Append('0');
                continue;
            }

            normalised.Append(codeCharacter);
        }

        return normalised.ToString();
    }
}
