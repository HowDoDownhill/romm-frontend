using Godot;

public partial class DownloadEntryUI : MarginContainer
{
    [Signal]
    public delegate void EntrySelectedEventHandler(string fileName);

    [Export] private Label _nameLabel;
    [Export] private Label _statusLabel;
    [Export] private ProgressBar _progressBar;
    [Export] private Panel _backgroundPanel;
    
    private StyleBoxFlat _backgroundStyle;

    public string FileName { get; private set; }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        
        if (_backgroundPanel != null)
        {
            _backgroundStyle = new StyleBoxFlat();
            _backgroundPanel.AddThemeStyleboxOverride("panel", _backgroundStyle);
        }

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
        EmitSignal(SignalName.EntrySelected, FileName);
        Highlight();
    }

    private void OnFocusExited()
    {
        Unhighlight();
    }

    public void SetFileName(string fileName)
    {
        FileName = fileName;
        if (_nameLabel != null)
        {
            _nameLabel.Text = fileName.GetBaseName();
        }
    }

    public void UpdateProgress(long current, long total)
    {
        if (_progressBar != null)
        {
            double percentage = total > 0 ? (double)current / total * 100 : 0;
            _progressBar.Value = percentage;
        }
        
        SetStatus("Downloading...");
    }

    public void SetStatus(string status)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = status;
        }
    }

    public void Highlight()
    {
        if (_backgroundStyle != null)
        {
            _backgroundStyle.BgColor = new Color(0.3f, 0.3f, 0.4f);
        }
    }

    public void Unhighlight()
    {
        if (_backgroundStyle != null)
        {
            _backgroundStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.5f);
        }
    }
}
