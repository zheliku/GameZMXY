extends SceneTree
## Compare every motion with its own official equipment selector, then render all pairings.

const SOURCE_SHA := "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7"
const MOTIONS := {"walk": [440, 16], "run": [452, 8], "jump": [533, 1],
    "jump_2": [539, 10], "fall": [543, 1], "hurt": [553, 15]}
const ART := "res://TheGame/Sprites/Characters/Heroes/wukong/"
var failures := 0

func _initialize() -> void:
    _run.call_deferred()

## This standalone visual fixture has no GGF scope; suppress methods on local copies only.
func _visual_only(actor: Node) -> void:
    var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
    for name in player.get_animation_library_list():
        var library: AnimationLibrary = player.get_animation_library(name).duplicate(true)
        for key in library.get_animation_list():
            var animation := library.get_animation(key)
            for track in animation.get_track_count():
                if animation.track_get_type(track) == Animation.TYPE_METHOD:
                    animation.track_set_enabled(track, false)
        player.remove_animation_library(name)
        player.add_animation_library(name, library)

func _check(condition: bool, message: String) -> void:
    if not condition:
        failures += 1
        if failures <= 12:
            push_error(message)

## Read every registered equipment identity from the production appearance resource.
func _wardrobe_entries(appearance: Resource, property: String, inventory: Dictionary) -> Array:
    var textures: Dictionary = appearance.get(property).duplicate()
    textures[0] = appearance.get("_baseBody" if property == "_armorTextures" else "_baseWeapon")
    var ids: Array = textures.keys()
    ids.sort()
    var entries := []
    for id in ids:
        var texture: Texture2D = textures[id]
        var name := texture.resource_path.get_file().get_basename().trim_prefix("wukong_")
        var variant: Dictionary = {}
        for candidate in inventory.variants:
            if candidate.name == name:
                variant = candidate
                break
        _check(not variant.is_empty(), "Missing source manifest for registered appearance: " + name)
        if not variant.is_empty():
            var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(variant.manifest))
            _check(manifest.source_sha256 == SOURCE_SHA and manifest.kind == variant.kind and
                int(manifest.selector) == int(variant.selector), "Appearance manifest identity mismatch: " + name)
            variant["data"] = manifest
        entries.append([int(id), name, variant])
    return entries

## Support the original 800px skill raster canvas and the current 200px cell canvas exactly.
func _source_svg(path: String, large_canvas: bool) -> Image:
    var svg := FileAccess.get_file_as_string(path)
    if large_canvas:
        svg = svg.replace('width="200px"', 'width="800px"').replace('height="200px"', 'height="800px"')
        svg = svg.replace('viewBox="-100 -128 200 200"', 'viewBox="-400 -428 800 800"')
    var image := Image.new()
    assert(image.load_svg_from_string(svg) == OK)
    return image.get_region(Rect2i(300, 300, 200, 200)) if large_canvas else image

## Rebuild a source layer without consulting another equipment's atlas.
func _source_frame(base: String, weapon: bool, large_canvas: bool = false) -> Image:
    var image := Image.create_empty(200, 200, false, Image.FORMAT_RGBA8)
    if not weapon:
        return _source_svg(base + "body.svg", large_canvas)
    var part := 0
    while FileAccess.file_exists(base + "weapon_%d.svg" % part):
        var fragment := _source_svg(base + "weapon_%d.svg" % part, large_canvas)
        var foreground := _source_svg(base + "occluder_%d.svg" % part, large_canvas)
        for y in range(200):
            for x in range(200):
                var color := fragment.get_pixel(x, y)
                color.a *= 1.0 - foreground.get_pixel(x, y).a
                fragment.set_pixel(x, y, color)
        image.blend_rect(fragment, Rect2i(0, 0, 200, 200), Vector2i.ZERO)
        part += 1
    return image

## Imported alpha-edge padding may change invisible RGB, but never visible pixels.
func _same_visible_pixels(actual: Image, expected: Image) -> bool:
    for y in range(200):
        for x in range(200):
            var a := actual.get_pixel(x, y)
            var b := expected.get_pixel(x, y)
            if a.a != b.a or (a.a > 0 and a != b):
                return false
    return true

