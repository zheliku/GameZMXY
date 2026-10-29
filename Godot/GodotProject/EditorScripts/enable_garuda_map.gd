extends SceneTree
## Bind the sixth map button and its native official current-level animation.

func _initialize() -> void:
    _build.call_deferred()

func _build() -> void:
    var path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var form: Control = load(path).instantiate()
    var button: TextureButton = form.get_node_or_null("m_Jialuoluo")
    if button == null:
        button = form.get_node("Location6")
    button.name = "m_Jialuoluo"
    var template: TextureButton = form.get_node("m_Qixingdong")
    button.set_script(template.get_script())
    button.disabled = true
    button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
    button.texture_hover = null
    button.texture_pressed = null
    for child in button.get_children():
        child.free()
    var glow: TextureRect = template.get_node("LocationGlow").duplicate()
    glow.texture = load("res://TheGame/Sprites/UI/frontend/map_jialuoluo_hover.png")
    glow.material = glow.material.duplicate()
    glow.visible = false
    button.add_child(glow)
    glow.owner = form
    button.set("m_HoverImage", glow)
    var hint: Sprite2D = template.get_node("CurrentHint").duplicate()
    hint.texture = load("res://TheGame/Sprites/UI/frontend/map_jialuoluo_current.png")
    hint.visible = false
    button.add_child(hint)
    hint.owner = form
    var player: AnimationPlayer = form.get_node("m_LocationAnimation")
    var library: AnimationLibrary = player.get_animation_library("qixingdong").duplicate(true)
    for name in library.get_animation_list():
        var animation := library.get_animation(name)
        for track in animation.get_track_count():
            animation.track_set_path(track, NodePath(String(animation.track_get_path(track)).replace("m_Qixingdong", "m_Jialuoluo")))
    var destination := "res://TheGame/Sprites/UI/frontend/map_jialuoluo_hover_animation.tres"
    assert(ResourceSaver.save(library, destination) == OK)
    if player.has_animation_library("jialuoluo"):
        player.remove_animation_library("jialuoluo")
    player.add_animation_library("jialuoluo", ResourceLoader.load(destination, "AnimationLibrary", ResourceLoader.CACHE_MODE_IGNORE))
    form.set("m_Jialuoluo", button)
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    form.free()
    print("GARUDA 2-3 MAP AUTHOR PASS: bound location, official 19-frame hint and hover")
    quit()
