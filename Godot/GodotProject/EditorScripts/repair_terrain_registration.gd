extends SceneTree
## 原生保存三关局部地形、出口和原版身体盒。只在编辑器隔离副本执行。

func _initialize() -> void:
	create_timer(60).timeout.connect(func(): quit(1))
	_build.call_deferred()

func _build() -> void:
	assert(Engine.is_editor_hint())
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for i in 10:
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	for number in [1, 2, 3]:
		var path := "res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1], number]
		var level: Node2D = load(path).instantiate()
		var data: Dictionary = source["1-%d" % number]
		preload("res://EditorScripts/normalize_level_geometry.gd").normalize(level, data, number)
		for item in data.scenery:
			if int(item.characterId) == 86:
				level.get_node("World/Exit").position = Vector2(item.position[0], item.position[1])
		_save(level, path)
	var frame_path := "res://TheGame/Sprites/Effects/level_exit/level_exit_frames.tres"
	var frames: SpriteFrames = load(frame_path)
	frames.clear(&"exit")
	frames.set_animation_speed(&"exit", 24)
	frames.set_animation_loop(&"exit", true)
	for i in 20:
		frames.add_frame(&"exit", load("res://TheGame/Sprites/Effects/level_exit/level_exit_%02d.png" % i))
	assert(ResourceSaver.save(frames, frame_path) == OK)
	var exit_path := "res://TheGame/Scenes/Widgets/level_exit.tscn"
	var exit: Node = load(exit_path).instantiate()
	var marker: AnimatedSprite2D = exit.get_node("Marker")
	marker.centered = false
	marker.position = Vector2(-90.75, -110.7)
	_save(exit, exit_path)
	# 造一 colipse 经过完整变换后的范围；美术网格原点为悟空 +28Y、小猴 -13Y。
	var registrations := [
		["res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn", Vector2(0.4, 27.95), Vector2(45.00122, 90.01099)],
		["res://TheGame/Entitys/Enemies/Monsters/huaguoshan_monkey_entity.tscn", Vector2(-0.05, -12.55), Vector2(39.997864, 59.999085)],
		["res://TheGame/Entitys/Enemies/Monsters/demon_monkey_entity.tscn", Vector2(0.55, -0.45), Vector2(41.999817, 82.00058)],
		["res://TheGame/Entitys/Enemies/Monsters/gorilla_entity.tscn", Vector2(0.5, -0.05), Vector2(109.99603, 109.99786)]
	]
	for item in registrations:
		var actor: CharacterBody2D = load(item[0]).instantiate()
		var body: CollisionShape2D = actor.get_node("m_BodyShape" if actor.has_node("m_BodyShape") else "CollisionShape2D")
		var capsule := CapsuleShape2D.new()
		capsule.radius = item[2].x * 0.5
		capsule.height = item[2].y
		body.shape = capsule
		body.position = item[1]
		var hurt: CollisionShape2D = actor.get_node("m_HurtBox/CollisionShape2D")
		var rectangle := RectangleShape2D.new()
		rectangle.size = item[2]
		hurt.shape = rectangle
		hurt.position = item[1] - hurt.get_parent().position
		_save(actor, item[0])
	print("TERRAIN REGISTRATION PASS: three levels, source exit 20 frames, four source colipse registrations; boss bounds retained.")
	quit()

func _save(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	node.free()
