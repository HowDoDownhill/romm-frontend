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

    public override void _GuiInput(InputEvent @event)
    {
        if (ItemCount == 0)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            int clickedIndex = ItemIndexAt(mouseButton.GlobalPosition);

            if (clickedIndex < 0)
            {
                return;
            }

            if (clickedIndex != SelectedIndex)
            {
                SelectIndex(clickedIndex, true);
            }

            else if (mouseButton.DoubleClick)
            {
                EmitSignal(SignalName.ItemActivated, SelectedIndex);
            }

            AcceptEvent();
            return;
        }

        if (HandleNavigation(@event))
        {
            AcceptEvent();
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
