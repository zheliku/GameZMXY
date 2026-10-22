extends SceneTree
## Rasterize registered layers and fit rectangles to weapons plus visible energy.

const HITBOX_PADDING := 3.0
const HITBOX_MIN_THICKNESS := 14.0

func _attack_hitbox(images: Array[Image], cell: int, effect: Image) -> Variant:
    # Cosmetic replacements share reach; fading energy remains part of the hit.
    var pixels: Array[Vector2] = []
    for image in images:
        var origin := Vector2i(cell % 6 * 200, cell / 6 * 200)
        for y in range(200):
            for x in range(200):
                if image.get_pixel(origin.x + x, origin.y + y).a < 0.25:
                    continue
                var point := Vector2(x + 0.5, y + 0.5) - Vector2(100, 100)
                pixels.append(point)
    for y in range(effect.get_height()):
        for x in range(effect.get_width()):
            if effect.get_pixel(x, y).a >= 0.1:
                pixels.append(Vector2(x + 0.5, y + 0.5) - Vector2(200, 150))
    return _fit_hitbox(pixels)

func _fit_hitbox(pixels: Array[Vector2]) -> Variant:
    if pixels.is_empty():
        return null
    var total := Vector2.ZERO
    for point in pixels:
        total += point
    var mean := total / pixels.size()
    var xx := 0.0
    var yy := 0.0
    var xy := 0.0
    for point in pixels:
        var delta := point - mean
        xx += delta.x * delta.x
        yy += delta.y * delta.y
        xy += delta.x * delta.y
    # Rods benefit from rotation; broad arcs can fit better without rotation.
    var rotation := 0.5 * atan2(2.0 * xy, xx - yy)
    var rotated := _rectangle_at_angle(pixels, rotation)
    var aligned := _rectangle_at_angle(pixels, 0.0)
    return aligned if aligned.size[0] * aligned.size[1] < rotated.size[0] * rotated.size[1] else rotated

func _rectangle_at_angle(pixels: Array[Vector2], rotation: float) -> Dictionary:
    var along := Vector2.from_angle(rotation)
    var across := along.orthogonal()
    var low := Vector2(INF, INF)
    var high := Vector2(-INF, -INF)
    for point in pixels:
        var projected := Vector2(point.dot(along), point.dot(across))
        low = low.min(projected)
        high = high.max(projected)
    var center := (low + high) * 0.5
    var size := high - low + Vector2.ONE * HITBOX_PADDING * 2.0
    size.y = maxf(size.y, HITBOX_MIN_THICKNESS)
    var position := along * center.x + across * center.y
    return {"size": [size.x, size.y], "position": [position.x, position.y], "rotation": rotation}

