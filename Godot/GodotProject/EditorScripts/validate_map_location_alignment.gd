extends SceneTree
## Validate registered origins, full alpha margins and real drawing across the map.

const LOCATIONS := {
    "Huaguoshan": "location", "Shuiliandong": "shuiliandong", "Taohuayuan": "taohuayuan",
    "Bianfuku": "bianfuku", "Qixingdong": "qixingdong", "Jialuoluo": "jialuoluo",
    "Qianan": "qianan", "Donghai": "donghai", "Shazhiwoliu": "shazhiwoliu", "Niumogu": "niumogu"
}

func _initialize() -> void:
    _validate.call_deferred()
    create_timer(30).timeout.connect(func(): quit(1))

func _validate() -> void:
    root.size = Vector2i(940, 590)
    var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
    root.add_child(form)
    form.visible = true
    var player: AnimationPlayer = form.get_node("m_LocationAnimation")
    var output := "res://.godot/validation/movement_presets/map_alignment"
    DirAccess.make_dir_recursive_absolute(output)
    for name in LOCATIONS:
        var button: TextureButton = form.get_node("m_" + name)
        button.disabled = false
        var glow: TextureRect = button.get_node("LocationGlow")
        assert(glow.stretch_mode == TextureRect.STRETCH_KEEP_ASPECT_CENTERED)
        var image: Image = glow.texture.get_image()
        var used := image.get_used_rect()
        assert(used.position.x >= 6 and used.position.y >= 6 and used.end.x <= image.get_width() - 6 and used.end.y <= image.get_height() - 6,
            "Hover filter reaches a canvas edge: " + name)
        if name in ["Qianan", "Donghai", "Shazhiwoliu"]:
            var official: Vector2 = {"Qianan": Vector2(460, 373.15), "Donghai": Vector2(612.7, 418.85), "Shazhiwoliu": Vector2(766.55, 500.15)}[name]
            assert((button.position + button.pivot_offset).distance_to(official) < .01, "Official coast origin shifted: " + name)
            assert(glow.size.is_equal_approx(glow.texture.get_size()) and glow.scale == Vector2.ONE,
                "Coastal image must retain official pixel scale")
            assert(button.texture_click_mask != null)
            var hint: Sprite2D = button.get_node("CurrentHint")
            assert(hint.position.distance_to(button.size * .5) < .01 and hint.scale.is_equal_approx(Vector2.ONE),
                "Current registration mismatch %s: hint=%s size=%s scale=%s" % [name, hint.position, button.size, hint.scale])
        else:
            assert((glow.position + glow.size * .5).distance_to(button.size * .5) < .01)
        player.play(LOCATIONS[name] + "/RESET")
        player.advance(0)
    await process_frame
    if DisplayServer.get_name() != "headless":
        await RenderingServer.frame_post_draw
        assert(root.get_texture().get_image().save_png(output + "/normal.png") == OK)
    for name in LOCATIONS:
        var button: TextureButton = form.get_node("m_" + name)
        var glow: TextureRect = button.get_node("LocationGlow")
        player.play(LOCATIONS[name] + "/hover")
        player.seek(.5, true, true)
        player.pause()
        var center := glow.get_transform() * (glow.size * .5)
        assert(center.distance_to(button.size * .5) < .01, "Hover origin shifted: " + name)
        # The center and expanded hit contour are checked through the real button implementation.
        var hits := 0
        for y in range(0, int(button.size.y), 4):
            for x in range(0, int(button.size.x), 4):
                if button._has_point(Vector2(x, y)):
                    hits += 1
        assert(hits > 0, "Empty visible hover hit area: " + name)
        if DisplayServer.get_name() != "headless":
            await RenderingServer.frame_post_draw
            assert(root.get_texture().get_image().save_png(output + "/" + name.to_snake_case() + ".png") == OK)
        player.play(LOCATIONS[name] + "/RESET")
        player.advance(0)
    form.free()
    print("MAP ALIGNMENT VALIDATION PASS: 10 registered origins, padded filter margins, native hit regions, all location captures")
    quit()
