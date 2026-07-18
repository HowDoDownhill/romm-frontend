using Godot;
using System.Collections.Generic;

public partial class SystemCarousel : HBoxContainer
{
    [Export] private Label _leftArrow;
    [Export] private TextureRect _systemIcon;
    [Export] private Label _systemLabel;
    [Export] private Label _rightArrow;
    [Export] private Timer _debounceTimer;

    public List<GameSystem> Systems { get; private set; } = new List<GameSystem>();
    public int SelectedIndex { get; private set; } = -1;

    [Signal]
    public delegate void SystemSelectedEventHandler(int index);

    public override void _Ready()
    {
        if (_debounceTimer != null)
        {
            _debounceTimer.Timeout += OnDebounceTimerTimeout;
        }
    }

    public void Populate(List<GameSystem> systems, int defaultIndex = 0)
    {
        Systems = systems;
        if (Systems.Count > 0)
        {
            SetSelectionSilently(defaultIndex);
        }
    }

    public void Next()
    {
        if (Systems.Count == 0) return;
        int newIndex = SelectedIndex + 1;
        if (newIndex >= Systems.Count) newIndex = 0;
        SetSelectionWithTimer(newIndex);
    }

    public void Previous()
    {
        if (Systems.Count == 0) return;
        int newIndex = SelectedIndex - 1;
        if (newIndex < 0) newIndex = Systems.Count - 1;
        SetSelectionWithTimer(newIndex);
    }

    public void SetSelectionSilently(int index)
    {
        if (index >= 0 && index < Systems.Count)
        {
            SelectedIndex = index;
            UpdateVisuals();
            _debounceTimer.Stop(); // Cancel any pending timers since we are setting silently
        }
    }

    private void SetSelectionWithTimer(int index)
    {
        if (index >= 0 && index < Systems.Count)
        {
            SelectedIndex = index;
            UpdateVisuals();
            _debounceTimer.Start();
        }
    }

    public void UpdateVisuals()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Systems.Count) return;
        
        var system = Systems[SelectedIndex];
        _systemLabel.Text = system.Name;
        
        Texture2D texture = null;
        if (!string.IsNullOrEmpty(system.IgdbSlug))
        {
            texture = FindPlatformIcon(system.IgdbSlug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
        }
        
        if (texture == null && !string.IsNullOrEmpty(system.Slug))
        {
            texture = FindPlatformIcon(system.Slug, "res://assets/platforms/titles/", new[] { ".svg", ".png" });
        }

        if (texture != null)
        {
            _systemIcon.Texture = texture;
            _systemIcon.Visible = true;
            _systemLabel.Text = "";
            _systemLabel.Visible = false;
        }
        else
        {
            _systemIcon.Texture = null;
            _systemIcon.Visible = false;
            _systemLabel.Text = system.Name;
            _systemLabel.Visible = true;
        }
    }

    private Texture2D FindPlatformIcon(string stub, string basePath, string[] extensions)
    {
        foreach (var ext in extensions)
        {
            string path = $"{basePath}{stub}{ext}";
            if (ResourceLoader.Exists(path))
            {
                return (Texture2D)ResourceLoader.Load(path);
            }
        }
        return null;
    }

    private void OnDebounceTimerTimeout()
    {
        EmitSignal(SignalName.SystemSelected, SelectedIndex);
    }

    public void SetOverrideText(string text)
    {
        _debounceTimer.Stop();
        _leftArrow.Visible = false;
        _rightArrow.Visible = false;
        _systemIcon.Visible = false;
        _systemLabel.Text = text;
        _systemLabel.Visible = true;
    }

    public void ClearOverride()
    {
        _leftArrow.Visible = true;
        _rightArrow.Visible = true;
        UpdateVisuals();
    }
}
