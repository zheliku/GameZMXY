extends SceneTree
## Render the native editor view, without custom collision artwork or overlays.

func _initialize() -> void:
    _run.call_deferred()

func _run() -> void:
    assert(Engine.is_editor_hint())
    var output: String = OS.get_cmdline_user_args()[0]
    var cases := [
        ["Monsters/Ground/demon_monkey/demon_monkey", 0.8],
        ["Monsters/Sky/bat/bat", 0.55],
        ["Monsters/Sea/shrimp_demon/shrimp_demon", 0.65],
        ["Bosses/zm1_06_garuda_cave/peng_demon_king", 0.55],
        ["Bosses/zm1_10_bull_demon_valley/bull_demon_king", 0.7],
        ["Bosses/zm1_11_hidden_dragon_palace/dragon_king", 0.7],
    ]
    for record in cases:
        var viewport := SubViewport.new()
        viewport.size = Vector2i(940, 590)
        viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
        root.add_child(viewport)
        var background := ColorRect.new()
        background.size = Vector2(940, 590)
        background.color = Color(0.13, 0.14, 0.16)
        viewport.add_child(background)
        var actor: Node2D = load("res://TheGame/Entitys/Enemies/" + record[0] + "_entity.tscn").instantiate()
        actor.position = Vector2(470, 335)
        actor.scale = Vector2.ONE * float(record[1])
        viewport.add_child(actor)
        var title := Label.new()
        title.text = String(record[0]).get_file() + "  |  native collision view"
        title.position = Vector2(24, 20)
        viewport.add_child(title)
        for frame in 6: await process_frame
        await RenderingServer.frame_post_draw
        assert(viewport.get_texture().get_image().save_png(output.path_join(String(record[0]).get_file() + ".png")) == OK)
        viewport.queue_free()
        await process_frame
    print("ENEMY NATIVE EDITOR VISUAL PASS: six representative enemy scenes")
    quit()
