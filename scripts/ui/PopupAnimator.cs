using Godot;
using System.Collections.Generic;

public static class PopupAnimator
{
    private const float ShowDuration = 0.18f;
    private const float HideDuration = 0.12f;
    private const float RestingScale = 0.96f;

    private static readonly Dictionary<ulong, Tween> running = new Dictionary<ulong, Tween>();
    private static readonly HashSet<ulong> hiding = new HashSet<ulong>();

    public static bool IsHiding(Control popup)
    {
        return popup != null && hiding.Contains(popup.GetInstanceId());
    }

    public static void Show(Control popup, Control scaleTarget = null)
    {
        if (popup == null) return;
        if (popup.Visible && !IsHiding(popup)) return;

        KillRunning(popup);
        hiding.Remove(popup.GetInstanceId());
        scaleTarget ??= popup;

        popup.Visible = true;

        Color transparent = popup.Modulate;
        transparent.A = 0.0f;
        popup.Modulate = transparent;

        CentrePivot(scaleTarget);
        scaleTarget.Scale = new Vector2(RestingScale, RestingScale);

        Tween tween = popup.CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(popup, "modulate:a", 1.0f, ShowDuration);
        tween.TweenProperty(scaleTarget, "scale", Vector2.One, ShowDuration);

        Track(popup, tween);
    }

    public static void Hide(Control popup, Control scaleTarget = null)
    {
        if (popup == null || !popup.Visible) return;
        if (IsHiding(popup)) return;

        KillRunning(popup);
        hiding.Add(popup.GetInstanceId());
        scaleTarget ??= popup;

        CentrePivot(scaleTarget);

        Tween tween = popup.CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(popup, "modulate:a", 0.0f, HideDuration);
        tween.TweenProperty(scaleTarget, "scale", new Vector2(RestingScale, RestingScale), HideDuration);
        tween.Chain().TweenCallback(Callable.From(() => Settle(popup, scaleTarget)));

        Track(popup, tween);
    }

    private static void Settle(Control popup, Control scaleTarget)
    {
        if (!GodotObject.IsInstanceValid(popup)) return;

        popup.Visible = false;
        running.Remove(popup.GetInstanceId());
        hiding.Remove(popup.GetInstanceId());

        Color opaque = popup.Modulate;
        opaque.A = 1.0f;
        popup.Modulate = opaque;

        if (GodotObject.IsInstanceValid(scaleTarget))
        {
            scaleTarget.Scale = Vector2.One;
        }
    }

    private static void CentrePivot(Control scaleTarget)
    {
        scaleTarget.PivotOffset = scaleTarget.Size / 2.0f;

        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(scaleTarget))
            {
                scaleTarget.PivotOffset = scaleTarget.Size / 2.0f;
            }
        }).CallDeferred();
    }

    private static void KillRunning(Control popup)
    {
        ulong id = popup.GetInstanceId();
        if (running.TryGetValue(id, out Tween existing) && existing != null && existing.IsValid())
        {
            existing.Kill();
        }
        running.Remove(id);
    }

    private static void Track(Control popup, Tween tween)
    {
        running[popup.GetInstanceId()] = tween;
    }
}
