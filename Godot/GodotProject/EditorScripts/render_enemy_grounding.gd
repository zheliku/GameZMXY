extends SceneTree
## Render actual native animation samples at a fixed body origin and ground line.

func _initialize() -> void:
    _run.call_deferred()

func _collect(directory: String, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(directory):
        if file.ends_with("_entity.tscn"):
            paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        _collect(directory.path_join(child), paths)

func _run() -> void:
    var output := OS.get_cmdline_user_args()[0]
    DirAccess.make_dir_recursive_absolute(output)
    var viewport := SubViewport.new()
    viewport.size = Vector2i(350, 250)
    viewport.transparent_bg = false
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    var background := ColorRect.new()
    background.color = Color("25333e")
    background.size = Vector2(350, 250)
    viewport.add_child(background)
    var sprite := Sprite2D.new()
    sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    viewport.add_child(sprite)
    var line := Line2D.new()
    line.add_point(Vector2(0, 220))
    line.add_point(Vector2(350, 220))
    line.width = 1
    line.default_color = Color("4ee997")
    viewport.add_child(line)
    var label := Label.new()
    label.add_theme_font_size_override("font_size", 12)
    viewport.add_child(label)
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys/Enemies", paths)
    var samples := 0
    for path in paths:
        var actor: Node2D = load(path).instantiate()
        actor.visible = false
        root.add_child(actor)
        actor.set_physics_process(false)
        var body: AnimatedSprite2D = actor.get("_body")
        body.stop()
        var player: AnimationPlayer = actor.get("_animPlayer")
        player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        var actions: Array[String] = []
        for name in player.get_animation_list():
            if name != "RESET":
                actions.append(name)
        var physics_shape: CollisionShape2D = actor.get_node("m_BodyShape")
        var aabb := physics_shape.shape.get_rect()
        var bottom := physics_shape.position.y + (aabb.size.x / 2 if absf(physics_shape.rotation) > 1 else aabb.end.y)
        var hurt := actor.get("_hurtBox") as Area2D
        var scale := minf(1.0, 160.0 / maxf(aabb.size.y, 1))
        var sheet := Image.create_empty(1050, 250 * actions.size(), false, Image.FORMAT_RGBA8)
        for row in actions.size():
            var name := actions[row]
            var animation := player.get_animation(name)
            var frames := maxi(1, roundi(animation.length * 24))
            for col in 3:
                var frame: int = [0, frames / 2, frames - 1][col]
                player.play(name)
                player.seek(minf(frame / 24.0 + .000001, animation.length - .000001), true, true)
                sprite.texture = body.sprite_frames.get_frame_texture(body.animation, body.frame)
                sprite.offset = body.offset
                sprite.scale = body.scale * scale
                sprite.rotation = body.rotation
                sprite.position = Vector2(175, 220 - bottom * scale) + body.position * scale
                label.text = "%s / %s #%d\nroot ground +%.2f, scale=%.2f" % [path.get_file().trim_suffix("_entity.tscn"), name, frame + 1, bottom, scale]
                await process_frame
                await RenderingServer.frame_post_draw
                sheet.blit_rect(viewport.get_texture().get_image(), Rect2i(0, 0, 350, 250), Vector2i(col * 350, row * 250))
                samples += 1
        sheet.save_png(output.path_join(path.get_file().trim_suffix("_entity.tscn") + "_native.png"))
        actor.queue_free()
        await process_frame
    viewport.queue_free()
    await process_frame
    print("ENEMY GROUND RENDER PASS: %d native actor scenes, %d grounded animation samples including death" % [paths.size(), samples])
    quit()
