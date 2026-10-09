using Godot;

public partial class SettingsSectionNavEntry : MarginContainer
{
    [Signal]
    public delegate void EntrySelectedEventHandler(string sectionName);

    public string SectionName { get; private set; }
    
    private Label sectionNameLabel;
    private PanelContainer backgroundPanel;
    private StyleBoxFlat backgroundStyle;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        
        backgroundPanel = GetNode<PanelContainer>("PanelContainer");
        sectionNameLabel = GetNode<Label>("PanelContainer/MarginContainer/Label");
        if (!string.IsNullOrEmpty(SectionName)) sectionNameLabel.Text = SectionName;

        backgroundStyle = FocusHighlight.CreateStyle();
        backgroundPanel.AddThemeStyleboxOverride("panel", backgroundStyle);

        GuiInput += OnGuiInput;
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
        
        Unhighlight();
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            GrabFocus();
        }
    }

    private void OnFocusEntered()
    {
        EmitSignal(SignalName.EntrySelected, SectionName);
        Highlight();
    }

    private void OnFocusExited()
    {
        Unhighlight();
    }

    public void Setup(string sectionName)
    {
        SectionName = sectionName;
        if (sectionNameLabel != null) sectionNameLabel.Text = sectionName;
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
