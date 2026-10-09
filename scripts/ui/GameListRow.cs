using Godot;

public partial class GameListRow : Control, IGameListItem
{
    private const float HorizontalPadding = 18.0f;
    private const float VerticalPadding = 10.0f;
    private const float IconSize = 30.0f;
    private const float RestingTextAlpha = 0.72f;

    private readonly Label titleLabel = new Label();
    private readonly TextureRect installedIcon = new TextureRect();
    private bool selected;

    public bool ShowsCover => false;

    public static float HeightForText(float textHeight)
    {
        return Mathf.Max(textHeight, IconSize) + VerticalPadding * 2.0f;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", (int)HorizontalPadding);
        margin.AddThemeConstantOverride("margin_right", (int)HorizontalPadding);
        AddChild(margin);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleLabel.ClipText = true;
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(titleLabel);

        installedIcon.CustomMinimumSize = new Vector2(IconSize, IconSize);
        installedIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        installedIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        installedIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        installedIcon.MouseFilter = MouseFilterEnum.Ignore;
        installedIcon.Visible = false;
        row.AddChild(installedIcon);

        ApplySelectedLook();
    }

    public string Title
    {
        set => titleLabel.Text = value;
    }

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            ApplySelectedLook();
        }
    }

    public void SetCover(Texture2D texture, bool isPlaceholder)
    {
    }

    public void SetInstalledIcon(Texture2D icon)
    {
        installedIcon.Texture = icon;
        installedIcon.Visible = icon != null;
    }

    public void Reveal()
    {
    }

    public void ResetReveal()
    {
    }

    private void ApplySelectedLook()
    {
        titleLabel.Modulate = new Color(1, 1, 1, selected ? 1.0f : RestingTextAlpha);
    }
}
