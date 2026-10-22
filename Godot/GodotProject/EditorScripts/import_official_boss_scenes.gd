extends SceneTree
## Create new boss scenes and native animation libraries from verified source frames.

const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D"
const ART_ROOT := "res://TheGame/Sprites/Characters/Enemies/Bosses/"
const ANIMATION_ROOT := "res://TheGame/Entitys/Animations/Enemies/Bosses/"
var source: Dictionary

func _initialize() -> void:
	call_deferred("_build")

## Keep animation frame timing, registration, hit windows and method keys in native resources.
func _build() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	source = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	DirAccess.make_dir_recursive_absolute(ANIMATION_ROOT)
	DirAccess.make_dir_recursive_absolute("res://TheGame/Entitys/Enemies/Bosses")
	for name in ["zm1_02_macaque_king", "zm1_03_tamarin_king"]:
		DirAccess.make_dir_recursive_absolute("res://TheGame/Entitys/Enemies/Bosses/" + name)
		DirAccess.make_dir_recursive_absolute(ANIMATION_ROOT + name)
	var wind_scene := await _wind()
	for actor in source.actors:
		_boss(actor, wind_scene)
	print("BOSS SCENES PASS: native frame tracks, registered artwork, hit windows, pulse and wind callbacks saved.")
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	quit()

## Add a discrete property track whose values are owned by the animation.
func _track(animation: Animation, path: String, times: Array, values: Array) -> void:
	var index := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(index, path)
	animation.value_track_set_update_mode(index, Animation.UPDATE_DISCRETE)
	for key in times.size():
		animation.track_insert_key(index, float(times[key]), values[key])

## Methods remain named, inspectable animation events rather than table instructions.
func _methods(animation: Animation, path: String, entries: Array) -> void:
	if entries.is_empty():
		return
	var index := animation.add_track(Animation.TYPE_METHOD)
	animation.track_set_path(index, path)
	for entry in entries:
		animation.track_insert_key(index, float(entry[0]), {"method": entry[1], "args": entry[2]})

## Read the visible rectangle in a registered atlas frame, excluding transparent padding.
func _visible_bounds(frames: SpriteFrames, action: String, frame: int, offset: Vector2) -> Rect2:
	var texture := frames.get_frame_texture(action, frame)
	var image := texture.get_image()
	assert(image != null, "Frame texture could not be read")
	var used := image.get_used_rect()
	if not used.has_area():
		return Rect2()
	return Rect2(Vector2(used.position) - texture.get_size() * 0.5 + offset, Vector2(used.size))

## Generate collision geometry keys from official probe transforms or the damaging effect layer.
func _hit_tracks(animation: Animation, path: String, bounds: Array) -> void:
	var times: Array = []
	var disabled: Array = []
	var shapes: Array = []
	var positions: Array = []
	for frame in bounds.size():
		var box: Rect2 = bounds[frame]
		var shape := RectangleShape2D.new()
		shape.size = box.size if box.has_area() else Vector2.ONE
		times.append(frame / float(source.fps))
		disabled.append(not box.has_area())
		shapes.append(shape)
		positions.append(box.get_center())
	_track(animation, path + ":disabled", times, disabled)
	_track(animation, path + ":shape", times, shapes)
	_track(animation, path + ":position", times, positions)

