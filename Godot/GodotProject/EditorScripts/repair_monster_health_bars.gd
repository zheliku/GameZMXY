extends SceneTree
## Complete the shared small-monster presentation contract without changing actor artwork.

func _initialize() -> void:
    _repair.call_deferred()

func _repair() -> void:
    assert(Engine.is_editor_hint())
    var bar_scene: PackedScene = load("res://TheGame/UIs/Battle/monster_health_bar.tscn")
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys/Enemies/Monsters", paths)
    var changed := 0
    for path in paths:
        var uid := ResourceLoader.get_resource_uid(path)
        var entity: Node = load(path).instantiate()
        if entity.get("m_HealthBar") == null:
            var bar: Control = entity.get_node_or_null("m_HealthBar")
            if bar == null:
                bar = bar_scene.instantiate()
                bar.name = "m_HealthBar"
                entity.add_child(bar)
                bar.owner = entity
            entity.set("m_HealthBar", bar)
            var packed := PackedScene.new()
            assert(packed.pack(entity) == OK)
            assert(ResourceSaver.save(packed, path) == OK)
            assert(ResourceSaver.set_uid(path, uid) == OK)
            print("MONSTER HEALTH BAR REPAIRED: " + path)
            changed += 1
        entity.free()
    print("MONSTER HEALTH AUTHOR PASS: checked=%d repaired=%d" % [paths.size(), changed])
    quit()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with("_entity.tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)
