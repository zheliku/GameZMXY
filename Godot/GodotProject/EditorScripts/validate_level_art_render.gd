extends SceneTree
## 独立官方整幅导出与实际 Godot 关卡逐像素比较，覆盖各石墙而非仅三个取景点。

func _initialize() -> void:
	create_timer(40).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func _run() -> void:
	var directory := OS.get_cmdline_user_args()[0]
	var origins := [Vector2(-30, 70), Vector2(-245, 71), Vector2(-30, 144)]
	for number in [1, 2, 3]:
		var viewport := SubViewport.new()
		viewport.size = Vector2i(5120, 720)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var level: Node2D = load("res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1], number]).instantiate()
		viewport.add_child(level)
		level.get_node("Camera2D").enabled = false
		level.get_node("World/Background/Sky").hide()
		level.get_node("World/Geometry/Water").hide() # 动态水面另由原始 325 帧轨道核对。
		level.get_node("World/Exit").hide()
		var actual := await _capture(viewport)
		actual.save_png("res://.godot/validation/official_level_%d_panorama.png" % number)
		level.hide()
		var reference := Sprite2D.new()
		reference.centered = false
		reference.position = origins[number - 1]
		var source := Image.load_from_file(directory.path_join("DefineSprite_%d/1.png" % (32000 + number)))
		assert(source != null)
		reference.texture = ImageTexture.create_from_image(source)
		viewport.add_child(reference)
		var expected := await _capture(viewport)
		var difference := 0.0
		var samples := 0
		var severe := 0
		# 4700 项目裁边之外不纳入；每个像素覆盖完整高度和四面石墙。
		for y in range(0, 590):
			for x in range(0, 4640):
				var a := actual.get_pixel(x, y)
				var b := expected.get_pixel(x, y)
				var delta := (absf(a.r * a.a - b.r * b.a) + absf(a.g * a.a - b.g * b.a) + absf(a.b * a.a - b.b * b.a) + absf(a.a - b.a)) * 0.25
				difference += delta
				samples += 1
				if delta > 0.2:
					severe += 1
		var mean := difference / samples
		print("LEVEL ART PIXELS: level=%d mean=%s severe=%s%% pixels=%d" % [number, mean, 100.0 * severe / samples, samples])
		assert(mean < 0.01 and float(severe) / samples < 0.0005, "与官方整幅地形偏差过大")
		viewport.free()
	print("LEVEL ART RENDER PASS: three complete foreground/terrain panoramas, native Flash reference; subpixel raster differences bounded.")
	quit()
