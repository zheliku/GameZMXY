extends SceneTree
## Register the four valley drops and their verified official selector-3 layers.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    if not Engine.is_editor_hint():
        printerr("Bull equipment registration requires --editor.")
        quit(1)
        return
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var path := "res://TheGame/Sprites/Items/item_appearances.tres"
    var library: Resource = load(path)
    var items: Array = library.get("m_Items")
    var script: Script = load("res://TheGame/GameScripts/Equipment/ItemAppearanceResource.cs")
    for entry in [[25, "pilgrim_clothes"], [26, "pilgrim_staff"], [27, "cassock"], [28, "pine_staff"]]:
        var matches := items.filter(func(item): return int(item.get("m_EquipmentId")) == entry[0])
        assert(matches.size() <= 1, "Duplicate Bull Demon Valley equipment appearance")
        var item: Resource = script.new() if matches.is_empty() else matches[0]
        item.set("m_EquipmentId", entry[0])
        item.set("m_InventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_fine_%s.png" % entry[1]))
        item.set("m_GroundIcon", load("res://TheGame/Sprites/Items/equipment/ground_fine_%s.png" % entry[1]))
        assert(item.get("m_InventoryIcon") != null and item.get("m_GroundIcon") != null)
        if matches.is_empty():
            items.append(item)
    library.set("m_Items", items)
    assert(ResourceSaver.save(library, path) == OK)
    var appearance_path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
    var appearance: Resource = load(appearance_path)
    # AllEquipment explicitly selects 3 for both excellent and fine equipment.
    for entry in [["m_ArmorTextures", 25, "body"], ["m_WeaponTextures", 26, "weapon"]]:
        var textures: Dictionary = appearance.get(entry[0])
        var texture: Texture2D = load(appearance_path.get_base_dir().path_join("wukong_%s_excellent.png" % entry[2]))
        assert(texture != null and texture.get_size() == Vector2(1200, 5800))
        textures[entry[1]] = texture
        appearance.set(entry[0], textures)
    assert(ResourceSaver.save(appearance, appearance_path) == OK)
    print("BULL EQUIPMENT PASS: four transparent drops, verified official selector 3 bindings")
    quit()
