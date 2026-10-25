extends SceneTree
## Remove adjacent equal discrete values; preserve continuous interpolation and method events.

var m_Removed := 0
var m_Changed := 0

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var paths: Array[String] = []
	_collect("res://TheGame", paths)
	for path in paths:
		var file := FileAccess.open(path, FileAccess.READ)
		var header := file.get_buffer(mini(160, file.get_length())).get_string_from_utf8()
		file.close()
		if not header.contains('type="AnimationLibrary"'):
			continue
		var library: AnimationLibrary = load(path)
		var expression := RegEx.create_from_string('uid="(uid://[^"]+)"')
		var uid_match := expression.search(header)
		var original_uid := ResourceUID.text_to_id(uid_match.get_string(1)) if uid_match != null else -1
		var before := m_Removed
		for name in library.get_animation_list():
			_optimize(library.get_animation(name))
		if m_Removed != before:
			assert(ResourceSaver.save(library, path) == OK)
			if original_uid != -1:
				assert(ResourceSaver.set_uid(path, original_uid) == OK)
			m_Changed += 1
			print("ANIMATION COMPACT: ", path, " removed=", m_Removed - before)
	print("ANIMATION COMPACTION PASS: libraries=", m_Changed, " removed=", m_Removed)
	quit()

func _optimize(clip: Animation) -> void:
	for track in clip.get_track_count():
		if clip.track_get_type(track) != Animation.TYPE_VALUE or clip.value_track_get_update_mode(track) != Animation.UPDATE_DISCRETE:
			continue
		# Setter calls with gameplay side effects are outside this optimization.
		var property := String(clip.track_get_path(track)).get_slice(":", 1)
		if not property in ["animation", "frame", "offset", "position", "rotation", "scale", "modulate", "visible", "disabled", "shape"]:
			continue
		var original := clip.duplicate(true) as Animation
		var samples: Array[float] = [0.0, clip.length]
		for key in clip.track_get_key_count(track):
			var time := clip.track_get_key_time(track, key)
			samples.append(time)
			samples.append(maxf(0, time - 0.00001))
			samples.append(minf(clip.length, time + 0.00001))
		var key := 1
		while key < clip.track_get_key_count(track):
			if _same(clip.track_get_key_value(track, key - 1), clip.track_get_key_value(track, key)):
				clip.track_remove_key(track, key)
				m_Removed += 1
			else:
				key += 1
		for time in samples:
			assert(_same(original.value_track_interpolate(track, time), clip.value_track_interpolate(track, time)),
				"Discrete interpolation changed at %s, %s" % [clip.track_get_path(track), time])

func _same(left: Variant, right: Variant) -> bool:
	if left == right:
		return true
	if left is Shape2D and right is Shape2D and left.get_class() == right.get_class() and left.get_script() == null and right.get_script() == null:
		for property in left.get_property_list():
			if property.usage & PROPERTY_USAGE_STORAGE and not String(property.name).begins_with("resource_") and property.name != &"script":
				if left.get(property.name) != right.get(property.name):
					return false
		return true
	return false

func _collect(folder: String, paths: Array[String]) -> void:
	for entry in DirAccess.get_files_at(folder):
		if entry.ends_with(".tres"):
			paths.append(folder.path_join(entry))
	for entry in DirAccess.get_directories_at(folder):
		_collect(folder.path_join(entry), paths)