## Cover the whole source table and every native frame reference, including idle, attacks and skills.
func _check_complete_atlases(player: AnimationPlayer, appearance: Resource, inventory_path: String) -> Dictionary:
    var inventory: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(inventory_path))
    _check(inventory.source_sha256 == SOURCE_SHA, "Unexpected wardrobe source inventory")
    for item in inventory.equipment:
        var textures: Dictionary = appearance.get("_armorTextures" if item.kind == "body" else "_weaponTextures")
        _check(textures.has(int(item.id)) and textures[int(item.id)].resource_path == item.texture,
            "Playable equipment binding differs from official identity: " + item.name)
    var samples := 0
    var large_canvas_samples := 0
    for variant in inventory.variants:
        var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(variant.manifest))
        _check(data.source_sha256 == SOURCE_SHA and data.kind == variant.kind and int(data.selector) == int(variant.selector),
            "Appearance export has the wrong selector: " + variant.name)
        var path: String = ART + "wukong_" + variant.name + ".png"
        var image := Image.load_from_file(ProjectSettings.globalize_path(path))
        var imported: Image = load(path).get_image()
        imported.convert(Image.FORMAT_RGBA8)
        for key in data.poses:
            var cell := int(key)
            var pose: Array = data.poses[key]
            var base: String = data.inputs[str(int(pose[0]))].path_join("%d_" % int(pose[1]))
            var expected := _source_frame(base, data.kind == "weapon")
            var region := Rect2i(cell % 6 * 200, cell / 6 * 200, 200, 200)
            var actual := image.get_region(region)
            if actual.get_data() != expected.get_data():
                # The first four sheets' skill cells were rasterized on an 800px
                # canvas. Compare that exact source too; do not relax pixel tolerances.
                var original_raster := _source_frame(base, data.kind == "weapon", true)
                if actual.get_data() == original_raster.get_data():
                    expected = original_raster
                    large_canvas_samples += 1
            _check(actual.get_data() == expected.get_data(), "%s cell %d has wrong or missing equipment art" % [variant.name, cell])
            expected.fix_alpha_edges()
            _check(_same_visible_pixels(imported.get_region(region), expected), "%s cell %d has stale imported art" % [variant.name, cell])
            samples += 1
        for name in player.get_animation_list():
            var animation := player.get_animation(name)
            for layer in ["m_Body:frame", "m_Weapon:frame"]:
                var track := animation.find_track(NodePath(layer), Animation.TYPE_VALUE)
                if track < 0:
                    continue
                for key in range(animation.track_get_key_count(track)):
                    var cell: int = animation.track_get_key_value(track, key)
                    _check(data.poses.has(str(cell)), "%s/%s references an unaudited frame %d" % [variant.name, name, cell])
    return {"complete_source_frames": samples, "legacy_canvas_frames": large_canvas_samples,
        "equipment_bindings": inventory.equipment.size(), "not_playable_items": inventory.not_playable_yet.size()}

