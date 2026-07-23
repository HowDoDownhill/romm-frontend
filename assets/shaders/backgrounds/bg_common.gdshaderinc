// Shared surface for every background style. All styles declare the same uniform
// names so MainScene.ApplyTheme can swap the shader on the shared material and
// re-apply one set of parameters, regardless of which style is active.

uniform vec4 bg_color : source_color = vec4(0.02, 0.02, 0.03, 1.0);
uniform vec4 primary_color : source_color = vec4(0.08, 0.04, 0.15, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.04, 0.08, 0.15, 1.0);
uniform float speed : hint_range(0.0, 5.0) = 0.2;
uniform float scale : hint_range(0.1, 10.0) = 1.0;

// Lays a soft drop shadow down and then the flat fill on top, so overlapping
// shapes read as separate stacked sheets instead of merging into one mass.
// Both distances are signed and negative inside the shape. Because a shape's
// body covers most of its own shadow, only the offset fringe stays visible --
// which is what sells the elevation.
vec3 elevate(vec3 dst, float body_dist, float shadow_dist, vec3 fill, float edge) {
    float shadow = 1.0 - smoothstep(-0.01, 0.07, shadow_dist);
    dst = mix(dst, dst * 0.42, shadow * 0.8);
    float body = 1.0 - smoothstep(-edge, edge, body_dist);
    return mix(dst, fill, body);
}

// Screen-space point with the horizontal axis corrected for aspect ratio, so
// circles stay circular. x spans 0..aspect, y spans 0..1.
vec2 aspect_corrected_uv(vec2 uv, float aspect) {
    return vec2(uv.x * aspect, uv.y);
}
