extends RefCounted
## Shared native collision palette for enemy scene authoring; no runtime drawing.

static func apply(node: Node, role := "body") -> void:
    if node.name == "Perception":
        role = "perception"
    elif "HitBox" in String(node.name) or node.name == "Damage":
        role = "hit"
    elif "HurtBox" in String(node.name):
        role = "hurt"
    if node is CollisionShape2D:
        var color := Color(0.58, 0.64, 0.72, 0.12)
        if role == "hit":
            color = Color(0.88, 0.36, 0.30, 0.18)
        elif role == "hurt":
            color = Color(0.30, 0.66, 0.62, 0.14)
        elif role == "perception":
            if node.name == "Detection":
                color = Color(0.36, 0.58, 0.80, 0.035)
            elif node.name == "Patrol":
                color = Color(0.45, 0.62, 0.48, 0.03)
            else:
                color = Color(0.85, 0.65, 0.35, 0.045)
        node.debug_color = color
        assert(node.get_property_list().any(func(property): return property.name == "debug_fill"))
        node.set("debug_fill", role in ["body", "hit"])
    for child in node.get_children():
        apply(child, role)

