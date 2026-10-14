extends SceneTree
## Append missing official wait poses, preserving every existing body/weapon cell.

const SOURCE_SHA := "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7"
const SHEETS := ["body_bare", "body_pilgrim", "weapon_default", "weapon_pilgrim"]
const OUTPUT := "res://TheGame/Sprites/Characters/Heroes/wukong/"
const CELL := 200
const COLUMNS := 6

func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 2:
        push_error("Expected base export manifest and output idle manifest.")
        quit(1)
        return
    var source = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    if source.source_sha256 != SOURCE_SHA or source.fps != 24:
        push_error("Unexpected official Role revision or timing.")
        quit(1)
        return
    var sheets: Array[Image] = []
    var cells: Array = []
    var hashes := {}
    for sheet in SHEETS:
        var image := Image.load_from_file(OUTPUT + "wukong_" + sheet + ".png")
        image.convert(Image.FORMAT_RGBA8)
        sheets.append(image)
    var old_rows := sheets[0].get_height() / CELL
    var old_count := old_rows * COLUMNS
    # Preserve existing indices, including padding cells referenced by other libraries.
    for cell in range(old_count):
        var images: Array[Image] = []
        for sheet in sheets:
            images.append(sheet.get_region(Rect2i(cell % COLUMNS * CELL, cell / COLUMNS * CELL, CELL, CELL)))
        hashes[_pose_hash(images)] = cell
        cells.append(images)
    var frames: Array[int] = []
    for frame in range(1, 101):
        var images: Array[Image] = []
        for name in SHEETS:
            var variant = source.variants.filter(func(item): return item.name == name)[0]
            var pose := _render_pose(variant.input["529"].path_join("%d_" % frame), variant.kind)
            if pose == null:
                quit(1)
                return
            images.append(pose)
        var hash := _pose_hash(images)
        if not hashes.has(hash):
            hashes[hash] = cells.size()
            cells.append(images)
        frames.append(hashes[hash])
    var rows := ceili(cells.size() / float(COLUMNS))
    for sheet_index in range(SHEETS.size()):
        var atlas := Image.create_empty(COLUMNS * CELL, rows * CELL, false, Image.FORMAT_RGBA8)
        for cell in range(cells.size()):
            atlas.blit_rect(cells[cell][sheet_index], Rect2i(0, 0, CELL, CELL), Vector2i(cell % COLUMNS * CELL, cell / COLUMNS * CELL))
        if atlas.save_png(OUTPUT + "wukong_" + SHEETS[sheet_index] + ".png") != OK:
            quit(1)
            return
    var manifest := {"source_sha256": SOURCE_SHA, "source_sprite": 529, "fps": 24,
        "source_count": 100, "frames": frames, "columns": COLUMNS, "rows": rows,
        "previous_count": old_count, "count": cells.size(), "added": cells.size() - old_count,
        "transform": source.transforms["529"]}
    var output := FileAccess.open(args[1], FileAccess.WRITE)
    output.store_string(JSON.stringify(manifest, "  "))
    output.close()
    print("IDLE IMPORT PASS: 100 official frames, ", manifest.added, " new poses; preserved ", old_count, " existing cells per sheet.")
    quit()

## Render one registered layer; weapons retain the original per-fragment foreground masks.
func _render_pose(base: String, kind: String) -> Image:
    if kind == "body":
        return _load_svg(base + "body.svg")
    var image := Image.create_empty(CELL, CELL, false, Image.FORMAT_RGBA8)
    var part_index := 0
    while FileAccess.file_exists(base + "weapon_%d.svg" % part_index):
        var part := _load_svg(base + "weapon_%d.svg" % part_index)
        var mask := _load_svg(base + "occluder_%d.svg" % part_index)
        if part == null or mask == null:
            return null
        for y in range(CELL):
            for x in range(CELL):
                var pixel := part.get_pixel(x, y)
                pixel.a *= 1.0 - mask.get_pixel(x, y).a
                part.set_pixel(x, y, pixel)
        image.blend_rect(part, Rect2i(0, 0, CELL, CELL), Vector2i.ZERO)
        part_index += 1
    return image

## Fail at the import boundary rather than silently substituting an empty pose.
func _load_svg(path: String) -> Image:
    var image := Image.new()
    if not FileAccess.file_exists(path) or image.load_svg_from_string(FileAccess.get_file_as_string(path)) != OK or image.get_size() != Vector2i(CELL, CELL):
        push_error("Invalid official idle source: " + path)
        return null
    return image

## Deduplicate only poses whose four independently replaceable layers match exactly.
func _pose_hash(images: Array[Image]) -> String:
    var result := ""
    for image in images:
        var hash := HashingContext.new()
        hash.start(HashingContext.HASH_SHA256)
        hash.update(image.get_data())
        result += hash.finish().hex_encode()
    return result
