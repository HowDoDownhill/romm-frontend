using Godot;
using System;
using System.Globalization;

public static class InterfaceScale
{
    public const string Automatic = "Auto";

    private const float HandheldScale = 1.3f;
    private const int HandheldMaximumShortSidePixels = 900;
    private const float HandheldMaximumShortSideInches = 6.0f;
    private const int MinimumTrustworthyDpi = 150;

    public static float Resolve(string setting)
    {
        if (!string.IsNullOrEmpty(setting) && setting != Automatic
            && float.TryParse(setting.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent) && percent > 0)
        {
            return percent / 100.0f;
        }

        return ResolveAutomatic();
    }

    private static float ResolveAutomatic()
    {
        int screen = DisplayServer.WindowGetCurrentScreen();
        Vector2I screenSize = DisplayServer.ScreenGetSize(screen);
        int shortSidePixels = Math.Min(screenSize.X, screenSize.Y);
        int dpi = DisplayServer.ScreenGetDpi(screen);

        if (shortSidePixels > 0 && shortSidePixels <= HandheldMaximumShortSidePixels)
        {
            return HandheldScale;
        }

        if (dpi >= MinimumTrustworthyDpi && (float)shortSidePixels / dpi < HandheldMaximumShortSideInches)
        {
            return HandheldScale;
        }

        return 1.0f;
    }

    public static void Apply(Window window, string setting)
    {
        if (window == null)
        {
            return;
        }

        float factor = Resolve(setting);
        window.ContentScaleFactor = factor;

        int screen = DisplayServer.WindowGetCurrentScreen();
        GD.Print($"[UI] interface size {setting} = {factor:0.##}x (screen {DisplayServer.ScreenGetSize(screen)}, {DisplayServer.ScreenGetDpi(screen)} dpi)");
    }
}
