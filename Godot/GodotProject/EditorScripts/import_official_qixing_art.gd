extends SceneTree
## Register new 2-2 artwork as native external SpriteFrames, preserving one source origin.

const FOLDERS := {
	"cave_brute": "Monsters/Ground/cave_brute",
	"knife_demon": "Monsters/Ground/knife_demon",
	"knife_demon_blade": "Monsters/Ground/knife_demon",
	"lion_camel_king": "Bosses/zm1_05_qixing_cave",
	"lion_camel_poison_cloud": "Bosses/zm1_05_qixing_cave",
	"poison_head": "Heroes/Status",
	"poison_up": "Heroes/Status",
}

func _initialize() -> void:
	_run.call_deferred()

## Copy only the newly exported artwork; do not replace existing character resources.
func _run() -> void:
	var path := OS.get_cmdline_user_args()[0]
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	for actor in manifest.actors + manifest.effects:
		for layer in actor.layers:
			assert(_pack_layer(actor.name, layer), "Unable to pack native frames: " + actor.name)
		var folder: String = "res://TheGame/Sprites/Characters/Enemies/" + FOLDERS[actor.name]
		if actor.name in ["poison_head", "poison_up"]:
			folder = "res://TheGame/Sprites/Characters/Heroes/Status"
		DirAccess.make_dir_recursive_absolute(folder)
		var destination := folder.path_join(actor.name + ".png")
		var image := Image.load_from_file(actor.layers[0].output)
		assert(image != null)
		if FileAccess.file_exists(destination):
			var existing := Image.load_from_file(destination)
			assert(existing != null and existing.get_size() == image.get_size() and existing.get_data() == image.get_data(),
				"Refusing to replace changed artwork: " + destination)
		else:
			assert(image.save_png(destination) == OK)
		actor["atlas_path"] = destination
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10): await process_frame
	while filesystem.is_scanning() or filesystem.is_importing(): await process_frame
	for actor in manifest.actors + manifest.effects:
		_save_frames(actor, float(manifest.fps))
	var output := FileAccess.open(path.get_base_dir().path_join("registered.json"), FileAccess.WRITE)
	output.store_string(JSON.stringify(manifest, "\t"))
	print("QIXING ART REGISTRATION PASS: three actors, two attacks and two poison visuals")
	quit()

## Pack occupied poses while preserving the registered source origin and frame map.
func _pack_layer(actor_name: String, layer: Dictionary) -> bool:
	var images: Array[Image] = []
	var poses := {}
	var hashes := {}
	var bounds := Rect2i()
	var placements: Array[Vector2i] = []
	for action in layer.actions:
		var cells: Array[int] = []
		for frame in range(1, int(action.frames) + 1):
			var picture := Image.load_from_file(action.input.path_join("%d.png" % frame))
			if picture == null: return false
			var visible := picture.get_used_rect()
			var placement := Vector2i(floori(float(action.bounds[0]) + float(action.matrix[4])), floori(float(action.bounds[1]) + float(action.matrix[5]))) + visible.position
			if visible.has_area():
				var registered := Rect2i(placement, visible.size)
				bounds = registered if not bounds.has_area() else bounds.merge(registered)
				picture = picture.get_region(visible)
			else: picture = Image.create_empty(1, 1, false, Image.FORMAT_RGBA8)
			var hash := HashingContext.new(); hash.start(HashingContext.HASH_SHA256); hash.update(picture.get_data())
			var digest := "%s/%s/%s" % [hash.finish().hex_encode(), picture.get_size(), placement]
			if not hashes.has(digest): hashes[digest] = images.size(); images.append(picture); placements.append(placement)
			cells.append(hashes[digest])
		poses[action.name] = cells
	if not bounds.has_area(): return false
	var columns := mini(8, images.size())
	var rows := ceili(float(images.size()) / columns)
	var atlas := Image.create_empty(bounds.size.x * columns, bounds.size.y * rows, false, Image.FORMAT_RGBA8)
	for cell in images.size():
		atlas.blit_rect(images[cell], Rect2i(Vector2i.ZERO, images[cell].get_size()), Vector2i(cell % columns, cell / columns) * bounds.size + placements[cell] - bounds.position)
	DirAccess.make_dir_recursive_absolute(layer.output.get_base_dir())
	if atlas.save_png(layer.output) != OK: return false
	layer["cell_size"] = [bounds.size.x, bounds.size.y]
	layer["sprite_offset"] = [float(bounds.position.x) + float(bounds.size.x)*0.5, float(bounds.position.y) + float(bounds.size.y)*0.5]
	layer["columns"] = columns
	layer["rows"] = rows
	layer["poses"] = poses
	return true

## Save frame order and timing without embedding the frames in an entity scene.
func _save_frames(actor: Dictionary, fps: float) -> void:
	var layer: Dictionary = actor.layers[0]
	var atlas: Texture2D = load(actor.atlas_path)
	assert(atlas != null)
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var textures := {}
	var size := Vector2(layer.cell_size[0], layer.cell_size[1])
	for action in actor.actions:
		frames.add_animation(action.name)
		frames.set_animation_speed(action.name, fps)
		frames.set_animation_loop(action.name, action.name in ["idle", "run"] or actor.name == "poison_head")
		for index in layer.poses[action.name]:
			var cell := int(index)
			if not textures.has(cell):
				var texture := AtlasTexture.new()
				texture.atlas = atlas
				texture.region = Rect2(Vector2(cell % int(layer.columns), cell / int(layer.columns))*size, size)
				textures[cell] = texture
			frames.add_frame(action.name, textures[cell])
	var destination: String = actor.atlas_path.get_basename() + "_frames.tres"
	assert(ResourceSaver.save(frames, destination) == OK)
	# SpriteFrames belongs to the exported layer. Keeping the path beside the
	# atlas makes the manifest correct when an actor later gains more layers.
	layer["frames_path"] = destination
