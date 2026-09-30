extends SceneTree
## 生成悟空的动画状态机资源 Entitys/Animations/wukong_animation_tree.tres。
##
## 真相源（2026-09-30 人类裁决）：产物已存在时**以编辑器保存的版本为准**，本脚本默认只做
## 动画库校验、不覆盖；首次生成 / 文件被删 / 显式 `-- --force` 时才重建（--force 会冲掉编辑器里
## 对状态机的修改，用前先提交）。下方边表/谓词是初版设计记录，编辑器改图后以 .tres 为准。
##
## 结构（2026-09-28 定稿：主图分组 + 子机收纳动画 + 属性表达式）：
##
##   Root（主图，一眼可读）
##     ├─ Ground（子机）: Idle（子机: idle1 / idle2）/ walk / run
##     ├─ Air   （子机）: jump / jump_2 / fall
##     ├─ Attack（子机）: attack_1..N（普攻连段收在组内）
##     ├─ Hurt
##     └─ Death
##
##   * C#（HeroEntity）只维护**角色事实**，全部单一事实源：MoveInput / Running / JumpCount /
##     AttackSegment / Hurt / Emoting（[Export] 字段）；Dead（ActorEntity，唯一置位点在
##     ReceiveHit 扣血扣到 0）；Rising / Airborne（[Export] 计算属性：getter 实时算、
##     setter 为空——只读属性不可导出（GD0103），不导出对引擎又不可见）；
##   * **主图只决定"在哪个组"**：Ground ⇄ Air、Ground/Air → Attack →（收招）Ground/Air、
##     → Hurt → Ground、→ Death；组内动画流转全部收在子机（走跑切换、跳→落、连段推进）；
##     出招时序（起手装填/收招推进）由攻击动画的方法轨道回调 C#，判定盒几何在动画值轨道上。
##   * **进组 = 从 Start 选一个状态**：子机用 ROOT 类型——每次进组 seek 到第 0 帧时
##     **重启到 Start**（NESTED 类型会恢复上次内部状态，那样组内边必须两两相连才能自纠）；
##     每个状态一条 `Start → 状态` 边，条件 = 该状态的组内区分项（互斥且完备）；
##   * **组内边只写真实会发生的转移**（不是完全图）：既有连段只 +1 推进，就不要
##     attack_1→attack_3 的直连边。各组的边表与"为什么没有某条边"见 GROUND_EDGES /
##     AIR_EDGES / _attack_edges() 注释（改 C# 事实逻辑时要同步检查）；
##   * **组内可以再嵌套**（Ground → Idle）：同族姿势收进子机（idle1/idle2），
##     每层子机都按"进组 = 从 Start 选路"工作，层级不改变时序语义；
##   * **组谓词只写一次**（`P_*` 常量，含 `not Dead` 的优先级链，互斥且完备）：主图边 = 目标组谓词，
##     组内边 = 组内区分项（走/跑、跳/二段/落、段序号）——组谓词不成立时主图同帧切出本组，
##     组内选择不出现在画面上（子机转移输出滞后一帧、而那一刻已不在本组）；
##   * **每条边 = 目标状态的互斥条件**（advance_expression 只读角色属性），不依赖 priority、
##     不怕同帧多条件为真、实体池复用后也能一步归位。
##
## 分组代价（4.7.2 源码 + 实测，改状态机前先读）：
##   * 主图转移同帧生效；子机**内部**转移也同帧生效（子机被 blend 时评估内部边）；
##   * 进组那一帧子机在同一 process 内完成"启动 + 选路"（teleport/链式转移在可见混合之前，
##     中间混合权重为 0、不产生可见输出）——可见延迟与主图直切相同，不存在"慢一帧"；
##   * 进组的边必须 reset=true（AnimationNodeStateMachineTransition 默认值）：
##     子机（ROOT 类型）靠"seek 到第 0 帧"重启到 Start 完成进组选路；缺 reset 子机停在空 current、永不播放。
##
## 引擎语义（4.7.2 源码 + 实测）：
##   1. 表达式边必须 advance_mode=AUTO；表达式在状态内**持续**评估，命中即转移；
##   2. 同帧多条边为真时取 priority 最小者、同值取后加入者——本文件条件互斥，不依赖它；
##   3. 表达式读 C# 成员只有一条路：[Export] 成员——字段（直接事实/事件事实）或
##      计算属性（派生事实，如 Airborne/Rising，空 setter 见 HeroEntity）；
##      基对象由 ActorEntity.OnInit 指向实体节点；引擎自带数据用引擎名（如 is_on_floor()）。
##
## 运行（校验 / 缺失时生成）：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/build_wukong_anim_tree.gd
## 强制重建：同上命令末尾加 `-- --force`

