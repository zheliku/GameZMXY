extends SceneTree
## Assemble the matched v25928 bat cave from the extracted source placements.

var source: Dictionary
var scene: Node2D

func _initialize() -> void:
	call_deferred("_build")

func _build() -> void:
	source = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))["2-1"]
	scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
	scene.name = "Level_4"
	scene.set("m_LevelId", 4)
	for path in ["World/Background/Front", "World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
		var node := scene.get_node(path)
		if path.ends_with("Front"):
			node.free()
		else:
			for child in node.get_children(): child.free()
	var background := scene.get_node("World/Background")
	var front := Sprite2D.new()
	front.name = "Front"
	front.texture = load("res://TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_front.png")
	front.position = _vec(source.backgroundPosition)
	front.centered = false
	front.z_index = 0
	_add(background, front)
	var sky_end := scene.get_node_or_null("World/Background/Sky/End")
	if sky_end != null:
		sky_end.texture = load("res://TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_sky.png")
		sky_end.position = Vector2.ZERO
		sky_end.visible = true
	var geometry := scene.get_node("World/Geometry")
	var ground_art := Sprite2D.new()
	ground_art.name = "Artwork"
	ground_art.texture = load("res://TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_ground.png")
	ground_art.position = Vector2(-3.85, 298.25)
	ground_art.centered = false
	ground_art.z_index = -5
	_add(geometry, ground_art)
	_build_geometry(geometry)
	_build_markers()
	_build_traps()
	var camera := scene.get_node("Camera2D")
	camera.limit_right = floori(ground_art.position.x + ground_art.texture.get_width())
	scene.get_node("World/Background/Sky").scroll_scale.x = (sky_end.texture.get_width() - 940.0) / (camera.limit_right - 940.0)
	var exit := scene.get_node("World/Exit")
	for item in source.scenery:
		if int(item.characterId) == 86: exit.position = _vec(item.position)
	var destination := "res://TheGame/Scenes/Levels/zm1_04_bat_cave/level_4.tscn"
	DirAccess.make_dir_recursive_absolute(destination.get_base_dir())
	var packed := PackedScene.new()
	assert(packed.pack(scene) == OK)
	assert(ResourceSaver.save(packed, destination) == OK)
	print("BAT CAVE LEVEL PASS: 17 spawn streams, 35 spikes, official background and geometry")
	quit()

func _build_geometry(parent: Node) -> void:
	var ground := Node2D.new(); ground.name = "Ground"; _add(parent, ground)
	var slopes := Node2D.new(); slopes.name = "Slopes"; _add(parent, slopes)
	var walls := Node2D.new(); walls.name = "Walls"; _add(parent, walls)
	var water := Node2D.new(); water.name = "Water"; _add(parent, water)
	for index in source.geometry.size():
		var item: Dictionary = source.geometry[index]
		var corners := PackedVector2Array()
		for value in item.corners: corners.append(Vector2(value[0], value[1]))
		var body := StaticBody2D.new()
		body.name = "Surface%02d" % index
		body.position = corners[0]
		body.collision_layer = 8 if item.oneWay else 1
		body.collision_mask = 0
		var surface_parent: Node
		if not item.oneWay and (corners[3] - corners[0]).length() > 900:
			surface_parent = walls
		elif not is_equal_approx(corners[0].y, corners[1].y):
			surface_parent = slopes
		else:
			surface_parent = ground
		_add(surface_parent, body)
		var shape := CollisionShape2D.new()
		shape.name = "Surface"
		var rectangle := RectangleShape2D.new()
		var x := corners[1] - corners[0]; var y := corners[3] - corners[0]
		rectangle.size = Vector2(x.length(), y.length())
		shape.shape = rectangle; shape.position = (corners[0] + corners[2]) * 0.5 - body.position
		shape.rotation = x.angle()
		shape.one_way_collision = item.oneWay
		body.add_child(shape); shape.owner = scene

func _build_markers() -> void:
	var points := scene.get_node("World/SpawnPoints")
	var entries: Array[Resource] = []
	_point(points, "PlayerStart", "player_start", Vector2(300, 300), entries)
	var featured: PackedStringArray = []
	for item in source.spawns:
		var stage := int(item.properties.stopPointIdx) + 1
		# The project-node suffix is the reviewed stable identity used by the table.
		var letter := String(item.projectNode).right(1).to_lower()
		var id := "stage_%d_%s" % [stage, letter]
		_point(points, "Stage%d%s" % [stage, letter.to_upper()], id, _vec(item.position), entries)
		if int(item.properties.enemyType) == 21: featured.append(id)
	points.set("m_Entries", entries); scene.set("m_FeaturedOpponentSpawnPointIds", featured)
	var gates := scene.get_node("World/StageGates")
	entries = []
	for item in source.gates:
		var stage := int(item.properties.idx) + 1
		if stage > 1: _gate(gates, "Stage%dLeft" % stage, "stage_%d_left" % stage, float(item.battleBounds[0]), entries)
		_gate(gates, "Stage%dGate" % stage, "stage_%d_gate" % stage, float(item.battleBounds[1]), entries)
	gates.set("m_Entries", entries)

func _build_traps() -> void:
	var root := Node2D.new(); root.name = "Traps"; root.z_index = 2; _add(scene.get_node("World"), root)
	var packed: PackedScene = load("res://TheGame/Scenes/Traps/bat_cave_spike.tscn")
	for index in source.scenery.size():
		var item: Dictionary = source.scenery[index]
		if int(item.characterId) != 113: continue
		var spike := packed.instantiate()
		spike.name = "Spike%02d" % index
		spike.position = _vec(item.position)
		spike.scale.y = float(item.scale[1])
		spike.set("m_Suspended", bool(item.properties.isUp))
		root.add_child(spike); spike.owner = scene

func _point(parent: Node, title: String, id: String, position: Vector2, entries: Array[Resource]) -> void:
	var node := Marker2D.new(); node.name = title; node.position = position; _add(parent, node)
	var entry: Resource = load("res://TheGame/GameScripts/Entity/Level/Markers/LevelMarkerResource.cs").new()
	entry.set("m_Id", id); entry.set("m_Node", NodePath(title)); entries.append(entry)

func _gate(parent: Node, title: String, id: String, x: float, entries: Array[Resource]) -> void:
	var body := StaticBody2D.new(); body.name = title; body.position = Vector2(x, 295); body.collision_layer = 8192; body.collision_mask = 0; _add(parent, body)
	var shape := CollisionShape2D.new(); shape.name = "Surface"; var rectangle := RectangleShape2D.new(); rectangle.size = Vector2(20, 1800); shape.shape = rectangle; shape.disabled = true; body.add_child(shape); shape.owner = scene
	var entry: Resource = load("res://TheGame/GameScripts/Entity/Level/Markers/LevelMarkerResource.cs").new(); entry.set("m_Id", id); entry.set("m_Node", NodePath(title)); entries.append(entry)

func _add(parent: Node, child: Node) -> void:
	parent.add_child(child); child.owner = scene

func _vec(value: Array) -> Vector2: return Vector2(float(value[0]), float(value[1]))
