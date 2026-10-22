extends SceneTree
## Enable the two completed stage entries and reuse the native location hover presentation.

func _initialize() -> void:
	call_deferred("_build")

## Keep existing map coordinates, images and UID, and add explicit form bindings.
func _build() -> void:
	var path := "res://TheGame/UIs/map_form.tscn"
	var form: Control = load(path).instantiate()
	var player: AnimationPlayer = form.get_node("m_LocationAnimation")
	var source: AnimationLibrary = player.get_animation_library("location")
	var template: TextureButton = form.get_node("m_Huaguoshan")
	for entry in [[2,"m_Shuiliandong","shuiliandong","水帘洞"],[3,"m_Taohuayuan","taohuayuan","桃花源"]]:
		var button: TextureButton = form.get_node("Location%d" % entry[0])
		button.name = entry[1]
		button.disabled = false
		button.tooltip_text = entry[3]
		button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		button.texture_hover = null
		button.texture_pressed = null
		button.set_script(template.get_script())
		var glow: TextureRect = template.get_node("LocationGlow").duplicate()
		glow.texture = load("res://TheGame/Sprites/UI/frontend/map_%s_hover.png" % entry[2])
		glow.material = glow.material.duplicate()
		glow.visible = false
		button.add_child(glow)
		glow.owner = form
		button.set("m_HoverImage",glow)
		var library := source.duplicate(true) as AnimationLibrary
		for name in library.get_animation_list():
			var animation := library.get_animation(name)
			for track in animation.get_track_count():
				animation.track_set_path(track,NodePath(String(animation.track_get_path(track)).replace("m_Huaguoshan",entry[1])))
		var destination := "res://TheGame/Sprites/UI/frontend/map_%s_hover_animation.tres" % entry[2]
		assert(ResourceSaver.save(library,destination)==OK)
		library.take_over_path(destination)
		player.add_animation_library(entry[2],library)
		form.set(entry[1],button)
	var packed := PackedScene.new()
	assert(packed.pack(form)==OK)
	assert(ResourceSaver.save(packed,path)==OK)
	form.free()
	print("MAP LEVELS PASS: native map locations 2 and 3 enabled, hover libraries and form bindings saved.")
	quit()
