extends SceneTree
## Finalize raw actors; --refresh-bindings only resaves existing scene bindings without replacing geometry.

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
    var count := 0
    for path in paths:
        var scene: PackedScene = load(path)
        var actor := scene.instantiate()
        var existing := actor.get_node_or_null("Perception") != null
        if existing and "--refresh-bindings" not in OS.get_cmdline_user_args():
            actor.free()
            continue
        var uid := ResourceLoader.get_resource_uid(path)
        if not existing:
            load("res://EditorScripts/enemy_perception_authoring.gd").configure(actor)
        var packed := PackedScene.new()
        assert(packed.pack(actor) == OK)
        assert(ResourceSaver.save(packed, path) == OK)
        if uid != -1:
            assert(ResourceSaver.set_uid(path, uid) == OK)
        count += 1
        actor.free()
    print("ENEMY PERCEPTION REGISTER PASS: %d new imports" % count)
    quit()
