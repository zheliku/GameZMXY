extends SceneTree
## 运行可选触发器的真实物理回归，不改正式场景或源表。
## Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/validate_level_triggers.gd

func _init() -> void:
	var validator = load("res://EditorScripts/LevelTriggerValidation.cs").new()
	root.add_child.call_deferred(validator)
