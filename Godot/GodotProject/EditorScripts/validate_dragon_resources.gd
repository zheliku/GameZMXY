extends SceneTree
## Check reviewed source regions and the hidden room/entrance bindings.

func _initialize() -> void:
    _check.call_deferred()

func _check_box(shape: CollisionShape2D, box: Dictionary, label: String) -> void:
    assert(shape.disabled == box.is_empty(), label + " incorrect native attack window")
    if box.is_empty():
        return
    assert(shape.shape is RectangleShape2D, label + " expected a basic rectangle")
    assert(shape.shape.size.is_equal_approx(Vector2(box.size[0], box.size[1])), label + " incorrect shape dimensions")
    assert(shape.position.is_equal_approx(Vector2(box.position[0], box.position[1])), label + " incorrect source origin")

func _check() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1)
    var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var base := "res://TheGame/Entitys/Enemies/Bosses/zm1_11_hidden_dragon_palace/"
    var count := 0
    for record in report.attacks:
        var is_actor: bool = record.actor == "dragon_king"
        var node: Node = load(base + record.actor + ("_entity.tscn" if is_actor else "_effect.tscn")).instantiate()
        node.process_mode = Node.PROCESS_MODE_DISABLED
        root.add_child(node)
        var player: AnimationPlayer = node.get("_animPlayer" if is_actor else "_player")
        var shape_path := "m_HitBoxRoot/m_HitBox/CollisionShape2D" if is_actor else "Origin/Visual/HitBox/CollisionShape2D"
        for frame in int(record.frames):
            player.play(record.animation)
            player.seek((frame + .1) / 24.0, true, true)
            var regions: Array = record.shapes[frame]
            # All seven pillars are independent; empty slots must remain disabled.
            for slot in int(record.get("slots", 1)):
                var shape: CollisionShape2D = node.get_node(shape_path + (str(slot + 1) if slot > 0 else ""))
                var expected: Dictionary = regions[slot] if regions.size() > slot and regions[slot] != null else {}
                _check_box(shape, expected, record.actor + "/" + str(frame) + "/" + str(slot))
            if is_actor:
                assert(node.get_node("m_HitBoxRoot/m_IceAura").visible == (record.animation == "attack_2"))
            count += 1
        node.free()
    var room: Node = load("res://TheGame/Scenes/Levels/zm1_11_hidden_dragon_palace/level_11.tscn").instantiate()
    assert(room.get("_levelId") == 11 and room.get("_musicId") == 108)
    assert(room.get_node("World/Exit").position == Vector2(814.75, 457.55))
    assert(room.get_node("World/SpawnPoints/Stage1A").position.is_equal_approx(Vector2(947.65, 285.75)))
    room.free()
    var cave: Node = load("res://TheGame/Scenes/Levels/zm1_02_shuiliandong/level_2.tscn").instantiate()
    var entrance: Area2D = cave.get("_hiddenEntrance")
    assert(entrance != null and not entrance.monitoring)
    assert(entrance.position.is_equal_approx(Vector2(2151.024558265, 615.010125734)))
    assert(entrance.get_child(0).shape.size.is_equal_approx(Vector2(499.95119367, 50.00032979)))
    var fade: AnimationPlayer = cave.get("_entranceFade")
    assert(fade != null and is_equal_approx(fade.get_animation("fade").length, 1))
    cave.free()
    print("DRAGON NATIVE RESOURCES PASS: ", count, " source collision frames, room/portal/music and water entrance bindings")
    quit()
