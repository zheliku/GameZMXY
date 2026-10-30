extends "res://EditorScripts/import_official_sea_level.gd"
## Assemble the source 9-1 room, then register its independent pooled attacks.

func _build() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1)
    m_Source = JSON.parse_string(FileAccess.get_file_as_string(args[0]))["9-1"]
    m_ArtPrefix = "dragon_palace"
    m_ArtRoot = "res://TheGame/Sprites/Levels/zm1_11_hidden_dragon_palace/"
    m_Destination = "res://TheGame/Scenes/Levels/zm1_11_hidden_dragon_palace/level_11.tscn"
    var uid := ResourceLoader.get_resource_uid(m_Destination) if FileAccess.file_exists(m_Destination) else -1
    m_Scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
    m_Scene.name = "Level_11"
    m_Scene.set("m_LevelId", 11)
    m_Scene.set("m_MusicId", 108)
    m_Scene.set("m_FeaturedOpponentSpawnPointIds", PackedStringArray(["stage_1_a"]))
    for path in ["World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
        for child in m_Scene.get_node(path).get_children():
            child.free()
    var front: Sprite2D = m_Scene.get_node("World/Background/Front")
    front.texture = load(m_ArtRoot + "dragon_palace_front.png")
    front.position = _vec(m_Source.backgroundPosition)
    front.centered = false
    var sky: Sprite2D = m_Scene.get_node("World/Background/Sky/End")
    sky.texture = load("res://TheGame/Sprites/Levels/zm1_07_shore/sea_shore_sky.png")
    sky.position = Vector2(-30, 0)
    sky.visible = true
    _geometry(Vector2(-43.4, 506.1))
    # The source labels this single gate idx=4; runtime stage order remains one.
    m_Source.gates[0].properties.idx = 0
    _markers()
    m_Scene.get_node("World/SpawnPoints/PlayerStart").position = Vector2(200, 200)
    var gates := m_Scene.get_node("World/StageGates")
    var entries: Array = gates.get("m_Entries")
    entries.append(_gate("Stage1Left", "stage_1_left", m_Source.gates[0].battleBounds[0]))
    gates.set("m_Entries", entries)
    m_Scene.get_node("World/Exit").position = Vector2(814.75, 457.55)
    var camera: Camera2D = m_Scene.get_node("Camera2D")
    camera.limit_right = 1222
    var parallax: Parallax2D = m_Scene.get_node("World/Background/Sky")
    parallax.scroll_scale.x = 0.13
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(m_Destination.get_base_dir()))
    var packed := PackedScene.new()
    assert(packed.pack(m_Scene) == OK)
    assert(ResourceSaver.save(packed, m_Destination) == OK)
    if uid >= 0:
        assert(ResourceSaver.set_uid(m_Destination, uid) == OK)
    m_Scene.free()
    var boss := "res://TheGame/Entitys/Enemies/Bosses/zm1_11_hidden_dragon_palace/"
    for path in [boss + "dragon_king_entity.tscn", boss + "dragon_king_wave_effect.tscn", boss + "dragon_king_torrent_effect.tscn"]:
        var scene: PackedScene = load(path)
        assert(scene != null, path)
        var actor := scene.instantiate()
        assert(actor != null)
        if actor is CharacterBody2D:
            load("res://EditorScripts/enemy_perception_authoring.gd").configure(actor)
            for field in ["m_Body", "m_AnimPlayer", "m_HitBox", "m_HurtBox", "m_Perception", "m_WaveScene", "m_TorrentScene"]:
                assert(actor.get(field) != null, path + " missing " + field)
        packed = PackedScene.new()
        assert(packed.pack(actor) == OK)
        assert(ResourceSaver.save(packed, path) == OK)
        actor.free()
    var pool_path := "res://TheGame/MainPack/Resources/NodePoolConfigRes.tres"
    var pool: Resource = load(pool_path)
    var pool_entries: Array = pool.get("Entries")
    for name in ["dragon_king_wave_effect.tscn", "dragon_king_torrent_effect.tscn"]:
        var path: String = boss + name
        if not pool_entries.any(func(item): return item.get("Scene") == path):
            var entry: Resource = load("res://TheGame/MainPack/Scripts/Resources/Pooling/PoolEntry.cs").new()
            entry.set("Scene", path)
            pool_entries.append(entry)
    pool.set("Entries", pool_entries)
    assert(ResourceSaver.save(pool, pool_path) == OK)
    print("DRAGON 9-1 AUTHOR PASS: source room, gates, floor, backdrop, portal, actor and two owned effect pools")
    quit()
