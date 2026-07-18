shader_type canvas_item;

// NOTE: This project uses the gl_compatibility renderer, where hint_screen_texture gets no mipmaps
// (so textureLod blur is a no-op). Instead we blur by sampling a wide disk of points, rotated
// per-fragment so undersampling reads as fine frost grain rather than ghost copies.
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
// Blur radius in screen pixels. Larger = only broad color survives, no shape.
uniform float blur_radius : hint_range(0.0, 150.0) = 45.0;
uniform vec4 mix_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);
// NOTE: the panel outline is NOT drawn here. A shader-computed border reconstructed from UV
// derivatives ends up ~1px asymmetric (top/left vs bottom/right) because of sub-pixel rasterization.
// Instead each mica panel gets a native StyleBoxFlat border overlay (see MicaBorder.cs), which Godot
// renders pixel-perfectly and symmetrically and follows the panel's own rounded corners.

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
    // Derive texel size from the screen texture itself. SCREEN_PIXEL_SIZE is unreliable (often 0)
    // in canvas shaders under the gl_compatibility renderer, which collapses the blur to nothing.
    vec2 texel = 1.0 / vec2(textureSize(screen_texture, 0));
    vec2 px = texel * blur_radius;

    // Per-fragment rotation turns the sparse disk into noise (frost) instead of visible copies.
    float ang = hash(SCREEN_UV) * 6.2831853;
    float s = sin(ang);
    float c = cos(ang);
    mat2 rot = mat2(vec2(c, -s), vec2(s, c));

    vec4 acc = texture(screen_texture, SCREEN_UV);
    float total = 1.0;

    // 32-point golden-angle spiral, uniformly covering the disk.
    const int SAMPLES = 32;
    for (int i = 0; i < SAMPLES; i++) {
        float t = (float(i) + 0.5) / float(SAMPLES);
        float r = sqrt(t);                 // uniform disk distribution
        float a = float(i) * 2.399963;     // golden angle
        vec2 offset = rot * vec2(cos(a), sin(a)) * r;
        acc += texture(screen_texture, SCREEN_UV + offset * px);
        total += 1.0;
    }

    vec4 blurred = acc / total;

    // Tint the frosted result toward the theme color by its alpha, then force the panel fully
    // opaque. If we let the panel stay semi-transparent, the SHARP (unblurred) background bleeds
    // through via normal alpha blending and visually cancels the frost — the panel must be opaque
    // so the only thing seen is the blurred screen + tint.
    vec3 frosted = mix(blurred.rgb, mix_color.rgb, mix_color.a);
    COLOR = vec4(frosted, 1.0);
}
