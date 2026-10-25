extends SceneTree
## Restore reviewed attack geometry without rewriting art, event keys or timing.

const KNIFE_LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/knife_demon_library.tres"
const LION_LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_library.tres"
const WUKONG_LIBRARY := "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres"
const TAMARIN_LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_library.tres"
const COMBO_LIBRARY := "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_combo_library.tres"
const MACAQUE_LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/macaque_king_library.tres"
const CLOUD_LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_library.tres"
const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D:shape"

## Use one rectangle per meaningful phase; cloud decoration is excluded in the authored boss phases.
func _replace_bounds(animation: Animation, path: String, phases: Array) -> void:
	var tracks: Array[int] = []
	for property in [":shape", ":position", ":rotation"]:
		var track := animation.find_track(NodePath(path + property), Animation.TYPE_VALUE)
		if track < 0:
			track = animation.add_track(Animation.TYPE_VALUE)
			animation.track_set_path(track, NodePath(path + property))
			animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
		tracks.append(track)
	var shape_track := tracks[0]
	var position_track := tracks[1]
	var rotation_track := tracks[2]
	for track in [shape_track, position_track, rotation_track]:
		while animation.track_get_key_count(track) > 0:
			animation.track_remove_key(track, 0)
	var palette := {}
	var previous_size := Vector2(-1, -1)
	var previous_position := Vector2(INF, INF)
	var previous_rotation = null
	for phase in phases:
		var bounds: Rect2 = phase[1]
		var rotation: float = phase[2] if phase.size() > 2 else 0.0
		if bounds.size != previous_size:
			if not palette.has(bounds.size):
				var shape := RectangleShape2D.new()
				shape.size = bounds.size
				palette[bounds.size] = shape
			animation.track_insert_key(shape_track, phase[0], palette[bounds.size])
		if bounds.get_center() != previous_position:
			animation.track_insert_key(position_track, phase[0], bounds.get_center())
		if previous_rotation == null or previous_rotation != rotation:
			animation.track_insert_key(rotation_track, phase[0], rotation)
		previous_size = bounds.size
		previous_position = bounds.get_center()
		previous_rotation = rotation

## Group cloud frames only while every edge stays within six pixels of its phase envelope.
## One simple rectangle follows growth/fading; no outline extraction or runtime fitting.
func _cloud() -> void:
	var scene: Node2D = load("res://TheGame/Entitys/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_effect.tscn").instantiate()
	var sprite: AnimatedSprite2D = scene.get_node("Sprite")
	var rectangles: Array[Rect2] = []
	for frame in sprite.sprite_frames.get_frame_count(&"play"):
		var texture := sprite.sprite_frames.get_frame_texture(&"play", frame)
		var image := texture.get_image()
		var used := image.get_used_rect()
		var visible := Rect2i()
		for y in range(used.position.y, used.end.y):
			for x in range(used.position.x, used.end.x):
				if image.get_pixel(x, y).a >= .1:
					var pixel := Rect2i(x, y, 1, 1)
					visible = visible.merge(pixel) if visible.has_area() else pixel
		rectangles.append(Rect2(Vector2(visible.position) - texture.get_size() * .5 + sprite.offset, Vector2(visible.size)) if visible.has_area() else Rect2())
	var phases: Array = []
	var first := 0
	while first < rectangles.size():
		var last := first + 1
		var envelope := rectangles[first]
		while last < rectangles.size():
			if envelope.has_area() != rectangles[last].has_area():
				break
			var candidate := envelope.merge(rectangles[last])
			var acceptable := true
			for index in range(first, last + 1):
				var bounds := rectangles[index]
				if bounds.position.x - candidate.position.x > 6 or bounds.position.y - candidate.position.y > 6 or candidate.end.x - bounds.end.x > 6 or candidate.end.y - bounds.end.y > 6:
					acceptable = false
					break
			if not acceptable:
				break
			envelope = candidate
			last += 1
		if phases.is_empty() or phases.back()[1] != envelope:
			phases.append([first / 24.0, envelope])
		first = last
	var uid := ResourceLoader.get_resource_uid(CLOUD_LIBRARY)
	var library: AnimationLibrary = load(CLOUD_LIBRARY)
	var animation := library.get_animation(&"play")
	_replace_bounds(animation, "HitBox/CollisionShape2D", phases.filter(func(phase): return phase[1].has_area()))
	var disabled := animation.find_track(NodePath("HitBox/CollisionShape2D:disabled"), Animation.TYPE_VALUE)
	while animation.track_get_key_count(disabled) > 0:
		animation.track_remove_key(disabled, 0)
	var previous = null
	for phase in phases:
		var value: bool = not phase[1].has_area()
		if previous == null or previous != value:
			animation.track_insert_key(disabled, phase[0], value)
		previous = value
	animation.track_insert_key(disabled, animation.length, true)
	assert(ResourceSaver.save(library, CLOUD_LIBRARY) == OK)
	assert(ResourceSaver.set_uid(CLOUD_LIBRARY, uid) == OK)
	scene.free()
	print("CLOUD PHASES: ", phases.size(), " geometry changes for ", rectangles.size(), " art frames; envelope error <=6px per edge")

