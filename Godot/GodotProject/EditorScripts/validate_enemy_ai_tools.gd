extends SceneTree
## Compile all affected import/validation tools; never run their destructive generation routines.

func _initialize() -> void:
    _run.call_deferred()

func _run() -> void:
    var files := ["enemy_perception_authoring", "register_enemy_perception", "validate_enemy_perception",
        "register_sea_actor_resources", "register_east_sea_resources", "register_shark_actor_resources",
        "register_bull_actor_resources", "import_official_dragon_level", "create_qixing_actor_scenes",
        "import_official_bat_cave_actors", "import_official_boss_scenes", "validate_garuda_level",
        "validate_qixing_level", "validate_enemy_fidelity", "apply_enemy_detection_ranges", "enemy_collision_authoring", "review_enemy_ranges",
        "validate_enemy_spatial_rules", "render_enemy_collision_styles", "repair_monster_health_bars"]
    for file in files:
        var script: GDScript = load("res://EditorScripts/" + file + ".gd")
        if script == null or script.reload() != OK:
            printerr("ENEMY AI TOOL VALIDATION FAIL: " + file)
            quit(1)
            return
    print("ENEMY AI TOOL VALIDATION PASS: %d scripts" % files.size())
    quit()