func _run() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() < 5 or args.size() > 6 or (args.size() == 6 and args[5] != "--render-only"):
        push_error("Expected original base export directory, armor manifest, sword manifest, capture directory and complete wardrobe inventory; optional --render-only follows a separate headless source audit")
        quit(1)
        return
    var render_only := args.size() == 6
    var original: String = args[0]
    _check(FileAccess.get_sha256(original.path_join("role.swf")) == SOURCE_SHA, "Unexpected official Role_v7 revision")
    var armor: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
    var sword: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[2]))
    # AllEquipment dsyj.showid=4 and jcbj.showid=7; the original regression used 2 for both.
    _check(armor.source_sha256 == SOURCE_SHA and armor.kind == "body" and int(armor.selector) == 4, "Wrong earth-fiend selector")
    _check(sword.source_sha256 == SOURCE_SHA and sword.kind == "weapon" and int(sword.selector) == 7, "Wrong heirloom selector")
    if failures > 0:
        quit(1)
        return
    var scene: PackedScene = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn")
    var hero = scene.instantiate()
    _visual_only(hero)
    hero.process_mode = Node.PROCESS_MODE_DISABLED
    root.add_child(hero)
    var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
    var body: Sprite2D = hero.get_node("m_Body")
    var weapon: Sprite2D = hero.get_node("m_Weapon")
    var appearance: Resource = load(ART + "wukong_appearance.tres")
    # Large atlases are read back without a GPU in the headless phase. The
    # separate render phase retains all equipment pairings and native playback.
    var complete_report := {"complete_source_frames": 0, "legacy_canvas_frames": 0}
    if not render_only:
        complete_report = _check_complete_atlases(player, appearance, args[4])
    var inventory: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[4]))
    var bodies := _wardrobe_entries(appearance, "_armorTextures", inventory)
    var weapons := _wardrobe_entries(appearance, "_weaponTextures", inventory)
    var source_samples := 0
    for entry in bodies + weapons:
        if render_only:
            continue
        var name: String = entry[1]
        var path: String = ART + "wukong_" + name + ".png"
        var image := Image.load_from_file(ProjectSettings.globalize_path(path))
        var imported: Image = load(path).get_image()
        imported.convert(Image.FORMAT_RGBA8)
        _check(image.get_size() == Vector2i(1200, 5800), "Unexpected shared atlas grid: " + name)
        var data: Dictionary = entry[2].data
        for action in MOTIONS:
            var animation := player.get_animation(action)
            _check(is_equal_approx(animation.length, MOTIONS[action][1] / 24.0), "Motion duration differs: " + action)
            var frame_track := animation.find_track(NodePath("m_Body:frame"), Animation.TYPE_VALUE)
            for frame in range(MOTIONS[action][1]):
                var cell: int = animation.track_get_key_value(frame_track, frame)
                var cell_key := str(cell)
                _check(data.poses.has(cell_key), "%s/%s has no official source frame for atlas cell %s" % [name, action, cell_key])
                if not data.poses.has(cell_key):
                    continue
                var pose: Array = data.poses[cell_key]
                var source_dir: String = data.inputs[str(int(pose[0]))]
                var base: String = source_dir.path_join("%d_" % int(pose[1]))
                var expected := _source_frame(base, name.begins_with("weapon_"))
                var actual := image.get_region(Rect2i(cell % 6 * 200, cell / 6 * 200, 200, 200))
                _check(actual.get_data() == expected.get_data(), "%s/%s source frame %d has the wrong appearance" % [name, action, frame + 1])
                var imported_frame := imported.get_region(Rect2i(cell % 6 * 200, cell / 6 * 200, 200, 200))
                expected.fix_alpha_edges()
                _check(_same_visible_pixels(imported_frame, expected), "%s/%s imported frame %d is stale or incorrect" % [name, action, frame + 1])
                source_samples += 1
    var playback_samples := 0
    for armor_entry in bodies:
        for weapon_entry in weapons:
            appearance.call("ApplyToSprites", body, weapon, weapon_entry[0], armor_entry[0])
            _check(body.texture.resource_path == ART + "wukong_" + armor_entry[1] + ".png" and
                weapon.texture.resource_path == ART + "wukong_" + weapon_entry[1] + ".png", "Equipment resource selected the wrong atlas")
            for direction in [-1, 1]:
                hero.call("SetFacing", direction)
                for action in player.get_animation_list():
                    var animation := player.get_animation(action)
                    var track := animation.find_track(NodePath("m_Body:frame"), Animation.TYPE_VALUE)
                    if track < 0:
                        continue
                    player.play("RESET")
                    player.seek(0, true, true)
                    player.stop()
                    player.play(action)
                    for frame in range(animation.track_get_key_count(track)):
                        var time := animation.track_get_key_time(track, frame)
                        player.seek(time + .00001, true, true)
                        _check(body.frame == int(animation.track_get_key_value(track, frame)) and body.frame == weapon.frame and
                            body.flip_h == weapon.flip_h and hero.position == Vector2.ZERO,
                            "Live animation layers differ: %s/%s/%s/%d" % [armor_entry[1], weapon_entry[1], action, frame + 1])
                        playback_samples += 1
    var armor_ids: Array[int] = []
    var weapon_ids: Array[int] = []
    for entry in bodies:
        armor_ids.append(entry[0])
    for entry in weapons:
        weapon_ids.append(entry[0])
    var motion_frames := {}
    for animation_name in player.get_animation_list():
        var animation := player.get_animation(animation_name)
        var track := animation.find_track(NodePath("m_Body:frame"), Animation.TYPE_VALUE)
        if track >= 0:
            motion_frames[String(animation_name)] = animation.track_get_key_count(track)
    if failures == 0 and DisplayServer.get_name() != "headless":
        await _render_pairings(scene, appearance, args[3], bodies, weapons)
    hero.free()
    assert(DirAccess.make_dir_recursive_absolute(args[3]) == OK)
    var report := FileAccess.open(args[3].path_join("report.json"), FileAccess.WRITE)
    complete_report.merge({"source_sha256": SOURCE_SHA, "source_frames": source_samples,
        "phase": "render_and_playback" if render_only else "source_and_playback",
        "live_samples": playback_samples, "failures": failures, "armor_ids": armor_ids,
        "weapon_ids": weapon_ids, "pairing_count": armor_ids.size() * weapon_ids.size(),
        "directions": [-1, 1], "motion_frames": motion_frames})
    report.store_string(JSON.stringify(complete_report, "  "))
    report.close()
    print("WARDROBE MOTION ", "PASS" if failures == 0 else "FAIL", ": ", source_samples,
        " motion source-layer frames, ", complete_report.complete_source_frames, " total source cells, ",
        playback_samples, " live samples, ", complete_report.legacy_canvas_frames, " original-canvas cells, ", failures, " failures")
    quit(0 if failures == 0 else 1)

