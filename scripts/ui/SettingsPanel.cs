using Godot;

[GlobalClass]
public partial class SettingsPanel : UiPanel
{
    public MainSceneSettingsHandler Handler { get; set; }

    public override bool HandleInput(InputEvent inputEvent)
    {
        if (Handler == null) return false;

        Control focusOwner = GetViewport().GuiGetFocusOwner();
        bool isFocusInTree = focusOwner != null && Handler.SectionsTree != null && Handler.SectionsTree.IsAncestorOf(focusOwner);
        bool isFocusInOptions = focusOwner != null && Handler.OptionsContainer != null && Handler.OptionsContainer.IsAncestorOf(focusOwner);

        if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("Back"))
        {
            if (isFocusInOptions) FocusCycler.Cycle(Handler.SectionsTree, 0);
            else Handler.ToggleSettingsMenu();
            return true;
        }

        if (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("Select"))
        {
            if (isFocusInTree) return FocusVisibleForm();
            if (isFocusInOptions)
            {
                InteractWith(focusOwner);
                return true;
            }
            return false;
        }

        bool isDown = inputEvent.IsActionPressed("ui_down", true) || inputEvent.IsActionPressed("MoveDown");

        if (isDown || inputEvent.IsActionPressed("ui_up", true) || inputEvent.IsActionPressed("MoveUp"))
        {
            int direction = isDown ? 1 : -1;

            if (isFocusInTree)
            {
                FocusCycler.Cycle(Handler.SectionsTree, direction);
            }
            else if (isFocusInOptions)
            {
                Control visibleForm = Handler.GetVisibleSettingsForm();
                if (visibleForm != null) FocusCycler.Cycle(visibleForm, direction);
            }

            return true;
        }

        if (inputEvent.IsActionPressed("ui_left", true) || inputEvent.IsActionPressed("ui_right", true))
        {
            if (!isFocusInOptions) return false;

            int direction = inputEvent.IsAction("ui_right") ? 1 : -1;

            if (focusOwner is SettingsListEntry entry) entry.CycleWidget(direction);
            else Handler.CycleFocusedOption(direction);

            return true;
        }

        return false;
    }

    private bool FocusVisibleForm()
    {
        Control visibleForm = Handler.GetVisibleSettingsForm();
        if (visibleForm == null) return false;

        Control firstFocusable = FocusCycler.FindFirstFocusable(visibleForm);
        if (firstFocusable == null) return false;

        firstFocusable.GrabFocus();
        return true;
    }

    private static void InteractWith(Control focusOwner)
    {
        if (focusOwner is SettingsListEntry entry)
        {
            entry.InteractWithWidget();
            return;
        }

        if (focusOwner is not BaseButton button) return;

        if (button is CheckButton checkButton)
        {
            checkButton.ButtonPressed = !checkButton.ButtonPressed;
            checkButton.EmitSignal(BaseButton.SignalName.Toggled, checkButton.ButtonPressed);
        }
        else if (button is OptionButton optionButton)
        {
            optionButton.ShowPopup();
        }
        else
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }
}
