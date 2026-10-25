extends SceneTree
## Remove the authoring-time root scale overrides so SkillEffectNode can apply the
## source role mirror to combo/landing effects. Prelude/descent keep their own source
## orientation through their scene roots.

const LIBRARIES := [
	"res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_combo_library.tres",
	"res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_landing_library.tres"
]

func _initialize() -> void:
	for path in LIBRARIES:
		var uid := ResourceLoader.get_resource_uid(path)
		var library: AnimationLibrary = load(path)
		for name in library.get_animation_list():
			var animation := library.get_animation(name)
			var track := animation.find_track(NodePath(".:scale"), Animation.TYPE_VALUE)
			while track >= 0:
				animation.remove_track(track)
				track = animation.find_track(NodePath(".:scale"), Animation.TYPE_VALUE)
		assert(ResourceSaver.save(library, path) == OK)
		assert(ResourceSaver.set_uid(path, uid) == OK)
	print("FIRE-DEMON ORIENTATION PASS: combo/landing use source role orientation, normal and musou")
	quit()
