extends SceneTree
## Read matched official source_inventory.json; check all actor roots and timeline durations.
var records: Array = []
var failed := false
func _initialize() -> void:
    create_timer(20).timeout.connect(func(): quit(1))
    call_deferred("_audit")

func _audit() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1, "Pass the matched official source_inventory.json")
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var paths: Array[String] = []
    _collect("res://TheGame/Entitys", source, paths)
    _check(paths.size() == source.size(), "Missing actor scenes from official inventory")
    for path in paths:
        var actor: CharacterBody2D = load(path).instantiate()
        root.add_child(actor)
        actor.set_physics_process(false)
        var sprite: Node2D = actor.get_node("m_Body")
        var body: CollisionShape2D = actor.get_node("CollisionShape2D" if actor.has_node("CollisionShape2D") else "m_BodyShape")
        var foot := body.position.y + body.shape.get_rect().end.y
        var reference: Dictionary = source[path.get_file().get_basename()]
        var bounds: Array = reference.hurt_bounds
        var expected := Rect2(bounds[0], bounds[1], bounds[2], bounds[3])
        var actual := Rect2(body.position + body.shape.get_rect().position, body.shape.get_rect().size)
        var hurt: CollisionShape2D = actor.get_node("m_HurtBox/CollisionShape2D")
        var hurt_bounds := Rect2(hurt.position + hurt.get_parent().position + hurt.shape.get_rect().position, hurt.shape.get_rect().size)
        _check(actual.position.distance_to(expected.position) < 0.02 and actual.size.distance_to(expected.size) < 0.02, "身体范围不符原版 " + path + " actual=" + str(actual) + " expected=" + str(expected))
        _check(hurt_bounds.position.distance_to(expected.position) < 0.02 and hurt_bounds.size.distance_to(expected.size) < 0.02, "受击范围不符原版 " + path)
        var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
        player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        for source_action in reference.actions:
            # Source frame scripts return to wait directly; an unused label must not force a runtime state.
            if source_action.label == "afterHurt":
                continue
            var action_name := _animation_name(source_action.label, path.contains("wukong"))
            _check(player.has_animation(action_name), path + " missing " + action_name)
            if player.has_animation(action_name):
                _check(absf(player.get_animation(action_name).length - float(source_action.frames) / 24) < 0.00001,
                    path + " duration mismatch " + action_name + " actual=" + str(player.get_animation(action_name).length) + " source frames=" + str(source_action.frames))
        for action in ["idle1", "walk", "run"] if path.contains("wukong") else ["idle", "run"]:
            if not player.has_animation(action):
                continue
            var animation := player.get_animation(action)
            var bottoms: Array = []
            for i in 24:
                player.play(action)
                player.seek(animation.length * i / 24.0, true, true)
                var texture: Texture2D
                var picture: Image
                var rectangle: Rect2
                if sprite is Sprite2D:
                    texture = sprite.texture
                    var cell := Vector2i(texture.get_width() / sprite.hframes, texture.get_height() / sprite.vframes)
                    picture = texture.get_image().get_region(Rect2i(sprite.frame_coords * cell, cell))
                    rectangle = sprite.get_rect()
                else:
                    texture = sprite.sprite_frames.get_frame_texture(sprite.animation, sprite.frame)
                    picture = texture.get_image()
                    rectangle = Rect2(sprite.offset - Vector2(texture.get_size()) * (0.5 if sprite.centered else 0.0), texture.get_size())
                var used := picture.get_used_rect()
                bottoms.append(sprite.position.y + rectangle.position.y + used.end.y)
            var record := {"scene":path.get_file(), "action":action, "body_foot":foot, "body_bounds":str(actual), "hurt_bounds":str(hurt_bounds), "visible_foot_min":bottoms.min(), "visible_foot_max":bottoms.max(), "ground_gap_min":foot-bottoms.max(), "ground_gap_max":foot-bottoms.min()}
            records.append(record)
            print("ACTOR REGISTER: ", JSON.stringify(record))
        actor.free()
    var file := FileAccess.open("res://.godot/validation/actor_registration_audit.json", FileAccess.WRITE)
    file.store_string(JSON.stringify(records, "\t"))
    print("ACTOR REGISTRATION ", "FAIL" if failed else "PASS", ": ", paths.size(), " source body/hurt bounds and complete timeline durations.")
    quit(1 if failed else 0)

func _collect(folder: String, source: Dictionary, paths: Array[String]) -> void:
    for file in DirAccess.get_files_at(folder):
        if file.ends_with(".tscn") and source.has(file.get_basename()):
            paths.append(folder.path_join(file))
    for child in DirAccess.get_directories_at(folder):
        _collect(folder.path_join(child), source, paths)

func _animation_name(label: String, hero: bool) -> String:
    if hero:
        var skills := {"hit6": "flame_thrust", "hit7": "blazing_flash", "hit8": "rising_dragon", "hit9": "flame_storm", "hit10": "fire_demon_slash"}
        if skills.has(label):
            return "skills/" + skills[label]
        return {"wait": "idle1", "jump1": "jump", "jump2": "jump_2", "jump3": "fall"}.get(label, label.replace("hit", "attack_"))
    return {"walk": "run", "wait": "idle", "dead": "death", "afterHurt": "after_hurt"}.get(label, label.replace("hit", "attack_"))

func _check(condition: bool, message: String) -> void:
    if not condition:
        failed = true
        push_error(message)
