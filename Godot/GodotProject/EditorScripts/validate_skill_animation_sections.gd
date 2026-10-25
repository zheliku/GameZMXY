extends SceneTree
## Exercise the actual C# animation entry with native method tracks; no gameplay test API.

class MethodRecorder extends Node:
	var events: Array[String] = []
	func record(method: String) -> void:
		events.append(method)

func _initialize() -> void:
	create_timer(15).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _run() -> void:
	var hero: CharacterBody2D = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
	var recorder := MethodRecorder.new()
	recorder.name = "MethodRecorder"
	hero.add_child(recorder)
	root.add_child(hero)
	var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
	var library := player.get_animation_library("skills").duplicate(true) as AnimationLibrary
	for name in library.get_animation_list():
		var clip := library.get_animation(name)
		for track in clip.get_track_count():
			if clip.track_get_type(track) != Animation.TYPE_METHOD: continue
			clip.track_set_path(track, NodePath("MethodRecorder"))
			for key in clip.track_get_key_count(track):
				var method: String = clip.method_track_get_name(track, key)
				clip.track_set_key_value(track, key, {"method": &"record", "args": [method]})
	player.remove_animation_library("skills")
	player.add_animation_library("skills", library)
	for iteration in 3:
		recorder.events.clear()
		hero.call("BeginSkillAnimation", "skills/fire_demon_slash", null)
		assert(is_zero_approx(player.current_animation_position) and not player.has_section())
		assert(recorder.events == ["PlaySoundAnimEvent", "BeginFireDemonAnimEvent"], str(recorder.events))
		player.advance(.15)
		assert(recorder.events.count("LaunchFireDemonAnimEvent") == 1)
		for marker in ["descent", "recovery"]:
			recorder.events.clear()
			if marker == "recovery": player.pause()
			hero.call("BeginSkillAnimation", "skills/fire_demon_slash", marker)
			var expected := "DescendFireDemonAnimEvent" if marker == "descent" else "RecoverFireDemonAnimEvent"
			assert(recorder.events == [expected], "Wrong stage methods: " + str(recorder.events))
			player.advance(1.0 / 120.0)
			assert(recorder.events == [expected], "Start method repeated: " + str(recorder.events))
		hero.call("EndSkillAnimation")
		assert(not player.has_section())
	for name in library.get_animation_list():
		recorder.events.clear()
		hero.call("BeginSkillAnimation", "skills/" + name, null)
		assert(is_zero_approx(player.current_animation_position) and not player.has_section())
		hero.call("EndSkillAnimation")
	print("SKILL SECTION PASS: actual C# entry; exact stage events once, no old startup/launch, pause/recovery/repeat, all five skills restart at zero.")
	quit()
