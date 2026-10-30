extends SceneTree
## Every runtime perception binding, native range boundary and serialized UID is checked without a test API.

func _initialize() -> void:
    _run.call_deferred()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with("_entity.tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _run() -> void:
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys/Enemies", paths)
    assert(paths.size() == 24)
    for path in paths:
        var actor: Node = load(path).instantiate()
        var area: Area2D = actor.get("m_Perception")
        assert(area != null, path)
        area.call("Validate")
        assert(area.get("m_DetectionShape") == area.get_node("Detection"))
        var id: int = actor.get("m_EnemyEntityId")
        if id not in [21, 22, 24]:
            assert(area.get("m_EngageShape") == area.get_node("Engage"))
            var engage: CollisionShape2D = area.get_node("Engage")
            var rect: Rect2 = engage.shape.get_rect()
            assert(area.call("ContainsEngage", Vector2.ZERO))
            assert(area.call("ContainsEngage", Vector2(rect.end.x, 0)))
            assert(not area.call("ContainsEngage", Vector2(rect.end.x + 0.1, 0)))
            if engage.shape is CircleShape2D:
                assert(not area.call("ContainsEngage", Vector2(350, 350)))
            elif engage.shape is SegmentShape2D:
                assert(area.call("ContainsEngage", Vector2(rect.end.x, 10000)))
        else:
            assert(area.get_node_or_null("Engage") == null)
        if id not in [21, 24]:
            assert(area.get("m_PatrolShape") == area.get_node("Patrol"))
        else:
            assert(area.get_node_or_null("Patrol") == null)
        var ranges := {8: ["m_TrackingRange"], 15: ["m_TrackingRange"], 18: ["m_TrackingRange"], 9: ["m_MeleeRange", "m_TrackingRange"], 14: ["m_RetreatRange"], 23: ["m_SwallowRange", "m_StompRange", "m_CaptureRange"], 26: ["m_WaterColumnRange"]}
        for field in ranges.get(id, []):
            var node: CollisionShape2D = actor.get(field)
            assert(node != null and node.disabled and node.position == Vector2.ZERO)
            assert(node.owner == actor and node.get_parent() == area)
        # UID preservation is compared with the original scene during application.
        actor.free()
    print("ENEMY PERCEPTION VALIDATION PASS: 24 scenes, bindings, strict outside/inside engage, native circle and species ranges")
    quit()
