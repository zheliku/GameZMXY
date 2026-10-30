extends RefCounted
## Import defaults only. Existing scenes own the editable runtime configuration.

const CollisionStyles = preload("res://EditorScripts/enemy_collision_authoring.gd")

const ENGAGE := {
    1: Vector2(80, 160), 2: Vector2(110, 220), 3: Vector2(150, 300),
    7: Vector2(110, 220), 8: Vector2(140, 90), 13: Vector2(150, 300),
    14: Vector2(350, 900), 15: Vector2(150, 100), 16: Vector2(150, 300),
    17: Vector2(400, 800), 18: Vector2(250, 50), 20: Vector2(200, 400),
    25: Vector2(110, 220), 4: Vector2(150, 300), 6: Vector2(250, 900),
    21: Vector2(150, 300), 11: Vector2(150, 300), 9: Vector2(400, 800),
    10: Vector2(250, 500), 19: Vector2(150, 300), 22: Vector2(400, 900),
    23: Vector2(200, 100), 24: Vector2(400, 800), 26: Vector2(450, 300),
}

static func _shape(actor: Node, area: Area2D, label: String, size: Vector2, center := Vector2.ZERO) -> CollisionShape2D:
    var node := area.get_node_or_null(label) as CollisionShape2D
    if node == null:
        node = CollisionShape2D.new()
        node.name = label
        area.add_child(node)
        node.owner = actor
    var rectangle := RectangleShape2D.new()
    rectangle.size = size
    node.shape = rectangle
    node.position = center
    node.disabled = true
    return node

static func _horizontal(actor: Node, area: Area2D, label: String, radius: float) -> CollisionShape2D:
    var node := _shape(actor, area, label, Vector2(radius * 2, 1))
    var line := SegmentShape2D.new()
    line.a = Vector2(-radius, 0)
    line.b = Vector2(radius, 0)
    node.shape = line
    return node

static func _vertical(actor: Node, area: Area2D, label: String, radius: float) -> CollisionShape2D:
    var node := _shape(actor, area, label, Vector2(1, radius * 2))
    var line := SegmentShape2D.new()
    line.a = Vector2(0, -radius)
    line.b = Vector2(0, radius)
    node.shape = line
    return node

static func configure(actor: Node) -> void:
    var id: int = actor.get("m_EnemyEntityId")
    assert(ENGAGE.has(id))
    var area := actor.get_node_or_null("Perception") as Area2D
    if area == null:
        area = actor.get_node("m_Detector")
        area.name = "Perception"
    area.set_script(load("res://TheGame/GameScripts/Entity/Enemies/Perception/EnemyPerceptionNode.cs"))
    var detection: CollisionShape2D = area.get_child(0)
    detection.name = "Detection"
    area.set("m_DetectionShape", detection)
    var engage := _shape(actor, area, "Engage", ENGAGE[id] * 2)
    if id == 9:
        var circle := CircleShape2D.new()
        circle.radius = 400
        engage.shape = circle
    if id in [14, 23, 26]:
        engage = _horizontal(actor, area, "Engage", ENGAGE[id].x)
    area.set("m_EngageShape", engage)
    area.set("m_PatrolShape", _horizontal(actor, area, "Patrol", 400))
    if id == 22:
        area.get_node("Patrol").shape.a = Vector2(-360, 0)
    if id in [21, 22, 24]:
        area.set("m_EngageShape", null)
        engage.free()
    if id in [21, 24]:
        var unused_patrol := area.get_node("Patrol")
        area.set("m_PatrolShape", null)
        unused_patrol.free()
    actor.set("m_Perception", area)
    if id in [8, 15, 18]:
        actor.set("m_TrackingRange", _vertical(actor, area, "Tracking", 900))
    elif id == 14:
        actor.set("m_RetreatRange", _horizontal(actor, area, "Retreat", 200))
    elif id == 9:
        var melee := _shape(actor, area, "Melee", Vector2(300, 300))
        var circle := CircleShape2D.new()
        circle.radius = 150
        melee.shape = circle
        actor.set("m_MeleeRange", melee)
        actor.set("m_TrackingRange", _vertical(actor, area, "Tracking", 800))
    elif id == 26:
        actor.set("m_WaterColumnRange", _horizontal(actor, area, "WaterColumn", 100))
    elif id == 23:
        actor.set("m_SwallowRange", _horizontal(actor, area, "Swallow", 80))
        actor.set("m_StompRange", _horizontal(actor, area, "Stomp", 100))
        actor.set("m_CaptureRange", _horizontal(actor, area, "Capture", 200))
    CollisionStyles.apply(actor)
