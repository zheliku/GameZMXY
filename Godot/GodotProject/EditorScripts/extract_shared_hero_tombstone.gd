extends SceneTree
## Move the existing 78-frame presentation out of Wukong's body library.

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var source_path := "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres"
	var target_path := "res://TheGame/Entitys/Animations/Heroes/hero_tombstone_library.tres"
	var source: AnimationLibrary = load(source_path)
	var death := source.get_animation("death")
	var old_track := death.find_track("m_DeathTombstone:frame", Animation.TYPE_VALUE)
	if old_track >= 0:
		var animation := Animation.new()
		animation.length = death.length
		var track := animation.add_track(Animation.TYPE_VALUE)
		animation.track_set_path(track, "m_Sprite:frame")
		animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
		for key in death.track_get_key_count(old_track):
			animation.track_insert_key(track, death.track_get_key_time(old_track, key), death.track_get_key_value(old_track, key))
		var library := AnimationLibrary.new()
		library.add_animation("play", animation)
		assert(ResourceSaver.save(library, target_path) == OK)
	else:
		assert(ResourceLoader.exists(target_path), "Missing original or extracted tombstone frames")
	for name in source.get_animation_list():
		var animation := source.get_animation(name)
		for track in range(animation.get_track_count() - 1, -1, -1):
			var path := String(animation.track_get_path(track))
			if path.begins_with("m_DeathTombstone:") or path.begins_with("m_TombstoneGround:"):
				animation.remove_track(track)
	death.length = 1.0 / 24.0
	assert(ResourceSaver.save(source, source_path) == OK)
	print("SHARED HERO TOMBSTONE AUTHOR PASS: independent 78-frame animation; body death retains only hide/sound/collision")
	quit(0)
