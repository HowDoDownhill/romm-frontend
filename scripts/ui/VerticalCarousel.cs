using Godot;
using System;

public partial class VerticalCarousel : Control
{
    [Export] public float itemSpacing = 160.0f;
    [Export] public float depthOffset = 80.0f; // Horizontal shift for 3D wheel curve
    [Export] public float xOffset = 0.0f; // Global horizontal adjustment
    [Export] public float minimumScale = 0.5f;
    [Export] public float minimumOpacity = 0.3f;
    [Export] public float animationDuration = 0.25f;
    [Export] public int visibleItemsHalfCount = 4;
    [Export] public int preloadItemsHalfCount = 2;
    [Export] public bool scaleItemsToWindow = true;
    [Export] public float windowWidthRatio = 0.25f;

    public int SelectedIndex = 0;
    private Tween tween;
    public bool IsAnimating => tween != null && tween.IsValid() && tween.IsRunning();

    [Signal]
    public delegate void ItemSelectedEventHandler(long index);

    [Signal]
    public delegate void ItemFocusedEventHandler(long index);

    [Signal]
    public delegate void JumpSectionRequestedEventHandler(int direction);


    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        ClipContents = true; // Clip items cleanly so they don't bleed over the header/footer
    }

    public override void _GuiInput(InputEvent @event)
    {
        int childCount = GetChildCount();

        if (childCount == 0)
        {
            return;
        }

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

        else if (@event.IsActionPressed("ui_right", true))
        {
            EmitSignal(SignalName.JumpSectionRequested, 1);
            AcceptEvent();
        }

        else if (@event.IsActionPressed("ui_left", true))
        {
            EmitSignal(SignalName.JumpSectionRequested, -1);
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

        if (childCount == 0)
        {
            return;
        }

        if (tween != null && tween.IsValid())
        {
            tween.Kill();
        }

        if (animated)
        {
            tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        Vector2 center = Size / 2.0f;
        float viewportWidth = GetViewportRect().Size.X;
        float targetWidth = viewportWidth * windowWidthRatio;

        for (int i = 0; i < childCount; i++)
        {
            Control child = GetChild<Control>(i);
            
            if (scaleItemsToWindow)
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

            child.PivotOffset = child.Size / 2.0f;
            
            int diff = i - SelectedIndex;
            
            int halfCount = childCount / 2;

            if (diff > halfCount)
            {
                diff -= childCount;
            }

            else if (diff < -halfCount)
            {
                diff += childCount;
            }

            if (childCount % 2 == 0 && diff == halfCount)
            {


            }

            float absDiff = Mathf.Abs(diff);
            float t = Mathf.Clamp(absDiff / visibleItemsHalfCount, 0.0f, 1.0f);
            
            float targetY = center.Y + (diff * itemSpacing) - (child.Size.Y / 2.0f);
            float targetX = center.X - (child.Size.X / 2.0f) - (t * t * depthOffset) + xOffset;
            
            Vector2 targetPos = new Vector2(targetX, targetY);
            
            float targetScaleVal = Mathf.Lerp(1.0f, minimumScale, t);
            Vector2 targetScale = new Vector2(targetScaleVal, targetScaleVal);
            
            Color targetColor = child.Modulate;
            targetColor.A = Mathf.Lerp(1.0f, minimumOpacity, t);

            child.ZIndex = visibleItemsHalfCount - Mathf.RoundToInt(absDiff);

            if (absDiff > visibleItemsHalfCount + preloadItemsHalfCount)
            {

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
                    tween.TweenProperty(child, "position", targetPos, animationDuration);
                    tween.TweenProperty(child, "scale", targetScale, animationDuration);
                    tween.TweenProperty(child, "modulate", targetColor, animationDuration);
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
