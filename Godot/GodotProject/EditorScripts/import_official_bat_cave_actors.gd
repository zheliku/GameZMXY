extends SceneTree
## Author only the new bat and statue resources from the matched official export.

const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D"
var source: Dictionary

func _initialize() -> void:
	call_deferred("_build")

## Import new atlases first, then save the external frame and animation resources.
func _build() -> void:
	source = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	for actor in source.actors:
		var folder := _folder(actor.name)
		DirAccess.make_dir_recursive_absolute("res://TheGame/Sprites/Characters/Enemies/" + folder)
		var atlas_path := "res://TheGame/Sprites/Characters/Enemies/%s/%s_body.png" % [folder, actor.name]
		if FileAccess.file_exists(atlas_path):
			assert(FileAccess.get_sha256(atlas_path) == FileAccess.get_sha256(actor.layers[0].output),
				"Refusing to replace an edited actor atlas")
		else:
			assert(DirAccess.copy_absolute(actor.layers[0].output, atlas_path) == OK)
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	for actor in source.actors:
		var atlas_path := "res://TheGame/Sprites/Characters/Enemies/%s/%s_body.png" % [_folder(actor.name), actor.name]
		filesystem.update_file(atlas_path)
		filesystem.reimport_files(PackedStringArray([atlas_path]))
		if not _actor(actor):
			quit(1)
			return
	print("BAT CAVE ACTORS PASS: registered full actions, source hurt boxes, four attack phases, pooled enemy scenes.")
	quit()

## Shared bats remain under their character name; the unique statue belongs to this level.
func _folder(actor_name: String) -> String:
	return "Monsters/Sky/bat" if actor_name == "bat" else "Bosses/zm1_04_bat_cave"

## Omit repeated values so the inspector shows meaningful phase changes.
func _track(animation: Animation, path: String, times: Array, values: Array) -> void:
	var track := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(track, NodePath(path))
	animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
	for index in times.size():
		if index == 0 or values[index] != values[index - 1]:
			animation.track_insert_key(track, float(times[index]), values[index])