const ANIM_LIB := "res://TheGame/Entitys/Animations/wukong_anim_library.tres"
const OUT := "res://TheGame/Entitys/Animations/wukong_animation_tree.tres"

## 资源 uid：WukongEntity.tscn 的 ext_resource 按 uid 引用本资源，必须稳定。
## headless 下 ResourceSaver 不会给新保存的资源写 uid，所以保存后由生成器补回文件头。
const OUT_UID := "uid://d0mcjylp207r7"

# ---- 悟空：动画名（角色专属映射；新英雄照抄一份、改这里的名字即可）----

const A_IDLE := "idle1"
const A_EMOTE := "idle2"      # 待机小动作（憨笑）
const A_WALK := "walk"
const A_RUN := "run"
const A_JUMP := "jump"
const A_JUMP2 := "jump_2"
const A_FALL := "fall"
const A_HURT := "hurt"
const A_DEATH := "death"
const A_ATTACK_FMT := "attack_%d"   # 普攻第 i 段（1 起）；段序号 i-1 ↔ AttackSegment

# ---- 组谓词：主图边的守卫（优先级链：死亡 > 普攻 > 受击 > 空中 > 地面）----
# 互斥必须写全，不依赖 priority/边顺序（本文件不依赖二者）；公共前缀抽成 P_ALIVE 一次。
# 派生事实 Rising/Airborne 是 HeroEntity 上的 [Export] 计算属性（getter 实时算，setter 为空）：
#   Rising   = velocity.y < 0
#   Airborne = not is_on_floor() or (JumpCount > 0 and Rising)   # 含"起跳那一帧还没离地"

const P_ALIVE := "not Dead and AttackSegment < 0"
const P_DEATH := "Dead"
const P_ATTACK := "not Dead and AttackSegment >= 0"
const P_HURT := P_ALIVE + " and Hurt"
const P_AIR := P_ALIVE + " and not Hurt and Airborne"
const P_GROUND := P_ALIVE + " and not Hurt and not Airborne"

# ---- Idle 子机（Ground 内的两级嵌套）：待机与憨笑是一对同族姿势，收进子机后
# Ground 图只剩 Idle/走/跑 三个节点。进 Idle 同样按 Start 选路（Emoting 区分）。----

const IDLE_MACHINE := "Idle"

const IDLE_STATES := [
	{"anim": A_IDLE, "guard": "not Emoting"},
	{"anim": A_EMOTE, "guard": "Emoting"},
]

const IDLE_EDGES := [
	[A_IDLE, A_EMOTE], [A_EMOTE, A_IDLE],
]

const GROUND_STATES := [
	{"anim": IDLE_MACHINE, "guard": "MoveInput == 0"},
	{"anim": A_WALK, "guard": "MoveInput != 0 and not Running"},
	{"anim": A_RUN, "guard": "MoveInput != 0 and Running"},
]

const AIR_STATES := [
	{"anim": A_JUMP, "guard": "Rising and JumpCount < 2"},
	{"anim": A_JUMP2, "guard": "Rising and JumpCount >= 2"},
	{"anim": A_FALL, "guard": "not Rising"},
]

# ---- 组内边：[from, to]，条件取 to 的 guard（组内区分项）----
# 只写"在组里时真实会发生"的转移，不做完全图。判断依据（改 C# 事实逻辑时同步检查）：
#   Ground：Idle ⇄ 走/跑（起步、停止、双击跑）；run→walk 不存在——
#           Running 只在 MoveInput==0 时回落（ReadInput），且此时目标是 Idle。
#   Idle  ：idle1 ⇄ idle2（憨笑开始/结束）。
#   Air   ：JumpCount 只增不减，所以 jump_2→jump 不存在；落→跳有两条
#           （走空摔下再起跳 = fall→jump，下落中二段跳 = fall→jump_2）。
#   Attack：段只 +1 推进（OnAttackEnd 的 AttackSegment++）；收招即离组，
#           重新进组的选段由 Start 边完成，所以组内只有 1→2→3→…链。

