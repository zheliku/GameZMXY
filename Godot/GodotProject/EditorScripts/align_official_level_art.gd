@tool
extends RefCounted
## 对应官方完整矩阵摆放可见地形；坐标来自导出清单，不按图像可见范围猜测。

static func align(level: Node2D, data: Dictionary, number: int) -> void:
	level.get_node("World/Background/Sky/End").position = Vector2.ZERO
	level.get_node("World/Background/Front").position = Vector2(data.backgroundPosition[0], data.backgroundPosition[1])
	level.get_node("World/Background/Front").modulate = Color.WHITE
	for item in data.spawns:
		level.get_node("World/SpawnPoints/" + item.projectNode).position = Vector2(item.position[0], item.position[1])
	var geometry := level.get_node("World/Geometry")
	var platforms := level.get_node("World/Platforms")
	var water := geometry.get_node("Water")
	water.z_index = -16
	# 删除旧的显示节点，保留连续土坡、墙体和单向平台原生碰撞。
	for parent in [geometry, platforms]:
		for sprite in parent.find_children("*", "Sprite2D", true, false):
			if not water.is_ancestor_of(sprite):
				sprite.free()
	var area: String = ["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1]
	var depths: Array = data.artwork.map(func(item): return int(item.depth))
	depths.sort()
	for item in data.artwork:
		var sprite := Sprite2D.new()
		sprite.name = "Artwork%d" % int(item.depth)
		sprite.texture = load("res://TheGame/Sprites/Levels/%s/%s.png" % [area, item.asset])
		sprite.centered = false
		var origin := Vector2(item.bounds[0], item.bounds[1])
		var values: Array = item.matrix
		var transform := Transform2D(Vector2(values[0], values[1]), Vector2(values[2], values[3]), Vector2(values[4], values[5]))
		var position := transform * Vector2((item.bounds[0] + item.bounds[2]) * 0.5, item.bounds[1])
		var parent: Node2D = geometry.get_node("Ground")
		if item.asset.ends_with("platform"):
			parent = _nearest(platforms, position)
		elif item.asset.ends_with("rock"):
			parent = _nearest(geometry.get_node("Ground"), position)
		parent.add_child(sprite)
		sprite.owner = level
		# 把源裁图原点合入变换，使 Sprite 的局部位置在平台附近，不留下数千像素的 offset。
		sprite.transform = parent.global_transform.affine_inverse() * level.global_transform * transform * Transform2D(0, origin)
		# 每个图层保留原始绘制顺序；天空和前景底图仍在全部地形之后。
		sprite.z_index = -15 + depths.find(int(item.depth))
		if number == 1 and item.asset.ends_with("ground"):
			sprite.region_enabled = true
			sprite.region_rect = Rect2(0, 0, 4708.35, sprite.texture.get_height())
	for item in data.gates:
		var stage := int(item.properties.idx) + 1
		level.get_node("World/StageGates/Stage%dGate" % stage).position.x = float(item.battleBounds[1])
		if stage > 1:
			level.get_node("World/StageGates/Stage%dLeft" % stage).position.x = float(item.battleBounds[0])

## 图像与对应平台/石墙共同移动；使用世界表面中心匹配，避免选择深埋地面的碰撞底边。
static func _nearest(group: Node, point: Vector2) -> StaticBody2D:
	var result: StaticBody2D
	var distance := INF
	for child in group.get_children():
		if not child is StaticBody2D:
			continue
		var surface: CollisionShape2D = child.get_node("Surface")
		var bounds: Rect2 = surface.global_transform * surface.shape.get_rect()
		var center := Vector2(bounds.get_center().x, bounds.position.y)
		var current := center.distance_squared_to(point)
		if current < distance:
			distance = current
			result = child
	assert(result != null)
	return result
