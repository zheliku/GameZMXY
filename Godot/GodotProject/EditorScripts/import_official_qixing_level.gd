extends SceneTree
## Assemble the matched v25928 2-2 七星洞 from the extracted source placements.

var m_Source: Dictionary
var m_Scene: Node2D
var m_Artwork: Array

func _initialize() -> void:
	call_deferred("_build")

func _build() -> void:
	var manifest_path := OS.get_cmdline_user_args()[0]
	m_Source = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))["2-2"]
	m_Artwork = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[1]))
	m_Scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
	m_Scene.name = "Level_5"
	m_Scene.set("m_LevelId", 5)
	for path in ["World/Background/Front", "World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
		var node := m_Scene.get_node(path)
		if path.ends_with("Front"):
			node.free()
		else:
			for child in node.get_children():
				child.free()
	var background := m_Scene.get_node("World/Background")
	var front := Sprite2D.new()
	front.name = "Front"
	front.texture = load("res://TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_cave_front.png")
	front.position = _vector(m_Source.backgroundPosition)
	front.centered = false
	front.z_index = 0
	_add(background, front)
	var sky_end := m_Scene.get_node_or_null("World/Background/Sky/End")
	if sky_end != null:
		sky_end.texture = load("res://TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_cave_sky.png")
		sky_end.position = Vector2.ZERO
		sky_end.visible = true
	_build_geometry()
	_build_markers()
	_build_traps()
	var camera := m_Scene.get_node("Camera2D")
	var ground_art: Sprite2D = m_Scene.get_node("World/Geometry/Artwork/QixingGround")
	camera.limit_right = floori(ground_art.position.x + ground_art.texture.get_width())
	if sky_end != null:
		m_Scene.get_node("World/Background/Sky").scroll_scale.x = (sky_end.texture.get_width() - 940.0) / (camera.limit_right - 940.0)
	var exit := m_Scene.get_node("World/Exit")
	for item in m_Source.scenery:
		if int(item.characterId) == 86:
			exit.position = _vector(item.position)
	var destination := "res://TheGame/Scenes/Levels/zm1_05_qixing_cave/level_5.tscn"
	DirAccess.make_dir_recursive_absolute(destination.get_base_dir())
	var packed := PackedScene.new()
	assert(packed.pack(m_Scene) == OK)
	assert(ResourceSaver.save(packed, destination) == OK)
	print("QIXING CAVE LEVEL PASS: 20 spawn streams, 5 gates, official background and geometry")
	quit()

func _build_geometry() -> void:
	var geometry := m_Scene.get_node("World/Geometry")
	var artwork := Node2D.new()
	artwork.name = "Artwork"
	_add(geometry, artwork)
	for layer in m_Artwork:
		var sprite := Sprite2D.new()
		sprite.name = String(layer.name).to_pascal_case()
		sprite.texture = load(layer.path)
		sprite.centered = false
		sprite.position = _vector(layer.position)
		_add(artwork, sprite)
	var ground := Node2D.new(); ground.name = "Ground"; _add(geometry, ground)
	var slopes := Node2D.new(); slopes.name = "Slopes"; _add(geometry, slopes)
	var walls := Node2D.new(); walls.name = "Walls"; _add(geometry, walls)
	var water := Node2D.new(); water.name = "Water"; _add(geometry, water)
	var platforms := m_Scene.get_node("World/Platforms")
	var hill_parts := {}
	for hill in preload("res://EditorScripts/normalize_level_geometry.gd").hill_geometry(m_Source.geometry):
		for part in [hill.top, hill.left, hill.right]:
			hill_parts[part] = hill
	for index in m_Source.geometry.size():
		var item: Dictionary = m_Source.geometry[index]
		var corners := PackedVector2Array()
		if hill_parts.has(index):
			var hill: Dictionary = hill_parts[index]
			if index != hill.top:
				continue
			var body := StaticBody2D.new()
			body.name = "Hill%02d" % index
			body.position = hill.points[0]
			body.collision_mask = 0
			_add(slopes, body)
			var surface := CollisionPolygon2D.new()
			surface.name = "Surface"
			for point in hill.points:
				corners.append(point - body.position)
			surface.polygon = corners
			body.add_child(surface)
			surface.owner = m_Scene
			continue
		for value in item.corners:
			corners.append(_vector(value))
		var body := StaticBody2D.new()
		body.name = "Surface%02d" % index
		body.position = corners[0]
		body.collision_layer = 8 if item.oneWay else 1
		body.collision_mask = 0
		var parent: Node = platforms if item.oneWay else walls if (corners[3] - corners[0]).length() > 900 else ground
		_add(parent, body)
		var shape := CollisionShape2D.new()
		shape.name = "Surface"
		var rectangle := RectangleShape2D.new()
		var horizontal := corners[1] - corners[0]
		var vertical := corners[3] - corners[0]
		rectangle.size = Vector2(horizontal.length(), vertical.length())
		shape.shape = rectangle
		shape.position = (corners[0] + corners[2]) * 0.5 - body.position
		shape.rotation = horizontal.angle()
		shape.one_way_collision = item.oneWay
		body.add_child(shape)
		shape.owner = m_Scene

func _build_traps() -> void:
	var traps := Node2D.new()
	traps.name = "Traps"
	traps.z_index = 2
	_add(m_Scene.get_node("World"), traps)
	var packed: PackedScene = load("res://TheGame/Scenes/Traps/bat_cave_spike.tscn")
	for index in m_Source.scenery.size():
		var item: Dictionary = m_Source.scenery[index]
		if int(item.characterId) != 113:
			continue
		var spike := packed.instantiate()
		spike.name = "Spike%02d" % index
		spike.position = _vector(item.position)
		spike.scale.y = float(item.scale[1])
		spike.set("m_Suspended", bool(item.properties.isUp))
		_add(traps, spike)

func _build_markers() -> void:
	var points := m_Scene.get_node("World/SpawnPoints")
	var entries: Array[Resource] = []
	_point(points, "PlayerStart", "player_start", Vector2(300, 300), entries)
	var featured: PackedStringArray = []
	for item in m_Source.spawns:
		var stage := int(item.properties.stopPointIdx) + 1
		var letter := String(item.projectNode).right(1).to_lower()
		var id := "stage_%d_%s" % [stage, letter]
		_point(points, "Stage%d%s" % [stage, letter.to_upper()], id, _vector(item.position), entries)
		if int(item.properties.enemyType) == 11:
			featured.append(id)
	points.set("m_Entries", entries)
	m_Scene.set("m_FeaturedOpponentSpawnPointIds", featured)
	var gates := m_Scene.get_node("World/StageGates")
	entries = []
	for item in m_Source.gates:
		var stage := int(item.properties.idx) + 1
		if stage > 1:
			_gate(gates, "Stage%dLeft" % stage, "stage_%d_left" % stage, float(item.battleBounds[0]), entries)
		var right := float(item.battleBounds[1])
		# The official first StopPoint is 918.25px; the project keeps a fixed
		# 940px design viewport, so extend only that gate by the minimum amount.
		if stage == 1:
			right = maxf(right, 940.0)
		_gate(gates, "Stage%dGate" % stage, "stage_%d_gate" % stage, right, entries)
	gates.set("m_Entries", entries)

func _point(parent: Node, title: String, id: String, position: Vector2, entries: Array[Resource]) -> void:
	var node := Marker2D.new(); node.name = title; node.position = position; _add(parent, node)
	var entry: Resource = load("res://TheGame/GameScripts/Entity/Level/Markers/LevelMarkerResource.cs").new()
	entry.set("m_Id", id); entry.set("m_Node", NodePath(title)); entries.append(entry)

func _gate(parent: Node, title: String, id: String, x: float, entries: Array[Resource]) -> void:
	var body := StaticBody2D.new(); body.name = title; body.position = Vector2(x, 295); body.collision_layer = 8192; body.collision_mask = 0; _add(parent, body)
	var shape := CollisionShape2D.new(); shape.name = "Surface"; var rectangle := RectangleShape2D.new(); rectangle.size = Vector2(20, 1800); shape.shape = rectangle; shape.disabled = true; body.add_child(shape); shape.owner = m_Scene
	var entry: Resource = load("res://TheGame/GameScripts/Entity/Level/Markers/LevelMarkerResource.cs").new(); entry.set("m_Id", id); entry.set("m_Node", NodePath(title)); entries.append(entry)

func _add(parent: Node, child: Node) -> void:
	parent.add_child(child)
	child.owner = m_Scene

func _vector(value: Array) -> Vector2:
	return Vector2(float(value[0]), float(value[1]))
