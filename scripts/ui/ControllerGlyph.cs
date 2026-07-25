using Godot;

public static class ControllerGlyph
{
    public static ControllerIconTexture For(string actionName)
    {
        var glyph = new ControllerIconTexture();
        glyph.path = actionName;
        glyph.show_mode = ControllerIcons.EShowMode.CONTROLLER;
        return glyph;
    }
}
