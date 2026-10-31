extends SceneTree
## Pack the twenty original frames into one atlas and author the native spike assembly.

func _initialize() -> void:
	call_deferred("_build")

func _build() -> void:
	var art := "res://TheGame/Sprites/Levels/zm1_04_bat_cave"
	var source := OS.get_cmdline_user_args()[0]
	var atlas := Image.create_empty(190, 256, false, Image.FORMAT_RGBA8)
	for index in range(20):
		var frame := Image.load_from_file(source.path_join("%d.png" % (index + 1)))
		assert(frame != null and frame.get_size() == Vector2i(38, 64))
		atlas.blit_rect(frame, Rect2i(0, 0, 38, 64), Vector2i(index % 5 * 38, index / 5 * 64))
	var atlas_path := art.path_join("bat_cave_spike_atlas.png")
	assert(atlas.save_png(atlas_path) == OK)
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for index in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	filesystem.update_file(atlas_path)
	filesystem.reimport_files(PackedStringArray([atlas_path]))
	var root := Area2D.new()
	root.name = "BatCaveSpike"
	root.set_script(load("res://TheGame/GameScripts/Entity/Level/Traps/BatCaveSpikeNode.cs"))
	root.collision_layer = 0
	root.collision_mask = 128
	root.monitorable = false
	var sprite := AnimatedSprite2D.new()
	sprite.name = "Sprite"
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	frames.add_animation("spike")
	frames.set_animation_speed("spike", 24.0)
	frames.set_animation_loop("spike", false)
	var texture: Texture2D = load(atlas_path)
	if texture == null:
		push_error("Spike atlas was not imported")
		quit(1)
		return
	for index in range(20):
		var cell := AtlasTexture.new()
		cell.atlas = texture
		cell.region = Rect2(index % 5 * 38, index / 5 * 64, 38, 64)
		frames.add_frame("spike", cell)
	var frames_path := art.path_join("bat_cave_spike_frames.tres")
	assert(ResourceSaver.save(frames, frames_path) == OK)
	frames.take_over_path(frames_path)
	sprite.sprite_frames = frames
	sprite.animation = "spike"
	sprite.offset = Vector2(1.45, 0.925)
	root.add_child(sprite); sprite.owner = root
	root.set("m_Sprite", sprite)
	var shape_node := CollisionShape2D.new()
	shape_node.name = "CollisionShape2D"
	var shape := RectangleShape2D.new(); shape.size = Vector2(30, 58)
	shape_node.shape = shape; shape_node.position = Vector2(1.45, 0.925)
	root.add_child(shape_node); shape_node.owner = root
	root.set("m_Shape", shape_node)
	var trigger := RayCast2D.new()
	trigger.name = "Trigger"
	trigger.collision_mask = root.collision_mask
	trigger.position = Vector2(1.45, 0)
	trigger.target_position = Vector2(0, 1000)
	trigger.collide_with_areas = true
	trigger.collide_with_bodies = false
	root.add_child(trigger); trigger.owner = root
	root.set("m_TriggerRay", trigger)
	sprite.speed_scale = 1
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, "res://TheGame/Scenes/Traps/bat_cave_spike.tscn") == OK)
	root.free()
	# Raw source frames remain in the ignored official export directory; the game uses one atlas.
	for index in range(1, 21):
		var loose := art.path_join("spikes/%d.png" % index)
		if FileAccess.file_exists(loose):
			assert(DirAccess.remove_absolute(loose) == OK)
		if FileAccess.file_exists(loose + ".import"):
			assert(DirAccess.remove_absolute(loose + ".import") == OK)
	if DirAccess.dir_exists_absolute(art.path_join("spikes")):
		assert(DirAccess.remove_absolute(art.path_join("spikes")) == OK)
	print("BAT CAVE SPIKE SCENE PASS")
	quit()
