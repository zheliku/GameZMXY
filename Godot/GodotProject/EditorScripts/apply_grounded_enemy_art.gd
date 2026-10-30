extends SceneTree
## Install reviewed shared-canvas artwork without rewriting method or hitbox tracks.

func _initialize() -> void:
    _run.call_deferred()

func _run() -> void:
    assert(Engine.is_editor_hint())
    var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    # Run the complete --editor --import pass after this authoring script returns;
    # an early editor script cannot force imports while the initial scan is busy.
    for actor in data.actors:
        var path: String = actor.installed_scene
        var entity: Node = load(path).instantiate()
        var body: AnimatedSprite2D = entity.get("m_Body")
        body.sprite_frames = load(actor.installed_frames)
        body.offset = Vector2(actor.layers[0].sprite_offset[0], actor.layers[0].sprite_offset[1])
        var player: AnimationPlayer = entity.get("m_AnimPlayer")
        var library := player.get_animation_library("")
        for name in library.get_animation_list():
            var animation := library.get_animation(name)
            var track := animation.find_track(NodePath("m_Body:offset"), Animation.TYPE_VALUE)
            assert(track >= 0)
            for key in animation.track_get_key_count(track):
                animation.track_set_key_value(track, key, body.offset)
        var library_path := library.resource_path
        var library_uid := ResourceLoader.get_resource_uid(library_path)
        assert(ResourceSaver.save(library, library_path) == OK)
        assert(ResourceSaver.set_uid(library_path, library_uid) == OK)
        var uid := ResourceLoader.get_resource_uid(path)
        var packed := PackedScene.new()
        assert(packed.pack(entity) == OK)
        assert(ResourceSaver.save(packed, path) == OK)
        assert(ResourceSaver.set_uid(path, uid) == OK)
        entity.free()
    print("GROUNDED ENEMY AUTHOR PASS: %d native scenes/libraries, existing events and geometry retained" % data.actors.size())
    quit()
