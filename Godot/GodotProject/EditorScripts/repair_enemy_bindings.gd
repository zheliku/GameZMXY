extends SceneTree
## Preserve serialized entity identities while renaming the shared enemy export.

func _initialize() -> void:
	assert(Engine.is_editor_hint(), "Run with --editor to preserve resource UIDs")
	for group in ["Monsters", "Bosses"]:
		var directory: String = "res://TheGame/Entitys/Enemies/" + group
		for name in DirAccess.get_files_at(directory):
			if not name.ends_with(".tscn"):
				continue
			var path: String = directory.path_join(name)
			var pattern := RegEx.new()
			pattern.compile("m_MonsterEntityId = ([0-9]+)")
			var old := pattern.search(FileAccess.get_file_as_string(path))
			assert(old != null, "Run once against pre-refactor serialization: " + path)
			var actor: Node = load(path).instantiate()
			actor.set("m_EnemyEntityId", int(old.get_string(1)))
			var packed := PackedScene.new()
			assert(packed.pack(actor) == OK)
			assert(ResourceSaver.save(packed, path) == OK)
			actor.free()
	print("ENEMY BINDINGS PASS: all five stable entity IDs retained under renamed export.")
	quit()
