extends SceneTree
## Compare native sea timelines and simple geometry with independently extracted source probes.

func _initialize() -> void:
    create_timer(30).timeout.connect(func():
        printerr("SEA ACTOR VALIDATION timed out")
        quit(1))
    _validate.call_deferred()

func _validate() -> void:
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var samples := 0
    for actor in manifest.actors:
        var directory: String = {"jiao_demon_king": "Bosses/zm1_08_east_sea",
            "shark_demon_king": "Bosses/zm1_09_shark_whirlpool",
            "shark_spawn": "Monsters/Sea/shark_spawn"}.get(actor.name, "Monsters/Sea/%s" % actor.name)
        var scene: Node = load("res://TheGame/Entitys/Enemies/%s/%s_entity.tscn" % [directory, actor.name]).instantiate()
        root.add_child(scene)
        scene.set_physics_process(false)
        var player: AnimationPlayer = scene.get("m_AnimPlayer")
        player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        var body: AnimatedSprite2D = scene.get("m_Body")
        var shape: CollisionShape2D = scene.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D")
        for action in actor.actions:
            var animation: Animation = player.get_animation(action.name)
            assert(is_equal_approx(animation.length, action.frames / 24.0))
            if action.get("complete_frames", []).size() > 0:
                assert(animation.has_marker("complete"))
                assert(is_equal_approx(animation.get_marker_time("complete"), (action.complete_frames[-1] - 1) / 24.0))
            for facing in [-1, 1]:
                scene.call("SetFacing", facing)
                player.play(action.name)
                for index in int(action.frames):
                    player.seek(index / 24.0 + 0.000001, true, true)
                    assert(body.animation == action.name and body.frame == index)
                    assert(body.offset.is_equal_approx(Vector2(actor.layers[0].sprite_offset[0], actor.layers[0].sprite_offset[1])))
                    var bounds = action.hit_bounds[index]
                    assert(shape.disabled == (bounds == null), "%s/%s/%d window" % [actor.name, action.name, index + 1])
                    if bounds != null:
                        assert(shape.shape is RectangleShape2D)
                        assert(shape.shape.size.is_equal_approx(Vector2(bounds[2], bounds[3])))
                        assert(shape.position.is_equal_approx(Vector2(bounds[0] + bounds[2] * .5, bounds[1] + bounds[3] * .5)))
                    samples += 1
            for event in action.get("frame_events", []):
                if not "newAttackId()" in event.body:
                    continue
                var found := false
                for track in animation.get_track_count():
                    if animation.track_get_type(track) != Animation.TYPE_METHOD:
                        continue
                    for key in animation.track_get_key_count(track):
                        var value: Dictionary = animation.track_get_key_value(track, key)
                        found = found or (value.method == "NewAttackAnimEvent" and
                            is_equal_approx(animation.track_get_key_time(track, key), (event.frame - 1) / 24.0))
                assert(found, "%s/%s missing source attack refresh frame %d" % [actor.name, action.name, event.frame])
        scene.queue_free()
        await process_frame
    print("SEA ACTOR VALIDATION PASS: %d native frames/directions, source geometry, terminal markers and nested refresh events" % samples)
    quit()
