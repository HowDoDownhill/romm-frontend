using Godot;
using System;

public partial class HoverPopupOverlay : PanelContainer
{
    private Control currentTarget;
    private IHoverPopupItem currentPopupItem;
    private Tween animTween;
    private MarginContainer contentContainer;

    public override void _Ready()
    {
        // Not TopLevel: stays in the normal draw flow (added last, so it renders on top) where Godot
        // auto-copies the back buffer for the mica shader. TopLevel + a manual BackBufferCopy did not
        // feed the screen texture under the gl_compatibility renderer, so the frost never blurred.
        ZIndex = 100;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        // Shared mica frosted-glass material (same as the panels) so it matches exactly.
        Material = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
        var style = new StyleBoxFlat
        {
            // The mica shader replaces the fill with the blurred screen + theme tint; this stylebox
            // only defines the rounded shape, so keep it opaque to cover the whole panel.
            BgColor = new Color(0, 0, 0, 1.0f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        AddThemeStyleboxOverride("panel", style);

        contentContainer = new MarginContainer();
        contentContainer.AddThemeConstantOverride("margin_left", 10);
        contentContainer.AddThemeConstantOverride("margin_top", 10);
        contentContainer.AddThemeConstantOverride("margin_right", 10);
        contentContainer.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(contentContainer);
    }

    // Retained for MainScene.ApplyTheme compatibility; the mica material is themed globally now,
    // so per-instance tinting is no longer needed here.
    public void ApplyThemeColor(Color bgColor, Color borderColor)
    {
    }

    public override void _Process(double delta)
    {
        if (Visible && currentTarget != null && GodotObject.IsInstanceValid(currentTarget))
        {
            if (!currentTarget.IsVisibleInTree())
            {
                ForceCancelPopup();
                return;
            }
            GlobalPosition = currentTarget.GlobalPosition - (Size - currentTarget.Size) / 2;
        }
        else if (Visible)
        {
            ForceCancelPopup();
        }
    }

    public void OnItemHovered(Control target, IHoverPopupItem popupItem)
    {
        if (currentTarget == target) return;
        CancelPopup(true);

        currentTarget = target;
        currentPopupItem = popupItem;
        ShowPopup();
    }

    public void OnItemUnhovered(Control target)
    {
        if (currentTarget == target)
        {
            CancelPopup();
        }
    }

    public void ForceCancelPopup()
    {
        CancelPopup(true);
    }

    // Rebuilds the popup content in place (no re-animation) if it is currently showing the given
    // target. Used to pick up a cover that finished downloading after the popup was already shown.
    public void RefreshContentIfTarget(Control target)
    {
        if (target == null || target != currentTarget)
        {
            return;
        }

        if (!Visible || currentPopupItem == null || !GodotObject.IsInstanceValid(currentTarget))
        {
            return;
        }

        foreach (Node child in contentContainer.GetChildren())
        {
            contentContainer.RemoveChild(child);
            child.QueueFree();
        }

        var content = currentPopupItem.GetPopupContent();
        if (content == null) return;

        contentContainer.AddChild(content);

        Size = Vector2.Zero;
        ResetSize();
        GlobalPosition = currentTarget.GlobalPosition - (Size - currentTarget.Size) / 2;
        PivotOffset = Size / 2;
    }

    private void CancelPopup(bool instant = false)
    {
        var targetToRestore = currentTarget;

        if (Visible)
        {
            if (animTween != null && animTween.IsValid()) animTween.Kill();
            
            if (instant)
            {
                Visible = false;
                foreach (Node child in contentContainer.GetChildren())
                {
                    contentContainer.RemoveChild(child);
                    child.QueueFree();
                }
                if (targetToRestore != null && GodotObject.IsInstanceValid(targetToRestore))
                {
                    targetToRestore.Modulate = new Color(1, 1, 1, 1);
                    targetToRestore.SelfModulate = new Color(1, 1, 1, 1);
                    var focusPanel = targetToRestore.GetNodeOrNull<Control>("FocusPanel");
                    if (focusPanel != null) focusPanel.Modulate = new Color(1, 1, 1, 1);
                }
            }
            else
            {
                animTween = CreateTween();
                animTween.TweenProperty(this, "modulate:a", 0.0f, 0.15f);
                animTween.TweenCallback(Callable.From(() => {
                    Visible = false;
                    foreach (Node child in contentContainer.GetChildren())
                    {
                        contentContainer.RemoveChild(child);
                        child.QueueFree();
                    }
                    if (targetToRestore != null && GodotObject.IsInstanceValid(targetToRestore))
                    {
                        targetToRestore.Modulate = new Color(1, 1, 1, 1);
                        targetToRestore.SelfModulate = new Color(1, 1, 1, 1);
                        var focusPanel = targetToRestore.GetNodeOrNull<Control>("FocusPanel");
                        if (focusPanel != null) focusPanel.Modulate = new Color(1, 1, 1, 1);
                    }
                }));
            }
        }
        else
        {
            if (targetToRestore != null && GodotObject.IsInstanceValid(targetToRestore))
            {
                targetToRestore.Modulate = new Color(1, 1, 1, 1);
                targetToRestore.SelfModulate = new Color(1, 1, 1, 1);
                var focusPanel = targetToRestore.GetNodeOrNull<Control>("FocusPanel");
                if (focusPanel != null) focusPanel.Modulate = new Color(1, 1, 1, 1);
            }
        }
        
        currentTarget = null;
        currentPopupItem = null;
    }

    private void ShowPopup()
    {
        if (currentTarget == null || !GodotObject.IsInstanceValid(currentTarget) || !currentTarget.IsVisibleInTree())
        {
            return;
        }

        if (currentPopupItem != null)
        {
            foreach (Node child in contentContainer.GetChildren())
            {
                contentContainer.RemoveChild(child);
                child.QueueFree();
            }

            var content = currentPopupItem.GetPopupContent();
            if (content == null) return;
            
            contentContainer.AddChild(content);
            
            Size = Vector2.Zero; // Reset to recalculate size
            ResetSize(); // Force resize to content

            GlobalPosition = currentTarget.GlobalPosition - (Size - currentTarget.Size) / 2;
            PivotOffset = Size / 2;
            
            currentTarget.Modulate = new Color(1, 1, 1, 0); // Hide original target
            currentTarget.SelfModulate = new Color(1, 1, 1, 0); 
            var focusPanel = currentTarget.GetNodeOrNull<Control>("FocusPanel");
            if (focusPanel != null) focusPanel.Modulate = new Color(1, 1, 1, 0);
            
            Modulate = new Color(1, 1, 1, 0);
            Scale = Vector2.One;
            Visible = true;

            if (animTween != null && animTween.IsValid()) animTween.Kill();
            animTween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            
            animTween.TweenProperty(this, "modulate:a", 1.0f, 0.2f);
            animTween.TweenProperty(this, "scale", new Vector2(1.1f, 1.1f), 0.2f);
        }
    }
}
