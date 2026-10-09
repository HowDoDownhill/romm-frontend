using Godot;
using System.Collections.Generic;

public partial class GameGridView : ScrollingGameListView
{
    private const float CellGap = 22.0f;
    private const float DefaultCellAspect = 1.4f;
    private const float MinimumCellAspect = 0.6f;
    private const float MaximumCellAspect = 1.6f;
    private const float TargetRowsVisible = 2.6f;
    private const int MinimumColumns = 3;
    private const int MaximumColumns = 8;
    private const float FallbackCardAspect = 1.4f;

    private int columns = MinimumColumns;
    private Vector2 cellSize;

    public static void ConfigureCard(GameCard card)
    {
        card.CaptionEnabled = false;
    }


    private int RowCount => (ItemCount + columns - 1) / columns;
    private float RowPitch => cellSize.Y + CellGap;

    protected override float ContentHeight => RowCount * RowPitch + CellGap;

    protected override float SelectionHighlightGrow => CellGap * 0.5f;

    protected override bool HandleNavigation(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_right", true))
        {
            MoveSelection(1, false);
            return true;
        }

        if (@event.IsActionPressed("ui_left", true))
        {
            MoveSelection(-1, false);
            return true;
        }

        if (@event.IsActionPressed("ui_down", true))
        {
            MoveRows(1);
            return true;
        }

        if (@event.IsActionPressed("ui_up", true))
        {
            MoveRows(-1);
            return true;
        }

        return base.HandleNavigation(@event);
    }

    public override void ScrollStep(int direction)
    {
        MoveRows(direction);
    }

    private void MoveRows(int direction)
    {
        if (ItemCount == 0)
        {
            return;
        }

        int currentRow = SelectedIndex / columns;
        int targetRow = currentRow + direction;

        if (targetRow < 0 || targetRow >= RowCount)
        {
            return;
        }

        SelectIndex(Mathf.Min(SelectedIndex + direction * columns, ItemCount - 1), true);
    }

    protected override void PrepareLayout()
    {
        float targetCellWidth = Size.Y / TargetRowsVisible / DefaultCellAspect;
        columns = Mathf.Clamp(Mathf.RoundToInt((Size.X - CellGap) / (targetCellWidth + CellGap)), MinimumColumns, MaximumColumns);

        float cellWidth = Mathf.Max(1.0f, (Size.X - CellGap * (columns + 1)) / columns);

        SampleCoverAspects();
        float cellAspect = DefaultCellAspect;

        foreach (Control card in BoundCards.Values)
        {
            if (UniformCoverAspect > 0.0f && card is GameCard gameCard)
            {
                ApplyUniformFrame(card);
                card.CustomMinimumSize = new Vector2(cellWidth, 0.0f);
                cellAspect = Mathf.Clamp(gameCard.CoverAspectRatio, MinimumCellAspect, MaximumCellAspect);
                break;
            }
        }

        cellSize = new Vector2(cellWidth, cellWidth * cellAspect);
    }

    protected override float ResolveTargetScroll(float currentScroll)
    {
        float rowTop = CellGap + (SelectedIndex / columns) * RowPitch;
        return KeepInView(currentScroll, rowTop, rowTop + cellSize.Y, Size.Y, CellGap);
    }

    protected override void ResolveVisibleItems(float fromScroll, float toScroll, HashSet<int> visibleItems)
    {
        int firstRow = Mathf.Max(0, Mathf.FloorToInt((fromScroll - CellGap) / RowPitch) - 1);
        int lastRow = Mathf.Min(RowCount - 1, Mathf.FloorToInt(toScroll / RowPitch) + 1);

        for (int itemIndex = firstRow * columns; itemIndex < Mathf.Min(ItemCount, (lastRow + 1) * columns); itemIndex++)
        {
            visibleItems.Add(itemIndex);
        }
    }

    protected override void PlaceCard(Control card, int itemIndex)
    {
        Vector2 cellOrigin = new Vector2(
            CellGap + (itemIndex % columns) * (cellSize.X + CellGap),
            CellGap + (itemIndex / columns) * RowPitch);

        Vector2 cardSize = FitCard(card);

        card.Scale = Vector2.One;
        card.Modulate = Colors.White;
        card.ZIndex = 0;
        card.Size = cardSize;
        card.Position = cellOrigin + (cellSize - cardSize) / 2.0f;
        card.PivotOffset = cardSize / 2.0f;
    }

    private Vector2 FitCard(Control card)
    {
        ApplyUniformFrame(card);
        card.CustomMinimumSize = new Vector2(cellSize.X, 0.0f);
        float aspect = CardAspect(card);
        float cardHeight = cellSize.X * aspect;

        if (cardHeight <= cellSize.Y)
        {
            return new Vector2(cellSize.X, cardHeight);
        }

        float narrowedWidth = cellSize.Y / aspect;
        card.CustomMinimumSize = new Vector2(narrowedWidth, 0.0f);
        aspect = CardAspect(card);
        return new Vector2(narrowedWidth, Mathf.Min(cellSize.Y, narrowedWidth * aspect));
    }

    private static float CardAspect(Control card)
    {
        float aspect = card is ICarouselItem item ? item.CoverAspectRatio : 0.0f;
        return aspect > 0.0f ? aspect : FallbackCardAspect;
    }
}
