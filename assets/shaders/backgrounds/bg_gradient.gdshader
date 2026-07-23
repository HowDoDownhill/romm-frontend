shader_type canvas_item;

// Gradient: a slow diagonal wash from bg through primary into secondary. The calmest of the
// animated styles -- there is no shape to track, just a gentle drift in the ramp position.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

void fragment() {
    // Diagonal ramp, nudged over time so the image is never completely static.
    float t = (UV.x + UV.y) * 0.5;
    t += 0.06 * sin(TIME * speed * 0.8);
    t = clamp(t * scale, 0.0, 1.0);

    // Two overlapping ramps rather than one three-stop mix: the accents keep their own weight
    // instead of meeting in a muddy midpoint.
    vec3 col = mix(bg_color.rgb, primary_color.rgb, smoothstep(0.0, 0.65, t));
    col = mix(col, secondary_color.rgb, smoothstep(0.45, 1.0, t));

    COLOR = vec4(col, 1.0);
}
