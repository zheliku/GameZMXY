extends SceneTree
## Latest-level hints use the original 19-frame glow; hover keeps its existing editor controls.

const ENTRIES := [
	["m_Huaguoshan", "huaguoshan", "location"],
	["m_Shuiliandong", "shuiliandong", "shuiliandong"],
	["m_Taohuayuan", "taohuayuan", "taohuayuan"],
	["m_Bianfuku", "bianfuku", "bianfuku"],
	["m_Qixingdong", "qixingdong", "qixingdong"]
]

func _key(animation: Animation, path: String, value: Variant) -> void:
	var track := animation.find_track(NodePath(path), Animation.TYPE_VALUE)
	if track < 0:
		track = animation.add_track(Animation.TYPE_VALUE)
		animation.track_set_path(track, NodePath(path))
	animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
	while animation.track_get_key_count(track) > 0:
		animation.track_remove_key(track, 0)
	animation.track_insert_key(track, 0, value)

func _initialize() -> void:
	_build.call_deferred()

func _build() -> void:
	assert(Engine.is_editor_hint())
	var path := "res://TheGame/UIs/map_form.tscn"
	var uid := ResourceLoader.get_resource_uid(path)
	var form := (load(path) as PackedScene).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var player: AnimationPlayer = form.get_node("m_LocationAnimation")
	for entry in ENTRIES:
		var button: TextureButton = form.get_node(entry[0])
		var sprite: Sprite2D = button.get_node_or_null("CurrentHint")
		if sprite == null:
			sprite = Sprite2D.new()
			sprite.name = "CurrentHint"
			button.add_child(sprite)
			sprite.owner = form
		sprite.position = button.size / 2
		sprite.texture = load("res://TheGame/Sprites/UI/frontend/map_%s_current.png" % entry[1])
		sprite.hframes = 5
		sprite.vframes = 4
		sprite.visible = false
		var library := player.get_animation_library(entry[2])
		var library_path := library.resource_path
		var library_uid := ResourceLoader.get_resource_uid(library_path)
		for name in [&"RESET", &"hover"]:
			_key(library.get_animation(name), "%s/CurrentHint:visible" % entry[0], false)
		var current := library.get_animation(&"current")
		for track in range(current.get_track_count()-1, -1, -1):
			current.remove_track(track)
		current.length = 19.0/24
		current.loop_mode = Animation.LOOP_LINEAR
		_key(current, "%s/LocationGlow:visible" % entry[0], false)
		_key(current, "%s:self_modulate" % entry[0], Color(1, 1, 1, 0))
		_key(current, "%s/CurrentHint:visible" % entry[0], true)
		_key(current, "%s/CurrentHint:frame" % entry[0], 0)
		var frame_track := current.find_track(NodePath("%s/CurrentHint:frame" % entry[0]), Animation.TYPE_VALUE)
		for frame in range(1, 19):
			current.track_insert_key(frame_track, frame/24.0, frame)
		assert(ResourceSaver.save(library, library_path) == OK)
		assert(ResourceSaver.set_uid(library_path, library_uid) == OK)
	# Start in the reset pose; runtime progress chooses the only current hint.
	for entry in ENTRIES:
		player.play(entry[2]+"/RESET")
		player.advance(0)
	player.stop()
	var packed := PackedScene.new()
	assert(packed.pack(form) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	assert(ResourceSaver.set_uid(path, uid) == OK)
	form.free()
	print("MAP HINT AUTHOR PASS: 5 original 19-frame hints, native frame tracks, existing hover preserved")
	quit()
