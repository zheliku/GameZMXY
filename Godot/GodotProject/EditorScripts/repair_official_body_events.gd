extends SceneTree
## Author actual terminal frame times and shared motion stops without rewriting artwork or hit geometry.

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	assert(args.size() == 1, "Pass the reviewed official body event manifest")
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	assert(source.fps == 24)
	var scenes := {}
	_collect("res://TheGame/Entitys", scenes)
	var libraries := {}
	var count := 0
	for entry in source.actions:
		assert(scenes.has(entry.scene), "Missing actor " + entry.scene)
		var actor: Node = load(scenes[entry.scene]).instantiate()
		var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
		assert(player.has_animation(entry.animation), "Missing action " + entry.scene + "/" + entry.animation)
		var library: AnimationLibrary = player.get_animation_library(&"")
		var animation := library.get_animation(entry.animation)
		if animation.has_marker(&"complete"):
			animation.remove_marker(&"complete")
		if entry.has("complete"):
			animation.add_marker(&"complete", float(entry.complete))
		if animation.has_marker(&"air_loop"):
			animation.remove_marker(&"air_loop")
		if entry.has("air_loop"):
			animation.add_marker(&"air_loop", float(entry.air_loop))
		_stop_track(animation, entry.get("motion_stops", []))
		libraries[library.resource_path] = library
		count += 1
		actor.free()
	for path in libraries:
		var uid := ResourceLoader.get_resource_uid(path)
		assert(ResourceSaver.save(libraries[path], path) == OK)
		if uid != ResourceUID.INVALID_ID:
			assert(ResourceSaver.set_uid(path, uid) == OK)
	print("BODY EVENT AUTHORING PASS: ", count, " actions; terminal markers and source motion stops; art/hit tracks retained")
	quit()

func _stop_track(animation: Animation, times: Array) -> void:
	# Re-running replaces only this operation; existing sound, attack and skill method keys stay intact.
	for track in range(animation.get_track_count() - 1, -1, -1):
		if animation.track_get_type(track) != Animation.TYPE_METHOD:
			continue
		for key in range(animation.track_get_key_count(track) - 1, -1, -1):
			if animation.method_track_get_name(track, key) == &"StopMotionAnimEvent":
				animation.track_remove_key(track, key)
		if animation.track_get_key_count(track) == 0:
			animation.remove_track(track)
	if times.is_empty():
		return
	var track := animation.add_track(Animation.TYPE_METHOD)
	animation.track_set_path(track, ^".")
	for time in times:
		animation.track_insert_key(track, float(time), {"method": &"StopMotionAnimEvent", "args": []})

func _collect(folder: String, scenes: Dictionary) -> void:
	for file in DirAccess.get_files_at(folder):
		if file.ends_with("_entity.tscn"):
			scenes[file.get_basename()] = folder.path_join(file)
	for child in DirAccess.get_directories_at(folder):
		_collect(folder.path_join(child), scenes)
