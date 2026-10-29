extends SceneTree
## Check source Wukong shapes and preserve the separately authored enemy approximations.

const CASES := [
	["res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/knife_demon_library.tres", "attack_1", [0.0833, 0.1667], [Vector2(110, 120), Vector2(95, 60)]],
	["res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_library.tres", "attack_1", [0.0833, 0.1667], [Vector2(171.5, 164.0), Vector2(109.41666498, 108.113640705)]],
]

func _value_at(animation: Animation, property: String, time: float):
	var track := animation.find_track(NodePath(property), Animation.TYPE_VALUE)
	assert(track >= 0)
	var result = null
	for key in animation.track_get_key_count(track):
		if animation.track_get_key_time(track, key) > time + 0.0001:
			break
		result = animation.track_get_key_value(track, key)
	return result

func _check_bounds(animation: Animation, path: String, time: float, expected: Rect2) -> void:
	var shape: Shape2D = _value_at(animation, path + ":shape", time)
	assert(shape is RectangleShape2D and (shape as RectangleShape2D).size.is_equal_approx(expected.size), "Geometry shape mismatch at %.4fs" % time)
	assert((_value_at(animation, path + ":position", time) as Vector2).is_equal_approx(expected.get_center()), "Geometry position mismatch at %.4fs" % time)
	assert(is_zero_approx(float(_value_at(animation, path + ":rotation", time))))

func _check_detached_source(source: Dictionary, name: String) -> void:
	var entry: Dictionary
	for candidate in source.detached:
		if candidate.name == name:
			entry = candidate
			break
	assert(not entry.is_empty(), "Missing detached source entry: " + name)
	var scene: Node = load("res://TheGame/Entitys/Heroes/Wukong/wukong_%s_effect.tscn" % name).instantiate()
	var library: AnimationLibrary = scene.get_node("AnimationPlayer").get_animation_library(&"")
	var animation := library.get_animation(&"play")
	var path := "Origin/Visual/HitBox/CollisionShape2D"
	var origin: Node2D = scene.get_node("Origin")
	var sprite: AnimatedSprite2D = scene.get_node("Origin/Visual/Sprite")
	assert(origin.position.is_zero_approx(), "%s effect origin must stay at the actor root" % name)
	assert(sprite.offset.is_equal_approx(Vector2(entry.offset[0], entry.offset[1])),
		"%s sprite offset no longer matches the exported source origin" % name)
	for frame in int(entry.count):
		var time := frame / 24.0
		var reference = entry.hitboxes[frame]
		var disabled := bool(_value_at(animation, path + ":disabled", time))
		if reference == null:
			assert(disabled, "%s blank frame remains active: %d" % [name, frame + 1])
			continue
		assert(not disabled, "%s active frame is disabled: %d" % [name, frame + 1])
		var shape: Shape2D = _value_at(animation, path + ":shape", time)
		var position: Vector2 = _value_at(animation, path + ":position", time)
		var rotation: float = _value_at(animation, path + ":rotation", time)
		var expected_size := Vector2(reference.size[0], reference.size[1])
		var expected_position := Vector2(reference.position[0], reference.position[1])
		assert(shape is RectangleShape2D and (shape as RectangleShape2D).size.is_equal_approx(expected_size), "%s shape mismatch at frame %d" % [name, frame + 1])
		assert(position.is_equal_approx(expected_position), "%s position mismatch at frame %d" % [name, frame + 1])
		# Landing uses the source image rectangle without child rotation, avoiding shear under its nonuniform scale.
		var expected_rotation := 0.0 if name == "fire_demon_landing" else float(reference.rotation)
		assert(is_equal_approx(rotation, expected_rotation), "%s rotation mismatch at frame %d" % [name, frame + 1])
	scene.free()

