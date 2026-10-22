extends SceneTree
## Compare every live wait frame with registered original art; render all loadouts.

const SHEETS := ["body_bare", "body_pilgrim", "weapon_default", "weapon_pilgrim"]
const SAMPLES := [1, 7, 10, 61, 63, 65, 66, 67, 75, 84, 85, 86, 100]
var failed := false

func _initialize() -> void:
    _run.call_deferred()

func _check(condition: bool, message: String) -> void:
    if not condition:
        failed = true
        push_error(message)

## Reconstruct the original registered SVG layer independently of atlas indices.
func _source_frame(variant: Dictionary, frame: int) -> Image:
    var base: String = variant.input["529"].path_join("%d_" % frame)
    var image := Image.create_empty(200, 200, false, Image.FORMAT_RGBA8)
    if variant.kind == "body":
        _check(image.load_svg_from_string(FileAccess.get_file_as_string(base + "body.svg")) == OK, "Unreadable body source")
        return image
    var part := 0
    while FileAccess.file_exists(base + "weapon_%d.svg" % part):
        var fragment := Image.new()
        var foreground := Image.new()
        _check(fragment.load_svg_from_string(FileAccess.get_file_as_string(base + "weapon_%d.svg" % part)) == OK, "Unreadable weapon source")
        _check(foreground.load_svg_from_string(FileAccess.get_file_as_string(base + "occluder_%d.svg" % part)) == OK, "Unreadable foreground source")
        for y in range(200):
            for x in range(200):
                var color := fragment.get_pixel(x, y)
                color.a *= 1.0 - foreground.get_pixel(x, y).a
                fragment.set_pixel(x, y, color)
        image.blend_rect(fragment, Rect2i(0, 0, 200, 200), Vector2i.ZERO)
        part += 1
    return image

## Ignore invisible RGB; the standard import filter repairs faint alpha borders.
func _same_visible_pixels(actual: Image, expected: Image) -> bool:
    for y in range(200):
        for x in range(200):
            var a := actual.get_pixel(x, y)
            var b := expected.get_pixel(x, y)
            if a.a != b.a or (a.a > 0 and a != b):
                return false
    return true

func _run() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 2:
        push_error("Expected verified base export manifest and idle manifest")
        quit(1)
        return
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
    _check(source.source_sha256 == manifest.source_sha256 and source.fps == 24 and manifest.frames.size() == 100, "Unexpected original source or timing")
    var hero = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
    hero.process_mode = Node.PROCESS_MODE_DISABLED
    root.add_child(hero)
    var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
    var body: Sprite2D = hero.get_node("m_Body")
    var weapon: Sprite2D = hero.get_node("m_Weapon")
    var hit: CollisionShape2D = hero.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D")
    var preview = load("res://TheGame/UIs/Inventory/hero_preview.tscn").instantiate()
    preview.process_mode = Node.PROCESS_MODE_DISABLED
    root.add_child(preview)
    var preview_player: AnimationPlayer = preview.get_node("AnimationPlayer")
    var preview_body: Sprite2D = preview.get_node("Body")
    var preview_weapon: Sprite2D = preview.get_node("Weapon")
    var idle := player.get_animation("idle1")
    _check(is_equal_approx(idle.length, 100.0 / 24) and idle.loop_mode == Animation.LOOP_LINEAR and not player.has_animation("idle2"), "Combat wait is not the complete original loop")
    _check(is_equal_approx(preview_player.get_animation("idle").length, idle.length), "Preview wait speed differs")
    var textures := {}
    for name in SHEETS:
        textures[name] = load("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_" + name + ".png")
        var image: Image = textures[name].get_image()
        image.convert(Image.FORMAT_RGBA8)
        var raw := Image.load_from_file("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_" + name + ".png")
        raw.convert(Image.FORMAT_RGBA8)
        _check(image.get_size() == Vector2i(1200, int(manifest.rows) * 200), "Unexpected atlas grid")
        var variant: Dictionary = source.variants.filter(func(item): return item.name == name)[0]
        # Every source frame is compared to the cell actually used by the live player.
        for frame in range(1, 101):
            player.play("idle1")
            player.seek((frame - 1) / 24.0 + .00001, true)
            var index := body.frame if variant.kind == "body" else weapon.frame
            var crop := image.get_region(Rect2i(index % 6 * 200, index / 6 * 200, 200, 200))
            var expected := _source_frame(variant, frame)
            _check(raw.get_region(Rect2i(index % 6 * 200, index / 6 * 200, 200, 200)).get_data() == expected.get_data(),
                "%s source pixels differ at frame %d" % [name, frame])
            expected.fix_alpha_edges()
            _check(_same_visible_pixels(crop, expected), "%s imported pixels differ at frame %d" % [name, frame])
    for frame in range(100):
        preview_player.play("idle")
        preview_player.seek(frame / 24.0 + .00001, true)
        player.play("idle1")
        player.seek(frame / 24.0 + .00001, true)
        _check(body.frame == weapon.frame and body.frame == preview_body.frame and body.frame == preview_weapon.frame and hit.disabled,
            "Wait layers/preview differ or attack collision enabled at frame %d" % (frame + 1))
    hero.queue_free()
    preview.queue_free()

    var viewport := SubViewport.new()
    viewport.size = Vector2i(1600, 200)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    var background := ColorRect.new()
    background.size = viewport.size
    background.color = Color(.18, .24, .27)
    viewport.add_child(background)
    var actors := []
    # Two directions for every independently replaceable weapon/body pairing.
    for i in range(8):
        var actor = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
        actor.process_mode = Node.PROCESS_MODE_DISABLED
        actor.position = Vector2(100 + i * 200, 72)
        var actor_body: Sprite2D = actor.get_node("m_Body")
        var actor_weapon: Sprite2D = actor.get_node("m_Weapon")
        actor_body.texture = textures["body_pilgrim" if i % 4 >= 2 else "body_bare"]
        actor_weapon.texture = textures["weapon_pilgrim" if i % 2 else "weapon_default"]
        actor_body.flip_h = i >= 4
        actor_weapon.flip_h = i >= 4
        viewport.add_child(actor)
        actors.append(actor)
    var contact := Image.create_empty(1600, SAMPLES.size() * 200, false, Image.FORMAT_RGBA8)
    var row := 0
    for frame in SAMPLES:
        for actor in actors:
            var animation: AnimationPlayer = actor.get_node("m_AnimPlayer")
            animation.play("idle1")
            animation.seek((frame - 1) / 24.0 + .00001, true)
            _check(actor.position == Vector2(100 + actors.find(actor) * 200, 72), "Wait moves the physics root")
        await process_frame
        await RenderingServer.frame_post_draw
        contact.blit_rect(viewport.get_texture().get_image(), Rect2i(0, 0, 1600, 200), Vector2i(0, row * 200))
        row += 1
    _check(contact.save_png("res://.godot/validation/official_idle_contact.png") == OK, "Cannot save wait contact sheet")
    viewport.queue_free()
    print("OFFICIAL IDLE ", "FAIL" if failed else "PASS", ": 400 source-layer frames, 100 preview frames, 4 loadouts x 2 directions; 100/24-second loop")
    quit(1 if failed else 0)
