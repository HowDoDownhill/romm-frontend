using Godot;

public partial class DownloadEntryUI : MarginContainer
{
    [Signal]
    public delegate void EntrySelectedEventHandler(string fileName);

    [Export] private Label nameLabel;
    [Export] private Label statusLabel;
    [Export] private ProgressBar progressBar;
    [Export] private PanelContainer backgroundPanel;
    
    private StyleBoxFlat backgroundStyle;
    private string stageDescription;

    public string FileName { get; private set; }
    public string GameId { get; private set; }
    private AppInstance appInstance;

    private const float EntryHorizontalPadding = 16.0f;
    private const float EntryVerticalPadding = 10.0f;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        FocusMode = FocusModeEnum.All;
        
        if (backgroundPanel != null)
        {
            backgroundStyle = FocusHighlight.CreateStyle(EntryHorizontalPadding, EntryVerticalPadding);
            backgroundPanel.AddThemeStyleboxOverride("panel", backgroundStyle);
        }

        SetChildrenMousePass(this);

        GuiInput += OnGuiInput;
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
        
        Unhighlight();
    }

    private void SetChildrenMousePass(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control control)
            {
                control.MouseFilter = MouseFilterEnum.Pass;
            }

            SetChildrenMousePass(child);
        }
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

    public void SetFileName(string fileName, string gameId = null)
    {
        FileName = fileName;
        GameId = gameId;

        if (nameLabel != null)
        {
            nameLabel.Text = DownloadProgressDisplay.DescribeEntryName(fileName);
        }
    }

    public void UpdateProgress(long current, long total)
    {
        if (!string.IsNullOrEmpty(stageDescription))
        {
            return;
        }

        DownloadProgressDisplay.ApplyTo(progressBar, current, total);
        SetStatus(DownloadProgressDisplay.DescribeProgress(current, total));
    }

    public void SetStage(string stage)
    {
        stageDescription = stage;

        if (string.IsNullOrEmpty(stageDescription))
        {
            return;
        }

        DownloadProgressDisplay.ApplyTo(progressBar, 0, 0);
        SetStatus(stageDescription);
    }

    private void SetStatus(string status)
    {
        if (statusLabel != null)
        {
            statusLabel.Text = status;
        }
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
