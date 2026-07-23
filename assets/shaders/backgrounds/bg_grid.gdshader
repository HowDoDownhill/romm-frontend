shader_type canvas_item;

// Grid: a slowly scrolling lattice lit by a drifting pool of accent colour. Flat and graphic
// rather than atmospheric, which suits a launcher; the lit pool stops it reading as wallpaper.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const float CELLS = 14.0;
const float LINE_WIDTH = 0.022;

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect) * scale;
    float time = TIME * speed;

    // Distance to the nearest cell edge on each axis, in cell-local units.
    vec2 cell = fract(p * CELLS + vec2(0.0, -time * 0.25));
    vec2 edge = min(cell, 1.0 - cell);
    float line = 1.0 - smoothstep(0.0, LINE_WIDTH, min(edge.x, edge.y));

    // A soft moving pool of light. Grid lines inside it pick up the secondary accent, which gives
    // the lattice somewhere to travel instead of scrolling uniformly.
    vec2 pool = vec2(aspect * (0.5 + 0.28 * sin(time * 0.6)),
                     0.5 + 0.22 * cos(time * 0.45));
    float glow = 1.0 - smoothstep(0.0, 0.55, distance(p, pool));

    vec3 lineColor = mix(primary_color.rgb, secondary_color.rgb, glow);

    vec3 col = mix(bg_color.rgb, lineColor, line * (0.35 + 0.65 * glow));
    // Lift the background very slightly inside the pool so the light reads as area, not just lines.
    col = mix(col, col + secondary_color.rgb * 0.10, glow);

    COLOR = vec4(col, 1.0);
}
