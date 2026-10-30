extends SceneTree
## Center every hover drawing in the same button rectangle as its normal image.

func _initialize() -> void:
    _repair.call_deferred()

func _repair() -> void:
    assert(Engine.is_editor_hint())
    var path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var form: Control = load(path).instantiate()
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in 5:
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var count := 0
    for child in form.get_children():
        if child is not TextureButton or not child.has_node("LocationGlow"):
            continue
        if child.name in ["m_Qianan", "m_Donghai", "m_Shazhiwoliu"]:
            # These two locations have asymmetric official state matrices, not a fitted center.
            continue
        var glow: TextureRect = child.get_node("LocationGlow")
        glow.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
        glow.position = Vector2.ZERO
        glow.size = child.size
        glow.pivot_offset = child.size * .5
        count += 1
    assert(count == 7)
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    var discarded := "res://TheGame/Sprites/UI/frontend/map_shazhiwoliu_hover_texture.tres"
    if FileAccess.file_exists(discarded):
        assert(DirAccess.remove_absolute(discarded) == OK)
    form.free()
    print("MAP ALIGNMENT AUTHOR PASS: 7 centered hover canvases, coastal registration preserved; apply bounds padding afterwards")
    quit()
