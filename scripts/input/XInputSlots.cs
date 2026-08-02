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
