extends SceneTree
## Register complete Monster1 timelines and update the three existing ground scenes.

const ACTORS := {
	"huaguoshan_monkey": {"folder": "huaguoshan_monkey", "scene": "res://TheGame/Entitys/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_entity.tscn", "library": "res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_anim_library.tres"},
	"demon_monkey": {"folder": "demon_monkey", "scene": "res://TheGame/Entitys/Enemies/Monsters/Ground/demon_monkey/demon_monkey_entity.tscn", "library": "res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/demon_monkey/demon_monkey_anim_library.tres"},
	"gorilla": {"folder": "gorilla", "scene": "res://TheGame/Entitys/Enemies/Monsters/Ground/gorilla/gorilla_entity.tscn", "library": "res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/gorilla/gorilla_anim_library.tres"},
}
const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var args := OS.get_cmdline_user_args()
	assert(args.size() > 0, "manifest path is required")
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	for actor in manifest.actors:
		assert(await _import_actor(actor), "failed to import " + actor.name)
	print("GROUND MONSTER IMPORT PASS: complete source frames, timing and hit windows for ", manifest.actors.size(), " actors")
	quit()

func _import_actor(actor: Dictionary) -> bool:
	var info: Dictionary = ACTORS[actor.name]
	var layer: Dictionary = actor.layers[0]
	var folder: String = "res://TheGame/Sprites/Characters/Enemies/Monsters/Ground/" + info.folder
	DirAccess.make_dir_recursive_absolute(folder)
	var atlas_path: String = folder.path_join(actor.name + "_official_body.png")
	var frames_path: String = folder.path_join(actor.name + "_official_frames.tres")
	if not _pack_layer(actor, layer, atlas_path):
		return false
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for i in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var atlas: Texture2D = load(atlas_path)
	if atlas == null:
		return false
	var frames := _save_frames(actor, layer, atlas, frames_path)
	if frames == null:
		return false
	var packed: PackedScene = load(info.scene)
	var root: Node = packed.instantiate()
	var body: AnimatedSprite2D = root.get_node("m_Body")
	body.sprite_frames = frames
	body.position = Vector2.ZERO
	body.offset = Vector2(layer.sprite_offset[0], layer.sprite_offset[1])
	body.animation = &"idle"
	var hurt := Rect2(actor.hurt_bounds[0], actor.hurt_bounds[1], actor.hurt_bounds[2], actor.hurt_bounds[3])
	var body_shape: CollisionShape2D = root.get_node("m_BodyShape")
	var capsule := CapsuleShape2D.new()
	capsule.radius = maxf(1.0, hurt.size.x * .5)
	capsule.height = maxf(hurt.size.y, capsule.radius * 2.0)
	body_shape.shape = capsule
	body_shape.position = hurt.get_center()
	var hurt_shape: CollisionShape2D = root.get_node("m_HurtBox/CollisionShape2D")
	var rectangle := RectangleShape2D.new()
	rectangle.size = hurt.size
	hurt_shape.shape = rectangle
	hurt_shape.position = hurt.get_center()
	root.get_node("m_HurtBox").position = Vector2.ZERO
	var library: AnimationLibrary = load(info.library).duplicate(true)
	if library.has_animation(&"after_hurt"):
		library.remove_animation(&"after_hurt")
	var sprite_offset := Vector2(layer.sprite_offset[0], layer.sprite_offset[1])
	var shapes := {}
	for action in actor.actions:
		_update_animation(library, action, sprite_offset, shapes)
	var reset := library.get_animation(&"RESET")
	_remove_tracks(reset, ["m_Body:offset"])
	_track(reset, "m_Body:offset", [0.0], [sprite_offset])
	# Keep the authored library external and share identical geometry across actions.
	if ResourceSaver.save(library, info.library) != OK:
		root.free()
		return false
	library.take_over_path(info.library)
	var player: AnimationPlayer = root.get_node("m_AnimPlayer")
	player.remove_animation_library("")
	player.add_animation_library("", library)
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_PHYSICS
	player.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
	var packed_out := PackedScene.new()
	if packed_out.pack(root) != OK or ResourceSaver.save(packed_out, info.scene) != OK:
		root.free()
		return false
	root.free()
	return true

