using Godot;
using System.Collections.Generic;

public partial class DownloadProgressUI : Panel
{
    [Export] private VBoxContainer downloadsVBox;
    [Export] private PackedScene downloadEntryScene;
    [Export] private Button cancelDownloadButton;
    
    private Dictionary<string, DownloadEntryUI> downloadEntries = new Dictionary<string, DownloadEntryUI>();
    private string currentlySelectedFile;
    private AppInstance appInstance;

    public void CycleSelection(int direction)
    {
        var children = downloadsVBox.GetChildren();

        if (children.Count == 0)
        {
            return;
        }

        var entries = new List<DownloadEntryUI>();

        foreach (var child in children)
        {
            if (child is DownloadEntryUI entry)
            {
                entries.Add(entry);
            }
            else if (child is MarginContainer mc && mc.GetChildCount() > 0 && mc.GetChild(0) is DownloadEntryUI mcEntry)
            {
                entries.Add(mcEntry);
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        DownloadEntryUI current = null;

        if (!string.IsNullOrEmpty(currentlySelectedFile) && downloadEntries.TryGetValue(currentlySelectedFile, out var e))
        {
            current = e;
        }

        int index = -1;

        if (current != null)
        {
            index = entries.IndexOf(current);
        }

        if (index == -1)
        {
            entries[0].GrabFocus();
            return;
        }

        index += direction;

        if (index < 0)
        {
            index = entries.Count - 1;
        }

        if (index >= entries.Count)
        {
            index = 0;
        }

        entries[index].GrabFocus();
    }

    public void CancelSelectedDownload()
    {
        OnCancelDownloadButtonPressed();
    }

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        
        appInstance.downloadManager.DownloadProgressUpdated += OnDownloadProgressUpdated;
        appInstance.downloadManager.DownloadCompleted += OnDownloadCompleted;
        appInstance.downloadManager.DownloadStageChanged += OnDownloadStageChanged;

        if (cancelDownloadButton != null)
        {
            cancelDownloadButton.Pressed += OnCancelDownloadButtonPressed;
        }
    }

    public override void _ExitTree()
    {
        if (appInstance?.downloadManager == null)
        {
            return;
        }

        appInstance.downloadManager.DownloadProgressUpdated -= OnDownloadProgressUpdated;
        appInstance.downloadManager.DownloadCompleted -= OnDownloadCompleted;
        appInstance.downloadManager.DownloadStageChanged -= OnDownloadStageChanged;
    }

    private void OnDownloadProgressUpdated(string fileName, long current, long total, string gameId)
    {
        if (!downloadEntries.ContainsKey(fileName))
        {
            if (downloadEntryScene == null)
            {
                GD.PrintErr("DownloadProgressUI: DownloadEntryScene is not assigned!");
                return;
            }

            var entryUi = downloadEntryScene.Instantiate<DownloadEntryUI>();
            var entryWrapper = new MarginContainer();
            entryWrapper.AddThemeConstantOverride("margin_left", 5);
            entryWrapper.AddThemeConstantOverride("margin_right", 5);
            entryWrapper.AddThemeConstantOverride("margin_top", 5);
            entryWrapper.AddThemeConstantOverride("margin_bottom", 5);
            entryWrapper.AddChild(entryUi);
            downloadsVBox.AddChild(entryWrapper);
            
            entryUi.SetFileName(fileName, gameId);
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

    private void OnDownloadStageChanged(string fileName, string stageDescription)
    {
        if (downloadEntries.TryGetValue(fileName, out var entry))
        {
            entry.SetStage(stageDescription);
        }
    }

    private void OnDownloadCompleted(string fileName, bool success)
    {
        if (downloadEntries.TryGetValue(fileName, out var entry))
        {
            var wrapper = entry.GetParent();
            if (wrapper != null && wrapper is MarginContainer)
            {
                wrapper.QueueFree();
            }
            else
            {
                entry.QueueFree();
            }
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
