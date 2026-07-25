using Godot;

[GlobalClass]
public partial class ProgressPanel : UiPanel
{
    [Export] public Label statusLabel;
    [Export] public ProgressBar progressBar;

    public void ShowStatus(string status, float percent = 0f)
    {
        SetStatus(status);
        SetProgress(percent);
        Open();
    }

    public void SetStatus(string status)
    {
        if (statusLabel != null) statusLabel.Text = status;
    }

    public void SetProgress(float percent)
    {
        if (progressBar != null) progressBar.Value = percent;
    }
}
