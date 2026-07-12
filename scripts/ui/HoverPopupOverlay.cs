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
        TopLevel = true;
        ZIndex = 100;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.08f, 1.0f),
            ShadowColor = new Color(0, 0, 0, 0.5f),
            ShadowSize = 20,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            BorderWidthTop = 0,
            BorderWidthBottom = 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0
        };
        AddThemeStyleboxOverride("panel", style);

        contentContainer = new MarginContainer();
        contentContainer.AddThemeConstantOverride("margin_left", 10);
        contentContainer.AddThemeConstantOverride("margin_top", 10);
        contentContainer.AddThemeConstantOverride("margin_right", 10);
        contentContainer.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(contentContainer);
    }

    public void ApplyThemeColor(Color bgColor, Color borderColor)
    {
        var style = GetThemeStylebox("panel") as StyleBoxFlat;
        if (style != null)
        {
            bgColor.A = 1.0f; // Force opaque as requested previously
            style.BgColor = bgColor;
            style.BorderColor = borderColor;
            style.BorderWidthTop = 0;
            style.BorderWidthBottom = 0;
            style.BorderWidthLeft = 0;
            style.BorderWidthRight = 0;
        }
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
