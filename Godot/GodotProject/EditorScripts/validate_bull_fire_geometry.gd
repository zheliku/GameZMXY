extends SceneTree
## Check current flame geometry, the warning gap and actual upper-flame contacts.

func _initialize() -> void:
    _validate.call_deferred()

func _validate() -> void:
    var args := OS.get_cmdline_user_args()
    var frames: Array = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var effect: Node2D = load("res://TheGame/Entitys/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire_effect.tscn").instantiate()
    root.add_child(effect)
    var player: AnimationPlayer = effect.get_node("AnimationPlayer")
    var shape: CollisionShape2D = effect.get_node("HitBox/Shape")
    var sprite: AnimatedSprite2D = effect.get_node("Sprite")
    player.play("play")
    var probe := RectangleShape2D.new()
    probe.size = Vector2(1, 1)
    for frame in 36:
        player.seek((frame + 0.001) / 24.0, true, true)
        assert(sprite.frame == frame)
        var expected = frames[frame]
        assert(shape.disabled == (expected == null), "Wrong active source frame: " + str(frame + 1))
        if expected == null:
            continue
        assert(shape.shape.size.is_equal_approx(Vector2(expected[2], expected[3])))
        assert(shape.position.is_equal_approx(Vector2(expected[0] + expected[2] / 2, expected[1] + expected[3] / 2)))
        assert(shape.shape.collide(shape.transform, probe, Transform2D(0, Vector2(0, -200))), "Upper visible flame must hit")
        assert(not shape.shape.collide(shape.transform, probe, Transform2D(0, Vector2(0, expected[1] - 2))), "Above source flame must miss")
    effect.queue_free()
    await process_frame
    print("BULL FIRE GEOMETRY PASS: 36 source frames, warning harmless, actual upper-flame hits and top-edge misses")
    quit()
