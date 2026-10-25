extends SceneTree
## Exercise actual animated collision bounds for all twenty frames and both facings.

const WORK := "res://.godot/validation/bat_sonic_review"
const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D"
const SOURCE_FRAMES := [0, 2, 4, 6, 8, 9, 10, 12, 14, 15, 17]
# Registered ring bounds from the official 20-frame render; include faint leading rings.
# The stationary bat body is excluded. These are reference samples, not generated polygons.
const RINGS := [
	Rect2(-19, 7, 10, 12), Rect2(-24, 8, 13, 15), Rect2(-29, 9, 15, 18),
	Rect2(-34, 7, 25, 24), Rect2(-39, 8, 28, 27), Rect2(-44, 7, 35, 33),
	Rect2(-48, 8, 37, 36), Rect2(-53, 9, 39, 39), Rect2(-58, 10, 42, 42),
	Rect2(-63, 11, 45, 45), Rect2(-68, 12, 47, 48), Rect2(-73, 13, 50, 51),
	Rect2(-78, 14, 53, 54), Rect2(-82, 15, 55, 57), Rect2(-87, 16, 57, 60),
	Rect2(-78, 17, 46, 51), Rect2(-82, 18, 48, 54), Rect2(-87, 19, 50, 57),
	Rect2(-82, 20, 43, 52), Rect2(-87, 21, 46, 55),
]

func _initialize() -> void:
	create_timer(20).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _run() -> void:
	var view := SubViewport.new()
	view.size = Vector2i(240, 200)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var bat: CharacterBody2D = load("res://TheGame/Entitys/Enemies/Monsters/Sky/bat/bat_entity.tscn").instantiate()
	bat.position = Vector2(120, 60)
	view.add_child(bat)
	bat.set_physics_process(false)
	var player: AnimationPlayer = bat.get_node("m_AnimPlayer")
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var clip := player.get_animation(&"attack_1")
	assert(is_equal_approx(clip.length, 20.0 / 24.0))
	assert(clip.track_get_key_count(clip.find_track(NodePath("m_Body:frame"), Animation.TYPE_VALUE)) == 20)
	var positions := clip.find_track(NodePath(HIT_PATH + ":position"), Animation.TYPE_VALUE)
	var shapes := clip.find_track(NodePath(HIT_PATH + ":shape"), Animation.TYPE_VALUE)
	assert(clip.track_get_key_count(positions) == SOURCE_FRAMES.size())
	assert(clip.track_get_key_count(shapes) == SOURCE_FRAMES.size())
	for index in SOURCE_FRAMES.size():
		var expected_time: float = float(SOURCE_FRAMES[index]) / 24.0
		assert(is_equal_approx(clip.track_get_key_time(positions, index), expected_time))
	var shape: CollisionShape2D = bat.get_node(HIT_PATH)
	var outline := Line2D.new()
	outline.default_color = Color.GREEN
	outline.width = 1
	outline.closed = true
	shape.add_child(outline)
	DirAccess.make_dir_recursive_absolute(WORK)
	for direction in [-1, 1]:
		bat.call("SetFacing", direction)
		for frame in 20:
			player.play(&"attack_1")
			player.seek(frame / 24.0 + .000001, true, true)
			player.pause()
			assert(not shape.disabled)
			assert(bat.get_node("m_Body").frame == frame)
			var bounds := shape.shape.get_rect()
			var actual := Rect2(shape.position + bounds.position, bounds.size)
			var expected: Rect2 = RINGS[frame]
			assert(actual.encloses(expected), "Sonic wave falls outside the phase rectangle: frame %d" % (frame + 1))
			assert(expected.position.x - actual.position.x <= 6 and expected.position.y - actual.position.y <= 6 and actual.end.x - expected.end.x <= 6 and actual.end.y - expected.end.y <= 6, "Sonic wave contains excess outside air")
			outline.points = PackedVector2Array([bounds.position, bounds.position + Vector2(bounds.size.x, 0),
				bounds.end, bounds.position + Vector2(0, bounds.size.y)])
			if not DisplayServer.get_name() == "headless":
				await process_frame
				await RenderingServer.frame_post_draw
				assert(view.get_texture().get_image().save_png(WORK.path_join("sonic_%s_%02d.png" % [direction, frame])) == OK)
	player.play(&"idle")
	player.advance(0)
	assert(shape.disabled)
	print("BAT SONIC GEOMETRY PASS: 20 source ring bounds; eleven nonredundant phases; frames 10/15 exact; no ring clipping; <=6px phase allowance; both facings; idle clears collision")
	quit()
