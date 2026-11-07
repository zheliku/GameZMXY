extends SceneTree
## Import and save only the valley resources, then register the owned fire pool.

const BOSS := "res://TheGame/Entitys/Enemies/Bosses/zm1_10_bull_demon_valley/"

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    if not Engine.is_editor_hint():
        printerr("Bull resource registration requires --editor.")
        quit(1)
        return
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var paths := [BOSS + "bull_demon_beast_entity.tscn", BOSS + "bull_demon_king_entity.tscn",
        BOSS + "bull_demon_heart_entity.tscn",
        "res://TheGame/Entitys/Enemies/Monsters/Ground/bull_guard/bull_guard_entity.tscn"]
    for path in paths:
        var scene: PackedScene = load(path)
        assert(scene != null, "Missing bull actor " + path)
        var actor := scene.instantiate()
        load("res://EditorScripts/enemy_perception_authoring.gd").configure(actor)
        for field in ["_perception", "_body", "_animPlayer", "_hurtBox", "_hitBox", "_hitBoxRoot"]:
            assert(actor.get(field) != null, path + " missing " + field)
        if path.ends_with("bull_demon_heart_entity.tscn"):
            for field in ["_heart", "_heartFade", "_presentation", "_leftHand", "_rightHand", "_fireScene"]:
                assert(actor.get(field) != null, path + " missing " + field)
        var packed := PackedScene.new()
        assert(packed.pack(actor) == OK)
        assert(ResourceSaver.save(packed, path) == OK)
        actor.free()
    var pool_path := "res://TheGame/MainPack/Resources/NodePoolConfigRes.tres"
    var pool: Resource = load(pool_path)
    var entries: Array = pool.get("Entries")
    var fire_path := BOSS + "bull_fire_effect.tscn"
    assert(load(fire_path) != null)
    if not entries.any(func(item): return item.get("Scene") == fire_path):
        var entry: Resource = load("res://TheGame/MainPack/Scripts/Resources/Pooling/PoolEntry.cs").new()
        entry.set("Scene", fire_path)
        entries.append(entry)
    pool.set("Entries", entries)
    assert(ResourceSaver.save(pool, pool_path) == OK)
    print("BULL ACTOR RESOURCE PASS: four actors, three forms, native bindings and owned fire pool")
    quit()
