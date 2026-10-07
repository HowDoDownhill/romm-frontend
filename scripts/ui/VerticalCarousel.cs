using Godot;
using System;
using System.Collections.Generic;

public partial class VerticalCarousel : Control
{
    [Export] public float itemSpacing = 160.0f;
    [Export] public float depthOffset = 80.0f;
    [Export] public float xOffset = 0.0f;
    [Export] public bool useScreenPercentageForOffsets = true;
    [Export] public float itemSpacingRatio = 0.074f;
    [Export] public float depthOffsetRatio = 0.104f;
    [Export] public float xOffsetRatio = -0.078f;
    [Export] public float minimumScale = 0.5f;
    [Export] public float minimumOpacity = 0.3f;
    [Export] public float animationDuration = 0.25f;
    [Export] public int visibleItemsHalfCount = 4;
    [Export] public int preloadItemsHalfCount = 2;
    [Export] public bool scaleItemsToWindow = true;
    [Export] public float windowWidthRatio = 0.25f;
    [Export] public Vector2 referenceCanvasSize = new Vector2(1920, 1080);
    [Export] public Vector2 referenceCarouselSize = new Vector2(945, 873);

    public int SelectedIndex = 0;
    public int ItemCount { get; private set; }
    private Tween tween;
    public bool IsAnimating => tween != null && tween.IsValid() && tween.IsRunning();

    public Func<Control> ItemFactory;
    public event Action<Control, int> ItemBound;
    public event Action<Control> ItemReleased;

    private readonly Dictionary<int, Control> cardsByItemIndex = new Dictionary<int, Control>();
    private readonly Stack<Control> idleCards = new Stack<Control>();

    public IReadOnlyDictionary<int, Control> BoundCards => cardsByItemIndex;

    [Signal]
    public delegate void ItemSelectedEventHandler(long index);

    [Signal]
    public delegate void ItemFocusedEventHandler(long index);

    [Signal]
    public delegate void JumpSectionRequestedEventHandler(int direction);

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        ClipContents = true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (ItemCount == 0)
        {
            return;
        }

        if (@event.IsActionPressed("ui_down", true))
        {
            SelectNext();
            AcceptEvent();
        }

