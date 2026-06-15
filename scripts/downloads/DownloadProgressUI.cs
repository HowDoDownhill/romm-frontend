using Godot;
using System.Collections.Generic;

public partial class DownloadProgressUI : Panel
{
    [Export] private VBoxContainer downloadsVBox;
    [Export] private PackedScene _downloadEntryScene;
    [Export] private Button cancelDownloadButton;
    
    private Dictionary<string, DownloadEntryUI> downloadEntries = new Dictionary<string, DownloadEntryUI>();
    private string currentlySelectedFile;

    private AppInstance appInstance;
    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        
        appInstance.downloadManager.DownloadProgressUpdated += OnDownloadProgressUpdated;
        appInstance.downloadManager.DownloadCompleted += OnDownloadCompleted;

        if (cancelDownloadButton != null)
        {
            cancelDownloadButton.Pressed += OnCancelDownloadButtonPressed;
        }
    }

    private void OnDownloadProgressUpdated(string fileName, long current, long total)
    {
        if (!downloadEntries.ContainsKey(fileName))
        {
            if (_downloadEntryScene == null)
            {
                GD.PrintErr("DownloadProgressUI: DownloadEntryScene is not assigned!");
                return;
            }

            var entryUi = _downloadEntryScene.Instantiate<DownloadEntryUI>();
            downloadsVBox.AddChild(entryUi);
            entryUi.SetFileName(fileName);
            entryUi.EntrySelected += OnEntrySelected;
            
            downloadEntries[fileName] = entryUi;
            
            if (string.IsNullOrEmpty(currentlySelectedFile))
            {
                OnEntrySelected(fileName);
            }
        }

        var entry = downloadEntries[fileName];
        entry.UpdateProgress(current, total);
    }

    private void OnEntrySelected(string selectedFile)
    {
        if (!string.IsNullOrEmpty(currentlySelectedFile) && downloadEntries.TryGetValue(currentlySelectedFile, out var oldEntry))
        {
            oldEntry.Unhighlight();
        }

        currentlySelectedFile = selectedFile;

        if (downloadEntries.TryGetValue(currentlySelectedFile, out var newEntry))
        {
            newEntry.Highlight();
        }
    }

    public void SetDownloadStatus(string fileName, string status)
    {
        if (downloadEntries.TryGetValue(fileName, out var entry))
        {
            entry.SetStatus(status);
        }
    }

    private void OnDownloadCompleted(string fileName, bool success)
    {
        if (downloadEntries.TryGetValue(fileName, out var entry))
        {
            entry.QueueFree();
            downloadEntries.Remove(fileName);
            
            if (currentlySelectedFile == fileName)
            {
                currentlySelectedFile = null;
            }
        }
    }

    private void OnCancelDownloadButtonPressed()
    {
        if (!string.IsNullOrEmpty(currentlySelectedFile))
        {
            appInstance.downloadManager.CancelDownload(currentlySelectedFile);
        }
    }
}
