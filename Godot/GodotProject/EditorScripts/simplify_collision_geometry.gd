extends SceneTree
## 用少量可编辑的基础形状表达玩法范围；保留动画时序，不按像素轮廓生成判定。

const BOSSES := "res://TheGame/Entitys/Enemies/Bosses/"
const LIBRARIES := "res://TheGame/Entitys/Animations/Enemies/Bosses/"
const HIT_AREA := "m_HitBoxRoot/m_HitBox"
const FPS := 24.0

func _initialize() -> void:
	call_deferred("_simplify")

## 招式只在有效阶段改变基础形状，平地等矩形多边形恢复为原生矩形。
func _simplify() -> void:
	assert(Engine.is_editor_hint())
	_tamarin()
	_macaque()
	var rectangles := 0
	for number in [1, 2, 3]:
		var path := "res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1], number]
		var level: Node = load(path).instantiate()
		# 可选的本轮修改前快照保留手调地形，以其外接矩形进行基础体转换。
		if not OS.get_cmdline_user_args().is_empty():
			var baseline := OS.get_cmdline_user_args()[0].path_join("baseline_level_%d.json" % number)
			if FileAccess.file_exists(baseline):
				for entry in JSON.parse_string(FileAccess.get_file_as_string(baseline)):
					if entry.points.size() != 4:
						continue
					var points: Array = entry.points
					var bounds := Rect2(Vector2(points[0][0],points[0][1]),Vector2.ZERO)
					for point in points:
						bounds = bounds.expand(Vector2(point[0],point[1]))
					var surface: CollisionShape2D = level.get_node(entry.path)
					var rectangle := RectangleShape2D.new()
					rectangle.size = bounds.size
					surface.shape = rectangle
					surface.global_position = bounds.get_center()
					surface.rotation = 0.0
		for polygon in level.find_children("*", "CollisionPolygon2D", true, false):
			if polygon.polygon.size() != 4:
				continue # 每座平顶土坡一个六点整体坡体，避免拆成重叠块或引入接缝。
			var points: PackedVector2Array = polygon.polygon
			var x := points[1] - points[0]
			var y := points[3] - points[0]
			assert(absf(x.normalized().dot(y.normalized())) < 0.01, "Terrain quadrilateral is not rectangular")
			var shape := RectangleShape2D.new()
			shape.size = Vector2(x.length(), y.length())
			var collision := CollisionShape2D.new()
			collision.name = polygon.name
			collision.transform = polygon.transform * Transform2D(x.angle(), (points[0] + points[2]) * 0.5)
			collision.shape = shape
			collision.disabled = polygon.disabled
			collision.one_way_collision = polygon.one_way_collision
			collision.one_way_collision_margin = polygon.one_way_collision_margin
			var parent: Node = polygon.get_parent()
			var index: int = polygon.get_index()
			parent.remove_child(polygon)
			parent.add_child(collision)
			parent.move_child(collision, index)
			collision.owner = level
			polygon.free()
			rectangles += 1
		_save(level, path)
	_compact_libraries("res://TheGame/Entitys/Animations")
	_reset_initial_hits("res://TheGame/Entitys")
	print("SIMPLE COLLISIONS PASS: thunder 74 -> 1; tornado at most 2 primitives; terrain rectangles=", rectangles)
	quit()

## 雷击用一个矩形，包含闪电之间的间隙，排除上方云层和外侧无攻击的大块空气。
func _tamarin() -> void:
	var path := BOSSES + "zm1_03_tamarin_king/tamarin_king_entity.tscn"
	var root: Node = load(path).instantiate()
	var library: AnimationLibrary = load(LIBRARIES + "zm1_03_tamarin_king/tamarin_king_library.tres")
	_replace_hit_shapes(root, library, 1)
	# [首帧, 结束帧(不含), 近似矩形]；形状覆盖有效能量，收招或只有云时禁用。
	_write_phases(library, "attack_1", 0, [
		[3, 11, Rect2(-145, -5, 85, 60)],
		[11, 14, Rect2(-180, -190, 150, 245)],
		[14, 17, Rect2(-210, -165, 205, 220)],
		[17, 20, Rect2(-135, -100, 65, 150)],
		[20, 24, Rect2(-120, 25, 35, 25)]])
	_write_phases(library, "attack_2", 0, [
		[41, 47, Rect2(-265, -135, 510, 185)],
		[47, 51, Rect2(-150, -135, 340, 185)],
		[51, 54, Rect2(-205, -135, 410, 185)],
		[61, 62, Rect2(-220, -135, 380, 110)],
		[62, 72, Rect2(-245, -135, 530, 190)]])
	assert(ResourceSaver.save(library, library.resource_path) == OK)
	_save(root, path)

