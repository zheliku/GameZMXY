shader_type canvas_item;

// Keep the original composition and baked names; only change hover saturation.
uniform float grayscale : hint_range(0.0, 1.0) = 1.0;
varying vec4 vertex_tint;
void vertex() { vertex_tint = COLOR; }
void fragment() {
    vec4 color = texture(TEXTURE, UV);
    float gray = dot(color.rgb, vec3(0.3086, 0.6094, 0.082));
    color.rgb = mix(color.rgb, vec3(gray), grayscale);
    COLOR = vertex_tint * color;
}
