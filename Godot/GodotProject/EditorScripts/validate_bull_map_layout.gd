extends SceneTree
## Render every original current frame and test the visible hover contour.

func _initialize() -> void:
    _validate.call_deferred()

func _validate() -> void:
    root.size = Vector2i(940, 590)
    root.content_scale_size = Vector2i(940, 590)
    var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
    root.add_child(form)
    var button: TextureButton = form.get_node("m_Niumogu")
    button.disabled = false
    var current: Sprite2D = button.get_node("CurrentHint")
    var hover: TextureRect = button.get_node("LocationGlow")
    assert(current.global_position.is_equal_approx(Vector2(283.2, 385.05)))
    assert(current.scale.is_equal_approx(Vector2.ONE))
    assert(hover.size.is_equal_approx(Vector2(176, 125)))
    var player: AnimationPlayer = form.get_node("m_LocationAnimation")
    player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
    var folder := "res://.godot/validation/bull_review_20261101/map"
    DirAccess.make_dir_recursive_absolute(folder)
    player.play("niumogu/current")
    player.advance(0)
    for index in 19:
        player.seek(index / 24.0 + 0.000001, true, true)
        assert(current.frame == index and current.visible)
        assert(current.scale.is_equal_approx(Vector2.ONE))
        await process_frame
        await RenderingServer.frame_post_draw
        if index in [0, 9, 18]:
            root.get_texture().get_image().save_png(folder + "/current_%02d.png" % index)
    # Scan the actual alpha silhouette in the padded hover, including the shader outline.
    player.play("niumogu/hover")
    player.advance(0)
    for time in [0.0, 0.5, 1.0]:
        player.seek(time + 0.000001, true, true)
        var count := 0
        var outside_normal := 0
        for y in range(-20, 195, 3):
            for x in range(-20, 185, 3):
                var hit: bool = button._has_point(Vector2(x, y))
                if hit:
                    count += 1
                    if absf(x - button.size.x / 2) > 29 or absf(y - button.size.y / 2) > 28:
                        outside_normal += 1
        assert(count > 0 and outside_normal > 0, "Expanded hover must retain its own input area")
        assert(not button._has_point(Vector2(-100, -100)))
        await process_frame
        await RenderingServer.frame_post_draw
        root.get_texture().get_image().save_png(folder + "/hover_%02d.png" % int(time * 10))
    # Drive real GUI routing after entering through the normal image, then stay in its enlarged part.
    var entered := func():
        player.play("niumogu/hover")
        player.advance(0)
    var exited := func():
        player.play("niumogu/current")
        player.advance(0)
    button.mouse_entered.connect(entered)
    button.mouse_exited.connect(exited)
    var clicks := [0]
    button.pressed.connect(func(): clicks[0] += 1)
    exited.call()
    var entry := Vector2.ZERO
    for y in range(60, 115):
        for x in range(50, 110):
            if button._has_point(Vector2(x, y)):
                entry = Vector2(x, y)
                break
        if entry != Vector2.ZERO:
            break
    assert(entry != Vector2.ZERO)
    _move(button.global_position + entry)
    await process_frame
    await process_frame
    assert(root.gui_get_hovered_control() == button and hover.visible)
    var expanded := Vector2.ZERO
    for y in range(100, 140):
        for x in range(15, 145):
            if absf(x - 80) > 30 and button._has_point(Vector2(x, y)):
                expanded = Vector2(x, y)
                break
        if expanded != Vector2.ZERO:
            break
    assert(expanded != Vector2.ZERO)
    for index in 5:
        player.seek(index / 5.0, true, true)
        _move(button.global_position + expanded)
        await process_frame
        assert(root.gui_get_hovered_control() == button and hover.visible)
    for pressed in [true, false]:
        var click := InputEventMouseButton.new()
        click.position = button.global_position + expanded
        click.global_position = click.position
        click.button_index = MOUSE_BUTTON_LEFT
        click.pressed = pressed
        root.push_input(click, true)
        await process_frame
    assert(clicks[0] == 1, "Expanded visible image must accept native click")
    _move(Vector2(900, 50))
    await process_frame
    await process_frame
    assert(not hover.visible and current.visible and root.gui_get_hovered_control() != button)
    form.queue_free()
    await process_frame
    print("BULL MAP LAYOUT PASS: 19 source frames, official origin, native size, actual mouse hover/expanded click/exit")
    quit()

func _move(position: Vector2) -> void:
    var motion := InputEventMouseMotion.new()
    motion.position = position
    motion.global_position = position
    root.push_input(motion, true)
