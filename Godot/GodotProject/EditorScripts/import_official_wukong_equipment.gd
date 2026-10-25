extends SceneTree
## Add one independent equipment atlas without modifying existing cells or animation timing.

func _initialize() -> void:
	var path := OS.get_cmdline_user_args()[0]
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	assert(not FileAccess.file_exists(data.output), "Refusing to overwrite authored wardrobe atlas")
	var atlas := Image.create_empty(int(data.columns)*200,int(data.rows)*200,false,Image.FORMAT_RGBA8)
	for key in data.poses:
		var pose: Array = data.poses[key]
		var base: String = data.inputs[str(int(pose[0]))].path_join("%d_" % int(pose[1]))
		var image := Image.create_empty(200, 200, false, Image.FORMAT_RGBA8)
		if data.get("kind", "body") == "weapon":
			var fragment := 0
			while FileAccess.file_exists(base + "weapon_%d.svg" % fragment):
				var part := Image.new()
				var occluder := Image.new()
				assert(part.load_svg_from_string(FileAccess.get_file_as_string(base + "weapon_%d.svg" % fragment)) == OK)
				assert(occluder.load_svg_from_string(FileAccess.get_file_as_string(base + "occluder_%d.svg" % fragment)) == OK)
				for y in range(200):
					for x in range(200):
						var pixel := part.get_pixel(x, y)
						pixel.a *= 1.0 - occluder.get_pixel(x, y).a
						part.set_pixel(x, y, pixel)
				image.blend_rect(part, Rect2i(0, 0, 200, 200), Vector2i.ZERO)
				fragment += 1
		else:
			assert(image.load_svg_from_string(FileAccess.get_file_as_string(base + "body.svg")) == OK)
			assert(image.get_size() == Vector2i(200,200))
		atlas.blit_rect(image,Rect2i(Vector2i.ZERO,image.get_size()),Vector2i(int(key)%6,int(key)/6)*200)
	assert(atlas.save_png(data.output)==OK)
	print("WARDROBE ART PASS: %d registered cells, selector %d, independent %s atlas." %
		[data.poses.size(), data.selector, data.get("kind", "body")])
	quit()
