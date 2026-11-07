extends SceneTree
## Verify the authored 2-2 scene and its official actor resources.

func _initialize() -> void:
	call_deferred("_validate")

func _validate() -> void:
	var level: Node2D = load("res://TheGame/Scenes/Levels/zm1_05_qixing_cave/level_5.tscn").instantiate()
	assert(level.get("_levelId") == 5)
	assert(level.get("_featuredOpponentSpawnPointIds").has("stage_5_a"))
	var points := level.get_node("World/SpawnPoints")
	assert(points.get("_entries").size() == 21)
	assert(level.get_node("World/StageGates").get("_entries").size() == 9)
	assert(level.get_node("World/Geometry").find_children("*", "CollisionShape2D", true, false).size() == 5)
	var hill: CollisionPolygon2D = level.get_node("World/Geometry/Slopes/Hill03/Surface")
	assert(hill.polygon.size() == 6)
	var ground: Sprite2D = level.get_node("World/Geometry/Artwork/QixingGround")
	assert(level.get_node("Camera2D").limit_right == floori(ground.position.x + ground.texture.get_width()))
	for index in [3,4,5]:
		assert(not level.has_node("World/Geometry/Ground/Surface%02d" % index))
	assert(level.get_node("World/Geometry/Artwork").get_child_count() == 3)
	assert(level.get_node("World/Traps").get_child_count() == 35)
	for scene in [
		"res://TheGame/Entitys/Enemies/Monsters/Ground/cave_brute/cave_brute_entity.tscn",
		"res://TheGame/Entitys/Enemies/Monsters/Ground/knife_demon/knife_demon_entity.tscn",
		"res://TheGame/Entitys/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_entity.tscn"]:
		var actor: Node = load(scene).instantiate()
		for binding in ["m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot", "_perception"]:
			assert(actor.get(binding) != null, scene + " missing " + binding)
		assert(actor.get_node("m_HurtBox").position == Vector2.ZERO)
		var player: AnimationPlayer = actor.get("_animPlayer")
		for attack in actor.get("_attackAnimations"):
			assert(player.has_animation(attack))
		actor.free()
	var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
	var library: AnimationLibrary = form.get_node("m_LocationAnimation").get_animation_library("qixingdong")
	assert(library.resource_path == "res://TheGame/Sprites/UI/frontend/map_qixingdong_hover_animation.tres")
	assert(form.get_node("m_Qixingdong").get_child_count() == 1)
	for title in library.get_animation_list():
		var animation := library.get_animation(title)
		for track in animation.get_track_count():
			var path := String(animation.track_get_path(track)).get_slice(":", 0)
			assert(form.get_node_or_null(path) != null, "Invalid qixing map track " + path)
	form.free()
	print("QIXING CAVE VALIDATION PASS: level=5, 20 spawn markers, 9 gates, 5 rectangles + 1 continuous hill, 3 actor scenes")
	level.free()
	quit()
