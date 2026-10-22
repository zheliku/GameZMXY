extends SceneTree
## 只读核对六类角色待机/移动的透明像素脚底、身体盒和受击盒，保留坐标证据。
var records: Array = []
func _initialize() -> void:
    create_timer(20).timeout.connect(func(): quit(1))
    call_deferred("_audit")

func _audit() -> void:
    var paths := ["res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn", "res://TheGame/Entitys/Enemies/Monsters/huaguoshan_monkey_entity.tscn", "res://TheGame/Entitys/Enemies/Monsters/demon_monkey_entity.tscn", "res://TheGame/Entitys/Enemies/Monsters/gorilla_entity.tscn", "res://TheGame/Entitys/Enemies/Bosses/zm1_02_macaque_king/macaque_king_entity.tscn", "res://TheGame/Entitys/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_entity.tscn"]
    # 来自完整 colipse 变换；两个网格原点的 Y 平移也必须应用于身体和受击盒。
    var registrations := [
        Rect2(-22.10061, -17.055495, 45.00122, 90.01099),
        Rect2(-20.048932, -42.549543, 39.997864, 59.999085),
        Rect2(-20.449909, -41.45029, 41.999817, 82.00058),
        Rect2(-54.498015, -55.04893, 109.99603, 109.99786),
        Rect2(-29.2972535, -60.049085, 59.994507, 119.99817),
        Rect2(-21.9983215, -40.0498475, 44.996643, 79.999695)
    ]
    for path in paths:
        var actor: CharacterBody2D = load(path).instantiate()
        root.add_child(actor)
        actor.set_physics_process(false)
        var sprite: Node2D = actor.get_node("m_Body")
        var body: CollisionShape2D = actor.get_node("CollisionShape2D" if actor.has_node("CollisionShape2D") else "m_BodyShape")
        var foot := body.position.y + body.shape.get_rect().end.y
        var expected: Rect2 = registrations[paths.find(path)]
        var actual := Rect2(body.position + body.shape.get_rect().position, body.shape.get_rect().size)
        var hurt: CollisionShape2D = actor.get_node("m_HurtBox/CollisionShape2D")
        var hurt_bounds := Rect2(hurt.position + hurt.get_parent().position + hurt.shape.get_rect().position, hurt.shape.get_rect().size)
        assert(actual.position.distance_to(expected.position) < 0.02 and actual.size.distance_to(expected.size) < 0.02, "身体范围不符原版 " + path)
        assert(hurt_bounds.position.distance_to(expected.position) < 0.02 and hurt_bounds.size.distance_to(expected.size) < 0.02, "受击范围不符原版 " + path)
        var player: AnimationPlayer = actor.get_node("m_AnimPlayer")
        player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        for action in ["idle1", "walk", "run"] if path.contains("wukong") else ["idle", "run"]:
            var animation := player.get_animation(action)
            var bottoms: Array = []
            for i in 24:
                player.play(action)
                player.seek(animation.length * i / 24.0, true)
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
    print("ACTOR REGISTRATION PASS: six source body/hurt bounds, 312 idle/movement samples; original foot overlap retained.")
    quit()
