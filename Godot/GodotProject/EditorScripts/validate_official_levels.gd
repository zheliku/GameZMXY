extends SceneTree
## Verify new scenes against native placement metadata and render actual Godot views.

var failures := 0

func _initialize() -> void:
	call_deferred("_validate")

## Inspect source geometry, authored slots, spawn streams and independent stage walls.
func _validate() -> void:
	var args := OS.get_cmdline_user_args()
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	var recipes: RefCounted = load("res://EditorScripts/ValidateOfficialLevelRecipes.cs").new()
	check(recipes.call("Verify", args[0]) == 47, "All native recipes and stable point identities")
	root.size = Vector2i(940, 590)
	for number in [1, 2, 3]:
		var data: Dictionary = source["1-%d" % number]
		var level: Node2D = load("res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1], number]).instantiate()
		root.add_child(level)
		check(level.get("m_LevelId") == number, "LevelId")
		check(level.get("m_FeaturedOpponentSpawnPointIds").size() == 1, "One boss health bar binding")
		var points := level.get_node("World/SpawnPoints")
		check(points.get_child_count() == data.spawns.size() + 1, "All source spawn streams")
		for item in data.spawns:
			var point := points.get_node(item.projectNode) as Marker2D
			check(point.position.is_equal_approx(_vec(item.position)), "Original spawn position " + item.projectNode)
		check(level.get_node("World/Background/Sky/End").position == Vector2.ZERO, "Original FloorBg origin")
		check(level.get_node("World/Background/Front").position.is_equal_approx(_vec(data.backgroundPosition)), "Original bgClips origin")
		check(level.get_node("World/Background/Front").modulate == Color.WHITE, "Original bgClips opacity")
		var artwork := level.get_node("World/Geometry").find_children("Artwork*", "Sprite2D", true, false)
		artwork.append_array(level.get_node("World/Platforms").find_children("Artwork*", "Sprite2D", true, false))
		check(artwork.size() == data.artwork.size(), "Complete visible artwork")
		for item in data.artwork:
			var sprite: Sprite2D = level.get_node("World").find_child("Artwork%d" % int(item.depth), true, false)
			check(sprite != null, "Original source depth")
			if sprite == null:
				continue
			var values: Array = item.matrix
			var expected := Transform2D(Vector2(values[0], values[1]), Vector2(values[2], values[3]), Vector2(values[4], values[5]))
			expected *= Transform2D(0, Vector2(item.bounds[0], item.bounds[1]))
			check(sprite.global_transform.is_equal_approx(expected), "Original terrain matrix " + item.asset)
			check(sprite.offset == Vector2.ZERO, "Local terrain registration " + item.asset)
		for item in data.gates:
			var stage := int(item.properties.idx) + 1
			var gate := level.get_node("World/StageGates/Stage%dGate" % stage) as StaticBody2D
			check(is_equal_approx(gate.position.x, float(item.battleBounds[1])), "Independent right boundary")
			if stage > 1:
				var left := level.get_node("World/StageGates/Stage%dLeft" % stage) as StaticBody2D
				check(is_equal_approx(left.position.x, float(item.battleBounds[0])), "Overlapping battle left boundary")
		var hills: Array = preload("res://EditorScripts/normalize_level_geometry.gd").hill_geometry(data.geometry)
		var hill_parts: Array = []
		for hill in hills:
			hill_parts.append_array([hill.top, hill.left, hill.right])
			var polygon: CollisionPolygon2D = level.get_node("World/Geometry/Slopes/Hill%02d/Surface" % hill.top)
			check(polygon.polygon.size() == 6, "Continuous convex hill contour")
			for corner in 6:
				check(polygon.to_global(polygon.polygon[corner]).distance_to(hill.points[corner]) < 0.01, "Source hill top/foot registration")
		for index in data.geometry.size():
			if index in hill_parts:
				continue
			var item: Dictionary = data.geometry[index]
			var title := "Platform%02d" % index if item.oneWay else "Surface%02d" % index
			var body := level.get_node("World").find_child(title, true, false)
			check(body != null, "Grouped terrain " + title)
			var collision := body.get_node("Surface") as CollisionShape2D
			check(collision.one_way_collision == item.oneWay, "One-way collision")
			check(collision.shape is RectangleShape2D, "Plain terrain uses a native rectangle")
			var expected := PackedVector2Array(item.corners.map(_vec))
			if number == 1 and index == 5:
				var width := expected[1].x - expected[0].x
				for i in 4:
					expected[i].x = 4700 + (width if i in [1, 2] else 0)
			var half: Vector2 = collision.shape.size * 0.5
			var corners := PackedVector2Array([Vector2(-half.x,-half.y),Vector2(half.x,-half.y),half,Vector2(-half.x,half.y)])
			# 原版镜像的矩形可能反转角点顺序；实际四个世界坐标必须仍然完全对应。
			for point in expected:
				var matched := false
				for corner in corners:
					matched = matched or collision.to_global(corner).distance_to(point) < 0.01
				check(matched, "Source collision transform %d/%s" % [number,title])
		check(level.get_node("World/Geometry").get_child_count() == 4, "Ground/Slopes/Walls/Water groups")
		for field in ["m_SpawnPoints", "m_Gates", "m_Spawner", "m_Camera", "m_Exit", "m_FloatingText"]:
			check(level.get(field) != null, "Native export binding " + field)
		if args.has("--snapshots"):
			var exit := level.get_node("World/Exit")
			exit.visible = true
			exit.get_node("Marker").play("exit")
			var camera := level.get_node("Camera2D") as Camera2D
			for x in [470.0, 1554.15, 2350.0, 3305.2, 4190.0]:
				camera.position.x = x
				camera.reset_smoothing()
				camera.force_update_scroll()
				await process_frame
				await RenderingServer.frame_post_draw
				var output := "res://.godot/validation/stages_2_3/level_%d_%d.png" % [number, int(x)]
				DirAccess.make_dir_recursive_absolute(output.get_base_dir())
				check(root.get_texture().get_image().save_png(output) == OK, "Godot screenshot")
		var native_count := level.get_node("World/Geometry").find_children("*", "StaticBody2D", true, false).size() + level.get_node("World/Platforms").get_child_count()
		print("OFFICIAL LEVEL %d: %d spawn streams, %d source surfaces / %d native bodies checked" % [number, data.spawns.size(), data.geometry.size(), native_count])
		level.free()
	if failures == 0:
		print("OFFICIAL LEVEL SCENES PASS")
	quit(1 if failures else 0)

## Preserve the source coordinates exactly during validation.
func _vec(value: Array) -> Vector2:
	return Vector2(float(value[0]), float(value[1]))

## Report all independent assembly failures before exiting.
func check(condition: bool, label: String) -> void:
	if not condition:
		failures += 1
		push_error("OFFICIAL LEVEL CHECK: " + label)
