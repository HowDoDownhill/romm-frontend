shader_type canvas_item;

// Blobs: flat circles at distinct elevations, each casting a shadow on whatever
// sits below it. Later blobs draw on top and are tinted slightly brighter, which
// is what gives the stack a readable front-to-back order.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const int BLOB_COUNT = 8;
const float EDGE = 0.0035;
const vec2 SHADOW_OFFSET = vec2(0.012, 0.020);

// Anchors sit on a jittered 4x2 grid rather than at raw hash positions: pure
// hashing clumped every blob into one corner and left the rest of the frame empty.
vec2 blob_center(int i, float time, float aspect) {
    float fi = float(i);
    float col = mod(fi, 4.0);
    float row = floor(fi / 4.0);

    vec2 anchor = vec2((col + 0.5) * 0.25 * aspect, (row + 0.5) * 0.5);
    anchor += vec2(fract(sin(fi * 12.9898) * 43758.5453) - 0.5,
                   fract(sin(fi * 78.2330) * 43758.5453) - 0.5) * vec2(0.22 * aspect, 0.22);

    return anchor + 0.065 * vec2(sin(time * (0.55 + 0.11 * fi) + fi * 2.1),
                                 cos(time * (0.47 + 0.15 * fi) + fi * 1.3));
}

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect) * scale;
    float time = TIME * speed;
    float field_aspect = aspect * scale;

    vec3 col = bg_color.rgb;

    for (int i = 0; i < BLOB_COUNT; i++) {
        float fi = float(i);
        vec2 center = blob_center(i, time, field_aspect);
        float radius = 0.11 + 0.10 * fract(sin(fi * 34.191) * 21374.531);

        // Alternate the two accents, then lift each successive blob toward the
        // light so overlapping shapes stay distinguishable.
        vec3 accent = mix(primary_color.rgb, secondary_color.rgb, mod(fi, 2.0));
        vec3 fill = mix(accent, accent + 0.16, fi / float(BLOB_COUNT));

        col = elevate(col,
            distance(p, center) - radius,
            distance(p, center - SHADOW_OFFSET) - radius,
            fill, EDGE);
    }

    COLOR = vec4(col, 1.0);
}