## Capture actual native sprites; rows are armor variants, columns are weapons in both directions.
func _render_pairings(scene: PackedScene, appearance: Resource, output: String,
        bodies: Array, weapons: Array) -> void:
    assert(DirAccess.make_dir_recursive_absolute(output) == OK)
    var viewport := SubViewport.new()
    viewport.size = Vector2i(maxi(weapons.size() * 400, 200), maxi(bodies.size() * 200, 200))
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    var background := ColorRect.new()
    background.size = viewport.size
    background.color = Color(.18, .22, .25)
    viewport.add_child(background)
    var actors := []
    for row in range(bodies.size()):
        for column in range(weapons.size() * 2):
            var actor = scene.instantiate()
            _visual_only(actor)
            actor.process_mode = Node.PROCESS_MODE_DISABLED
            actor.position = Vector2(column * 200 + 100, row * 200 + 140)
            viewport.add_child(actor)
            appearance.call("ApplyToSprites", actor.get_node("m_Body"), actor.get_node("m_Weapon"), weapons[column / 2][0], bodies[row][0])
            actor.call("SetFacing", -1 if column % 2 == 0 else 1)
            actors.append(actor)
            var label := Label.new()
            label.position = Vector2(column * 200 + 6, row * 200 + 8)
            label.text = "%s\n%s" % [bodies[row][1], weapons[column / 2][1]]
            label.add_theme_font_size_override("font_size", 12)
            viewport.add_child(label)
    for sample in [["run", 0], ["run", 4], ["walk", 0], ["jump_2", 4], ["hurt", 5]]:
        for actor in actors:
            var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
            player.stop()
            player.play(sample[0])
            player.seek((sample[1] + .01) / 24.0, true, true)
        await process_frame
        await RenderingServer.frame_post_draw
        _check(viewport.get_texture().get_image().save_png(output.path_join("%s_%02d.png" % [sample[0], sample[1] + 1])) == OK,
            "Cannot save wardrobe motion capture")
    viewport.queue_free()
