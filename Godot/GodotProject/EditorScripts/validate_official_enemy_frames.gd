extends SceneTree
## Compare live enemy body/effect cells and registration with official source PNG timelines.

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	create_timer(60).timeout.connect(func(): quit(1))
	var actors: Array = []
	for path in OS.get_cmdline_user_args():
		var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
		actors.append_array(source.actors)
	var paths := {}
	_collect("res://TheGame/Entitys/Enemies", paths)
	var count := 0
	var atlases := {}
	for reference in actors:
		assert(paths.has(reference.name + "_entity"), "Missing enemy scene: " + reference.name)
		var actor: Node2D = load(paths[reference.name + "_entity"]).instantiate()
		actor.process_mode = Node.PROCESS_MODE_DISABLED
		root.add_child(actor)
		var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
		player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		for layer in reference.layers:
			var body: AnimatedSprite2D = actor.get_node("m_Body" if layer.name == "body" else "m_Body/Effect")
			for action in layer.actions:
				for frame in int(action.frames):
					player.play(action.name)
					player.seek((frame + 0.01) / 24.0, true, true)
					player.pause()
					var texture: AtlasTexture = body.sprite_frames.get_frame_texture(body.animation, body.frame)
					var actual := Image.create_empty(1024, 1024, false, Image.FORMAT_RGBA8)
					if texture != null:
						var atlas_path := ProjectSettings.globalize_path(texture.atlas.resource_path)
						if not atlases.has(atlas_path):
							atlases[atlas_path] = Image.load_from_file(atlas_path)
						var atlas: Image = atlases[atlas_path]
						var image := atlas.get_region(texture.region)
						var location := body.global_position + body.offset - texture.get_size() * 0.5 + Vector2(512, 512)
						assert(location.is_equal_approx(location.round()), "%s/%s/%s frame %d fractional atlas registration at %s, position %s, offset %s, size %s" % [reference.name, layer.name, action.name, frame + 1, location, body.global_position, body.offset, texture.get_size()])
						actual.blit_rect(image, Rect2i(Vector2i.ZERO, image.get_size()), Vector2i(location))
					var raw := Image.load_from_file(action.input.path_join("%d.png" % (frame + 1)))
					var expected := Image.create_empty(1024, 1024, false, Image.FORMAT_RGBA8)
					var point := Vector2i(floori(action.bounds[0] + action.matrix[4]), floori(action.bounds[1] + action.matrix[5])) + Vector2i(512, 512)
					expected.blit_rect(raw, Rect2i(Vector2i.ZERO, raw.get_size()), point)
					assert(actual.get_data() == expected.get_data(), "%s/%s/%s frame %d source pixels or origin mismatch" % [reference.name, layer.name, action.name, frame + 1])
					count += 1
		print("ENEMY SOURCE PIXELS: ", reference.name)
		actor.free()
	print("ENEMY SOURCE PIXEL PASS: ", count, " complete layer frames, ", actors.size(), " enemies, live AnimationPlayer cells and root registration")
	quit()

func _collect(folder: String, paths: Dictionary) -> void:
	for file in DirAccess.get_files_at(folder):
		if file.ends_with("_entity.tscn"):
			paths[file.get_basename()] = folder.path_join(file)
	for child in DirAccess.get_directories_at(folder):
		_collect(folder.path_join(child), paths)
