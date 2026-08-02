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

        if (notesLabel != null)
        {
            notesLabel.BbcodeEnabled = true;
        }

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

    public enum PromptSubject
    {
        ApplicationUpdate,
        ControllerLayer
    }

    public PromptSubject ActiveSubject { get; private set; } = PromptSubject.ApplicationUpdate;

    public void ShowUpdate(string version, string releaseNotes)
    {
        ActiveSubject = PromptSubject.ApplicationUpdate;

        if (notesLabel != null)
        {
            string sanitizedNotes = EscapeMarkup(releaseNotes.Replace("\r", "").Replace("\b", ""));
            notesLabel.Text = $"[b]A new version ({version}) of Romm Frontend is available.[/b]\n\nRelease Notes:\n{sanitizedNotes}";
        }

        if (acceptButton != null) acceptButton.Text = "Install";
        if (cancelButton != null) cancelButton.Text = "Close";

        Open();
    }

    public void ShowControllerLayerOffer(string bodyText, string acceptText, string declineText)
    {
        ActiveSubject = PromptSubject.ControllerLayer;

        if (notesLabel != null)
        {
            notesLabel.Text = bodyText;
        }

        if (acceptButton != null) acceptButton.Text = acceptText;
        if (cancelButton != null) cancelButton.Text = declineText;

        Open();
    }

    public override bool HandleInput(InputEvent inputEvent)
    {
        if (State == PanelState.Closing) return true;

        if (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("Select"))
        {
            EmitAccepted();
        }
        else if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("Back"))
        {
            EmitDismissed();
        }

        return true;
    }

    private static string EscapeMarkup(string untrustedText)
    {
        return untrustedText.Replace("[", "[lb]");
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
