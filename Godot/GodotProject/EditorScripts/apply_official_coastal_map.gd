extends SceneTree
## Apply the exporter registration, including the invisible official input rectangles.

func _initialize() -> void:
    _apply.call_deferred()

func _apply() -> void:
    assert(Engine.is_editor_hint())
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1, "Pass the coastal export manifest")
    var manifest_path: String = args[0]
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in 5:
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var form: Control = load(path).instantiate()
    var player: AnimationPlayer = form.get_node("m_LocationAnimation")
    for location in manifest.locations:
        var name: String = location.name
        var button: TextureButton = form.get_node("m_" + name.capitalize())
        var size := Vector2(location.size[0], location.size[1])
        var origin := Vector2(location.position[0], location.position[1])
        var offset := Vector2(location.offset[0], location.offset[1])
        button.position = origin + offset
        button.size = size
        button.pivot_offset = -offset
        button.stretch_mode = TextureButton.STRETCH_KEEP_CENTERED
        button.texture_normal = load("res://TheGame/Sprites/UI/frontend/map_" + name + "_normal.png")
        button.texture_disabled = button.texture_normal
        var mask := BitMap.new()
        mask.create_from_image_alpha(Image.load_from_file(manifest_path.get_base_dir() + "/" + name + "_hit.png"))
        var mask_path := "res://TheGame/Sprites/UI/frontend/map_" + name + "_hit_mask.res"
        assert(ResourceSaver.save(mask, mask_path) == OK)
        mask.take_over_path(mask_path)
        button.texture_click_mask = mask
        var glow: TextureRect = button.get_node("LocationGlow")
        glow.texture = load("res://TheGame/Sprites/UI/frontend/map_" + name + "_hover.png")
        glow.position = Vector2.ZERO
        glow.size = size
        glow.pivot_offset = -offset
        glow.scale = Vector2.ONE
        var hint: Sprite2D = button.get_node("CurrentHint")
        hint.texture = load("res://TheGame/Sprites/UI/frontend/map_" + name + "_current.png")
        hint.hframes = 5
        hint.vframes = ceili(float(location.states.current.frames) / 5)
        hint.position = size * .5
        hint.scale = Vector2.ONE
        var library := player.get_animation_library(name)
        for animation_name in library.get_animation_list():
            var animation := library.get_animation(animation_name)
            for track in animation.get_track_count():
                var track_path := String(animation.track_get_path(track))
                if track_path.ends_with("shader_parameter/image_scale") or track_path.ends_with("shader_parameter/line_thickness"):
                    var value := 1.0 if track_path.ends_with("image_scale") else 0.0
                    for key in animation.track_get_key_count(track):
                        animation.track_set_key_value(track, key, value)
        assert(ResourceSaver.save(library, library.resource_path) == OK)
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    form.free()
    print("OFFICIAL COAST APPLY PASS: native positions, shared registration, full glow and original hit areas")
    quit()
