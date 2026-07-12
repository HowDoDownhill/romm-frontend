shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_amount : hint_range(0.0, 5.0) = 2.0;
uniform vec4 mix_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);

void fragment() {
    // Sample the screen texture with blur
    vec4 blurred = textureLod(screen_texture, SCREEN_UV, blur_amount);
    
    // Mix the blurred background with the tint color
    COLOR = mix(blurred, mix_color, mix_color.a);
}
