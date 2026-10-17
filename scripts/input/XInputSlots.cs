using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class XInputSlots
{
    public const int SlotCount = 4;
    private const uint SlotConnected = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepadState
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepadState Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint ReadSlotState(uint slotIndex, ref XInputState state);

    private const int CapabilitiesExSize = 32;
    private const int CapabilitiesExVendorOffset = 20;
    private const int CapabilitiesExProductOffset = 22;

    [DllImport("xinput1_4.dll", EntryPoint = "#108")]
    private static extern uint ReadSlotCapabilitiesEx(uint reserved, uint slotIndex, uint flags, byte[] capabilities);

    public static List<(int Slot, ushort Vendor, ushort Product)> ReadSlotIdentities()
    {
        var identities = new List<(int Slot, ushort Vendor, ushort Product)>();

        foreach (int slotIndex in ReadConnectedSlots())
        {
            byte[] capabilities = new byte[CapabilitiesExSize];

            try
            {
                bool readIdentity = ReadSlotCapabilitiesEx(1, (uint)slotIndex, 0, capabilities) == SlotConnected;
                identities.Add(readIdentity
                    ? (slotIndex, BitConverter.ToUInt16(capabilities, CapabilitiesExVendorOffset), BitConverter.ToUInt16(capabilities, CapabilitiesExProductOffset))
                    : (slotIndex, (ushort)0, (ushort)0));
            }
            catch (EntryPointNotFoundException)
            {
                identities.Add((slotIndex, 0, 0));
            }
        }

        return identities;
    }

    public static List<int> ReadConnectedSlots()
    {
        var connectedSlots = new List<int>();

        if (!OperatingSystem.IsWindows())
        {
            return connectedSlots;
        }

        for (uint slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            XInputState slotState = new XInputState();

            try
            {
                if (ReadSlotState(slotIndex, ref slotState) == SlotConnected)
                {
                    connectedSlots.Add((int)slotIndex);
                }
            }
            catch (DllNotFoundException)
            {
                return connectedSlots;
            }
            catch (EntryPointNotFoundException)
            {
                return connectedSlots;
            }
        }

        return connectedSlots;
    }
}
