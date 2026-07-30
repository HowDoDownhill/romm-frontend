using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class NetplayPortMapper : Node
{
    private const int DiscoveryTimeoutMilliseconds = 2000;
    private const string PortMappingDescription = "RomM Frontend Netplay";
    private const int PortMappingDurationSeconds = 0;

    private AppInstance appInstance;
    private Upnp upnpDevice;

    private readonly List<int> mappedPorts = new List<int>();

    public string ExternalAddress { get; private set; }
    public bool HasMappedPorts => mappedPorts.Count > 0;
    public string LastFailureReason { get; private set; }

    public bool IsPortMapped(int port) => mappedPorts.Contains(port);

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.netplayPortMapper = this;
    }

    public async Task<bool> TryMapPortsAsync(params int[] portsToMap)
    {
        ReleasePorts();

        return await Task.Run(() => MapPorts(portsToMap));
    }

    private bool MapPorts(int[] portsToMap)
    {
        var discoveredUpnp = new Upnp();

        int discoverResult = discoveredUpnp.Discover(DiscoveryTimeoutMilliseconds, 2, "InternetGatewayDevice");

        if (discoverResult != (int)Upnp.UpnpResult.Success || discoveredUpnp.GetGateway() == null || !discoveredUpnp.GetGateway().IsValidGateway())
        {
            LastFailureReason = "No UPnP gateway responded. UPnP is probably disabled on the router.";
            GD.Print($"[Netplay] {LastFailureReason} (discover result {discoverResult})");
            return false;
        }

        string queriedAddress = discoveredUpnp.QueryExternalAddress();

        foreach (int portToMap in portsToMap)
        {
            int mapResultUdp = discoveredUpnp.AddPortMapping(portToMap, portToMap, PortMappingDescription, "UDP", PortMappingDurationSeconds);
            int mapResultTcp = discoveredUpnp.AddPortMapping(portToMap, portToMap, PortMappingDescription, "TCP", PortMappingDurationSeconds);

            if (mapResultUdp != (int)Upnp.UpnpResult.Success && mapResultTcp != (int)Upnp.UpnpResult.Success)
            {
                LastFailureReason = $"The router refused to map port {portToMap}.";
                GD.Print($"[Netplay] {LastFailureReason} (udp {mapResultUdp}, tcp {mapResultTcp})");
                continue;
            }

            mappedPorts.Add(portToMap);
        }

        if (mappedPorts.Count == 0)
        {
            return false;
        }

        upnpDevice = discoveredUpnp;
        ExternalAddress = string.IsNullOrWhiteSpace(queriedAddress) ? null : queriedAddress;
        LastFailureReason = null;

        GD.Print($"[Netplay] Mapped ports {string.Join(", ", mappedPorts)} via UPnP. External address: {ExternalAddress ?? "unknown"}.");

        return true;
    }

    public void ReleasePorts()
    {
        if (upnpDevice != null)
        {
            foreach (int mappedPort in mappedPorts)
            {
                upnpDevice.DeletePortMapping(mappedPort, "UDP");
                upnpDevice.DeletePortMapping(mappedPort, "TCP");
            }
        }

        mappedPorts.Clear();
        upnpDevice = null;
        ExternalAddress = null;
    }

    public override void _ExitTree()
    {
        ReleasePorts();
    }
}
