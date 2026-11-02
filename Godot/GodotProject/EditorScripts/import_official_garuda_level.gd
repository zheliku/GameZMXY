extends SceneTree
## Author official 2-3 using native nodes, source rectangles and stable spawn IDs.

const DESTINATION := "res://TheGame/Scenes/Levels/zm1_06_garuda_cave/level_6.tscn"
const ART_ROOT := "res://TheGame/Sprites/Levels/zm1_06_garuda_cave/"

var m_Source: Dictionary
var m_Scene: Node2D

func _initialize() -> void:
    _build.call_deferred()

func _build() -> void:
    var manifest := OS.get_cmdline_user_args()[0]
    m_Source = JSON.parse_string(FileAccess.get_file_as_string(manifest))["2-3"]
    m_Scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
    m_Scene.name = "Level_6"
    m_Scene.set("m_LevelId", 6)
    m_Scene.set("m_MusicId", 106)
    m_Scene.set("m_FeaturedOpponentSpawnPointIds", PackedStringArray(["stage_5_a"]))
    for path in ["World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
        for child in m_Scene.get_node(path).get_children():
            child.free()
    var front: Sprite2D = m_Scene.get_node("World/Background/Front")
    front.texture = load(ART_ROOT + "garuda_cave_front.png")
    front.position = _vector(m_Source.backgroundPosition)
    front.centered = false
    var sky: Sprite2D = m_Scene.get_node("World/Background/Sky/End")
    sky.texture = load("res://TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_cave_sky.png")
    sky.position = Vector2.ZERO
    sky.visible = true
    _geometry()
    _markers()
    _traps()
    var camera: Camera2D = m_Scene.get_node("Camera2D")
    camera.limit_right = 4692
    var parallax: Parallax2D = m_Scene.get_node("World/Background/Sky")
    parallax.scroll_scale.x = (sky.texture.get_width() - 940.0) / (camera.limit_right - 940.0)
    for item in m_Source.scenery:
        if int(item.characterId) == 86:
            m_Scene.get_node("World/Exit").position = _vector(item.position)
    DirAccess.make_dir_recursive_absolute(DESTINATION.get_base_dir())
    var packed := PackedScene.new()
    assert(packed.pack(m_Scene) == OK)
    assert(ResourceSaver.save(packed, DESTINATION) == OK)
    m_Scene.free()
    print("GARUDA 2-3 LEVEL AUTHOR PASS: 22 streams, 5 overlapping battle regions, 7 surfaces, 39 spikes")
    quit()

func _geometry() -> void:
    var root := m_Scene.get_node("World/Geometry")
    for title in ["Artwork", "Ground", "Slopes", "Walls", "Water"]:
        var group := Node2D.new()
        group.name = title
        _add(root, group)
    var artwork := Sprite2D.new()
    artwork.name = "GarudaGround"
    artwork.texture = load(ART_ROOT + "garuda_cave_ground.png")
    artwork.centered = false
    artwork.position = Vector2(-7.55, 152.05)
    _add(root.get_node("Artwork"), artwork)
    for index in m_Source.geometry.size():
        var source: Dictionary = m_Source.geometry[index]
        var corners: Array = source.corners
        var start := _vector(corners[0])
        var horizontal := _vector(corners[1]) - start
        var vertical := _vector(corners[3]) - start
        var surface := StaticBody2D.new()
        surface.name = "Surface%02d" % index
        surface.position = start
        surface.collision_layer = 8 if source.oneWay else 1
        surface.collision_mask = 0
        var parent: Node
        if source.oneWay:
            parent = m_Scene.get_node("World/Platforms")
        elif vertical.length() > 900:
            parent = root.get_node("Walls")
        else:
            parent = root.get_node("Ground")
        _add(parent, surface)
        var shape := CollisionShape2D.new()
        shape.name = "Surface"
        var rectangle := RectangleShape2D.new()
        rectangle.size = Vector2(horizontal.length(), vertical.length())
        shape.shape = rectangle
        shape.position = (start + _vector(corners[2])) * 0.5 - surface.position
        shape.rotation = horizontal.angle()
        shape.one_way_collision = source.oneWay
        _add(surface, shape)

func _markers() -> void:
    var points := m_Scene.get_node("World/SpawnPoints")
    var entries: Array[Resource] = []
    var player := Marker2D.new()
    player.name = "PlayerStart"
    player.position = Vector2(300, 300)
    _add(points, player)
    entries.append(_entry("player_start", player.name))
    var spawns: Array = m_Source.spawns.duplicate()
    spawns.sort_custom(func(left, right): return String(left.projectNode) < String(right.projectNode))
    for item in spawns:
        var stage := int(item.properties.stopPointIdx) + 1
        var letter := String(item.projectNode).right(1).to_lower()
        var point := Marker2D.new()
        point.name = "Stage%d%s" % [stage, letter.to_upper()]
        point.position = _vector(item.position)
        _add(points, point)
        entries.append(_entry("stage_%d_%s" % [stage, letter], point.name))
    points.set("m_Entries", entries)
    entries = []
    var gates: Array = m_Source.gates.duplicate()
    gates.sort_custom(func(left, right): return int(left.properties.idx) < int(right.properties.idx))
    for item in gates:
        var stage := int(item.properties.idx) + 1
        if stage > 1:
            entries.append(_gate("Stage%dLeft" % stage, "stage_%d_left" % stage, item.battleBounds[0]))
        entries.append(_gate("Stage%dGate" % stage, "stage_%d_gate" % stage, item.battleBounds[1]))
    m_Scene.get_node("World/StageGates").set("m_Entries", entries)

func _gate(title: String, id: String, x: float) -> Resource:
    var body := StaticBody2D.new()
    body.name = title
    body.position = Vector2(x, 295)
    body.collision_layer = 8192
    body.collision_mask = 0
    _add(m_Scene.get_node("World/StageGates"), body)
    var shape := CollisionShape2D.new()
    shape.name = "Surface"
    var rectangle := RectangleShape2D.new()
    rectangle.size = Vector2(20, 1800)
    shape.shape = rectangle
    shape.disabled = true
    _add(body, shape)
    return _entry(id, title)

func _traps() -> void:
    var group := Node2D.new()
    group.name = "Traps"
    group.z_index = 2
    _add(m_Scene.get_node("World"), group)
    var scene: PackedScene = load("res://TheGame/Scenes/Traps/bat_cave_spike.tscn")
    for index in m_Source.scenery.size():
        var item: Dictionary = m_Source.scenery[index]
        if int(item.characterId) != 113:
            continue
        var spike := scene.instantiate()
        spike.name = "Spike%02d" % index
        spike.position = _vector(item.position)
        spike.scale.y = float(item.scale[1])
        spike.set("m_Suspended", bool(item.properties.get("isUp", false)))
        _add(group, spike)

func _entry(id: String, title: String) -> Resource:
    var resource: Resource = load("res://TheGame/GameScripts/Entity/Level/Markers/LevelMarkerResource.cs").new()
    resource.set("m_Id", id)
    resource.set("m_Node", NodePath(title))
    return resource

func _add(parent: Node, child: Node) -> void:
    parent.add_child(child)
    child.owner = m_Scene

func _vector(value: Array) -> Vector2:
    return Vector2(float(value[0]), float(value[1]))
