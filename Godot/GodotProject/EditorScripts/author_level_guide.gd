extends SceneTree
## Author a map tooltip from existing shared panel/font resources; retain map geometry.

func _initialize() -> void:
    _author.call_deferred()

func _child(parent: Node, child: Node, name: String, owner: Node) -> Node:
    child.name = name
    parent.add_child(child)
    child.owner = owner
    if child is Control:
        child.mouse_filter = Control.MOUSE_FILTER_IGNORE
    return child

func _label(parent: Node, name: String, font_size: int, owner: Node) -> Label:
    var label: Label = _child(parent, Label.new(), name, owner)
    label.add_theme_font_size_override("font_size", font_size)
    label.custom_minimum_size.x = 188
    label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
    label.size.x = 188
    return label

func _author() -> void:
    var icon_path := "res://TheGame/UIs/Map/guide_equipment_icon.tscn"
    var icon_uid := ResourceLoader.get_resource_uid(icon_path)
    var icon: Control = load("res://TheGame/UIs/Inventory/equipment_cell.tscn").instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
    icon.name = "GuideEquipmentIcon"
    icon.custom_minimum_size = Vector2(32, 32)
    icon.texture = null
    var icon_scene := PackedScene.new()
    assert(icon_scene.pack(icon) == OK)
    assert(ResourceSaver.save(icon_scene, icon_path) == OK)
    if icon_uid != -1:
        assert(ResourceSaver.set_uid(icon_path, icon_uid) == OK)
    icon.free()
    var card_path := "res://TheGame/UIs/Map/level_guide_tooltip.tscn"
    var card_uid := ResourceLoader.get_resource_uid(card_path)
    var card := PanelContainer.new()
    card.name = "LevelGuideTooltip"
    card.set_script(load("res://TheGame/GameScripts/UI/MapForm/Components/LevelGuideTooltip.cs"))
    card.custom_minimum_size.x = 208
    card.mouse_filter = Control.MOUSE_FILTER_STOP
    card.z_index = 100
    card.theme = load("res://TheGame/MainPack/Themes/brown_gold_theme.tres")
    var panel_path := "res://TheGame/MainPack/Themes/Styles/brown_guide_panel.tres"
    var panel: StyleBoxFlat = load("res://TheGame/MainPack/Themes/Styles/brown_dialog_panel.tres").duplicate()
    panel.content_margin_left = 10
    panel.content_margin_right = 10
    panel.content_margin_top = 8
    panel.content_margin_bottom = 8
    var panel_uid := ResourceLoader.get_resource_uid(panel_path) if FileAccess.file_exists(panel_path) else -1
    assert(ResourceSaver.save(panel, panel_path) == OK)
    if panel_uid != -1:
        assert(ResourceSaver.set_uid(panel_path, panel_uid) == OK)
    card.add_theme_stylebox_override("panel", load(panel_path))
    var content: VBoxContainer = _child(card, VBoxContainer.new(), "Content", card)
    content.add_theme_constant_override("separation", 4)
    var header: VBoxContainer = _child(content, VBoxContainer.new(), "Header", card)
    header.add_theme_constant_override("separation", 3)
    card.set("_title", _label(header, "Title", 18, card))
    card.set("_description", _label(header, "Description", 12, card))
    var encounter: VBoxContainer = _child(content, VBoxContainer.new(), "Encounter", card)
    encounter.add_theme_constant_override("separation", 2)
    var encounter_caption := _label(encounter, "Caption", 12, card)
    encounter_caption.text = "出没妖怪"
    encounter_caption.add_theme_color_override("font_color", Color("ffdf9d"))
    var enemies: ItemList = _child(encounter, ItemList.new(), "Enemies", card)
    enemies.custom_minimum_size.x = 188
    enemies.auto_height = true
    enemies.max_columns = 2
    enemies.fixed_column_width = 90
    enemies.fixed_icon_size = Vector2i(28, 28)
    enemies.icon_mode = ItemList.ICON_MODE_LEFT
    enemies.focus_mode = Control.FOCUS_NONE
    enemies.add_theme_font_size_override("font_size", 11)
    enemies.add_theme_color_override("font_color", Color.WHITE)
    enemies.add_theme_color_override("guide_color", Color.TRANSPARENT)
    enemies.add_theme_constant_override("h_separation", 4)
    enemies.add_theme_constant_override("v_separation", 2)
    enemies.add_theme_constant_override("icon_margin", 4)
    enemies.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
    enemies.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
    card.set("_enemies", enemies)
    # Retain the approved EntityId keys and registered map portrait crops.
    var previous_card := (load(card_path) as PackedScene).instantiate()
    var portraits: Dictionary = previous_card.get("_enemyTextures")
    previous_card.free()
    card.set("_enemyTextures", portraits)
    var loot: VBoxContainer = _child(content, VBoxContainer.new(), "Loot", card)
    loot.add_theme_constant_override("separation", 2)
    var caption := _label(loot, "Caption", 12, card)
    caption.text = "可获装备"
    caption.add_theme_color_override("font_color", Color("ffdf9d"))
    var grid: GridContainer = _child(loot, GridContainer.new(), "Drops", card)
    grid.columns = 5
    grid.add_theme_constant_override("h_separation", 4)
    grid.add_theme_constant_override("v_separation", 2)
    card.set("_drops", grid)
    card.set("_appearances", load("res://TheGame/Sprites/Items/item_appearances.tres"))
    card.set("_equipmentCellScene", load("res://TheGame/UIs/Map/guide_equipment_icon.tscn"))
    var directory := "res://TheGame/UIs/Map"
    DirAccess.make_dir_recursive_absolute(directory)
    var packed := PackedScene.new()
    assert(packed.pack(card) == OK)
    assert(ResourceSaver.save(packed, directory + "/level_guide_tooltip.tscn") == OK)
    if card_uid != -1:
        assert(ResourceSaver.set_uid(card_path, card_uid) == OK)
    card.free()
    if not "--bind-map" in OS.get_cmdline_user_args():
        print("LEVEL GUIDE AUTHOR PASS: image-led card, transparent icons; existing map instance retained")
        quit()
        return
    var path := "res://TheGame/UIs/map_form.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var map: Control = load(path).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
    var names := ["m_Huaguoshan", "m_Shuiliandong", "m_Taohuayuan", "m_Bianfuku", "m_Qixingdong", "m_Jialuoluo", "m_Qianan", "m_Donghai", "m_Shazhiwoliu", "m_Niumogu"]
    var tooltip: PackedScene = load(directory + "/level_guide_tooltip.tscn")
    if map.has_node("m_LevelGuide"):
        var previous := map.get_node("m_LevelGuide")
        map.remove_child(previous)
        previous.free()
    var guide: Control = tooltip.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
    guide.name = "m_LevelGuide"
    guide.visible = false
    map.add_child(guide)
    guide.owner = map
    for i in names.size():
        var button: TextureButton = map.get_node(NodePath(names[i]))
        button.set("_levelId", i + 1)
        button.set("_guide", guide)
        button.tooltip_text = ""
    var scene := PackedScene.new()
    assert(scene.pack(map) == OK)
    assert(ResourceSaver.save(scene, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    map.free()
    print("LEVEL GUIDE AUTHOR PASS: shared brown panel/round font, 10 native map tooltip bindings, unchanged map geometry")
    quit()
