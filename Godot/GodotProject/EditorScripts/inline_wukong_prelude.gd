extends SceneTree
## 将只有播放职责的火魔前奏并入悟空；独立伤害阶段仍保留节点池和场景。

func _initialize() -> void:
	assert(Engine.is_editor_hint())
	var path := "res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn"
	var root: Node = load(path).instantiate()
	if root.has_node("m_FireDemonPrelude"):
		root.free()
		print("PRELUDE PASS: already inline")
		quit()
		return
	var sprite := AnimatedSprite2D.new()
	sprite.name = "m_FireDemonPrelude"
	sprite.top_level = true
	sprite.visible = false
	sprite.sprite_frames = load("res://TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_prelude_frames.tres")
	sprite.animation = &"play"
	sprite.offset = Vector2(52.15, -89.6)
	root.add_child(sprite)
	sprite.owner = root
	sprite.connect("animation_finished", Callable(sprite, "hide"), CONNECT_PERSIST)
	root.set("m_FireDemonPrelude", sprite)
	var library: AnimationLibrary = root.get_node("m_AnimPlayer").get_animation_library("skills")
	var animation := library.get_animation("fire_demon_slash")
	for track in animation.get_track_count():
		if animation.track_get_type(track) != Animation.TYPE_METHOD:
			continue
		for key in animation.track_get_key_count(track):
			var call: Dictionary = animation.track_get_key_value(track, key)
			if call.method == &"BeginFireDemonAnimEvent":
				call.args = []
				animation.track_set_key_value(track, key, call)
	assert(ResourceSaver.save(library, library.resource_path) == OK)
	var scene := PackedScene.new()
	assert(scene.pack(root) == OK)
	assert(ResourceSaver.save(scene, path) == OK)
	root.free()
	for unused in ["res://TheGame/Entitys/Effects/Skills/wukong_fire_demon_prelude_effect.tscn", "res://TheGame/Entitys/Animations/Skills/wukong_fire_demon_prelude_library.tres"]:
		assert(DirAccess.remove_absolute(unused) == OK)
	print("PRELUDE PASS: native world-space sprite, source position/offset, completion signal; removed empty hitbox and pooled scene.")
	quit()
