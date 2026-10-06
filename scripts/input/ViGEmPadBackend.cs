using Godot;
using System;
using System.Collections.Generic;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

public class ViGEmPadBackend : IVirtualPadBackend
{
    public const ushort VirtualPadVendorId = 0x045E;
    public const ushort VirtualPadProductId = 0x028E;

    private const short StickFullDeflection = 32767;
    private const byte TriggerFullPress = 255;

    private static readonly (PadControl Control, Xbox360Button Button)[] ButtonBindings = new (PadControl, Xbox360Button)[]
    {
        (PadControl.FaceSouth, Xbox360Button.A),
        (PadControl.FaceEast, Xbox360Button.B),
        (PadControl.FaceWest, Xbox360Button.X),
        (PadControl.FaceNorth, Xbox360Button.Y),
        (PadControl.LeftShoulder, Xbox360Button.LeftShoulder),
        (PadControl.RightShoulder, Xbox360Button.RightShoulder),
        (PadControl.Back, Xbox360Button.Back),
        (PadControl.Start, Xbox360Button.Start),
        (PadControl.Guide, Xbox360Button.Guide),
        (PadControl.LeftStick, Xbox360Button.LeftThumb),
        (PadControl.RightStick, Xbox360Button.RightThumb),
        (PadControl.DpadUp, Xbox360Button.Up),
        (PadControl.DpadDown, Xbox360Button.Down),
        (PadControl.DpadLeft, Xbox360Button.Left),
        (PadControl.DpadRight, Xbox360Button.Right)
    };

    private ViGEmClient vigemClient;
    private readonly List<IXbox360Controller> virtualPads = new List<IXbox360Controller>();

    public bool IsAvailable => DetectAvailability();
    public string UnavailableReason { get; private set; } = "";
    public string SdlDeviceName => "Xbox 360 Controller";
    public int CreatedPadCount => virtualPads.Count;

    private bool DetectAvailability()
    {
        if (vigemClient != null)
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            UnavailableReason = "ViGEm is a Windows-only driver";
            return false;
        }

        return TryConnectToBus();
    }

    private bool TryConnectToBus()
    {
        if (vigemClient != null)
        {
            return true;
        }

        try
        {
            vigemClient = new ViGEmClient();
            UnavailableReason = "";
            return true;
        }
        catch (Exception busFailure)
        {
            UnavailableReason = $"could not reach ViGEmBus ({DescribeFailure(busFailure)}); the driver is probably not installed";
            return false;
        }
    }

    public bool TryCreatePads(int padCount)
    {
        DestroyPads();

        if (!TryConnectToBus())
        {
            return false;
        }

        for (int playerIndex = 0; playerIndex < padCount; playerIndex++)
        {
            IXbox360Controller createdPad = TryCreateSinglePad(playerIndex);

            if (createdPad == null)
            {
                DestroyPads();
                return false;
            }

            virtualPads.Add(createdPad);
        }

        return true;
    }

    private IXbox360Controller TryCreateSinglePad(int playerIndex)
    {
        IXbox360Controller virtualPad;

        try
        {
            virtualPad = vigemClient.CreateXbox360Controller(VirtualPadVendorId, VirtualPadProductId);
            virtualPad.AutoSubmitReport = false;
        }
        catch (Exception allocationFailure)
        {
            UnavailableReason = $"could not allocate virtual pad {playerIndex + 1}: {DescribeFailure(allocationFailure)}";
            GD.PrintErr($"[InputLayer] {UnavailableReason}");
            return null;
        }

        try
        {
            virtualPad.Connect();
        }
        catch (Exception connectionFailure)
        {
            UnavailableReason = $"could not plug in virtual pad {playerIndex + 1}: {DescribeFailure(connectionFailure)}";
            GD.PrintErr($"[InputLayer] {UnavailableReason}");
            return null;
        }

        return virtualPad;
    }

    public void Submit(int playerIndex, PadState state)
    {
        if (playerIndex < 0 || playerIndex >= virtualPads.Count)
        {
            return;
        }

        IXbox360Controller virtualPad = virtualPads[playerIndex];

        foreach ((PadControl control, Xbox360Button button) in ButtonBindings)
        {
            virtualPad.SetButtonState(button, state.IsPressed(control));
        }

        virtualPad.SetSliderValue(Xbox360Slider.LeftTrigger, ToTriggerValue(state.Get(PadControl.LeftTrigger)));
        virtualPad.SetSliderValue(Xbox360Slider.RightTrigger, ToTriggerValue(state.Get(PadControl.RightTrigger)));

        virtualPad.SetAxisValue(Xbox360Axis.LeftThumbX, ToStickValue(state.ResolveAxis(PadControl.LeftStickLeft, PadControl.LeftStickRight)));
        virtualPad.SetAxisValue(Xbox360Axis.LeftThumbY, ToStickValue(state.ResolveAxis(PadControl.LeftStickDown, PadControl.LeftStickUp)));
        virtualPad.SetAxisValue(Xbox360Axis.RightThumbX, ToStickValue(state.ResolveAxis(PadControl.RightStickLeft, PadControl.RightStickRight)));
        virtualPad.SetAxisValue(Xbox360Axis.RightThumbY, ToStickValue(state.ResolveAxis(PadControl.RightStickDown, PadControl.RightStickUp)));

        try
        {
            virtualPad.SubmitReport();
        }
        catch (Exception submissionFailure)
        {
            GD.PrintErr($"[InputLayer] virtual pad {playerIndex + 1} rejected a report: {DescribeFailure(submissionFailure)}");
        }
    }

    public void DestroyPads()
    {
        foreach (IXbox360Controller virtualPad in virtualPads)
        {
            try
            {
                virtualPad.Disconnect();
            }
            catch (Exception disconnectionFailure)
            {
                GD.PrintErr($"[InputLayer] a virtual pad would not unplug cleanly: {DescribeFailure(disconnectionFailure)}");
            }
        }

        virtualPads.Clear();
    }

    public IReadOnlyList<string> DescribeCreatedPads()
    {
        var descriptions = new List<string>();

        for (int playerIndex = 0; playerIndex < virtualPads.Count; playerIndex++)
        {
            descriptions.Add($"player {playerIndex + 1} -> virtual Xbox 360 pad");
        }

        return descriptions;
    }

    public void Dispose()
    {
        DestroyPads();
        vigemClient?.Dispose();
        vigemClient = null;
    }

    private static short ToStickValue(float axisValue)
    {
        return (short)Mathf.Clamp(axisValue * StickFullDeflection, -StickFullDeflection, StickFullDeflection);
    }

    private static byte ToTriggerValue(float triggerValue)
    {
        return (byte)Mathf.Clamp(triggerValue * TriggerFullPress, 0, TriggerFullPress);
    }

    private static string DescribeFailure(Exception failure)
    {
        int win32ErrorCode = failure is System.ComponentModel.Win32Exception win32Failure ? win32Failure.NativeErrorCode : 0;
        return $"{failure.GetType().Name} (win32 {win32ErrorCode}): {failure.Message}";
    }
}
