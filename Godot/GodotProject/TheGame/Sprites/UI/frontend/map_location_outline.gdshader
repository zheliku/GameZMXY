shader_type canvas_item;

// Original map icons include a painted halo. Scale the hover bitmap inside its
// transparent bounds, and pulse an alpha-based yellow contour like LevelButton.
uniform float image_scale = 1.0;
uniform float line_thickness = 0.0;
varying vec4 vertex_tint;
void vertex() { vertex_tint = COLOR; }
const vec2 OFFSETS[8] = {vec2(-1,-1), vec2(-1,0), vec2(-1,1), vec2(0,-1),
    vec2(0,1), vec2(1,-1), vec2(1,0), vec2(1,1)};
void fragment() {
    vec2 uv = (UV - vec2(0.5)) / image_scale + vec2(0.5);
    vec4 color = texture(TEXTURE, uv);
    float outline = 0.0;
    for (int i = 0; i < 8; i++) {
        outline = max(outline, texture(TEXTURE, uv + TEXTURE_PIXEL_SIZE * line_thickness * OFFSETS[i]).a);
    }
    float ring = max(outline - color.a, 0.0);
    float alpha = color.a + ring;
    vec3 rgb = alpha > 0.0 ? (color.rgb * color.a + vec3(1.0, 0.88, 0.0) * ring) / alpha : vec3(0.0);
    COLOR = vertex_tint * vec4(rgb, alpha);
}
