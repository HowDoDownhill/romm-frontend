using Godot;
using System.Collections.Generic;

// Adds a native StyleBoxFlat outline on top of a mica frosted-glass panel so adjacent/overlapping
// panels are visually separable. We use a real stylebox instead of drawing the border in the mica
// shader because a shader border reconstructed from UV derivatives ends up ~1px asymmetric
// (top/left vs bottom/right) due to sub-pixel rasterization. Godot renders stylebox borders
// pixel-perfectly and symmetrically, and follows each panel's own rounded-corner radius.
public static class MicaBorder
{
    // Default outline appearance. Tweak here (or pass explicit args) to restyle every panel at once.
    public static readonly Color DefaultColor = new Color(1f, 1f, 1f, 0.4f);
    public const int DefaultWidth = 1;

    // Attaches a border overlay to a single frosted panel. Reads the panel's existing corner radius
    // so the outline hugs the same rounded corners. Safe to call twice (no-op if already added).
    public static void Attach(Control panel, Color color, int width = DefaultWidth)
    {
        if (panel == null || panel.HasNode("MicaBorder")) return;

        int radius = 0;
        if (panel.GetThemeStylebox("panel") is StyleBoxFlat existing)
            radius = existing.CornerRadiusTopLeft;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0), // transparent: the frost shows through, only the border draws
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
            MouseFilter = Control.MouseFilterEnum.Ignore, // never intercept input
        };
        overlay.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    // Attaches a border to every Control under `root` that uses `material` (the shared mica material).
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
