using Godot;
using System.Collections.Generic;

public static class MicaBorder
{
    public static readonly Color DefaultColor = new Color(1f, 1f, 1f, 0.4f);
    public const int DefaultWidth = 1;

    public static void Attach(Control panel, Color color, int width = DefaultWidth)
    {
        if (panel == null || panel.HasNode("MicaBorder")) return;

        int radius = 0;
        if (panel.GetThemeStylebox("panel") is StyleBoxFlat existing)
            radius = existing.CornerRadiusTopLeft;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = color,
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };

        var overlay = new Panel
        {
            Name = "MicaBorder",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlay.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public static void AttachToAll(Node root, ShaderMaterial material, Color color, int width = DefaultWidth)
    {
        var found = new List<Control>();
        Collect(root, material, found);
        foreach (var c in found)
            Attach(c, color, width);
    }

    private static void Collect(Node node, ShaderMaterial material, List<Control> outList)
    {
        if (node is Control c && c.Material == material)
            outList.Add(c);
        foreach (Node child in node.GetChildren())
            Collect(child, material, outList);
    }
}
