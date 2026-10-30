extends SceneTree
## Build the native 2-2 actor scenes from the verified source manifest.

const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D"
const ACTOR_INFO := {
	"cave_brute": {"folder": "Monsters/Ground/cave_brute", "script": "CaveBruteEntity", "id": 7, "attacks": ["attack_1"], "boss": false, "events": {}},
	"knife_demon": {"folder": "Monsters/Ground/knife_demon", "script": "KnifeDemonEntity", "id": 13, "attacks": ["attack_1", "attack_2"], "boss": false, "events": {"attack_2": {"frame": 2, "method": "ThrowKnifeAnimEvent", "effect": "knife_demon_blade"}}},
	"lion_camel_king": {"folder": "Bosses/zm1_05_qixing_cave", "script": "LionCamelKingEntity", "id": 11, "attacks": ["attack_1", "attack_2", "attack_3"], "boss": true, "events": {"attack_3": {"frame": 23, "method": "SpawnPoisonCloudAnimEvent", "effect": "lion_camel_poison_cloud"}}},
}

const EFFECT_INFO := {
	"knife_demon_blade": {"folder": "Monsters/Ground/knife_demon", "kind": "moving_blade"},
	"lion_camel_poison_cloud": {"folder": "Bosses/zm1_05_qixing_cave", "kind": "poison_cloud"},
}

const EFFECT_SCENE_PATHS := {
	"knife_demon_blade": "res://TheGame/Entitys/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_effect.tscn",
	"lion_camel_poison_cloud": "res://TheGame/Entitys/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_effect.tscn",
}

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10): await process_frame
	while filesystem.is_scanning() or filesystem.is_importing(): await process_frame
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	for effect in manifest.effects:
		if EFFECT_INFO.has(effect.name):
			assert(_build_effect(effect), "Failed to build effect scene: " + effect.name)
	for actor in manifest.actors:
		assert(_build_actor(actor), "Failed to build actor scene: " + actor.name)
	print("QIXING ACTOR SCENES PASS: three actors, knife and poison effects")
	quit()

