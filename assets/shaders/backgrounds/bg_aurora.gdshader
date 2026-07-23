shader_type canvas_item;

// Aurora: soft vertical curtains drifting sideways. Additive rather than mixed, so overlapping
// curtains brighten where they cross the way real ones do.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const int CURTAIN_COUNT = 5;

// Gaussian falloff -- gives a curtain with no hard edge at any width.
float curtain(float x, float centre, float width) {
    float d = (x - centre) / width;
    return exp(-d * d);
}

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect) * scale;
    float time = TIME * speed;

    vec3 col = bg_color.rgb;

    for (int i = 0; i < CURTAIN_COUNT; i++) {
        float fi = float(i);

        // Each curtain drifts at its own rate and wobbles vertically, so they never lock into a
        // repeating pattern the eye can latch onto.
        float centre = aspect * (0.15 + 0.18 * fi)
            + 0.10 * sin(time * (0.4 + 0.13 * fi) + fi * 2.3)
            + 0.04 * sin(p.y * 2.4 + time * (0.7 + 0.1 * fi));

        float width = 0.07 + 0.03 * fract(sin(fi * 45.233) * 8123.77);
        float intensity = curtain(p.x, centre, width);

        // Fade out toward the bottom so the curtains read as hanging rather than as bars.
        intensity *= smoothstep(1.05, 0.15, p.y);

        vec3 accent = mix(primary_color.rgb, secondary_color.rgb, fract(fi * 0.37));
        col += accent * intensity * 0.55;
    }

    COLOR = vec4(col, 1.0);
}
