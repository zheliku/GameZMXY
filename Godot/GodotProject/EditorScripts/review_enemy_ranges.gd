extends SceneTree
## Native scene range/shape authoring, driven only by independently checked official expressions.

const CollisionStyles = preload("res://EditorScripts/enemy_collision_authoring.gd")

func _initialize() -> void:
    _run.call_deferred()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with(".tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _horizontal(node: CollisionShape2D, width: float) -> void:
    var shape := SegmentShape2D.new()
    shape.a = Vector2(-width, 0)
    shape.b = Vector2(width, 0)
    node.shape = shape
    node.position = Vector2.ZERO
    node.disabled = true

func _tracking(actor: Node, area: Area2D, size: Vector2) -> void:
    var node := area.get_node_or_null("Tracking") as CollisionShape2D
    if node == null:
        node = CollisionShape2D.new()
        node.name = "Tracking"
        area.add_child(node)
        node.owner = actor
    var shape := SegmentShape2D.new()
    shape.a = Vector2(0, -size.y / 2)
    shape.b = Vector2(0, size.y / 2)
    node.shape = shape
    node.disabled = true
    actor.set("m_TrackingRange", node)

func _run() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 2)
    var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys/Enemies", paths)
    var report: Array = []
    for path in paths:
        var actor: Node = load(path).instantiate()
        if not path.ends_with("_entity.tscn"):
            CollisionStyles.apply(actor, "hit")
            var effect := PackedScene.new()
            var uid := ResourceLoader.get_resource_uid(path)
            assert(effect.pack(actor) == OK and ResourceSaver.save(effect, path) == OK)
            if uid != -1: assert(ResourceSaver.set_uid(path, uid) == OK)
            actor.free()
            continue
        var id: int = actor.get("m_EnemyEntityId")
        var expected: Dictionary = data.actors.filter(func(row): return int(row.id) == id)[0]
        var area: Area2D = actor.get("m_Perception")
        # The source has no target-height condition for these horizontal decisions.
        if id in [14, 23, 26]:
            _horizontal(area.get_node("Engage"), float(expected.engage.x))
        for label in expected.extra:
            if label == "Tracking" and id in [8, 15, 18]:
                _tracking(actor, area, Vector2(expected.extra[label][0], expected.extra[label][1]) * 2)
            elif label != "Tracking" and id != 9:
                if label == "Capture" and area.get_node_or_null(label) == null:
                    var capture := CollisionShape2D.new()
                    capture.name = label
                    area.add_child(capture)
                    capture.owner = actor
                    actor.set("m_CaptureRange", capture)
                _horizontal(area.get_node(label), float(expected.extra[label][0]))
        if id not in [21, 24]:
            var patrol: CollisionShape2D = area.get_node("Patrol")
            var segment := SegmentShape2D.new()
            segment.a = Vector2(float(expected.patrol[0]), 0)
            segment.b = Vector2(float(expected.patrol[1]), 0)
            patrol.shape = segment
            patrol.position = Vector2.ZERO
        if id == 9:
            _tracking(actor, area, Vector2(0, 1600))
        if id in [21, 22, 24]:
            area.set("m_EngageShape", null)
            if area.get_node_or_null("Engage") != null: area.get_node("Engage").free()
        if id in [21, 24]:
            area.set("m_PatrolShape", null)
            if area.get_node_or_null("Patrol") != null: area.get_node("Patrol").free()
        CollisionStyles.apply(actor)
        var packed := PackedScene.new()
        assert(packed.pack(actor) == OK)
        var uid := ResourceLoader.get_resource_uid(path)
        assert(ResourceSaver.save(packed, path) == OK)
        if uid != -1: assert(ResourceSaver.set_uid(path, uid) == OK)
        var ranges: Dictionary = {}
        for node in area.get_children():
            if node is CollisionShape2D:
                var bounds: Rect2 = node.transform * node.shape.get_rect()
                ranges[String(node.name)] = {"type": node.shape.get_class(), "rect": [bounds.position.x, bounds.position.y, bounds.size.x, bounds.size.y], "color": [node.debug_color.r, node.debug_color.g, node.debug_color.b, node.debug_color.a]}
        report.append({"id": id, "scene": path, "ranges": ranges})
        actor.free()
    assert(report.size() == 24)
    var output := FileAccess.open(args[1], FileAccess.WRITE)
    output.store_string(JSON.stringify(report, "  "))
    output.close()
    print("ENEMY RANGE REVIEW AUTHOR PASS: 24 native scenes, source-specific ranges, quiet semantic colors")
    quit()
