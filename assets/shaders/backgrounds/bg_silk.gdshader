shader_type canvas_item;

// Silk: long satin folds lit from the upper left. One family of folds, warped sideways so they
// sway and bunch, is shaded from its surface normal (two extra height samples give the slope).
// Each fold gets a lit face, a shadowed face and a narrow sheen along its crest, the way fabric
// catches light.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

const vec3 KEY_LIGHT = vec3(-0.45, -0.65, 0.62);

float fold_height(vec2 p, float time) {
    float phase = p.y * 6.5
        + 1.9 * sin(p.x * 1.2 + time * 0.30)
        + 0.9 * sin(p.x * 2.5 - time * 0.21 + 1.3)
        + 0.6 * sin(p.y * 1.7 + p.x * 0.6 + time * 0.16);
    return sin(phase) + 0.3 * sin(phase * 2.0 + 0.8);
}

void fragment() {
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    vec2 p = aspect_corrected_uv(UV, aspect) * scale * 1.3;
    float time = TIME * speed;

    const float STEP = 0.004;
    float h = fold_height(p, time);
    float dx = (fold_height(p + vec2(STEP, 0.0), time) - h) / STEP;
    float dy = (fold_height(p + vec2(0.0, STEP), time) - h) / STEP;

    vec3 normal = normalize(vec3(-dx * 0.07, -dy * 0.07, 1.0));
    vec3 light = normalize(KEY_LIGHT);
    float diffuse = clamp(dot(normal, light), 0.0, 1.0);
    float specular = pow(clamp(dot(reflect(-light, normal), vec3(0.0, 0.0, 1.0)), 0.0, 1.0), 18.0);

    vec3 cloth = mix(primary_color.rgb, secondary_color.rgb, smoothstep(-0.8, 0.9, h + 0.4 * sin(p.x * 0.7 + time * 0.1)));
    vec3 shadow_side = mix(bg_color.rgb, cloth, 0.22);
    vec3 col = mix(shadow_side, cloth * 1.45, pow(diffuse, 1.8));
    col += mix(cloth, vec3(1.0), 0.35) * specular * 0.35;

    float fall_off = mix(0.7, 1.0, smoothstep(1.0, 0.1, UV.y));
    COLOR = vec4(finish(col * fall_off, UV, FRAGCOORD.xy, TIME), 1.0);
}
