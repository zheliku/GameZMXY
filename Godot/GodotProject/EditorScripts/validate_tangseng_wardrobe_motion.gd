extends SceneTree
## Play the production Role2 scene through each audited body/weapon frame mapping.

const SCENE := "res://TheGame/Entitys/Heroes/Tangseng/tangseng_entity.tscn"
const ART := "res://TheGame/Sprites/Characters/Heroes/tangseng/"
const ACTIONS := {"idle1": "wait", "walk": "walk", "run": "run", "jump": "jump1",
    "jump_2": "jump2", "fall": "jump3", "hurt": "hurt", "attack_1": "hit1",
    "skills/ice_dragon": "hit2", "skills/dark_ice": "hit3",
    "skills/water_burst": "hit4", "skills/heavenly_dew": "hit5"}
var failures := 0

func _initialize() -> void:
    _run.call_deferred()

func _check(condition: bool, message: String) -> void:
    if not condition:
        failures += 1
        if failures < 16:
            push_error(message)

func _run() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 2:
        push_error("Expected semantic install manifest and capture directory")
        quit(1)
        return
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var expected := {}
    for frame in range(manifest.source_frames.size()):
        var action: String = manifest.source_frames[frame].action
        if not expected.has(action):
            expected[action] = []
        expected[action].append([int(manifest.frame_cells.body[frame]),
            int(manifest.frame_cells.weapon[frame])])
    var scene: PackedScene = load(SCENE)
    var hero := scene.instantiate()
    hero.process_mode = Node.PROCESS_MODE_DISABLED
    root.add_child(hero)
    _check(int(hero.get("HeroId")) == 2, "Tang scene has the wrong hero identity")
    var body: Sprite2D = hero.get_node("m_Body")
    var weapon: Sprite2D = hero.get_node("m_Weapon")
    var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
    var appearance: Resource = load(ART + "tangseng_appearance.tres")
    var armors: Dictionary = appearance.get("m_ArmorTextures").duplicate()
    var weapons: Dictionary = appearance.get("m_WeaponTextures").duplicate()
    armors[0] = appearance.get("m_BaseBody")
    weapons[0] = appearance.get("m_BaseWeapon")
    _check(armors.size() == 8 and weapons.size() == 9, "Missing one of the 15 registered Tang equipment identities")
    var playback := 0
    for armor in armors:
        for staff in weapons:
            appearance.call("ApplyToSprites", body, weapon, staff, armor)
            _check(body.hframes == 6 and body.vframes == 17 and
                weapon.hframes == 6 and weapon.vframes == 7, "Independent body/weapon grids were lost")
            for direction in [-1, 1]:
                hero.call("SetFacing", direction)
                for name in ACTIONS:
                    var animation: Animation = player.get_animation(name)
                    var source: Array = expected[ACTIONS[name]]
                    player.play(name)
                    for frame in range(source.size()):
                        player.seek((frame + .001) / 24.0, true, true)
                        _check(body.frame == source[frame][0] and weapon.frame == source[frame][1],
                            "Frame mismatch %s/%s/%s/%d" % [armor, staff, name, frame])
                        _check(body.flip_h == weapon.flip_h and hero.position == Vector2.ZERO,
                            "Layer origin or facing changed during playback")
                        playback += 1
                    _check(is_equal_approx(animation.length, source.size() / 24.0), "Source duration changed: " + name)
    var output: String = args[1]
    DirAccess.make_dir_recursive_absolute(output)
    if failures == 0 and DisplayServer.get_name() != "headless":
        var viewport := SubViewport.new()
        viewport.size = Vector2i(2800, 1200)
        viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
        root.add_child(viewport)
        var background := ColorRect.new()
        background.size = viewport.size
        background.color = Color(.18, .22, .25)
        viewport.add_child(background)
        var armor_textures: Array = []
        var staff_textures: Array = []
        for value in armors.values():
            if not armor_textures.has(value):
                armor_textures.append(value)
        for value in weapons.values():
            if not staff_textures.has(value):
                staff_textures.append(value)
        for row in range(armor_textures.size()):
            for column in range(staff_textures.size() * 2):
                var actor := scene.instantiate()
                actor.process_mode = Node.PROCESS_MODE_DISABLED
                actor.position = Vector2(column * 200 + 100, row * 200 + 145)
                viewport.add_child(actor)
                var body_sprite: Sprite2D = actor.get_node("m_Body")
                var weapon_sprite: Sprite2D = actor.get_node("m_Weapon")
                body_sprite.texture = armor_textures[row]
                weapon_sprite.texture = staff_textures[column / 2]
                actor.call("SetFacing", -1 if column % 2 == 0 else 1)
                var animation_player: AnimationPlayer = actor.get_node("m_AnimPlayer")
                animation_player.play("attack_1")
                animation_player.seek(6.001 / 24.0, true, true)
        await process_frame
        await RenderingServer.frame_post_draw
        _check(viewport.get_texture().get_image().save_png(output.path_join("tangseng_pairings.png")) == OK,
            "Cannot save Tang native pairings")
        viewport.free()
    hero.free()
    var report := FileAccess.open(output.path_join("report.json"), FileAccess.WRITE)
    report.store_string(JSON.stringify({"playback_samples": playback, "equipment_pairings": 72,
        "source_frames": 311, "directions": [-1, 1], "failures": failures}, "  "))
    report.close()
    print("TANG WARDROBE MOTION ", "PASS" if failures == 0 else "FAIL", ": ", playback,
        " native samples, 72 equipment pairings, ", failures, " failures; gameplay is not certified")
    quit(0 if failures == 0 else 1)
