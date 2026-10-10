extends SceneTree
## 使用真实渲染器验证无双全部帧的圆形裁剪、参数调节与图标中心颜色。
## 用法：Godot4CSharp_console.exe --path Godot/GodotProject --script res://EditorScripts/validate_musou_flash.gd
## 输出：res://.godot/validation/musou_flash.png，依次为原帧、正式材质、缩小、偏移、硬边、零半径。

const FrameSize := 89
const FrameCount := 10
const AlphaTolerance := 0.02


## 延迟创建视口，等待引擎主循环和渲染服务就绪。
func _initialize() -> void:
	_run.call_deferred()


## 从正式 HUD 读取材质，再用参数变体逐像素检查实际裁剪范围。
func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("MUSOU VISUAL FAIL: 需要真实渲染器，请省略 --headless。")
		quit(1)
		return
	var hud: Node = load("res://TheGame/UIs/BattleHud.tscn").instantiate()
	var source: AnimatedSprite2D = hud.get_node("MenuPanel/m_WsMax")
	var frames := source.sprite_frames
	var animation := source.animation
	var material := source.material as ShaderMaterial
	hud.free()
	if material == null:
		push_error("MUSOU VISUAL FAIL: 正式 HUD 没有绑定无双材质。")
		quit(1)
		return

	# 参数变体复用正式材质的 shader，覆盖缩小、偏心、无羽化与空范围。
	var materials: Array[ShaderMaterial] = [material]
	var narrow := material.duplicate() as ShaderMaterial
	narrow.set_shader_parameter("clip_radius", 24.0)
	materials.append(narrow)
	var shifted := material.duplicate() as ShaderMaterial
	shifted.set_shader_parameter("clip_center", Vector2(0.4, 0.56))
	materials.append(shifted)
	var hard := material.duplicate() as ShaderMaterial
	hard.set_shader_parameter("clip_feather", 0.0)
	materials.append(hard)
	var empty := material.duplicate() as ShaderMaterial
	empty.set_shader_parameter("clip_radius", 0.0)
	materials.append(empty)

	# 原图和材质结果在同一透明视口中渲染，位置对齐到完整像素。
	var viewport := SubViewport.new()
	viewport.size = Vector2i(FrameSize * FrameCount, FrameSize * (materials.size() + 1))
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	for row in range(materials.size() + 1):
		for index in range(FrameCount):
			var sprite := AnimatedSprite2D.new()
			sprite.sprite_frames = frames
			sprite.animation = animation
			sprite.frame = index
			sprite.position = Vector2((index + 0.5) * FrameSize, (row + 0.5) * FrameSize)
			if row > 0:
				sprite.material = materials[row - 1]
			viewport.add_child(sprite)
	for index in range(3):
		await process_frame
	RenderingServer.force_draw()
	var result := viewport.get_texture().get_image()
	var visible_counts: Array[int] = []
	for case_index in range(materials.size()):
		var center_uv: Vector2 = materials[case_index].get_shader_parameter("clip_center")
		var radius: float = materials[case_index].get_shader_parameter("clip_radius")
		var feather: float = minf(materials[case_index].get_shader_parameter("clip_feather"), radius)
		var row_y := FrameSize * (case_index + 1)
		var visible_pixels := 0
		for index in range(FrameCount):
			for y in range(FrameSize):
				for x in range(FrameSize):
					var mask_alpha := result.get_pixel(x, y).a
					var before := result.get_pixel(index * FrameSize + x, y)
					var after := result.get_pixel(index * FrameSize + x, y + row_y)
					var distance_px := (Vector2(x + 0.5, y + 0.5) - center_uv * FrameSize).length()
					var coverage := 0.0
					if distance_px < radius:
						coverage = 1.0 if feather == 0.0 else 1.0 - smoothstep(radius - feather, radius, distance_px)
					var expected_alpha := before.a * mask_alpha * coverage
					# 硬边恰落在像素中心时，CPU/GPU 浮点舍入可落在边界任一侧。
					var on_hard_edge := feather == 0.0 and absf(distance_px - radius) < 0.001
					if not on_hard_edge and absf(after.a - expected_alpha) > AlphaTolerance:
						push_error("MUSOU VISUAL FAIL: 参数组 %d 第 %d 帧 (%d,%d) 透明度 %f ≠ %f" % [case_index, index, x, y, after.a, expected_alpha])
						quit(1)
						return
					if after.a > AlphaTolerance:
						visible_pixels += 1
			# 非空范围下图标中心处在实心区，必须保留原帧的亮度变化。
			if radius > 0.0:
				var center := Vector2i(index * FrameSize + 44, 44)
				var before := result.get_pixelv(center)
				var after := result.get_pixelv(center + Vector2i(0, row_y))
				if after.a < 0.95 or not before.is_equal_approx(after):
					push_error("MUSOU VISUAL FAIL: 参数组 %d 第 %d 帧中心变化。" % [case_index, index])
					quit(1)
					return
		visible_counts.append(visible_pixels)

	# 快照保存在引擎缓存目录，不提交渲染产物。
	DirAccess.make_dir_recursive_absolute("res://.godot/validation")
	var saved := result.save_png("res://.godot/validation/musou_flash.png")
	if visible_counts[0] <= visible_counts[1] or visible_counts[-1] != 0 or saved != OK:
		push_error("MUSOU VISUAL FAIL: 半径调节未生效或保存失败，可见像素=%s，保存结果=%d" % [visible_counts, saved])
		quit(1)
		return
	print("MUSOU VISUAL PASS: 10 帧 × 5 组裁剪参数逐像素通过，中心颜色保持不变，可见像素=%s。" % [visible_counts])
	quit(0)
