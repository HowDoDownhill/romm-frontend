using Godot;
using System.Collections.Generic;

public partial class GameTextListView : ScrollingGameListView
{
    private const float SidePadding = 12.0f;
    private const float RowGap = 4.0f;
    private const float FallbackRowHeight = 52.0f;
    private const int RowsKeptInView = 3;

    private float rowHeight = FallbackRowHeight;

    private float RowPitch => rowHeight + RowGap;

    protected override float ContentHeight => ItemCount * RowPitch + SidePadding * 2.0f;

    protected override float SelectionHighlightGrow => 0.0f;

    protected override void PrepareLayout()
    {
        Font font = GetThemeFont("font", "Label");
        float fontHeight = font != null ? font.GetHeight(GetThemeFontSize("font_size", "Label")) : 0.0f;
        rowHeight = fontHeight > 0.0f ? GameListRow.HeightForText(fontHeight) : FallbackRowHeight;
    }

    protected override float ResolveTargetScroll(float currentScroll)
    {
        float rowTop = SidePadding + SelectedIndex * RowPitch;
        float margin = Mathf.Min(RowsKeptInView * RowPitch, Mathf.Max(0.0f, (Size.Y - rowHeight) / 2.0f));
        return KeepInView(currentScroll, rowTop, rowTop + rowHeight, Size.Y, margin);
    }

    protected override void ResolveVisibleItems(float fromScroll, float toScroll, HashSet<int> visibleItems)
    {
        int firstItem = Mathf.Max(0, Mathf.FloorToInt((fromScroll - SidePadding) / RowPitch) - 2);
        int lastItem = Mathf.Min(ItemCount - 1, Mathf.FloorToInt((toScroll - SidePadding) / RowPitch) + 2);

        for (int itemIndex = firstItem; itemIndex <= lastItem; itemIndex++)
        {
            visibleItems.Add(itemIndex);
        }
    }

    protected override void PlaceCard(Control card, int itemIndex)
    {
        card.Scale = Vector2.One;
        card.Modulate = Colors.White;
        card.Position = new Vector2(SidePadding, SidePadding + itemIndex * RowPitch);
        card.Size = new Vector2(Mathf.Max(1.0f, Size.X - SidePadding * 2.0f), rowHeight);
    }
}
