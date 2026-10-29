extends SceneTree
## Validate source timings independently from the not-yet-implemented gameplay class.

func _initialize() -> void:
    _validate.call_deferred()

func _calls(animation: Animation, method: String) -> Array:
    var result := []
    for track in animation.get_track_count():
        if animation.track_get_type(track) != Animation.TYPE_METHOD:
            continue
        for key in animation.track_get_key_count(track):
            var call: Dictionary = animation.track_get_key_value(track, key)
            if call.method == method:
                result.append([animation.track_get_key_time(track, key), call.args])
    return result

func _validate() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 2, "Expected source actor manifest and verified rule report")
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var rules: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
    assert(source.source_sha256 == rules.source_sha256)
    var root := "res://TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/"
    var frames: SpriteFrames = load(root + "dragon_king_official_frames.tres")
    var library: AnimationLibrary = load("res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_library.tres")
    for action in source.actors[0].actions:
        var animation := library.get_animation(action.name)
        assert(frames.get_frame_count(action.name) == int(action.frames))
        assert(is_equal_approx(animation.length, float(action.frames) / 24))
        if not action.complete_frames.is_empty():
            assert(animation.has_marker("complete") and is_equal_approx(animation.get_marker_time("complete"),
                (float(action.complete_frames[-1]) - 1) / 24))
    var pulse_animation := library.get_animation("attack_2")
    var pulses := _calls(pulse_animation, "NewAttackAnimEvent")
    assert(pulses.size() == 6 and _calls(pulse_animation, "BeginDashAnimEvent").is_empty())
    for index in pulses.size():
        assert(is_equal_approx(pulses[index][0], (33 + index * 5) / 24.0))
    for entry in [["attack_2", "1"], ["attack_3", "2"], ["attack_4", "3"]]:
        var calls := _calls(library.get_animation(entry[0]), "BeginInvulnerabilityAnimEvent")
        assert(calls.size() == 1 and is_equal_approx(calls[0][0], 0))
        assert(is_equal_approx(calls[0][1][0], rules.skill_invulnerability_seconds[entry[1]]))
    assert(is_equal_approx(_calls(library.get_animation("attack_1"), "SpawnWaveAnimEvent")[0][0], 10 / 24.0))
    assert(is_equal_approx(_calls(library.get_animation("attack_4"), "SpawnTorrentAnimEvent")[0][0], 0))
    var frozen: SpriteFrames = load("res://TheGame/Sprites/Characters/Heroes/Status/hero_frozen_frames.tres")
    assert(frozen.get_frame_count("play") == 35 and not frozen.get_animation_loop("play"))
    var hero: Node = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
    var overlay: AnimatedSprite2D = hero.get("m_FrozenEffect")
    assert(overlay != null and not overlay.visible and overlay.position == Vector2(-90, -115))
    assert((overlay.material as CanvasItemMaterial).blend_mode == CanvasItemMaterial.BLEND_MODE_ADD)
    hero.free()
    print("DRAGON ART/TIMING 9-1 PASS: 315 actor frames, six native pulses, three invulnerability clocks, source effect events and frozen binding")
    quit()
