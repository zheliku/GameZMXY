@tool
extends RefCounted
## 将原版世界坐标转换为可编辑的局部地形；不触及出生点、关门或流程绑定。

static func normalize(level: Node2D, data: Dictionary, number: int) -> void:
	var geometry := level.get_node("World/Geometry")
	var platforms := level.get_node("World/Platforms")
	var artwork: Array[Sprite2D] = []
	var water := geometry.get_node_or_null("Rivers")
	if water == null:
		water = geometry.get_node_or_null("Water")
	if water != null:
		_own(water, null)
		geometry.remove_child(water)
	if number != 1:
		for child in geometry.find_children("*", "Sprite2D", true, false):
			artwork.append(child)
			var position: Vector2 = child.global_position
			child.owner = null
			child.get_parent().remove_child(child)
			child.position = position
		for child in platforms.find_children("*", "Sprite2D", true, false):
			artwork.append(child)
			var position: Vector2 = child.global_position
			child.owner = null
			child.get_parent().remove_child(child)
			child.position = position
	for parent in [geometry, platforms]:
		for child in parent.get_children():
			child.free()
	var ground := _group(geometry, "Ground", level)
	var slopes := _group(geometry, "Slopes", level)
	var walls := _group(geometry, "Walls", level)
	if water != null:
		water.name = "Water"
		geometry.add_child(water)
		_own(water, level)
	else:
		_group(geometry, "Water", level)
	if number == 1:
		var floor_art := _sprite(ground, "Artwork", "huaguoshan_ground", Vector2(-8.35, 431.65), level)
		# 保留本项目 4700 的完整地形右界，裁去原版画布外的圆角收尾。
		floor_art.region_enabled = true
		floor_art.region_rect = Rect2(0, 0, 4708.35, floor_art.texture.get_height())
	var bodies: Array[StaticBody2D] = []
	var hill_parts := {}
	for hill in hill_geometry(data.geometry):
		for part in [hill.top, hill.left, hill.right]:
			hill_parts[part] = hill
	for index in data.geometry.size():
		var item: Dictionary = data.geometry[index]
		var points := PackedVector2Array()
		var is_hill := hill_parts.has(index)
		if is_hill:
			var hill: Dictionary = hill_parts[index]
			if index != hill.top:
				continue
			points = hill.points
		else:
			for value in item.corners:
				points.append(Vector2(value[0], value[1]))
		var is_wall: bool = not item.oneWay and (points[3] - points[0]).length() > 900
		var is_slope := not is_equal_approx(points[0].y, points[1].y)
		if number == 1 and index == 5:
			var width := points[1].x - points[0].x
			points[0].x = 4700
			points[3].x = 4700
			points[1].x = 4700 + width
			points[2].x = 4700 + width
		var parent: Node = platforms if item.oneWay else walls if is_wall else slopes if is_slope else ground
		var body := StaticBody2D.new()
		body.name = "Hill%02d" % index if is_hill else "Platform%02d" % index if item.oneWay else "Surface%02d" % index
		body.position = points[0]
		body.collision_layer = 8 if item.oneWay else 1
		body.collision_mask = 0
		parent.add_child(body)
		body.owner = level
		for i in points.size():
			points[i] -= body.position
		var surface: Node2D
		if is_hill:
			var hill_surface := CollisionPolygon2D.new()
			hill_surface.polygon = points
			surface = hill_surface
		else:
			var rectangle := RectangleShape2D.new()
			var x := points[1] - points[0]
			var y := points[3] - points[0]
			rectangle.size = Vector2(x.length(), y.length())
			var collision := CollisionShape2D.new()
			collision.shape = rectangle
			collision.position = (points[0] + points[2]) * 0.5
			collision.rotation = x.angle()
			surface = collision
		surface.name = "Surface"
		surface.one_way_collision = item.oneWay
		body.add_child(surface)
		surface.owner = level
		bodies.append(body)
	if number == 1:
		for item in data.scenery:
			if int(item.characterId) == 56:
				var position := Vector2(item.position[0], item.position[1]) + Vector2(2210.45, 192.5)
				var body := _nearest(bodies, position + Vector2(131.5, 10), true)
				_sprite(body, "Artwork", "huaguoshan_platform", position - body.position, level)
	else:
		for sprite in artwork:
			var position := sprite.position
			var parent: Node = ground
			if sprite.name.begins_with("PlatformArtwork") or sprite.name == "Artwork" and sprite.texture.resource_path.contains("platform"):
				parent = _nearest(bodies, position + Vector2(90, 5.5), true)
			elif sprite.texture.resource_path.contains("rock"):
				parent = _nearest(bodies, position, false)
			parent.add_child(sprite)
			sprite.position = position - (parent.position if parent is StaticBody2D else Vector2.ZERO)
			sprite.owner = level
			if sprite.name.begins_with("GroundArtwork") or sprite.name.begins_with("PlatformArtwork"):
				sprite.name = "Artwork"

