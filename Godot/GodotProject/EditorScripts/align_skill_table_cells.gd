extends SceneTree
## Align content to the visible grid strokes in the official table bitmaps.

func _initialize() -> void:
    _align.call_deferred()

func _align() -> void:
    assert(Engine.is_editor_hint())
    var path := "res://TheGame/UIs/skill_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var form: Control = load(path).instantiate()
    var row_tops := [0, 78, 158, 239, 319]
    var row_heights := [76, 78, 79, 78, 76]
    for page_name in ["m_ActivePage", "m_PassivePage"]:
        var passive: bool = page_name == "m_PassivePage"
        var page: Control = form.get_node("Content/" + page_name)
        var starts := [0, 82, 167, 349, 537, 643] if passive else [0, 82, 167, 251, 336]
        var widths := [80, 83, 180, 186, 104, 103] if passive else [80, 83, 82, 83, 410]
        var headers: Control = page.get_node("Headers")
        headers.size.y = 34
        for column in headers.get_child_count():
            var label: Label = headers.get_child(column)
            label.position = Vector2(starts[column], 0)
            label.size = Vector2(widths[column], 34)
        var groups := ["m_PassiveRows"] if passive else ["m_WukongRows", "m_TangsengRows"]
        for group_name in groups:
            var group: Control = page.get_node(group_name)
            group.position = Vector2(100, 119)
            group.size = Vector2(746, 395)
            for index in 5:
                var row: GridContainer = group.get_child(index)
                row.position = Vector2(0, row_tops[index])
                row.size = Vector2(746, row_heights[index])
                row.add_theme_constant_override("h_separation", 2)
                for column in row.get_child_count():
                    var cell: Control = row.get_child(column)
                    cell.custom_minimum_size = Vector2(widths[column], row_heights[index])
                    if cell.name == "IconCell":
                        var icon: TextureButton = cell.get_node("Icon")
                        icon.position = Vector2((widths[column] - 65) * .5, (row_heights[index] - 65) * .5)
                        icon.size = Vector2(65, 65)
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    form.free()
    print("SKILL TABLE ALIGN AUTHOR PASS: active Wukong/Sanzang and passive content follows visible grid interiors")
    quit()
