using Godot;

public sealed class SelectionIndicator
{
    private const float GlideRate = 22.0f;
    private const float FadeRate = 18.0f;
    private const float SnapDistance = 0.5f;
    private const float InvisibleAlpha = 0.01f;
    private const float FreshStartAlpha = 0.05f;

    private static readonly StyleBoxFlat Style = FocusHighlight.CreateIndicatorStyle();

    private readonly Control host;
    private readonly Rid canvasItem;
    private Control target;
    private float grow;
    private Rect2 currentRect;
    private float alpha;
    private bool active;

    public SelectionIndicator(Control host)
    {
        this.host = host;
        canvasItem = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(canvasItem, host.GetCanvasItem());
        RenderingServer.CanvasItemSetDrawIndex(canvasItem, -1);
    }

    public bool IsHostValid => GodotObject.IsInstanceValid(host) && host.IsInsideTree();

    public void MoveTo(Control area, float areaGrow)
    {
        target = area;
        grow = areaGrow;
        active = true;

        if (alpha < FreshStartAlpha && TryResolveTargetRect(out Rect2 startRect))
        {
            currentRect = startRect;
        }
    }

    public void Release(Control area)
    {
        if (area == target)
        {
            active = false;
        }
    }

    public void Update(double delta)
    {
        if (!GodotObject.IsInstanceValid(target) || !target.IsVisibleInTree())
        {
            active = false;
        }

        if (active && TryResolveTargetRect(out Rect2 targetRect))
        {
            float glide = 1.0f - Mathf.Exp(-GlideRate * (float)delta);
            currentRect = new Rect2(currentRect.Position.Lerp(targetRect.Position, glide), currentRect.Size.Lerp(targetRect.Size, glide));

            if (currentRect.Position.DistanceTo(targetRect.Position) < SnapDistance && currentRect.Size.DistanceTo(targetRect.Size) < SnapDistance)
            {
                currentRect = targetRect;
            }
        }

        if (!active && alpha <= 0.0f)
        {
            return;
        }

        float fade = 1.0f - Mathf.Exp(-FadeRate * (float)delta);
        alpha = Mathf.Lerp(alpha, active ? 1.0f : 0.0f, fade);

        if (!active && alpha < InvisibleAlpha)
        {
            alpha = 0.0f;
        }

        RenderingServer.CanvasItemClear(canvasItem);

        if (alpha > 0.0f)
        {
            RenderingServer.CanvasItemSetModulate(canvasItem, new Color(1, 1, 1, alpha));
            Style.Draw(canvasItem, currentRect);
        }
    }

    public void Free()
    {
        RenderingServer.FreeRid(canvasItem);
    }

    private bool TryResolveTargetRect(out Rect2 rect)
    {
        rect = default;

        if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || !IsHostValid)
        {
            return false;
        }

        Transform2D toHost = host.GetGlobalTransform().AffineInverse() * target.GetGlobalTransform();
        Vector2 start = toHost * Vector2.Zero;
        Vector2 end = toHost * target.Size;
        rect = new Rect2(start, end - start).Abs().Grow(grow);
        return true;
    }
}
