extends SceneTree
## Move explicitly listed game resources through the editor's FileSystem action.
## Usage: --headless --editor --script res://EditorScripts/move_game_resources.gd -- <moves.json>

func _initialize() -> void:
	call_deferred("_move_resources")

## Keep the native editor responsible for UID, dependencies and open-scene paths.
func _move_resources() -> void:
	if not Engine.is_editor_hint():
		push_error("Resource moves require the Godot editor.")
		quit(1)
		return
	var moves: Array = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	var filesystem := EditorInterface.get_resource_filesystem()
	var dock := EditorInterface.get_file_system_dock()
	if not await _wait_for_scan(filesystem):
		quit(1)
		return
	for entry in moves:
		var source: String = entry.source
		var folder: String = entry.folder
		var rename: bool = entry.has("name")
		var target := folder.path_join(entry.name if rename else source.get_file())
		if not source.begins_with("res://TheGame/") or not folder.begins_with("res://TheGame/") or ".." in folder or ".." in source:
			push_error("Resource move is outside TheGame: " + source)
			quit(1)
			return
		var is_directory := DirAccess.dir_exists_absolute(source)
		if (not FileAccess.file_exists(source) and not is_directory) or FileAccess.file_exists(target) or DirAccess.dir_exists_absolute(target):
			push_error("Move source is missing or destination is occupied: " + source)
			quit(1)
			return
		var uid := -1 if is_directory else ResourceLoader.get_resource_uid(source)
		DirAccess.make_dir_recursive_absolute(folder)
		filesystem.scan()
		if not await _wait_for_scan(filesystem):
			quit(1)
			return
		dock.navigate_to_path(source + "/" if is_directory else source)
		for frame in range(10):
			await process_frame
		var selected := false
		var selected_tree: Tree
		for tree in dock.find_children("*", "Tree", true, false):
			var item: TreeItem = tree.get_selected()
			if item != null and item.get_metadata(0) is String and String(item.get_metadata(0)).trim_suffix("/") == source:
				tree.grab_focus()
				tree.emit_signal("item_mouse_selected", Vector2.ZERO, MOUSE_BUTTON_RIGHT)
				selected_tree = tree
				selected = true
				break
		if not selected:
			push_error("Editor did not select the move source: " + source)
			quit(1)
			return
		var requested := false
		for menu in dock.find_children("*", "PopupMenu", true, false):
			for index in menu.item_count:
				if menu.get_item_text(index).begins_with("Rename" if rename else "Move/Duplicate To"):
					menu.emit_signal("id_pressed", menu.get_item_id(index))
					menu.hide()
					requested = true
					break
			if requested:
				break
		for frame in range(3):
			await process_frame
		var moved := false
		if rename:
			for edit in selected_tree.find_children("*", "LineEdit", true, false):
				if edit.is_visible_in_tree():
					edit.text = entry.name
					edit.emit_signal("text_submitted", entry.name)
					moved = true
					break
		else:
			for window in dock.find_children("*", "Window", true, false):
				if window.visible and window.has_signal("move_pressed"):
					window.emit_signal("move_pressed", folder)
					window.hide()
					moved = true
					break
		if not moved or not await _wait_for_scan(filesystem):
			push_error("Editor move action did not complete: " + source)
			quit(1)
			return
		var moved_directory := is_directory and not DirAccess.dir_exists_absolute(source) and DirAccess.dir_exists_absolute(target)
		var moved_file := not is_directory and not FileAccess.file_exists(source) and FileAccess.file_exists(target) and ResourceLoader.get_resource_uid(target) == uid
		if not moved_directory and not moved_file:
			push_error("Editor move did not preserve path and UID: " + source)
			quit(1)
			return
		print("MOVE PASS: %s -> %s (%s)" % [source, target, ResourceUID.id_to_text(uid)])
	quit()

## Wait for a native scan/import to finish before selecting another file.
func _wait_for_scan(filesystem: EditorFileSystem) -> bool:
	var deadline := Time.get_ticks_msec() + 30000
	for frame in range(3):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		if Time.get_ticks_msec() >= deadline:
			push_error("Editor resource scan timed out.")
			return false
		await process_frame
	return true
