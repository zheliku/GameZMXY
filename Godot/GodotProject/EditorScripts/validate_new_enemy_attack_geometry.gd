extends SceneTree
## Read actual animation values and query Godot shapes for both facing directions.

const PATHS := {
    "flying_demon": "Monsters/Sky/flying_demon/flying_demon_entity",
    "peng_demon_king": "Bosses/zm1_06_garuda_cave/peng_demon_king_entity",
    "tide_demon": "Monsters/Sea/tide_demon/tide_demon_entity",
    "lantern_fish": "Monsters/Sea/lantern_fish/lantern_fish_entity",
    "peng_fire": "Bosses/zm1_06_garuda_cave/peng_fire_effect",
    "bull_demon_king": "Bosses/zm1_10_bull_demon_valley/bull_demon_king_entity",
    "dragon_king": "Bosses/zm1_11_hidden_dragon_palace/dragon_king_entity",
    "dragon_king_wave": "Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave_effect",
    "dragon_king_torrent": "Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent_effect"
}

const EFFECTS := ["peng_fire", "dragon_king_wave", "dragon_king_torrent"]

func _initialize() -> void:
    _validate.call_deferred()

func _overlaps_any(nodes: Array, mirror: Transform2D, probe: Shape2D, point: Vector2) -> bool:
    for node: CollisionShape2D in nodes:
        if not node.disabled and node.shape.collide(mirror * node.transform, probe, Transform2D(0, point)):
            return true
    return false

func _validate() -> void:
    var args := OS.get_cmdline_user_args()
    var plan: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var samples := 0
    var queries := 0
    var ice_gap_queries := 0
    var probe := CircleShape2D.new()
    probe.radius = .5
    for record in plan.attacks:
        var scene: Node2D = load("res://TheGame/Entitys/Enemies/" + PATHS[record.actor] + ".tscn").instantiate()
        scene.process_mode = Node.PROCESS_MODE_DISABLED
        root.add_child(scene)
        var effect: bool = record.actor in EFFECTS
        var player: AnimationPlayer = scene.get_node("AnimationPlayer" if effect else "m_AnimPlayer")
        var base := "Origin/Visual/HitBox/CollisionShape2D" if effect else "m_HitBoxRoot/m_HitBox/CollisionShape2D"
        var nodes: Array = []
        for slot in int(record.get("slots", 3 if record.actor == "peng_fire" else 1)):
            nodes.append(scene.get_node(base + (str(slot + 1) if slot > 0 else "")))
        player.play(record.animation)
        if record.actor == "peng_fire":
            player.seek(0, true, true)
            for slot in 3:
                var node: CollisionShape2D = scene.get_node(base + (str(slot + 1) if slot > 0 else ""))
                assert(not node.disabled, "Source fade-in must retain active flames from time zero")
        for frame in record.frames:
            var time: float = record.get("fade_seconds", 0.0) + (frame + .1) / 24.0
            player.seek(time, true, true)
            var entries: Array = record.shapes[frame]
            for slot in int(record.get("slots", 3 if record.actor == "peng_fire" else 1)):
                var node: CollisionShape2D = scene.get_node(base + (str(slot + 1) if slot > 0 else ""))
                var expected: Dictionary = entries[slot] if entries.size() > slot and entries[slot] != null else {}
                assert(node.disabled == expected.is_empty(), "Wrong active window: " + record.actor + "/" + str(frame))
                samples += 1
                if expected.is_empty():
                    continue
                assert(node.position.distance_to(Vector2(expected.position[0], expected.position[1])) < .001)
                assert(is_equal_approx(node.rotation, float(expected.get("rotation", 0.0))))
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
            if record.actor == "dragon_king" and record.animation == "attack_2":
                # Separate pillars leave real horizontal spaces during growth/fade.
                # Query the entire set: testing each box alone would miss a stale union box.
                var active := nodes.filter(func(node: CollisionShape2D) -> bool: return not node.disabled)
                active.sort_custom(func(a: CollisionShape2D, b: CollisionShape2D) -> bool: return a.position.x < b.position.x)
                for index in range(active.size() - 1):
                    var left: Rect2 = active[index].transform * active[index].shape.get_rect()
                    var right: Rect2 = active[index + 1].transform * active[index + 1].shape.get_rect()
                    if right.position.x - left.end.x <= 2:
                        continue
                    var point := Vector2((left.end.x + right.position.x) / 2, (maxf(left.position.y, right.position.y) + minf(left.end.y, right.end.y)) / 2)
                    for facing in [-1, 1]:
                        var mirror := Transform2D.IDENTITY.scaled(Vector2(facing, 1))
                        assert(not _overlaps_any(nodes, mirror, probe, mirror * point), "Invisible ice-pillar gap still deals damage")
                        ice_gap_queries += 1
        # Ordinary poses must clear the newly added shape rotation after an interrupted attack.
        if not effect:
            player.play("idle")
            player.seek(0, true, true)
            for node: CollisionShape2D in nodes:
                assert(is_zero_approx(node.rotation))
                if record.actor == "dragon_king":
                    assert(node.disabled, "Ice pillar survives ordinary pose/reset")
        scene.free()
    print("NEW ENEMY GEOMETRY VALIDATION PASS: ", samples, " native frame/region samples, ", queries, " physics inclusion/exclusion queries, ", ice_gap_queries, " separate-pillar gap queries, both directions, rotation reset")
    quit()
