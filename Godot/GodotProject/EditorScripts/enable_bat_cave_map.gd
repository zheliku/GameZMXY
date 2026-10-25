extends SceneTree
## Register the official bat-cave map location using the existing location widget contract.

func _initialize() -> void:
	call_deferred("_build")

func _build() -> void:
	var path := "res://TheGame/UIs/map_form.tscn"
	var form: Control = load(path).instantiate()
	var button = form.get_node("Location4")
	var template = form.get_node("m_Huaguoshan")
	button.set_script(template.get_script())
	button.disabled = false
	button.visible = true
	button.tooltip_text = "蝙蝠窟"
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.texture_hover = null
	button.texture_pressed = null
	var glow: TextureRect = template.get_node("LocationGlow").duplicate()
	glow.name = "LocationGlow"
	glow.texture = load("res://TheGame/Sprites/UI/frontend/map_bianfuku_hover.png")
	glow.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	glow.material = glow.material.duplicate()
	glow.visible = false
	button.add_child(glow); glow.owner = form
	button.set("m_HoverImage", glow)
	var source: AnimationLibrary = form.get_node("m_LocationAnimation").get_animation_library("taohuayuan")
	var library := source.duplicate(true) as AnimationLibrary
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		for track in animation.get_track_count():
			animation.track_set_path(track, NodePath(String(animation.track_get_path(track)).replace("m_Taohuayuan", "Location4")))
	var destination := "res://TheGame/Sprites/UI/frontend/map_bianfuku_hover_animation.tres"
	assert(ResourceSaver.save(library, destination) == OK)
	library.take_over_path(destination)
	form.get_node("m_LocationAnimation").add_animation_library("bianfuku", library)
	form.set("m_Bianfuku", button)
	var packed := PackedScene.new()
	assert(packed.pack(form) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	print("MAP BAT CAVE PASS: location 4 uses the shared map button and external hover animation")
	quit()
