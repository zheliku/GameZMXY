extends SceneTree
## 检查少量基础形状的玩法覆盖：允许闪电间隙，云层和区域外不伤害，结束/中断清理。

const SCENES := "res://TheGame/Entitys/Enemies/Bosses/"
var hits := 0
var misses := 0

func _initialize() -> void:
	call_deferred("_validate")

func _validate() -> void:
	root.size = Vector2i(940, 590)
	debug_collisions_hint = OS.get_cmdline_user_args().has("--snapshots")
	for entry in [["zm1_02_macaque_king", "macaque_king"], ["zm1_03_tamarin_king", "tamarin_king"]]:
		var boss: CharacterBody2D = load(SCENES + entry[0] + "/" + entry[1] + "_entity.tscn").instantiate()
		root.add_child(boss)
		boss.position = Vector2(450,450)
		boss.set_physics_process(false)
		var player: AnimationPlayer = boss.get_node("m_AnimPlayer")
		player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		var area: Area2D = boss.get_node("m_HitBoxRoot/m_HitBox")
		for facing in [-1, 1]:
			boss.call("SetFacing", facing)
			var samples: Array
			if entry[1] == "tamarin_king":
				samples = [
					["attack_1",11,[Vector2(-110,-80)],[Vector2(-300,-80),Vector2(80,-80)]],
					["attack_2",38,[],[Vector2(-100,-240),Vector2.ZERO]],
					["attack_2",43,[Vector2(-160,-80),Vector2.ZERO,Vector2(100,-80)],[Vector2(-100,-240),Vector2(-350,-60),Vector2(350,-60)]],
					["attack_2",56,[],[Vector2.ZERO,Vector2(-100,-240)]],
					["attack_2",63,[Vector2(-180,-60),Vector2(0,-60),Vector2(240,-60)],[Vector2(-100,-240),Vector2(-350,-60),Vector2(350,-60)]],
					["attack_2",77,[],[Vector2.ZERO]]]
			else:
				samples = [
					["attack_2",2,[],[Vector2.ZERO]],
					["attack_2",8,[Vector2(10,20)],[Vector2(-100,20),Vector2(150,20)]],
					["attack_2",38,[Vector2(0,-50),Vector2(-100,-170),Vector2(100,-170)],[Vector2(-120,40),Vector2(120,40),Vector2(200,-170)]]]
			for sample in samples:
				player.play(sample[0])
				player.seek((sample[1]-1) / 24.0 + 0.00001,true)
				await physics_frame
				await process_frame
				for point in sample[2]:
					if not _expect(boss,area,point,true,entry[1],sample[1]):
						return
				for point in sample[3]:
					if not _expect(boss,area,point,false,entry[1],sample[1]):
						return
				if OS.get_cmdline_user_args().has("--snapshots") and facing == -1 and sample[1] in [11,38,43,63]:
					await RenderingServer.frame_post_draw
					var output := "res://.godot/validation/simple_collisions/%s_%d.png" % [entry[1],sample[1]]
					DirAccess.make_dir_recursive_absolute(output.get_base_dir())
					root.get_texture().get_image().save_png(output)
				# 任意攻击阶段被打断时，原生 RESET 必须同步关掉所有攻击形状。
				player.play("RESET")
				player.advance(0)
				for shape in area.get_children():
					if shape is CollisionShape2D and not shape.disabled:
						push_error("Attack shape survives interruption")
						quit(1)
						return
		boss.free()
	print("BASIC ATTACK AREAS PASS: hits=",hits,", misses=",misses,", both directions, cloud/outer-air exclusion, lightning gaps included, RESET cleanup.")
	quit()

## 使用真实物理查询，不把可见能量中的透明间隙当作缺陷。
func _expect(boss: Node2D, area: Area2D, point: Vector2, expected: bool, name: String, frame: int) -> bool:
	var query := PhysicsPointQueryParameters2D.new()
	query.position = area.global_transform * point
	query.collision_mask = 64
	query.collide_with_areas = true
	query.collide_with_bodies = false
	var actual := boss.get_world_2d().direct_space_state.intersect_point(query).any(func(hit): return hit.collider == area)
	if actual != expected:
		push_error("%s frame %d point %s: expected %s" % [name,frame,point,expected])
		quit(1)
		return false
	if expected:
		hits += 1
	else:
		misses += 1
	return true
