using Godot;

public class GodotPhysicalPadReader : IPhysicalPadReader
{
    private const float AxisDeadzone = 0.15f;

    public void ReadInto(int physicalDeviceId, PadState destination)
    {
        destination.Clear();

        if (physicalDeviceId < 0)
        {
            return;
        }

        ReadButton(physicalDeviceId, JoyButton.A, PadControl.FaceSouth, destination);
        ReadButton(physicalDeviceId, JoyButton.B, PadControl.FaceEast, destination);
        ReadButton(physicalDeviceId, JoyButton.X, PadControl.FaceWest, destination);
        ReadButton(physicalDeviceId, JoyButton.Y, PadControl.FaceNorth, destination);
        ReadButton(physicalDeviceId, JoyButton.LeftShoulder, PadControl.LeftShoulder, destination);
        ReadButton(physicalDeviceId, JoyButton.RightShoulder, PadControl.RightShoulder, destination);
        ReadButton(physicalDeviceId, JoyButton.Back, PadControl.Back, destination);
        ReadButton(physicalDeviceId, JoyButton.Start, PadControl.Start, destination);
        ReadButton(physicalDeviceId, JoyButton.Guide, PadControl.Guide, destination);
        ReadButton(physicalDeviceId, JoyButton.LeftStick, PadControl.LeftStick, destination);
        ReadButton(physicalDeviceId, JoyButton.RightStick, PadControl.RightStick, destination);
        ReadButton(physicalDeviceId, JoyButton.DpadUp, PadControl.DpadUp, destination);
        ReadButton(physicalDeviceId, JoyButton.DpadDown, PadControl.DpadDown, destination);
        ReadButton(physicalDeviceId, JoyButton.DpadLeft, PadControl.DpadLeft, destination);
        ReadButton(physicalDeviceId, JoyButton.DpadRight, PadControl.DpadRight, destination);

        destination.Set(PadControl.LeftTrigger, ApplyDeadzone(Input.GetJoyAxis(physicalDeviceId, JoyAxis.TriggerLeft)));
        destination.Set(PadControl.RightTrigger, ApplyDeadzone(Input.GetJoyAxis(physicalDeviceId, JoyAxis.TriggerRight)));

        ReadAxisAsDirections(physicalDeviceId, JoyAxis.LeftX, PadControl.LeftStickLeft, PadControl.LeftStickRight, destination);
        ReadAxisAsDirections(physicalDeviceId, JoyAxis.LeftY, PadControl.LeftStickUp, PadControl.LeftStickDown, destination);
        ReadAxisAsDirections(physicalDeviceId, JoyAxis.RightX, PadControl.RightStickLeft, PadControl.RightStickRight, destination);
        ReadAxisAsDirections(physicalDeviceId, JoyAxis.RightY, PadControl.RightStickUp, PadControl.RightStickDown, destination);
    }

    private static void ReadButton(int physicalDeviceId, JoyButton button, PadControl control, PadState destination)
    {
        destination.Set(control, Input.IsJoyButtonPressed(physicalDeviceId, button) ? 1.0f : 0.0f);
    }

    private static void ReadAxisAsDirections(int physicalDeviceId, JoyAxis axis, PadControl negativeControl, PadControl positiveControl, PadState destination)
    {
        float axisValue = ApplyDeadzone(Input.GetJoyAxis(physicalDeviceId, axis));

        destination.Set(negativeControl, axisValue < 0.0f ? -axisValue : 0.0f);
        destination.Set(positiveControl, axisValue > 0.0f ? axisValue : 0.0f);
    }

    private static float ApplyDeadzone(float axisValue)
    {
        return Mathf.Abs(axisValue) < AxisDeadzone ? 0.0f : axisValue;
    }
}