## Create the independent stationary Boss2Bullet1 with its original 50-frame lifetime.
func _wind() -> PackedScene:
	var destination := "res://TheGame/Entitys/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade_effect.tscn"
	assert(not FileAccess.file_exists(destination), "Refusing to overwrite wind effect")
	var pictures: Array[Image] = []
	var bounds := Rect2i()
	var origin := Vector2i(floori(source.wind.bounds[0]), floori(source.wind.bounds[1]))
	for frame in int(source.wind.frames):
		var picture := Image.load_from_file(source.wind.input.path_join("%d.png" % (frame + 1)))
		assert(picture != null)
		pictures.append(picture)
		var used := picture.get_used_rect()
		if used.has_area():
			var registered := Rect2i(origin + used.position, used.size)
			bounds = registered if not bounds.has_area() else bounds.merge(registered)
	var hashes := {}
	var sequences: Array[int] = []
	var unique: Array[Image] = []
	for picture in pictures:
		var hash := HashingContext.new()
		hash.start(HashingContext.HASH_SHA256)
		hash.update(picture.get_data())
		var digest := hash.finish().hex_encode()
		if not hashes.has(digest):
			hashes[digest] = unique.size()
			unique.append(picture)
		sequences.append(hashes[digest])
	var columns := mini(8, unique.size())
	var atlas := Image.create_empty(bounds.size.x * columns, bounds.size.y * ceili(unique.size() / float(columns)), false, Image.FORMAT_RGBA8)
	for index in unique.size():
		var used := unique[index].get_used_rect()
		atlas.blit_rect(unique[index], used, Vector2i(index % columns, index / columns) * bounds.size + origin + used.position - bounds.position)
	var atlas_path := ART_ROOT + "zm1_02_macaque_king/macaque_wind_blade.png"
	if not FileAccess.file_exists(atlas_path):
		assert(atlas.save_png(atlas_path) == OK)
	var texture: Texture2D
	# Save the atlas-backed SpriteFrames after import, so runtime uses a normal texture resource.
	EditorInterface.get_resource_filesystem().scan()
	for frame in range(10):
		await process_frame
	while EditorInterface.get_resource_filesystem().is_scanning() or EditorInterface.get_resource_filesystem().is_importing():
		await process_frame
	EditorInterface.get_resource_filesystem().update_file(atlas_path)
	EditorInterface.get_resource_filesystem().reimport_files(PackedStringArray([atlas_path]))
	texture = load(atlas_path)
	assert(texture != null, "Wind atlas import did not complete")
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	frames.add_animation("play")
	frames.set_animation_speed("play", source.fps)
	frames.set_animation_loop("play", false)
	for index in sequences:
		var cell := AtlasTexture.new()
		cell.atlas = texture
		cell.region = Rect2(Vector2(index % columns, index / columns) * Vector2(bounds.size), Vector2(bounds.size))
		frames.add_frame("play", cell)
	var frames_path := ART_ROOT + "zm1_02_macaque_king/macaque_wind_blade_frames.tres"
	assert(ResourceSaver.save(frames, frames_path) == OK)
	frames.take_over_path(frames_path)
	var root := Node2D.new()
	root.name = "MacaqueWindBlade"
	root.set_script(load("res://TheGame/GameScripts/Entity/Effects/SkillEffectNode.cs"))
	var sprite := AnimatedSprite2D.new()
	sprite.name = "Sprite"
	sprite.sprite_frames = frames
	sprite.animation = "play"
	sprite.offset = Vector2(bounds.position) + Vector2(bounds.size) * 0.5
	_add(root, root, sprite)
	var area := Area2D.new()
	area.name = "HitBox"
	area.collision_layer = 64
	area.collision_mask = 128
	area.monitorable = false
	_add(root, root, area)
	var shape := CollisionShape2D.new()
	shape.name = "CollisionShape2D"
	shape.shape = RectangleShape2D.new()
	shape.disabled = true
	_add(root, area, shape)
	var animation := Animation.new()
	animation.length = source.wind.frames / source.fps
	_track(animation, "Sprite:animation", [0], [&"play"])
	var times: Array = []
	var values: Array = []
	var geometry: Array = []
	for frame in int(source.wind.frames):
		times.append(frame / float(source.fps))
		values.append(frame)
		geometry.append(_visible_bounds(frames, "play", frame, sprite.offset))
	_track(animation, "Sprite:frame", times, values)
	_hit_tracks(animation, "HitBox/CollisionShape2D", geometry)
	_methods(animation, ".", [[38.0 / source.fps, &"NewAttackAnimEvent", []]])
	var library := AnimationLibrary.new()
	library.add_animation("play", animation)
	assert(ResourceSaver.save(library, ANIMATION_ROOT + "zm1_02_macaque_king/macaque_wind_blade_library.tres") == OK)
	library.take_over_path(ANIMATION_ROOT + "zm1_02_macaque_king/macaque_wind_blade_library.tres")
	var player := AnimationPlayer.new()
	player.name = "AnimationPlayer"
	player.add_animation_library("", library)
	_add(root, root, player)
	root.set("m_Player", player)
	root.set("m_HitBox", area)
	root.set("m_Shape", shape)
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, destination) == OK)
	packed.take_over_path(destination)
	root.free()
	return packed