func _initialize() -> void:
    var manifest_path := OS.get_cmdline_user_args()[0]
    var manifest = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
    var weapon_images: Array[Image] = []
    for variant in manifest.variants:
        var atlas = Image.create_empty(int(manifest.columns) * 200, int(manifest.rows) * 200, false, Image.FORMAT_RGBA8)
        for i in range(manifest.poses.size()):
            var pose = manifest.poses[str(i)]
            var base = '%s/%d_' % [variant.input[str(int(pose[0]))], pose[1]]
            var path = base + ('weapon_0' if variant.kind == 'weapon' else variant.kind) + '.svg'
            # Some spinning poses intentionally have no visible weapon.
            if variant.kind == 'weapon' and not FileAccess.file_exists(path):
                continue
            if not FileAccess.file_exists(path):
                push_error('Missing official pose: ' + path)
                quit(1)
                return
            var image = Image.new()
            if image.load_svg_from_string(FileAccess.get_file_as_string(path)) != OK or image.get_size() != Vector2i(200, 200):
                push_error('Official pose failed: ' + path)
                quit(1)
                return
            if variant.kind == 'weapon':
                image = Image.create_empty(200, 200, false, Image.FORMAT_RGBA8)
                var part_index = 0
                while FileAccess.file_exists(base + 'weapon_%d.svg' % part_index):
                    var part = Image.new()
                    var occluder = Image.new()
                    if part.load_svg_from_string(FileAccess.get_file_as_string(base + 'weapon_%d.svg' % part_index)) != OK or occluder.load_svg_from_string(FileAccess.get_file_as_string(base + 'occluder_%d.svg' % part_index)) != OK:
                        quit(1)
                        return
                    # Preserve each fragment's own original foreground coverage.
                    for y in range(200):
                        for x in range(200):
                            var pixel = part.get_pixel(x, y)
                            pixel.a *= 1.0 - occluder.get_pixel(x, y).a
                            part.set_pixel(x, y, pixel)
                    image.blend_rect(part, Rect2i(0, 0, 200, 200), Vector2i.ZERO)
                    part_index += 1
            atlas.blit_rect(image, Rect2i(0, 0, 200, 200), Vector2i(i % 6 * 200, i / 6 * 200))
        DirAccess.make_dir_recursive_absolute(variant.output.get_base_dir())
        if atlas.save_png(variant.output) != OK:
            quit(1)
            return
        print('Imported ', variant.output)
        if variant.kind == 'weapon':
            weapon_images.append(atlas)
    # Effects need a wider canvas, with the same Flash origin (local 0, 28).
    var effect_images: Array[Image] = []
    for attack in manifest.attacks:
        attack["effect_cells"] = []
        attack["hitboxes"] = []
        for frame in range(int(attack.count)):
            var base = '%s/%d_' % [manifest.effects_input[str(int(attack.sprite))], frame + 1]
            var image = Image.create_empty(400, 300, false, Image.FORMAT_RGBA8)
            var native: Dictionary = manifest.get("native_effects", {}).get(str(int(attack.sprite)), {})
            var part_index = 0
            while FileAccess.file_exists(base + 'effect_%d.svg' % part_index):
                var part = Image.new()
                var occluder = Image.new()
                if part.load_svg_from_string(FileAccess.get_file_as_string(base + 'effect_%d.svg' % part_index)) != OK or occluder.load_svg_from_string(FileAccess.get_file_as_string(base + 'effect_occluder_%d.svg' % part_index)) != OK:
                    quit(1)
                    return
                if not native.is_empty():
                    # FFDec 原生滤镜保留完整弧光，仍用原始父级矩阵和后景遮挡对齐。
                    part = Image.load_from_file(native.input.path_join("%d.png" % (frame + 1)))
                    assert(part.get_size() == Vector2i(400, 300))
                for y in range(300):
                    for x in range(400):
                        var pixel = part.get_pixel(x, y)
                        pixel.a *= 1.0 - occluder.get_pixel(x, y).a
                        part.set_pixel(x, y, pixel)
                image.blend_rect(part, Rect2i(0, 0, 400, 300), Vector2i.ZERO)
                part_index += 1
            var rectangle = _attack_hitbox(weapon_images, int(attack.start) + frame, image)
            attack.hitboxes.append(rectangle if attack.hit_windows[frame] else null)
            # Blank source frames stay in the timeline as null, not atlas cells.
            if image.is_invisible():
                attack.effect_cells.append(null)
            else:
                attack.effect_cells.append(effect_images.size())
                effect_images.append(image)
    manifest["effects_columns"] = 6
    manifest["effects_rows"] = ceili(effect_images.size() / 6.0)
    var effects = Image.create_empty(2400, int(manifest.effects_rows) * 300, false, Image.FORMAT_RGBA8)
    for index in range(effect_images.size()):
        effects.blit_rect(effect_images[index], Rect2i(0, 0, 400, 300), Vector2i(index % 6 * 400, index / 6 * 300))
    DirAccess.make_dir_recursive_absolute(manifest.effects_output.get_base_dir())
    if effects.save_png(manifest.effects_output) != OK:
        quit(1)
        return
    print('Imported ', manifest.effects_output)
    # The explicit timeline importer consumes this compact cell mapping.
    var manifest_file := FileAccess.open(manifest_path, FileAccess.WRITE)
    manifest_file.store_string(JSON.stringify(manifest, "  "))
    quit()
