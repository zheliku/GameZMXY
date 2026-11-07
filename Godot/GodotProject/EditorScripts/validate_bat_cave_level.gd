extends SceneTree
## Verify the authored fourth-level scene's stable marker, gate and trap assembly.

func _initialize() -> void:
	call_deferred("_validate")

func _validate() -> void:
	var level: Node2D = load("res://TheGame/Scenes/Levels/zm1_04_bat_cave/level_4.tscn").instantiate()
	assert(level.get("_levelId") == 4)
	assert(level.get("_featuredOpponentSpawnPointIds").has("stage_5_d"))
	var points := level.get_node("World/SpawnPoints")
	for id in ["stage_1_a","stage_1_b","stage_2_a","stage_2_b","stage_2_c","stage_3_a","stage_4_a","stage_4_b","stage_4_c","stage_4_d","stage_4_e","stage_4_f","stage_4_g","stage_5_a","stage_5_b","stage_5_c","stage_5_d"]:
		assert(points.get("_entries").any(func(entry): return entry.get("_id") == id))
	assert(level.get_node("World/StageGates").get("_entries").size() == 9)
	assert(level.get_node("World/Geometry").find_children("*", "CollisionShape2D", true, false).size() == 6)
	assert(level.get_node("World/Traps").get_child_count() == 35)
	assert(load("res://TheGame/Entitys/Enemies/Monsters/Sky/bat/bat_entity.tscn") != null)
	assert(load("res://TheGame/Entitys/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_entity.tscn") != null)
	print("BAT CAVE VALIDATION PASS: level=4, 17 markers, 9 gates, 35 traps, bat/statue scenes")
	level.free()
	quit()
