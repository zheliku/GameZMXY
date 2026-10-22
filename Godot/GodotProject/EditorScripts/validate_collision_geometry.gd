extends SceneTree
## 巡检全部业务场景与动画：少量基础形状、没有重复碰撞体，土坡只保留一个简单整体坡体。

var failures := 0
var scenes := 0
var primitives := 0
var hills := 0
var animated_shapes := 0
var terrain_surfaces := 0

func _initialize() -> void:
	_scan("res://TheGame")
	if failures == 0:
		print("COLLISION AUDIT PASS: scenes=", scenes, ", primitive nodes=", primitives, ", simple whole hills=", hills, ", animated shape keys=", animated_shapes, ", terrain baseline surfaces=", terrain_surfaces)
	quit(1 if failures else 0)

## 枚举业务场景，不检查框架或修改任何资源。
func _scan(directory: String) -> void:
	for child in DirAccess.get_directories_at(directory):
		_scan(directory.path_join(child))
	for file in DirAccess.get_files_at(directory):
		if not file.ends_with(".tscn"):
			continue
		var path := directory.path_join(file)
		var scene: Node = load(path).instantiate()
		scenes += 1
		_inspect(scene, scene, path)
		if path.contains("/Scenes/Levels/") and not OS.get_cmdline_user_args().is_empty():
			_terrain_baseline(scene, OS.get_cmdline_user_args()[0])
		scene.free()

## 同一物理对象的相同形状、位置和用途不应重复；身体、受击盒与感知区是不同职责。
func _inspect(node: Node, root: Node, path: String) -> void:
	if node is CollisionPolygon2D:
		var location := String(root.get_path_to(node))
		_check(path.contains("/Scenes/Levels/") and location.contains("/Slopes/Hill") and node.polygon.size() == 6,
			path + ": detailed/unsupported collision polygon " + location)
		hills += 1
	if node is CollisionShape2D:
		_check(_simple(node.shape), path + ": nonprimitive shape " + String(root.get_path_to(node)))
		if node.get_parent().name in [&"HitBox", &"m_HitBox"]:
			_check(node.disabled, path + ": attack collider is enabled before activation")
		primitives += 1
	if node is CollisionObject2D:
		var shapes := node.find_children("*", "CollisionShape2D", false, false)
		var maximum := 2 if node.name == &"PlayerScreenWalls" or node.name == &"m_HitBox" and path.contains("zm1_02_macaque_king") else 1
		_check(shapes.size() <= maximum, path + ": redundant shapes under " + String(root.get_path_to(node)))
		for a in shapes.size():
			for b in range(a + 1, shapes.size()):
				_check(not (shapes[a].transform == shapes[b].transform and shapes[a].shape == shapes[b].shape), path + ": duplicate collision nodes")
	if node is AnimationPlayer:
		for library_name in node.get_animation_library_list():
			var library: AnimationLibrary = node.get_animation_library(library_name)
			for name in library.get_animation_list():
				var animation: Animation = library.get_animation(name)
				for track in animation.get_track_count():
					var target := String(animation.track_get_path(track))
					_check(not target.ends_with(":polygon"), path + ": animated pixel contour " + target)
					if animation.track_get_type(track) != Animation.TYPE_VALUE or not target.ends_with(":shape"):
						continue
					for key in animation.track_get_key_count(track):
						_check(_simple(animation.track_get_key_value(track, key)), path + ": nonprimitive attack geometry")
						animated_shapes += 1
	for child in node.get_children():
		_inspect(child, root, path)

## 战斗和普通地形只使用基础体；土坡单独检查少量拐点，不接受精细轮廓。
func _simple(shape: Shape2D) -> bool:
	return shape is RectangleShape2D or shape is CapsuleShape2D or shape is CircleShape2D

## 与修改前的真实地形对照：普通地形保留外接矩形，五座整体土坡保留六个拐点。
func _terrain_baseline(level: Node2D, directory: String) -> void:
	var number: int = level.get("m_LevelId")
	var file := directory.path_join("baseline_level_%d.json" % number)
	_check(FileAccess.file_exists(file), "Missing pre-task terrain snapshot " + file)
	if not FileAccess.file_exists(file):
		return
	for entry in JSON.parse_string(FileAccess.get_file_as_string(file)):
		var surface: Node2D = level.get_node(entry.path)
		var points: Array = entry.points
		if points.size() == 6:
			_check(surface is CollisionPolygon2D, "Whole hill was split into parts")
			for index in points.size():
				_check(surface.to_global(surface.polygon[index]).distance_to(Vector2(points[index][0],points[index][1])) < 0.01, "Hill shape changed")
		else:
			_check(surface is CollisionShape2D and surface.shape is RectangleShape2D, "Plain terrain is not a rectangle")
			var expected := Rect2(Vector2(points[0][0],points[0][1]),Vector2.ZERO)
			for point in points:
				expected = expected.expand(Vector2(point[0],point[1]))
			var actual: Rect2 = surface.global_transform * surface.shape.get_rect()
			_check(actual.position.distance_to(expected.position) < 0.01 and actual.end.distance_to(expected.end) < 0.01,
				"Pre-task terrain coverage changed: %d/%s" % [number,entry.path])
		terrain_surfaces += 1

## 输出所有错误后用非零退出码报告失败。
func _check(condition: bool, message: String) -> void:
	if not condition:
		failures += 1
		push_error("COLLISION AUDIT: " + message)
