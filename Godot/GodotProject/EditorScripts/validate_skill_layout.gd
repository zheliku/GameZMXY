extends SceneTree
## 检查造一技能页与战斗素材，左侧保留造三五入口并验证无双黑色长底板。


func _initialize() -> void:
	create_timer(20.0).timeout.connect(func(): quit(1))
	_run.call_deferred()


## 只移除测试实例的业务脚本；不启动流程、不改模型，也不写正式存档。
func _strip_scripts(node: Node) -> void:
	for child in node.get_children():
		_strip_scripts(child)
	node.set_script(null)


## 保持 940×590 设计坐标，并分别采集未学习、学习后、被动页和放大画面。
func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("SKILL LAYOUT FAIL: 需要真实渲染器。")
		quit(1)
		return
	var viewport := SubViewport.new()
	viewport.size = Vector2i(940, 590)
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var form: Control = load("res://TheGame/UIs/skill_form.tscn").instantiate()
	_strip_scripts(form)
	viewport.add_child(form)
	var rows: Control = form.get_node("Content/m_ActivePage/m_WukongRows")
	var positions := [Vector2(192.05, 124.7), Vector2(193.05, 201.85), Vector2(193.05, 284), Vector2(193.05, 364.15), Vector2(193.05, 443.3)]
	await process_frame
	await process_frame
	for i in range(5):
		var icon: TextureButton = rows.get_node("Skill%d/IconCell/Icon" % (i + 1))
		assert(icon.global_position.distance_to(positions[i]) < 0.02, "学习图标偏离官方坐标")
	await _capture(viewport, "skills_layout_active")
	form.get_node("Content/m_ActivePage").hide()
	form.get_node("Content/m_PassivePage").show()
	await _capture(viewport, "skills_layout_passive")
	form.queue_free()
	await process_frame
	var background := ColorRect.new()
	background.color = Color("809561")
	background.size = Vector2(940, 590)
	viewport.add_child(background)
	var hud: Control = load("res://TheGame/UIs/BattleForm.tscn").instantiate()
	_strip_scripts(hud)
	viewport.add_child(hud)
	var menu: Sprite2D = hud.get_node("MenuPanel")
	assert(menu.position == Vector2(90, 531) && menu.texture.resource_path.ends_with("hud_menu_panel.png"))
	for entry in [["m_InventoryButton", "inventory"], ["m_SettingsButton", "settings"], ["m_SkillsButton", "skills"], ["WeaponButton", "weapon"], ["PetButton", "pet"]]:
		var button: TextureButton = menu.get_node(entry[0])
		assert(button.visible && button.texture_normal.resource_path.ends_with("menu_%s_normal.png" % entry[1]), "旧菜单素材被替换或隐藏")
	var frame: Sprite2D = menu.get_node("WsFrame")
	assert(frame.texture != null && frame.texture.resource_path.ends_with("hud_ws_frame.png"), "无双黑色长底板缺失")
	assert(frame.position == Vector2(113, 25) && frame.texture.get_size() == Vector2(260, 22), "无双底板偏离造三菜单布局")
	var ws_bar: TextureProgressBar = frame.get_node("m_WsBar")
	assert(ws_bar.scale == Vector2(1.17, 1.03), "无双条宽度未适配黑色底板")
	var atlas: SpriteFrames = load("res://TheGame/Sprites/UI/hud/battle_skill_icon_frames.tres")
	var names := ["RisingDragon", "FlameThrust", "BlazingFlash", "FlameStorm", "FireDemonSlash"]
	for state in range(2):
		ws_bar.value = state * 0.5
		for i in range(5):
			var icon: TextureRect = hud.get_node("MenuPanel/SkillBar/Skill%d/Icon" % (i + 1))
			icon.texture = atlas.get_frame_texture(names[i], state)
			assert(icon.size == Vector2(45, 45), "战斗栏图标不是原版 45 像素")
			assert(absf(icon.global_position.y - 506.0) < 0.02, "技能栏没有对齐造三菜单布局")
		await _capture(viewport, "skills_layout_hud_%d" % state)
	var glow: AnimatedSprite2D = hud.get_node("MenuPanel/m_WsMax")
	assert(glow.material == null, "官方无双图片仍叠加旧裁剪材质")
	glow.show()
	glow.play()
	ws_bar.value = 1.0
	await _capture(viewport, "skills_layout_hud_full")
	print("SKILL LAYOUT PASS: 造一技能页/45px图标、造三五入口、无双黑底板与空/半满/满值渲染通过。")
	quit()


## 等待实际绘制完成后保存画面，避免读到上一帧。
func _capture(viewport: SubViewport, name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute("res://.godot/validation")
	assert(viewport.get_texture().get_image().save_png("res://.godot/validation/%s.png" % name) == OK)
