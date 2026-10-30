extends SceneTree
## Register the reviewed sea projectile and normalize its imported native resources.

func _initialize() -> void:
    _run.call_deferred()

func _run() -> void:
    var pool_path := "res://TheGame/MainPack/Resources/NodePoolConfigRes.tres"
    var pool: Resource = load(pool_path)
    var entries: Array = pool.get("Entries")
    entries = entries.filter(func(item): return not String(item.get("Scene")).ends_with("tide_projectile_effect.tscn"))
    var projectile_path := "res://TheGame/Entitys/Enemies/Monsters/Sea/shrimp_demon/shrimp_projectile_effect.tscn"
    if not entries.any(func(item): return item.get("Scene") == projectile_path):
        var entry: Resource = load("res://TheGame/MainPack/Scripts/Resources/Pooling/PoolEntry.cs").new()
        entry.set("Scene", projectile_path)
        entries.append(entry)
    pool.set("Entries", entries)
    assert(ResourceSaver.save(pool, pool_path) == OK)
    for name in ["shrimp_demon", "tide_demon", "turtle_minister"]:
        var scene_path := "res://TheGame/Entitys/Enemies/Monsters/Sea/%s/%s_entity.tscn" % [name, name]
        var packed: PackedScene = load(scene_path)
        var instance: Node = packed.instantiate()
        load("res://EditorScripts/enemy_perception_authoring.gd").configure(instance)
        assert(instance.get("m_EnemyEntityId") in [14, 16, 17])
        for field in ["m_Perception", "m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot"]:
            assert(instance.get(field) != null, name + " missing " + field)
        var player: AnimationPlayer = instance.get("m_AnimPlayer")
        for action in ["attack_1", "hurt", "death"]:
            assert(player.get_animation(action).has_marker("complete"), name + " missing terminal source frame")
        assert(packed.pack(instance) == OK)
        assert(ResourceSaver.save(packed, scene_path) == OK)
        instance.free()
    print("SEA 3-1 ACTOR REGISTRATION PASS: three real scenes, terminal markers and shrimp projectile pool")
    quit()
