using Godot;

public static class ControllerGlyph
{
    private const int GlyphSize = 48;
    private const int GlyphSpacing = 10;

    public static ControllerIconTexture For(string actionName)
    {
        var glyph = new ControllerIconTexture();
        glyph.path = actionName;
        glyph.show_mode = ControllerIcons.EShowMode.CONTROLLER;
        return glyph;
    }

    public static void Apply(Button button, string actionName)
    {
        button.Icon = For(actionName);
        button.ExpandIcon = false;
        button.IconAlignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("icon_max_width", GlyphSize);
        button.AddThemeConstantOverride("h_separation", GlyphSpacing);
    }
}
