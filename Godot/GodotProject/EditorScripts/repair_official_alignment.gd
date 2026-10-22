extends SceneTree
## 以官方清单修复背景、可见地形和出生点；资源只通过原生打包保存。

func _initialize() -> void:
	create_timer(40).timeout.connect(func(): quit(1))
	_run.call_deferred()

func _run() -> void:
	assert(Engine.is_editor_hint())
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for i in 10:
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	for number in [1, 2, 3]:
		var path := "res://TheGame/Scenes/Levels/%s/level_%d.tscn" % [["zm1_01_huaguoshan", "zm1_02_shuiliandong", "zm1_03_taohuayuan"][number - 1], number]
		var level: Node2D = load(path).instantiate()
		preload("res://EditorScripts/align_official_level_art.gd").align(level, source["1-%d" % number], number)
		_save(level, path)
	var hud_path := "res://TheGame/UIs/battle_form.tscn"
	var hud: Control = load(hud_path).instantiate()
	var ws: TextureProgressBar = hud.get_node("MenuPanel/WsFrame/m_WsBar")
	ws.position.x = -130
	_save(hud, hud_path)
	var boss_path := "res://TheGame/UIs/Battle/encounter_health_bar.tscn"
	var boss: Control = load(boss_path).instantiate()
	boss.get_node("Health").texture_progress_offset = Vector2(0, -3)
	_save(boss, boss_path)
	print("OFFICIAL ALIGNMENT SAVE PASS: full source terrain matrices, original spawns and stage bounds, source sky origin; WS -5px X and boss fill -3px Y.")
	quit()

func _save(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	node.free()
