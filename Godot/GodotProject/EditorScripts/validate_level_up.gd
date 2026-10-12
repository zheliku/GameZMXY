extends SceneTree
## 用真实渲染器逐像素验证升级动画：只裁掉蓝色边缘，保持其余画面和注册尺寸。
## 上排为原始帧，下排为正式动画；每幅原画连续使用两帧，输出 14 组对照。


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("LEVEL UP VISUAL FAIL: 需要真实渲染器。")
		quit(1)
		return
	var frames: SpriteFrames = load("res://TheGame/Sprites/Effects/level_up/level_up_frames.tres")
	assert(frames.get_frame_count(&"level_up") == 28)
	assert(frames.get_animation_speed(&"level_up") == 24.0)
	assert(not frames.get_animation_loop(&"level_up"))
	var viewport := SubViewport.new()
	viewport.size = Vector2i(122 * 14, 136 * 2)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	for index in range(14):
		var texture := frames.get_frame_texture(&"level_up", index * 2)
		assert(texture.get_size() == Vector2(122, 136))
		assert(frames.get_frame_texture(&"level_up", index * 2 + 1).get_size() == texture.get_size())
		for row in range(2):
			var sprite := Sprite2D.new()
			sprite.centered = false
			sprite.texture = texture.atlas if row == 0 and texture is AtlasTexture else texture
			sprite.position = Vector2(index * 122, row * 136)
			viewport.add_child(sprite)
	for index in range(3):
		await process_frame
	RenderingServer.force_draw()
	var result := viewport.get_texture().get_image()
	var removed := 0
	for index in range(14):
		var texture := frames.get_frame_texture(&"level_up", index * 2)
		var top := int(texture.margin.position.y) if texture is AtlasTexture else 0
		for y in range(136):
			for x in range(122):
				var before := result.get_pixel(index * 122 + x, y)
				var after := result.get_pixel(index * 122 + x, y + 136)
				var expected := Color.TRANSPARENT if y < top else before
				if absf(after.a - expected.a) > 0.01 or (after.a > 0.01 and not after.is_equal_approx(expected)):
					push_error("LEVEL UP VISUAL FAIL: 第 %d 帧 (%d,%d) 裁剪或注册位置不符。" % [index * 2 + 1, x, y])
					quit(1)
					return
				if before.a > 0.01 and after.a < 0.01:
					removed += 1
	assert(removed > 250, "必须实际移除后半段蓝线。")
	DirAccess.make_dir_recursive_absolute("res://.godot/validation")
	assert(result.save_png("res://.godot/validation/level_up_frames.png") == OK)
	print("LEVEL UP VISUAL PASS: 28 帧尺寸/时长、14 组原画逐像素通过；仅移除 %d 个边缘像素。" % removed)
	quit(0)
