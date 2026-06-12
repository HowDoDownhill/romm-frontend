using Godot;

public partial class DownloadEntryUI : MarginContainer
{
    [Export] private Label _nameLabel;
    [Export] private Label _statusLabel;
    [Export] private ProgressBar _progressBar;

    public void SetFileName(string fileName)
    {
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
}
