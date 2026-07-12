using Godot;

public partial class SettingsSectionNavEntry : MarginContainer
{
    [Signal]
    public delegate void EntrySelectedEventHandler(string sectionName);

    public string SectionName { get; private set; }
    
    private Label _label;
    private PanelContainer _backgroundPanel;
    private StyleBoxFlat _backgroundStyle;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        
        _backgroundPanel = GetNode<PanelContainer>("PanelContainer");
        _label = GetNode<Label>("PanelContainer/MarginContainer/Label");
        if (!string.IsNullOrEmpty(SectionName)) _label.Text = SectionName;

        _backgroundStyle = new StyleBoxFlat();
        _backgroundStyle.BgColor = new Color(0, 0, 0, 0);
        _backgroundStyle.DrawCenter = false;
        _backgroundPanel.AddThemeStyleboxOverride("panel", _backgroundStyle);

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
        if (_label != null) _label.Text = sectionName;
    }

    public void Highlight()
    {
        if (_backgroundStyle != null)
        {
            _backgroundStyle.BgColor = new Color(1f, 1f, 1f, 0.5f);
            _backgroundStyle.DrawCenter = true;
        }
    }

    public void Unhighlight()
    {
        if (_backgroundStyle != null)
        {
            _backgroundStyle.BgColor = new Color(0, 0, 0, 0);
            _backgroundStyle.DrawCenter = false;
        }
    }
}
