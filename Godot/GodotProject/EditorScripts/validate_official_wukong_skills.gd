extends SceneTree
## Verify source frames, equipment registration, live hit areas and rendered FX.

var failed := false

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
    var half: Vector2 = hit.shape.size/2.0
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
    var hero = load("res://TheGame/Entitys/wukong_entity.tscn").instantiate()
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
            body.texture = appearance.get("m_ArmoredBody" if armor else "m_BaseBody")
            weapon.texture = appearance.get("m_EquippedWeapon" if armed else "m_BaseWeapon")
            var weapon_image := weapon.texture.get_image()
            var other_weapon: Texture2D = appearance.get("m_BaseWeapon" if armed else "m_EquippedWeapon")
            var other_weapon_image := other_weapon.get_image()
            _check(body.texture.get_size() == Vector2(1200, int(manifest.rows)*200) and weapon.texture.get_size() == body.texture.get_size(), "Unexpected expanded atlas")
            _check(body.hframes == 6 and body.vframes == int(manifest.rows) and weapon.hframes == body.hframes and weapon.vframes == body.vframes, "Body/weapon grid differs")
            for facing in [-1, 1]:
                hero.call("SetFacing", facing)
                var sheet := Image.create_empty(4000, 2000, false, Image.FORMAT_RGBA8)
                for action_index in range(5):
                    var action: Dictionary = manifest.skills[action_index]
                    var name: String = "skills/" + action.name
                    var animation: Animation = player.get_animation(name)
                    var count := int(action.count)
                    _check(abs(animation.length-count/24.0) < 0.000001, "Incorrect duration: " + name)
                    for index in range(animation.get_track_count()):
                        if animation.track_get_type(index) == Animation.TYPE_METHOD:
                            for key in animation.track_get_key_count(index):
                                _check(String(animation.method_track_get_name(index, key)).ends_with("AnimEvent"), "Animation event name differs")
                    for property in ["m_Body:frame", "m_Weapon:frame", "m_EffectRoot/m_Effect:frame"]:
                        var index := animation.find_track(NodePath(property), Animation.TYPE_VALUE)
                        _check(index >= 0 and animation.track_get_key_count(index) == count, "Missing original frames: " + name)
                        for frame in range(count):
                            _check(abs(animation.track_get_key_time(index, frame)-frame/24.0) < 0.000001, "Incorrect frame timing")
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
                            _check(hit.shape is RectangleShape2D and hit.shape.size.distance_to(Vector2(rectangle.size[0], rectangle.size[1])) < .001, "Wrong hit shape: " + context)
                            _check(hit.position.distance_to(Vector2(rectangle.position[0], rectangle.position[1])) < .001 and abs(hit.rotation-rectangle.rotation) < .00001, "Wrong hit geometry: " + context)
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
        var scene = load("res://TheGame/Entitys/Effects/Skills/wukong_%s_effect.tscn" % entry.name).instantiate()
        scene.process_mode = Node.PROCESS_MODE_DISABLED
        scene.position = Vector2(400, 270)
        viewport.add_child(scene)
        var fx_player: AnimationPlayer = scene.get_node("AnimationPlayer")
        var sprite: AnimatedSprite2D = scene.get_node("Visual/Sprite")
        var fx_hit: CollisionShape2D = scene.get_node("Visual/HitBox/CollisionShape2D")
        var fx_area: Area2D = fx_hit.get_parent()
        fx_area.disable_mode = CollisionObject2D.DISABLE_MODE_KEEP_ACTIVE
        _check(sprite.offset.distance_to(Vector2(entry.offset[0], entry.offset[1])) < .00001, "Detached effect origin differs")
        var animation := fx_player.get_animation("play")
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
                    _check(texture != null and _covers(texture.get_image(), sprite.global_transform, texture.get_size()/2-sprite.offset, false, fx_hit, .1), "Detached rectangle misses actual FX pixels")
                    target.global_position = fx_hit.global_position
                    far.global_position = fx_hit.global_transform * Vector2(fx_hit.shape.size.x/2+12, 0)
                else:
                    target.global_position = scene.position
                    far.global_position = scene.position+Vector2(700, 0)
                await physics_frame
                await physics_frame
                _check(fx_area.get_overlapping_areas().has(target) == not fx_hit.disabled, "Detached FX physical hit differs")
                _check(not fx_area.get_overlapping_areas().has(far), "Detached FX physical reach exceeds shape")
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
    print("OFFICIAL-SKILLS ", "FAIL" if failed else "PASS", ": 976 body/weapon frames, 5 detached FX, source timing and real Area2D hits")
    viewport.queue_free()
    await process_frame
    quit(1 if failed else 0)
