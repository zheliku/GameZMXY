extends SceneTree
## 核对第三段原生滤镜特效、固定原点与四种外观组合，输出左右朝向的实际渲染。

func _initialize() -> void:
	create_timer(45).timeout.connect(func(): quit(1))
	_run.call_deferred()

## 不推进玩法，只通过原生动画取帧；参考图集来自官方独立导出流程。
func _run() -> void:
	assert(DisplayServer.get_name() != "headless")
	var args := OS.get_cmdline_user_args()
	assert(args.size() == 1, "Pass the independently generated effect atlas")
	var expected := Image.load_from_file(args[0])
	# 对照图先应用正式贴图相同的 Godot 导入设置，不把透明边缘 RGB 修补误判为素材变化。
	expected.fix_alpha_edges()
	root.size = Vector2i(800, 500)
	var background := ColorRect.new()
	background.size = Vector2(800, 500)
	background.color = Color(0.25, 0.25, 0.25)
	root.add_child(background)
	var hero: CharacterBody2D = load("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").instantiate()
	hero.process_mode = Node.PROCESS_MODE_DISABLED
	hero.position = Vector2(400, 240)
	root.add_child(hero)
	var player: AnimationPlayer = hero.get_node("m_AnimPlayer")
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var animation := player.get_animation("attack_3")
	for track in animation.get_track_count():
		if animation.track_get_type(track) == Animation.TYPE_METHOD:
			animation.track_set_enabled(track, false)
	var body: Sprite2D = hero.get_node("m_Body")
	var weapon: Sprite2D = hero.get_node("m_Weapon")
	var effect: AnimatedSprite2D = hero.get_node("m_EffectRoot/m_Effect")
	var appearance: Resource = load("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres")
	var output := "res://.godot/validation/hud_entry_attack_crop"
	DirAccess.make_dir_recursive_absolute(output)
	var samples := 0
	for armor in [0, 1]:
		body.texture = appearance.get("m_BaseBody") if armor == 0 else appearance.get("m_ArmorTextures")[1]
		for armed in [0, 1]:
			weapon.texture = appearance.get("m_BaseWeapon") if armed == 0 else appearance.get("m_WeaponTextures")[2]
			for facing in [-1, 1]:
				player.play("RESET")
				player.advance(0)
				hero.call("SetFacing", facing)
				player.play("attack_3")
				for frame in range(11):
					player.seek((frame + 0.1) / 24.0, true)
					assert(hero.position == Vector2(400, 240), "Attack changed the actor origin")
					if body.frame != weapon.frame or effect.frame != frame:
						push_error("Pose mismatch: armor=%d weapon=%d facing=%d frame=%d actual=%d/%d/%d" % [armor, armed, facing, frame, body.frame, weapon.frame, effect.frame])
						quit(1)
						return
					var texture := effect.sprite_frames.get_frame_texture("attack_3", frame)
					assert((texture != null) == (frame < 9), "Source blank frames changed")
					if texture != null and armor == 0 and armed == 0 and facing == -1:
						var atlas := texture as AtlasTexture
						var actual := texture.get_image()
						var reference := expected.get_region(Rect2i(atlas.region))
						for y in actual.get_height():
							for x in actual.get_width():
								var a := actual.get_pixel(x, y)
								var b := reference.get_pixel(x, y)
								assert(absf(a.a - b.a) < 0.005, "Native effect alpha changed")
								# Godot 修补透明边缘 RGB；按实际可见的预乘颜色比较。
								if (Vector3(a.r, a.g, a.b) * a.a).distance_to(Vector3(b.r, b.g, b.b) * b.a) >= 0.015:
									actual.save_png(output.path_join("mismatched_actual.png"))
									reference.save_png(output.path_join("mismatched_reference.png"))
									push_error("Native effect color changed: frame=%d point=(%d,%d), actual=%s expected=%s" % [frame, x, y, a, b])
									quit(1)
									return
					await process_frame
					await RenderingServer.frame_post_draw
					if frame == 8:
						root.get_texture().get_image().save_png(output.path_join("attack3_armor%d_weapon%d_facing%d.png" % [armor, armed, facing]))
					samples += 1
	print("ATTACK EFFECT PASS: ", samples, " rendered poses, 9 native filtered frames, 2 blank frames, both directions, four loadouts, stable actor origin.")
	hero.free()
	quit()