func _initialize() -> void:
	create_timer(60).timeout.connect(func(): quit(1))
	var args := OS.get_cmdline_user_args()
	assert(not args.is_empty(), "Pass the verified skill exporter manifest after --")
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	assert(source.source_sha256 == "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7")
	var probe_checks := 0
	for file in ["wukong_anim_library.tres", "wukong_skill_library.tres"]:
		var library: AnimationLibrary = load("res://TheGame/Entitys/Animations/Heroes/Wukong/" + file)
		for action in source.hero_hit_probes:
			if not library.has_animation(action):
				continue
			var animation := library.get_animation(action)
			var frames: Array = source.hero_hit_probes[action]
			for frame in frames.size():
				var probe = frames[frame]
				if probe == null:
					continue
				var path := "m_HitBoxRoot/m_HitBox/CollisionShape2D"
				var time := frame / 24.0
				assert(not _value_at(animation, path + ":disabled", time), "Official active probe is disabled")
				var shape: RectangleShape2D = _value_at(animation, path + ":shape", time)
				assert(shape.size.x >= 14 and shape.size.y >= 14)
				# User-authored normal attacks intentionally reduce the invisible source margin.
				# Their visual coverage and real physics are checked by validate_official_wukong_attacks.
				if String(action).begins_with("attack_"):
					continue
				var center: Vector2 = _value_at(animation, path + ":position", time)
				var rotation: float = _value_at(animation, path + ":rotation", time)
				var inverse := Transform2D(rotation, center).affine_inverse()
				var reference := Transform2D(probe.rotation, Vector2(probe.position[0], probe.position[1]))
				var half := Vector2(probe.size[0], probe.size[1]) * .5
				for corner in [Vector2(-half.x,-half.y),Vector2(half.x,-half.y),half,Vector2(-half.x,half.y)]:
					assert(shape.get_rect().grow(.001).has_point(inverse * (reference * corner)), "Official probe is smaller than the authored hero range: %s frame %d" % [action,frame+1])
					probe_checks += 1
	print("HERO SKILL PROBE PASS: ", probe_checks, " official source corners; normal attack active windows retained")
	var combo: AnimationLibrary = load("res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_combo_library.tres")
	if args.has("--normal-attacks-only"):
		print("NORMAL-ATTACK SOURCE PASS: official probe corners and active windows retained")
		quit()
		return
	for frame in 42:
		var bounds: Array = source.fire_demon_combo_bounds[frame]
		_check_bounds(combo.get_animation(&"play"), "Origin/Visual/HitBox/CollisionShape2D", frame / 24.0, Rect2(bounds[0], bounds[1], bounds[2], bounds[3]))
	if args.has("--fire-demon-only"):
		_check_detached_source(source, "fire_demon_descent")
		_check_detached_source(source, "fire_demon_landing")
		print("FIRE-DEMON SOURCE PASS: all 42 active combo frames retain official bounds")
		_validate_combo_physics.call_deferred(source)
		return
	var tamarin: AnimationLibrary = load("res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_library.tres")
	# Project-authored lightning approximations exclude clouds; these are not Flash probe bounds.
	for phase in [
		[41, Rect2(-210, -147.5, 400, 210)],
		[47, Rect2(-140, -135, 280, 185)],
		[51, Rect2(-140, -135, 280, 185)],
		[61, Rect2(-220, -195, 380, 150)],
		[62, Rect2(-240, -220, 520, 280)]]:
		_check_bounds(tamarin.get_animation(&"attack_2"), "m_HitBoxRoot/m_HitBox/CollisionShape2D", (phase[0] - 1) / 24.0, phase[1])
	for item in CASES:
		var library: AnimationLibrary = load(item[0])
		var animation: Animation = library.get_animation(item[1])
		var track := animation.find_track(NodePath("m_HitBoxRoot/m_HitBox/CollisionShape2D:shape"), Animation.TYPE_VALUE)
		assert(track >= 0)
		for index in item[2].size():
			var key := -1
			for candidate in animation.track_get_key_count(track):
				if abs(animation.track_get_key_time(track, candidate) - item[2][index]) < .001:
					key = candidate
					break
			assert(key >= 0)
			var shape: Shape2D = animation.track_get_key_value(track, key)
			assert(shape is RectangleShape2D)
			assert((shape as RectangleShape2D).size.is_equal_approx(item[3][index]), "Authored enemy size changed")
	_check_detached_source(source, "fire_demon_descent")
	_check_detached_source(source, "fire_demon_landing")
	print("OFFICIAL ATTACK VALIDATION PASS: hero active windows, 42 combo source frames, descent/landing, knife/lion; five authored thunder phases")
	_validate_combo_physics.call_deferred(source)