func _build_actor(actor: Dictionary) -> bool:
	var info: Dictionary = ACTOR_INFO[actor.name]
	var folder: String = info.folder
	var root: CharacterBody2D = load("res://TheGame/Entitys/Enemies/Monsters/Ground/gorilla/gorilla_entity.tscn").instantiate()
	root.name = info.script
	root.set_script(load("res://TheGame/GameScripts/Entity/Enemies/%s/%s.cs" % [folder, info.script]))
	root.set("m_Perception", root.get_node("Perception"))
	root.set("m_EnemyEntityId", info.id)
	root.set("m_HealthBar", root.get_node("m_HealthBar"))
	root.set("m_Body", root.get_node("m_Body"))
	root.set("m_AnimPlayer", root.get_node("m_AnimPlayer"))
	root.set("m_AttackAnimations", PackedStringArray(info.attacks))
	root.set("m_HurtBox", root.get_node("m_HurtBox"))
	root.set("m_HitBox", root.get_node("m_HitBoxRoot/m_HitBox"))
	root.set("m_HitBoxRoot", root.get_node("m_HitBoxRoot"))
	var layer: Dictionary = actor.layers[0]
	# Normalize the one previously emitted manifest before scene authoring; new
	# manifests always store the path on the layer itself.
	var frames_path: String = layer.get("frames_path", actor.get("frames_path", ""))
	layer["frames_path"] = frames_path
	assert(not frames_path.is_empty(), "Missing layer frames_path for " + actor.name)
	var frames: SpriteFrames = load(frames_path)
	assert(frames != null, "Unable to load SpriteFrames: " + frames_path)
	var body: AnimatedSprite2D = root.get_node("m_Body")
	body.sprite_frames = frames
	body.animation = "idle"
	body.offset = Vector2(actor.layers[0].sprite_offset[0], actor.layers[0].sprite_offset[1])
	var hurt := Rect2(actor.hurt_bounds[0], actor.hurt_bounds[1], actor.hurt_bounds[2], actor.hurt_bounds[3])
	var capsule := CapsuleShape2D.new()
	capsule.radius = max(1.0, hurt.size.x * .5)
	capsule.height = max(hurt.size.y, capsule.radius * 2.0)
	root.get_node("m_BodyShape").shape = capsule
	root.get_node("m_BodyShape").position = hurt.get_center()
	var hurt_shape := RectangleShape2D.new()
	hurt_shape.size = hurt.size
	root.get_node("m_HurtBox/CollisionShape2D").shape = hurt_shape
	root.get_node("m_HurtBox/CollisionShape2D").position = hurt.get_center()
	root.get_node("m_HurtBox").position = Vector2.ZERO
	# Initial local layout only; later hand-authored positions remain in the scene.
	root.get_node("m_HealthBar").size = Vector2(50, 5)
	root.get_node("m_HealthBar").position = Vector2(hurt.get_center().x - 25, hurt.position.y - 13)
	var detector := RectangleShape2D.new()
	detector.size = Vector2(600, 300)
	root.get_node("Perception/Detection").shape = detector
	root.get_node("Perception/Detection").position = Vector2.ZERO
	detector = null
	var library := AnimationLibrary.new()
	for action in actor.actions:
		var animation := Animation.new()
		animation.length = action.frames / 24.0
		animation.loop_mode = Animation.LOOP_LINEAR if action.name in ["idle", "run"] else Animation.LOOP_NONE
		_track(animation, "m_Body:animation", [0], [StringName(action.name)])
		_track(animation, "m_Body:offset", [0], [body.offset])
		var times: Array = []
		var values: Array = []
		var disabled: Array = []
		var positions: Array = []
		var shapes: Array = []
		for frame in action.frames:
			var box := Rect2()
			if action.hit_bounds[frame] != null:
				var raw: Array = action.hit_bounds[frame]
				box = Rect2(raw[0], raw[1], raw[2], raw[3])
			var shape := RectangleShape2D.new()
			shape.size = box.size if box.has_area() else Vector2.ONE
			times.append(frame / 24.0)
			values.append(frame)
			disabled.append(not box.has_area())
			positions.append(box.get_center())
			shapes.append(shape)
		_track(animation, "m_Body:frame", times, values)
		_track(animation, HIT_PATH + ":disabled", times, disabled)
		_track(animation, HIT_PATH + ":position", times, positions)
		_track(animation, HIT_PATH + ":shape", times, shapes)
		if info.events.has(action.name):
			var event: Dictionary = info.events[action.name]
			var event_scene: PackedScene = load(EFFECT_SCENE_PATHS[event.effect])
			assert(event_scene != null, "Missing animation event scene: " + event.effect)
			var method_track := animation.add_track(Animation.TYPE_METHOD)
			animation.track_set_path(method_track, NodePath("."))
			animation.track_insert_key(method_track, float(event.frame) / 24.0,
				{"method": StringName(event.method), "args": [event_scene]})
		library.add_animation(action.name, animation)
	var reset := library.get_animation("idle").duplicate(true) as Animation
	reset.length = .001
	reset.loop_mode = Animation.LOOP_NONE
	for track in reset.get_track_count():
		while reset.track_get_key_count(track) > 1: reset.track_remove_key(track, 1)
	library.add_animation("RESET", reset)
	var animation_path: String = "res://TheGame/Entitys/Animations/Enemies/" + folder + "/" + actor.name + "_library.tres"
	var entity_path: String = "res://TheGame/Entitys/Enemies/" + folder + "/" + actor.name + "_entity.tscn"
	DirAccess.make_dir_recursive_absolute(animation_path.get_base_dir())
	DirAccess.make_dir_recursive_absolute(entity_path.get_base_dir())
	assert(ResourceSaver.save(library, animation_path) == OK)
	library.take_over_path(animation_path)
	root.get_node("m_AnimPlayer").remove_animation_library("")
	root.get_node("m_AnimPlayer").add_animation_library("", library)
	root.get_node("m_AnimPlayer").callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
	var packed := PackedScene.new()
	if root.get("m_EnemyEntityId") != null:
		load("res://EditorScripts/enemy_perception_authoring.gd").configure(root)
	assert(packed.pack(root) == OK and ResourceSaver.save(packed, entity_path) == OK)
	root.free()
	return true

