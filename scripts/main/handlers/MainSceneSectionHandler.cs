using Godot;

public class MainSceneSectionHandler
{
    public enum Section
    {
        GameList,
        Downloads,
        Settings
    }

    private const float FooterDuration = 0.12f;

    private static readonly Section[] AllSections = { Section.GameList, Section.Downloads, Section.Settings };

    private readonly MainScene mainScene;
    private Tween footerTween;

    public Section CurrentSection { get; private set; } = Section.GameList;

    public MainSceneSectionHandler(MainScene mainScene)
    {
        this.mainScene = mainScene;
    }

    public bool IsTransitioning
    {
        get
        {
            foreach (Section section in AllSections)
            {
                if (PanelFor(section)?.IsBusy == true) return true;
            }
            return false;
        }
    }

    public void Initialise()
    {
        foreach (Section section in AllSections)
        {
            UiPanel panel = PanelFor(section);
            if (panel == null) continue;

            if (section == CurrentSection) panel.Open(false);
            else panel.Close(false);
        }

        ApplyFooters(CurrentSection, CurrentSection, false);

        foreach (Section section in AllSections)
        {
            UiPanel panel = PanelFor(section);
            if (panel == null) continue;

            Section arrived = section;
            panel.Opened += () => GrabFocusFor(arrived);
        }
    }

    public void ToggleSettings()
    {
        ShowSection(CurrentSection == Section.Settings ? Section.GameList : Section.Settings);
    }

    public void ToggleDownloads()
    {
        ShowSection(CurrentSection == Section.Downloads ? Section.GameList : Section.Downloads);
    }

    public void ShowSection(Section target, bool animate = true)
    {
        if (target == CurrentSection && !IsTransitioning) return;

        Section previous = CurrentSection;
        CurrentSection = target;

        mainScene.UpdateHeaderLabel();

        foreach (Section section in AllSections)
        {
            UiPanel panel = PanelFor(section);
            if (panel == null) continue;

            if (section == target) panel.Open(animate);
            else panel.Close(false);
        }

        ApplyFooters(target, previous, animate);
    }

    private UiPanel PanelFor(Section section)
    {
        switch (section)
        {
            case Section.Downloads: return mainScene.downloadsListContainer;
            case Section.Settings: return mainScene.settingsMenuContainer;
            default: return mainScene.gameListSection;
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

    private void ApplyFooters(Section target, Section previous, bool animate)
    {
        if (footerTween != null && footerTween.IsValid())
        {
            footerTween.Kill();
        }
        footerTween = null;

        Control incoming = FooterFor(target);
        Control outgoing = FooterFor(previous);

        foreach (Section section in AllSections)
        {
            Control footer = FooterFor(section);
            if (footer == null || footer == incoming || footer == outgoing) continue;
            SetVisible(footer, false);
        }

        if (incoming == null) return;

        if (!animate)
        {
            if (outgoing != null && outgoing != incoming) SetVisible(outgoing, false);
            SetVisible(incoming, true);
            return;
        }

        incoming.Visible = true;
        SetAlpha(incoming, 0f);

        footerTween = mainScene.CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        if (outgoing != null && outgoing != incoming)
        {
            footerTween.TweenProperty(outgoing, "modulate:a", 0f, FooterDuration);
        }

        footerTween.TweenProperty(incoming, "modulate:a", 1f, FooterDuration);
        footerTween.Chain().TweenCallback(Callable.From(() => SettleFooters(target)));
    }

    private void SettleFooters(Section target)
    {
        footerTween = null;

        foreach (Section section in AllSections)
        {
            Control footer = FooterFor(section);
            if (footer == null) continue;
            SetVisible(footer, section == target);
        }
    }

    private static void SetVisible(Control control, bool visible)
    {
        SetAlpha(control, 1f);
        control.Visible = visible;
    }

    private static void SetAlpha(Control control, float alpha)
    {
        Color tint = control.Modulate;
        tint.A = alpha;
        control.Modulate = tint;
    }

    private void GrabFocusFor(Section target)
    {
        if (target != CurrentSection) return;

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
