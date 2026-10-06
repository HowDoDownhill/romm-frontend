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
    public string UnavailableReason => "physical controllers stay visible; emulator configs are pointed at the virtual pads instead";

    public bool HidePhysicalPads(IReadOnlyList<ConnectedController> physicalPads)
    {
        return false;
    }

    public void UnhideAll()
    {
    }
}
