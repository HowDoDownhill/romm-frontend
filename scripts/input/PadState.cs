using System;

public class PadState
{
    public const float DigitalPressThreshold = 0.5f;

    private readonly float[] controlValues = new float[PadControls.Count];

    public float Get(PadControl control)
    {
        return controlValues[(int)control];
    }

    public void Set(PadControl control, float value)
    {
        controlValues[(int)control] = value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
    }

    public void Raise(PadControl control, float value)
    {
        if (value > Get(control))
        {
            Set(control, value);
        }
    }

    public bool IsPressed(PadControl control)
    {
        return Get(control) >= DigitalPressThreshold;
    }

    public float ResolveAxis(PadControl negativeControl, PadControl positiveControl)
    {
        return Get(positiveControl) - Get(negativeControl);
    }

    public void Clear()
    {
        Array.Clear(controlValues, 0, controlValues.Length);
    }

    public void CopyFrom(PadState source)
    {
        Array.Copy(source.controlValues, controlValues, controlValues.Length);
    }

    public bool Matches(PadState other)
    {
        for (int controlIndex = 0; controlIndex < controlValues.Length; controlIndex++)
        {
            if (controlValues[controlIndex] != other.controlValues[controlIndex])
            {
                return false;
            }
        }

        return true;
    }
}
