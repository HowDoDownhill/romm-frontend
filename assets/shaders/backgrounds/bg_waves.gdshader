shader_type canvas_item;

// Waves: the same elevation model as Blobs applied to horizontal sheets, which
// reads like layered cut paper. Sheets blend primary -> secondary top to bottom.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const int SHEET_COUNT = 4;
const float EDGE = 0.0025;
const float SHADOW_RISE = 0.014;

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect) * scale;
    float time = TIME * speed;
    float field_aspect = aspect * scale;

    vec3 col = bg_color.rgb;

    for (int i = 0; i < SHEET_COUNT; i++) {
        float fi = float(i);
        float crest = 0.30 + 0.17 * fi
            + 0.035 * sin(p.x * (3.1 + 0.7 * fi) / field_aspect + time * (0.9 + 0.25 * fi))
            + 0.020 * sin(p.x * (6.7 - 0.9 * fi) / field_aspect - time * (0.6 + 0.2 * fi));

        vec3 accent = mix(primary_color.rgb, secondary_color.rgb, fi / float(SHEET_COUNT - 1));
        vec3 fill = mix(accent, accent + 0.10, fi / float(SHEET_COUNT));

        // A sheet fills everything below its crest, so its drop shadow has to sit
        // above the crest line -- the only side not covered by its own body.
        col = elevate(col, crest - p.y, (crest - SHADOW_RISE) - p.y, fill, EDGE);
    }

    COLOR = vec4(col, 1.0);
}
