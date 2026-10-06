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
