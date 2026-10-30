extends SceneTree
## Author official acquisition heights; scene owns both acquisition dimensions; source script supplies import evidence.

func _initialize() -> void:
    _run.call_deferred()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with("_entity.tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _run() -> void:
    assert(Engine.is_editor_hint())
    var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys/Enemies", paths)
    var count := 0
    for path in paths:
        var entity: Node = load(path).instantiate()
        var identity: int = entity.get("m_EnemyEntityId")
        var records: Array = data.enemies.filter(func(e): return e.legacy_id == identity)
        assert(records.size() == 1, path + " missing source identity")
        var range_y: float = records[0].attack_range
        var detector: Area2D = entity.get("m_Perception")
        var shape: CollisionShape2D = detector.get_child(0)
        var rectangle: RectangleShape2D = shape.shape.duplicate()
        rectangle.size = Vector2(float(records[0].sight_range) * 2, range_y * 2)
        shape.position = Vector2.ZERO
        shape.shape = rectangle
        var uid := ResourceLoader.get_resource_uid(path)
        var packed := PackedScene.new()
        assert(packed.pack(entity) == OK)
        assert(ResourceSaver.save(packed, path) == OK)
        assert(ResourceSaver.set_uid(path, uid) == OK)
        entity.free()
        count += 1
    print("ENEMY DETECTION AUTHOR PASS: %d official root-distance acquisition regions" % count)
    quit()