## Two rectangles cover the wind crown and lower funnel; preserve the existing phase times.
func _macaque() -> void:
	var uid := ResourceLoader.get_resource_uid(MACAQUE_LIBRARY)
	var library: AnimationLibrary = load(MACAQUE_LIBRARY)
	_replace_bounds(library.get_animation(&"attack_1"), "m_HitBoxRoot/m_HitBox/CollisionShape2D", [
		[0.0, Rect2(-.5, -.5, 1, 1)],
		[2.0/24, Rect2(Vector2(-35.35, -3.85) - Vector2(69.509883, 166.82809)*.5, Vector2(69.509883, 166.82809)), -1.1553792],
		[4.0/24, Rect2(Vector2(-41.3, 20.95) - Vector2(172.038334, 48.967025)*.5, Vector2(172.038334, 48.967025))]])
	var animation := library.get_animation(&"attack_2")
	_replace_bounds(animation, "m_HitBoxRoot/m_HitBox/CollisionShape2D", [
		[0.0, Rect2(-.5, -.5, 1, 1)],
		[4.0/24, Rect2(-51, -10, 167, 79)],
		[15.0/24, Rect2(-51, -43, 162, 114)],
		[19.0/24, Rect2(-51, -59, 162, 131)],
		[24.0/24, Rect2(-51, -78, 162, 151)],
		[29.0/24, Rect2(-51, -120, 162, 198)],
		[34.0/24, Rect2(-79, -75, 170, 145)]])
	_replace_bounds(animation, "m_HitBoxRoot/m_HitBox/WindCrown", [
		[0.0, Rect2(-.5, -.5, 1, 1)],
		[34.0/24, Rect2(-173, -215, 352, 140)]])
	assert(ResourceSaver.save(library, MACAQUE_LIBRARY) == OK)
	assert(ResourceSaver.set_uid(MACAQUE_LIBRARY, uid) == OK)

## Keep existing legacy-art timing; source probes restore the missing reach/stage changes.
func _ground_monsters() -> void:
	for item in [
		["huaguoshan_monkey", [[0.283333, Rect2(-46.3997, -47.8496, 34.9994, 61.9992)]]],
		["demon_monkey", [[0.283333, Rect2(Vector2(-32.45,-5.05) - Vector2(38.999849,97.999049)*.5, Vector2(38.999849,97.999049)), -0.7854081], [0.3334, Rect2(-69.4, -2.8502, 98, 39.0004)]]],
		["gorilla", [[0.366667, Rect2(-135.75, -34.5996, 144.75, 61.9992)]]]]:
		var file: String = "res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/%s/%s_anim_library.tres" % [item[0], item[0]]
		var uid := ResourceLoader.get_resource_uid(file)
		var library: AnimationLibrary = load(file)
		_replace_bounds(library.get_animation(&"attack_1"), "m_HitBoxRoot/m_HitBox/CollisionShape2D", item[1])
		preload("res://EditorScripts/repair_animation_defaults.gd").complete_rotation_tracks(library)
		assert(ResourceSaver.save(library, file) == OK)
		assert(ResourceSaver.set_uid(file, uid) == OK)