const GROUND_EDGES := [
	[IDLE_MACHINE, A_WALK], [IDLE_MACHINE, A_RUN],
	[A_WALK, IDLE_MACHINE], [A_WALK, A_RUN],
	[A_RUN, IDLE_MACHINE],
]

const AIR_EDGES := [
	[A_JUMP, A_JUMP2], [A_JUMP, A_FALL], [A_JUMP2, A_FALL],
	[A_FALL, A_JUMP], [A_FALL, A_JUMP2],
]

# ---- 主图边：[from, to, guard]。guard = to 组的组谓词（互斥，所以任一时刻最多一条成立）----

const ROOT_EDGES := [
	# 开局落位
	["Start", "Ground", P_GROUND],
	["Start", "Air", P_AIR],
	# 地面 ⇄ 空中
	["Ground", "Air", P_AIR],
	["Air", "Ground", P_GROUND],
	# 普攻：地面/空中都能起手；收招按当前在哪回到对应组
	["Ground", "Attack", P_ATTACK],
	["Air", "Attack", P_ATTACK],
	["Attack", "Ground", P_GROUND],
	["Attack", "Air", P_AIR],
	# 受击（出招期间不打断由谓词表达：P_HURT 要求 AttackSegment < 0）
	["Ground", "Hurt", P_HURT],
	["Air", "Hurt", P_HURT],
	["Hurt", "Ground", P_GROUND],
	["Hurt", "Air", P_AIR],
	# 死亡：任意组可进；回收复用（OnShow 复位后 Dead=false）由 Death → Ground 拉回
	["Ground", "Death", P_DEATH],
	["Air", "Death", P_DEATH],
	["Attack", "Death", P_DEATH],
	["Hurt", "Death", P_DEATH],
	["Death", "Ground", P_GROUND],
]

# ---- 节点布局（仅编辑器观感）----

