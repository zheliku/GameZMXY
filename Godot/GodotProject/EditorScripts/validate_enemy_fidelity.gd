extends SceneTree
## Verify native multi-shape gaps and every small-monster scene contract.

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
    _collect("res://TheGame/Entitys/Enemies/Monsters", paths)
    for path in paths:
        var entity: Node = load(path).instantiate()
        for slot in ["m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot", "_perception", "m_HealthBar"]:
            assert(entity.get(slot) != null, path + " missing " + slot)
        assert(entity.get("_hurtBox").get("_owner") == entity)
        assert(entity.get("_animPlayer").has_animation("death"))
        assert(entity.get("_animPlayer").has_animation("hurt"))
        assert(entity.get("_animPlayer").has_animation("idle"))
        assert(entity.collision_mask & 8192 == 0, path + " blocked by camera gates")
        entity.free()
    var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var scene: Node2D = load("res://TheGame/Entitys/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_entity.tscn").instantiate()
    root.add_child(scene)
    scene.set_physics_process(false)
    var player: AnimationPlayer = scene.get("_animPlayer")
    player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
    var area: Area2D = scene.get("_hitBox")
    var samples := 0
    var gap_samples := 0
    for facing in [-1, 1]:
        scene.call("SetFacing", facing)
        player.play("attack_3")
        for frame in data.regions.size():
            player.seek(frame / 24.0 + .000001, true, true)
            assert(area.get_node("CollisionShape2D").disabled)
            var rectangles: Array[Rect2] = []
            for slot in 4:
                var shape: CollisionShape2D = area.get_node("Ice%d" % (slot + 1))
                var region = data.regions[frame][slot]
                assert(shape.disabled == (region == null))
                if region != null:
                    var expected := Rect2(region[0], region[1], region[2], region[3])
                    assert(shape.shape.size.is_equal_approx(expected.size))
                    assert(shape.position.is_equal_approx(expected.get_center()))
                    rectangles.append(expected)
                samples += 1
            # A 1x1 test shape checks actual Godot shape intersections, not a union calculation.
            var point := RectangleShape2D.new()
            point.size = Vector2.ONE
            for first in rectangles.size():
                for second in range(first + 1, rectangles.size()):
                    var a := rectangles[first]
                    var b := rectangles[second]
                    var center := (a.get_center() + b.get_center()) / 2
                    if rectangles.any(func(r): return r.grow(1).has_point(center)):
                        continue
                    for slot in 4:
                        var shape: CollisionShape2D = area.get_node("Ice%d" % (slot + 1))
                        if not shape.disabled:
                            assert(not shape.shape.collide(shape.global_transform, point,
                                Transform2D(0, area.to_global(center))), "ice gap became harmful")
                    gap_samples += 1
        player.play("idle")
        player.advance(0)
        for slot in 4:
            assert(area.get_node("Ice%d" % (slot + 1)).disabled)
    assert(gap_samples > 0)
    scene.queue_free()
    await process_frame
    print("ENEMY FIDELITY VALIDATION PASS: %d complete monsters, %d ice shape samples, %d native gap exclusions, both facings and idle reset" % [paths.size(), samples, gap_samples])
    quit()