## Normal attacks retain visible weapons/energy plus a modest allowance from the source probe.
## Skills retain the official probe minimum. Author native keys only; never fit at runtime.
func _hero_attack_geometry(probes: Dictionary, normal_only: bool = false) -> void:
	var actor: Node2D = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
	actor.set_script(null)
	var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
	for library_name in player.get_animation_library_list():
		var library: AnimationLibrary = player.get_animation_library(library_name).duplicate(true)
		for name in library.get_animation_list():
			var animation := library.get_animation(name)
			for track in animation.get_track_count():
				if animation.track_get_type(track) == Animation.TYPE_METHOD:
					animation.track_set_enabled(track, false)
		player.remove_animation_library(library_name)
		player.add_animation_library(library_name, library)
	root.add_child(actor)
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var weapon: Sprite2D = actor.get_node("m_Weapon")
	var effect: AnimatedSprite2D = actor.get_node("m_EffectRoot/m_Effect")
	var hit: CollisionShape2D = actor.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D")
	var appearance: Resource = load("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres")
	var textures: Array[Texture2D] = [appearance.get("m_BaseWeapon")]
	for texture in (appearance.get("m_WeaponTextures") as Dictionary).values():
		if not textures.has(texture):
			textures.append(texture)
	var libraries := {"":WUKONG_LIBRARY, "skills":"res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_skill_library.tres"}
	if normal_only:
		libraries.erase("skills")
	var expanded := 0
	for library_name in libraries:
		var path: String = libraries[library_name]
		var uid := ResourceLoader.get_resource_uid(path)
		var target: AnimationLibrary = load(path)
		for action in target.get_animation_list():
			if not String(action).begins_with("attack_") and library_name != "skills":
				continue
			var animation := target.get_animation(action)
			assert(probes.has(String(action)))
			var name: String = String(action) if library_name == "" else library_name + "/" + String(action)
			var rows: Array = []
			var changed := false
			for frame in roundi(animation.length * 24):
				player.stop()
				player.play(&"RESET")
				player.advance(0)
				player.stop()
				player.play(name)
				player.advance(0)
				player.seek(frame / 24.0 + .00001, true, true)
				player.pause()
				var size: Vector2 = hit.shape.size
				var position: Vector2 = hit.position
				if not hit.disabled:
					var low := Vector2(INF, INF)
					var high := Vector2(-INF, -INF)
					var probe_low := Vector2(INF, INF)
					var probe_high := Vector2(-INF, -INF)
					var probe = probes[String(action)][frame]
					if probe != null:
						var center := Vector2(probe.position[0], probe.position[1])
						var half := Vector2(probe.size[0], probe.size[1]) * .5
						var source := Transform2D(probe.rotation, center)
						var inverse := hit.global_transform.affine_inverse() * actor.global_transform
						for corner in [Vector2(-half.x,-half.y),Vector2(half.x,-half.y),half,Vector2(-half.x,half.y)]:
							var point: Vector2 = inverse * (source * corner)
							probe_low = probe_low.min(point)
							probe_high = probe_high.max(point)
					var transform := hit.global_transform.affine_inverse() * weapon.global_transform
					for texture in textures:
						var cell := Vector2i(texture.get_size()) / Vector2i(weapon.hframes, weapon.vframes)
						var image := texture.get_image().get_region(Rect2i(weapon.frame_coords * cell, cell))
						var used := image.get_used_rect()
						for y in range(used.position.y, used.end.y):
							for x in range(used.position.x, used.end.x):
								if image.get_pixel(x, y).a < .25:
									continue
								var point := Vector2(x + .5, y + .5) - Vector2(cell) * .5
								point *= Vector2(-1 if weapon.flip_h else 1, -1 if weapon.flip_v else 1)
								point = transform * (point + weapon.offset)
								low = low.min(point)
								high = high.max(point)
					if library_name == "":
						var texture := effect.sprite_frames.get_frame_texture(effect.animation, effect.frame)
						if texture != null:
							var image := texture.get_image()
							var used := image.get_used_rect()
							var effect_transform := hit.global_transform.affine_inverse() * effect.global_transform
							for y in range(used.position.y, used.end.y):
								for x in range(used.position.x, used.end.x):
									if image.get_pixel(x, y).a < .1:
										continue
									var point := effect_transform * (Vector2(x + .5, y + .5) - Vector2(image.get_size()) * .5 + effect.offset)
									low = low.min(point)
									high = high.max(point)
						assert(is_finite(low.x), "An active normal attack has no weapon or energy")
						var padded_low := low - Vector2.ONE * 3
						var padded_high := high + Vector2.ONE * 3
						# The source probe is a reference, not a minimum: keep half its excess reach.
						low = padded_low.min(padded_low.lerp(probe_low, .5)) if probe != null else padded_low
						high = padded_high.max(padded_high.lerp(probe_high, .5)) if probe != null else padded_high
						# Tightening must never enlarge a previously authored normal attack.
						low = low.max(-size * .5)
						high = high.min(size * .5)
						var previous_size := size
						var previous_position := position
						position = (hit.transform * ((low + high) * .5)).snapped(Vector2.ONE * .0001)
						size = (high - low).max(Vector2.ONE * 14).snapped(Vector2.ONE * .0001)
						if not size.is_equal_approx(previous_size) or not position.is_equal_approx(previous_position):
							changed = true
							expanded += 1
					elif low.min(probe_low).x < -size.x/2 - .01 or low.min(probe_low).y < -size.y/2 - .01 or high.max(probe_high).x > size.x/2 + .01 or high.max(probe_high).y > size.y/2 + .01:
						low = (low - Vector2.ONE * 3).min(-size * .5).min(probe_low)
						high = (high + Vector2.ONE * 3).max(size * .5).max(probe_high)
						position = (hit.transform * ((low + high) * .5)).snapped(Vector2.ONE * .0001)
						size = (high - low).snapped(Vector2.ONE * .0001)
						changed = true
						expanded += 1
				rows.append([frame / 24.0, size, position, hit.rotation])
			if not changed:
				continue
			var palette := {}
			for property in [":shape", ":position", ":rotation"]:
				var track := animation.find_track(NodePath(HIT_PATH.replace(":shape", property)), Animation.TYPE_VALUE)
				assert(track >= 0)
				while animation.track_get_key_count(track) > 0:
					animation.track_remove_key(track, 0)
				var previous = null
				for row in rows:
					var value = row[1] if property == ":shape" else row[2] if property == ":position" else row[3]
					if previous == null or previous != value:
						if property == ":shape":
							if not palette.has(value):
								var shape := RectangleShape2D.new()
								shape.size = value
								palette[value] = shape
							animation.track_insert_key(track, row[0], palette[value])
						else:
							animation.track_insert_key(track, row[0], value)
					previous = value
		assert(ResourceSaver.save(target, path) == OK)
		assert(ResourceSaver.set_uid(path, uid) == OK)
	actor.free()
	print("HERO REACH: ", textures.size(), " appearances; ", expanded, " active pose samples authored; normals use reduced probe allowance; gameplay windows/events retained")

