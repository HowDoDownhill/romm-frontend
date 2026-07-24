using Godot;
using System.Collections.Generic;

public static class MicaShadow
{
    public static readonly Color DefaultColor = new Color(0f, 0f, 0f, 0.3f);
    public static readonly Vector2 DefaultOffset = new Vector2(0f, 4f);
    public const int DefaultSize = 16;

    public static void Attach(Control panel, Color color, int size = DefaultSize, Vector2? offset = null)
    {
        if (panel == null) return;

        var shadow = panel.GetNodeOrNull<Panel>("MicaShadow") ?? CreateShadowNode(panel);
        shadow.AddThemeStyleboxOverride("panel", BuildStyle(panel, color, size, offset ?? DefaultOffset));
    }

    private static Panel CreateShadowNode(Control panel)
    {
        var shadow = new Panel
        {
            Name = "MicaShadow",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ShowBehindParent = true,
        };
        panel.AddChild(shadow);
        shadow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return shadow;
    }

    private static StyleBoxFlat BuildStyle(Control panel, Color color, int size, Vector2 offset)
    {
        int radius = 0;
        if (panel.GetThemeStylebox("panel") is StyleBoxFlat existing)
            radius = existing.CornerRadiusTopLeft;

        return new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            ShadowColor = color,
            ShadowSize = size,
            ShadowOffset = offset,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };
    }

    public static void AttachToAll(Node root, ShaderMaterial material, Color color, int size = DefaultSize, Vector2? offset = null)
    {
        var found = new List<Control>();
        Collect(root, material, found);
        foreach (var c in found)
            Attach(c, color, size, offset);
    }

    private static void Collect(Node node, ShaderMaterial material, List<Control> outList)
    {
        if (node is Control c && c.Material == material)
            outList.Add(c);
        foreach (Node child in node.GetChildren())
            Collect(child, material, outList);
    }
}
