extends SceneTree
## Check the complete native scene graph against the official 2-3 extraction.

func _initialize() -> void:
    _validate.call_deferred()

func _validate() -> void:
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))["2-3"]
    var scene: Node2D = load("res://TheGame/Scenes/Levels/zm1_06_garuda_cave/level_6.tscn").instantiate()
    assert(scene.get("m_LevelId") == 6 and scene.get("m_MusicId") == 106)
    assert(scene.get("m_FeaturedOpponentSpawnPointIds") == PackedStringArray(["stage_5_a"]))
    assert(scene.get_node("World/SpawnPoints").get("m_Entries").size() == 23)
    assert(scene.get_node("World/StageGates").get("m_Entries").size() == 9)
    assert(scene.get_node("World/Platforms").get_child_count() == 4)
    assert(scene.get_node("World/Geometry").find_children("*", "CollisionShape2D", true, false).size() == 3)
    assert(scene.get_node("World/Traps").get_child_count() == 39)
    assert(scene.get_node("World/Exit").position.is_equal_approx(Vector2(4266.55, 424.15)))
    for item in source.spawns:
        var node: Marker2D = scene.get_node("World/SpawnPoints/" + item.projectNode)
        assert(node.position.is_equal_approx(Vector2(item.position[0], item.position[1])))
    for gate in source.gates:
        var stage := int(gate.properties.idx) + 1
        assert(is_equal_approx(scene.get_node("World/StageGates/Stage%dGate" % stage).position.x, gate.battleBounds[1]))
        if stage > 1:
            assert(is_equal_approx(scene.get_node("World/StageGates/Stage%dLeft" % stage).position.x, gate.battleBounds[0]))
    for path in ["Bosses/zm1_06_garuda_cave/peng_demon_king_entity", "Monsters/Sky/flying_demon/flying_demon_entity"]:
        var actor: CharacterBody2D = load("res://TheGame/Entitys/Enemies/" + path + ".tscn").instantiate()
        assert(actor.collision_mask == 9)
        for binding in ["m_Body", "m_AnimPlayer", "m_HitBox", "m_HurtBox", "m_HitBoxRoot", "m_Detector"]:
            assert(actor.get(binding) != null)
        var player: AnimationPlayer = actor.get("m_AnimPlayer")
        for key in player.get_animation_list():
            var animation := player.get_animation(key)
            for track in animation.get_track_count():
                var previous := -1.0
                for index in animation.track_get_key_count(track):
                    var time := animation.track_get_key_time(track, index)
                    assert(time > previous, "Duplicate native method/value time: " + path + "/" + key)
                    previous = time
        actor.free()
    var effect: Node2D = load("res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/peng_fire_effect.tscn").instantiate()
    var box: Area2D = effect.get_node("Origin/Visual/HitBox")
    assert(box.collision_layer == 64 and box.collision_mask == 128 and box.get_child_count() == 3)
    var animation: Animation = effect.get_node("AnimationPlayer").get_animation("play")
    assert(is_equal_approx(animation.length, .6 + 15.0 / 24))
    effect.free()
    var form: Control = load("res://TheGame/UIs/map_form.tscn").instantiate()
    assert(form.get("m_Jialuoluo") == form.get_node("m_Jialuoluo"))
    var library: AnimationLibrary = form.get_node("m_LocationAnimation").get_animation_library("jialuoluo")
    assert(is_equal_approx(library.get_animation("current").length, 19.0 / 24))
    form.free()
    scene.free()
    print("GARUDA 2-3 NATIVE VALIDATION PASS: source coordinates, 22 streams, 9 gates, 7 surfaces, 39 traps, actors/fire/map")
    quit()
