extends SceneTree
## Register East Sea's independent projectile and official accessory icons.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var pool_path := "res://TheGame/MainPack/Resources/NodePoolConfigRes.tres"
    var pool: Resource = load(pool_path)
    var entries: Array = pool.get("Entries")
    var projectile_path := "res://TheGame/Entitys/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_effect.tscn"
    if not entries.any(func(item): return item.get("Scene") == projectile_path):
        var entry: Resource = load("res://TheGame/MainPack/Scripts/Resources/Pooling/PoolEntry.cs").new()
        entry.set("Scene", projectile_path)
        entries.append(entry)
    pool.set("Entries", entries)
    assert(ResourceSaver.save(pool, pool_path) == OK)
    var path := "res://TheGame/Sprites/Items/item_appearances.tres"
    var library: Resource = load(path)
    var items: Array = library.get("_items")
    var matches := items.filter(func(item): return int(item.get("_equipmentId")) == 22)
    assert(matches.size() <= 1, "Duplicate BloodSeaShell appearance")
    var item: Resource = load("res://TheGame/GameScripts/Models/Profile/ItemAppearanceResource.cs").new() if matches.is_empty() else matches[0]
    item.set("_equipmentId", 22)
    item.set("_inventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_blood_sea_shell.png"))
    item.set("_groundIcon", load("res://TheGame/Sprites/Items/equipment/ground_blood_sea_shell.png"))
    assert(item.get("_inventoryIcon") != null and item.get("_groundIcon") != null)
    if matches.is_empty():
        items.append(item)
    library.set("_items", items)
    assert(ResourceSaver.save(library, path) == OK)
    for scene_path in [
        "res://TheGame/Entitys/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_entity.tscn",
        "res://TheGame/Entitys/Enemies/Monsters/Sea/lantern_fish/lantern_fish_entity.tscn"]:
        var packed: PackedScene = load(scene_path)
        var actor: Node = packed.instantiate()
        if actor.get("_enemyEntityId") == 10:
            actor.set("_dragonWaveScene", load(projectile_path))
            actor.set("DeathScene", load("res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/boss_death_effect.tscn"))
            actor.set("_backHit", actor.get_node("m_BackHit"))
            for field in ["_dragonWaveScene", "DeathScene", "_backHit"]:
                assert(actor.get(field) != null, scene_path + " missing " + field)
        load("res://EditorScripts/enemy_perception_authoring.gd").configure(actor)
        for field in ["_perception", "_body", "_animPlayer", "_hurtBox", "_hitBox", "_hitBoxRoot"]:
            assert(actor.get(field) != null, scene_path + " missing " + field)
        assert(packed.pack(actor) == OK)
        assert(ResourceSaver.save(packed, scene_path) == OK)
        actor.free()
    print("EAST SEA RESOURCE PASS: two bound enemies, pooled dragon wave and transparent accessory icons")
    quit()
