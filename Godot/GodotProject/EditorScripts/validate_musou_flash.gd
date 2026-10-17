extends SceneTree
## 使用真实渲染器验证原版无双逐帧透明度；HUD 不再使用裁剪 shader。


func _initialize() -> void:
    create_timer(20.0).timeout.connect(func(): quit(1))
    _run.call_deferred()


## 比较正式 HUD 的原始纹理与 GPU 输出，确认光晕边角透明且动画保持 24fps 循环。
func _run() -> void:
    if DisplayServer.get_name() == "headless":
        push_error("MUSOU VISUAL FAIL: 需要真实渲染器。")
        quit(1)
        return
    var hud: Node = load("res://TheGame/UIs/BattleHud.tscn").instantiate()
    var source: AnimatedSprite2D = hud.get_node("MenuPanel/m_WsMax")
    var frames := source.sprite_frames
    var animation := source.animation
    assert(source.material == null, "官方动画仍使用旧 shader")
    assert(frames.get_animation_loop(animation))
    assert(frames.get_animation_speed(animation) == 24.0)
    hud.free()
    var count := frames.get_frame_count(animation)
    assert(count == 19, "官方无双动画帧数错误")
    var viewport := SubViewport.new()
    viewport.size = Vector2i(count * 84, 84)
    viewport.transparent_bg = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    for index in range(count):
        var sprite := Sprite2D.new()
        sprite.centered = false
        sprite.texture = frames.get_frame_texture(animation, index)
        sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
        sprite.position = Vector2(index * 84, 0)
        viewport.add_child(sprite)
    await process_frame
    await RenderingServer.frame_post_draw
    var result := viewport.get_texture().get_image()
    var pixels := 0
    for index in range(count):
        var before: Image = frames.get_frame_texture(animation, index).get_image()
        assert(before.get_size() == Vector2i(78, 78))
        # 原版最亮帧的柔光会延伸到角落（最高 32/255），不能把低 alpha 柔光误判为底色。
        for corner in [Vector2i(0, 0), Vector2i(77, 0), Vector2i(0, 77), Vector2i(77, 77)]:
            assert(before.get_pixelv(corner).a < 0.15, "源动画带不透明方形背景")
        for y in range(78):
            for x in range(78):
                var alpha := before.get_pixel(x, y).a
                assert(absf(result.get_pixel(index * 84 + x, y).a - alpha) < 0.015, "GPU 改变原版透明轮廓")
                if alpha > 0.5:
                    pixels += 1
    assert(pixels > 10000, "无双动画不可见")
    DirAccess.make_dir_recursive_absolute("res://.godot/validation")
    result.save_png("res://.godot/validation/musou_flash.png")
    print("MUSOU VISUAL PASS: 官方 19 帧、24fps、透明光晕；无 shader，全部像素 alpha 通过。")
    quit()
