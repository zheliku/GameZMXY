extends SceneTree
## Apply the reviewed offline measurements to native geometry tracks only.

const ACTOR_PATHS := {
    "flying_demon": "Monsters/Sky/flying_demon/flying_demon",
    "peng_demon_king": "Bosses/zm1_06_garuda_cave/peng_demon_king",
    "tide_demon": "Monsters/Sea/tide_demon/tide_demon",
    "lantern_fish": "Monsters/Sea/lantern_fish/lantern_fish",
    "peng_fire": "Bosses/zm1_06_garuda_cave/peng_fire",
    "bull_demon_king": "Bosses/zm1_10_bull_demon_valley/bull_demon_king",
    "dragon_king": "Bosses/zm1_11_hidden_dragon_palace/dragon_king",
    "dragon_king_wave": "Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave",
    "dragon_king_torrent": "Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent"
}

const EFFECTS := ["peng_fire", "dragon_king_wave", "dragon_king_torrent"]

func _initialize() -> void:
    _apply.call_deferred()

func _track(animation: Animation, path: NodePath, times: Array, values: Array) -> void:
    var index := animation.find_track(path, Animation.TYPE_VALUE)
    if index < 0:
        index = animation.add_track(Animation.TYPE_VALUE)
        animation.track_set_path(index, path)
    animation.value_track_set_update_mode(index, Animation.UPDATE_DISCRETE)
    while animation.track_get_key_count(index) > 0:
        animation.track_remove_key(index, 0)
    var previous = null
    for i in times.size():
        if previous == null or previous != values[i]:
            animation.track_insert_key(index, times[i], values[i])
        previous = values[i]

func _shape(value: Dictionary, palette: Dictionary) -> Shape2D:
    var key: String = JSON.stringify(value.get("size", value.get("radius"))) + value.kind
    if not palette.has(key):
        if value.kind == "circle":
            var circle := CircleShape2D.new()
            circle.radius = value.radius
            palette[key] = circle
        else:
            var rectangle := RectangleShape2D.new()
            rectangle.size = Vector2(value.size[0], value.size[1])
            palette[key] = rectangle
    return palette[key]

func _apply() -> void:
    assert(Engine.is_editor_hint())
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1)
    var plan: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var libraries := {}
    var palettes := {}
    var counts := 0
    var library_slots := {}
    for record in plan.attacks:
        var relative: String = ACTOR_PATHS[record.actor]
        var path := "res://TheGame/Entitys/Animations/Enemies/" + relative + "_library.tres"
        if not libraries.has(path):
            libraries[path] = load(path)
            palettes[path] = {}
        var library: AnimationLibrary = libraries[path]
        var animation: Animation = library.get_animation(record.animation)
        var base := "Origin/Visual/HitBox/CollisionShape2D" if record.actor in EFFECTS else "m_HitBoxRoot/m_HitBox/CollisionShape2D"
        var count := int(record.get("slots", 3 if record.actor == "peng_fire" else 1))
        library_slots[path] = maxi(library_slots.get(path, 1), count)
        for slot in count:
            var node_path := base + (str(slot + 1) if slot > 0 else "")
            var times: Array = []
            var shapes: Array = []
            var positions: Array = []
            var rotations: Array = []
            var disabled: Array = []
            var entries: Array = record.shapes.duplicate()
            var fade: float = record.get("fade_seconds", 0.0)
            if fade > 0:
                entries.push_front(entries[0])
            for index in entries.size():
                var time := 0.0 if fade > 0 and index == 0 else fade + (index - (1 if fade > 0 else 0)) / 24.0
                times.append(time)
                var value: Dictionary = entries[index][slot] if entries[index].size() > slot and entries[index][slot] != null else {}
                disabled.append(value.is_empty())
                # Disabled frames share one immutable placeholder instead of hundreds of identical resources.
                shapes.append(_shape(value, palettes[path]) if not value.is_empty() else _shape({"kind": "rectangle", "size": [1, 1]}, palettes[path]))
                positions.append(Vector2(value.position[0], value.position[1]) if not value.is_empty() else Vector2.ZERO)
                rotations.append(value.get("rotation", 0.0))
            _track(animation, NodePath(node_path + ":shape"), times, shapes)
            _track(animation, NodePath(node_path + ":position"), times, positions)
            _track(animation, NodePath(node_path + ":rotation"), times, rotations)
            _track(animation, NodePath(node_path + ":disabled"), times, disabled)
        counts += record.frames
    for path in libraries:
        var library: AnimationLibrary = libraries[path]
        # Rotation is newly authored: other actions must clear it after interruption or a later attack.
        var base := "Origin/Visual/HitBox/CollisionShape2D" if path.contains("peng_fire") or path.contains("dragon_king_wave") or path.contains("dragon_king_torrent") else "m_HitBoxRoot/m_HitBox/CollisionShape2D"
        var slots: int = library_slots[path]
        for name in library.get_animation_list():
            var animation := library.get_animation(name)
            for slot in slots:
                var target := NodePath(base + (str(slot + 1) if slot > 0 else "") + ":rotation")
                if animation.find_track(target, Animation.TYPE_VALUE) < 0:
                    _track(animation, target, [0.0], [0.0])
                var disabled := NodePath(base + (str(slot + 1) if slot > 0 else "") + ":disabled")
                if slot > 0 and animation.find_track(disabled, Animation.TYPE_VALUE) < 0:
                    _track(animation, disabled, [0.0], [true])
        var uid := ResourceLoader.get_resource_uid(path)
        assert(ResourceSaver.save(libraries[path], path) == OK)
        assert(ResourceSaver.set_uid(path, uid) == OK)
    print("NEW ENEMY GEOMETRY AUTHOR PASS: ", libraries.size(), " libraries, ", counts, " source frames; only shape/position/rotation/disabled tracks")
    quit()
