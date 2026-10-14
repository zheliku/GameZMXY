shader_type canvas_item;

// Only equipped slots clear the caption baked into their background texture.
uniform bool occupied = false;
varying vec4 vertex_tint;

void vertex() {
    vertex_tint = COLOR;
}

void fragment() {
    vec4 background = texture(TEXTURE, UV);
    if (occupied && UV.x > 0.1 && UV.x < 0.9) {
        // The clear inner-left column preserves the original row color and borders.
        background = texture(TEXTURE, vec2(0.08, UV.y));
    }
    COLOR = background * vertex_tint;
}
