using Godot;
using System.Collections.Generic;

public class UiPanelStack
{
    private readonly List<UiPanel> openPanels = new List<UiPanel>();

    public bool HasOpenPanel => TopPanel != null;

    public UiPanel TopPanel
    {
        get
        {
            for (int i = openPanels.Count - 1; i >= 0; i--)
            {
                if (GodotObject.IsInstanceValid(openPanels[i])) return openPanels[i];
                openPanels.RemoveAt(i);
            }
            return null;
        }
    }

    public void Register(UiPanel panel)
    {
        if (panel == null) return;

        panel.AboutToOpen += () => Push(panel);
        panel.AboutToClose += () => openPanels.Remove(panel);
        panel.TreeExiting += () => openPanels.Remove(panel);
    }

    public void HandleInput(InputEvent inputEvent)
    {
        TopPanel?.HandleInput(inputEvent);
    }

    private void Push(UiPanel panel)
    {
        openPanels.Remove(panel);
        openPanels.Add(panel);
    }
}
