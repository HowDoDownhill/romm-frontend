using System.Collections.Generic;

public interface IVirtualPadBackend
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    string SdlDeviceName { get; }
    int CreatedPadCount { get; }
    bool TryCreatePads(int padCount);
    void Submit(int playerIndex, PadState state);
    void DestroyPads();
    IReadOnlyList<string> DescribeCreatedPads();
}

public class UnsupportedPlatformPadBackend : IVirtualPadBackend
{
    public bool IsAvailable => false;
    public string UnavailableReason => "virtual controllers are only available on Windows for now";
    public string SdlDeviceName => "";
    public int CreatedPadCount => 0;

    public bool TryCreatePads(int padCount)
    {
        return false;
    }

    public void Submit(int playerIndex, PadState state)
    {
    }

    public void DestroyPads()
    {
    }

    public IReadOnlyList<string> DescribeCreatedPads()
    {
        return new List<string>();
    }
}
