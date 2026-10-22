extends SceneTree
## Apply damage feedback ownership and remove its duplicate hurt animation key.

func _initialize() -> void:
	call_deferred("_repair")

func _repair() -> void:
	var scenes := ["res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn"]
	for name in ["huaguoshan_monkey", "demon_monkey", "gorilla"]:
		scenes.append("res://TheGame/Entitys/Enemies/Monsters/%s_entity.tscn" % name)
	for name in ["macaque_king", "tamarin_king"]:
        scenes.append("res://TheGame/Entitys/Enemies/Bosses/%s/%s_entity.tscn" % ["zm1_02_macaque_king" if name == "macaque_king" else "zm1_03_tamarin_king", name])
	for path in scenes:
		var actor: Node = load(path).instantiate()
		actor.set("m_DamageSoundId", 4 if path.contains("wukong") else 6)
		var packed := PackedScene.new()
		assert(packed.pack(actor) == OK)
		assert(ResourceSaver.save(packed, path) == OK)
		actor.free()
	var path := "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres"
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
	print("FEEDBACK RESOURCES PASS: six receivers; hurt audio belongs to successful damage, death/attack methods retained.")
	quit()
