using Godot;

public partial class GameGridItem : Control
{
    [Export]
    private TextureRect cover;
    [Export]
    private Label title;

    private Game game;
    private StyleBoxFlat focusStyle;

    [Signal]
    public delegate void ItemSelectedEventHandler(GameGridItem item);

    public override void _Ready()
    {
        focusStyle = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0), // Transparent background
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color("#4f8fcf"), // A nice blue color for focus
            DrawCenter = false // We only want the border
        };

        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
    }

    private void OnFocusEntered()
    {
        AddThemeStyleboxOverride("panel", focusStyle);
    }

    private void OnFocusExited()
    {
        RemoveThemeStyleboxOverride("panel");
    }

    public void SetGame(Game game)
    {
        this.game = game;

        if (title != null)
        {
            title.Text = game.Name;
        }
    }

    public Game GetGame()
    {
        return game;
    }

    public void SetCoverTexture(Texture2D texture)
    {
        if (cover != null)
        {
            cover.Texture = texture;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        bool isSelected = false;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            isSelected = true;
        }

        else if (@event.IsActionPressed("ui_accept"))
        {
            isSelected = true;
        }

        if (isSelected)
        {
            EmitSignal(SignalName.ItemSelected, this);
        }
    }
}