func _build_effect(effect: Dictionary) -> bool:
	var info: Dictionary = EFFECT_INFO[effect.name]
	var folder: String = info.folder
	var layer: Dictionary = effect.layers[0]
	var frames: SpriteFrames = load(layer.frames_path)
	if frames == null:
		return false

	var root := Node2D.new()
	root.name = String(effect.name).to_pascal_case() + "Effect"
	var script_path := "res://TheGame/GameScripts/Entity/Effects/SkillEffectNode.cs" if info.kind == "moving_blade" else "res://TheGame/GameScripts/Entity/Enemies/Bosses/zm1_05_qixing_cave/LionCamelPoisonCloudNode.cs"
	root.set_script(load(script_path))
	var sprite := AnimatedSprite2D.new()
	sprite.name = "Sprite"
	sprite.sprite_frames = frames
	sprite.animation = "play"
	sprite.offset = Vector2(layer.sprite_offset[0], layer.sprite_offset[1])
	root.add_child(sprite)
	sprite.owner = root
	var hitbox := Area2D.new()
	hitbox.name = "HitBox"
	hitbox.collision_layer = 64
	hitbox.collision_mask = 128
	hitbox.monitorable = false
	root.add_child(hitbox)
	hitbox.owner = root
	var shape := CollisionShape2D.new()
	shape.name = "CollisionShape2D"
	shape.disabled = true
	var rectangle := RectangleShape2D.new()
	var bounds: Array = effect.actions[0].get("hit_bounds", [])
	if info.kind == "moving_blade":
		rectangle.size = Vector2.ONE
		for bound in bounds:
			if bound != null:
				rectangle.size.x = maxf(rectangle.size.x, float(bound[2]))
				rectangle.size.y = maxf(rectangle.size.y, float(bound[3]))
	else:
		# One simple region covers the visible puffs, not the invisible Flash debug probe.
		rectangle.size = Vector2(340, 139)
		shape.position = Vector2(111, -21.5)
	shape.shape = rectangle
	hitbox.add_child(shape)
	shape.owner = root
	var player := AnimationPlayer.new()
	player.name = "AnimationPlayer"
	player.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
	root.add_child(player)
	player.owner = root
	root.set("m_Player", player)
	root.set("m_HitBox", hitbox)

	var action: Dictionary = effect.actions[0]
	var animation := Animation.new()
	animation.length = float(action.frames) / 24.0
	var frame_times: Array[float] = []
	var frame_values: Array[int] = []
	var positions: Array[Vector2] = []
	for frame in action.frames:
		frame_times.append(float(frame) / 24.0)
		frame_values.append(frame)
		if info.kind == "moving_blade":
			var bound: Variant = bounds[frame]
			if bound == null:
				positions.append(Vector2.ZERO)
			else:
				positions.append(Vector2(float(bound[0]) + float(bound[2]) * 0.5,
					float(bound[1]) + float(bound[3]) * 0.5))
	_track(animation, "Sprite:animation", [0.0], [StringName("play")])
	_track(animation, "Sprite:frame", frame_times, frame_values)
	_track(animation, "HitBox/CollisionShape2D:disabled", [0.0, animation.length], [false, true])
	if info.kind == "moving_blade":
		_track(animation, "HitBox/CollisionShape2D:position", frame_times, positions)
	var library := AnimationLibrary.new()
	library.add_animation("play", animation)
	var animation_path: String = "res://TheGame/Entitys/Animations/Enemies/" + folder + "/" + String(effect.name) + "_library.tres"
	DirAccess.make_dir_recursive_absolute(animation_path.get_base_dir())
	assert(ResourceSaver.save(library, animation_path) == OK)
	library.take_over_path(animation_path)
	player.add_animation_library("", library)
	var scene_path: String = "res://TheGame/Entitys/Enemies/" + folder + "/" + String(effect.name) + "_effect.tscn"
	DirAccess.make_dir_recursive_absolute(scene_path.get_base_dir())
	var packed := PackedScene.new()
	if root.get("m_EnemyEntityId") != null:
		load("res://EditorScripts/enemy_perception_authoring.gd").configure(root)
	assert(packed.pack(root) == OK and ResourceSaver.save(packed, scene_path) == OK)
	root.free()
	return true

func _track(animation: Animation, path: String, times: Array, values: Array) -> void:
	var track := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(track, path)
	animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
	for index in times.size(): animation.track_insert_key(track, float(times[index]), values[index])
