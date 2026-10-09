shader_type canvas_item;

// Mesh: a few large colour lights drifting over the base, blended as a weighted average so the
// field reads as one continuous gradient with no visible shapes, the way mesh-gradient
// wallpapers do. Light colours are lifted from the theme accents so the field glows.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const int LIGHT_COUNT = 5;

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect);
    float time = TIME * speed;

    vec3 base = bg_color.rgb;
    vec3 sum = base * 0.55;
    float weight_sum = 0.55;

    for (int i = 0; i < LIGHT_COUNT; i++) {
        float fi = float(i);
        vec2 anchor = vec2((0.12 + 0.19 * fi) * aspect, 0.5 + 0.32 * sin(fi * 2.17));
        vec2 centre = anchor + vec2(0.22 * aspect * sin(time * (0.21 + 0.05 * fi) + fi * 1.9),
                                    0.20 * cos(time * (0.17 + 0.04 * fi) + fi * 2.7));

        float radius = (0.42 + 0.16 * fract(sin(fi * 34.19) * 213.7)) * scale;
        float d = distance(p, centre) / radius;
        float w = 1.0 / (1.0 + d * d * d * d);

        vec3 accent = mix(primary_color.rgb, secondary_color.rgb, fract(fi * 0.5));
        vec3 light = i == 2 ? mix(base, accent, 0.35) : accent * (1.35 + 0.25 * fract(fi * 0.37));

        sum += light * w;
        weight_sum += w;
    }

    COLOR = vec4(finish(sum / weight_sum, UV, FRAGCOORD.xy, TIME), 1.0);
}