## Reuse the standard monster physics assembly while assigning concrete scripts and full native timelines.
func _boss(actor: Dictionary, wind_scene: PackedScene) -> void:
	var name: String = actor.name
	var macaque := name == "macaque_king"
	var folder := "zm1_02_macaque_king" if macaque else "zm1_03_tamarin_king"
	var destination := "res://TheGame/Entitys/Enemies/Bosses/%s/%s_entity.tscn" % [folder,name]
	assert(not FileAccess.file_exists(destination), "Refusing to overwrite boss scene")
	var root: CharacterBody2D = load("res://TheGame/Entitys/Enemies/Monsters/gorilla_entity.tscn").instantiate()
	root.name = "MacaqueKingEntity" if macaque else "TamarinKingEntity"
	root.set_script(load("res://TheGame/GameScripts/Entity/Enemies/Bosses/%s/%s.cs" % [folder, root.name]))
	for pair in [["m_Detector","m_Detector"],["m_Body","m_Body"],["m_AnimPlayer","m_AnimPlayer"],["m_HurtBox","m_HurtBox"],["m_HitBox","m_HitBoxRoot/m_HitBox"],["m_HitBoxRoot","m_HitBoxRoot"]]:
		root.set(pair[0], root.get_node(pair[1]))
	root.set("m_EnemyEntityId", 4 if macaque else 6)
	root.set("m_AttackAnimations", PackedStringArray(["attack_1","attack_2","attack_3"] if macaque else ["attack_1","attack_2"]))
	root.get_node("m_HealthBar").free()
	root.set("m_HealthBar", null)
	var body: AnimatedSprite2D = root.get_node("m_Body")
	var body_frames: SpriteFrames = load(ART_ROOT + "%s/%s_body_frames.tres" % [folder,name])
	var effect_frames: SpriteFrames = load(ART_ROOT + "%s/%s_effect_frames.tres" % [folder,name])
	body.sprite_frames = body_frames
	body.animation = "idle"
	body.offset = Vector2(actor.layers[0].sprite_offset[0],actor.layers[0].sprite_offset[1])
	var effect := AnimatedSprite2D.new()
	effect.name = "Effect"
	effect.sprite_frames = effect_frames
	effect.animation = effect_frames.get_animation_names()[0]
	effect.offset = Vector2(actor.layers[1].sprite_offset[0],actor.layers[1].sprite_offset[1])
	effect.visible = false
	_add(root, body, effect)
	var hurt := Rect2(actor.hurt_bounds[0],actor.hurt_bounds[1],actor.hurt_bounds[2],actor.hurt_bounds[3])
	var body_shape: CollisionShape2D = root.get_node("m_BodyShape")
	var capsule := CapsuleShape2D.new()
	capsule.radius = hurt.size.x * .5
	capsule.height = hurt.size.y
	body_shape.shape = capsule
	body_shape.position = hurt.get_center()
	var hurt_area: Area2D = root.get_node("m_HurtBox")
	hurt_area.position = Vector2.ZERO
	var hurt_shape: CollisionShape2D = hurt_area.get_node("CollisionShape2D")
	var rectangle := RectangleShape2D.new()
	rectangle.size = hurt.size
	hurt_shape.shape = rectangle
	hurt_shape.position = hurt.get_center()
	var detector: CollisionShape2D = root.get_node("m_Detector/CollisionShape2D")
	var sight := RectangleShape2D.new()
	sight.size = Vector2(600,1800 if not macaque else 600)
	detector.shape = sight
	detector.position = Vector2.ZERO
	var library := AnimationLibrary.new()
	for action in actor.actions:
		var animation := Animation.new()
		animation.length = action.frames / source.fps
		if action.name in ["idle","run"]:
			animation.loop_mode = Animation.LOOP_LINEAR
		_track(animation,"m_Body:animation",[0],[StringName(action.name)])
		_track(animation,"m_Body:offset",[0],[body.offset])
		_track(animation,"m_Body/Effect:visible",[0],[effect_frames.has_animation(action.name)])
		var times: Array = []
		var values: Array = []
		var geometry: Array = []
		for frame in int(action.frames):
			times.append(frame / float(source.fps))
			values.append(frame)
			var box := Rect2()
			if action.probe_bounds[frame] != null:
				var probe: Array = action.probe_bounds[frame]
				box = Rect2(probe[0],probe[1],probe[2],probe[3])
			elif not action.hit_windows[frame].is_empty() and effect_frames.has_animation(action.name):
				box = _visible_bounds(effect_frames,action.name,frame,effect.offset)
			geometry.append(box)
		_track(animation,"m_Body:frame",times,values)
		if effect_frames.has_animation(action.name):
			_track(animation,"m_Body/Effect:animation",[0],[StringName(action.name)])
			_track(animation,"m_Body/Effect:frame",times,values)
		else:
			_track(animation,"m_Body/Effect:animation",[0],[StringName(effect_frames.get_animation_names()[0])])
			_track(animation,"m_Body/Effect:frame",[0],[0])
		_hit_tracks(animation,HIT_PATH,geometry)
		var methods: Array = []
		if macaque and action.name == "attack_2":
			for frame in [5,11,16,20,25,30,35,41,46,52,58,65]:
				methods.append([(frame-1)/float(source.fps),&"NewAttackAnimEvent",[]])
		if not macaque and action.name == "attack_2":
			for frame in [47,51,56,62,68]:
				methods.append([(frame-1)/float(source.fps),&"NewAttackAnimEvent",[]])
		if macaque and action.name == "attack_3":
			methods.append([3.0/source.fps,&"SpawnWindBladeAnimEvent",[wind_scene]])
		_methods(animation,".",methods)
		library.add_animation(action.name,animation)
	var reset := library.get_animation("idle").duplicate(true) as Animation
	reset.length = .001
	reset.loop_mode = Animation.LOOP_NONE
	for track in reset.get_track_count():
		while reset.track_get_key_count(track)>1:
			reset.track_remove_key(track,1)
	library.add_animation("RESET",reset)
	assert(ResourceSaver.save(library,ANIMATION_ROOT+folder+"/"+name+"_library.tres")==OK)
	library.take_over_path(ANIMATION_ROOT+folder+"/"+name+"_library.tres")
	var player: AnimationPlayer = root.get_node("m_AnimPlayer")
	player.remove_animation_library("")
	player.add_animation_library("",library)
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_PHYSICS
	player.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
	var packed := PackedScene.new()
	assert(packed.pack(root)==OK)
	assert(ResourceSaver.save(packed,destination)==OK)
	root.free()
	print("BOSS SCENE: %s, %d complete animations, source hurt box %s" % [name,actor.actions.size(),hurt])

## Persist created nodes under the root scene rather than flattening independent child scenes.
func _add(root: Node, parent: Node, child: Node) -> void:
	parent.add_child(child)
	child.owner = root
