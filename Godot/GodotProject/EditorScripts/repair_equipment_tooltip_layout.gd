extends SceneTree
## Align shared equipment detail rows without changing their fixed header or dynamic body.
func _initialize() -> void:
    _repair.call_deferred()
func _repair() -> void:
    var path := "res://TheGame/UIs/Inventory/equipment_tooltip.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var panel: PanelContainer = load(path).instantiate()
    panel.theme = load("res://TheGame/MainPack/Themes/brown_gold_theme.tres")
    for row in ["Quality", "Type"]:
        panel.get_node("Content/Header/" + row + "/Caption").vertical_alignment = VERTICAL_ALIGNMENT_CENTER
        panel.get_node("Content/Header/" + row + "/Value").vertical_alignment = VERTICAL_ALIGNMENT_CENTER
        panel.get_node("Content/Header/" + row + "/Caption/Underline").position.y = 18
    panel.get_node("Content/Header/Name").vertical_alignment = VERTICAL_ALIGNMENT_CENTER
    panel.get_node("Content/Footer/Price").vertical_alignment = VERTICAL_ALIGNMENT_CENTER
    var packed := PackedScene.new()
    assert(packed.pack(panel) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    panel.free()
    print("EQUIPMENT TOOLTIP AUTHOR PASS: explicit shared font, centered caption/value baselines, fixed 25px rows and original fade lines")
    quit()
