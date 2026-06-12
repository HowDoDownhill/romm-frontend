using Godot;
using System.Collections.Generic;

public partial class DownloadProgressUI : Panel
{
    [Export] private VBoxContainer _downloadsVBox;
    [Export] private PackedScene _downloadEntryScene;
	
    private Dictionary<string, DownloadEntryUI> _downloadEntries = new Dictionary<string, DownloadEntryUI>();
    private DownloadManager _downloadManager;

    public override void _Ready()
    {
        _downloadManager = GetNode<DownloadManager>("/root/DownloadManager");
        _downloadManager.DownloadProgressUpdated += OnDownloadProgressUpdated;
        _downloadManager.DownloadCompleted += OnDownloadCompleted;
    }

    private void OnDownloadProgressUpdated(string fileName, long current, long total)
    {
        if (!_downloadEntries.ContainsKey(fileName))
        {
            if (_downloadEntryScene == null)
            {
                GD.PrintErr("DownloadProgressUI: DownloadEntryScene is not assigned!");
                return;
            }

            var entryUI = _downloadEntryScene.Instantiate<DownloadEntryUI>();
            _downloadsVBox.AddChild(entryUI);
            entryUI.SetFileName(fileName);
            
            _downloadEntries[fileName] = entryUI;
        }

        var entry = _downloadEntries[fileName];
        entry.UpdateProgress(current, total);
    }

    public void SetDownloadStatus(string fileName, string status)
    {
        if (_downloadEntries.TryGetValue(fileName, out var entry))
        {
            entry.SetStatus(status);
        }
    }

    private void OnDownloadCompleted(string fileName)
    {
        if (_downloadEntries.TryGetValue(fileName, out var entry))
        {
            entry.QueueFree();
            _downloadEntries.Remove(fileName);
        }
    }
}
