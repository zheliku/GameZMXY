extends SceneTree
## Append the two original drops and the complete selector-4 weapon. Preserve all existing bindings.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var path := "res://TheGame/Sprites/Items/item_appearances.tres"
    var library: Resource = load(path)
    var items: Array = library.get("m_Items")
    var script: Script = load("res://TheGame/GameScripts/Models/Profile/ItemAppearanceResource.cs")
    for entry in [[16, "moon_halberd"], [17, "robe"]]:
        if items.any(func(item): return int(item.get("m_EquipmentId")) == entry[0]):
            continue
        var item: Resource = script.new()
        item.set("m_EquipmentId", entry[0])
        item.set("m_InventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_heavenly_fiend_%s.png" % entry[1]))
        item.set("m_GroundIcon", load("res://TheGame/Sprites/Items/equipment/ground_heavenly_fiend_%s.png" % entry[1]))
        assert(item.get("m_InventoryIcon") != null and item.get("m_GroundIcon") != null)
        items.append(item)
    library.set("m_Items", items)
    assert(ResourceSaver.save(library, path) == OK)
    var appearance_path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
    var appearance: Resource = load(appearance_path)
    var weapons: Dictionary = appearance.get("m_WeaponTextures")
    weapons[16] = load(appearance_path.get_base_dir().path_join("wukong_weapon_heavenly_fiend.png"))
    assert(weapons[16] != null)
    appearance.set("m_WeaponTextures", weapons)
    assert(ResourceSaver.save(appearance, appearance_path) == OK)
    print("GARUDA 2-3 EQUIPMENT PASS: two transparent drops, complete weapon selector 4")
    quit()
