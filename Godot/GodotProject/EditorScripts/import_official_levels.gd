extends SceneTree
## Build new stage scenes from decoded official placements; never replace authored scenes.

var source: Dictionary
var scene: Node2D

func _initialize() -> void:
	call_deferred("_build")

## Keep the existing LevelViewController slots and replace only the new scene's content.
func _build() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	source = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	for number in [2, 3]:
		var destination := "res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1], number]
		if FileAccess.file_exists(destination):
			push_error("Refusing to overwrite authored stage: " + destination)
			quit(1)
			return
		var data: Dictionary = source["1-%d" % number]
		var area := "shuiliandong" if number == 2 else "taohuayuan"
		var folder := "zm1_%02d_%s" % [number, area]
		scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
		scene.name = "Level_%d" % number
		scene.set("m_LevelId", number)
		# Scene references stay attached to the same slot owners, with fresh entries.
		for path in ["World/Background/Front", "World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
			var node := scene.get_node(path)
			if path.ends_with("Front"):
				node.free()
			else:
				for child in node.get_children():
					child.free()
		var background := scene.get_node("World/Background")
		var front := _sprite(background, "Front", "res://TheGame/Sprites/Levels/%s/%s_front.png" % [folder, area],
			_vec(data.backgroundPosition))
		front.z_index = 0
		var geometry := scene.get_node("World/Geometry")
		_sprite(geometry, "GroundArtwork", "res://TheGame/Sprites/Levels/%s/%s_ground.png" % [folder, area],
			Vector2(-18, 346.6) if number == 2 else Vector2(-16.9, 451.7))
		# 地形在 normalize 中一次生成原生矩形与整体坡体，不创建随后会被删除的临时多边形。
		for index in data.scenery.size():
			var item: Dictionary = data.scenery[index]
			if item.characterId == 88:
				_sprite(geometry, "PlatformArtwork%d" % index,
					"res://TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_platform.png",
					_vec(item.position) + Vector2(3169.35, 359.1))
			elif item.characterId == 103:
				var rock := _sprite(geometry, "Rock%d" % index,
					"res://TheGame/Sprites/Levels/zm1_03_taohuayuan/taohuayuan_rock.png", _vec(item.position))
				rock.centered = true
				rock.scale = _vec(item.scale)
		if number == 2:
			var rivers := Node2D.new()
			rivers.name = "Rivers"
			rivers.z_index = -6
			_add(geometry, rivers)
			var animation := Animation.new()
			animation.length = data.river.positions.size() / float(data.river.fps)
			animation.loop_mode = Animation.LOOP_LINEAR
			for item in data.scenery:
				if int(item.characterId) == 95:
					_river(rivers, data.river, _vec(item.position), animation)
			var library := AnimationLibrary.new()
			library.add_animation("flow", animation)
			var library_path := "res://TheGame/Entitys/Animations/shuiliandong_river_animation.tres"
			if ResourceSaver.save(library, library_path) != OK:
				quit(1)
				return
			var player := AnimationPlayer.new()
			player.name = "AnimationPlayer"
			player.add_animation_library("", library)
			player.autoplay = "flow"
			_add(rivers, player)
		var entries: Array[Resource] = []
		var points := scene.get_node("World/SpawnPoints")
		_point(points, "PlayerStart", "player_start", Vector2(300, 300), entries)
		var stages := {}
		var featured: PackedStringArray = []
		for item in data.spawns:
			var stage := int(item.properties.stopPointIdx) + 1
			var index := int(stages.get(stage, 0))
			stages[stage] = index + 1
			var id := "stage_%d_%s" % [stage, String.chr(97 + index)]
			_point(points, "Stage%d%s" % [stage, String.chr(65 + index)], id, _vec(item.position), entries)
			if int(item.properties.enemyType) in [4, 6]:
				featured.append(id)
		points.set("m_Entries", entries)
		scene.set("m_FeaturedOpponentSpawnPointIds", featured)
		entries = []
		var gates := scene.get_node("World/StageGates")
		for item in data.gates:
			var stage := int(item.properties.idx) + 1
			if stage > 1:
				_gate(gates, "Stage%dLeft" % stage, "stage_%d_left" % stage, float(item.battleBounds[0]), entries)
			_gate(gates, "Stage%dGate" % stage, "stage_%d_gate" % stage, float(item.battleBounds[1]), entries)
		gates.set("m_Entries", entries)
		for item in data.scenery:
			if item.characterId == 86:
				scene.get_node("World/Exit").position = _vec(item.position)
		var camera := scene.get_node("Camera2D")
		camera.limit_right = 4700 if number == 2 else 4660
		scene.get_node("World/Background/Sky").scroll_scale.x = 500.0 / (camera.limit_right - 940.0)
		preload("res://EditorScripts/normalize_level_geometry.gd").normalize(scene, data, number)
		preload("res://EditorScripts/align_official_level_art.gd").align(scene, data, number)
		var packed := PackedScene.new()
		if packed.pack(scene) != OK or ResourceSaver.save(packed, destination) != OK:
			quit(1)
			return
		print("LEVEL IMPORT PASS: %s; %d spawn streams; %d source collision surfaces" % [destination, data.spawns.size(), data.geometry.size()])
		scene.free()
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	quit()

## Assign ownership so all children remain editable in the saved scene.
func _add(parent: Node, child: Node) -> void:
	parent.add_child(child)
	child.owner = scene

## Keep texture registration in source world coordinates.
func _sprite(parent: Node, title: String, path: String, position: Vector2) -> Sprite2D:
	var node := Sprite2D.new()
	node.name = title
	node.texture = load(path)
	node.position = position
	node.centered = false
	node.z_index = -5
	_add(parent, node)
	return node

## Register a pure Marker2D under its existing collection.
func _point(parent: Node, title: String, id: String, position: Vector2, entries: Array[Resource]) -> void:
	var node := Marker2D.new()
	node.name = title
	node.position = position
	_add(parent, node)
	_entry(title, id, entries)

## Give each battle its independent native left/right wall.
func _gate(parent: Node, title: String, id: String, x: float, entries: Array[Resource]) -> void:
	var body := StaticBody2D.new()
	body.name = title
	body.position = Vector2(x, 295)
	body.collision_layer = 8192
	body.collision_mask = 0
	_add(parent, body)
	var collision := CollisionShape2D.new()
	collision.name = "Surface"
	var shape := RectangleShape2D.new()
	shape.size = Vector2(20, 1800)
	collision.shape = shape
	collision.disabled = true
	_add(body, collision)
	_entry(title, id, entries)

## Match the collection's typed resource list without per-marker scripts.
func _entry(title: String, id: String, entries: Array[Resource]) -> void:
	var entry: Resource = load("res://TheGame/GameScripts/Level/Markers/LevelMarkerResource.cs").new()
	entry.set("m_Id", id)
	entry.set("m_Node", NodePath(title))
	entries.append(entry)

## Decode an original position without applying any layout normalization.
func _vec(value: Array) -> Vector2:
	return Vector2(float(value[0]), float(value[1]))

## Recreate the original rectangular bitmap mask and all 325 wave positions.
func _river(parent: Node2D, data: Dictionary, position: Vector2, animation: Animation) -> void:
	var mask := Control.new()
	mask.name = "River%d" % parent.get_child_count()
	mask.position = position + Vector2(float(data.maskBounds[0]), float(data.maskBounds[1]))
	mask.size = Vector2(float(data.maskBounds[2]) - float(data.maskBounds[0]), float(data.maskBounds[3]) - float(data.maskBounds[1]))
	mask.clip_contents = true
	mask.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_add(parent, mask)
	_sprite(mask, "Water", "res://TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_water.png", Vector2.ZERO).z_index = 0
	var origin := Vector2(float(data.waveBounds[0]) - float(data.maskBounds[0]),
		float(data.waveBounds[1]) - float(data.maskBounds[1]) - 16.5)
	var wave := _sprite(mask, "Waves", "res://TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_waves.png", origin + _vec(data.positions[0]))
	wave.z_index = 0
	var track := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(track, NodePath(str(parent.get_path_to(wave)) + ":position"))
	animation.track_set_interpolation_type(track, Animation.INTERPOLATION_NEAREST)
	for frame in data.positions.size():
		animation.track_insert_key(track, frame / float(data.fps), origin + _vec(data.positions[frame]))