## Query the actual animated Area at source rectangle edges and outside it, in both directions.
func _validate_combo_physics(source: Dictionary) -> void:
	root.size = Vector2i(680, 440)
	var effect: Node2D = load("res://TheGame/Entitys/Heroes/Wukong/wukong_fire_demon_combo_effect.tscn").instantiate()
	# This fixture owns geometry only. Run gameplay callbacks through the full UI smoke flow.
	effect.set_script(null)
	var player: AnimationPlayer = effect.get_node("AnimationPlayer")
	var library: AnimationLibrary = player.get_animation_library(&"").duplicate(true)
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		for track in animation.get_track_count():
			if animation.track_get_type(track) == Animation.TYPE_METHOD:
				animation.track_set_enabled(track, false)
	player.remove_animation_library(&"")
	player.add_animation_library(&"", library)
	root.add_child(effect)
	effect.position = Vector2(340, 180)
	effect.set_physics_process(false)
	var visual: Node2D = effect.get_node("Origin/Visual")
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var area: Area2D = visual.get_node("HitBox")
	var hit: CollisionShape2D = area.get_node("CollisionShape2D")
	var outline := Line2D.new()
	outline.default_color = Color.GREEN
	outline.width = 1
	outline.closed = true
	hit.add_child(outline)
	var checked := 0
	for direction in [-1, 1]:
		visual.scale = Vector2(direction, 1)
		for frame in 44:
			player.stop()
			player.play(&"play")
			player.advance(0)
			player.seek(frame / 24.0 + .00001, true, true)
			player.pause()
			await physics_frame
			await process_frame
			assert(hit.disabled == (frame >= 42), "Combo enable window changed: frame %d direction %d disabled=%s" % [frame + 1, direction, hit.disabled])
			if frame >= 42:
				continue
			var values: Array = source.fire_demon_combo_bounds[frame]
			var bounds := Rect2(values[0], values[1], values[2], values[3])
			for point in [bounds.get_center(), bounds.position + Vector2(1, 1), bounds.end - Vector2(1, 1),
				Vector2(bounds.position.x - 2, bounds.get_center().y), Vector2(bounds.end.x + 2, bounds.get_center().y),
				Vector2(bounds.get_center().x, bounds.position.y - 2), Vector2(bounds.get_center().x, bounds.end.y + 2)]:
				var query := PhysicsPointQueryParameters2D.new()
				query.position = visual.global_transform * point
				query.collision_mask = 16
				query.collide_with_areas = true
				query.collide_with_bodies = false
				var actual := effect.get_world_2d().direct_space_state.intersect_point(query).any(func(result): return result.collider == area)
				assert(actual == bounds.has_point(point), "Combo frame %d direction %d source edge mismatch: %s" % [frame + 1, direction, point])
				checked += 1
			if OS.get_cmdline_user_args().has("--snapshots") and frame in [0, 6, 12, 18, 30, 40]:
				var rectangle := hit.shape.get_rect()
				outline.points = PackedVector2Array([rectangle.position, rectangle.position + Vector2(rectangle.size.x, 0), rectangle.end, rectangle.position + Vector2(0, rectangle.size.y)])
				await RenderingServer.frame_post_draw
				var path := "res://.godot/validation/bat_sonic_review/combo_%s_%02d.png" % [direction, frame]
				assert(root.get_texture().get_image().save_png(path) == OK)
	effect.queue_free()
	print("COMBO PHYSICS PASS: ", checked, " real Area point queries; 44 source frames x both directions; blank tail disabled")
	if not OS.get_cmdline_user_args().has("--fire-demon-only"):
		await _validate_cloud_physics()
	quit()

## Check growth, fading and mirrored physical boundaries against the actual native cloud frames.
func _validate_cloud_physics() -> void:
	var effect: Node2D = load("res://TheGame/Entitys/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_effect.tscn").instantiate()
	effect.set_script(null)
	root.add_child(effect)
	effect.position = Vector2(340, 180)
	var player: AnimationPlayer = effect.get_node("AnimationPlayer")
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var sprite: AnimatedSprite2D = effect.get_node("Sprite")
	sprite.stop()
	var area: Area2D = effect.get_node("HitBox")
	var hit: CollisionShape2D = area.get_node("CollisionShape2D")
	var checks := 0
	var blanks := 0
	for direction in [-1, 1]:
		effect.scale = Vector2(direction, 1)
		for frame in sprite.sprite_frames.get_frame_count(&"play"):
			player.stop()
			player.play(&"play")
			player.advance(0)
			player.seek(frame / 24.0 + .00001, true, true)
			player.pause()
			var texture := sprite.sprite_frames.get_frame_texture(&"play", frame)
			var image := texture.get_image()
			var used := image.get_used_rect()
			var visible := Rect2i()
			for y in range(used.position.y, used.end.y):
				for x in range(used.position.x, used.end.x):
					if image.get_pixel(x, y).a >= .1:
						var pixel := Rect2i(x, y, 1, 1)
						visible = visible.merge(pixel) if visible.has_area() else pixel
			await physics_frame
			await process_frame
			assert(hit.disabled == not visible.has_area(), "Cloud phase enable mismatch: %d" % frame)
			if not visible.has_area():
				blanks += 1
				continue
			var bounds := Rect2(Vector2(visible.position) - texture.get_size() * .5 + sprite.offset, Vector2(visible.size))
			var actual := Rect2(hit.position - hit.shape.size * .5, hit.shape.size)
			assert(actual.grow(.001).encloses(bounds), "Cloud shrinks away from active visual range: %d" % frame)
			assert(bounds.position.x - actual.position.x <= 6.001 and bounds.position.y - actual.position.y <= 6.001 and actual.end.x - bounds.end.x <= 6.001 and actual.end.y - bounds.end.y <= 6.001, "Cloud retains too much outside air: %d" % frame)
			for point in [bounds.get_center(), bounds.position + Vector2(.5, .5), bounds.end - Vector2(.5, .5),
				Vector2(actual.position.x - 2, actual.get_center().y), Vector2(actual.end.x + 2, actual.get_center().y)]:
				var query := PhysicsPointQueryParameters2D.new()
				query.position = effect.global_transform * point
				query.collision_mask = 64
				query.collide_with_areas = true
				query.collide_with_bodies = false
				var overlaps := effect.get_world_2d().direct_space_state.intersect_point(query).any(func(result): return result.collider == area)
				assert(overlaps == actual.has_point(point), "Cloud physical edge mismatch: frame %d direction %d" % [frame, direction])
				checks += 1
	effect.queue_free()
	print("CLOUD PHYSICS PASS: ", checks, " real Area queries; all 241 frames x both directions; ", blanks, " faint/blank samples disabled; <=6px outside allowance")
