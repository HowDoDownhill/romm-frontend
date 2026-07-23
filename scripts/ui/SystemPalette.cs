using Godot;
using System.Collections.Generic;

// Derives a theme palette from a platform's logo, for the dynamic "System" app theme.
//
// Only the *hue* is taken from the artwork. Saturation and value are forced to the same targets the
// hand-tuned palettes in ConfigManager use, because logo colours are chosen to read on packaging at
// full brightness -- dropped in unaltered they blow out the background and drown the UI panels.
public static class SystemPalette
{
    // Matched to the built-in palettes: dark background, mid-dark accents that stay behind the UI.
    private const float BgSaturation = 0.62f;
    private const float BgValue = 0.075f;
    private const float PrimarySaturation = 0.80f;
    private const float PrimaryValue = 0.32f;
    private const float SecondarySaturation = 0.82f;
    private const float SecondaryValue = 0.39f;
    private const float PanelAlpha = 0.549f; // 0x8c, as in the built-in palettes

    private const int HueBuckets = 24;
    private const int SampleSize = 48;
    // Below these a pixel carries no usable hue: transparent, near-grey, or near-black/white.
    private const float MinAlpha = 0.35f;
    private const float MinSaturation = 0.18f;
    private const float MinValue = 0.15f;
    // Two accents drawn from nearly the same hue would be indistinguishable once normalised.
    private const int MinBucketSeparation = 3;

    private static readonly Dictionary<string, (Color Bg, Color Primary, Color Secondary, Color Panel)> cache =
        new Dictionary<string, (Color, Color, Color, Color)>();

    public static void ClearCache()
    {
        cache.Clear();
    }

    // Returns null when the logo has no usable colour -- a great many platform logos are pure white
    // or greyscale silhouettes -- so the caller can fall back to a static palette.
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

    // Prefers the plain platform mark over the "titles" wordmark: wordmarks are more often flat
    // white lettering, while the mark usually carries the platform's actual brand colour.
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

        // Downsample first: a logo can be 1024px square and only the hue distribution matters.
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

                // Weight by saturation and value so a small vivid mark outvotes a large washed one.
                int bucket = Mathf.Clamp((int)(pixel.H * HueBuckets), 0, HueBuckets - 1);
                float weight = s * v;
                weights[bucket] += weight;
                total += weight;
            }
        }

        if (total <= 0.0f) return null;

        int primaryBucket = IndexOfMax(weights, -1);
        if (primaryBucket < 0) return null;

        // A second hue far enough away to stay distinct; if the logo is essentially monochrome,
        // rotate off the primary instead so the two accents still differ.
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

    // Highest-weighted bucket, optionally excluding anything too close to an already-picked one.
    // Hue is circular, so distance wraps.
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