        else if (@event.IsActionPressed("ui_up", true))
        {
            SelectPrevious();
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

    public void SelectNext()
    {
        if (ItemCount == 0) return;
        SelectedIndex = (SelectedIndex + 1) % ItemCount;
        UpdateLayout(true);
    }

    public void SelectPrevious()
    {
        if (ItemCount == 0) return;
        SelectedIndex = (SelectedIndex - 1 + ItemCount) % ItemCount;
        UpdateLayout(true);
    }

    public void ReloadItems(int itemCount, int selectedIndex)
    {
        foreach (int itemIndex in new List<int>(cardsByItemIndex.Keys))
        {
            ReleaseCard(itemIndex);
        }

        ItemCount = Math.Max(0, itemCount);
        SelectedIndex = ItemCount == 0 ? 0 : Math.Clamp(selectedIndex, 0, ItemCount - 1);
        UpdateLayout(false);
    }

    public void Refresh()
    {
        if (SelectedIndex >= ItemCount && ItemCount > 0)
        {
            SelectedIndex = ItemCount - 1;
        }

        UpdateLayout(false);
    }

    private void ReleaseCard(int itemIndex)
    {
        if (!cardsByItemIndex.Remove(itemIndex, out Control card))
        {
            return;
        }

        card.Visible = false;
        ItemReleased?.Invoke(card);
        idleCards.Push(card);
    }

    private Control AcquireCard(int itemIndex)
    {
        Control card = idleCards.Count > 0 ? idleCards.Pop() : ItemFactory?.Invoke();

        if (card == null)
        {
            return null;
        }

        if (card.GetParent() == null)
        {
            AddChild(card);
        }

        cardsByItemIndex[itemIndex] = card;
        card.Visible = true;
        ItemBound?.Invoke(card, itemIndex);
        return card;
    }

    private Vector2 ResolveEffectiveCanvasSize()
    {
        if (Size.X <= 0.0f || Size.Y <= 0.0f || referenceCarouselSize.X <= 0.0f || referenceCarouselSize.Y <= 0.0f)
        {
            return GetViewport().GetVisibleRect().Size;
        }

        float layoutScale = Mathf.Min(Size.X / referenceCarouselSize.X, Size.Y / referenceCarouselSize.Y);
        return referenceCanvasSize * layoutScale;
    }

    private static float GetItemAspectRatio(Control child)
    {
        if (child is ICarouselItem item)
        {
            return item.CoverAspectRatio;
        }

        if (child is TextureRect texRect && texRect.Texture != null && texRect.Texture.GetSize().X > 0)
        {
            return texRect.Texture.GetSize().Y / texRect.Texture.GetSize().X;
        }

        return 0.0f;
    }

    private int WrappedOffsetFromSelection(int itemIndex)
    {
        int diff = itemIndex - SelectedIndex;
        int halfCount = ItemCount / 2;

        if (diff > halfCount)
        {
            diff -= ItemCount;
        }

        else if (diff < -halfCount)
        {
            diff += ItemCount;
        }

        return diff;
    }

    private List<(int ItemIndex, int Offset)> ResolveWindow()
    {
        int windowHalfCount = visibleItemsHalfCount + preloadItemsHalfCount;
        var window = new List<(int, int)>();

        if (ItemCount <= windowHalfCount * 2 + 1)
        {
            for (int itemIndex = 0; itemIndex < ItemCount; itemIndex++)
            {
                window.Add((itemIndex, WrappedOffsetFromSelection(itemIndex)));
            }

            return window;
        }

        for (int offset = -windowHalfCount; offset <= windowHalfCount; offset++)
        {
            window.Add((((SelectedIndex + offset) % ItemCount + ItemCount) % ItemCount, offset));
        }

        return window;
    }

    public void UpdateLayout(bool animated = true)
    {
        if (tween != null && tween.IsValid())
        {
            tween.Kill();
        }

        if (ItemCount == 0)
        {
            foreach (int itemIndex in new List<int>(cardsByItemIndex.Keys))
            {
                ReleaseCard(itemIndex);
            }

            return;
        }

        List<(int ItemIndex, int Offset)> window = ResolveWindow();
        var itemsInWindow = new HashSet<int>();

        foreach (var entry in window)
        {
            itemsInWindow.Add(entry.ItemIndex);
        }

        foreach (int itemIndex in new List<int>(cardsByItemIndex.Keys))
        {
            if (!itemsInWindow.Contains(itemIndex))
            {
                ReleaseCard(itemIndex);
            }
        }

        if (animated)
        {
            tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        Vector2 center = Size / 2.0f;
        Vector2 effectiveCanvasSize = ResolveEffectiveCanvasSize();
        float viewportWidth = effectiveCanvasSize.X;
        float targetWidth = viewportWidth * windowWidthRatio;
        float currentItemSpacing = useScreenPercentageForOffsets ? effectiveCanvasSize.Y * itemSpacingRatio : itemSpacing;
        float currentDepthOffset = useScreenPercentageForOffsets ? viewportWidth * depthOffsetRatio : depthOffset;
        float currentXOffset = useScreenPercentageForOffsets ? viewportWidth * xOffsetRatio : xOffset;

        bool tweenHasWork = false;

        foreach (var (itemIndex, offset) in window)
        {
            bool isNewlyBound = !cardsByItemIndex.TryGetValue(itemIndex, out Control child);

            if (isNewlyBound)
            {
                child = AcquireCard(itemIndex);

                if (child == null)
                {
                    continue;
                }
            }

            if (scaleItemsToWindow)
            {
                child.CustomMinimumSize = new Vector2(targetWidth, 0);

                float aspect = GetItemAspectRatio(child);

                if (aspect > 0.0f)
                {
                    child.Size = new Vector2(targetWidth, targetWidth * aspect);
                }

                else
                {
                    child.Size = new Vector2(targetWidth, child.Size.Y);
                }
            }

            child.PivotOffset = child.Size / 2.0f;

            float absDiff = Mathf.Abs(offset);
            float t = Mathf.Clamp(absDiff / visibleItemsHalfCount, 0.0f, 1.0f);

            float targetY = center.Y + (offset * currentItemSpacing) - (child.Size.Y / 2.0f);
            float targetX = center.X - (child.Size.X / 2.0f) - (t * t * currentDepthOffset) + currentXOffset;

            Vector2 targetPos = new Vector2(targetX, targetY);

            float targetScaleVal = Mathf.Lerp(1.0f, minimumScale, t);
            Vector2 targetScale = new Vector2(targetScaleVal, targetScaleVal);

            Color targetColor = child.Modulate;
            targetColor.A = Mathf.Lerp(1.0f, minimumOpacity, t);

            child.ZIndex = visibleItemsHalfCount - Mathf.RoundToInt(absDiff);

            if (animated && !isNewlyBound)
            {
                tween.TweenProperty(child, "position", targetPos, animationDuration);
                tween.TweenProperty(child, "scale", targetScale, animationDuration);
                tween.TweenProperty(child, "modulate", targetColor, animationDuration);
                tweenHasWork = true;
            }

            else
            {
                child.Position = targetPos;
                child.Scale = targetScale;
                child.Modulate = targetColor;
            }
        }

        if (animated && !tweenHasWork)
        {
            tween.Kill();
            tween = null;
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
