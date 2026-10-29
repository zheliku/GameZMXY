extends "res://EditorScripts/import_official_sea_level.gd"
## Build the two-region final valley from its complete source coordinates and artwork.

func _build() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1, "Expected the verified all-level source JSON")
    m_Source = JSON.parse_string(FileAccess.get_file_as_string(args[0]))["4-1"]
    m_ArtPrefix = "bull_demon_valley"
    m_ArtRoot = "res://TheGame/Sprites/Levels/zm1_10_bull_demon_valley/"
    m_Destination = "res://TheGame/Scenes/Levels/zm1_10_bull_demon_valley/level_10.tscn"
    var uid := ResourceLoader.get_resource_uid(m_Destination) if FileAccess.file_exists(m_Destination) else -1
    m_Scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
    m_Scene.name = "Level_10"
    m_Scene.set("m_LevelId", 10)
    m_Scene.set("m_MusicId", 108)
    m_Scene.set("m_FeaturedOpponentSpawnPointIds", PackedStringArray(["stage_2_a"]))
    for path in ["World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
        for child in m_Scene.get_node(path).get_children():
            child.free()
    # The official last stage has no portal: the heart's complete death advances to settlement.
    m_Scene.set("m_Exit", null)
    m_Scene.get_node("World/Exit").free()
    var front: Sprite2D = m_Scene.get_node("World/Background/Front")
    front.texture = load(m_ArtRoot + m_ArtPrefix + "_front.png")
    front.position = _vec(m_Source.backgroundPosition)
    front.centered = false
    var sky: Sprite2D = m_Scene.get_node("World/Background/Sky/End")
    sky.texture = load(m_ArtRoot + m_ArtPrefix + "_sky.png")
    sky.position = Vector2.ZERO
    sky.visible = true
    _geometry(Vector2(-16.05, 501.45))
    _markers()
    var camera: Camera2D = m_Scene.get_node("Camera2D")
    camera.limit_right = 1951
    var parallax: Parallax2D = m_Scene.get_node("World/Background/Sky")
    parallax.scroll_scale.x = (sky.texture.get_width() - 940.0) / (camera.limit_right - 940.0)
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(m_Destination.get_base_dir()))
    var packed := PackedScene.new()
    assert(packed.pack(m_Scene) == OK)
    assert(ResourceSaver.save(packed, m_Destination) == OK)
    if uid >= 0:
        assert(ResourceSaver.set_uid(m_Destination, uid) == OK)
    m_Scene.free()
    print("BULL 4-1 LEVEL AUTHOR PASS: three streams, two source battle regions, complete artwork, music and no synthetic portal")
    quit()
