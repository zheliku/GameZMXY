extends SceneTree
## Register the official 2-2 map location using the existing location widget contract.

func _initialize() -> void:
	call_deferred("_build")

func _build() -> void:
	var path := "res://TheGame/UIs/map_form.tscn"
	var form: Control = load(path).instantiate()
	var button: TextureButton = form.get_node_or_null("m_Qixingdong")
	if button == null:
		button = form.get_node("Location5")
	button.name = "m_Qixingdong"
	var template: TextureButton = form.get_node("m_Huaguoshan")
	button.set_script(template.get_script())
	button.disabled = true
	button.visible = true
	button.tooltip_text = "七星洞"
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.texture_hover = null
	button.texture_pressed = null
	for child in button.get_children():
		if child is TextureRect:
			child.free()
	var glow: TextureRect = template.get_node("LocationGlow").duplicate()
	glow.texture = load("res://TheGame/Sprites/UI/frontend/map_qixingdong_hover.png")
	glow.material = glow.material.duplicate()
	glow.visible = false
	button.add_child(glow)
	glow.name = "LocationGlow"
	glow.owner = form
	button.set("m_HoverImage", glow)
	var source: AnimationLibrary = form.get_node("m_LocationAnimation").get_animation_library("taohuayuan")
	var library := source.duplicate(true) as AnimationLibrary
	for name in library.get_animation_list():
		var animation := library.get_animation(name)
		for track in animation.get_track_count():
			animation.track_set_path(track, NodePath(String(animation.track_get_path(track)).replace("m_Taohuayuan", "m_Qixingdong")))
	var destination := "res://TheGame/Sprites/UI/frontend/map_qixingdong_hover_animation.tres"
	assert(ResourceSaver.save(library, destination) == OK)
	if form.get_node("m_LocationAnimation").has_animation_library("qixingdong"):
		form.get_node("m_LocationAnimation").remove_animation_library("qixingdong")
	var external: AnimationLibrary = ResourceLoader.load(destination, "AnimationLibrary", ResourceLoader.CACHE_MODE_IGNORE)
	assert(external != null and external.resource_path == destination)
	form.get_node("m_LocationAnimation").add_animation_library("qixingdong", external)
	form.set("m_Qixingdong", button)
	var packed := PackedScene.new()
	assert(packed.pack(form) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	print("MAP QIXING CAVE PASS: location 5 uses the shared map button and external hover animation")
	quit()
