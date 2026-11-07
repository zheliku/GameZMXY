extends SceneTree
## Register the four original sea drops and both complete selector-3 Wukong layers.

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
    for entry in [[18, "pilgrim_clothes"], [19, "pilgrim_staff"], [20, "cassock"], [21, "pine_staff"]]:
        var matches := items.filter(func(item): return int(item.get("_equipmentId")) == entry[0])
        assert(matches.size() <= 1, "Duplicate equipment appearance")
        var item: Resource = script.new() if matches.is_empty() else matches[0]
        item.set("_equipmentId", entry[0])
        item.set("_inventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_excellent_%s.png" % entry[1]))
        item.set("_groundIcon", load("res://TheGame/Sprites/Items/equipment/ground_excellent_%s.png" % entry[1]))
        assert(item.get("_inventoryIcon") != null and item.get("_groundIcon") != null)
        if matches.is_empty():
            items.append(item)
    library.set("_items", items)
    assert(ResourceSaver.save(library, path) == OK)
    var appearance_path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
    var appearance: Resource = load(appearance_path)
    for entry in [["_armorTextures", 18, "body"], ["_weaponTextures", 19, "weapon"]]:
        var textures: Dictionary = appearance.get(entry[0])
        var texture: Texture2D = load(appearance_path.get_base_dir().path_join("wukong_%s_excellent.png" % entry[2]))
        assert(texture != null and texture.get_size() == Vector2(1200, 5800))
        textures[entry[1]] = texture
        appearance.set(entry[0], textures)
    assert(ResourceSaver.save(appearance, appearance_path) == OK)
    print("SEA 3-1 EQUIPMENT PASS: four transparent drops, independent body/weapon selector 3")
    quit()
