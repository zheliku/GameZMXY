extends SceneTree
## Register official Dragon Palace equipment and treasure art without merging actor layers.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    if not Engine.is_editor_hint():
        printerr("Equipment registration requires --editor.")
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
    var items: Array = library.get("_items")
    var script: Script = load("res://TheGame/GameScripts/Models/Profile/ItemAppearanceResource.cs")
    for entry in [[29, "black_tortoise_armor"], [30, "azure_dragon_blade"], [31, "ruyi_golden_staff"],
            [32, "white_tiger_staff"], [33, "kirin_robe"], [34, "vermilion_bird_ring"],
            [35, "seven_star_guard"], [36, "nine_ring_monk_staff"], [37, "east_sea_banner"], [38, "diamond_snare"]]:
        var matches := items.filter(func(item): return int(item.get("_equipmentId")) == entry[0])
        assert(matches.size() <= 1, "Duplicate equipment appearance")
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
    for entry in [["_armorTextures", 29, "body_black_tortoise"],
            ["_weaponTextures", 30, "weapon_azure_dragon"], ["_weaponTextures", 31, "weapon_ruyi"]]:
        var textures: Dictionary = appearance.get(entry[0])
        var texture: Texture2D = load(appearance_path.get_base_dir().path_join("wukong_%s.png" % entry[2]))
        assert(texture != null and texture.get_size() == Vector2(1200, 5800))
        textures[entry[1]] = texture
        appearance.set(entry[0], textures)
    assert(ResourceSaver.save(appearance, appearance_path) == OK)
    print("DRAGON EQUIPMENT PASS: ten transparent items, three complete independent wardrobe bindings")
    quit()
