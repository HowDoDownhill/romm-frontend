using Godot;
using System.Collections.Generic;

public partial class SystemCarousel : HBoxContainer
{
    [Export] private Label leftArrow;
    [Export] private TextureRect systemIcon;
    [Export] private Label systemLabel;
    [Export] private Label rightArrow;
    [Export] private Timer debounceTimer;
    [Export] private Control slider;

    private const float SlideDuration = 0.16f;

    private Tween slideTween;

    public List<GameSystem> Systems { get; private set; } = new List<GameSystem>();
    public int SelectedIndex { get; private set; } = -1;

    [Signal]
    public delegate void SystemSelectedEventHandler(int index);

    [Signal]
    public delegate void JumpRequestedEventHandler();

    [Signal]
    public delegate void CycledEventHandler();

    public override void _Ready()
    {
        if (debounceTimer != null)
        {
            debounceTimer.Timeout += OnDebounceTimerTimeout;
        }

        MakeClickable(leftArrow, ev => HandleArrowClick(ev, false));
        MakeClickable(rightArrow, ev => HandleArrowClick(ev, true));
        MakeClickable(systemIcon, HandleLogoClick);
        MakeClickable(systemLabel, HandleLogoClick);
    }

    private void MakeClickable(Control control, Control.GuiInputEventHandler onGuiInput)
    {
        if (control == null) return;
        control.MouseFilter = Control.MouseFilterEnum.Stop;
        control.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        control.GuiInput += onGuiInput;
    }

    private static bool IsLeftClick(InputEvent @event)
    {
        return @event is InputEventMouseButton mouseButton
            && mouseButton.Pressed
            && mouseButton.ButtonIndex == MouseButton.Left;
    }

    private void HandleArrowClick(InputEvent @event, bool moveNext)
    {
        if (!IsLeftClick(@event)) return;

        if (moveNext) Next();
        else Previous();

        EmitSignal(SignalName.Cycled);
        AcceptEvent();
    }

    private void HandleLogoClick(InputEvent @event)
    {
        if (!IsLeftClick(@event)) return;

        EmitSignal(SignalName.JumpRequested);
        AcceptEvent();
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
        SetSelectionWithTimer(newIndex, 1);
    }

    public void Previous()
    {
        if (Systems.Count == 0) return;
        int newIndex = SelectedIndex - 1;
        if (newIndex < 0) newIndex = Systems.Count - 1;
        SetSelectionWithTimer(newIndex, -1);
    }

    private void SlideInFrom(int direction)
    {
        if (slider == null || direction == 0) return;

        if (slideTween != null && slideTween.IsValid()) slideTween.Kill();

        float travel = slider.Size.X > 0 ? slider.Size.X : 64f;

        slider.Position = new Vector2(travel * direction, slider.Position.Y);

        Color transparent = slider.Modulate;
        transparent.A = 0f;
        slider.Modulate = transparent;

        slideTween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        slideTween.TweenProperty(slider, "position:x", 0f, SlideDuration);
        slideTween.TweenProperty(slider, "modulate:a", 1f, SlideDuration);
    }

    public void SetSelectionSilently(int index, bool animate = false)
    {
        if (index >= 0 && index < Systems.Count)
        {
            int direction = animate ? System.Math.Sign(index - SelectedIndex) : 0;

            SelectedIndex = index;
            UpdateVisuals();
            SlideInFrom(direction);
            debounceTimer.Stop();
        }
    }

    private void SetSelectionWithTimer(int index, int direction = 0)
    {
        if (index >= 0 && index < Systems.Count)
        {
            SelectedIndex = index;
            UpdateVisuals();
            SlideInFrom(direction);
            debounceTimer.Start();
        }
    }

    public void UpdateVisuals()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Systems.Count) return;

        var system = Systems[SelectedIndex];
        systemLabel.Text = system.Name;

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
            systemIcon.Texture = texture;
            systemIcon.Visible = true;
            systemLabel.Text = "";
            systemLabel.Visible = false;
        }
        else
        {
            systemIcon.Texture = null;
            systemIcon.Visible = false;
            systemLabel.Text = system.Name;
            systemLabel.Visible = true;
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
        debounceTimer.Stop();
        leftArrow.Visible = false;
        rightArrow.Visible = false;
        systemIcon.Visible = false;
        systemLabel.Text = text;
        systemLabel.Visible = true;
    }

    public void ClearOverride()
    {
        leftArrow.Visible = true;
        rightArrow.Visible = true;
        UpdateVisuals();
    }
}
