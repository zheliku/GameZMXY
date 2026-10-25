extends SceneTree

## 原版角色时间轴与独立九连同时推进；阶段标记与原生方法键精确同刻。
func _initialize() -> void:
	var path := "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_skill_library.tres"
	assert(ResourceSaver.set_uid("res://TheGame/Entitys/Heroes/Wukong/wukong_fire_demon_descent_effect.tscn",
		ResourceUID.text_to_id("uid://b5gc20phhdixl")) == OK)
	var library: AnimationLibrary = load(path)
	var clip: Animation = library.get_animation(&"fire_demon_slash")
	var found := 0
	for track in clip.get_track_count():
		if clip.track_get_type(track) != Animation.TYPE_METHOD:
			continue
		for key in clip.track_get_key_count(track):
			var call: Dictionary = clip.track_get_key_value(track, key)
			if call.method in [&"DescendFireDemonAnimEvent", &"RecoverFireDemonAnimEvent"]:
				var marker := &"descent" if call.method == &"DescendFireDemonAnimEvent" else &"recovery"
				var color := clip.get_marker_color(marker)
				clip.remove_marker(marker)
				clip.add_marker(marker, clip.track_get_key_time(track, key))
				clip.set_marker_color(marker, color)
			if call.method != &"SearchFireDemonTargetAnimEvent":
				continue
			assert(call.args.size() == 3 or call.args.size() == 4)
			if call.args.size() == 4:
				call.args = call.args.slice(0, 3)
				clip.track_set_key_value(track, key, call)
			found += 1
	assert(found == 1)
	assert(ResourceSaver.save(library, path) == OK)
	assert(ResourceSaver.set_uid(path, ResourceUID.text_to_id("uid://j3uaow5vlljx")) == OK)
	print("FIRE-DEMON TIMING PASS: search remains parallel; markers exactly match method key times")
	quit()
