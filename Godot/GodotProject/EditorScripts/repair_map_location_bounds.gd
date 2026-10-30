extends SceneTree
## Preserve source pixel scale while padding every hover canvas beyond the largest outline.

const PADDING := 12
const SOURCE_SIZES := {
    "Huaguoshan": Vector2i(160, 174), "Shuiliandong": Vector2i(177, 138),
    "Taohuayuan": Vector2i(162, 163), "Bianfuku": Vector2i(150, 95),
    "Qixingdong": Vector2i(153, 108), "Jialuoluo": Vector2i(222, 119),
    "Niumogu": Vector2i(152, 101)
}

func _initialize() -> void:
    _repair.call_deferred()

func _repair() -> void:
    assert(Engine.is_editor_hint())
    var scene_path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(scene_path)
    var form: Control = load(scene_path).instantiate()
    for name in SOURCE_SIZES:
        var button: TextureButton = form.get_node("m_" + name)
        var glow: TextureRect = button.get_node("LocationGlow")
        var source_size: Vector2i = SOURCE_SIZES[name]
        var padded_size := source_size + Vector2i.ONE * PADDING * 2
        var image := Image.load_from_file(glow.texture.resource_path)
        if image.get_size() == source_size:
            var padded := Image.create_empty(padded_size.x, padded_size.y, false, Image.FORMAT_RGBA8)
            padded.blit_rect(image, Rect2i(Vector2i.ZERO, source_size), Vector2i.ONE * PADDING)
            assert(padded.save_png(glow.texture.resource_path) == OK)
        else:
            assert(image.get_size() == padded_size, "Unexpected source canvas: " + name)
        var fit := minf(button.size.x / source_size.x, button.size.y / source_size.y)
        var margin := Vector2.ONE * PADDING * fit
        glow.position = -margin
        glow.size = button.size + margin * 2
        glow.pivot_offset = glow.size * .5
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, scene_path) == OK)
    assert(ResourceSaver.set_uid(scene_path, uid) == OK)
    form.free()
    print("MAP BOUNDS AUTHOR PASS: 7 padded hover canvases; coastal registration belongs to apply_official_coastal_map")
    quit()
