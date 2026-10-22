extends SceneTree
## 对现有角色物理参数和 UI 布局做原生保存；不重建动画或装备资源。
func _initialize() -> void:
    assert(Engine.is_editor_hint())
    for path in ["res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn", "res://TheGame/Entitys/Enemies/Monsters/huaguoshan_monkey_entity.tscn", "res://TheGame/Entitys/Enemies/Monsters/demon_monkey_entity.tscn", "res://TheGame/Entitys/Enemies/Monsters/gorilla_entity.tscn", "res://TheGame/Entitys/Enemies/Bosses/zm1_02_macaque_king/macaque_king_entity.tscn", "res://TheGame/Entitys/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_entity.tscn"]:
        var actor: CharacterBody2D = load(path).instantiate()
        actor.floor_max_angle = deg_to_rad(50)
        actor.floor_snap_length = 12
        actor.floor_constant_speed = true
        _save(actor, path)
    var tooltip: Control = load("res://TheGame/UIs/Inventory/equipment_tooltip.tscn").instantiate()
    tooltip.custom_minimum_size.x = 170
    var description: Label = tooltip.get_node("Content/Footer/Description")
    description.custom_minimum_size.x = 135
    description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
    tooltip.get_node("Content/Header/Name").autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
    _save(tooltip, "res://TheGame/UIs/Inventory/equipment_tooltip.tscn")
    var hud: Control = load("res://TheGame/UIs/battle_form.tscn").instantiate()
    hud.get_node("MenuPanel/m_WsMax").position = Vector2(-103.75, -84.25)
    _save(hud, "res://TheGame/UIs/battle_form.tscn")
    print("PRESENTATION RESOURCES PASS: six slope bodies, source-width wrapped tooltip, full glow registration.")
    quit()

## 原生打包保持各自场景和外部资源引用。
func _save(node: Node, path: String) -> void:
    var packed := PackedScene.new()
    assert(packed.pack(node) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    node.free()
