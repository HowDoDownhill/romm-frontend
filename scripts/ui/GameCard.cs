using Godot;

public partial class GameCard : Control, ICarouselItem
{
    private const float CardPadding = 12.0f;
    private const float RevealDuration = 0.18f;

    private static readonly ShaderMaterial MicaMaterial =
        GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");

    private Control body;
    private PanelContainer cardPanel;
    private TextureRect coverRect;
    private Label captionLabel;
    private Label titleLabel;
    private TextureRect installedIcon;
    private Panel focusPanel;

    private Control Body => body ??= GetNode<Control>("Body");
    private PanelContainer CardPanel => cardPanel ??= GetNode<PanelContainer>("Body/CardPanel");
    private TextureRect CoverRect => coverRect ??= GetNode<TextureRect>("Body/CardPanel/Content/Stack/Cover");
    private Label CaptionLabel => captionLabel ??= GetNode<Label>("Body/CardPanel/Content/Stack/Caption");
    private Label TitleLabel => titleLabel ??= GetNode<Label>("Body/TitleLabel");
    private TextureRect InstalledIcon => installedIcon ??= GetNode<TextureRect>("Body/InstalledIcon");
    private Panel FocusPanel => focusPanel ??= GetNode<Panel>("Body/FocusPanel");

    public Texture2D Cover => CoverRect.Texture;

    public bool HasRealCover { get; private set; }

    public void SetCover(Texture2D texture, bool isPlaceholder)
    {
        HasRealCover = !isPlaceholder && texture != null;
        CoverRect.Texture = texture;

        TitleLabel.Visible = !HasRealCover;
        CaptionLabel.Visible = HasRealCover;
    }

    public void Reveal()
    {
        if (revealed)
        {
            return;
        }
        revealed = true;

        if (!IsInsideTree())
        {
            SetBodyAlpha(1.0f);
            return;
        }

        SetBodyAlpha(0.0f);
        revealTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        revealTween.TweenProperty(Body, "modulate:a", 1.0f, RevealDuration);
    }

    private void SetBodyAlpha(float alpha)
    {
        if (revealTween != null && revealTween.IsValid())
        {
            revealTween.Kill();
        }
        revealTween = null;

        Color modulate = Body.Modulate;
        modulate.A = alpha;
        Body.Modulate = modulate;
    }

    public void ResetReveal()
    {
        revealed = false;
        SetBodyAlpha(0.0f);
    }

    private bool revealed;
    private Tween revealTween;

    public string Title
    {
        set
        {
            TitleLabel.Text = value;
            CaptionLabel.Text = value;
        }
    }

    private bool selected;

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            FocusPanel.Visible = value;
            CardPanel.Material = value ? MicaMaterial : null;
        }
    }

    public void SetInstalledIcon(Texture2D icon)
    {
        InstalledIcon.Texture = icon;
        InstalledIcon.Visible = icon != null;
    }

    public float CoverAspectRatio
    {
        get
        {
            Texture2D texture = CoverRect.Texture;
            if (texture == null || texture.GetSize().X <= 0)
            {
                return 0.0f;
            }

            float coverAspect = texture.GetSize().Y / texture.GetSize().X;
            float cardWidth = CustomMinimumSize.X;

            if (cardWidth <= 0.0f)
            {
                return coverAspect;
            }

            float innerWidth = Mathf.Max(cardWidth - (2.0f * CardPadding), 1.0f);
            float captionHeight = CaptionLabel.Visible
                ? CaptionLabel.GetCombinedMinimumSize().Y + CaptionLabel.GetParent<BoxContainer>().GetThemeConstant("separation")
                : 0.0f;
            float cardHeight = (innerWidth * coverAspect) + (2.0f * CardPadding) + captionHeight;

            return cardHeight / cardWidth;
        }
    }
}
