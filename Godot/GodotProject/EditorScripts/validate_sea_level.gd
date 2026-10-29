extends SceneTree
## Check a native sea level against the official extraction, including every platform.

func _initialize() -> void:
    create_timer(20).timeout.connect(func():
        printerr("SEA validation timed out before completion")
        quit(1))
    _validate.call_deferred()

func _validate() -> void:
    var args := OS.get_cmdline_user_args()
    var label := args[1] if args.size() > 1 else "3-1"
    var settings: Array = {"3-1": [7, "zm1_07_shore", "m_Qianan", "qianan", 19],
        "3-2": [8, "zm1_08_east_sea", "m_Donghai", "donghai", 19],
        "3-3": [9, "zm1_09_shark_whirlpool", "m_Shazhiwoliu", "shazhiwoliu", 48]}[label]
    var id: int = settings[0]
    var directory: String = settings[1]
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))[label]
    var scene: Node2D = load("res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [directory, id]).instantiate()
    assert(scene.get("m_LevelId") == id and scene.get("m_MusicId") == 107)
    assert(scene.get("m_FeaturedOpponentSpawnPointIds") == PackedStringArray(["stage_5_a"]))
    assert(scene.get_node("World/SpawnPoints").get("m_Entries").size() == source.spawns.size() + 1)
    assert(scene.get_node("World/StageGates").get("m_Entries").size() == 9)
    assert(scene.get_node("World/Platforms").get_child_count() == source.geometry.filter(func(item): return item.oneWay).size())
    assert(scene.get_node("World/Geometry").find_children("*", "CollisionShape2D", true, false).size() == 3)
    assert(not scene.has_node("World/Traps") or scene.get_node("World/Traps").get_child_count() == 0)
    assert(scene.get_node("World/Background/Front").position.is_equal_approx(Vector2(source.backgroundPosition[0], source.backgroundPosition[1])))
    var layouts: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0].get_base_dir().path_join("art/layout.json")))
    var layout: Dictionary = layouts.levels.filter(func(item): return item.label == label)[0]
    var origin: Array = layout.terrain[0].local_top_left
    assert(scene.get_node("World/Geometry/Artwork/SeaShoreGround").position.is_equal_approx(Vector2(origin[0], origin[1])))
    assert(scene.get_node("World/Background/Sky/End").texture.resource_path.ends_with("sea_shore_sky.png"))
    var exit_source: Dictionary = source.scenery.filter(func(item): return item.characterId == 86)[0]
    assert(scene.get_node("World/Exit").position.is_equal_approx(Vector2(exit_source.position[0], exit_source.position[1])))
    var floor_source: Dictionary = source.geometry.filter(func(item): return not item.oneWay and item.corners[1][0] - item.corners[0][0] > 900 and item.corners[3][1] - item.corners[0][1] < 100)[0]
    assert(scene.get_node("Camera2D").limit_right == ceili(floor_source.corners[1][0]))
    for item in source.spawns:
        var node: Marker2D = scene.get_node("World/SpawnPoints/" + item.projectNode)
        assert(node.position.is_equal_approx(Vector2(item.position[0], item.position[1])))
    for gate in source.gates:
        var stage := int(gate.properties.idx) + 1
        assert(is_equal_approx(scene.get_node("World/StageGates/Stage%dGate" % stage).position.x, gate.battleBounds[1]))
        if stage > 1:
            assert(is_equal_approx(scene.get_node("World/StageGates/Stage%dLeft" % stage).position.x, gate.battleBounds[0]))
    var shapes := scene.get_node("World/Geometry").find_children("*", "CollisionShape2D", true, false)
    shapes.append_array(scene.get_node("World/Platforms").find_children("*", "CollisionShape2D", true, false))
    for index in source.geometry.size():
        var item: Dictionary = source.geometry[index]
        var matches := shapes.filter(func(shape): return shape.get_parent().name == "Surface%02d" % index)
        assert(matches.size() == 1)
        var shape: CollisionShape2D = matches[0]
        var corners: Array = item.corners
        var start := Vector2(corners[0][0], corners[0][1])
        var end := Vector2(corners[2][0], corners[2][1])
        assert((shape.position + shape.get_parent().position).is_equal_approx((start + end) * .5))
        assert(shape.shape.size.is_equal_approx(end - start))
        assert(shape.one_way_collision == item.oneWay)
    var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
    var field: String = settings[2]
    assert(form.get(field) == form.get_node(field))
    var library: AnimationLibrary = form.get_node("m_LocationAnimation").get_animation_library(settings[3])
    assert(is_equal_approx(library.get_animation("current").length, settings[4] / 24.0))
    assert(form.get_node(field + "/LocationGlow").size.is_equal_approx(form.get_node(field).size))
    form.free()
    scene.free()
    print("SEA %s NATIVE VALIDATION PASS: %d streams, 9 gates, %d source surfaces, sea sky, exit and official map hint" % [label, source.spawns.size(), source.geometry.size()])
    quit()
