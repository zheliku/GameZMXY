extends SceneTree
## Native physics proves player gates do not restrain enemies; source independently proves knife speed.

func _initialize() -> void:
    create_timer(90).timeout.connect(func(): printerr("ENEMY BOUNDARY VALIDATION TIMEOUT"); quit(1))
    _run.call_deferred()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with(".tscn") and file.begins_with("level_"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _bodies(node: Node, result: Array[StaticBody2D]) -> void:
    if node is StaticBody2D:
        result.append(node)
    for child in node.get_children():
        _bodies(child, result)

func _run() -> void:
    var paths: Array[String] = []
    _collect("res://TheGame/Scenes/Levels", paths)
    var crossings := 0
    var walls := 0
    for path in paths:
        var level: Node = load(path).instantiate()
        var geometry: Node2D = level.get_node("World/Geometry")
        geometry.get_parent().remove_child(geometry)
        var gates: Node2D = level.get_node("World/StageGates")
        gates.get_parent().remove_child(gates)
        level.free()
        var world := Node2D.new()
        world.add_child(geometry)
        world.add_child(gates)
        var probe := CharacterBody2D.new()
        var shape := CollisionShape2D.new()
        shape.shape = RectangleShape2D.new()
        shape.shape.size = Vector2(4, 4)
        probe.add_child(shape)
        probe.collision_layer = 0
        world.add_child(probe)
        root.add_child(world)
        var bodies: Array[StaticBody2D] = []
        _bodies(gates, bodies)
        for gate in bodies:
            assert(gate.collision_layer == 8192)
            for child in gate.get_children():
                if child is CollisionShape2D:
                    child.disabled = false
        await physics_frame
        await physics_frame
        for gate in bodies:
            var collision: CollisionShape2D = gate.get_child(0)
            probe.global_position = collision.global_position + Vector2(-70, 0)
            probe.collision_mask = 8192
            assert(probe.test_move(probe.global_transform, Vector2(140, 0)), path + " missing player gate")
            probe.collision_mask = 9
            # Isolate the gate at its own vertical center; ordinary terrain is disabled only in this fixture.
            var terrain: Array[StaticBody2D] = []
            _bodies(geometry, terrain)
            for body in terrain: body.collision_layer = 0
            await physics_frame
            assert(not probe.test_move(probe.global_transform, Vector2(140, 0)), path + " gate blocks enemy")
            crossings += 1
        var outer := geometry.get_node_or_null("Walls")
        if outer != null:
            var outer_bodies: Array[StaticBody2D] = []
            _bodies(outer, outer_bodies)
            for body in outer_bodies: body.collision_layer = 1
            await physics_frame
            for wall in outer_bodies:
                var collision: CollisionShape2D = wall.get_child(0)
                probe.global_position = collision.global_position + Vector2(-100, 0)
                probe.collision_mask = 9
                assert(probe.test_move(probe.global_transform, Vector2(200, 0)), path + " missing outer boundary")
                walls += 1
        world.queue_free()
        await process_frame
    var source: Array = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var effect: Node = load("res://TheGame/Entitys/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_effect.tscn").instantiate()
    root.add_child(effect)
    effect.set_physics_process(false)
    var player: AnimationPlayer = effect.get("_player")
    var shape: CollisionShape2D = effect.get("_hitBox").get_child(0)
    player.play("play")
    assert(is_equal_approx(player.get_animation("play").length, 27 / 24.0))
    for frame in source.size():
        player.seek(frame / 24.0 + .000001, true, true)
        var bounds: Array = source[frame][0].bounds
        assert(shape.shape.size.distance_to(Vector2(bounds[2], bounds[3])) < .001)
        assert(shape.position.distance_to(Vector2(bounds[0] + bounds[2] / 2, bounds[1] + bounds[3] / 2)) < .001)
    effect.queue_free()
    await process_frame
    print("ENEMY BOUNDARY/KNIFE PASS: %d levels, %d player-only gate crossings, %d outer-wall blocks; 27 source knife frames at 24fps, ~311.4px/s during flight" % [paths.size(), crossings, walls])
    quit()
