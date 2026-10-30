extends SceneTree
## Check native geometry against source mask intersections, independent of C# combat.

func _initialize() -> void:
    _check.call_deferred()

func _check_box(shape: CollisionShape2D, box: Variant, label: String) -> void:
    assert(shape.disabled == (box == null), label + " incorrect native attack window")
    if box == null:
        return
    assert(shape.shape is RectangleShape2D, label + " expected one basic rectangle")
    assert(shape.shape.size.is_equal_approx(Vector2(box[2], box[3])), label + " incorrect shape dimensions")
    assert(shape.position.is_equal_approx(Vector2(box[0] + box[2] / 2, box[1] + box[3] / 2)), label + " incorrect source origin")

func _check() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1)
    var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var base := "res://TheGame/Entitys/Enemies/Bosses/zm1_11_hidden_dragon_palace/"
    var actor: Node = load(base + "dragon_king_entity.tscn").instantiate()
    root.add_child(actor)
    var player: AnimationPlayer = actor.get("m_AnimPlayer")
    for library_name in player.get_animation_library_list():
        var library := player.get_animation_library(library_name)
        for name in library.get_animation_list():
            var animation := library.get_animation(name)
            for track in animation.get_track_count():
                if animation.track_get_type(track) == Animation.TYPE_METHOD:
                    animation.track_set_enabled(track, false)
    var count := 0
    for action in report.actor:
        var boxes: Array = report.actor[action]
        for frame in boxes.size():
            player.play(action)
            player.seek(frame / 24.0, true, true)
            _check_box(actor.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D"), boxes[frame], action + "/" + str(frame))
            var aura: AnimatedSprite2D = actor.get_node("m_HitBoxRoot/m_IceAura")
            assert(aura.visible == (action == "attack_2"))
            count += 1
    actor.free()
    for name in report.effects:
        var effect: Node = load(base + name + "_effect.tscn").instantiate()
        root.add_child(effect)
        var effect_player: AnimationPlayer = effect.get("m_Player")
        var boxes: Array = report.effects[name]
        for frame in boxes.size():
            effect_player.play("play")
            effect_player.seek(frame / 24.0, true, true)
            _check_box(effect.get("m_HitBox").get_child(0), boxes[frame], name + "/" + str(frame))
            count += 1
        effect.free()
    var room: Node = load("res://TheGame/Scenes/Levels/zm1_11_hidden_dragon_palace/level_11.tscn").instantiate()
    assert(room.get("m_LevelId") == 11 and room.get("m_MusicId") == 108)
    assert(room.get_node("World/Exit").position == Vector2(814.75, 457.55))
    assert(room.get_node("World/SpawnPoints/Stage1A").position.is_equal_approx(Vector2(947.65, 285.75)))
    room.free()
    var cave: Node = load("res://TheGame/Scenes/Levels/zm1_02_shuiliandong/level_2.tscn").instantiate()
    var entrance: Area2D = cave.get("m_HiddenEntrance")
    assert(entrance != null and not entrance.monitoring)
    assert(entrance.position.is_equal_approx(Vector2(2151.024558265, 615.010125734)))
    assert(entrance.get_child(0).shape.size.is_equal_approx(Vector2(499.95119367, 50.00032979)))
    var fade: AnimationPlayer = cave.get("m_EntranceFade")
    assert(fade != null and is_equal_approx(fade.get_animation("fade").length, 1))
    cave.free()
    print("DRAGON NATIVE RESOURCES PASS: ", count, " source collision frames, room/portal/music and water entrance bindings")
    quit()
