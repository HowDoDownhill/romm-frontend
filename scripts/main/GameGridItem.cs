using Godot;

public partial class GameGridItem : Control
{
    [Export]
    private TextureRect _cover;
    [Export]
    private Label _title;

    private Game _game;
    private StyleBoxFlat _focusStyle;

    [Signal]
    public delegate void ItemSelectedEventHandler(GameGridItem item);

    public override void _Ready()
    {
        _focusStyle = new StyleBoxFlat
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
        AddThemeStyleboxOverride("panel", _focusStyle);
    }

    private void OnFocusExited()
    {
        RemoveThemeStyleboxOverride("panel");
    }

    public void SetGame(Game game)
    {
        _game = game;
        if (_title != null)
        {
            _title.Text = game.Name;
        }
    }

    public Game GetGame()
    {
        return _game;
    }

    public void SetCoverTexture(Texture2D texture)
    {
        if (_cover != null)
        {
            _cover.Texture = texture;
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
