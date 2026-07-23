shader_type canvas_item;

// Bokeh: out-of-focus discs drifting upward. The soft-edged counterpart to Blobs -- same idea of
// discrete shapes, but they glow and overlap additively instead of stacking with hard shadows.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const int DISC_COUNT = 14;

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect) * scale;
    float time = TIME * speed;
    float field_aspect = aspect * scale;

    vec3 col = bg_color.rgb;

    for (int i = 0; i < DISC_COUNT; i++) {
        float fi = float(i);
        float hx = fract(sin(fi * 12.9898) * 43758.5453);
        float hy = fract(sin(fi * 78.2330) * 43758.5453);
        float hr = fract(sin(fi * 34.1910) * 21374.5310);

        // Rise continuously and wrap, so the field never empties out at the top.
        float y = fract(hy - time * (0.03 + 0.04 * hr));
        float x = hx * field_aspect + 0.05 * sin(time * (0.5 + hr) + fi);

        float radius = 0.04 + 0.10 * hr;
        float d = distance(p, vec2(x, y));

        // Bright core with a wide soft shoulder: the shoulder is what reads as defocus, and the
        // squared falloff keeps the centre from blowing out when discs overlap.
        float disc = 1.0 - smoothstep(radius * 0.25, radius, d);
        disc = disc * disc;

        vec3 accent = mix(primary_color.rgb, secondary_color.rgb, hr);
        col += accent * disc * 0.45;
    }

    COLOR = vec4(col, 1.0);
}
