extends SceneTree
## Complete native reset tracks and keep a pooled blade inactive until its animation starts.

func _initialize() -> void:
	assert(Engine.is_editor_hint())
	var added := 0
	for name in ["huaguoshan_monkey", "demon_monkey", "gorilla"]:
		var path := "res://TheGame/Entitys/Animations/Enemies/Monsters/Ground/%s/%s_anim_library.tres" % [name,name]
		var library: AnimationLibrary = load(path)
		var count := complete_rotation_tracks(library)
		if count > 0:
			var uid := ResourceLoader.get_resource_uid(path)
			assert(ResourceSaver.save(library,path) == OK)
			assert(ResourceSaver.set_uid(path,uid) == OK)
		added += count
	var path := "res://TheGame/Entitys/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_effect.tscn"
	var blade: Node2D = load(path).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	blade.get_node("HitBox/CollisionShape2D").disabled = true
	var uid := ResourceLoader.get_resource_uid(path)
	var packed := PackedScene.new()
	assert(packed.pack(blade) == OK)
	assert(ResourceSaver.save(packed,path) == OK)
	assert(ResourceSaver.set_uid(path,uid) == OK)
	blade.free()
	print("ANIMATION DEFAULTS PASS: ",added," missing rotation resets added; blade starts disabled")
	quit()

static func complete_rotation_tracks(library: AnimationLibrary) -> int:
	var added := 0
	var path := NodePath("m_HitBoxRoot/m_HitBox/CollisionShape2D:rotation")
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		if animation.find_track(path,Animation.TYPE_VALUE) >= 0:
			continue
		var track := animation.add_track(Animation.TYPE_VALUE)
		animation.track_set_path(track,path)
		animation.value_track_set_update_mode(track,Animation.UPDATE_DISCRETE)
		animation.track_insert_key(track,0,0.0)
		added += 1
	return added
