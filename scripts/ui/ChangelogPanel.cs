using Godot;

[GlobalClass]
public partial class ChangelogPanel : UiPanel
{
    [Export] public RichTextLabel notesLabel;
    [Export] public Button acceptButton;
    [Export] public Button cancelButton;

    [Signal]
    public delegate void AcceptedEventHandler();

    [Signal]
    public delegate void DismissedEventHandler();

    public override void _Ready()
    {
        base._Ready();

        if (acceptButton != null)
        {
            acceptButton.Pressed += EmitAccepted;
            acceptButton.Icon = ControllerGlyph.For("Select");
        }

        if (cancelButton != null)
        {
            cancelButton.Pressed += EmitDismissed;
            cancelButton.Icon = ControllerGlyph.For("Back");
        }
    }

    public void ShowUpdate(string version, string releaseNotes)
    {
        if (notesLabel != null)
        {
            string sanitizedNotes = releaseNotes.Replace("\r", "").Replace("\b", "");
            notesLabel.Text = $"[b]A new version ({version}) of Romm Frontend is available.[/b]\n\nRelease Notes:\n{sanitizedNotes}";
        }

        if (acceptButton != null) acceptButton.Text = "Install";
        if (cancelButton != null) cancelButton.Text = "Close";

        Open();
    }

    public override void HandleInput(InputEvent inputEvent)
    {
        if (State == PanelState.Closing) return;

        if (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("Select"))
        {
            EmitAccepted();
        }
        else if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("Back"))
        {
            EmitDismissed();
        }
    }

    private void EmitAccepted()
    {
        EmitSignal(SignalName.Accepted);
    }

    private void EmitDismissed()
    {
        EmitSignal(SignalName.Dismissed);
    }
}
