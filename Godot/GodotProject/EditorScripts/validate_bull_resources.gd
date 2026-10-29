extends SceneTree
## Compare the actual valley scene, frames, method times and fades with source manifests.

func _initialize() -> void:
    create_timer(30).timeout.connect(func():
        printerr("BULL RESOURCE FAIL: validation timed out")
        quit(1))
    _validate.call_deferred()

func _method_at(animation: Animation, method: String, frame: int) -> bool:
    for track in animation.get_track_count():
        if animation.track_get_type(track) != Animation.TYPE_METHOD:
            continue
        for key in animation.track_get_key_count(track):
            var call: Dictionary = animation.track_get_key_value(track, key)
            if call.method == method and is_equal_approx(animation.track_get_key_time(track, key), (frame - 1) / 24.0):
                return true
    return false

func _validate() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 3, "Expected bosses, monsters and all-level source JSON")
    var samples := 0
    for file in [args[0], args[1]]:
        var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(file))
        for actor in manifest.actors:
            var relative: String = "Monsters/Ground/bull_guard" if actor.name == "bull_guard" else "Bosses/zm1_10_bull_demon_valley"
            var scene: Node = load("res://TheGame/Entitys/Enemies/%s/%s_entity.tscn" % [relative, actor.name]).instantiate()
            root.add_child(scene)
            scene.set_physics_process(false)
            var player: AnimationPlayer = scene.get("m_AnimPlayer")
            player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
            var body: AnimatedSprite2D = scene.get("m_Body")
            var shape: CollisionShape2D = scene.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D")
            for action in actor.actions:
                var animation := player.get_animation(action.name)
                assert(is_equal_approx(animation.length, action.frames / 24.0))
                if action.complete_frames.size() > 0:
                    assert(animation.has_marker("complete") and is_equal_approx(animation.get_marker_time("complete"), (action.complete_frames[-1] - 1) / 24.0))
                for direction in [-1, 1]:
                    scene.call("SetFacing", direction)
                    player.play(action.name)
                    for frame in int(action.frames):
                        player.seek(frame / 24.0 + 0.000001, true, true)
                        assert(body.animation == action.name and body.frame == frame)
                        assert(body.offset.is_equal_approx(Vector2(actor.layers[0].sprite_offset[0], actor.layers[0].sprite_offset[1])))
                        var bounds = action.hit_bounds[frame]
                        assert(shape.disabled == (bounds == null), "%s/%s/%d hit window" % [actor.name, action.name, frame + 1])
                        if bounds != null:
                            assert(shape.shape is RectangleShape2D and shape.shape.size.is_equal_approx(Vector2(bounds[2], bounds[3])))
                            assert(shape.position.is_equal_approx(Vector2(bounds[0] + bounds[2] / 2, bounds[1] + bounds[3] / 2)))
                        samples += 1
            if actor.name == "bull_demon_beast":
                assert(_method_at(player.get_animation("death"), "CompleteTransformAnimEvent", 25))
                assert(player.get_animation("charge").loop_mode == Animation.LOOP_LINEAR)
            elif actor.name == "bull_demon_king":
                assert(_method_at(player.get_animation("attack_1"), "NewAttackAnimEvent", 33))
                assert(_method_at(player.get_animation("attack_2"), "BeginDashAnimEvent", 30))
                assert(_method_at(player.get_animation("attack_3"), "NewAttackAnimEvent", 43))
                assert(_method_at(player.get_animation("attack_3"), "NewAttackAnimEvent", 55))
                assert(_method_at(player.get_animation("attack_5"), "CaptureHeroAnimEvent", 28))
                for frame in [46, 62, 78, 95, 112]:
                    assert(_method_at(player.get_animation("attack_5"), "DrainCaptiveAnimEvent", frame))
                assert(_method_at(player.get_animation("death"), "SpawnBossDeathAnimEvent", 25))
            elif actor.name == "bull_demon_heart":
                var fade: AnimationPlayer = scene.get("m_HeartFade")
                var heart: AnimatedSprite2D = scene.get("m_Heart")
                fade.play("show")
                fade.seek(.5, true, true)
                assert(is_equal_approx(heart.modulate.a, .4375), "Native fade does not match official Quad.easeOut")
                fade.play("hide")
                fade.seek(.5, true, true)
                assert(is_equal_approx(heart.modulate.a, .5625))
            scene.queue_free()
            await process_frame
    var level: Node = load("res://TheGame/Scenes/Levels/zm1_10_bull_demon_valley/level_10.tscn").instantiate()
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[2]))["4-1"]
    assert(level.get("m_LevelId") == 10 and level.get("m_MusicId") == 108)
    assert(level.get("m_Exit") == null and not level.has_node("World/Exit"))
    assert(level.get("m_FeaturedOpponentSpawnPointIds") == PackedStringArray(["stage_2_a"]))
    assert(level.get_node("World/SpawnPoints").get("m_Entries").size() == 4)
    assert(level.get_node("World/StageGates").get("m_Entries").size() == 3)
    assert(level.get_node("World/Geometry").find_children("*", "CollisionShape2D", true, false).size() == 3)
    for point in source.spawns:
        assert(level.get_node("World/SpawnPoints/" + point.projectNode).position.is_equal_approx(Vector2(point.position[0], point.position[1])))
    for gate in source.gates:
        var order := int(gate.properties.idx) + 1
        assert(is_equal_approx(level.get_node("World/StageGates/Stage%dGate" % order).position.x, gate.battleBounds[1]))
    level.free()
    var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
    assert(form.get("m_Niumogu") == form.get_node("m_Niumogu"))
    assert(is_equal_approx(form.get_node("m_LocationAnimation").get_animation("niumogu/current").length, 19 / 24.0))
    form.free()
    print("BULL RESOURCE 4-1 PASS: %d source-frame/direction samples, five drain methods, native fades, complete level/map bindings" % samples)
    quit()
