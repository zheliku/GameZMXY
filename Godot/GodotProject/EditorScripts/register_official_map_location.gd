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
    var field: String = args[2]
    var frame_count := int(args[4]) if args.size() > 4 else 19
    var path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var form: Control = load(path).instantiate()
    var button: TextureButton = form.get_node_or_null(field)
    if button == null:
        button = form.get_node(args[1])
    var template: TextureButton = form.get_node("m_Jialuoluo")
    button.name = field
    button.set_script(template.get_script())
    button.disabled = true
    button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
    button.texture_hover = null
    button.texture_pressed = null
    for child in button.get_children():
        child.free()
    var glow: TextureRect = template.get_node("LocationGlow").duplicate()
    glow.texture = load("res://TheGame/Sprites/UI/frontend/map_%s_hover.png" % key)
    if key == "shazhiwoliu":
        glow.texture = load("res://TheGame/Sprites/UI/frontend/map_shazhiwoliu_hover_registered.png")
    glow.material = glow.material.duplicate()
    glow.position = Vector2.ZERO
    glow.size = button.size
    glow.pivot_offset = button.size * .5
    glow.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    glow.visible = false
    button.add_child(glow)
    glow.owner = form
    button.set("m_HoverImage", glow)
    var hint: Sprite2D = template.get_node("CurrentHint").duplicate()
    hint.texture = load("res://TheGame/Sprites/UI/frontend/map_%s_current.png" % key)
    hint.hframes = 5
    hint.vframes = ceili(frame_count / 5.0)
    hint.position = button.size * .5
    var fit := minf(button.size.x / button.texture_normal.get_width(), button.size.y / button.texture_normal.get_height())
    hint.scale = Vector2.ONE * fit
    hint.visible = false
    button.add_child(hint)
    hint.owner = form
    var player: AnimationPlayer = form.get_node("m_LocationAnimation")
    var library: AnimationLibrary = player.get_animation_library("jialuoluo").duplicate(true)
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
    assert(ResourceSaver.save(library, destination) == OK)
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