## Create a standard enemy assembly without touching already authored actors.
func _actor(actor: Dictionary) -> bool:
	var bat: bool = actor.name == "bat"
	var folder := _folder(actor.name)
	var art_root := "res://TheGame/Sprites/Characters/Enemies/" + folder
	var animation_root := "res://TheGame/Entitys/Animations/Enemies/" + folder
	var entity_root := "res://TheGame/Entitys/Enemies/" + folder
	DirAccess.make_dir_recursive_absolute(animation_root)
	DirAccess.make_dir_recursive_absolute(entity_root)
	var destination := entity_root.path_join(actor.name + "_entity.tscn")
	assert(not FileAccess.file_exists(destination), "Refusing to replace an authored actor scene")
	var layer: Dictionary = actor.layers[0]
	var atlas: Texture2D = load(art_root.path_join(actor.name + "_body.png"))
	assert(atlas != null)
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var cells := {}
	for action in actor.actions:
		frames.add_animation(action.name)
		frames.set_animation_speed(action.name, source.fps)
		frames.set_animation_loop(action.name, action.name in ["idle", "run"])
		for value in layer.poses[action.name]:
			var cell := int(value)
			if not cells.has(cell):
				var texture := AtlasTexture.new()
				texture.atlas = atlas
				texture.region = Rect2(Vector2(cell % int(layer.columns), cell / int(layer.columns)) *
					Vector2(layer.cell_size[0], layer.cell_size[1]), Vector2(layer.cell_size[0], layer.cell_size[1]))
				cells[cell] = texture
			frames.add_frame(action.name, cells[cell])
	var frames_path := art_root.path_join(actor.name + "_frames.tres")
	assert(ResourceSaver.save(frames, frames_path) == OK)
	frames.take_over_path(frames_path)
	var root: CharacterBody2D = load("res://TheGame/Entitys/Enemies/Monsters/Ground/gorilla/gorilla_entity.tscn").instantiate()
	root.name = "BatEntity" if bat else "BatCaveStatueEntity"
	root.set_script(load("res://TheGame/GameScripts/Entity/Enemies/%s/%s.cs" %
		["Monsters/Bat" if bat else "Bosses/zm1_04_bat_cave", root.name]))
	for pair in [["_perception", "Perception"], ["m_Body", "m_Body"], ["m_AnimPlayer", "m_AnimPlayer"],
		["m_HurtBox", "m_HurtBox"], ["m_HitBox", "m_HitBoxRoot/m_HitBox"], ["m_HitBoxRoot", "m_HitBoxRoot"],
		["m_HealthBar", "m_HealthBar"]]:
		root.set(pair[0], root.get_node(pair[1]))
	root.set("_enemyEntityId", 8 if bat else 21)
	root.set("_attackAnimations", PackedStringArray(["attack_1"] if bat else []))
	root.motion_mode = CharacterBody2D.MOTION_MODE_FLOATING
	root.collision_mask = 1
	var body: AnimatedSprite2D = root.get_node("m_Body")
	body.sprite_frames = frames
	body.animation = "idle"
	body.offset = Vector2(layer.sprite_offset[0], layer.sprite_offset[1])
	var hurt := Rect2(actor.hurt_bounds[0], actor.hurt_bounds[1], actor.hurt_bounds[2], actor.hurt_bounds[3])
	var rectangle := RectangleShape2D.new()
	rectangle.size = hurt.size
	root.get_node("m_BodyShape").shape = rectangle
	root.get_node("m_BodyShape").position = hurt.get_center()
	root.get_node("m_HurtBox").position = Vector2.ZERO
	# Initial local layout only; later hand-authored positions remain in the scene.
	root.get_node("m_HealthBar").size = Vector2(50, 5)
	root.get_node("m_HealthBar").position = Vector2(hurt.get_center().x - 25, hurt.position.y - 13)
	root.get_node("m_HurtBox/CollisionShape2D").shape = rectangle
	root.get_node("m_HurtBox/CollisionShape2D").position = hurt.get_center()
	var detector := RectangleShape2D.new()
	detector.size = Vector2(600, 1000)
	root.get_node("Perception/Detection").shape = detector
	root.get_node("Perception/Detection").position = Vector2.ZERO
	var library := AnimationLibrary.new()
	var shapes := {}
	for action in actor.actions:
		var animation := Animation.new()
		animation.length = (action.frames - (1 if action.name == "hurt" and bat else 0)) / float(source.fps)
		animation.loop_mode = Animation.LOOP_LINEAR if action.name in ["idle", "run"] else Animation.LOOP_NONE
		_track(animation, "m_Body:animation", [0], [StringName(action.name)])
		_track(animation, "m_Body:offset", [0], [body.offset])
		var times: Array = []
		var indices: Array = []
		var disabled: Array = []
		var positions: Array = []
		var shape_keys: Array = []
		for frame in int(action.frames):
			var box := Rect2()
			if action.hit_bounds[frame] != null:
				var values: Array = action.hit_bounds[frame]
				box = Rect2(values[0], values[1], values[2], values[3])
			var size := box.size if box.has_area() else Vector2.ONE
			if not shapes.has(size):
				var shape := RectangleShape2D.new()
				shape.size = size
				shapes[size] = shape
			times.append(frame / float(source.fps))
			indices.append(frame)
			disabled.append(not box.has_area())
			positions.append(box.get_center())
			shape_keys.append(shapes[size])
		_track(animation, "m_Body:frame", times, indices)
		_track(animation, HIT_PATH + ":disabled", times, disabled)
		_track(animation, HIT_PATH + ":position", times, positions)
		_track(animation, HIT_PATH + ":shape", times, shape_keys)
		library.add_animation(action.name, animation)
	var reset := library.get_animation("idle").duplicate(true) as Animation
	reset.length = 0.001
	reset.loop_mode = Animation.LOOP_NONE
	for track in reset.get_track_count():
		while reset.track_get_key_count(track) > 1:
			reset.track_remove_key(track, 1)
	library.add_animation("RESET", reset)
	var library_path := animation_root.path_join(actor.name + "_library.tres")
	assert(ResourceSaver.save(library, library_path) == OK)
	library.take_over_path(library_path)
	var player: AnimationPlayer = root.get_node("m_AnimPlayer")
	player.remove_animation_library("")
	player.add_animation_library("", library)
	player.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
	var packed := PackedScene.new()
	if root.get("_enemyEntityId") != null:
		load("res://EditorScripts/enemy_perception_authoring.gd").configure(root)
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, destination) == OK)
	root.free()
	return true
