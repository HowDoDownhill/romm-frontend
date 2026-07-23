using Godot;

// Owns which top-level section is on screen and animates the change.
//
// Section state used to live implicitly in each container's Visible flag, with ToggleSettingsMenu
// and SwapLists independently re-deriving which footer belonged to which section and duplicating
// the focus restore. CurrentSection is now the single source of truth; the containers' Visible
// flags are just what the transition happens to have rendered.
public class MainSceneSectionHandler
{
    public enum Section
    {
        GameList,
        Downloads,
        Settings
    }

    // Matches HoverPopupOverlay's 0.15-0.2s Cubic/Out so section changes read as the same motion
    // vocabulary as the rest of the UI rather than a second, unrelated one.
    private const float TransitionDuration = 0.18f;
    private const float FooterDuration = 0.12f;
    // Incoming sections start fractionally small and settle, which reads as depth without the
    // layout fight that animating position would cause (containers overwrite child positions).
    private const float EnterScale = 0.98f;

    private readonly MainScene _mainScene;
    private Tween _tween;

    public Section CurrentSection { get; private set; } = Section.GameList;

    // MainScene._Input routes on the containers' Visible flags, but during a crossfade the outgoing
    // and incoming sections are both visible, so those checks cannot say which one owns the input.
    // Every input path is gated on this instead of rewriting ~15 call sites.
    public bool IsTransitioning { get; private set; }

    public MainSceneSectionHandler(MainScene mainScene)
    {
        _mainScene = mainScene;
    }

    public void ToggleSettings()
    {
        ShowSection(CurrentSection == Section.Settings ? Section.GameList : Section.Settings);
    }

    public void ToggleDownloads()
    {
        ShowSection(CurrentSection == Section.Downloads ? Section.GameList : Section.Downloads);
    }

    // The games list has no exported field of its own -- it is the game carousel's grandparent.
    private Control GameListContainer => _mainScene.gameList?.GetParent()?.GetParent<Control>();

    private Control ContainerFor(Section section)
    {
        switch (section)
        {
            case Section.Downloads: return _mainScene.downloadsListContainer;
            case Section.Settings: return _mainScene.settingsMenuContainer;
            default: return GameListContainer;
        }
    }

    private Control FooterFor(Section section)
    {
        switch (section)
        {
            case Section.Downloads: return _mainScene.downloadsFooter;
            case Section.Settings: return _mainScene.settingsFooter;
            default: return _mainScene.gameListFooter;
        }
    }

    public void ShowSection(Section target, bool animate = true)
    {
        if (target == CurrentSection && !IsTransitioning)
        {
            return;
        }

        Section previous = CurrentSection;
        // Flip the logical state up front so anything asking "where am I?" mid-transition -- the
        // header label especially -- describes where we are going, not where we came from.
        CurrentSection = target;

        // An interrupted transition must not strand a container at half alpha or the wrong
        // visibility, so kill the running tween and snap every section to a known state first.
        if (_tween != null && _tween.IsValid())
        {
            _tween.Kill();
        }
        _tween = null;
        ResetAllSections(target);

        Control outgoing = ContainerFor(previous);
        Control incoming = ContainerFor(target);
        Control outgoingFooter = FooterFor(previous);
        Control incomingFooter = FooterFor(target);

        _mainScene.UpdateHeaderLabel();

        if (!animate || incoming == null)
        {
            FinishTransition(target, outgoing, outgoingFooter);
            return;
        }

        PrepareIncoming(incoming);
        PrepareIncoming(incomingFooter);

        IsTransitioning = true;
        _tween = _mainScene.CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        if (outgoing != null && outgoing != incoming)
        {
            _tween.TweenProperty(outgoing, "modulate:a", 0.0f, TransitionDuration);
        }
        if (outgoingFooter != null && outgoingFooter != incomingFooter)
        {
            _tween.TweenProperty(outgoingFooter, "modulate:a", 0.0f, FooterDuration);
        }

        _tween.TweenProperty(incoming, "modulate:a", 1.0f, TransitionDuration);
        _tween.TweenProperty(incoming, "scale", Vector2.One, TransitionDuration);
        if (incomingFooter != null)
        {
            _tween.TweenProperty(incomingFooter, "modulate:a", 1.0f, FooterDuration);
        }

        _tween.Chain().TweenCallback(Callable.From(() => FinishTransition(target, outgoing, outgoingFooter)));
    }

    private void PrepareIncoming(Control control)
    {
        if (control == null) return;

        control.Visible = true;
        Color transparent = control.Modulate;
        transparent.A = 0f;
        control.Modulate = transparent;
        // Scale from the middle so the settle reads as the panel arriving, not sliding off-corner.
        control.PivotOffset = control.Size / 2f;
        control.Scale = new Vector2(EnterScale, EnterScale);
    }

    // Snaps every section except `keepVisible` to hidden and fully opaque, so a transition always
    // starts from a clean slate no matter what a killed tween left behind.
    private void ResetAllSections(Section keepVisible)
    {
        foreach (Section section in new[] { Section.GameList, Section.Downloads, Section.Settings })
        {
            if (section == keepVisible) continue;
            RestoreControl(ContainerFor(section), false);
            RestoreControl(FooterFor(section), false);
        }
    }

    private static void RestoreControl(Control control, bool visible)
    {
        if (control == null) return;

        Color opaque = control.Modulate;
        opaque.A = 1f;
        control.Modulate = opaque;
        control.Scale = Vector2.One;
        control.Visible = visible;
    }

    private void FinishTransition(Section target, Control outgoing, Control outgoingFooter)
    {
        IsTransitioning = false;
        _tween = null;

        Control incoming = ContainerFor(target);
        Control incomingFooter = FooterFor(target);

        if (outgoing != null && outgoing != incoming)
        {
            RestoreControl(outgoing, false);
        }
        if (outgoingFooter != null && outgoingFooter != incomingFooter)
        {
            RestoreControl(outgoingFooter, false);
        }

        RestoreControl(incoming, true);
        RestoreControl(incomingFooter, true);

        // Focus only once the incoming section has actually arrived, otherwise controller input
        // lands on a list that is still fading out.
        GrabFocusFor(target);
    }

    private void GrabFocusFor(Section target)
    {
        if (target == Section.Settings)
        {
            _mainScene.SettingsHandler.FocusFirstSettingsEntry();
            return;
        }

        if (target == Section.GameList && _mainScene.gameList != null)
        {
            _mainScene.gameList.GrabFocus();
            _mainScene.GameListHandler.OnGameSelected((long)_mainScene.gameList.Get("SelectedIndex"));
        }
    }
}
