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
        
        var leftArrow = new Label();
        leftArrow.Text = "< ";
        leftArrow.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
        leftArrow.AddThemeFontSizeOverride("font_size", 20);
        AddChild(leftArrow);

        valueLabel = new Label();
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        valueLabel.AddThemeFontSizeOverride("font_size", 20);
        AddChild(valueLabel);

        var rightArrow = new Label();
        rightArrow.Text = " >";
        rightArrow.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
        rightArrow.AddThemeFontSizeOverride("font_size", 20);
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
