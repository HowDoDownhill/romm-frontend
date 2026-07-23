using Godot;

public class MainSceneSectionHandler
{
    public enum Section
    {
        GameList,
        Downloads,
        Settings
    }

    private const float TransitionDuration = 0.18f;
    private const float FooterDuration = 0.12f;
    private const float EnterScale = 0.98f;

    private readonly MainScene mainScene;
    private Tween tween;

    public Section CurrentSection { get; private set; } = Section.GameList;

    public bool IsTransitioning { get; private set; }

    public MainSceneSectionHandler(MainScene mainScene)
    {
        this.mainScene = mainScene;
    }

    public void ToggleSettings()
    {
        ShowSection(CurrentSection == Section.Settings ? Section.GameList : Section.Settings);
    }

    public void ToggleDownloads()
    {
        ShowSection(CurrentSection == Section.Downloads ? Section.GameList : Section.Downloads);
    }

    private Control GameListContainer => mainScene.gameList?.GetParent()?.GetParent<Control>();

    private Control ContainerFor(Section section)
    {
        switch (section)
        {
            case Section.Downloads: return mainScene.downloadsListContainer;
            case Section.Settings: return mainScene.settingsMenuContainer;
            default: return GameListContainer;
        }
    }

    private Control FooterFor(Section section)
    {
        switch (section)
        {
            case Section.Downloads: return mainScene.downloadsFooter;
            case Section.Settings: return mainScene.settingsFooter;
            default: return mainScene.gameListFooter;
        }
    }

    public void ShowSection(Section target, bool animate = true)
    {
        if (target == CurrentSection && !IsTransitioning)
        {
            return;
        }

        Section previous = CurrentSection;
        CurrentSection = target;

        if (tween != null && tween.IsValid())
        {
            tween.Kill();
        }
        tween = null;
        ResetAllSections(target);

        Control outgoing = ContainerFor(previous);
        Control incoming = ContainerFor(target);
        Control outgoingFooter = FooterFor(previous);
        Control incomingFooter = FooterFor(target);

        mainScene.UpdateHeaderLabel();

        if (!animate || incoming == null)
        {
            FinishTransition(target, outgoing, outgoingFooter);
            return;
        }

        PrepareIncoming(incoming);
        PrepareIncoming(incomingFooter);

        IsTransitioning = true;
        tween = mainScene.CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        if (outgoing != null && outgoing != incoming)
        {
            tween.TweenProperty(outgoing, "modulate:a", 0.0f, TransitionDuration);
        }
        if (outgoingFooter != null && outgoingFooter != incomingFooter)
        {
            tween.TweenProperty(outgoingFooter, "modulate:a", 0.0f, FooterDuration);
        }

        tween.TweenProperty(incoming, "modulate:a", 1.0f, TransitionDuration);
        tween.TweenProperty(incoming, "scale", Vector2.One, TransitionDuration);
        if (incomingFooter != null)
        {
            tween.TweenProperty(incomingFooter, "modulate:a", 1.0f, FooterDuration);
        }

        tween.Chain().TweenCallback(Callable.From(() => FinishTransition(target, outgoing, outgoingFooter)));
    }

    private void PrepareIncoming(Control control)
    {
        if (control == null) return;

        control.Visible = true;
        Color transparent = control.Modulate;
        transparent.A = 0f;
        control.Modulate = transparent;
        control.PivotOffset = control.Size / 2f;
        control.Scale = new Vector2(EnterScale, EnterScale);
    }

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
        tween = null;

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

        GrabFocusFor(target);
    }

    private void GrabFocusFor(Section target)
    {
        if (target == Section.Settings)
        {
            mainScene.SettingsHandler.FocusFirstSettingsEntry();
            return;
        }

        if (target == Section.GameList && mainScene.gameList != null)
        {
            mainScene.gameList.GrabFocus();
            mainScene.GameListHandler.OnGameSelected((long)mainScene.gameList.Get("SelectedIndex"));
        }
    }
}