func _pack_layer(actor: Dictionary, layer: Dictionary, output: String) -> bool:
	var images: Array[Image] = []
	var placements: Array[Vector2i] = []
	var hashes := {}
	var bounds := Rect2i()
	var poses := {}
	for action in layer.actions:
		var cells: Array[int] = []
		for frame in int(action.frames):
			var image := Image.load_from_file(action.input.path_join("%d.png" % (frame + 1)))
			if image == null:
				return false
			var used := image.get_used_rect()
			var placement := Vector2i(floori(float(action.bounds[0]) + float(action.matrix[4])), floori(float(action.bounds[1]) + float(action.matrix[5]))) + used.position
			if used.has_area():
				var registered := Rect2i(placement, used.size)
				bounds = registered if not bounds.has_area() else bounds.merge(registered)
				image = image.get_region(used)
			else:
				image = Image.create_empty(1, 1, false, Image.FORMAT_RGBA8)
			var hash := HashingContext.new()
			hash.start(HashingContext.HASH_SHA256)
			hash.update(image.get_data())
			var digest := "%s/%s/%s" % [hash.finish().hex_encode(), image.get_size(), placement]
			if not hashes.has(digest):
				hashes[digest] = images.size()
				images.append(image)
				placements.append(placement)
			cells.append(hashes[digest])
		poses[action.name] = cells
	if not bounds.has_area():
		return false
	var columns := mini(8, images.size())
	var rows := ceili(float(images.size()) / columns)
	var atlas := Image.create_empty(bounds.size.x * columns, bounds.size.y * rows, false, Image.FORMAT_RGBA8)
	for index in images.size():
		atlas.blit_rect(images[index], Rect2i(Vector2i.ZERO, images[index].get_size()),
			Vector2i(index % columns, index / columns) * bounds.size + placements[index] - bounds.position)
	DirAccess.make_dir_recursive_absolute(output.get_base_dir())
	if atlas.save_png(output) != OK:
		return false
	layer["cell_size"] = [bounds.size.x, bounds.size.y]
	layer["sprite_offset"] = [float(bounds.position.x) + float(bounds.size.x) * .5, float(bounds.position.y) + float(bounds.size.y) * .5]
	layer["columns"] = columns
	layer["rows"] = rows
	layer["poses"] = poses
	return true

func _save_frames(actor: Dictionary, layer: Dictionary, atlas: Texture2D, path: String) -> SpriteFrames:
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var textures := {}
	var size := Vector2(layer.cell_size[0], layer.cell_size[1])
	for action in actor.actions:
		frames.add_animation(action.name)
		frames.set_animation_speed(action.name, 24.0)
		frames.set_animation_loop(action.name, action.name in ["idle", "run"])
		for cell in layer.poses[action.name]:
			if not textures.has(cell):
				var texture := AtlasTexture.new()
				texture.atlas = atlas
				texture.region = Rect2(Vector2(cell % int(layer.columns), cell / int(layer.columns)) * size, size)
				textures[cell] = texture
			frames.add_frame(action.name, textures[cell])
	if ResourceSaver.save(frames, path) != OK:
		return null
	frames.take_over_path(path)
	return frames

func _update_animation(library: AnimationLibrary, action: Dictionary, sprite_offset: Vector2, shapes: Dictionary) -> void:
	var animation: Animation
	if library.has_animation(action.name):
		animation = library.get_animation(action.name)
	else:
		animation = Animation.new()
		animation.resource_name = action.name
		library.add_animation(action.name, animation)
	_remove_tracks(animation, ["m_Body:animation", "m_Body:frame", "m_Body:offset", HIT_PATH + ":disabled", HIT_PATH + ":shape", HIT_PATH + ":position", HIT_PATH + ":rotation"])
	animation.length = float(action.frames) / 24.0
	animation.loop_mode = Animation.LOOP_LINEAR if action.name in ["idle", "run"] else Animation.LOOP_NONE
	_track(animation, "m_Body:animation", [0.0], [StringName(action.name)])
	_track(animation, "m_Body:offset", [0.0], [sprite_offset])
	_track(animation, HIT_PATH + ":rotation", [0.0], [0.0])
	var times: Array[float] = []
	var values: Array[int] = []
	for frame in int(action.frames):
		times.append(float(frame) / 24.0)
		values.append(frame)
	_track(animation, "m_Body:frame", times, values)
	var boxes: Array[Rect2] = []
	for value in action.hit_bounds:
		boxes.append(Rect2(value[0], value[1], value[2], value[3]) if value != null else Rect2())
	if boxes.is_empty():
		boxes.resize(int(action.frames))
	_hit_tracks(animation, boxes, shapes)

func _remove_tracks(animation: Animation, paths: Array[String]) -> void:
	for index in range(animation.get_track_count() - 1, -1, -1):
		if String(animation.track_get_path(index)) in paths:
			animation.remove_track(index)

func _track(animation: Animation, path: String, times: Array, values: Array) -> void:
	var track := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(track, path)
	animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
	for index in times.size():
		if index > 0 and values[index] == values[index - 1]:
			continue
		animation.track_insert_key(track, float(times[index]), values[index])

func _hit_tracks(animation: Animation, boxes: Array[Rect2], shape_cache: Dictionary) -> void:
	var times: Array[float] = []
	var disabled: Array[bool] = []
	var shapes: Array[Shape2D] = []
	var positions: Array[Vector2] = []
	for box in boxes:
		times.append(times.size() / 24.0)
		disabled.append(not box.has_area())
		var size := box.size if box.has_area() else Vector2.ONE
		if not shape_cache.has(size):
			var shape := RectangleShape2D.new()
			shape.size = size
			shape_cache[size] = shape
		shapes.append(shape_cache[size])
		positions.append(box.get_center())
	_track(animation, HIT_PATH + ":disabled", times, disabled)
	_track(animation, HIT_PATH + ":shape", times, shapes)
	_track(animation, HIT_PATH + ":position", times, positions)