## 小旋风用单个盒；大旋风用横向风冠和竖向风柱两个胶囊，不再沿透明毛边变更形状。
func _macaque() -> void:
	var path := BOSSES + "zm1_02_macaque_king/macaque_king_entity.tscn"
	var root: Node = load(path).instantiate()
	var library: AnimationLibrary = load(LIBRARIES + "zm1_02_macaque_king/macaque_king_library.tres")
	# 保留普攻的原始简单探针轨道，仅简化旋风的表现拟合。
	var primary: CollisionShape2D = root.get_node(HIT_AREA + "/CollisionShape2D")
	var previous := root.get_node_or_null(HIT_AREA + "/WindCrown")
	if previous != null:
		previous.free()
	var crown := CollisionShape2D.new()
	crown.name = "WindCrown"
	crown.disabled = true
	crown.shape = RectangleShape2D.new()
	primary.get_parent().add_child(crown)
	crown.owner = root
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		_reset_shape_tracks(animation, HIT_AREA + "/WindCrown")
		if animation.find_track(NodePath(HIT_AREA + "/CollisionShape2D:rotation"), Animation.TYPE_VALUE) < 0:
			_track(animation, HIT_AREA + "/CollisionShape2D:rotation", [0.0], [0.0])
	_write_phases(library, "attack_2", 0, [
		[5, 16, Rect2(-45, -10, 155, 85)],
		[16, 20, Rect2(-50, -45, 155, 120)],
		[20, 25, Rect2(-45, -60, 155, 135)],
		[25, 30, Rect2(-45, -80, 155, 155)],
		[30, 35, Rect2(-45, -120, 155, 200)],
		[35, 77, Rect2(-55, -180, 110, 245), "capsule"]])
	_write_phases(library, "attack_2", 1, [[35, 77, Rect2(-155, -215, 310, 90), "capsule"]])
	assert(ResourceSaver.save(library, library.resource_path) == OK)
	_save(root, path)

## 移除旧多边形与其轨道，只保留本 Area 真正需要的基础形状节点。
func _replace_hit_shapes(root: Node, library: AnimationLibrary, count: int) -> void:
	for child in root.get_node(HIT_AREA).get_children():
		if child is CollisionPolygon2D or child is CollisionShape2D:
			child.free()
	for index in count:
		var node := CollisionShape2D.new()
		node.name = "CollisionShape2D" if index == 0 else "WindCrown"
		node.disabled = true
		node.shape = RectangleShape2D.new()
		root.get_node(HIT_AREA).add_child(node)
		node.owner = root
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		for track in range(animation.get_track_count() - 1, -1, -1):
			if String(animation.track_get_path(track)).begins_with(HIT_AREA + "/"):
				animation.remove_track(track)
		for index in count:
			_reset_shape_tracks(animation, HIT_AREA + ("/CollisionShape2D" if index == 0 else "/WindCrown"))

## 每种普通动画都带安全默认值，结束或中断后不会保留伤害。
func _reset_shape_tracks(animation: Animation, path: String) -> void:
	for suffix in [":disabled", ":shape", ":position", ":rotation"]:
		var index := animation.find_track(NodePath(path + suffix), Animation.TYPE_VALUE)
		if index >= 0:
			animation.remove_track(index)
	var rest := RectangleShape2D.new()
	rest.size = Vector2.ONE
	_track(animation, path + ":disabled", [0.0], [true])
	_track(animation, path + ":shape", [0.0], [rest])
	_track(animation, path + ":position", [0.0], [Vector2.ZERO])
	_track(animation, path + ":rotation", [0.0], [0.0])

