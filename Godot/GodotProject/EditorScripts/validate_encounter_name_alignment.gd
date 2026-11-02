extends SceneTree
## Render the shared boss bar with short/long names and measure actual visible text.

func _initialize() -> void:
    _check.call_deferred()

func _check() -> void:
    root.size = Vector2i(940, 590)
    var scene: Control = load("res://TheGame/UIs/Battle/encounter_health_bar.tscn").instantiate()
    root.add_child(scene)
    var label: Label = scene.get_node("Name")
    var bar: TextureProgressBar = scene.get_node("Health")
    var output := "res://.godot/validation/dragon_review_20261101/health_bar"
    DirAccess.make_dir_recursive_absolute(output)
    assert(label.horizontal_alignment == HORIZONTAL_ALIGNMENT_RIGHT)
    assert(label.position.x + label.size.x > bar.position.x)
    for name in ["龙王", "猕猴王", "禺狨王", "蝙蝠洞石像", "狮驼王", "彭魔王", "鲛魔王", "鲨魔王", "牛魔王", "大猩猩", "龟丞相"]:
        scene.call("SetValue", name, 600, 1000)
        await process_frame
        await RenderingServer.frame_post_draw
        root.get_texture().get_image().save_png(output + "/" + name + ".png")
        assert(label.get_minimum_size().x <= label.size.x)
        assert(is_equal_approx(label.position.x + label.size.x, 90.0), "Long name shifted the fixed right edge")
        assert(is_equal_approx(scene.get_global_rect().get_center().x, 470.0))
    scene.queue_free()
    await process_frame
    print("BOSS NAME ALIGNMENT PASS: shared right edge, left-cap overlap, 11 name renders including 5-character name, group centered")
    quit()
