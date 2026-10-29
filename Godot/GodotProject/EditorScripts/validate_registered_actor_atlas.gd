extends SceneTree
## Compare a compact AtlasTexture's native drawing with the full official source canvas.

func _initialize() -> void:
    create_timer(90).timeout.connect(func():
        printerr("REGISTERED ATLAS FAIL: native drawing timed out")
        quit(1))
    _run.call_deferred()

func _viewport() -> SubViewport:
    var viewport := SubViewport.new()
    viewport.size = Vector2i(1000, 1000)
    viewport.transparent_bg = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    return viewport

func _staged_frames(layer: Dictionary, actions: Array) -> SpriteFrames:
    var image := Image.load_from_file(layer.output)
    image.fix_alpha_edges()
    var atlas := ImageTexture.create_from_image(image)
    var frames := SpriteFrames.new()
    frames.remove_animation("default")
    for action in actions:
        frames.add_animation(action.name)
        for cell in layer.poses[action.name]:
            var texture := AtlasTexture.new()
            texture.atlas = atlas
            if layer.has("atlas_regions"):
                var entry: Dictionary = layer.atlas_regions[str(int(cell))]
                texture.region = Rect2(entry.region[0], entry.region[1], entry.region[2], entry.region[3])
                texture.margin = Rect2(entry.margin[0], entry.margin[1], entry.margin[2], entry.margin[3])
            else:
                texture.region = Rect2(int(cell) % int(layer.columns) * layer.cell_size[0],
                    int(cell) / int(layer.columns) * layer.cell_size[1], layer.cell_size[0], layer.cell_size[1])
            frames.add_frame(action.name, texture)
    return frames

func _apply_additive(viewport: SubViewport, sprite: Sprite2D) -> void:
    var background := ColorRect.new()
    background.color = Color(.16, .28, .38)
    background.size = Vector2(viewport.size)
    viewport.add_child(background)
    viewport.move_child(background, 0)
    var material := CanvasItemMaterial.new()
    material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
    sprite.material = material

func _run() -> void:
    var args := OS.get_cmdline_user_args()
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var entries: Array = manifest.actors.duplicate()
    entries.append_array(manifest.get("effects", []))
    var actor: Dictionary = entries.filter(func(value): return value.name == args[1])[0]
    var layer: Dictionary = actor.layers[0]
    var frames: SpriteFrames = _staged_frames(layer, actor.actions) if args[2] == "--staged" else load(args[2])
    var actual_view := _viewport()
    var expected_view := _viewport()
    var actual := Sprite2D.new()
    actual.position = Vector2(500, 500)
    actual.offset = Vector2(layer.sprite_offset[0], layer.sprite_offset[1])
    actual.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    actual_view.add_child(actual)
    var expected := Sprite2D.new()
    expected.position = actual.position
    expected.offset = actual.offset
    expected.texture_filter = actual.texture_filter
    expected_view.add_child(expected)
    if actor.get("blend_mode", "mix") == "add":
        _apply_additive(actual_view, actual)
        _apply_additive(expected_view, expected)
    var count := 0
    for action in actor.actions:
        for frame in int(action.frames):
            var image := Image.load_from_file(String(action.input).path_join("%d.png" % (frame + 1)))
            var canvas := Image.create_empty(int(layer.cell_size[0]), int(layer.cell_size[1]), false, Image.FORMAT_RGBA8)
            var origin := Vector2(layer.sprite_offset[0], layer.sprite_offset[1]) - Vector2(layer.cell_size[0], layer.cell_size[1]) * .5
            var position := Vector2i(floor(action.bounds[0] + action.matrix[4] - origin.x), floor(action.bounds[1] + action.matrix[5] - origin.y))
            # The original uniform pack stores the complete visible bounding rect;
            # invisible RGB outside it is not part of either registered atlas.
            var used := image.get_used_rect()
            canvas.blit_rect(image, used, position + used.position)
            # Match Godot's existing PNG import setting, including low-alpha edge RGB.
            canvas.fix_alpha_edges()
            actual.texture = frames.get_frame_texture(action.name, frame)
            expected.texture = ImageTexture.create_from_image(canvas)
            if actual.texture.get_size() != Vector2(layer.cell_size[0], layer.cell_size[1]):
                printerr("REGISTERED ATLAS FAIL: virtual frame size changed")
                quit(1)
                return
            await process_frame
            await RenderingServer.frame_post_draw
            var first := actual_view.get_texture().get_image()
            var second := expected_view.get_texture().get_image()
            if first.get_data() != second.get_data():
                first.save_png(args[3] + "/actual_%s_%d.png" % [action.name, frame + 1])
                second.save_png(args[3] + "/expected_%s_%d.png" % [action.name, frame + 1])
                printerr("REGISTERED ATLAS FAIL: %s/%d source registration differs" % [action.name, frame + 1])
                quit(1)
                return
            count += 1
            if actor.get("blend_mode", "mix") == "add" and frame == int(action.frames) / 2:
                first.save_png(args[3] + "/additive_%s.png" % actor.name)
    print("REGISTERED ATLAS PASS: %s, %d actual rendered frames retain complete official canvas and root" % [actor.name, count])
    actual_view.queue_free()
    expected_view.queue_free()
    await process_frame
    quit()
