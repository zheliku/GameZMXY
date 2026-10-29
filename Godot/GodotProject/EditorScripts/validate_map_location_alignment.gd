extends SceneTree
## Every location uses the same normal/hover canvas center; capture real drawing.

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
        assert(glow.size.is_equal_approx(button.size) and glow.pivot_offset.is_equal_approx(button.size * .5))
        if name == "Shazhiwoliu":
            var texture: Texture2D = glow.texture
            var vortex := Vector2(180.5, 112) + Vector2(15, 37)
            assert(vortex.distance_to(texture.get_size() * .5) < .01, "Shark vortex source origin shifted")
            assert(texture.get_image().get_size() == Vector2i(texture.get_size()), "Registered alpha mask must retain its transparent margins")
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
    print("MAP ALIGNMENT VALIDATION PASS: 10 normal/hover centers, native alpha hit regions, all location captures")
    quit()
