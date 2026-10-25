extends SceneTree

func _initialize() -> void:
	call_deferred("_build")

func _build() -> void:
	var path := "res://TheGame/UIs/map_form.tscn"
	var form: Control = load(path).instantiate()
	var button := form.get_node("Location4")
	button.name = "m_Bianfuku"
	form.set("m_Bianfuku", button)
	var player: AnimationPlayer = form.get_node("m_LocationAnimation")
	var library: AnimationLibrary = player.get_animation_library("bianfuku")
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		for track in animation.get_track_count():
			animation.track_set_path(track, NodePath(String(animation.track_get_path(track)).replace("Location4", "m_Bianfuku")))
	ResourceSaver.save(library, "res://TheGame/Sprites/UI/frontend/map_bianfuku_hover_animation.tres")
	library.take_over_path("res://TheGame/Sprites/UI/frontend/map_bianfuku_hover_animation.tres")
	var packed := PackedScene.new()
	assert(packed.pack(form) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	print("MAP BAT CAVE NODE PASS: m_Bianfuku binding and animation paths aligned")
	quit()
