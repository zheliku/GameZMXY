extends SceneTree
## 校验角色动画库（只校验、不写产物；退出码非 0 = 失败）。
##
## 动画契约（2026-10-01 定稿，A 方案：动画纯数据、代码唯一时钟）：
##   1. 必需动画齐全，attack_<i> 编号连续；
##   2. **禁止一切方法轨道**——动画库只放表现数据（帧/特效/判定盒值轨道），从不调用代码；
##      动作时长由 C# OnInit 读动画长度（ActorEntity.GetAnimLength），音效由状态钩子触发；
##      将来"第 N 帧放子弹/脚步声"这类中途时点用 Godot 4.3+ 动画标记（Animation.GetMarkerTime，
##      代码读时间自己计时），也不是方法轨道；
##   3. 身体状态要计时的动画（attack_*、hurt、death、待机小动作）不得循环
##      ——时长 = 长度是隐含契约，循环动画语义不符；
##   4. 值轨道完备：库里**任一**动画写过的值轨道路径，**每个**动画（含 RESET）都必须带
##      ——AnimationPlayer 直驱不回卷轨道，缺轨的属性会残留上一个动画写入的值。
##
## 运行：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/validate_anim_libraries.gd

## 角色差异表：新角色加一行（必需动画、待机小动作动画名）
const CHARACTERS := [
	{
		"lib": "res://TheGame/Entitys/Animations/wukong_anim_library.tres",
		"name": "悟空",
		"required": ["idle1", "idle2", "walk", "run", "jump", "jump_2", "fall", "hurt", "death"],
		"emote": "idle2",
	},
	{
		"lib": "res://TheGame/Entitys/Animations/huaguoshan_monkey_anim_library.tres",
		"name": "花果山猴子",
		"required": ["idle", "run", "hurt", "death"],
		"emote": "",
	},
]


func _init() -> void:
	var failed := false
	for ch in CHARACTERS:
		var lib := ResourceLoader.load(String(ch.lib)) as AnimationLibrary
		if lib == null:
			push_error("加载动画库失败：%s" % String(ch.lib))
			failed = true
			continue
		if not _validate(ch, lib):
			failed = true
		print("checked %s（%s）" % [String(ch.name), String(ch.lib)])
	quit(1 if failed else 0)


func _validate(ch: Dictionary, lib: AnimationLibrary) -> bool:
	var ok := true
	var anims := lib.get_animation_list()

	# 1. 必需动画齐全
	for anim in ch.required:
		if not anims.has(anim):
			push_error("%s 动画库缺少动画：%s" % [String(ch.name), String(anim)])
			ok = false

	# 2. attack_<i> 连续
	var max_index := 0
	for anim in anims:
		if String(anim).begins_with("attack_"):
			max_index = maxi(max_index, int(String(anim).substr(7)))
	if max_index == 0:
		push_error("%s 动画库没有 attack_<i> 动画" % String(ch.name))
		ok = false
	for i in range(1, max_index + 1):
		if not anims.has("attack_%d" % i):
			push_error("%s 普攻动画不连续：缺少 attack_%d" % [String(ch.name), i])
			ok = false

	# 3-5. 逐动画检查
	var value_paths := {}
	for anim in anims:
		var a := lib.get_animation(String(anim))
		for t in a.get_track_count():
			if a.track_get_type(t) == Animation.TYPE_VALUE:
				value_paths[String(a.track_get_path(t))] = true

	# 身体状态要计时的动画：attack_*/hurt/death + 各角色 emote；必须非循环
	var wait_anims := ["^attack_\\d+$", "^hurt$", "^death$"]
	if String(ch.emote) != "":
		wait_anims.append("^" + String(ch.emote) + "$")

	for anim in anims:
		var a := lib.get_animation(String(anim))
		var anim_name := String(anim)

		# 禁止一切方法轨道（动画纯数据）
		for t in a.get_track_count():
			if a.track_get_type(t) == Animation.TYPE_METHOD:
				var methods := []
				for k in a.track_get_key_count(t):
					methods.append(String(a.method_track_get_name(t, k)))
				push_error("%s/%s 有方法轨道 %s（动画库只放表现数据：逻辑/时点/音效归代码，中途时点用动画标记）"
					% [String(ch.name), anim_name, ",".join(methods)])
				ok = false

		# 要计时的动画必须非循环
		for pattern in wait_anims:
			if anim_name.matchn(pattern) and a.loop_mode != Animation.LOOP_NONE:
				push_error("%s/%s 是循环动画：身体状态按「动画长度」计时，循环动画语义不符"
					% [String(ch.name), anim_name])
				ok = false

		# 值轨道完备（含 RESET）
		var own := {}
		for t in a.get_track_count():
			if a.track_get_type(t) == Animation.TYPE_VALUE:
				own[String(a.track_get_path(t))] = true
		for path in value_paths:
			if not own.has(path):
				push_error("%s/%s 缺少值轨道 %s（轨道完备性：直驱播放不回卷，会残留上一个动画的值）"
					% [String(ch.name), anim_name, path])
				ok = false
	return ok
