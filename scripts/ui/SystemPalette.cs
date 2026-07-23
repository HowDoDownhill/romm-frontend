using Godot;
using System.Collections.Generic;

public static class SystemPalette
{
    private const float BgSaturation = 0.62f;
    private const float BgValue = 0.075f;
    private const float PrimarySaturation = 0.80f;
    private const float PrimaryValue = 0.32f;
    private const float SecondarySaturation = 0.82f;
    private const float SecondaryValue = 0.39f;
    private const float PanelAlpha = 0.549f;

    private const int HueBuckets = 24;
    private const int SampleSize = 48;
    private const float MinAlpha = 0.35f;
    private const float MinSaturation = 0.18f;
    private const float MinValue = 0.15f;
    private const int MinBucketSeparation = 3;

    private static readonly Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)> cache =
        new Dictionary<string, (Color, Color, Color, Color)>();

    public static void ClearCache()
    {
        cache.Clear();
    }

    public static (Color Bg, Color Primary, Color Secondary, Color Panel)? FromSystem(GameSystem system)
    {
        if (system == null) return null;

        string key = !string.IsNullOrEmpty(system.IgdbSlug) ? system.IgdbSlug : system.Slug;
        if (string.IsNullOrEmpty(key)) return null;

        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        Texture2D texture = FindIcon(key);
        if (texture == null) return null;

        var palette = Extract(texture);
        if (palette == null) return null;

        cache[key] = palette.Value;
        return palette;
    }

    private static Texture2D FindIcon(string stub)
    {
        foreach (string basePath in new[] { "res://assets/platforms/", "res://assets/platforms/titles/" })
        {
            foreach (string ext in new[] { ".png", ".svg" })
            {
                string path = $"{basePath}{stub}{ext}";
                if (ResourceLoader.Exists(path))
                {
                    var texture = ResourceLoader.Load(path) as Texture2D;
                    if (texture != null) return texture;
                }
            }
        }
        return null;
    }

    private static (Color Bg, Color Primary, Color Secondary, Color Panel)? Extract(Texture2D texture)
    {
        Image image = texture.GetImage();
        if (image == null) return null;

        if (image.IsCompressed() && image.Decompress() != Error.Ok)
        {
            return null;
        }

        if (image.GetWidth() > SampleSize || image.GetHeight() > SampleSize)
        {
            image.Resize(SampleSize, SampleSize, Image.Interpolation.Bilinear);
        }

        var weights = new float[HueBuckets];
        float total = 0.0f;

        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (pixel.A < MinAlpha) continue;

                float s = pixel.S;
                float v = pixel.V;
                if (s < MinSaturation || v < MinValue) continue;

                int bucket = Mathf.Clamp((int)(pixel.H * HueBuckets), 0, HueBuckets - 1);
                float weight = s * v;
                weights[bucket] += weight;
                total += weight;
            }
        }

        if (total <= 0.0f) return null;

        int primaryBucket = IndexOfMax(weights, -1);
        if (primaryBucket < 0) return null;

        int secondaryBucket = IndexOfMax(weights, primaryBucket);
        float primaryHue = (primaryBucket + 0.5f) / HueBuckets;
        float secondaryHue = secondaryBucket >= 0
            ? (secondaryBucket + 0.5f) / HueBuckets
            : Mathf.PosMod(primaryHue + 0.42f, 1.0f);

        var bg = Color.FromHsv(primaryHue, BgSaturation, BgValue);
        var primary = Color.FromHsv(primaryHue, PrimarySaturation, PrimaryValue);
        var secondary = Color.FromHsv(secondaryHue, SecondarySaturation, SecondaryValue);
        var panel = new Color(bg.R, bg.G, bg.B, PanelAlpha);

        return (bg, primary, secondary, panel);
    }

    private static int IndexOfMax(float[] weights, int excludeNear)
    {
        int best = -1;
        float bestWeight = 0.0f;

        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0.0f) continue;

            if (excludeNear >= 0)
            {
                int raw = Mathf.Abs(i - excludeNear);
                int distance = Mathf.Min(raw, weights.Length - raw);
                if (distance < MinBucketSeparation) continue;
            }

            if (weights[i] > bestWeight)
            {
                bestWeight = weights[i];
                best = i;
            }
        }

        return best;
    }
}
