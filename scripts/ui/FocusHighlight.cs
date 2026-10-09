using Godot;

public static class FocusHighlight
{
    private static readonly Color FocusedFill = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color FocusedEdge = new Color(1f, 1f, 1f, 0.16f);
    private const int CornerRadius = 10;

    public static StyleBoxFlat CreateStyle(float horizontalPadding = -1, float verticalPadding = -1)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(FocusedFill, 0f),
            BorderColor = new Color(FocusedEdge, 0f),
            DrawCenter = false,
            AntiAliasing = true
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(CornerRadius);

        if (horizontalPadding >= 0)
        {
            style.ContentMarginLeft = horizontalPadding;
            style.ContentMarginRight = horizontalPadding;
        }

        if (verticalPadding >= 0)
        {
            style.ContentMarginTop = verticalPadding;
            style.ContentMarginBottom = verticalPadding;
        }

        return style;
    }

    public static StyleBoxFlat CreateIndicatorStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = FocusedFill,
            BorderColor = FocusedEdge,
            DrawCenter = true,
            AntiAliasing = true
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(CornerRadius);
        return style;
    }

    public static void Set(Control item, Control area, bool focused)
    {
        SelectionHighlighter.Set(item, area, focused);
    }
}
