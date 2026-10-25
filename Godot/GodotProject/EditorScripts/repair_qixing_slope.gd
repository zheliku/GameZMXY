extends SceneTree
## Join the Qixing hill and keep cave viewports inside their complete ground artwork.

const LEVEL := "res://TheGame/Scenes/Levels/zm1_05_qixing_cave/level_5.tscn"

func _initialize() -> void:
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))["2-2"]
	var hills: Array = preload("res://EditorScripts/normalize_level_geometry.gd").hill_geometry(source.geometry)
	assert(hills.size() == 1)
	var level: Node2D = load(LEVEL).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var hill: Dictionary = hills[0]
	var slopes := level.get_node("World/Geometry/Slopes")
	var body := slopes.get_node_or_null("Hill%02d" % hill.top) as StaticBody2D
	if body == null:
		body = level.get_node("World/Geometry/Ground/Surface%02d" % hill.top)
		body.owner = null
		body.reparent(slopes)
		body.name = "Hill%02d" % hill.top
		for child in body.get_children():
			child.free()
		for index in [hill.left, hill.right]:
			level.get_node("World/Geometry/Ground/Surface%02d" % index).free()
		var surface := CollisionPolygon2D.new()
		surface.name = "Surface"
		body.add_child(surface)
		surface.owner = level
	var points: PackedVector2Array = hill.points
	body.position = points[0]
	for index in points.size():
		points[index] -= body.position
	body.get_node("Surface").polygon = points
	body.owner = level
	_save(level, LEVEL)
	for item in [
		["res://TheGame/Scenes/Levels/zm1_04_bat_cave/level_4.tscn", "World/Geometry/Artwork"],
		[LEVEL, "World/Geometry/Artwork/QixingGround"]]:
		level = ResourceLoader.load(item[0], "PackedScene", ResourceLoader.CACHE_MODE_IGNORE).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
		var ground: Sprite2D = level.get_node(item[1])
		var camera: Camera2D = level.get_node("Camera2D")
		camera.limit_right = floori(ground.position.x + ground.texture.get_width())
		var sky: Sprite2D = level.get_node("World/Background/Sky/End")
		level.get_node("World/Background/Sky").scroll_scale.x = (sky.texture.get_width() - 940.0) / (camera.limit_right - 940.0)
		_save(level, item[0])
	print("QIXING SLOPE REPAIR PASS: one continuous six-point hill; cave camera edges stay inside complete ground artwork")
	quit()

func _save(level: Node, path: String) -> void:
	var uid := ResourceLoader.get_resource_uid(path)
	var packed := PackedScene.new()
	assert(packed.pack(level) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	assert(ResourceSaver.set_uid(path, uid) == OK)
	level.free()
