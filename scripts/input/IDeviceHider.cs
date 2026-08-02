using System.Collections.Generic;

public interface IDeviceHider
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    bool HidePhysicalPads(IReadOnlyList<ConnectedController> physicalPads);
    void UnhideAll();
}

public class NullDeviceHider : IDeviceHider
{
    public bool IsAvailable => false;
    public string UnavailableReason => "device hiding is not implemented on this platform yet";

    public bool HidePhysicalPads(IReadOnlyList<ConnectedController> physicalPads)
    {
        return false;
    }

    public void UnhideAll()
    {
    }
}
