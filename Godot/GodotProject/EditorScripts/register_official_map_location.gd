extends SceneTree
## Add an authored map location using the existing window and native hover library.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var args := OS.get_cmdline_user_args()
    assert(args.size() in [4, 5], "Expected key, old button name, export field, caption and optional source frame count")
    var key: String = args[0]
    assert(key not in ["qianan", "donghai", "shazhiwoliu"], "Coastal locations require apply_official_coastal_map.gd with the registered export manifest")
    var field: String = args[2]
    var frame_count := int(args[4]) if args.size() > 4 else 19
    var path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var form: Control = load(path).instantiate()
    var button: TextureButton = form.get_node_or_null(field)
    if button == null:
        button = form.get_node(args[1])
    var template: TextureButton = form.get_node("m_Jialuoluo")
    var player: AnimationPlayer = form.get_node("m_LocationAnimation")
    var library: AnimationLibrary = player.get_animation_library(key if player.has_animation_library(key) else "jialuoluo").duplicate(true)
    button.name = field
    button.set_script(template.get_script())
    button.disabled = true
    button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
    button.texture_hover = null
    button.texture_pressed = null
    # Rebuilding an existing location must preserve its authored registration and hover padding.
    var glow: TextureRect = button.get_node_or_null("LocationGlow")
    if glow == null:
        glow = template.get_node("LocationGlow").duplicate()
        button.add_child(glow)
        glow.owner = form
    glow.texture = load("res://TheGame/Sprites/UI/frontend/map_%s_hover.png" % key)
    glow.material = glow.material.duplicate()
    glow.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    glow.visible = false
    button.set("_hoverImage", glow)
    var hint: Sprite2D = button.get_node_or_null("CurrentHint")
    if hint == null:
        hint = template.get_node("CurrentHint").duplicate()
        button.add_child(hint)
        hint.owner = form
        hint.position = button.size * .5
        hint.scale = Vector2.ONE
    hint.texture = load("res://TheGame/Sprites/UI/frontend/map_%s_current.png" % key)
    hint.hframes = 5
    hint.vframes = ceili(frame_count / 5.0)
    hint.visible = false
    for name in library.get_animation_list():
        var animation := library.get_animation(name)
        for track in animation.get_track_count():
            animation.track_set_path(track, NodePath(String(animation.track_get_path(track)).replace("m_Jialuoluo", field)))
        if name == "current":
            animation.length = frame_count / 24.0
            var track := animation.find_track(NodePath(field + "/CurrentHint:frame"), Animation.TYPE_VALUE)
            assert(track >= 0)
            for index in range(animation.track_get_key_count(track) - 1, -1, -1):
                animation.track_remove_key(track, index)
            for index in frame_count:
                animation.track_insert_key(track, index / 24.0, index)
    var destination := "res://TheGame/Sprites/UI/frontend/map_%s_hover_animation.tres" % key
    var library_uid := ResourceLoader.get_resource_uid(destination)
    assert(ResourceSaver.save(library, destination) == OK)
    if library_uid != -1:
        assert(ResourceSaver.set_uid(destination, library_uid) == OK)
    if player.has_animation_library(key):
        player.remove_animation_library(key)
    player.add_animation_library(key, ResourceLoader.load(destination, "AnimationLibrary", ResourceLoader.CACHE_MODE_IGNORE))
    # First author the node, generate the Ge export, then rerun after the C# build to bind it.
    if form.get_property_list().any(func(property): return property.name == field):
        form.set(field, button)
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    form.free()
    print("MAP AUTHOR PASS: %s, independent official hint and sized hover" % key)
    quit()
