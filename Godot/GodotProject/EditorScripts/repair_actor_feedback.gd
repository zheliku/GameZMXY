extends SceneTree
## Assign attacker impact/victim voice and remove duplicate hurt animation sound keys.

func _initialize() -> void:
	call_deferred("_repair")

func _repair() -> void:
	var scenes := ["res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn"]
	for name in ["huaguoshan_monkey", "demon_monkey", "gorilla", "cave_brute", "knife_demon"]:
		scenes.append("res://TheGame/Entitys/Enemies/Monsters/Ground/%s/%s_entity.tscn" % [name, name])
	scenes.append("res://TheGame/Entitys/Enemies/Monsters/Sky/bat/bat_entity.tscn")
	for name in ["macaque_king", "tamarin_king"]:
		scenes.append("res://TheGame/Entitys/Enemies/Bosses/%s/%s_entity.tscn" % ["zm1_02_macaque_king" if name == "macaque_king" else "zm1_03_tamarin_king", name])
	for path in scenes:
		var original_uid := ResourceLoader.get_resource_uid(path)
		var actor: Node = load(path).instantiate(PackedScene.GEN_EDIT_STATE_MAIN)
		var hero := path.contains("wukong")
		actor.set("_hurtSoundId", 4 if hero else 0)
		actor.set("_hitSoundId", 6 if hero else 0)
		if hero: actor.z_index = 1
		var packed := PackedScene.new()
		assert(packed.pack(actor) == OK)
		assert(ResourceSaver.save(packed, path) == OK)
		assert(ResourceSaver.set_uid(path, original_uid) == OK)
		actor.free()
	var path := "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres"
	var original_uid := ResourceLoader.get_resource_uid(path)
	var library: AnimationLibrary = load(path)
	var hurt := library.get_animation("hurt")
	for track in range(hurt.get_track_count() - 1, -1, -1):
		if hurt.track_get_type(track) != Animation.TYPE_METHOD:
			continue
		for key in range(hurt.track_get_key_count(track) - 1, -1, -1):
			if hurt.method_track_get_name(track, key) == &"PlaySoundAnimEvent":
				hurt.track_remove_key(track, key)
		if hurt.track_get_key_count(track) == 0:
			hurt.remove_track(track)
	assert(ResourceSaver.save(library, path) == OK)
	assert(ResourceSaver.set_uid(path, original_uid) == OK)
	print("FEEDBACK RESOURCES PASS: impact belongs to attacker; victim voice/death retained; hero draws in front.")
	quit()
