using Godot;
using System.Collections.Generic;

public static class SelectionHighlighter
{
    private static readonly Dictionary<ulong, SelectionIndicator> indicatorsByHost = new Dictionary<ulong, SelectionIndicator>();
    private static readonly List<ulong> hostsToRemove = new List<ulong>();
    private static Control focusedListButton;

    public static void Attach(Viewport viewport)
    {
        viewport.GuiFocusChanged += OnFocusChanged;
        viewport.GetTree().ProcessFrame += OnProcessFrame;
    }

    public static void Set(Control item, Control area, bool selected, float grow = 0.0f)
    {
        if (!GodotObject.IsInstanceValid(item) || !GodotObject.IsInstanceValid(area) || item.GetParent() is not Control host || !host.IsInsideTree())
        {
            return;
        }

        if (selected)
        {
            if (!indicatorsByHost.TryGetValue(host.GetInstanceId(), out SelectionIndicator indicator))
            {
                indicator = new SelectionIndicator(host);
                indicatorsByHost[host.GetInstanceId()] = indicator;
            }

            indicator.MoveTo(area, grow);
        }

        else if (indicatorsByHost.TryGetValue(host.GetInstanceId(), out SelectionIndicator indicator))
        {
            indicator.Release(area);
        }
    }

    private static void OnProcessFrame()
    {
        double delta = Engine.GetMainLoop() is SceneTree tree ? tree.Root.GetProcessDeltaTime() : 0.016;
        hostsToRemove.Clear();

        foreach (var entry in indicatorsByHost)
        {
            if (!entry.Value.IsHostValid)
            {
                entry.Value.Free();
                hostsToRemove.Add(entry.Key);
                continue;
            }

            entry.Value.Update(delta);
        }

        foreach (ulong hostId in hostsToRemove)
        {
            indicatorsByHost.Remove(hostId);
        }
    }

    private static void OnFocusChanged(Control focused)
    {
        if (GodotObject.IsInstanceValid(focusedListButton) && focusedListButton != focused)
        {
            Set(focusedListButton, focusedListButton, false);
        }

        focusedListButton = IsListButton(focused) ? focused : null;

        if (focusedListButton != null)
        {
            Set(focusedListButton, focusedListButton, true);
        }
    }

    private static bool IsListButton(Control control)
    {
        return control is Button button
            && (button.ThemeTypeVariation == "ListButton" || button.ThemeTypeVariation == "TitleButton");
    }
}
