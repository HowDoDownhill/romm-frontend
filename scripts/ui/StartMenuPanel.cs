using Godot;

[GlobalClass]
public partial class StartMenuPanel : UiPanel
{
    [Export] public Control menuView;
    [Export] public Control biosView;
    [Export] public Control optionsList;
    [Export] public VBoxContainer biosList;

    private Control focusBeforeBiosView;

    [Signal]
    public delegate void BiosViewRequestedEventHandler();

    public bool IsBiosViewOpen => biosView != null && biosView.Visible;

    public void ShowMenuView()
    {
        if (menuView != null) menuView.Visible = true;
        if (biosView != null) biosView.Visible = false;

        if (GodotObject.IsInstanceValid(focusBeforeBiosView))
        {
            focusBeforeBiosView.GrabFocus();
        }
        else
        {
            FocusCycler.Cycle(optionsList, 0);
        }

        focusBeforeBiosView = null;
    }

    public void ShowBiosView()
    {
        focusBeforeBiosView = GetViewport().GuiGetFocusOwner();

        if (menuView != null) menuView.Visible = false;
        if (biosView != null) biosView.Visible = true;

        EmitSignal(SignalName.BiosViewRequested);
    }

    protected override void OnOpened()
    {
        FocusCycler.Cycle(optionsList, 0);
    }

    public override void HandleInput(InputEvent inputEvent)
    {
        if (State == PanelState.Closing) return;

        Control activeList = IsBiosViewOpen ? biosView : optionsList;

        if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("Back"))
        {
            if (IsBiosViewOpen) ShowMenuView();
            else Close();
            return;
        }

        if (inputEvent.IsActionPressed("ui_up", true) || inputEvent.IsActionPressed("MoveUp"))
        {
            FocusCycler.Cycle(activeList, -1);
            return;
        }

        if (inputEvent.IsActionPressed("ui_down", true) || inputEvent.IsActionPressed("MoveDown"))
        {
            FocusCycler.Cycle(activeList, 1);
            return;
        }

        if (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("Select"))
        {
            if (GetViewport().GuiGetFocusOwner() is BaseButton focusedButton && !focusedButton.Disabled)
            {
                focusedButton.EmitSignal(BaseButton.SignalName.Pressed);
            }
        }
    }
}
