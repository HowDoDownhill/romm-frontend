shader_type canvas_item;

// Solid: flat theme background, for OLED panels and minimum GPU cost. The unused
// accent and animation uniforms come from the shared include so that switching to
// and from this style preserves the parameters the other styles need.

#include "res://assets/shaders/backgrounds/bg_common.gdshaderinc"

void fragment() {
    COLOR = vec4(bg_color.rgb, 1.0);
}
