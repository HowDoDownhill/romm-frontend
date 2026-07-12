shader_type canvas_item;

uniform vec4 bg_color : source_color = vec4(0.02, 0.02, 0.03, 1.0);
uniform vec4 primary_color : source_color = vec4(0.08, 0.04, 0.15, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.04, 0.08, 0.15, 1.0);
uniform float speed : hint_range(0.0, 5.0) = 0.2;
uniform float scale : hint_range(0.1, 10.0) = 1.0;

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise(vec2 x) {
    vec2 i = floor(x);
    vec2 f = fract(x);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i + vec2(0.0, 0.0)), hash(i + vec2(1.0, 0.0)), u.x),
               mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
    vec2 uv = UV * scale;
    float time = TIME * speed;
    
    vec2 q = vec2(0.0);
    q.x = noise(uv + vec2(0.0, 0.0) + time * 0.3);
    q.y = noise(uv + vec2(5.2, 1.3) + time * 0.4);
    
    vec2 r = vec2(0.0);
    r.x = noise(uv + 4.0 * q + vec2(1.7, 9.2) + time * 0.2);
    r.y = noise(uv + 4.0 * q + vec2(8.3, 2.8) + time * 0.5);
    
    float f = noise(uv + 4.0 * r);
    
    vec4 final_color = mix(bg_color, primary_color, clamp(f * f * 4.0, 0.0, 1.0));
    final_color = mix(final_color, secondary_color, clamp(length(q), 0.0, 1.0));
    
    COLOR = final_color;
}
