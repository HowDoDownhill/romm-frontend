using Godot;
using System.Collections.Generic;

public partial class SystemJumpPopup : UiPanel
{
    private GridContainer gridContainer;
    private List<GameSystem> systems;

    private Color focusColor = new Color(1, 1, 1, 0.6f);

    [Signal]
    public delegate void SystemSelectedEventHandler(int index);

    public SystemJumpPopup()
    {
        BackdropDim = 0.7f;
    }

    public override void _Ready()
    {
        base._Ready();

        ZIndex = 200;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 30);
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_right", 30);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        ContentRoot.AddChild(margin);

        gridContainer = new GridContainer();
        gridContainer.Columns = 5;
        gridContainer.AddThemeConstantOverride("h_separation", 20);
        gridContainer.AddThemeConstantOverride("v_separation", 20);
        margin.AddChild(gridContainer);
    }

    public void Populate(List<GameSystem> systems, int currentIndex = 0)
    {
        this.systems = systems;

        foreach (Node child in gridContainer.GetChildren())
        {
            gridContainer.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < systems.Count; i++)
        {
            var system = systems[i];
            int index = i;

            var entryBtn = new Button();
            entryBtn.CustomMinimumSize = new Vector2(150, 150);
            StyleEntryButton(entryBtn);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset(LayoutPreset.FullRect);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            entryBtn.AddChild(vbox);

            Texture2D texture = null;
            if (!string.IsNullOrEmpty(system.IgdbSlug)) texture = FindPlatformIcon(system.IgdbSlug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
            if (texture == null && !string.IsNullOrEmpty(system.Slug)) texture = FindPlatformIcon(system.Slug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });

            if (texture != null)
            {
                var icon = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(100, 80) };
                vbox.AddChild(icon);
            }

            var label = new Label { Text = system.Name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(140, 0) };
            label.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(label);

            entryBtn.Pressed += () =>
            {
                EmitSignal(SignalName.SystemSelected, index);
            };

            gridContainer.AddChild(entryBtn);
        }
    }

    private void StyleEntryButton(Button entryBtn)
    {
        var btnStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
        var focusStyle = new StyleBoxFlat { BgColor = focusColor, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };

        entryBtn.AddThemeStyleboxOverride("normal", btnStyle);
        entryBtn.AddThemeStyleboxOverride("hover", focusStyle);
        entryBtn.AddThemeStyleboxOverride("focus", focusStyle);
        entryBtn.AddThemeStyleboxOverride("pressed", focusStyle);
    }

    public void ApplyTheme(Color accentColor)
    {
        accentColor.A = 0.65f;
        focusColor = accentColor;

        if (gridContainer != null)
        {
            foreach (Node child in gridContainer.GetChildren())
            {
                if (child is Button btn) StyleEntryButton(btn);
            }
        }
    }

    private Texture2D FindPlatformIcon(string stub, string basePath, string[] extensions)
    {
        foreach (var ext in extensions)
        {
            string path = $"{basePath}{stub}{ext}";
            if (ResourceLoader.Exists(path)) return (Texture2D)ResourceLoader.Load(path);
        }
        return null;
    }

    public void FocusSystem(int index)
    {
        if (gridContainer.GetChildCount() > index)
        {
            var btn = gridContainer.GetChild<Button>(index);
            btn.GrabFocus();
        }
    }

    public override void HandleInput(InputEvent @event)
    {
        if (State == PanelState.Closing) return;

        if (@event.IsActionPressed("Back") || @event.IsActionPressed("ui_cancel"))
        {
            Close();
            return;
        }

        if (@event.IsActionPressed("Select") || @event.IsActionPressed("ui_accept"))
        {
            int idx = GetFocusedIndex();
            if (idx >= 0) EmitSignal(SignalName.SystemSelected, idx);
            return;
        }

        int current = GetFocusedIndex();
        if (current < 0) return;

        int columns = gridContainer.Columns;
        int count = gridContainer.GetChildCount();
        int target = current;

        if (@event.IsActionPressed("ui_left", true))
        {
            if (current % columns > 0) target = current - 1;
        }
        else if (@event.IsActionPressed("ui_right", true))
        {
            if (current % columns < columns - 1 && current + 1 < count) target = current + 1;
        }
        else if (@event.IsActionPressed("MoveUp", true) || @event.IsActionPressed("ui_up", true))
        {
            if (current - columns >= 0) target = current - columns;
        }
        else if (@event.IsActionPressed("MoveDown", true) || @event.IsActionPressed("ui_down", true))
        {
            if (current + columns < count) target = current + columns;
        }

        if (target != current) FocusSystem(target);
    }

    private int GetFocusedIndex()
    {
        var focused = GetViewport().GuiGetFocusOwner();
        if (focused == null) return -1;
        for (int i = 0; i < gridContainer.GetChildCount(); i++)
        {
            if (gridContainer.GetChild(i) == focused) return i;
        }
        return -1;
    }
}
