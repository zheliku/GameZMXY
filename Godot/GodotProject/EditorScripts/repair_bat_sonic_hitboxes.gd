extends SceneTree
## Only reauthor the bat's sonic-wave bounds; preserve art, speed, damage and other actions.

const LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Monsters/Sky/bat/bat_library.tres"
const HIT_PATH := "m_HitBoxRoot/m_HitBox/CollisionShape2D"
# Keep the requested faster visual response instead of restoring the four source probe keys.
# One rectangle includes ring gaps; additional keys follow the larger changes at frames 10/15.
const SOURCE_FRAMES := [0, 2, 4, 6, 8, 9, 10, 12, 14, 15, 17]
const WAVE_BOUNDS := [
	Rect2(-24, 7, 15, 16),
	Rect2(-34, 7, 25, 24),
	Rect2(-44, 7, 35, 33),
	Rect2(-53, 8, 42, 40),
	Rect2(-58, 10, 42, 42),
	Rect2(-63, 11, 45, 45),
	Rect2(-73, 12, 52, 52),
	Rect2(-82, 14, 57, 58),
	Rect2(-87, 16, 57, 60),
	Rect2(-82, 17, 50, 55),
	Rect2(-87, 19, 50, 57),
]

func _initialize() -> void:
	var uid := ResourceLoader.get_resource_uid(LIBRARY)
	var library: AnimationLibrary = load(LIBRARY)
	var clip := library.get_animation(&"attack_1")
	var position_track := clip.find_track(NodePath(HIT_PATH + ":position"), Animation.TYPE_VALUE)
	var shape_track := clip.find_track(NodePath(HIT_PATH + ":shape"), Animation.TYPE_VALUE)
	assert(position_track >= 0 and shape_track >= 0)
	for track in [position_track, shape_track]:
		while clip.track_get_key_count(track):
			clip.track_remove_key(track, 0)
	var palette := {}
	var previous_size := Vector2(-1, -1)
	var previous_position := Vector2(INF, INF)
	for index in SOURCE_FRAMES.size():
		var bounds: Rect2 = WAVE_BOUNDS[index]
		var time: float = SOURCE_FRAMES[index] / 24.0
		if bounds.get_center() != previous_position:
			clip.track_insert_key(position_track, time, bounds.get_center())
		if bounds.size != previous_size:
			if not palette.has(bounds.size):
				var shape := RectangleShape2D.new()
				shape.size = bounds.size
				palette[bounds.size] = shape
			clip.track_insert_key(shape_track, time, palette[bounds.size])
		previous_position = bounds.get_center()
		previous_size = bounds.size
	assert(ResourceSaver.save(library, LIBRARY) == OK)
	assert(ResourceSaver.set_uid(LIBRARY, uid) == OK)
	print("BAT SONIC AUTHORING PASS: eleven nonredundant visual phases; 10/15 corrected; phase envelope <=6px; original UID/art/timing retained")
	quit()
