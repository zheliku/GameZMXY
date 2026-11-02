extends SceneTree
## Match the seven independently placed source ice pillars with seven native shapes.

func _initialize() -> void:
    var path := "res://TheGame/Entitys/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_entity.tscn"
    var scene: Node = load(path).instantiate()
    var area: Area2D = scene.get_node("m_HitBoxRoot/m_HitBox")
    var first: CollisionShape2D = area.get_child(0)
    for index in range(1, 7):
        var name := "CollisionShape2D" + str(index + 1)
        if area.has_node(NodePath(name)):
            area.get_node(NodePath(name)).shape = first.shape
            continue
        var shape := CollisionShape2D.new()
        shape.name = name
        shape.shape = first.shape
        shape.disabled = true
        shape.debug_color = first.debug_color
        area.add_child(shape)
        shape.owner = scene
    var uid := ResourceLoader.get_resource_uid(path)
    var packed := PackedScene.new()
    assert(packed.pack(scene) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    scene.free()
    print("DRAGON SHAPES AUTHOR PASS: seven original independent pillars, shared attack ownership")
    quit()
