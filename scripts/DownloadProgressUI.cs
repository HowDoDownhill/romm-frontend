using Godot;
using System.Collections.Generic;

public partial class DownloadProgressUI : Panel
{
    [Export] private VBoxContainer _downloadsVBox;
	
    private Dictionary<string, ProgressBar> _progressBars = new Dictionary<string, ProgressBar>();
    private DownloadManager _downloadManager;

    public override void _Ready()
    {
        _downloadManager = GetNode<DownloadManager>("/root/DownloadManager");
        _downloadManager.DownloadProgressUpdated += OnDownloadProgressUpdated;
        _downloadManager.DownloadCompleted += OnDownloadCompleted;
    }

    private void OnDownloadProgressUpdated(string fileName, long current, long total)
    {
        if (!_progressBars.ContainsKey(fileName))
        {
            var progressBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0
            };
            var label = new Label { Text = fileName };
            _downloadsVBox.AddChild(label);
            _downloadsVBox.AddChild(progressBar);
            _progressBars[fileName] = progressBar;
        }

        double percentage = total > 0 ? (double)current / total * 100 : 0;
        _progressBars[fileName].Value = percentage;
    }

    private void OnDownloadCompleted(string fileName)
    {
        if (_progressBars.TryGetValue(fileName, out var progressBar))
        {
            var label = progressBar.GetParent().GetChild<Label>(progressBar.GetIndex() - 1);
            progressBar.QueueFree();
            label.QueueFree();
            _progressBars.Remove(fileName);
        }
    }
}