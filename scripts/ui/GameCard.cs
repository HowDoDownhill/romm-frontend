using Godot;

// One entry in the game carousel.
//
// Entries used to be a bare TextureRect that MainSceneGameListHandler decorated in code on every
// rebuild, with a separate HoverPopupOverlay drawing an imitation of the selected entry on top of
// it. Those were two objects that had to agree on size and position and never quite did -- the real
// art visibly peeked out from behind the popup. The card is now the only object: selecting it
// changes its own appearance rather than spawning a copy, so there is nothing to misalign, and a
// cover that finishes loading updates the card because it *is* the card.
public partial class GameCard : Control, ICarouselItem
{
    // Matches the margins set on the Content node in the scene.
    private const float CardPadding = 12.0f;
    private const float RevealDuration = 0.18f;

    private static readonly ShaderMaterial MicaMaterial =
        GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");

    // Resolved lazily rather than in _Ready: the list handler assigns content immediately after
    // Instantiate(), which is before the node enters the tree and _Ready runs.
    private Control body;
    private PanelContainer cardPanel;
    private TextureRect coverRect;
    private Label captionLabel;
    private Label titleLabel;
    private TextureRect installedIcon;
    private Panel focusPanel;

    // Everything visible hangs off Body so the reveal can fade the whole card. Modulate is
    // hierarchical, so this composes with the root modulate VerticalCarousel writes for depth
    // falloff instead of fighting it.
    private Control Body => body ??= GetNode<Control>("Body");
    private PanelContainer CardPanel => cardPanel ??= GetNode<PanelContainer>("Body/CardPanel");
    private TextureRect CoverRect => coverRect ??= GetNode<TextureRect>("Body/CardPanel/Content/Stack/Cover");
    private Label CaptionLabel => captionLabel ??= GetNode<Label>("Body/CardPanel/Content/Stack/Caption");
    private Label TitleLabel => titleLabel ??= GetNode<Label>("Body/TitleLabel");
    private TextureRect InstalledIcon => installedIcon ??= GetNode<TextureRect>("Body/InstalledIcon");
    private Panel FocusPanel => focusPanel ??= GetNode<Panel>("Body/FocusPanel");

    public Texture2D Cover => CoverRect.Texture;

    // Whether the card is showing actual art rather than the shared placeholder. The fallback title
    // is the centred label that stands in for missing art; the caption is the name under the cover.
    public bool HasRealCover { get; private set; }

    public void SetCover(Texture2D texture, bool isPlaceholder)
    {
        HasRealCover = !isPlaceholder && texture != null;
        CoverRect.Texture = texture;

        // Exactly one of the two labels shows at a time. Without art the centred title fills the
        // empty cover area; with art the caption sits under it. Showing both named the game twice.
        TitleLabel.Visible = !HasRealCover;
        CaptionLabel.Visible = HasRealCover;
    }

    // Cards are built hidden (Body starts at alpha 0 in the scene) and revealed once their cover
    // load has been *attempted*, so the list fills in with finished cards rather than flashing a
    // grid of empty placeholders while decoding catches up.
    //
    // Revealing on the attempt rather than on success is deliberate: plenty of games have no art at
    // all, and gating on success would leave those cards invisible forever.
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
            // Only the selected card gets the frosted material. Mica samples the back buffer, and
            // carousel cards overlap by design, so frosting all of them would have them blurring
            // each other's output instead of the background.
            CardPanel.Material = value ? MicaMaterial : null;
        }
    }

    public void SetInstalledIcon(Texture2D icon)
    {
        InstalledIcon.Texture = icon;
        InstalledIcon.Visible = icon != null;
    }

    // Reports the aspect of the whole card, not just the cover: the carousel sets the card's width
    // and derives its height from this, so padding and the caption strip have to be included or the
    // cover gets letterboxed by exactly the space they occupy.
    //
    // Relies on VerticalCarousel assigning CustomMinimumSize.X before it asks.
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
