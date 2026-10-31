extends SceneTree
## Assemble official 3-1 from extracted source coordinates and staged sea artwork.

var m_Source: Dictionary
var m_Scene: Node2D
var m_Destination: String
var m_ArtRoot: String
var m_ArtPrefix: String

func _initialize() -> void:
    _build.call_deferred()

func _build() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    var args := OS.get_cmdline_user_args()
    var label := args[1] if args.size() > 1 else "3-1"
    var level_id := int(args[2]) if args.size() > 2 else 7
    var directory := args[3] if args.size() > 3 else "zm1_07_shore"
    m_ArtPrefix = args[4] if args.size() > 4 else "sea_shore"
    m_Destination = "res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [directory, level_id]
    m_ArtRoot = "res://TheGame/Sprites/Levels/%s/" % directory
    var previous_uid := ResourceLoader.get_resource_uid(m_Destination) if FileAccess.file_exists(m_Destination) else -1
    m_Source = JSON.parse_string(FileAccess.get_file_as_string(args[0]))[label]
    m_Scene = load("res://TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn").instantiate()
    m_Scene.name = "Level_%d" % level_id
    m_Scene.set("m_LevelId", level_id)
    m_Scene.set("m_MusicId", 107)
    m_Scene.set("m_FeaturedOpponentSpawnPointIds", PackedStringArray(["stage_5_a"]))
    for path in ["World/Geometry", "World/Platforms", "World/SpawnPoints", "World/StageGates"]:
        for child in m_Scene.get_node(path).get_children():
            child.free()
    var front: Sprite2D = m_Scene.get_node("World/Background/Front")
    front.texture = load(m_ArtRoot + m_ArtPrefix + "_front.png")
    front.position = _vec(m_Source.backgroundPosition)
    front.centered = false
    var sky: Sprite2D = m_Scene.get_node("World/Background/Sky/End")
    sky.texture = load("res://TheGame/Sprites/Levels/zm1_07_shore/sea_shore_sky.png")
    sky.position = Vector2.ZERO
    sky.visible = true
    var layouts: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0].get_base_dir().path_join("art/layout.json")))
    var layout: Dictionary = layouts.levels.filter(func(item): return item.label == label)[0]
    _geometry(_vec(layout.terrain[0].local_top_left))
    _markers()
    var camera: Camera2D = m_Scene.get_node("Camera2D")
    var ground: Array = m_Source.geometry.filter(func(item):
        return not item.oneWay and item.corners[1][0] - item.corners[0][0] > 900 and item.corners[3][1] - item.corners[0][1] < 100)
    assert(ground.size() == 1, "Review the level's ground/camera extent before authoring")
    camera.limit_right = ceili(ground[0].corners[1][0])
    var parallax: Parallax2D = m_Scene.get_node("World/Background/Sky")
    parallax.scroll_scale.x = (sky.texture.get_width() - 940.0) / (camera.limit_right - 940.0)
    for item in m_Source.scenery:
        if int(item.characterId) == 86:
            m_Scene.get_node("World/Exit").position = _vec(item.position)
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(m_Destination.get_base_dir()))
    var packed := PackedScene.new()
    assert(packed.pack(m_Scene) == OK)
    assert(ResourceSaver.save(packed, m_Destination) == OK)
    if previous_uid >= 0:
        assert(ResourceSaver.set_uid(m_Destination, previous_uid) == OK)
    m_Scene.free()
    print("SEA %s LEVEL AUTHOR PASS: %d streams and five source battle regions" % [label, m_Source.spawns.size()])
    quit()

## Use source rectangles, with one-way platforms separated from solid terrain.
func _geometry(ground_position: Vector2) -> void:
    var root := m_Scene.get_node("World/Geometry")
    for title in ["Artwork", "Ground", "Slopes", "Walls", "Water"]:
        var group := Node2D.new()
        group.name = title
        _add(root, group)
    var artwork := Sprite2D.new()
    artwork.name = "SeaShoreGround"
    artwork.texture = load(m_ArtRoot + m_ArtPrefix + "_ground.png")
    artwork.centered = false
    artwork.position = ground_position
    _add(root.get_node("Artwork"), artwork)
    for index in m_Source.geometry.size():
        var item: Dictionary = m_Source.geometry[index]
        var corners: Array = item.corners
        var start := _vec(corners[0])
        var horizontal := _vec(corners[1]) - start
        var vertical := _vec(corners[3]) - start
        var body := StaticBody2D.new()
        body.name = "Surface%02d" % index
        body.position = start
        body.collision_layer = 8 if item.oneWay else 1
        body.collision_mask = 0
        var parent: Node = m_Scene.get_node("World/Platforms") if item.oneWay else root.get_node("Walls" if vertical.length() > 900 else "Ground")
        _add(parent, body)
        var shape := CollisionShape2D.new()
        shape.name = "Surface"
        var rectangle := RectangleShape2D.new()
        rectangle.size = Vector2(horizontal.length(), vertical.length())
        shape.shape = rectangle
        shape.position = (start + _vec(corners[2])) * .5 - body.position
        shape.rotation = horizontal.angle()
        shape.one_way_collision = item.oneWay
        _add(body, shape)

## Keep all source spawn positions and independent overlapping battle regions.
func _markers() -> void:
    var points := m_Scene.get_node("World/SpawnPoints")
    var entries: Array[Resource] = []
    var player := Marker2D.new()
    player.name = "PlayerStart"
    player.position = Vector2(300, 300)
    _add(points, player)
    entries.append(_entry("player_start", player.name))
    var spawns: Array = m_Source.spawns.duplicate()
    spawns.sort_custom(func(a, b): return String(a.projectNode) < String(b.projectNode))
    for item in spawns:
        var stage := int(item.properties.stopPointIdx) + 1
        var letter := String(item.projectNode).right(1).to_lower()
        var marker := Marker2D.new()
        marker.name = "Stage%d%s" % [stage, letter.to_upper()]
        marker.position = _vec(item.position)
        _add(points, marker)
        entries.append(_entry("stage_%d_%s" % [stage, letter], marker.name))
    points.set("m_Entries", entries)
    var gates := m_Scene.get_node("World/StageGates")
    entries = []
    var sourceGates: Array = m_Source.gates.duplicate()
    sourceGates.sort_custom(func(a, b): return int(a.properties.idx) < int(b.properties.idx))
    for item in sourceGates:
        var stage := int(item.properties.idx) + 1
        if stage > 1:
            entries.append(_gate("Stage%dLeft" % stage, "stage_%d_left" % stage, item.battleBounds[0]))
        entries.append(_gate("Stage%dGate" % stage, "stage_%d_gate" % stage, item.battleBounds[1]))
    gates.set("m_Entries", entries)

## Create one disabled gate and its resource-backed marker entry.
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

## Keep marker identities readable without duplicating coordinates in tables.
func _entry(id: String, title: String) -> Resource:
    var entry: Resource = load("res://TheGame/GameScripts/Entity/Level/Markers/LevelMarkerResource.cs").new()
    entry.set("m_Id", id)
    entry.set("m_Node", NodePath(title))
    return entry

## Set ownership when authoring children for PackedScene.
func _add(parent: Node, child: Node) -> void:
    parent.add_child(child)
    child.owner = m_Scene

## Read source pixel coordinates from the extracted JSON arrays.
func _vec(value: Array) -> Vector2:
    return Vector2(float(value[0]), float(value[1]))
