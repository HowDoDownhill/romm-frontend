using Godot;

public partial class SettingsListEntry : MarginContainer
{
    private StyleBoxFlat backgroundStyle;
    private Control interactableWidget;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;

        backgroundStyle = FocusHighlight.CreateStyle();

        var panel = GetNodeOrNull<PanelContainer>("PanelContainer");
        if (panel != null)
        {
            panel.AddThemeStyleboxOverride("panel", backgroundStyle);
        }

        GuiInput += OnGuiInput;
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;

        Unhighlight();

        CallDeferred(nameof(FindWidget));
    }

    private void FindWidget()
    {
        interactableWidget = FindInteractableWidget(this);
        if (interactableWidget != null)
        {
            interactableWidget.FocusMode = FocusModeEnum.None;
        }
    }

    private Control FindInteractableWidget(Node node)
    {
        if (node is CarouselButton || node is OptionButton || node is CheckButton || node is SpinBox || node is LineEdit || node is Button)
        {
            return node as Control;
        }
        foreach (Node child in node.GetChildren())
        {
            var res = FindInteractableWidget(child);
            if (res != null) return res;
        }
        return null;
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            GrabFocus();
        }
    }

    public void CycleWidget(int direction)
    {
        if (interactableWidget == null) return;

        if (interactableWidget is CarouselButton carBtn)
        {
            carBtn.Step(direction);
        }
        else if (interactableWidget is OptionButton optBtn)
        {
            if (optBtn.ItemCount == 0) return;
            int newIdx = optBtn.Selected + direction;
            if (newIdx < 0) newIdx = optBtn.ItemCount - 1;
            if (newIdx >= optBtn.ItemCount) newIdx = 0;
            optBtn.Select(newIdx);
            optBtn.EmitSignal(OptionButton.SignalName.ItemSelected, newIdx);
        }
        else if (interactableWidget is SpinBox spinBox)
        {
            double step = spinBox.Step > 0 ? spinBox.Step : 1;
            double newValue = spinBox.Value + (direction * step);
            if (newValue < spinBox.MinValue) newValue = spinBox.MaxValue;
            if (newValue > spinBox.MaxValue) newValue = spinBox.MinValue;
            spinBox.Value = newValue;
        }
    }

    public void InteractWithWidget()
    {
        if (interactableWidget == null) return;

        if (interactableWidget is CarouselButton carBtn)
        {
        }
        else if (interactableWidget is OptionButton optBtn)
        {
            var popup = optBtn.GetPopup();
            if (popup != null)
            {
                popup.Position = (Vector2I)optBtn.GlobalPosition + new Vector2I(0, (int)optBtn.Size.Y);
                popup.Show();
            }
        }
        else if (interactableWidget is BaseButton btn)
        {
            if (btn.ToggleMode)
            {
                btn.ButtonPressed = !btn.ButtonPressed;
                btn.EmitSignal(BaseButton.SignalName.Toggled, btn.ButtonPressed);
            }
            else
            {
                btn.EmitSignal(BaseButton.SignalName.Pressed);
            }
        }
    }

    private void OnFocusEntered()
    {
        Highlight();
    }

    private void OnFocusExited()
    {
        Unhighlight();
    }

    public void Highlight()
    {
        FocusHighlight.Set(this, backgroundStyle, true);
    }

    public void Unhighlight()
    {
        FocusHighlight.Set(this, backgroundStyle, false);
    }
}
