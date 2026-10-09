using Godot;
using System.Collections.Generic;

public abstract partial class ScrollingGameListView : GameListView
{
    private const float ScrollRate = 16.0f;
    private const float SettleDistance = 0.5f;
    private const float MaximumGlideScreens = 2.0f;

    private readonly Control content = new Control { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
    private float scrollOffset;
    private float targetScrollOffset;

    protected override Control CardParent => content;

    public override bool IsAnimating => Mathf.Abs(scrollOffset - targetScrollOffset) > SettleDistance;

    protected abstract float ContentHeight { get; }

    protected abstract float ResolveTargetScroll(float currentScroll);

    protected abstract void ResolveVisibleItems(float fromScroll, float toScroll, HashSet<int> visibleItems);

    protected abstract void PlaceCard(Control card, int itemIndex);

    protected abstract float SelectionHighlightGrow { get; }

    public override void _Ready()
    {
        base._Ready();
        AddChild(content);
        SetProcess(false);
    }

    protected virtual void PrepareLayout()
    {
    }

    public override void UpdateLayout(bool animated = true)
    {
        if (ItemCount == 0 || Size.X <= 0.0f || Size.Y <= 0.0f)
        {
            ReleaseAllCards();
            scrollOffset = 0.0f;
            targetScrollOffset = 0.0f;
            content.Position = Vector2.Zero;

            if (ItemCount > 0)
            {
                EmitSignal(SignalName.ItemFocused, SelectedIndex);
            }

            return;
        }

        PrepareLayout();

        float maximumScroll = Mathf.Max(0.0f, ContentHeight - Size.Y);
        targetScrollOffset = Mathf.Clamp(ResolveTargetScroll(targetScrollOffset), 0.0f, maximumScroll);

        if (!animated || Mathf.Abs(targetScrollOffset - scrollOffset) > Size.Y * MaximumGlideScreens)
        {
            scrollOffset = targetScrollOffset;
        }

        scrollOffset = Mathf.Clamp(scrollOffset, 0.0f, maximumScroll);

        var visibleItems = new HashSet<int>();
        ResolveVisibleItems(Mathf.Min(scrollOffset, targetScrollOffset), Mathf.Max(scrollOffset, targetScrollOffset) + Size.Y, visibleItems);
        visibleItems.Add(SelectedIndex);
        ReleaseCardsOutside(visibleItems);

        foreach (int itemIndex in visibleItems)
        {
            if (TryGetOrAcquireCard(itemIndex, out Control card, out _))
            {
                PlaceCard(card, itemIndex);
            }
        }

        content.Position = new Vector2(0.0f, -scrollOffset);
        SetProcess(IsAnimating);

        if (BoundCards.TryGetValue(SelectedIndex, out Control selectedCard))
        {
            SelectionHighlighter.Set(selectedCard, selectedCard, true, SelectionHighlightGrow);
        }

        EmitSignal(SignalName.ItemFocused, SelectedIndex);
    }

    public override void _Process(double delta)
    {
        float glide = 1.0f - Mathf.Exp(-ScrollRate * (float)delta);
        scrollOffset = Mathf.Lerp(scrollOffset, targetScrollOffset, glide);

        if (!IsAnimating)
        {
            scrollOffset = targetScrollOffset;
            SetProcess(false);
        }

        content.Position = new Vector2(0.0f, -scrollOffset);
    }

    protected static float KeepInView(float currentScroll, float itemTop, float itemBottom, float viewHeight, float margin)
    {
        if (itemTop - margin < currentScroll)
        {
            return itemTop - margin;
        }

        if (itemBottom + margin > currentScroll + viewHeight)
        {
            return itemBottom + margin - viewHeight;
        }

        return currentScroll;
    }
}
