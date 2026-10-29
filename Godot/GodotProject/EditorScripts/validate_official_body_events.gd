extends SceneTree
## Verify source terminal markers and invoke actual C# motion-stop methods through the native player.

func _initialize() -> void:
	create_timer(30).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _run() -> void:
	var args := OS.get_cmdline_user_args()
	assert(args.size() == 1)
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	var paths := {}
	_collect("res://TheGame/Entitys", paths)
	var markers := 0
	var stops := 0
	var loops := 0
	for entry in source.actions:
		var actor: CharacterBody2D = load(paths[entry.scene]).instantiate()
		actor.process_mode = Node.PROCESS_MODE_DISABLED
		root.add_child(actor)
		var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
		player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		player.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
		var animation := player.get_animation(entry.animation)
		assert(animation.has_marker(&"complete") == entry.has("complete"), "Terminal marker mismatch: " + entry.scene + "/" + entry.animation)
		if entry.has("complete"):
			assert(absf(animation.get_marker_time(&"complete") - float(entry.complete)) < .00001)
			markers += 1
		# This fixture validates motion only; gameplay/audio callbacks run through the GGF smoke flow.
		var library: AnimationLibrary = player.get_animation_library(&"").duplicate(true)
		for name in library.get_animation_list():
			var clip := library.get_animation(name)
			for track in clip.get_track_count():
				if clip.track_get_type(track) == Animation.TYPE_METHOD:
					var stop_track := false
					for key in clip.track_get_key_count(track):
						stop_track = stop_track or clip.method_track_get_name(track, key) == &"StopMotionAnimEvent"
					clip.track_set_enabled(track, stop_track)
		player.remove_animation_library(&"")
		player.add_animation_library(&"", library)
		for time in entry.get("motion_stops", []):
			player.stop()
			player.play(entry.animation)
			if float(time) > 0:
				player.advance(0)
				player.seek(float(time) - .001, true, true)
			actor.velocity = Vector2(150, -160)
			player.advance(.002 if float(time) > 0 else 0)
			assert(actor.velocity.is_zero_approx(), "Native stop did not execute: " + entry.scene + "/" + entry.animation)
			stops += 1
		if entry.has("air_loop"):
			assert(animation.has_marker(&"air_loop") and absf(animation.get_marker_time(&"air_loop") - float(entry.air_loop)) < .00001)
			actor.call("RestartAnim", entry.animation, "air_loop")
			var body: AnimatedSprite2D = actor.get_node("m_Body")
			assert(body.frame == 8 and absf(player.current_animation_position - float(entry.air_loop)) < .00001,
				"Corpse loop did not start at source frame 9: %s frame=%d animation=%s time=%.12f start=%.12f" % [entry.scene, body.frame, body.animation, player.current_animation_position, float(entry.air_loop)])
			actor.call("PlayAnim", "hurt")
			assert(body.frame == 0 and is_zero_approx(player.current_animation_position), "Section leaked into the next body action")
			loops += 1
		actor.free()
	print("BODY EVENT VALIDATION PASS: ", markers, " actual frame endpoints, ", stops, " native C# motion stops, ", loops, " frame-9 loops and next-action section resets")
	quit()

func _collect(folder: String, paths: Dictionary) -> void:
	for file in DirAccess.get_files_at(folder):
		if file.ends_with("_entity.tscn"):
			paths[file.get_basename()] = folder.path_join(file)
	for child in DirAccess.get_directories_at(folder):
		_collect(folder.path_join(child), paths)
