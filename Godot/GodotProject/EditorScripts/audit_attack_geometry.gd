extends SceneTree
## Print all animated attack primitives so source ranges and visible effects can be compared.

const LIBRARY_ROOT := "res://TheGame/Entitys/Animations"
var redundant := 0

func _initialize() -> void:
	_scan(LIBRARY_ROOT)
	print("ATTACK KEY AUDIT: redundant consecutive geometry keys=", redundant)
	quit(1 if redundant > 0 else 0)

func _scan(directory: String) -> void:
	for child in DirAccess.get_directories_at(directory):
		_scan(directory.path_join(child))
	for file in DirAccess.get_files_at(directory):
		if not file.ends_with(".tres"):
			continue
		var library := load(directory.path_join(file)) as AnimationLibrary
		if library == null:
			continue
		for name in library.get_animation_list():
			if not String(name).begins_with("attack") and name != &"play" and file != "wukong_skill_library.tres":
				continue
			var animation := library.get_animation(name)
			var output: Array[String] = []
			for track in animation.get_track_count():
				if animation.track_get_type(track) != Animation.TYPE_VALUE:
					continue
				var path := String(animation.track_get_path(track))
				if path.contains("HitBox") and path.get_slice(":", 1) in ["shape", "position", "rotation", "disabled"]:
					var previous := ""
					for key in animation.track_get_key_count(track):
						var value = animation.track_get_key_value(track, key)
						var signature: String = _shape_text(value) if value is Shape2D else str(value)
						if key > 0 and signature == previous:
							redundant += 1
							push_error("Redundant attack geometry: %s/%s %s at %.4fs" % [file, name, path, animation.track_get_key_time(track, key)])
						previous = signature
				if not path.ends_with(":shape") or not path.contains("HitBox"):
					continue
				var entries: Array[String] = []
				for key in animation.track_get_key_count(track):
					var shape: Shape2D = animation.track_get_key_value(track, key)
					entries.append("%.4f:%s" % [animation.track_get_key_time(track, key), _shape_text(shape)])
				output.append(path + "=[" + ",".join(entries) + "]")
			if not output.is_empty():
				print(file, " / ", name, " / ", " ; ".join(output))

func _shape_text(shape: Shape2D) -> String:
	if shape is RectangleShape2D:
		return "rect(%s)" % (shape as RectangleShape2D).size
	if shape is CapsuleShape2D:
		var capsule := shape as CapsuleShape2D
		return "capsule(r=%s,h=%s)" % [capsule.radius, capsule.height]
	if shape is CircleShape2D:
		return "circle(r=%s)" % (shape as CircleShape2D).radius
	return shape.get_class()
