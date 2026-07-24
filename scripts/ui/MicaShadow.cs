using Godot;
using System.Collections.Generic;

public static class MicaShadow
{
    public static readonly Color DefaultColor = new Color(0f, 0f, 0f, 0.3f);
    public static readonly Vector2 DefaultOffset = new Vector2(0f, 4f);
    public const int DefaultSize = 16;

    public static void Attach(Control panel, Color color, int size = DefaultSize, Vector2? offset = null)
    {
        if (panel == null || panel.HasNode("MicaShadow")) return;

        int radius = 0;
        if (panel.GetThemeStylebox("panel") is StyleBoxFlat existing)
            radius = existing.CornerRadiusTopLeft;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            ShadowColor = color,
            ShadowSize = size,
            ShadowOffset = offset ?? DefaultOffset,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };

        var shadow = new Panel
        {
            Name = "MicaShadow",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ShowBehindParent = true,
        };
        shadow.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(shadow);
        shadow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public static void AttachToAll(Node root, ShaderMaterial material, Color color, int size = DefaultSize)
    {
        var found = new List<Control>();
        Collect(root, material, found);
        foreach (var c in found)
            Attach(c, color, size);
    }

    private static void Collect(Node node, ShaderMaterial material, List<Control> outList)
    {
        if (node is UiPanel)
            return;
        if (node is Control c && c.Material == material)
            outList.Add(c);
        foreach (Node child in node.GetChildren())
            Collect(child, material, outList);
    }
}
