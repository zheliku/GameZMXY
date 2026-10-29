extends SceneTree
## Verify source frames, equipment registration, live hit areas and rendered FX.

var failed := false
const DETACHED_SOURCE_ORIGIN := Vector2.ZERO

func _weapon_texture(appearance: Resource, armed: bool) -> Texture2D:
    if not armed:
        return appearance.get("m_BaseWeapon")
    return (appearance.get("m_WeaponTextures") as Dictionary)[2]

func _body_texture(appearance: Resource, armored: bool) -> Texture2D:
    if not armored:
        return appearance.get("m_BaseBody")
    return (appearance.get("m_ArmorTextures") as Dictionary)[1]

func _check(condition: bool, message: String) -> void:
    if not condition:
        failed = true
        push_error(message)

func _target(viewport: SubViewport) -> Area2D:
    var area := Area2D.new()
    area.collision_layer = 32
    area.collision_mask = 0
    area.monitoring = false
    var collision := CollisionShape2D.new()
    var shape := RectangleShape2D.new()
    shape.size = Vector2(2, 2)
    collision.shape = shape
    area.add_child(collision)
    viewport.add_child(area)
    return area

func _initialize() -> void:
    _run.call_deferred()

func _covers(image: Image, transform: Transform2D, center: Vector2, flip: bool, hit: CollisionShape2D, alpha: float) -> bool:
    # Read actual imported pixels independently of the generated manifest.
    var rect := image.get_used_rect()
    var inverse := hit.global_transform.affine_inverse()
    var half: Vector2 = hit.shape.size / 2.0 + Vector2.ONE*.01
    for y in range(rect.position.y, rect.end.y):
        for x in range(rect.position.x, rect.end.x):
            if image.get_pixel(x, y).a < alpha:
                continue
            var point := Vector2(x+.5, y+.5)-center
            if flip:
                point.x = -point.x
            point = inverse * (transform * point)
            if abs(point.x) > half.x or abs(point.y) > half.y:
                return false
    return true

func _outline(line: Line2D, hit: CollisionShape2D) -> void:
    line.visible = not hit.disabled
    line.clear_points()
    if hit.disabled:
        return
    var half: Vector2 = (hit.shape as RectangleShape2D).size / 2.0
    for corner in [Vector2(-half.x, -half.y), Vector2(half.x, -half.y), half, Vector2(-half.x, half.y)]:
        line.add_point(hit.global_transform * corner)

