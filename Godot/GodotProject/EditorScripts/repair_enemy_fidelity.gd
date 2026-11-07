extends SceneTree
## Native scene/animation authoring; preserves resource UIDs and existing methods.

func _initialize() -> void:
    _run.call_deferred()

func _save_scene(entity: Node, path: String) -> void:
    var uid := ResourceLoader.get_resource_uid(path)
    var packed := PackedScene.new()
    assert(packed.pack(entity) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with(".tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _value(animation: Animation, path: NodePath, times: Array, values: Array) -> void:
    var track := animation.find_track(path, Animation.TYPE_VALUE)
    if track >= 0:
        animation.remove_track(track)
    track = animation.add_track(Animation.TYPE_VALUE)
    animation.track_set_path(track, path)
    animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
    for i in values.size():
        if i == 0 or values[i] != values[i - 1]:
            animation.track_insert_key(track, times[i], values[i])

func _run() -> void:
    assert(Engine.is_editor_hint())
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys", paths)
    var migrated := 0
    # The Area now owns all its direct shapes; remove the obsolete single-shape export.
    for path in paths:
        var source := FileAccess.get_file_as_string(path)
        if not "_shape = NodePath" in source or "BullDemonFireNode.cs" in source or "BatCaveSpikeNode.cs" in source:
            continue
        if not ("SkillEffectNode.cs" in source or "ShrimpProjectileEffectNode.cs" in source or "LionCamelPoisonCloudNode.cs" in source):
            continue
        var effect: Node = load(path).instantiate()
        _save_scene(effect, path)
        effect.free()
        migrated += 1

    var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var path := "res://TheGame/Entitys/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_entity.tscn"
    var entity: Node = load(path).instantiate()
    var hitbox: Area2D = entity.get("_hitBox")
    var player: AnimationPlayer = entity.get("_animPlayer")
    var library := player.get_animation_library("")
    var ice := library.get_animation("attack_3")
    var times: Array = []
    for frame in data.regions.size():
        times.append(frame / 24.0)
    for slot in 4:
        var name := "Ice%d" % (slot + 1)
        var shape: CollisionShape2D = hitbox.get_node_or_null(name)
        if shape == null:
            shape = CollisionShape2D.new()
            shape.name = name
            shape.shape = RectangleShape2D.new()
            shape.disabled = true
            hitbox.add_child(shape)
            shape.owner = entity
        var shape_path := String(entity.get_path_to(shape))
        for animation_name in library.get_animation_list():
            _value(library.get_animation(animation_name), NodePath(shape_path + ":disabled"), [0.0], [true])
        var disabled: Array = []
        var positions: Array = []
        var shapes: Array = []
        for row in data.regions:
            var region = row[slot]
            disabled.append(region == null)
            positions.append(Vector2.ZERO if region == null else Vector2(region[0] + region[2] / 2, region[1] + region[3] / 2))
            var rectangle := RectangleShape2D.new()
            rectangle.size = Vector2.ONE if region == null else Vector2(region[2], region[3])
            shapes.append(rectangle)
        _value(ice, NodePath(shape_path + ":disabled"), times, disabled)
        _value(ice, NodePath(shape_path + ":position"), times, positions)
        _value(ice, NodePath(shape_path + ":shape"), times, shapes)
    # The shared primary shape remains for the two other body attacks, not the ice group.
    var primary_path := String(entity.get_path_to(hitbox.get_node("CollisionShape2D")))
    _value(ice, NodePath(primary_path + ":disabled"), [0.0], [true])
    for suffix in [":position", ":shape"]:
        var track := ice.find_track(NodePath(primary_path + suffix), Animation.TYPE_VALUE)
        if track >= 0:
            ice.remove_track(track)
    var library_path := library.resource_path
    var uid := ResourceLoader.get_resource_uid(library_path)
    assert(ResourceSaver.save(library, library_path) == OK)
    assert(ResourceSaver.set_uid(library_path, uid) == OK)
    _save_scene(entity, path)
    entity.free()
    print("ENEMY FIDELITY AUTHOR PASS: %d effect bindings, Jiao 45 frames/four independent ice shapes" % migrated)
    quit()
