extends SceneTree
## 校验角色动画库（只校验、不写产物；退出码非 0 = 失败）。
##
## 动画契约：
##   1. 必需动画齐全，attack_<i> 编号连续；
##   2. 方法轨道只调用 AnimEvent 后缀的具名入口，目标节点及方法必须存在；
##      RESET 不含方法键；声音和动作时点归原生动画，分支及安全清理归 GGF 状态；
##   3. 身体状态要计时的动画（attack_*、hurt、death）不得循环
##      ——时长 = 长度是隐含契约，循环动画语义不符；
##   4. 值轨道完备：库里**任一**动画写过的值轨道路径，**每个**动画（含 RESET）都必须带
##      ——AnimationPlayer 直驱不回卷轨道，缺轨的属性会残留上一个动画写入的值。
##
## 运行：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/validate_anim_libraries.gd

## 角色差异表：新角色加一行必需动画。
const CHARACTERS := [
	{
		"lib": "res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres",
		"name": "悟空",
		"scene": "res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn",
		"required": ["RESET", "idle1", "walk", "run", "jump", "jump_2", "fall", "hurt", "death"],
	},
	{
		"lib": "res://TheGame/Entitys/Animations/Enemies/Monsters/huaguoshan_monkey_anim_library.tres",
		"name": "花果山猴子",
		"scene": "res://TheGame/Entitys/Enemies/Monsters/huaguoshan_monkey_entity.tscn",
		"required": ["idle", "run", "hurt", "death"],
	},
	{
		"lib": "res://TheGame/Entitys/Animations/Enemies/Monsters/demon_monkey_anim_library.tres",
		"name": "妖猴",
		"scene": "res://TheGame/Entitys/Enemies/Monsters/demon_monkey_entity.tscn",
		"required": ["idle", "run", "hurt", "death"],
	},
	{
		"lib": "res://TheGame/Entitys/Animations/Enemies/Monsters/gorilla_anim_library.tres",
		"name": "大猩猩",
		"scene": "res://TheGame/Entitys/Enemies/Monsters/gorilla_entity.tscn",
		"required": ["idle", "run", "hurt", "death"],
	},
	{
		"lib": "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/macaque_king_library.tres",
		"name": "猕猴王",
		"scene": "res://TheGame/Entitys/Enemies/Bosses/zm1_02_macaque_king/macaque_king_entity.tscn",
		"required": ["idle", "run", "hurt", "death"],
	},
	{
		"lib": "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_library.tres",
		"name": "禺狨王",
		"scene": "res://TheGame/Entitys/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_entity.tscn",
		"required": ["idle", "run", "hurt", "death"],
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

	for anim in anims:
		var a := lib.get_animation(String(anim))
		var anim_name := String(anim)

		# 要计时的动画必须非循环
		if (anim_name.begins_with("attack_") or anim_name in ["hurt", "death"]) and a.loop_mode != Animation.LOOP_NONE:
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
	# 检查实例化后的真实 C# 方法，包括技能库与独立特效；不进入树、不启动玩法。
	var scene := load(String(ch.scene)) as PackedScene
	var actor := scene.instantiate()
	if not _validate_callbacks(actor):
		ok = false
	actor.free()
	return ok


func _validate_callbacks(node: Node) -> bool:
	var ok := true
	if node is AnimationPlayer:
		var animation_root := node.get_node(node.root_node)
		for name in node.get_animation_list():
			var animation: Animation = node.get_animation(name)
			for track in animation.get_track_count():
				if animation.track_get_type(track) != Animation.TYPE_METHOD:
					continue
				var target := animation_root.get_node_or_null(animation.track_get_path(track))
				for key in animation.track_get_key_count(track):
					var method: StringName = animation.method_track_get_name(track, key)
					if String(name).get_file() == "RESET" or not String(method).ends_with("AnimEvent") or target == null or not target.has_method(method):
						push_error("动画方法未绑定或命名不符：%s/%s/%s" % [node.get_parent().name, name, method])
						ok = false
					for argument in animation.method_track_get_params(track, key):
						if argument is PackedScene:
							var effect: Node = argument.instantiate()
							if not _validate_callbacks(effect):
								ok = false
							effect.free()
	for child in node.get_children():
		if not _validate_callbacks(child):
			ok = false
	return ok