func _run() -> void:
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    _check(manifest.skills.size() == 5 and manifest.source_count == 209 and manifest.fps == 24, "Incomplete skill source frames")
    var viewport := SubViewport.new()
    viewport.size = Vector2i(800, 400)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    var background := ColorRect.new()
    background.size = viewport.size
    background.color = Color(0.18, 0.24, 0.27)
    viewport.add_child(background)
    var hero = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
    hero.process_mode = Node.PROCESS_MODE_DISABLED
    hero.position = Vector2(400, 210)
    viewport.add_child(hero)
    var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
    var body: Sprite2D = hero.get_node("m_Body")
    var weapon: Sprite2D = hero.get_node("m_Weapon")
    var effect: AnimatedSprite2D = hero.get_node("m_EffectRoot/m_Effect")
    var hit: CollisionShape2D = hero.get_node("m_HitBoxRoot/m_HitBox/CollisionShape2D")
    var hit_area: Area2D = hit.get_parent()
    hit_area.disable_mode = CollisionObject2D.DISABLE_MODE_KEEP_ACTIVE
    var target := _target(viewport)
    var far := _target(viewport)
    var appearance = load("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres")
    var outline := Line2D.new()
    outline.width = 1
    outline.closed = true
    outline.default_color = Color(0.3, .85, 1, .8)
    viewport.add_child(outline)
    var output := "res://.godot/validation/official_skills"
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
    var samples := 0
    for armor in range(2):
        for armed in range(2):
            body.texture = _body_texture(appearance, armor == 1)
            weapon.texture = _weapon_texture(appearance, armed)
            body.vframes = body.texture.get_height() / 200
            weapon.vframes = weapon.texture.get_height() / 200
            var weapon_image := weapon.texture.get_image()
            var other_weapon: Texture2D = _weapon_texture(appearance, not armed)
            var other_weapon_image := other_weapon.get_image()
            # Later append-only idle/equipment imports can extend the atlas.
            _check(body.texture.get_width() == 1200 and body.texture.get_height() >= int(manifest.rows)*200 and weapon.texture.get_size() == body.texture.get_size(), "Unexpected expanded atlas")
            _check(body.hframes == 6 and body.vframes * 200 == body.texture.get_height() and weapon.hframes == body.hframes and weapon.vframes == body.vframes, "Body/weapon grid differs")
            for facing in [-1, 1]:
                hero.call("SetFacing", facing)
                var sheet := Image.create_empty(4000, 2000, false, Image.FORMAT_RGBA8)
                for action_index in range(5):
                    var action: Dictionary = manifest.skills[action_index]
                    var name: String = "skills/" + action.name
                    var animation: Animation = player.get_animation(name)
                    var count := int(action.count)
                    for track in animation.get_track_count():
                        if animation.track_get_type(track) == Animation.TYPE_METHOD:
                            animation.track_set_enabled(track, false)
                    _check(abs(animation.length-count/24.0) < 0.000001, "Incorrect duration: " + name)
                    for index in range(animation.get_track_count()):
                        if animation.track_get_type(index) == Animation.TYPE_METHOD:
                            for key in animation.track_get_key_count(index):
                                _check(String(animation.method_track_get_name(index, key)).ends_with("AnimEvent"), "Animation event name differs")
                    for property in ["m_Body:frame", "m_Weapon:frame", "m_EffectRoot/m_Effect:frame"]:
                        var index := animation.find_track(NodePath(property), Animation.TYPE_VALUE)
                        _check(index >= 0 and animation.track_get_key_count(index) > 0, "Missing source track: " + name)
                        if index < 0:
                            continue
                        # Repeated source values are compressed; every retained key
                        # must still lie on the original frame lattice. Values are
                        # sampled against every source frame below.
                        for key in animation.track_get_key_count(index):
                            var source_frame := animation.track_get_key_time(index, key) * 24
                            _check(abs(source_frame - round(source_frame)) < .00001 and source_frame < count, "Incorrect frame timing: " + name)
                    player.play(name)
                    var picked := [0, count/4, count/2, count*3/4, count-1]
                    for frame in range(count):
                        player.seek((frame+0.1)/24.0, true)
                        var context := "%s frame=%d armor=%d weapon=%d facing=%d" % [name, frame+1, armor, armed, facing]
                        _check(hero.position == Vector2(400, 210), "Animation moved root: " + context)
                        _check(body.frame == int(action.frame_cells[frame]) and body.frame == weapon.frame, "Body/weapon frame mismatch: " + context)
                        _check(body.flip_h == weapon.flip_h and body.flip_h == (facing > 0), "Facing mismatch: " + context)
                        _check(effect.animation == action.name and effect.frame == frame, "Inline effect frame mismatch: " + context)
                        var rectangle = action.hitboxes[frame]
                        _check(hit.disabled == (rectangle == null), "Hit window mismatch: " + context)
                        var texture = effect.sprite_frames.get_frame_texture(effect.animation, frame)
                        _check((texture == null) == (action.effect_cells[frame] == null), "Lost inline source effect: " + context)
                        if rectangle != null:
                            # The original fit is migration data, not official probe
                            # geometry. Check real source/weapon coverage instead of
                            # rejecting authored extensions for later equipment.
                            _check(hit.shape is RectangleShape2D and hit.shape.size.x > 0 and hit.shape.size.y > 0, "Wrong hit shape: " + context)
                            if action.name == "blazing_flash":
                                var probe := Image.load_from_file(OS.get_cmdline_user_args()[0].get_base_dir().path_join("raster/unarmed/%d_probe.png" % (int(action.start)+frame)))
                                _check(_covers(probe, hit_area.get_parent().global_transform, Vector2(400, 400), false, hit, .25), "Rectangle misses original invisible dash probe: " + context)
                            else:
                                var cell := Rect2i(body.frame%6*200, body.frame/6*200, 200, 200)
                                for source in [weapon_image, other_weapon_image]:
                                    _check(_covers(source.get_region(cell), weapon.global_transform, Vector2(100, 100), weapon.flip_h, hit, .25), "Rectangle misses equipped weapon: " + context)
                                if texture != null:
                                    _check(_covers(texture.get_image(), effect.global_transform, Vector2(400, 200), false, hit, .1), "Rectangle misses inline energy: " + context)
                            target.global_position = hit.global_position
                            far.global_position = hit.global_transform * Vector2(hit.shape.size.x/2+12, 0)
                        else:
                            target.global_position = hero.global_position
                            far.global_position = hero.global_position+Vector2(700, 0)
                        await physics_frame
                        await physics_frame
                        _check(hit_area.get_overlapping_areas().has(target) == (rectangle != null), "Actual near-hit mismatch: " + context)
                        _check(not hit_area.get_overlapping_areas().has(far), "Actual hit beyond rectangle: " + context)
                        _outline(outline, hit)
                        samples += 1
                        if picked.has(frame):
                            await RenderingServer.frame_post_draw
                            sheet.blit_rect(viewport.get_texture().get_image(), Rect2i(0, 0, 800, 400), Vector2i(picked.find(frame)*800, action_index*400))
                    player.seek(animation.length, true)
                    _check(hit.disabled, "Hitbox remains active after skill: " + name)
                sheet.save_png(output.path_join("skills_a%d_w%d_f%d.png" % [armor, armed, facing]))
    hero.hide()
    # The descent projectile extends below its owner; keep the complete source
    # frame visible in the fixture instead of cropping it at the viewport edge.
    viewport.size = Vector2i(800, 600)
    background.size = viewport.size
    for entry in manifest.detached:
        if entry.name == "fire_demon_prelude":
            continue # 纯视觉已由角色内的 TopLevel AnimatedSprite2D 播放，真实技能回归覆盖原地播放和完成隐藏。
        var scene = load("res://TheGame/Entitys/Heroes/Wukong/wukong_%s_effect.tscn" % entry.name).instantiate()
        scene.process_mode = Node.PROCESS_MODE_DISABLED
        scene.position = Vector2(400, 270)
        viewport.add_child(scene)
        var fx_player: AnimationPlayer = scene.get_node("AnimationPlayer")
        var sprite: AnimatedSprite2D = scene.get_node("Origin/Visual/Sprite")
        var fx_hit: CollisionShape2D = scene.get_node("Origin/Visual/HitBox/CollisionShape2D")
        var fx_area: Area2D = fx_hit.get_parent()
        fx_area.disable_mode = CollisionObject2D.DISABLE_MODE_KEEP_ACTIVE
        _check(sprite.offset.distance_to(Vector2(entry.offset[0], entry.offset[1])) < .00001, "Detached effect origin differs")
        _check(sprite.get_parent().position.is_equal_approx(Vector2.ZERO), "Detached visual unexpectedly moves the source origin: %s" % entry.name)
        _check(sprite.get_parent().get_parent().position.is_equal_approx(DETACHED_SOURCE_ORIGIN), "Detached effect does not stay at the actor root: %s pos=%s" % [entry.name, sprite.get_parent().get_parent().position])
        var animation := fx_player.get_animation("play")
        # This fixture owns image/physics sampling; the real GGF smoke owns callbacks.
        for clip_name in fx_player.get_animation_list():
            var clip := fx_player.get_animation(clip_name)
            for track in clip.get_track_count():
                if clip.track_get_type(track) == Animation.TYPE_METHOD:
                    clip.track_set_enabled(track, false)
        _check(abs(animation.length-entry.count/24.0) < .000001, "Detached duration differs")
        var count := int(entry.count)
        var picked: Array[int] = []
        for frame in [0, count/4, count/2, count*3/4, count-1]:
            if not picked.has(frame):
                picked.append(frame)
        var sheet := Image.create_empty(picked.size()*800, 1200, false, Image.FORMAT_RGBA8)
        for facing in [-1, 1]:
            scene.scale.x = facing
            fx_player.play("play")
            for frame in range(count):
                fx_player.seek((frame+.1)/24.0, true)
                _check(sprite.frame == frame and fx_hit.disabled == (entry.hitboxes[frame] == null), "Detached FX frame or hit window differs")
                if not fx_hit.disabled:
                    var texture := sprite.sprite_frames.get_frame_texture("play", frame)
                    var visual_is_gameplay_exact: bool = entry.name != "fire_demon_combo"
                    _check(texture != null and (not visual_is_gameplay_exact or _covers(texture.get_image(), sprite.global_transform, texture.get_size()/2-sprite.offset, false, fx_hit, .1)), "Detached rectangle misses actual FX pixels: %s frame=%d facing=%d" % [entry.name, frame + 1, facing])
                    target.global_position = fx_hit.global_position
                    far.global_position = fx_hit.global_transform * Vector2(fx_hit.shape.size.x/2+12, 0)
                else:
                    target.global_position = scene.position
                    far.global_position = scene.position+Vector2(700, 0)
                await physics_frame
                await physics_frame
                _check(fx_area.get_overlapping_areas().has(target) == not fx_hit.disabled, "Detached FX physical hit differs")
                _check(not fx_area.get_overlapping_areas().has(far), "Detached FX physical reach exceeds shape: %s/%s frame %d" % [entry.name, facing, frame+1])
                _outline(outline, fx_hit)
                if picked.has(frame):
                    await RenderingServer.frame_post_draw
                    sheet.blit_rect(viewport.get_texture().get_image(), Rect2i(0, 0, 800, 600), Vector2i(picked.find(frame)*800, 0 if facing == -1 else 600))
            fx_player.seek(animation.length, true)
            _check(fx_hit.disabled, "Detached hit remains after animation")
        sheet.save_png(output.path_join(entry.name + ".png"))
        viewport.remove_child(scene)
        scene.free()
    _check(samples == 976, "Incomplete skill equipment/facing coverage")
    print("OFFICIAL-SKILLS ", "FAIL" if failed else "PASS", ": 976 body/weapon frames, 4 pooled FX, source timing and real Area2D hits; in-scene prelude covered by UI smoke")
    viewport.queue_free()
    await process_frame
    quit(1 if failed else 0)
