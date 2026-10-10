shader_type canvas_item;

/** 裁剪圆心，相对单张图集帧的坐标；(0.5, 0.5) 为图标中心。 */
uniform vec2 clip_center = vec2(0.5, 0.5);
/** 裁剪外半径，单位为原图像素；当前 89×89 图标建议 30～33，越小越集中。 */
uniform float clip_radius : hint_range(0.0, 64.0, 0.5) = 32.0;
/** 从裁剪边界向内羽化的宽度，单位为原图像素；0 为硬边，不向圆外扩散。 */
uniform float clip_feather : hint_range(0.0, 8.0, 0.25) = 1.0;

// 图集横排等宽正方形帧；首帧有干净轮廓，其余帧自带方形黄色底色。
// 先按圆形范围裁剪，再应用首帧透明轮廓，保留当前帧的颜色和 CanvasItem 调制。
void fragment() {
	vec2 atlas_size = vec2(textureSize(TEXTURE, 0));
	float columns = atlas_size.x / atlas_size.y;
	vec2 frame_uv = vec2(fract(UV.x * columns), UV.y);
	float distance_px = length((frame_uv - clip_center) * atlas_size.y);
	float feather = min(clip_feather, clip_radius);
	float circle_alpha = feather > 0.0
		? 1.0 - smoothstep(clip_radius - feather, clip_radius, distance_px)
		: 1.0 - step(clip_radius, distance_px);
	vec2 mask_uv = vec2(frame_uv.x / columns, frame_uv.y);
	COLOR.a *= texture(TEXTURE, mask_uv).a * circle_alpha;
}
