extends SceneTree
## 从 GPU 像素核对两种填充与黑框；覆盖真实 BattleForm 装配和窗口缩放。

func _initialize() -> void:
	create_timer(30).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _strip(node: Node) -> void:
	for child in node.get_children():
		_strip(child)
	node.set_script(null)

func _draw(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func _run() -> void:
	assert(DisplayServer.get_name() != "headless")
	for scale in [1.0, 1.25, 2.0]:
		var viewport := SubViewport.new()
		viewport.size = Vector2i(Vector2(940, 590) * scale)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var hud: Control = load("res://TheGame/UIs/battle_form.tscn").instantiate()
		_strip(hud)
		hud.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
		hud.scale = Vector2.ONE * scale
		hud.size = Vector2(940, 590)
		viewport.add_child(hud)
		var ws: TextureProgressBar = hud.get_node("MenuPanel/WsFrame/m_WsBar")
		var boss: Control = hud.get_node("m_EncounterHealthBar")
		var health: TextureProgressBar = boss.get_node("Health")
		boss.show()
		boss.get_node("Name").text = "禺狨王"
		hud.get_node("m_MusouActivation").hide()
		hud.get_node("MenuPanel/m_WsMax").hide()
		ws.value = 0
		health.value = 0
		var under := ws.texture_under
		ws.texture_under = null
		var frame := await _draw(viewport)
		ws.texture_under = under
		var baseline := await _draw(viewport)
		for ratio in [0.1, 0.5, 1.0]:
			ws.value = ratio
			health.value = ratio
			var rendered := await _draw(viewport)
			var yellow_count := 0
			var red_count := 0
			var yellow_outside := 0
			var red_outside := 0
			for y in rendered.get_height():
				for x in rendered.get_width():
					var color := rendered.get_pixel(x, y)
					if y > 540 * scale and x > 70 * scale and x < 340 * scale and color.r > 0.7 and color.g > 0.3 and color.b < 0.2 and color.a > 0.5:
						yellow_count += 1
						if frame.get_pixel(x, y).a < 0.3:
							yellow_outside += 1
					elif y < 80 * scale and x > 330 * scale and color.r > 0.7 and color.g < 0.2 and color.b < 0.2 and color.a > 0.5:
						red_count += 1
						if baseline.get_pixel(x, y).a < 0.3:
							red_outside += 1
			assert(yellow_count > 0 and red_count > 0, "填充没有绘制：yellow=%d red=%d ratio=%s" % [yellow_count, red_count, ratio])
			assert(yellow_outside == 0, "无双黄条露出黑框：%d pixels, scale=%s, ratio=%s" % [yellow_outside, scale, ratio])
			assert(red_outside == 0, "BOSS 红条露出黑框：%d pixels, scale=%s, ratio=%s" % [red_outside, scale, ratio])
			print("HUD BAR PIXELS PASS: scale=%s ratio=%s yellow=%d red=%d outside=0" % [scale, ratio, yellow_count, red_count])
			if scale == 1 and ratio in [0.5, 1.0]:
				rendered.save_png("res://.godot/validation/hud_bar_alignment_%d.png" % int(ratio * 100))
		viewport.free()
	print("HUD BAR ALIGNMENT PASS: real HUD, three fill ratios, three viewport scales.")
	quit()
