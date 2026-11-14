extends SceneTree
## Add one independent equipment atlas without modifying existing cells or animation timing.

func _initialize() -> void:
	var path := OS.get_cmdline_user_args()[0]
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	assert(not FileAccess.file_exists(data.output), "Refusing to overwrite authored wardrobe atlas")
	var density := int(data.get("density", 1))
	var cell_size := 200 * density
	var atlas := Image.create_empty(int(data.columns)*cell_size,int(data.rows)*cell_size,false,Image.FORMAT_RGBA8)
	for key in data.poses:
		var pose: Array = data.poses[key]
		var base: String = data.inputs[str(int(pose[0]))].path_join("%d_" % int(pose[1]))
		var image := Image.create_empty(cell_size, cell_size, false, Image.FORMAT_RGBA8)
		if data.get("kind", "body") == "weapon":
			var fragment := 0
			while FileAccess.file_exists(base + "weapon_%d.svg" % fragment):
				var part := Image.new()
				var occluder := Image.new()
				assert(part.load_svg_from_string(FileAccess.get_file_as_string(base + "weapon_%d.svg" % fragment), density) == OK)
				assert(occluder.load_svg_from_string(FileAccess.get_file_as_string(base + "occluder_%d.svg" % fragment), density) == OK)
				for y in range(cell_size):
					for x in range(cell_size):
						var pixel := part.get_pixel(x, y)
						pixel.a *= 1.0 - occluder.get_pixel(x, y).a
						part.set_pixel(x, y, pixel)
				image.blend_rect(part, Rect2i(0, 0, cell_size, cell_size), Vector2i.ZERO)
				fragment += 1
		else:
			assert(image.load_svg_from_string(FileAccess.get_file_as_string(base + "body.svg"), density) == OK)
			assert(image.get_size() == Vector2i(cell_size,cell_size))
		atlas.blit_rect(image,Rect2i(Vector2i.ZERO,image.get_size()),Vector2i(int(key)%6,int(key)/6)*cell_size)
	assert(atlas.save_png(data.output)==OK)
	print("WARDROBE ART PASS: %d registered cells, selector %d, independent %s atlas." %
		[data.poses.size(), data.selector, data.get("kind", "body")])
	quit()
