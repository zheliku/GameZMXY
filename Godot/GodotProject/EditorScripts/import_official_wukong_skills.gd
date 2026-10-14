extends SceneTree
## Audit and rasterize skill layers with the same Flash registration as basics.

const AUDIT_SIZE := 800
const AUDIT_CENTER := Vector2i(400, 400)

func _svg(path: String) -> Image:
    var image := Image.new()
    if image.load_svg_from_string(FileAccess.get_file_as_string(path)) != OK:
        push_error("Skill SVG failed: " + path)
        quit(1)
    return image

func _fragments(base: String, kind: String) -> Image:
    var result := Image.create_empty(AUDIT_SIZE, AUDIT_SIZE, false, Image.FORMAT_RGBA8)
    var index := 0
    while FileAccess.file_exists(base + "%s_%d.svg" % [kind, index]):
        var part := _svg(base + "%s_%d.svg" % [kind, index])
        var mask_name := "occluder" if kind == "weapon" else "effect_occluder"
        var mask := _svg(base + "%s_%d.svg" % [mask_name, index])
        var rect := part.get_used_rect()
        for y in range(rect.position.y, rect.end.y):
            for x in range(rect.position.x, rect.end.x):
                var color := part.get_pixel(x, y)
                color.a *= 1.0 - mask.get_pixel(x, y).a
                part.set_pixel(x, y, color)
        result.blend_rect(part, rect, rect.position)
        index += 1
    return result

func _initialize() -> void:
    var path := OS.get_cmdline_user_args()[0]
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
    var work := path.get_base_dir()
    var body_rect := Rect2i()
    var effect_rect := Rect2i()
    var probe_rect := Rect2i()
    var raster := work.path_join("raster")
    DirAccess.make_dir_recursive_absolute(raster)
    for variant in manifest.variants:
        var target := raster.path_join(variant.name)
        DirAccess.make_dir_recursive_absolute(target)
        for key in manifest.poses:
            var pose: Array = manifest.poses[key]
            var base: String = variant.input[str(int(pose[0]))] + "/%d_" % int(pose[1])
            var body := _svg(base + "body.svg")
            var weapon := _fragments(base, "weapon")
            var rect := body.get_used_rect()
            var weapon_rect := weapon.get_used_rect()
            if weapon_rect.size != Vector2i.ZERO:
                rect = weapon_rect if rect.size == Vector2i.ZERO else rect.merge(weapon_rect)
            if rect.size != Vector2i.ZERO:
                body_rect = rect if body_rect.size == Vector2i.ZERO else body_rect.merge(rect)
            body.save_png(target.path_join(key + "_body.png"))
            weapon.save_png(target.path_join(key + "_weapon.png"))
            if variant.name != "unarmed":
                continue
            var effect := _fragments(base, "effect")
            effect.save_png(target.path_join(key + "_effect.png"))
            rect = effect.get_used_rect()
            if rect.size != Vector2i.ZERO:
                effect_rect = rect if effect_rect.size == Vector2i.ZERO else effect_rect.merge(rect)
            if FileAccess.file_exists(base + "probe.svg"):
                var probe := _svg(base + "probe.svg")
                probe.save_png(target.path_join(key + "_probe.png"))
                rect = probe.get_used_rect()
                probe_rect = rect if probe_rect.size == Vector2i.ZERO else probe_rect.merge(rect)
    manifest["audit"] = {"canvas": AUDIT_SIZE,
        "body": [body_rect.position.x-400, body_rect.position.y-400, body_rect.end.x-400, body_rect.end.y-400],
        "effect": [effect_rect.position.x-400, effect_rect.position.y-400, effect_rect.end.x-400, effect_rect.end.y-400],
        "probe": [probe_rect.position.x-400, probe_rect.position.y-400, probe_rect.end.x-400, probe_rect.end.y-400]}
    var file := FileAccess.open(path, FileAccess.WRITE)
    file.store_string(JSON.stringify(manifest, "  "))
    print("SKILL RASTER AUDIT: ", manifest.audit)
    quit()
