using Godot;
using System.Collections.Generic;

public static class FocusCycler
{
    public static void Cycle(Control container, int direction)
    {
        if (container == null) return;

        List<Control> focusableChildren = new List<Control>();
        Gather(container, focusableChildren);

        if (focusableChildren.Count == 0) return;

        var focusOwner = container.GetViewport().GuiGetFocusOwner();
        int currentIndex = focusOwner != null ? focusableChildren.IndexOf(focusOwner) : -1;

        if (currentIndex == -1)
        {
            focusableChildren[0].GrabFocus();
            return;
        }

        int nextIndex = currentIndex + direction;

        if (nextIndex < 0)
        {
            nextIndex = focusableChildren.Count - 1;
        }
        else if (nextIndex >= focusableChildren.Count)
        {
            nextIndex = 0;
        }

        focusableChildren[nextIndex].GrabFocus();
    }

    public static Control FindFirstFocusable(Node node)
    {
        if (node is Control c && c.FocusMode != Control.FocusModeEnum.None && c.Visible)
        {
            return c;
        }

        foreach (Node child in node.GetChildren())
        {
            Control found = FindFirstFocusable(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void Gather(Node parent, List<Control> list)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Control c)
            {
                if (!c.Visible)
                {
                    continue;
                }

                if (c is BaseButton disableableButton && disableableButton.Disabled)
                {
                    continue;
                }

                if (c.FocusMode != Control.FocusModeEnum.None)
                {
                    list.Add(c);
                }
            }

            Gather(child, list);
        }
    }
}
