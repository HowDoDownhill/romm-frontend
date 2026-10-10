using Godot;
using System;
using System.Collections.Generic;

public abstract partial class GameListView : Control
{
    public int SelectedIndex { get; protected set; }
    public int ItemCount { get; private set; }

    public Func<Control> ItemFactory;
    public event Action<Control, int> ItemBound;
    public event Action<Control> ItemReleased;

    private readonly Dictionary<int, Control> cardsByItemIndex = new Dictionary<int, Control>();
    private readonly Stack<Control> idleCards = new Stack<Control>();

    public IReadOnlyDictionary<int, Control> BoundCards => cardsByItemIndex;

    public virtual bool IsAnimating => false;

    protected virtual Control CardParent => this;

    [Signal]
    public delegate void ItemSelectedEventHandler(long index);

    [Signal]
    public delegate void ItemFocusedEventHandler(long index);

    [Signal]
    public delegate void ItemActivatedEventHandler(long index);

    [Signal]
    public delegate void JumpSectionRequestedEventHandler(int direction);

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        ClipContents = true;
    }

    private const float DragStartDistance = 18.0f;
    private const float FlingSeconds = 0.35f;
    private const int MaximumFlingSteps = 12;

    private bool pointerDown;
    private bool dragging;
    private Vector2 pointerPressPosition;
    private int pointerPressIndex = -1;
    private float dragTravel;
    private float dragVelocity;

    protected virtual float DragStepPixels => 120.0f;

    protected virtual int DragStepItems => 1;

    public override void _GuiInput(InputEvent @event)
    {
        if (ItemCount == 0)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                BeginPointer(mouseButton);
            }

            else
            {
                EndPointer();
            }

            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseMotion motion && pointerDown && (motion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            TrackDrag(motion);
            AcceptEvent();
            return;
        }

        if (HandleNavigation(@event))
        {
            AcceptEvent();
        }
    }

    private void BeginPointer(InputEventMouseButton press)
    {
        pointerPressIndex = ItemIndexAt(press.GlobalPosition);

        if (press.DoubleClick && pointerPressIndex >= 0 && pointerPressIndex == SelectedIndex)
        {
            pointerDown = false;
            EmitSignal(SignalName.ItemActivated, SelectedIndex);
            return;
        }

        pointerDown = true;
        dragging = false;
        pointerPressPosition = press.Position;
        dragTravel = 0.0f;
        dragVelocity = 0.0f;
    }

    private void TrackDrag(InputEventMouseMotion motion)
    {
        if (!dragging && motion.Position.DistanceTo(pointerPressPosition) < DragStartDistance)
        {
            return;
        }

        dragging = true;
        dragTravel -= motion.Relative.Y;
        dragVelocity = -motion.Velocity.Y;
        float step = Mathf.Max(1.0f, DragStepPixels);

        while (Mathf.Abs(dragTravel) >= step)
        {
            int direction = Math.Sign(dragTravel);
            MoveSelection(direction * DragStepItems, false);
            dragTravel -= direction * step;
        }
    }

    private void EndPointer()
    {
        if (!pointerDown)
        {
            return;
        }

        pointerDown = false;

        if (dragging)
        {
            int flingSteps = Math.Clamp((int)(dragVelocity * FlingSeconds / Mathf.Max(1.0f, DragStepPixels)), -MaximumFlingSteps, MaximumFlingSteps);

            if (flingSteps != 0)
            {
                MoveSelection(flingSteps * DragStepItems, false);
            }

            return;
        }

        if (pointerPressIndex >= 0 && pointerPressIndex != SelectedIndex)
        {
            SelectIndex(pointerPressIndex, true);
        }
    }

    protected virtual bool HandleNavigation(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_down", true))
        {
            SelectNext();
            return true;
        }

        if (@event.IsActionPressed("ui_up", true))
        {
            SelectPrevious();
            return true;
        }

        if (@event.IsActionPressed("ui_accept"))
        {
            EmitSignal(SignalName.ItemSelected, SelectedIndex);
            return true;
        }

        if (@event.IsActionPressed("ui_right", true))
        {
            EmitSignal(SignalName.JumpSectionRequested, 1);
            return true;
        }

        if (@event.IsActionPressed("ui_left", true))
        {
            EmitSignal(SignalName.JumpSectionRequested, -1);
            return true;
        }

        return false;
    }

    public void SelectNext()
    {
        MoveSelection(1, true);
    }

    public void SelectPrevious()
    {
        MoveSelection(-1, true);
    }

    public virtual void ScrollStep(int direction)
    {
        MoveSelection(direction, true);
    }

    protected void MoveSelection(int step, bool wrap)
    {
        if (ItemCount == 0)
        {
            return;
        }

        int target = wrap
            ? ((SelectedIndex + step) % ItemCount + ItemCount) % ItemCount
            : Math.Clamp(SelectedIndex + step, 0, ItemCount - 1);

        SelectIndex(target, true);
    }

    public void SelectIndex(int index, bool animated)
    {
        if (ItemCount == 0)
        {
            return;
        }

        SelectedIndex = Math.Clamp(index, 0, ItemCount - 1);
        UpdateLayout(animated);
    }

    public void ReloadItems(int itemCount, int selectedIndex)
    {
        ReleaseAllCards();
        ItemCount = Math.Max(0, itemCount);
        SelectedIndex = ItemCount == 0 ? 0 : Math.Clamp(selectedIndex, 0, ItemCount - 1);
        ResetCoverAspectSamples();
        OnItemsReloaded();
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

    public abstract void UpdateLayout(bool animated = true);

    private bool cardRefreshQueued;
    private bool cardRefreshWaitingForAnimation;
    private bool refreshingCards;

    public void RequestCardRefresh()
    {
        if (cardRefreshQueued)
        {
            return;
        }

        cardRefreshQueued = true;
        Callable.From(RunQueuedCardRefresh).CallDeferred();
    }

    private void RunQueuedCardRefresh()
    {
        cardRefreshQueued = false;

        if (IsAnimating)
        {
            cardRefreshWaitingForAnimation = true;
            return;
        }

        RefreshCards();
    }

    protected void OnAnimationSettled()
    {
        if (cardRefreshWaitingForAnimation)
        {
            cardRefreshWaitingForAnimation = false;
            RefreshCards();
        }
    }

    private void RefreshCards()
    {
        refreshingCards = true;
        UpdateLayout(false);
        refreshingCards = false;
    }

    protected void NotifyItemFocused()
    {
        if (!refreshingCards)
        {
            EmitSignal(SignalName.ItemFocused, SelectedIndex);
        }
    }

    private const int CoverAspectSampleTarget = 6;
    private const float MinimumCoverAspect = 0.5f;
    private const float MaximumCoverAspect = 1.6f;

    private readonly List<float> coverAspectSamples = new List<float>();
    private readonly HashSet<int> sampledItems = new HashSet<int>();
    private bool coverAspectLocked;

    protected float UniformCoverAspect { get; private set; }

    protected void SampleCoverAspects()
    {
        if (coverAspectLocked)
        {
            return;
        }

        bool sampled = false;

        foreach (var entry in cardsByItemIndex)
        {
            if (entry.Value is GameCard card && card.HasRealCover && card.TextureAspect > 0.0f && sampledItems.Add(entry.Key))
            {
                coverAspectSamples.Add(card.TextureAspect);
                sampled = true;
            }
        }

        if (!sampled)
        {
            return;
        }

        var sortedSamples = new List<float>(coverAspectSamples);
        sortedSamples.Sort();
        UniformCoverAspect = Mathf.Clamp(sortedSamples[sortedSamples.Count / 2], MinimumCoverAspect, MaximumCoverAspect);
        coverAspectLocked = coverAspectSamples.Count >= Math.Min(CoverAspectSampleTarget, ItemCount);
    }

    protected void ApplyUniformFrame(Control card)
    {
        if (card is GameCard gameCard)
        {
            gameCard.FrameCoverAspect = UniformCoverAspect;
        }
    }

    private void ResetCoverAspectSamples()
    {
        coverAspectSamples.Clear();
        sampledItems.Clear();
        coverAspectLocked = false;
        UniformCoverAspect = 0.0f;
    }

    protected virtual void OnItemsReloaded()
    {
    }

    protected void ReleaseAllCards()
    {
        foreach (int itemIndex in new List<int>(cardsByItemIndex.Keys))
        {
            ReleaseCard(itemIndex);
        }
    }

    protected void ReleaseCardsOutside(HashSet<int> itemsToKeep)
    {
        foreach (int itemIndex in new List<int>(cardsByItemIndex.Keys))
        {
            if (!itemsToKeep.Contains(itemIndex))
            {
                ReleaseCard(itemIndex);
            }
        }
    }

    protected bool TryGetOrAcquireCard(int itemIndex, out Control card, out bool isNewlyBound)
    {
        isNewlyBound = !cardsByItemIndex.TryGetValue(itemIndex, out card);

        if (isNewlyBound)
        {
            card = AcquireCard(itemIndex);
        }

        return card != null;
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
            CardParent.AddChild(card);
        }

        cardsByItemIndex[itemIndex] = card;
        card.Visible = true;
        ItemBound?.Invoke(card, itemIndex);
        return card;
    }

    protected virtual int ItemIndexAt(Vector2 globalPosition)
    {
        int hitIndex = -1;
        int hitZIndex = int.MinValue;

        foreach (var entry in cardsByItemIndex)
        {
            Control card = entry.Value;

            if (card.Visible && card.ZIndex > hitZIndex && card.GetGlobalRect().HasPoint(globalPosition))
            {
                hitIndex = entry.Key;
                hitZIndex = card.ZIndex;
            }
        }

        return hitIndex;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateLayout(false);
        }
    }
}
