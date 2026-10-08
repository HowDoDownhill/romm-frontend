using Godot;

[GlobalClass]
public partial class StartMenuPanel : UiPanel
{
    [Export] public Control menuView;
    [Export] public Control biosView;
    [Export] public Control optionsList;
    [Export] public VBoxContainer biosList;

    [ExportGroup("Options")]
    [Export] public Button launchEmulatorButton;
    [Export] public Button updateEmulatorButton;
    [Export] public Button uninstallEmulatorButton;
    [Export] public Button selectBiosButton;
    [Export] public Button favoriteGameButton;
    [Export] public Button hostNetplayButton;
    [Export] public Button joinNetplayButton;
    [Export] public Control netplayView;
    [Export] public Label netplayCodeLabel;
    [Export] public Label netplayInfoLabel;
    [Export] public Button assignControllersButton;
    [Export] public Button settingsButton;
    [Export] public Button randomGameButton;
    [Export] public Button refreshAllGamesButton;
    [Export] public Button refreshCurrentSystemButton;
    [Export] public Button quitButton;

    private Control focusBeforeBiosView;

    [Signal]
    public delegate void BiosViewRequestedEventHandler();

    [Signal]
    public delegate void NetplayCancelRequestedEventHandler();

    public bool IsBiosViewOpen => biosView != null && biosView.Visible;

    private static readonly Vector2 MinimumPanelSize = new Vector2(400, 600);
    private const float PanelInset = 20.0f;

    public override void _Ready()
    {
        base._Ready();
        AboutToOpen += FitPanelToMenu;
    }

    private void FitPanelToMenu()
    {
        Vector2 contentSize = menuView != null ? menuView.GetCombinedMinimumSize() + new Vector2(PanelInset, PanelInset) : Vector2.Zero;
        Vector2 panelSize = new Vector2(Mathf.Max(MinimumPanelSize.X, contentSize.X), Mathf.Max(MinimumPanelSize.Y, contentSize.Y));

        OffsetLeft = -panelSize.X / 2.0f;
        OffsetRight = panelSize.X / 2.0f;
        OffsetTop = -panelSize.Y / 2.0f;
        OffsetBottom = panelSize.Y / 2.0f;
    }

    public void ShowMenuView()
    {
        FitPanelToMenu();
        if (menuView != null) menuView.Visible = true;
        if (biosView != null) biosView.Visible = false;
        if (netplayView != null) netplayView.Visible = false;

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
        if (netplayView != null) netplayView.Visible = false;

        EmitSignal(SignalName.BiosViewRequested);
    }

    public void ShowNetplayView(string joinCode, string joinInformation)
    {
        focusBeforeBiosView = GetViewport().GuiGetFocusOwner();

        if (menuView != null) menuView.Visible = false;
        if (biosView != null) biosView.Visible = false;
        if (netplayView != null) netplayView.Visible = true;

        if (netplayCodeLabel != null) netplayCodeLabel.Text = joinCode;
        if (netplayInfoLabel != null) netplayInfoLabel.Text = joinInformation;
    }

    public bool IsNetplayViewOpen => netplayView != null && netplayView.Visible;

    protected override void OnOpened()
    {
        FocusCycler.Cycle(optionsList, 0);
    }

    public override bool HandleInput(InputEvent inputEvent)
    {
        if (State == PanelState.Closing) return true;

        Control activeList = optionsList;

        if (IsBiosViewOpen) activeList = biosView;
        else if (IsNetplayViewOpen) activeList = netplayView;

        if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("Back"))
        {
            if (IsBiosViewOpen) ShowMenuView();
            else if (IsNetplayViewOpen) EmitSignal(SignalName.NetplayCancelRequested);
            else Close();
            return true;
        }

        if (inputEvent.IsActionPressed("ui_up", true) || inputEvent.IsActionPressed("MoveUp"))
        {
            FocusCycler.Cycle(activeList, -1);
            return true;
        }

        if (inputEvent.IsActionPressed("ui_down", true) || inputEvent.IsActionPressed("MoveDown"))
        {
            FocusCycler.Cycle(activeList, 1);
            return true;
        }

        if (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("Select"))
        {
            if (GetViewport().GuiGetFocusOwner() is BaseButton focusedButton && !focusedButton.Disabled)
            {
                focusedButton.EmitSignal(BaseButton.SignalName.Pressed);
            }
        }

        return true;
    }
}
