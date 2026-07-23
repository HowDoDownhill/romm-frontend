using Godot;
using System.Collections.Generic;

// Shared show/hide animation for the overlay popups, so they use the same motion vocabulary as the
// rest of the UI (Cubic/Out, ~0.18s in) instead of snapping into place.
//
// `scaleTarget` is the element that should grow. For a popup with a dimmed backdrop it must NOT be
// the root: scaling the root scales the backdrop too, pulling its edges away from the screen. Pass
// the inner panel and let the root handle only the fade.
public static class PopupAnimator
{
    private const float ShowDuration = 0.18f;
    private const float HideDuration = 0.12f;
    private const float RestingScale = 0.96f;

    // Keyed by instance id rather than by the Control so a freed popup can't keep an entry alive.
    private static readonly Dictionary<ulong, Tween> running = new Dictionary<ulong, Tween>();
    private static readonly HashSet<ulong> hiding = new HashSet<ulong>();

    // A popup mid-hide still reports Visible == true, so callers that re-show on demand need to be
    // able to tell "already open" from "currently closing" -- otherwise a popup dismissed and
    // immediately re-triggered finishes fading out and disappears.
    public static bool IsHiding(Control popup)
    {
        return popup != null && hiding.Contains(popup.GetInstanceId());
    }

    // Both Show and Hide are idempotent. Callers drive these from _Process, so a non-idempotent
    // version restarts its tween every frame: the animation is killed after one frame of progress
    // and begins again from the current value, so it creeps along and never arrives.
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

    // Restores opacity and scale after hiding. Any code that still flips Visible directly then gets
    // a correctly-rendered popup rather than an invisible or shrunken one.
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

    // Content-sized popups (the fuzzy search box grows with the query) have not been laid out at
    // their new size yet when Show is called, so the pivot is set once from the current size and
    // again after the layout pass. Scale is applied per frame, so the later correction just
    // re-centres an animation already in flight.
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

    // Reopening mid-close (or vice versa) must not leave the popup stranded part-way through the
    // previous animation.
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
