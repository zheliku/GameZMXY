extends SceneTree
## Repair missing small-monster bars; --author-positions explicitly authors initial local layout.

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
        if entity.get("m_HealthBar") == null or OS.get_cmdline_user_args().has("--author-positions"):
            var bar: Control = entity.get_node_or_null("m_HealthBar")
            if bar == null:
                bar = bar_scene.instantiate()
                bar.name = "m_HealthBar"
                entity.add_child(bar)
                bar.owner = entity
            entity.set("m_HealthBar", bar)
            # Position is authored once. Runtime only updates value, visibility and fade.
            var hurt: Area2D = entity.get("m_HurtBox")
            var bounds := Rect2()
            var any := false
            for child in hurt.get_children():
                if child is CollisionShape2D and child.shape != null and not child.disabled:
                    var rect: Rect2 = hurt.transform * child.transform * child.shape.get_rect()
                    bounds = bounds.merge(rect) if any else rect
                    any = true
            assert(any and bar.get_parent() == entity)
            bar.size = Vector2(50, 5)
            bar.position = Vector2(bounds.get_center().x - 25, bounds.position.y - 13)
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
