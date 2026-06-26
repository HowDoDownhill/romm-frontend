using Godot;
using System;

public partial class VerticalCarousel : Control
{
    [Export] public float ItemSpacing = 160.0f;
    [Export] public float DepthOffset = 80.0f; // Horizontal shift for 3D wheel curve
    [Export] public float XOffset = 0.0f; // Global horizontal adjustment
    [Export] public float MinimumScale = 0.5f;
    [Export] public float MinimumOpacity = 0.3f;
    [Export] public float AnimationDuration = 0.25f;
    [Export] public int VisibleItemsHalfCount = 4;
    [Export] public int PreloadItemsHalfCount = 2;
    [Export] public bool ScaleItemsToWindow = true;
    [Export] public float WindowWidthRatio = 0.25f;

    public int SelectedIndex = 0;
    private Tween _tween;
    public bool IsAnimating => _tween != null && _tween.IsValid() && _tween.IsRunning();

    [Signal]
    public delegate void ItemSelectedEventHandler(long index);

    [Signal]
    public delegate void ItemFocusedEventHandler(long index);

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        ClipContents = true; // Clip items cleanly so they don't bleed over the header/footer
    }

    public override void _GuiInput(InputEvent @event)
    {
        int childCount = GetChildCount();
        if (childCount == 0) return;

        if (@event.IsActionPressed("ui_down", true))
        {
            SelectedIndex = (SelectedIndex + 1) % childCount;
            UpdateLayout(true);
            AcceptEvent();
        }
        else if (@event.IsActionPressed("ui_up", true))
        {
            SelectedIndex = (SelectedIndex - 1 + childCount) % childCount;
            UpdateLayout(true);
            AcceptEvent();
        }
        else if (@event.IsActionPressed("ui_accept"))
        {
            EmitSignal(SignalName.ItemSelected, SelectedIndex);
            AcceptEvent();
        }
    }

    public void Refresh()
    {
        if (SelectedIndex >= GetChildCount() && GetChildCount() > 0)
        {
            SelectedIndex = GetChildCount() - 1;
        }
        UpdateLayout(false);
    }

    public void UpdateLayout(bool animated = true)
    {
        int childCount = GetChildCount();
        if (childCount == 0) return;

        if (_tween != null && _tween.IsValid())
        {
            _tween.Kill();
        }

        if (animated)
        {
            _tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        Vector2 center = Size / 2.0f;
        float viewportWidth = GetViewportRect().Size.X;
        float targetWidth = viewportWidth * WindowWidthRatio;

        for (int i = 0; i < childCount; i++)
        {
            Control child = GetChild<Control>(i);
            
            if (ScaleItemsToWindow)
            {
                child.CustomMinimumSize = new Vector2(targetWidth, 0);
                if (child is TextureRect texRect && texRect.Texture != null && texRect.Texture.GetSize().X > 0)
                {
                    float aspect = texRect.Texture.GetSize().Y / texRect.Texture.GetSize().X;
                    child.Size = new Vector2(targetWidth, targetWidth * aspect);
                }
                else
                {
                    child.Size = new Vector2(targetWidth, child.Size.Y);
                }
            }

            // Center the pivot for scaling
            child.PivotOffset = child.Size / 2.0f;
            
            int diff = i - SelectedIndex;
            
            int halfCount = childCount / 2;
            if (diff > halfCount) diff -= childCount;
            else if (diff < -halfCount) diff += childCount;

            // Handle edge case for even numbers so we don't snap wildly from top to bottom
            if (childCount % 2 == 0 && diff == halfCount)
            {
                // If it's exactly the halfway point on an even count, we can keep it at halfCount or let it be -halfCount.
                // It's usually invisible anyway because VisibleItemsHalfCount should hide it.
            }

            float absDiff = Mathf.Abs(diff);
            float t = Mathf.Clamp(absDiff / VisibleItemsHalfCount, 0.0f, 1.0f);
            
            float targetY = center.Y + (diff * ItemSpacing) - (child.Size.Y / 2.0f);
            float targetX = center.X - (child.Size.X / 2.0f) - (t * t * DepthOffset) + XOffset;
            
            Vector2 targetPos = new Vector2(targetX, targetY);
            
            float targetScaleVal = Mathf.Lerp(1.0f, MinimumScale, t);
            Vector2 targetScale = new Vector2(targetScaleVal, targetScaleVal);
            
            Color targetColor = child.Modulate;
            targetColor.A = Mathf.Lerp(1.0f, MinimumOpacity, t);

            // Ensure ZIndex remains positive so items don't render behind the root background
            child.ZIndex = VisibleItemsHalfCount - Mathf.RoundToInt(absDiff);

            if (absDiff > VisibleItemsHalfCount + PreloadItemsHalfCount)
            {
                // Hide off-screen items to save rendering, but place them roughly where they should be if they fade in
                child.Visible = false;
                child.Position = targetPos;
                child.Scale = targetScale;
                child.Modulate = targetColor;
            }
            else
            {
                child.Visible = true;
                if (animated)
                {
                    _tween.TweenProperty(child, "position", targetPos, AnimationDuration);
                    _tween.TweenProperty(child, "scale", targetScale, AnimationDuration);
                    _tween.TweenProperty(child, "modulate", targetColor, AnimationDuration);
                }
                else
                {
                    child.Position = targetPos;
                    child.Scale = targetScale;
                    child.Modulate = targetColor;
                }
            }
        }

        EmitSignal(SignalName.ItemFocused, SelectedIndex);
    }
    
    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateLayout(false);
        }
    }
}
