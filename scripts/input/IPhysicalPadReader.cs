public interface IPhysicalPadReader
{
    void ReadInto(int physicalDeviceId, PadState destination);
}