## Flash 地面逐轴吸附允许微小交叠；Godot 实体侧边会卡住，土坡平台须与相邻坡顶共享顶点。
static func hill_geometry(geometry: Array) -> Array[Dictionary]:
	var hills: Array[Dictionary] = []
	for index in geometry.size():
		var item: Dictionary = geometry[index]
		var start := Vector2(item.corners[0][0], item.corners[0][1])
		var end := Vector2(item.corners[1][0], item.corners[1][1])
		if item.oneWay or not is_equal_approx(start.y, end.y) or end.x - start.x > 400:
			continue
		var left_index := -1
		var right_index := -1
		for other in geometry.size():
			var slope: Dictionary = geometry[other]
			var left := Vector2(slope.corners[0][0], slope.corners[0][1])
			var right := Vector2(slope.corners[1][0], slope.corners[1][1])
			if slope.oneWay:
				continue
			if right.y < left.y and start.distance_to(right) < 4:
				left_index = other
			elif right.y > left.y and end.distance_to(left) < 4:
				right_index = other
		if left_index < 0 or right_index < 0:
			continue
		var left_corners: Array = geometry[left_index].corners
		var right_corners: Array = geometry[right_index].corners
		var points := PackedVector2Array()
		# 一个凸轮廓消除三个独立碰撞体的内部侧面；顶边保留原来的左右坡面。
		for corner in [left_corners[0], left_corners[1], right_corners[0], right_corners[1], right_corners[2], left_corners[3]]:
			points.append(Vector2(corner[0], corner[1]))
		hills.append({"top": index, "left": left_index, "right": right_index, "points": points})
	return hills

static func _nearest(bodies: Array[StaticBody2D], point: Vector2, one_way: bool) -> StaticBody2D:
	var best: StaticBody2D
	var distance := INF
	for body in bodies:
		var surface: Node2D = body.get_node("Surface")
		if surface.one_way_collision != one_way:
			continue
		var bounds: Rect2
		if surface is CollisionShape2D:
			bounds = surface.transform * surface.shape.get_rect()
		else:
			bounds = Rect2(surface.polygon[0], Vector2.ZERO)
			for vertex in surface.polygon:
				bounds = bounds.expand(vertex)
		var center := body.position + bounds.get_center()
		var current := center.distance_squared_to(point)
		if current < distance:
			distance = current
			best = body
	return best

static func _group(parent: Node, title: String, level: Node) -> Node2D:
	var group := Node2D.new()
	group.name = title
	parent.add_child(group)
	group.owner = level
	return group

static func _sprite(parent: Node, title: String, asset: String, position: Vector2, level: Node) -> Sprite2D:
	var sprite := Sprite2D.new()
	sprite.name = title
	sprite.texture = load("res://TheGame/Sprites/Levels/zm1_01_huaguoshan/%s.png" % asset)
	sprite.centered = false
	sprite.position = position
	sprite.z_index = -5
	parent.add_child(sprite)
	sprite.owner = level
	return sprite

static func _own(node: Node, level: Node) -> void:
	node.owner = level
	for child in node.get_children():
		_own(child, level)
