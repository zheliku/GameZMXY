extends SceneTree
## 生成花果山猴子的动画状态机资源 Entitys/huaguoshan_monkey_animation_tree.tres。
## 机制与 build_wukong_anim_tree.gd 完全一致（表达式边 / ROOT 子机 / 组谓词互斥完备 / reset 进组），
## 此处只写怪物的差异表；引擎语义与"为什么这样搭"见悟空生成器头注释。
##
## 结构（M4：沙包；M5 的 AI 只写事实，不改本图）：
##
##   Root
##     ├─ Ground（子机）: idle / run        —— MoveInput 区分
##     ├─ Attack（子机）: attack_1..N       —— AttackSegment 区分，段只 +1 推进
##     ├─ Hurt
##     └─ Death
##
##   * C#（MonsterEntity）维护事实：MoveInput / AttackSegment / Hurt / Dead；
##   * 优先级链：死亡 > 受击 > 攻击 > 地面。**与英雄不同：怪物受击打断出招**（旧 BaseMonster state_hurt
##     直接切 hurt）——C# 受击时已把 AttackSegment 置 -1，谓词里 Hurt 先于攻击是双保险；
##   * 怪物没有空中组：M4/M5 怪物都贴地（空中怪随飞行怪一起加组）。
##
## 运行：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/build_huaguoshan_monkey_anim_tree.gd

const ANIM_LIB := "res://TheGame/Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_anim_library.tres"
const OUT := "res://TheGame/Entitys/huaguoshan_monkey_animation_tree.tres"

## 资源 uid：HuaguoshanMonkeyEntity.tscn 按 uid 引用本资源，必须稳定（headless 保存不写 uid，保存后补回）。
const OUT_UID := "uid://c4seyh1vbcds4"

const A_IDLE := "idle"
const A_RUN := "run"
const A_HURT := "hurt"
const A_DEATH := "death"
const A_ATTACK_FMT := "attack_%d"

# ---- 组谓词（互斥完备，含优先级链）----
const P_DEATH := "Dead"
const P_HURT := "not Dead and Hurt"
const P_ATTACK := "not Dead and not Hurt and AttackSegment >= 0"
const P_GROUND := "not Dead and not Hurt and AttackSegment < 0"

const GROUND_STATES := [
	{"anim": A_IDLE, "guard": "MoveInput == 0"},
	{"anim": A_RUN, "guard": "MoveInput != 0"},
]

const GROUND_EDGES := [
	[A_IDLE, A_RUN], [A_RUN, A_IDLE],
]

const ROOT_EDGES := [
	["Start", "Ground", P_GROUND],
	["Ground", "Attack", P_ATTACK],
	["Attack", "Ground", P_GROUND],
	# 受击：地面/出招中都可进（怪物受击打断出招）
	["Ground", "Hurt", P_HURT],
	["Attack", "Hurt", P_HURT],
	["Hurt", "Ground", P_GROUND],
	# 连续受击：硬直中再被打，C# 重置硬直计时——Hurt 状态不离开，动画不重播。
	# 旧项目每次受击重播 hurt；要重播需要"受击次数"之类的事实，M4 不需要，保持简单。
	["Ground", "Death", P_DEATH],
	["Attack", "Death", P_DEATH],
	["Hurt", "Death", P_DEATH],
	# 回收复用：OnShow 复位 Dead=false 后拉回地面
	["Death", "Ground", P_GROUND],
]

const ROOT_LAYOUT := {
	"Ground": Vector2(0, 160),
	"Attack": Vector2(320, 160),
	"Hurt": Vector2(0, 340),
	"Death": Vector2(320, 340),
}

var _attack_count := 0


func _init() -> void:
	var lib := ResourceLoader.load(ANIM_LIB) as AnimationLibrary
	if lib == null:
		push_error("加载动画库失败：%s" % ANIM_LIB)
		quit(1)
		return

	if not _validate(lib):
		quit(1)
		return

	var err := ResourceSaver.save(_build_root(), OUT)
	if err != OK:
		push_error("保存失败：%s" % err)
		quit(1)
		return

	_restore_uid()
	print("wrote %s（主图 %d 状态，Ground %d / Attack %d 段）"
		% [OUT, ROOT_LAYOUT.size() + 1, GROUND_STATES.size(), _attack_count])
	quit(0)


