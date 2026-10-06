using System;
using System.Collections.Generic;

public enum PadControl
{
    FaceSouth,
    FaceEast,
    FaceWest,
    FaceNorth,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
    Back,
    Start,
    Guide,
    LeftStick,
    RightStick,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight
}

public static class PadControls
{
    public static readonly PadControl[] All = (PadControl[])Enum.GetValues(typeof(PadControl));
    public static readonly int Count = All.Length;

    private static readonly Dictionary<string, PadControl> ControlsByStandardName = BuildControlsByStandardName();
    private static readonly string[] StandardNamesByControl = BuildStandardNamesByControl();

    private static Dictionary<string, PadControl> BuildControlsByStandardName()
    {
        var controlsByName = new Dictionary<string, PadControl>(StringComparer.OrdinalIgnoreCase);

        foreach (PadControl control in All)
        {
            controlsByName[ToStandardName(control)] = control;
            controlsByName[control.ToString()] = control;
        }

        return controlsByName;
    }

    private static string[] BuildStandardNamesByControl()
    {
        var namesByControl = new string[Count];

        foreach (PadControl control in All)
        {
            namesByControl[(int)control] = ToStandardName(control);
        }

        return namesByControl;
    }

    private static string ToStandardName(PadControl control)
    {
        switch (control)
        {
            case PadControl.LeftStickUp: return "LeftStick_Up";
            case PadControl.LeftStickDown: return "LeftStick_Down";
            case PadControl.LeftStickLeft: return "LeftStick_Left";
            case PadControl.LeftStickRight: return "LeftStick_Right";
            case PadControl.RightStickUp: return "RightStick_Up";
            case PadControl.RightStickDown: return "RightStick_Down";
            case PadControl.RightStickLeft: return "RightStick_Left";
            case PadControl.RightStickRight: return "RightStick_Right";
            default: return control.ToString();
        }
    }

    public static string GetStandardName(PadControl control)
    {
        return StandardNamesByControl[(int)control];
    }

    public static bool TryParseStandardName(string standardName, out PadControl control)
    {
        control = PadControl.FaceSouth;
        return !string.IsNullOrEmpty(standardName) && ControlsByStandardName.TryGetValue(standardName, out control);
    }
}
