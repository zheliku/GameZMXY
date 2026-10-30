extends SceneTree
## Compare every consumed AI shape with a separately extracted original-source reference.

func _initialize() -> void:
    _run.call_deferred()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with("_entity.tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _run() -> void:
    var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys/Enemies", paths)
    assert(paths.size() == 24 and data.actors.size() == 24)
    var checks := 0
    for path in paths:
        var actor: Node = load(path).instantiate()
        var id: int = actor.get("m_EnemyEntityId")
        var expected: Dictionary = data.actors.filter(func(row): return int(row.id) == id)[0]
        var area: Area2D = actor.get("m_Perception")
        area.call("Validate")
        var detection: CollisionShape2D = area.get_node("Detection")
        assert(detection.position == Vector2.ZERO)
        assert(detection.shape.size == Vector2(expected.detection[0], expected.detection[1]) * 2, str(id) + " detection")
        if id not in [21, 22, 24]:
            var engage: CollisionShape2D = area.get_node("Engage")
            var rect := engage.shape.get_rect()
            assert(engage.shape.get_class() == expected.engage.shape, str(id) + " shape semantics")
            assert(is_equal_approx(rect.end.x, float(expected.engage.x)), str(id) + " horizontal engage")
            if expected.engage.y != null and not engage.shape is CircleShape2D:
                assert(is_equal_approx(rect.end.y, float(expected.engage.y)), str(id) + " vertical engage")
            assert(area.call("ContainsEngage", Vector2(rect.end.x, 0)))
            assert(not area.call("ContainsEngage", Vector2(rect.end.x + 0.1, 0)))
            assert(area.call("ContainsEngage", Vector2(-rect.end.x, 0)))
            assert(not area.call("ContainsEngage", Vector2(-rect.end.x - 0.1, 0)))
            if engage.shape is SegmentShape2D:
                assert(area.call("ContainsEngage", Vector2(0, 10000)))
            elif engage.shape is CircleShape2D:
                assert(not area.call("ContainsEngage", Vector2(rect.end.x, rect.end.y)))
            else:
                assert(area.call("ContainsEngage", Vector2(0, rect.end.y)))
                assert(not area.call("ContainsEngage", Vector2(0, rect.end.y + 0.1)))
            checks += 8
        else:
            assert(area.get_node_or_null("Engage") == null)
        if id not in [21, 24]:
            var patrol: CollisionShape2D = area.get_node("Patrol")
            assert(patrol.shape is SegmentShape2D)
            assert(patrol.shape.a.x == float(expected.patrol[0]) and patrol.shape.b.x == float(expected.patrol[1]))
        else:
            assert(area.get_node_or_null("Patrol") == null)
        for label in expected.extra:
            var node: CollisionShape2D = area.get_node(label)
            assert(node.disabled and node.position == Vector2.ZERO)
            var rect := node.shape.get_rect()
            if label == "Tracking":
                assert(node.shape is SegmentShape2D and rect.size.x == 0)
                assert(rect.end.y == float(expected.extra[label][1]))
            else:
                assert(rect.end.x == float(expected.extra[label][0]))
            checks += 1
        for child in area.get_children():
            if child is CollisionShape2D:
                assert(child.debug_color.a <= 0.05 and not child.get("debug_fill"))
        actor.free()
    print("ENEMY OFFICIAL SPACE PASS: 24 source-identified species, %d boundary/semantic checks, unused fixed-actor ranges removed" % checks)
    quit()
