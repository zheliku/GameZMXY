extends SceneTree
## Check source timing, fitted rectangles, real physics and all four loadouts.
## Pass the exporter manifest after --; images stay under ignored .godot/validation.

const COUNTS := [8, 8, 11, 20, 15]
const EFFECT_FRAMES := [[4, 5, 6], [4, 5, 6], [1, 2, 3, 4, 5, 6, 7, 8, 9], [1, 2, 3, 5], []]
const BODY_PATH := "m_Body:frame"
const WEAPON_PATH := "m_Weapon:frame"
const EFFECT_PATH := "m_EffectRoot/m_Effect:frame"
var failed := false

func _target(viewport: SubViewport) -> Area2D:
    var area := Area2D.new()
    area.collision_layer = 32
    area.collision_mask = 0
    area.monitoring = false
    var collision := CollisionShape2D.new()
    var shape := RectangleShape2D.new()
    shape.size = Vector2(4, 4)
    collision.shape = shape
    area.add_child(collision)
    viewport.add_child(area)
    return area

func _initialize() -> void:
    _run.call_deferred()

func _check(condition: bool, message: String) -> void:
    if not condition:
        failed = true
        push_error(message)

func _visible_points(image: Image, transform: Transform2D, center: Vector2, flip: bool, alpha: float) -> Array[Vector2]:
    var points: Array[Vector2] = []
    for y in range(image.get_height()):
        for x in range(image.get_width()):
            if image.get_pixel(x, y).a < alpha:
                continue
            var point := Vector2(x + 0.5, y + 0.5) - center
            if flip:
                point.x = -point.x
            points.append(transform * point)
    return points