const ROOT_LAYOUT := {
	"Ground": Vector2(0, 160),
	"Air": Vector2(280, 0),
	"Attack": Vector2(560, 160),
	"Hurt": Vector2(0, 340),
	"Death": Vector2(280, 340),
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

	# 编辑器为真相源：产物已存在且未 --force 时只校验不覆盖
	if FileAccess.file_exists(OUT) and not OS.get_cmdline_user_args().has("--force"):
		print("skip %s（已存在，以编辑器版本为准；动画库校验通过。重建用 -- --force）" % OUT)
		quit(0)
		return

	var err := ResourceSaver.save(_build_root(), OUT)
	if err != OK:
		push_error("保存失败：%s" % err)
		quit(1)
		return

	_restore_uid()
	print("wrote %s（主图 %d 状态，Ground %d / Air %d / Attack %d 段）"
		% [OUT, ROOT_LAYOUT.size() + 1, GROUND_STATES.size(), AIR_STATES.size(), _attack_count])
	quit(0)


func _validate(lib: AnimationLibrary) -> bool:
	var ok := true
	var anims := lib.get_animation_list()

	for anim in [A_IDLE, A_EMOTE, A_WALK, A_RUN, A_JUMP, A_JUMP2, A_FALL, A_HURT, A_DEATH]:
		if not anims.has(anim):
			push_error("悟空动画库缺少动画：%s" % anim)
			ok = false

	_attack_count = 0
	for anim in anims:
		if anim.begins_with("attack_"):
			_attack_count = maxi(_attack_count, int(anim.substr(7)))
	if _attack_count == 0:
		push_error("悟空动画库里没有 attack_<i> 普攻动画")
		ok = false
	for i in range(1, _attack_count + 1):
		if not anims.has(A_ATTACK_FMT % i):
			push_error("普攻段动画不连续：缺少 %s" % (A_ATTACK_FMT % i))
			ok = false
		else:
			# 出招链路完整性：攻击动画缺生命周期方法轨道，C# 侧就会"装填不了包 / 永远停在攻击态"
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

	# 判定盒轨道完备性（"reset 轨道"的等价保证）：先收集库里被任一动画写过的判定盒值轨道，
	# 再要求每个动画都带齐——切到任何动画首帧都写回安全值，不残留上一个动画的状态。
	# 未被任何动画写过的属性不在此列（恒为场景值、无切换残留）；
	# 攻击动画的几何键比判定窗早一帧（生成器保证），判定永远先见到本招几何。
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


## 攻击组状态表：按动画库实际段数展开（guard 只写组内区分项：段序号）。
func _attack_states() -> Array:
	var out := []
	for i in range(1, _attack_count + 1):
		out.append({
			"anim": A_ATTACK_FMT % i,
			"guard": "AttackSegment == %d" % (i - 1),
		})
	return out


## 攻击组内边：段只 +1 推进，所以是 1→2→3→…链（收招离组，重进由 Start 选段）。
func _attack_edges() -> Array:
	var out := []
	for i in range(1, _attack_count):
		out.append([A_ATTACK_FMT % i, A_ATTACK_FMT % (i + 1)])
	return out


func _tr(expr: String) -> AnimationNodeStateMachineTransition:
	var t := AnimationNodeStateMachineTransition.new()
	t.switch_mode = AnimationNodeStateMachineTransition.SWITCH_MODE_IMMEDIATE
	t.advance_mode = AnimationNodeStateMachineTransition.ADVANCE_MODE_AUTO
	t.xfade_time = 0.0   # 旧项目无淡入淡出：帧切换即切
	t.priority = 0       # 条件互斥，priority 不参与决策
	t.reset = true       # 子机靠 reset 进组启动；普通状态切回也从第 0 帧开始
	t.advance_expression = expr
	return t


func _anim(anim_name: String) -> AnimationNodeAnimation:
	var n := AnimationNodeAnimation.new()
	n.animation = anim_name
	return n


func _build_root() -> AnimationNodeStateMachine:
	var root := AnimationNodeStateMachine.new()
	root.state_machine_type = AnimationNodeStateMachine.STATE_MACHINE_TYPE_ROOT

	var idle := _build_group(IDLE_STATES, IDLE_EDGES)
	root.add_node("Ground", _build_group(GROUND_STATES, GROUND_EDGES, {IDLE_MACHINE: idle}), ROOT_LAYOUT["Ground"])
	root.add_node("Air", _build_group(AIR_STATES, AIR_EDGES), ROOT_LAYOUT["Air"])
	root.add_node("Attack", _build_group(_attack_states(), _attack_edges()), ROOT_LAYOUT["Attack"])
	root.add_node("Hurt", _anim(A_HURT), ROOT_LAYOUT["Hurt"])
	root.add_node("Death", _anim(A_DEATH), ROOT_LAYOUT["Death"])

	for edge in ROOT_EDGES:
		root.add_transition(edge[0], edge[1], _tr(edge[2]))

	return root


## 子机：每次进组重启到 Start，按 guard 选一个状态进组；组内只连真实转移。
## 每条边条件 = 目标状态的**组内区分项**（互斥，最多一条成立）。
## sub_machines：状态名 → 子机（状态本身是子状态机，如 Ground 里的 Idle）。
func _build_group(states: Array, edges: Array, sub_machines: Dictionary = {}) -> AnimationNodeStateMachine:
	var machine := AnimationNodeStateMachine.new()
	# ROOT 类型：seek 到第 0 帧（进组）时重启到 Start。NESTED 会恢复上次内部状态，
	# 那种语义下组内边必须两两直连才能自纠——正是要被这份生成器淘汰的东西。
	machine.state_machine_type = AnimationNodeStateMachine.STATE_MACHINE_TYPE_ROOT

	var guards := {}
	for i in states.size():
		var anim: String = states[i]["anim"]
		guards[anim] = states[i]["guard"]
		if sub_machines.has(anim):
			machine.add_node(anim, sub_machines[anim], Vector2(i * 220, 0))
		else:
			machine.add_node(anim, _anim(anim), Vector2(i * 220, 0))

	# 进组选路：Start → 每个状态一条边（条件互斥且完备，恰好命中一个）。
	for i in states.size():
		var anim: String = states[i]["anim"]
		machine.add_transition("Start", anim, _tr(guards[anim]))

	# 组内转移：条件取目标状态的 guard。
	for edge in edges:
		machine.add_transition(edge[0], edge[1], _tr(guards[edge[1]]))

	return machine


## 把 OUT_UID 补/改写进 .tres 文件头（见 OUT_UID 注释）。
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
