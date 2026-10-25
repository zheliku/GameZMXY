extends SceneTree
## Validate rendered alignment and actual C# hover hit geometry for the short cave image.

const WORK := "res://.godot/validation/fire_demon_review"

func _initialize() -> void:
	create_timer(15).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _capture(view: SubViewport, name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	assert(view.get_texture().get_image().save_png(WORK.path_join(name + ".png")) == OK)

func _run() -> void:
	var view := SubViewport.new()
	view.size = Vector2i(940, 590)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
	view.add_child(form)
	var button: TextureButton = form.get_node("m_Bianfuku")
	var glow: TextureRect = button.get_node("LocationGlow")
	var player: AnimationPlayer = form.get_node("m_LocationAnimation")
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	assert(glow.stretch_mode == TextureRect.STRETCH_KEEP_ASPECT_CENTERED)
	var source: Image = glow.texture.get_image()
	var fit := minf(glow.size.x / source.get_width(), glow.size.y / source.get_height())
	var draw_size := source.get_size() * fit
	var origin := (glow.size - draw_size) * .5
	assert((glow.get_transform() * (origin + draw_size * .5)).distance_to(button.size * .5) < .01)
	glow.hide()
	button.self_modulate = Color.WHITE
	await _capture(view, "bat_map_normal")
	for time in [0.0, 0.5]:
		player.stop()
		player.play("bianfuku/hover")
		player.seek(time, true, true)
		player.pause()
		var image_scale: float = glow.material.get_shader_parameter("image_scale")
		var checked := 0
		for y in range(3, source.get_height(), 7):
			for x in range(3, source.get_width(), 7):
				if source.get_pixel(x, y).a < .9: continue
				var uv := Vector2(x + .5, y + .5) / Vector2(source.get_size())
				var local := origin + ((uv - Vector2(.5, .5)) * image_scale + Vector2(.5, .5)) * draw_size
				assert(button.call("_has_point", glow.get_transform() * local), "Visible hover pixel rejected")
				checked += 1
		assert(checked > 30)
		assert(not button.call("_has_point", Vector2(80, -20)), "Empty space above image is interactive")
		await _capture(view, "bat_map_hover_" + str(time))
	print("BAT MAP HOVER PASS: normal/hover center preserved; current visible silhouette hit at min/max scale, empty upper margin excluded.")
	quit()
