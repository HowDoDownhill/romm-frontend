using Godot;

public static class FocusHighlight
{
    private static readonly Color FocusedFill = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color FocusedEdge = new Color(1f, 1f, 1f, 0.16f);
    private const int CornerRadius = 10;
    private const float FadeSeconds = 0.12f;

    public static StyleBoxFlat CreateStyle(float horizontalPadding = -1, float verticalPadding = -1)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(FocusedFill, 0f),
            BorderColor = new Color(FocusedEdge, 0f),
            DrawCenter = true,
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

    public static void Set(Node owner, StyleBoxFlat style, bool focused)
    {
        if (style == null)
        {
            return;
        }

        Color targetFill = focused ? FocusedFill : new Color(FocusedFill, 0f);
        Color targetEdge = focused ? FocusedEdge : new Color(FocusedEdge, 0f);

        if (owner == null || !owner.IsInsideTree())
        {
            style.BgColor = targetFill;
            style.BorderColor = targetEdge;
            return;
        }

        Tween fade = owner.CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        fade.TweenProperty(style, "bg_color", targetFill, FadeSeconds);
        fade.TweenProperty(style, "border_color", targetEdge, FadeSeconds);
    }
}
