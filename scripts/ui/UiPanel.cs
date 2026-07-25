using Godot;

[GlobalClass]
public partial class UiPanel : Control
{
    public enum PanelState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    public enum PanelTransition
    {
        None,
        Fade,
        ScaleFade
    }

    private const string MicaMaterialPath = "res://assets/materials/mica_panel.tres";

    [Export] public PanelTransition Transition = PanelTransition.ScaleFade;
    [Export] public float OpenDuration = 0.18f;
    [Export] public float CloseDuration = 0.12f;
    [Export] public float RestingScale = 0.96f;
    [Export] public bool UseMica = true;
    [Export] public int CornerRadius = 12;
    [Export] public float BackdropDim = 0.0f;
    [Export] public bool CenterContent = true;
    [Export] public int ShadowSize = MicaShadow.DefaultSize;
    [Export] public Color ShadowColor = MicaShadow.DefaultColor;
    [Export] public Vector2 ShadowOffset = MicaShadow.DefaultOffset;
    [Export] public Control ContentRoot;

    private Tween activeTween;

    public PanelState State { get; private set; } = PanelState.Closed;

    public bool IsOpen => State == PanelState.Open || State == PanelState.Opening;

    public bool IsBusy => State == PanelState.Opening || State == PanelState.Closing;

    [Signal]
    public delegate void OpenedEventHandler();

    [Signal]
    public delegate void ClosedEventHandler();

    [Signal]
    public delegate void AboutToOpenEventHandler();

    [Signal]
    public delegate void AboutToCloseEventHandler();

    public override void _Ready()
    {
        Visible = false;
        State = PanelState.Closed;
        MouseFilter = MouseFilterEnum.Ignore;

        if (ContentRoot == null)
        {
            ContentRoot = BuildDefaultChrome();
        }
    }

    public void Open(bool animate = true)
    {
        if (State == PanelState.Open || State == PanelState.Opening) return;

        KillActiveTween();
        State = PanelState.Opening;
        Visible = true;
        EmitSignal(SignalName.AboutToOpen);

        Control scaleTarget = ScaleTarget;

        if (!animate || Transition == PanelTransition.None)
        {
            SetModulateAlpha(1.0f);
            scaleTarget.Scale = Vector2.One;
            FinishOpen();
            return;
        }

        SetModulateAlpha(0.0f);
        CentrePivot(scaleTarget);

        if (Transition == PanelTransition.ScaleFade)
        {
            scaleTarget.Scale = new Vector2(RestingScale, RestingScale);
        }

        activeTween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        activeTween.TweenProperty(this, "modulate:a", 1.0f, OpenDuration);

        if (Transition == PanelTransition.ScaleFade)
        {
            activeTween.TweenProperty(scaleTarget, "scale", Vector2.One, OpenDuration);
        }

        activeTween.Chain().TweenCallback(Callable.From(FinishOpen));
    }

    public void Close(bool animate = true)
    {
        if (State == PanelState.Closed || State == PanelState.Closing) return;

        EmitSignal(SignalName.AboutToClose);
        KillActiveTween();
        State = PanelState.Closing;

        Control scaleTarget = ScaleTarget;

        if (!animate || Transition == PanelTransition.None)
        {
            FinishClose();
            return;
        }

        CentrePivot(scaleTarget);

        activeTween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        activeTween.TweenProperty(this, "modulate:a", 0.0f, CloseDuration);

        if (Transition == PanelTransition.ScaleFade)
        {
            activeTween.TweenProperty(scaleTarget, "scale", new Vector2(RestingScale, RestingScale), CloseDuration);
        }

        activeTween.Chain().TweenCallback(Callable.From(FinishClose));
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    protected virtual void OnOpened()
    {
    }

    protected virtual void OnClosed()
    {
    }

    public virtual void HandleInput(InputEvent inputEvent)
    {
    }

    private Control ScaleTarget => ContentRoot ?? this;

    private Control BuildDefaultChrome()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        if (BackdropDim > 0.0f)
        {
            var backdrop = new ColorRect { Color = new Color(0, 0, 0, BackdropDim) };
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(backdrop);
        }

        var panel = new PanelContainer();

        if (CenterContent)
        {
            var centreContainer = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            centreContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(centreContainer);
            centreContainer.AddChild(panel);
        }
        else
        {
            AddChild(panel);
        }

        StylePanel(panel);
        return panel;
    }

    private void StylePanel(PanelContainer panel)
    {
        if (UseMica)
        {
            panel.Material = GD.Load<ShaderMaterial>(MicaMaterialPath);
        }

        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 1f),
            CornerRadiusTopLeft = CornerRadius,
            CornerRadiusTopRight = CornerRadius,
            CornerRadiusBottomLeft = CornerRadius,
            CornerRadiusBottomRight = CornerRadius
        });

        if (ShadowSize > 0)
        {
            MicaShadow.Attach(panel, ShadowColor, ShadowSize, ShadowOffset);
        }
    }

    private void FinishOpen()
    {
        activeTween = null;
        State = PanelState.Open;
        OnOpened();
        EmitSignal(SignalName.Opened);
    }

    private void FinishClose()
    {
        activeTween = null;
        State = PanelState.Closed;
        Visible = false;
        SetModulateAlpha(1.0f);

        Control scaleTarget = ScaleTarget;
        if (GodotObject.IsInstanceValid(scaleTarget))
        {
            scaleTarget.Scale = Vector2.One;
        }

        OnClosed();
        EmitSignal(SignalName.Closed);
    }

    private void SetModulateAlpha(float alpha)
    {
        Color tint = Modulate;
        tint.A = alpha;
        Modulate = tint;
    }

    private void KillActiveTween()
    {
        if (activeTween != null && activeTween.IsValid())
        {
            activeTween.Kill();
        }
        activeTween = null;
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
}
