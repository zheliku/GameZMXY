extends SceneTree
## Register Shark Whirlpool drops and the complete official selector-6 armor.

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
    var items: Array = library.get("_items")
    var script: Script = load("res://TheGame/GameScripts/Models/Profile/ItemAppearanceResource.cs")
    for entry in [[23, "blood_sea_demon_armor"], [24, "blood_sea_evil_staff"]]:
        var matches := items.filter(func(item): return int(item.get("_equipmentId")) == entry[0])
        assert(matches.size() <= 1, "Duplicate Shark Whirlpool equipment appearance")
        var item: Resource = script.new() if matches.is_empty() else matches[0]
        item.set("_equipmentId", entry[0])
        item.set("_inventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_%s.png" % entry[1]))
        item.set("_groundIcon", load("res://TheGame/Sprites/Items/equipment/ground_%s.png" % entry[1]))
        assert(item.get("_inventoryIcon") != null and item.get("_groundIcon") != null)
        if matches.is_empty():
            items.append(item)
    library.set("_items", items)
    assert(ResourceSaver.save(library, path) == OK)
    var appearance_path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
    var appearance: Resource = load(appearance_path)
    var armors: Dictionary = appearance.get("_armorTextures")
    var texture: Texture2D = load(appearance_path.get_base_dir().path_join("wukong_body_blood_sea.png"))
    assert(texture != null and texture.get_size() == appearance.get("_baseBody").get_size())
    armors[23] = texture
    appearance.set("_armorTextures", armors)
    assert(ResourceSaver.save(appearance, appearance_path) == OK)
    print("SHARK EQUIPMENT PASS: two transparent drops, complete Wukong armor selector 6")
    quit()
