using Godot;
using System.Collections.Generic;

public partial class CarouselButton : HBoxContainer
{
    private Label valueLabel;
    public List<KeyValuePair<string, Variant>> Options = new List<KeyValuePair<string, Variant>>();
    
    public int Selected { get; private set; } = -1;
    public int ItemCount => Options.Count;
    
    private bool disabled = false;
    public bool Disabled 
    { 
        get => disabled; 
        set 
        { 
            disabled = value;
            Modulate = disabled ? new Color(1, 1, 1, 0.5f) : new Color(1, 1, 1, 1);
        } 
    }

    [Signal]
    public delegate void ItemSelectedEventHandler(long index);

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        
        leftArrow = CreateArrow(LeftChevron);
        AddChild(leftArrow);

        valueLabel = new Label();
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(valueLabel);

        rightArrow = CreateArrow(RightChevron);
        AddChild(rightArrow);

        if (Selected != -1)
        {
            Select(Selected);
        }
    }

    public void AddItem(string label, int id = -1)
    {
        Options.Add(new KeyValuePair<string, Variant>(label, id));
        if (Selected == -1)
        {
            Selected = 0;
        }
    }

    public void SetItemMetadata(int index, Variant meta)
    {
        if (index >= 0 && index < Options.Count)
        {
            Options[index] = new KeyValuePair<string, Variant>(Options[index].Key, meta);
        }
    }

    public Variant GetItemMetadata(int index)
    {
        if (index >= 0 && index < Options.Count) return Options[index].Value;
        return default;
    }
    
    public string GetItemText(int index)
    {
        if (index >= 0 && index < Options.Count) return Options[index].Key;
        return "";
    }

    private static readonly Texture2D LeftChevron = GD.Load<Texture2D>("res://assets/icons/chevron_left.svg");
    private static readonly Texture2D RightChevron = GD.Load<Texture2D>("res://assets/icons/chevron_right.svg");
    private static readonly Color ArrowRestingTint = new Color(0.635f, 0.659f, 0.733f, 0.85f);
    private static readonly Color ArrowActiveTint = new Color(0.961f, 0.965f, 0.98f, 1.0f);
    private const float ArrowSize = 28.0f;
    private const float ArrowPulseScale = 1.25f;
    private const float ArrowPulseSeconds = 0.2f;

    private TextureRect leftArrow;
    private TextureRect rightArrow;

    private static TextureRect CreateArrow(Texture2D chevron)
    {
        return new TextureRect
        {
            Texture = chevron,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(ArrowSize, ArrowSize),
            SelfModulate = ArrowRestingTint
        };
    }

    public void Step(int direction)
    {
        if (ItemCount == 0 || Disabled) return;

        int newIndex = ((Selected + direction) % ItemCount + ItemCount) % ItemCount;
        Select(newIndex);
        PulseArrow(direction < 0 ? leftArrow : rightArrow);
        EmitSignal(SignalName.ItemSelected, newIndex);
    }

    private static void PulseArrow(TextureRect arrow)
    {
        if (arrow == null) return;

        arrow.PivotOffset = arrow.Size / 2.0f;
        arrow.SelfModulate = ArrowActiveTint;
        arrow.Scale = new Vector2(ArrowPulseScale, ArrowPulseScale);

        var arrowTween = arrow.CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        arrowTween.TweenProperty(arrow, "self_modulate", ArrowRestingTint, ArrowPulseSeconds);
        arrowTween.TweenProperty(arrow, "scale", Vector2.One, ArrowPulseSeconds);
    }

    public void Select(int index)
    {
        if (index >= 0 && index < Options.Count)
        {
            Selected = index;
            if (valueLabel != null)
            {
                valueLabel.Text = Options[index].Key;
            }
        }
    }
}