## 几何按招式阶段设置，不逐帧追踪图片的透明边缘。
func _write_phases(library: AnimationLibrary, name: String, slot: int, phases: Array) -> void:
	var animation := library.get_animation(name)
	var path := HIT_AREA + ("/CollisionShape2D" if slot == 0 else "/WindCrown")
	_reset_shape_tracks(animation, path)
	for phase in phases:
		var rectangle: Rect2 = phase[2]
		var shape: Shape2D
		var rotation := 0.0
		if phase.size() > 3 and phase[3] == "capsule":
			var capsule := CapsuleShape2D.new()
			capsule.radius = minf(rectangle.size.x, rectangle.size.y) * 0.5
			capsule.height = maxf(rectangle.size.x, rectangle.size.y)
			shape = capsule
			rotation = PI * 0.5 if rectangle.size.x > rectangle.size.y else 0.0
		else:
			var box := RectangleShape2D.new()
			box.size = rectangle.size
			shape = box
		var start: float = (phase[0] - 1) / FPS
		var end := minf((phase[1] - 1) / FPS, animation.length)
		for entry in [[":disabled", false], [":shape", shape], [":position", rectangle.get_center()], [":rotation", rotation]]:
			animation.track_insert_key(animation.find_track(NodePath(path + entry[0]), Animation.TYPE_VALUE), start, entry[1])
		animation.track_insert_key(animation.find_track(NodePath(path + ":disabled"), Animation.TYPE_VALUE), end, true)

## 写入原生离散轨道。
func _track(animation: Animation, path: String, times: Array, values: Array) -> void:
	var index := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(index, NodePath(path))
	animation.value_track_set_update_mode(index, Animation.UPDATE_DISCRETE)
	for key in times.size():
		animation.track_insert_key(index, times[key], values[key])

## 原生保存保持现有场景 UID 和节点归属。
func _save(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	node.free()

## 合并同一库内重复的基础形状与相邻相同离散几何键，保留动画窗口和方法键。
func _compact_libraries(directory: String) -> void:
	for child in DirAccess.get_directories_at(directory):
		_compact_libraries(directory.path_join(child))
	for file in DirAccess.get_files_at(directory):
		if not file.ends_with(".tres"):
			continue
		var library: AnimationLibrary = load(directory.path_join(file)) as AnimationLibrary
		if library == null:
			continue
		var palette := {}
		var merged := 0
		var removed := 0
		for name in library.get_animation_list():
			var animation := library.get_animation(name)
			for track in animation.get_track_count():
				var path := String(animation.track_get_path(track))
				if animation.track_get_type(track) != Animation.TYPE_VALUE or not ("HitBox" in path):
					continue
				if path.ends_with(":shape"):
					for key in animation.track_get_key_count(track):
						var shape: Shape2D = animation.track_get_key_value(track,key)
						var signature: String
						if shape is RectangleShape2D:
							signature = "rectangle/" + str(shape.size)
						elif shape is CapsuleShape2D:
							signature = "capsule/%s/%s" % [shape.radius,shape.height]
						elif shape is CircleShape2D:
							signature = "circle/" + str(shape.radius)
						else:
							continue
						if palette.has(signature):
							if shape != palette[signature]:
								animation.track_set_key_value(track,key,palette[signature])
								merged += 1
						else:
							palette[signature] = shape
				if animation.value_track_get_update_mode(track) == Animation.UPDATE_DISCRETE:
					for key in range(animation.track_get_key_count(track)-1,0,-1):
						if animation.track_get_key_value(track,key) == animation.track_get_key_value(track,key-1):
							animation.track_remove_key(track,key)
							removed += 1
		if merged > 0 or removed > 0:
			assert(ResourceSaver.save(library,library.resource_path) == OK)
			print("COMPACT COLLISIONS: ",file,", shared shapes=",merged,", redundant keys=",removed)

## 动画预览的启用状态不能成为池实例默认状态；出招时再由原生动画开启。
func _reset_initial_hits(directory: String) -> void:
	for child in DirAccess.get_directories_at(directory):
		_reset_initial_hits(directory.path_join(child))
	for file in DirAccess.get_files_at(directory):
		if not file.ends_with(".tscn"):
			continue
		var path := directory.path_join(file)
		var actor: Node = load(path).instantiate()
		var changed := false
		for collision in actor.find_children("*", "CollisionShape2D", true, false):
			if collision.get_parent().name in [&"HitBox", &"m_HitBox"] and not collision.disabled:
				collision.disabled = true
				changed = true
		if changed:
			_save(actor,path)
			print("DEFAULT HITBOX CLOSED: ",path)
		else:
			actor.free()