func _validate(lib: AnimationLibrary) -> bool:
	var ok := true
	var anims := lib.get_animation_list()
	for anim in [A_IDLE, A_RUN, A_HURT, A_DEATH]:
		if not anims.has(anim):
			push_error("猴子动画库缺少动画：%s" % anim)
			ok = false

	_attack_count = 0
	for anim in anims:
		if anim.begins_with("attack_"):
			_attack_count = maxi(_attack_count, int(anim.substr(7)))
	if _attack_count == 0:
		push_error("猴子动画库里没有 attack_<i> 攻击动画")
		ok = false
	for i in range(1, _attack_count + 1):
		if not anims.has(A_ATTACK_FMT % i):
			push_error("攻击段动画不连续：缺少 %s" % (A_ATTACK_FMT % i))
			ok = false
		else:
			# 出招链路完整性：同 build_wukong_anim_tree.gd 注释
			var attack := lib.get_animation(A_ATTACK_FMT % i)
			var methods := {}
			for t in attack.get_track_count():
				if attack.track_get_type(t) == Animation.TYPE_METHOD:
					for k in attack.track_get_key_count(t):
						methods[attack.method_track_get_name(t, k)] = true
			for required in ["OnAttackBegin", "OnAttackEnd"]:
				if not methods.has(required):
					push_error("%s 缺少 %s() 方法轨道（出招链路断裂）" % [A_ATTACK_FMT % i, required])
					ok = false

	# 判定盒轨道完备性（"reset 轨道"的等价保证）：同 build_wukong_anim_tree.gd 注释
	# （并集规则：被任一动画写过的轨道，全体带齐）
	var hitbox_paths := {}
	for anim_name in anims:
		var anim := lib.get_animation(anim_name)
		for t in anim.get_track_count():
			if anim.track_get_type(t) == Animation.TYPE_VALUE:
				var value_path := anim.track_get_path(t)
				if String(value_path).contains("m_HitBox"):
					hitbox_paths[value_path] = true
	for anim_name in anims:
		var anim := lib.get_animation(anim_name)
		var value_paths := {}
		for t in anim.get_track_count():
			if anim.track_get_type(t) == Animation.TYPE_VALUE:
				value_paths[anim.track_get_path(t)] = true
		for geometry_path in hitbox_paths:
			if not value_paths.has(geometry_path):
				push_error("%s 缺少判定盒值轨道 %s（轨道完备性）" % [anim_name, geometry_path])
				ok = false
	return ok


func _attack_states() -> Array:
	var out := []
	for i in range(1, _attack_count + 1):
		out.append({"anim": A_ATTACK_FMT % i, "guard": "AttackSegment == %d" % (i - 1)})
	return out


func _attack_edges() -> Array:
	var out := []
	for i in range(1, _attack_count):
		out.append([A_ATTACK_FMT % i, A_ATTACK_FMT % (i + 1)])
	return out


func _tr(expr: String) -> AnimationNodeStateMachineTransition:
	var t := AnimationNodeStateMachineTransition.new()
	t.switch_mode = AnimationNodeStateMachineTransition.SWITCH_MODE_IMMEDIATE
	t.advance_mode = AnimationNodeStateMachineTransition.ADVANCE_MODE_AUTO
	t.xfade_time = 0.0
	t.priority = 0
	t.reset = true
	t.advance_expression = expr
	return t


func _anim(anim_name: String) -> AnimationNodeAnimation:
	var n := AnimationNodeAnimation.new()
	n.animation = anim_name
	return n


func _build_root() -> AnimationNodeStateMachine:
	var root := AnimationNodeStateMachine.new()
	root.state_machine_type = AnimationNodeStateMachine.STATE_MACHINE_TYPE_ROOT
	root.add_node("Ground", _build_group(GROUND_STATES, GROUND_EDGES), ROOT_LAYOUT["Ground"])
	root.add_node("Attack", _build_group(_attack_states(), _attack_edges()), ROOT_LAYOUT["Attack"])
	root.add_node("Hurt", _anim(A_HURT), ROOT_LAYOUT["Hurt"])
	root.add_node("Death", _anim(A_DEATH), ROOT_LAYOUT["Death"])
	for edge in ROOT_EDGES:
		root.add_transition(edge[0], edge[1], _tr(edge[2]))
	return root


func _build_group(states: Array, edges: Array) -> AnimationNodeStateMachine:
	var machine := AnimationNodeStateMachine.new()
	machine.state_machine_type = AnimationNodeStateMachine.STATE_MACHINE_TYPE_ROOT
	var guards := {}
	for i in states.size():
		var anim: String = states[i]["anim"]
		guards[anim] = states[i]["guard"]
		machine.add_node(anim, _anim(anim), Vector2(i * 220, 0))
	for i in states.size():
		var anim: String = states[i]["anim"]
		machine.add_transition("Start", anim, _tr(guards[anim]))
	for edge in edges:
		machine.add_transition(edge[0], edge[1], _tr(guards[edge[1]]))
	return machine


func _restore_uid() -> void:
	var text := FileAccess.get_file_as_string(OUT)
	if text.is_empty():
		push_error("补写 uid 失败：读不到 %s" % OUT)
		return
	var lines := text.split("\n")
	if lines.is_empty() or not lines[0].begins_with("[gd_resource"):
		push_error("补写 uid 失败：%s 文件头异常" % OUT)
		return
	var header := lines[0]
	var re := RegEx.create_from_string(' uid="uid://[^"]+"')
	if re.search(header) != null:
		header = re.sub(header, ' uid="%s"' % OUT_UID)
	else:
		header = header.left(-1) + ' uid="%s"]' % OUT_UID
	if header == lines[0]:
		return
	lines[0] = header
	var f := FileAccess.open(OUT, FileAccess.WRITE)
	if f == null:
		push_error("补写 uid 失败：无法写入 %s" % OUT)
		return
	f.store_string("\n".join(lines))
