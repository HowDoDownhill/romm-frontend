shader_type canvas_item;

// Horizon: light rising from below the frame through slow bands of fog. The glow is an
// ellipse centred under the bottom edge; the fog is three octaves of value noise stretched
// sideways and drifting at different rates, and it is lit by the glow behind it.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

float horizon_hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float horizon_noise(vec2 x) {
    vec2 i = floor(x);
    vec2 f = fract(x);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(horizon_hash(i), horizon_hash(i + vec2(1.0, 0.0)), u.x),
               mix(horizon_hash(i + vec2(0.0, 1.0)), horizon_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect);
    float time = TIME * speed;

    vec2 glow_centre = vec2(0.5 * aspect + 0.08 * aspect * sin(time * 0.12), 1.18);
    vec2 to_glow = (p - glow_centre) * vec2(0.55 / max(aspect, 1.0), 1.0);
    float glow = exp(-dot(to_glow, to_glow) * 2.6);
    float core = exp(-dot(to_glow, to_glow) * 9.0);

    const mat2 FOG_TILT = mat2(vec2(0.96, 0.28), vec2(-0.28, 0.96));
    vec2 fog_uv = FOG_TILT * vec2(p.x * 1.6, p.y * 5.0) * scale;
    float fog = horizon_noise(fog_uv + vec2(time * 0.10, 0.0)) * 0.55
        + horizon_noise(FOG_TILT * fog_uv * 2.1 + vec2(-time * 0.16, 3.7)) * 0.30
        + horizon_noise(FOG_TILT * FOG_TILT * fog_uv * 4.3 + vec2(time * 0.23, 7.1)) * 0.15;
    fog = smoothstep(0.25, 0.85, fog) * smoothstep(0.15, 0.85, UV.y);

    vec3 col = bg_color.rgb;
    col += primary_color.rgb * glow * 1.6;
    col += secondary_color.rgb * core * 1.1;
    col += mix(primary_color.rgb, secondary_color.rgb, 0.5) * fog * (0.12 + glow * 0.55);

    COLOR = vec4(finish(col, UV, FRAGCOORD.xy, TIME), 1.0);
}
