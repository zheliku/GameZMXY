extends SceneTree
## Read actual animation values and query Godot shapes for both facing directions.

const PATHS := {
    "flying_demon": "Monsters/Sky/flying_demon/flying_demon_entity",
    "peng_demon_king": "Bosses/zm1_06_garuda_cave/peng_demon_king_entity",
    "tide_demon": "Monsters/Sea/tide_demon/tide_demon_entity",
    "lantern_fish": "Monsters/Sea/lantern_fish/lantern_fish_entity",
    "peng_fire": "Bosses/zm1_06_garuda_cave/peng_fire_effect"
}

func _initialize() -> void:
    _validate.call_deferred()

func _validate() -> void:
    var args := OS.get_cmdline_user_args()
    var plan: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var samples := 0
    var queries := 0
    var probe := CircleShape2D.new()
    probe.radius = .5
    for record in plan.attacks:
        var scene: Node2D = load("res://TheGame/Entitys/Enemies/" + PATHS[record.actor] + ".tscn").instantiate()
        scene.process_mode = Node.PROCESS_MODE_DISABLED
        root.add_child(scene)
        var effect: bool = record.actor == "peng_fire"
        var player: AnimationPlayer = scene.get_node("AnimationPlayer" if effect else "m_AnimPlayer")
        var base := "Origin/Visual/HitBox/CollisionShape2D" if effect else "m_HitBoxRoot/m_HitBox/CollisionShape2D"
        player.play(record.animation)
        if effect:
            player.seek(0, true, true)
            for slot in 3:
                var node: CollisionShape2D = scene.get_node(base + (str(slot + 1) if slot > 0 else ""))
                assert(not node.disabled, "Source fade-in must retain active flames from time zero")
        for frame in record.frames:
            var time: float = record.get("fade_seconds", 0.0) + (frame + .1) / 24.0
            player.seek(time, true, true)
            var entries: Array = record.shapes[frame]
            for slot in (3 if effect else 1):
                var node: CollisionShape2D = scene.get_node(base + (str(slot + 1) if slot > 0 else ""))
                var expected: Dictionary = entries[slot] if entries.size() > slot and entries[slot] != null else {}
                assert(node.disabled == expected.is_empty(), "Wrong active window: " + record.actor + "/" + str(frame))
                samples += 1
                if expected.is_empty():
                    continue
                assert(node.position.distance_to(Vector2(expected.position[0], expected.position[1])) < .001)
                if expected.kind == "circle":
                    assert(node.shape is CircleShape2D and is_equal_approx(node.shape.radius, expected.radius))
                else:
                    assert(node.shape is RectangleShape2D and node.shape.size.distance_to(Vector2(expected.size[0], expected.size[1])) < .001)
                for facing in [-1, 1]:
                    var mirror := Transform2D.IDENTITY.scaled(Vector2(facing, 1))
                    var transform := mirror * node.transform
                    assert(node.shape.collide(transform, probe, Transform2D(0, transform.origin)))
                    var extent: float = node.shape.radius if node.shape is CircleShape2D else node.shape.size.y * .5
                    var outside := transform * Vector2(0, extent + 4)
                    assert(not node.shape.collide(transform, probe, Transform2D(0, outside)))
                    queries += 2
                if record.actor == "flying_demon" and frame < 2:
                    assert(node.shape.size.y < 20 and absf(node.rotation) > .3)
                if record.actor == "tide_demon":
                    assert(node.shape.get_rect().size.x < 280 and node.shape.get_rect().size.y < 280)
        # Ordinary poses must clear the newly added shape rotation after an interrupted attack.
        if not effect:
            player.play("idle")
            player.seek(0, true, true)
            assert(is_zero_approx(scene.get_node(base).rotation))
        scene.free()
    print("NEW ENEMY GEOMETRY VALIDATION PASS: ", samples, " native frame/region samples, ", queries, " physics inclusion/exclusion queries, both directions, rotation reset")
    quit()
