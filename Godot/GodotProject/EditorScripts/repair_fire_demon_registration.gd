extends SceneTree
## Register detached effect visuals in the entity physics-root coordinate system.
## Supply the verified skill exporter manifest after --. Authored timing and shapes are preserved.

const ENTITY_ROOT := "res://TheGame/Entitys/Heroes/Wukong/"
const ANIMATION_ROOT := "res://TheGame/Entitys/Animations/Heroes/Wukong/"
const SOURCE_ORIGIN := Vector2(0, 28)

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	assert(args.size() == 1, "Pass the verified skill exporter manifest")
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	assert(source.source_sha256 == "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7")
	assert(Vector2(source.actor_origin[0], source.actor_origin[1]) == SOURCE_ORIGIN)
	_normalize_hero()
	for entry in source.detached:
		if entry.name != "fire_demon_prelude":
			_normalize_effect(entry)
	print("WUKONG ACTION ORIGIN PASS: detached effects keep the source origin in the sprite offset; scaled visuals do not move the physics root")
	quit()

## Existing atlas cells contain the historical +28Y canvas translation.
## Correct every native layer together so the actor root really is Flash Role1.x/y.
func _normalize_hero() -> void:
	var path := ENTITY_ROOT.path_join("wukong_entity.tscn")
	var hero: Node2D = load(path).instantiate()
	for node in ["m_Body", "m_Weapon", "m_EffectRoot", "m_HitBoxRoot"]:
		hero.get_node(node).position = -SOURCE_ORIGIN
	for node in ["CollisionShape2D", "m_HurtBox/CollisionShape2D"]:
		hero.get_node(node).position = Vector2(0.4, -0.05)
	_save_scene(hero, path)
	hero.free()

## Preserve source timing and collision shapes while removing the duplicated actor-origin translation.
func _normalize_effect(reference: Dictionary) -> void:
	var library_path: String = ANIMATION_ROOT.path_join("wukong_%s_library.tres" % reference.name)
	var library: AnimationLibrary = load(library_path)
	var library_uid := ResourceLoader.get_resource_uid(library_path)
	for name in library.get_animation_list():
		var clip := library.get_animation(name)
		var track := clip.find_track(^"Origin/Visual/HitBox/CollisionShape2D:position", Animation.TYPE_VALUE)
		assert(track >= 0)
		for key in clip.track_get_key_count(track):
			var frame := roundi(clip.track_get_key_time(track, key) * 24)
			var bounds: Variant = reference.hitboxes[frame] if frame < reference.hitboxes.size() else null
			if bounds == null:
				continue
			var center := Vector2(bounds.position[0], bounds.position[1])
			var current: Vector2 = clip.track_get_key_value(track, key)
			if reference.name == "fire_demon_landing":
				# Rotated children of a nonuniformly scaled Visual produce shear;
				# use source-image rectangles that remain exact in native physics.
				var shapes := clip.find_track(^"Origin/Visual/HitBox/CollisionShape2D:shape", Animation.TYPE_VALUE)
				assert(clip.track_get_key_count(shapes) == clip.track_get_key_count(track))
				var shape: RectangleShape2D = clip.track_get_key_value(shapes, key)
				shape.size = Vector2(bounds.size[0], bounds.size[1])
			else:
				assert(current.distance_to(center) < .001 or current.distance_to(center + SOURCE_ORIGIN) < .001,
					"Effect geometry has independent edits: %s frame %d" % [reference.name, frame])
			clip.track_set_key_value(track, key, center)
		if reference.name == "fire_demon_landing":
			var rotations := clip.find_track(^"Origin/Visual/HitBox/CollisionShape2D:rotation", Animation.TYPE_VALUE)
			assert(rotations >= 0)
			for key in range(clip.track_get_key_count(rotations) - 1, 0, -1):
				clip.track_remove_key(rotations, key)
			clip.track_set_key_value(rotations, 0, 0.0)
	assert(ResourceSaver.save(library, library_path) == OK)
	if library_uid != ResourceUID.INVALID_ID:
		assert(ResourceSaver.set_uid(library_path, library_uid) == OK)
	var scene_path: String = ENTITY_ROOT.path_join("wukong_%s_effect.tscn" % reference.name)
	var scene: Node2D = load(scene_path).instantiate()
	var origin: Node2D = scene.get_node("Origin")
	origin.position = Vector2.ZERO
	var visual: Node2D = scene.get_node("Origin/Visual")
	visual.position = Vector2.ZERO
	visual.get_node("Sprite").offset = Vector2(reference.offset[0], reference.offset[1])
	if reference.name == "fire_demon_landing":
		visual.scale = Vector2(1.4, 2)
	_save_scene(scene, scene_path)
	scene.free()

## Native packing retains node ownership; preserve external UIDs even with a cold CLI cache.
func _save_scene(scene: Node, path: String) -> void:
	var original := FileAccess.get_file_as_string(path).split("\n")
	var uid := ResourceLoader.get_resource_uid(path)
	var packed := PackedScene.new()
	assert(packed.pack(scene) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	if uid != ResourceUID.INVALID_ID:
		assert(ResourceSaver.set_uid(path, uid) == OK)
	var lines := FileAccess.get_file_as_string(path).split("\n")
	var pattern := RegEx.new()
	assert(pattern.compile(' uid="([^"]+)".* path="([^"]+)"') == OK)
	for previous in original:
		if not previous.begins_with("[ext_resource "):
			continue
		var matched := pattern.search(previous)
		if matched == null:
			continue
		var token := ' path="%s"' % matched.get_string(2)
		for index in lines.size():
			if lines[index].begins_with("[ext_resource ") and not lines[index].contains(" uid=") and lines[index].contains(token):
				lines[index] = lines[index].replace(token, ' uid="%s"%s' % [matched.get_string(1), token])
	var file := FileAccess.open(path, FileAccess.WRITE)
	assert(file != null)
	file.store_string("\n".join(lines))
	file.close()
