extends SceneTree
## Scan official dragon frames and attach the original shared frozen overlay by export binding.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    if not Engine.is_editor_hint():
        printerr("Dragon art registration requires --editor.")
        quit(1)
        return
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var root := "res://TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/"
    for entry in [["dragon_king_official", 315], ["dragon_king_ice_aura", 90],
            ["dragon_king_wave", 41], ["dragon_king_torrent", 68]]:
        var frames: SpriteFrames = load(root + entry[0] + "_frames.tres")
        assert(frames != null)
        var count := 0
        for animation in frames.get_animation_names():
            count += frames.get_frame_count(animation)
        assert(count == entry[1], "Incomplete source frames: " + entry[0])
    var library: AnimationLibrary = load("res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_library.tres")
    assert(library != null and library.has_animation("attack_4"))
    var scene_path := "res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn"
    var uid := ResourceLoader.get_resource_uid(scene_path)
    var actor: Node = load(scene_path).instantiate()
    var statuses := actor.get_node("m_StatusEffects")
    var frozen := statuses.get_node_or_null("m_FrozenEffect") as AnimatedSprite2D
    if frozen == null:
        frozen = AnimatedSprite2D.new()
        frozen.name = "m_FrozenEffect"
        statuses.add_child(frozen)
        frozen.owner = actor
    frozen.sprite_frames = load("res://TheGame/Sprites/Characters/Heroes/Status/hero_frozen_frames.tres")
    assert(frozen.sprite_frames.get_frame_count("play") == 35)
    frozen.animation = "play"
    frozen.offset = Vector2(96, 96)
    frozen.position = Vector2(-90, -115)
    frozen.visible = false
    var material := CanvasItemMaterial.new()
    material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
    frozen.material = material
    actor.set("_frozenEffect", frozen)
    assert(actor.get("_frozenEffect") == frozen)
    var packed := PackedScene.new()
    assert(packed.pack(actor) == OK)
    assert(ResourceSaver.save(packed, scene_path) == OK)
    assert(ResourceSaver.set_uid(scene_path, uid) == OK)
    actor.free()
    print("DRAGON ART REGISTRATION PASS: 514 source actor/effect frames; native 35-frame additive frozen overlay at official anchor")
    quit()