func _run() -> void:
    var args := OS.get_cmdline_user_args()
    if args.is_empty():
        push_error("Missing official exporter manifest")
        quit(1)
        return
    var manifest = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    _check(manifest.source_sha256 == "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7", "Unexpected official revision")
    _check(manifest.fps == 24 and manifest.attacks.size() == 5, "Source FPS or combo count differs")
    var viewport := SubViewport.new()
    viewport.size = Vector2i(400, 300)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    var background := ColorRect.new()
    background.size = viewport.size
    background.color = Color(0.18, 0.24, 0.27)
    viewport.add_child(background)
    var guide := Line2D.new()
    guide.add_point(Vector2(0, 217))
    guide.add_point(Vector2(400, 217))
    guide.width = 1
    guide.default_color = Color(0.4, 0.7, 0.5)
    viewport.add_child(guide)
    var hero = load("res://TheGame/Entitys/wukong_entity.tscn").instantiate()
    hero.process_mode = Node.PROCESS_MODE_DISABLED
    hero.position = Vector2(200, 150)
    viewport.add_child(hero)
    var label := Label.new()
    label.position = Vector2(8, 8)
    viewport.add_child(label)
    # Ignore the changing label; require actual character pixels over the stage.
    var render_area := Rect2i(0, 40, 400, 260)
    hero.hide()
    await process_frame
    await RenderingServer.frame_post_draw
    var background_pixels := viewport.get_texture().get_image().get_region(render_area).get_data()
    hero.show()
    var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
    var body: Sprite2D = hero.get_node("m_Body")
    var weapon: Sprite2D = hero.get_node("m_Weapon")
    var effect: AnimatedSprite2D = hero.get_node("m_EffectRoot/m_Effect")
    var hit: CollisionShape2D = hero.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D")
    var hit_area: Area2D = hit.get_parent()
    # Hero logic is disabled for seeking; keep this real physics fixture active.
    hit_area.disable_mode = CollisionObject2D.DISABLE_MODE_KEEP_ACTIVE
    var near_target := _target(viewport)
    var far_target := _target(viewport)
    var energy_target := _target(viewport)
    var outline := Line2D.new()
    outline.width = 1.0
    outline.default_color = Color(0.3, 0.85, 1.0, 0.8)
    outline.closed = true
    viewport.add_child(outline)
    var appearance = load("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres")
    var output := "res://.godot/validation/official_attacks"
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
    var samples := 0
    var energy_samples := [0, 0, 0, 0, 0]
    var lingering_samples := [0, 0, 0, 0, 0]
    for armor in range(2):
        for armed in range(2):
            body.texture = appearance.get("m_ArmoredBody" if armor else "m_BaseBody")
            weapon.texture = appearance.get("m_EquippedWeapon" if armed else "m_BaseWeapon")
            # Skills append after the original 87 poses on the unchanged grid.
            var atlas_size := Vector2(body.hframes * 200, body.vframes * 200)
            _check(body.hframes == int(manifest.columns) and body.vframes >= int(manifest.rows), "Existing pose grid changed")
            _check(body.texture.get_size() == atlas_size and weapon.texture.get_size() == atlas_size, "Unexpected atlas registration")
            var body_image := body.texture.get_image()
            var weapon_image := weapon.texture.get_image()
            var other_weapon_image: Image = appearance.get("m_BaseWeapon" if armed else "m_EquippedWeapon").get_image()
            for cell in range(manifest.poses.size()):
                _check(not body_image.get_region(Rect2i(cell % 6 * 200, cell / 6 * 200, 200, 200)).is_invisible(), "Unused cell inside compact body atlas")
            for facing in [-1, 1]:
                hero.call("SetFacing", facing)
                var mirrored_scale := Vector2(-1 if facing > 0 else 1, 1)
                _check(hero.get_node("m_EffectRoot").scale == mirrored_scale and hero.get_node("m_HitBoxRoot").scale == mirrored_scale, "Effect/probe facing mismatch")
                var preview := Image.create_empty(2000, 1500, false, Image.FORMAT_RGBA8)
                for segment in range(5):
                    var expected = manifest.attacks[segment]
                    var animation: Animation = player.get_animation(expected.name)
                    var count: int = COUNTS[segment]
                    _check(expected.count == count and abs(animation.length - count / 24.0) < 0.000001, "Incorrect duration: " + expected.name)
                    for path in [BODY_PATH, WEAPON_PATH, EFFECT_PATH]:
                        var track_index := animation.find_track(NodePath(path), Animation.TYPE_VALUE)
                        _check(track_index >= 0 and animation.track_get_key_count(track_index) == count, "Missing source frames: " + expected.name)
                        for frame in range(count):
                            _check(abs(animation.track_get_key_time(track_index, frame) - frame / 24.0) < 0.000001, "Incorrect source key time: " + expected.name)
                    player.play(expected.name)
                    for frame in range(count):
                        var context := "%s frame=%d armor=%d weapon=%d facing=%d" % [expected.name, frame+1, armor, armed, facing]
                        player.seek((frame + 0.1) / 24.0, true)
                        _check(body.frame == int(expected.start) + frame and weapon.frame == body.frame, "Body/weapon mismatch: " + expected.name)
                        _check(effect.animation == expected.name and effect.frame == frame, "Effect timeline mismatch: " + expected.name)
                        _check(body.flip_h == (facing > 0) and weapon.flip_h == body.flip_h, "Facing mismatch")
                        _check(hero.position == Vector2(200, 150), "Animation changed root position")
                        var region = effect.sprite_frames.get_frame_texture(effect.animation, frame)
                        _check((region != null) == EFFECT_FRAMES[segment].has(frame + 1), "Original slash/spin effects were removed, or fixed staff ghosts remain")
                        var index = expected.effect_cells[frame]
                        if index == null:
                            _check(region == null, "Blank source effect should use a null frame")
                        else:
                            _check(region is AtlasTexture and region.region == Rect2(int(index) % 6 * 400, int(index) / 6 * 300, 400, 300), "Incorrect effect atlas cell")
                            _check(region.atlas.get_size() == Vector2(2400, int(manifest.effects_rows) * 300), "Unexpected compact effect atlas size")
                        var rectangle = expected.hitboxes[frame]
                        _check(region == null or rectangle != null, "Visible energy outlives its hit window: " + context)
                        var energy_point: Variant = null
                        energy_target.global_position = Vector2(-500, -500)
                        _check(hit.disabled == (rectangle == null), "Incorrect fitted hit window: " + expected.name)
                        if rectangle != null:
                            _check(expected.hit_windows[frame] and hit.shape is RectangleShape2D, "Hitbox is not a rectangle inside the source window")
                            var size := Vector2(rectangle.size[0], rectangle.size[1])
                            _check(hit.shape.size.distance_to(size) < 0.001 and hit.position.distance_to(Vector2(rectangle.position[0], rectangle.position[1])) < 0.001 and abs(hit.rotation - rectangle.rotation) < 0.0001, "Incorrect animated rectangle transform")
                            # Check both visible layers against the live shape, independently of manifest bounds.
                            var low := Vector2(INF, INF)
                            var high := Vector2(-INF, -INF)
                            var origin := Vector2i(body.frame % 6 * 200, body.frame / 6 * 200)
                            var weapon_points := _visible_points(weapon_image.get_region(Rect2i(origin, Vector2i(200, 200))), weapon.global_transform, Vector2(100, 100), weapon.flip_h, 0.25)
                            var energy_points: Array[Vector2] = []
                            if region != null:
                                energy_points = _visible_points(region.get_image(), effect.global_transform, Vector2(200, 150), false, 0.1)
                            var points := weapon_points + energy_points
                            # Reach is shared by both cosmetics; padding is measured against their full union.
                            points.append_array(_visible_points(other_weapon_image.get_region(Rect2i(origin, Vector2i(200, 200))), weapon.global_transform, Vector2(100, 100), weapon.flip_h, 0.25))
                            _check(not points.is_empty(), "Attack has neither a rod nor visible energy")
                            for global_point in points:
                                var point: Vector2 = hit.global_transform.affine_inverse() * global_point
                                low = low.min(point)
                                high = high.max(point)
                            _check(low.x >= -size.x/2 and high.x <= size.x/2 and low.y >= -size.y/2 and high.y <= size.y/2, "Rectangle misses actual weapon/energy pixels")
                            _check(low.x + size.x/2 <= 3.01 and size.x/2 - high.x <= 3.01, "Rectangle extends too far beyond weapon/energy: " + context)
                            _check(size.y <= maxf(high.y - low.y + 6.01, 14.01), "Rectangle is too thick for weapon/energy: " + context)
                            # Place a real target on energy clearly outside even the weapon's axis-aligned bounds.
                            var weapon_bounds := Rect2()
                            if not weapon_points.is_empty():
                                weapon_bounds = Rect2(weapon_points[0], Vector2.ZERO)
                                for point in weapon_points:
                                    weapon_bounds = weapon_bounds.expand(point)
                            var distance := -INF
                            for point in energy_points:
                                if not weapon_points.is_empty() and weapon_bounds.grow(8).has_point(point):
                                    continue
                                var candidate := point.distance_squared_to(weapon.global_position)
                                if candidate > distance:
                                    distance = candidate
                                    energy_point = point
                            if energy_point != null:
                                energy_target.global_position = energy_point
                            near_target.global_position = hit.global_position
                            far_target.global_position = hit.global_transform * Vector2(size.x/2 + 12, 0)
                        else:
                            near_target.global_position = hero.global_position
                            far_target.global_position = hero.global_position + Vector2(150, 0)
                        # Real broadphase must hit the nearby target and miss beyond the tip.
                        await physics_frame
                        await physics_frame
                        var overlaps := hit_area.get_overlapping_areas()
                        _check(overlaps.has(near_target) == (rectangle != null), "Physical near-target mismatch: " + context)
                        _check(not overlaps.has(far_target), "Physical hit outside the fitted rectangle: " + context)
                        if energy_point != null:
                            _check(overlaps.has(energy_target), "Visible energy outside the weapon cannot hit: " + context)
                            energy_samples[segment] += 1
                            if frame >= 4:
                                lingering_samples[segment] += 1
                        label.text = "%s  source %d/%d  armor=%d weapon=%d facing=%d" % [expected.name, frame+1, count, armor, armed, facing]
                        await process_frame
                        await RenderingServer.frame_post_draw
                        var rendered := viewport.get_texture().get_image()
                        _check(rendered.get_region(render_area).get_data() != background_pixels, "Character was not drawn: " + expected.name)
                        var selection := [0, 1, 2, 4, 7 if segment == 2 else count-1]
                        if selection.has(frame):
                            # Draw the exact live rectangle only on the review contact sheets.
                            outline.clear_points()
                            if rectangle != null:
                                var half: Vector2 = hit.shape.size * 0.5
                                for corner in [Vector2(-half.x, -half.y), Vector2(half.x, -half.y), half, Vector2(-half.x, half.y)]:
                                    outline.add_point(hit.global_transform * corner)
                            await process_frame
                            await RenderingServer.frame_post_draw
                            preview.blit_rect(viewport.get_texture().get_image(), Rect2i(0, 0, 400, 300), Vector2i(selection.find(frame)*400, segment*300))
                            outline.clear_points()
                        samples += 1
                    # Any return to locomotion must clear both effects and probes.
                    player.play("idle1")
                    player.seek(0, true)
                    _check(effect.animation == "empty" and hit.disabled and hit.rotation == 0, "Attack state leaked into idle")
                preview.save_png(output + "/attacks_armor%d_weapon%d_facing%d.png" % [armor, armed, facing])
    _check(energy_samples[2] >= 40 and energy_samples[3] >= 24 and lingering_samples[2] > 0 and lingering_samples[3] > 0, "Missing third/fourth attack energy and lingering-energy physical coverage")
    print("OFFICIAL-ATTACKS: ", "FAIL" if failed else "PASS", " ", samples, " source frames; energy-only hits=", energy_samples, "; lingering hits=", lingering_samples)
    viewport.queue_free()
    await process_frame
    quit(1 if failed else 0)