func _set_shape_keys(animation: Animation, values: Dictionary) -> void:
	var track := animation.find_track(NodePath(HIT_PATH), Animation.TYPE_VALUE)
	assert(track >= 0)
	for key in animation.track_get_key_count(track):
		var time := animation.track_get_key_time(track, key)
		for expected in values:
			if abs(time - float(expected)) < .001:
				var shape := RectangleShape2D.new()
				shape.size = values[expected]
				animation.track_set_key_value(track, key, shape)

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	assert(not args.is_empty(), "Pass the verified skill exporter manifest after --")
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	assert(source.source_sha256 == "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7")
	assert(source.fire_demon_combo_bounds.size() == 44)
	assert(source.has("hero_hit_probes") and source.hero_hit_probes.size() == 10)
	if args.has("--normal-attacks-only"):
		_hero_attack_geometry(source.hero_hit_probes, true)
		quit()
		return
	_cloud()
	_macaque()
	_ground_monsters()
	var combo_uid := ResourceLoader.get_resource_uid(COMBO_LIBRARY)
	var combo: AnimationLibrary = load(COMBO_LIBRARY)
	var phases: Array = []
	var previous := Rect2()
	# Source frames 43/44 have no visible attack; preserve the existing disabled window.
	for frame in 42:
		var values: Array = source.fire_demon_combo_bounds[frame]
		var bounds := Rect2(values[0], values[1], values[2], values[3])
		if frame == 0 or bounds != previous:
			phases.append([frame / 24.0, bounds])
		previous = bounds
	_replace_bounds(combo.get_animation(&"play"), "Visual/HitBox/CollisionShape2D", phases)
	assert(ResourceSaver.save(combo, COMBO_LIBRARY) == OK)
	assert(ResourceSaver.set_uid(COMBO_LIBRARY, combo_uid) == OK)

	var tamarin_uid := ResourceLoader.get_resource_uid(TAMARIN_LIBRARY)
	var tamarin: AnimationLibrary = load(TAMARIN_LIBRARY)
	var path := HIT_PATH.replace(":shape", "")
	_replace_bounds(tamarin.get_animation(&"attack_1"), path, [
		[0.0, Rect2(-0.5, -0.5, 1, 1)],
		[2.0/24, Rect2(-145, -5, 85, 60)],
		[10.0/24, Rect2(-180, -190, 150, 245)],
		[13.0/24, Rect2(-210, -165, 205, 220)],
		[16.0/24, Rect2(-135, -100, 65, 150)],
		[19.0/24, Rect2(-120, 25, 35, 25)]])
	_replace_bounds(tamarin.get_animation(&"attack_2"), path, [
		[0.0, Rect2(-0.5, -0.5, 1, 1)],
		[40.0/24, Rect2(-265, -135, 510, 185)],
		[46.0/24, Rect2(-150, -135, 340, 185)],
		[50.0/24, Rect2(-205, -135, 410, 185)],
		[60.0/24, Rect2(-220, -135, 380, 110)],
		[61.0/24, Rect2(-245, -135, 530, 190)]])
	assert(ResourceSaver.save(tamarin, TAMARIN_LIBRARY) == OK)
	assert(ResourceSaver.set_uid(TAMARIN_LIBRARY, tamarin_uid) == OK)

	var knife_uid := ResourceLoader.get_resource_uid(KNIFE_LIBRARY)
	var knife: AnimationLibrary = load(KNIFE_LIBRARY)
	var knife_attack := knife.get_animation(&"attack_1")
	_set_shape_keys(knife_attack, {
		0.0833: Vector2(151.75, 136.1),
		0.1667: Vector2(110.85, 77.4),
	})
	var knife_position_track := knife_attack.find_track(NodePath(HIT_PATH.replace(":shape", ":position")), Animation.TYPE_VALUE)
	assert(knife_position_track >= 0)
	for key in knife_attack.track_get_key_count(knife_position_track):
		if abs(knife_attack.track_get_key_time(knife_position_track, key) - 0.0833) < .001:
			knife_attack.track_set_key_value(knife_position_track, key, Vector2(-59.525, -15.25))
	assert(ResourceSaver.save(knife, KNIFE_LIBRARY) == OK)
	assert(ResourceSaver.set_uid(KNIFE_LIBRARY, knife_uid) == OK)

	var lion_uid := ResourceLoader.get_resource_uid(LION_LIBRARY)
	var lion: AnimationLibrary = load(LION_LIBRARY)
	var attack := lion.get_animation(&"attack_1")
	_set_shape_keys(attack, {
		0.0833: Vector2(171.5, 164.0),
		0.1667: Vector2(109.41666498, 108.113640705),
	})
	var position_track := attack.find_track(NodePath("m_HitBoxRoot/m_HitBox/CollisionShape2D:position"), Animation.TYPE_VALUE)
	assert(position_track >= 0)
	for key in attack.track_get_key_count(position_track):
		var time := snappedf(attack.track_get_key_time(position_track, key), 0.0001)
		if time == 0.0833:
			attack.track_set_key_value(position_track, key, Vector2(-52.65, -0.35))
		elif time == 0.1667:
			attack.track_set_key_value(position_track, key, Vector2(-63.2, 24.9500001))
	assert(ResourceSaver.save(lion, LION_LIBRARY) == OK)
	assert(ResourceSaver.set_uid(LION_LIBRARY, lion_uid) == OK)

	_hero_attack_geometry(source.hero_hit_probes)
	print("ATTACK GEOMETRY AUTHORING PASS: Wukong attack 3, source combo rectangles, knife/lion and authored lightning phases; art/events/windows preserved")
	quit()
