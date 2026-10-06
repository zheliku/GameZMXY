extends SceneTree
## 校验正式关卡的 Tool Resource、条目绑定、UID 路径和保留的地形。
## 运行：Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --script res://EditorScripts/validate_level_scene.gd

var failures: Array[String] = []

func _init() -> void:
	call_deferred("validate")

func validate() -> void:
	var packed := load("res://TheGame/Scenes/Level_1.tscn") as PackedScene
	if packed == null:
		push_error("LEVEL SCENE FAIL: 无法加载正式关卡")
		quit(1)
		return
	var level := packed.instantiate()
	var snapshot := OS.get_cmdline_user_args().has("--snapshot")
	if snapshot:
		root.size = Vector2i(940, 590)
		for path in ["World/SpawnPoints", "World/StageGates", "World/StageTriggers", "Camera2D"]:
			level.get_node(path).set("m_DrawInGame", true)
	root.add_child(level)
	await process_frame
	for path in ["World/SpawnPoints", "World/StageGates", "World/StageTriggers"]:
		var manager := level.get_node(path)
		check(manager.get_script().is_tool(), "%s 必须运行 Tool 脚本" % path)
		var warnings = manager.call("_GetConfigurationWarnings")
		check(warnings.size() == 0, "%s 有条目绑定警告：%s" % [path, warnings])
		for entry in manager.get("m_Entries"):
			check(entry != null and entry.get_script().is_tool(), "条目必须是可在编辑器中运行的 Tool Resource")
			check(not str(entry.get("m_Id")).is_empty(), "条目必须有可读 ID")
			check(manager.get_node_or_null(entry.get("m_Node")) != null, "条目的直接子节点引用必须有效")
		manager.queue_redraw()
	check(level.get_node("World/SpawnPoints").get("m_Entries").size() == 6, "六个生成点必须保留")
	check(level.get_node("World/StageGates").get("m_Entries").size() == 3, "三个阶段门必须保留")
	check(level.get_node("World/StageTriggers").get_child_count() == 0, "普通关卡不需要刷怪触发区")
	check(level.get_node("World/Geometry/Ground/Slope1").get_child_count() == 3, "用户添加的斜坡碰撞必须保留")
	check(level.get_node("World/Geometry/Ground/FloorGround").position == Vector2(2406, 545), "用户调整的地面位置必须保留")
	check(level.get_node("Camera2D").get_script().is_tool(), "相机必须支持编辑器引导绘制")
	await process_frame
	if snapshot:
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		DirAccess.make_dir_recursive_absolute("res://.godot/validation")
		check(image.save_png("res://.godot/validation/level_guides.png") == OK, "绘制快照保存失败")
		var marker_color := image.get_pixel(316, 300)
		var camera_color := image.get_pixel(430, 100)
		check(marker_color.b > 0.7 and marker_color.g > 0.6, "生成点圆圈未出现在实际绘制中")
		check(camera_color.b > 0.7 and camera_color.g > 0.6, "相机死区未出现在实际绘制中")
		print("LEVEL GUIDES SNAPSHOT: res://.godot/validation/level_guides.png")
	if OS.get_cmdline_user_args().has("--resize"):
		root.size = Vector2i(1920, 1080)
		await process_frame
		check(level.get_node("Camera2D").get("ViewSize") == Vector2(940, 590), "宽屏窗口必须保持设计取景，不能超过阶段可容纳的视野")
	if OS.get_cmdline_user_args().has("--fractional"):
		var target := Node2D.new()
		level.add_child(target)
		target.global_position = Vector2(1250.4, 300)
		var camera := level.get_node("Camera2D")
		camera.call("Follow", target)
		camera.call("TravelTo", 1680.4)
		camera.call("_PhysicsProcess", 2.0)
		check(absf(camera.get("ViewRight") - 1680.4) < 0.02, "分数坐标的阶段右界不得被原生整数限位裁掉")
		camera.call("StopFollowing")
	level.queue_free()
	if failures.is_empty():
		print("LEVEL SCENE PASS: Tool 条目、管理器引用、地形和相机全部通过（editor_hint=%s）" % Engine.is_editor_hint())
	else:
		for failure in failures:
			push_error("LEVEL SCENE FAIL: " + failure)
	quit(0 if failures.is_empty() else 1)

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
