extends SceneTree
## Pack native boss poses with one registration and deduplicate identical frames.

func _initialize() -> void:
	var path := OS.get_cmdline_user_args()[0]
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	for actor in manifest.actors:
		for layer in actor.layers:
			if not _pack_layer(actor.name, layer):
				quit(1)
				return
	var output := FileAccess.open(path.get_base_dir().path_join("rasterized.json"), FileAccess.WRITE)
	output.store_string(JSON.stringify(manifest, "\t"))
	quit()

## Preserve one logical origin per layer while packing only unique occupied poses.
func _pack_layer(actor_name: String, layer: Dictionary) -> bool:
	var images: Array[Image] = []
	var poses := {}
	var hashes := {}
	var bounds := Rect2i()
	var placements: Array[Vector2i] = []
	for action in layer.actions:
		var cells: Array[int] = []
		for frame in range(1, int(action.frames) + 1):
			var source: String = action.input.path_join("%d.png" % frame)
			var picture := Image.load_from_file(source)
			if picture == null:
				push_error("Boss PNG could not be read: " + source)
				return false
			var visible := picture.get_used_rect()
			var placement := Vector2i(floori(float(action.bounds[0]) + float(action.matrix[4])),
				floori(float(action.bounds[1]) + float(action.matrix[5]))) + visible.position
			if visible.has_area():
				var registered := Rect2i(placement, visible.size)
				bounds = registered if not bounds.has_area() else bounds.merge(registered)
				picture = picture.get_region(visible)
			else:
				picture = Image.create_empty(1, 1, false, Image.FORMAT_RGBA8)
			var hash := HashingContext.new()
			hash.start(HashingContext.HASH_SHA256)
			hash.update(picture.get_data())
			var digest := "%s/%s/%s" % [hash.finish().hex_encode(), picture.get_size(), placement]
			if not hashes.has(digest):
				hashes[digest] = images.size()
				images.append(picture)
				placements.append(placement)
			cells.append(hashes[digest])
		poses[action.name] = cells
	if not bounds.has_area():
		push_error("Empty boss export: " + actor_name)
		return false
	var columns := mini(8, images.size())
	var rows := ceili(float(images.size()) / columns)
	var atlas := Image.create_empty(bounds.size.x * columns, bounds.size.y * rows, false, Image.FORMAT_RGBA8)
	for cell in images.size():
		atlas.blit_rect(images[cell], Rect2i(Vector2i.ZERO, images[cell].get_size()),
			Vector2i(cell % columns, cell / columns) * bounds.size + placements[cell] - bounds.position)
	DirAccess.make_dir_recursive_absolute(layer.output.get_base_dir())
	if atlas.save_png(layer.output) != OK:
		push_error("Boss atlas could not be saved: " + layer.output)
		return false
	layer["cell_size"] = [bounds.size.x, bounds.size.y]
	var center := Vector2(bounds.position) + Vector2(bounds.size) * 0.5
	layer["sprite_offset"] = [center.x, center.y]
	layer["columns"] = columns
	layer["rows"] = rows
	layer["poses"] = poses
	print("BOSS ART: %s/%s, %d unique poses, cell %s, offset %s" % [actor_name, layer.name, images.size(), bounds.size, layer.sprite_offset])
	return true
