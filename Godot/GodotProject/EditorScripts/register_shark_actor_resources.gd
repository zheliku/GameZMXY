extends SceneTree
## Register Shark Whirlpool's pooled effects and validate both native actor scenes.

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
    var effect_paths := [
        "res://TheGame/Entitys/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash_effect.tscn",
        "res://TheGame/Entitys/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning_effect.tscn",
        "res://TheGame/Entitys/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado_effect.tscn",
        "res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave_effect.tscn",
        "res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning_effect.tscn",
        "res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_effect.tscn",
    ]
    var entry_script: Script = load("res://TheGame/MainPack/Scripts/Resources/Pooling/PoolEntry.cs")
    for scene_path in effect_paths:
        assert(load(scene_path) != null, "Missing Shark Whirlpool effect: " + scene_path)
        if not entries.any(func(item): return item.get("Scene") == scene_path):
            var entry: Resource = entry_script.new()
            entry.set("Scene", scene_path)
            entries.append(entry)
    pool.set("Entries", entries)
    assert(ResourceSaver.save(pool, pool_path) == OK)

    var shark: Node = load("res://TheGame/Entitys/Enemies/Monsters/Sea/shark_spawn/shark_spawn_entity.tscn").instantiate()
    var king: Node = load("res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_entity.tscn").instantiate()
    for actor in [shark, king]:
        load("res://EditorScripts/enemy_perception_authoring.gd").configure(actor)
        for field in ["m_Perception", "m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot"]:
            assert(actor.get(field) != null, actor.name + " missing " + field)
        assert(actor.get("m_EnemyEntityId") in [18, 19], actor.name + " has wrong entity id")
        var path := "res://TheGame/Entitys/Enemies/Monsters/Sea/shark_spawn/shark_spawn_entity.tscn" if actor == shark else "res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_entity.tscn"
        var packed := PackedScene.new()
        assert(packed.pack(actor) == OK)
        assert(ResourceSaver.save(packed, path) == OK)
        actor.free()
    print("SHARK ACTOR RESOURCE PASS: 2 actors, 6 pooled effects, native bindings")
    quit()
