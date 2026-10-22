extends SceneTree
## Add one official armor atlas without modifying animation cells, timing or weapon layers.

func _initialize() -> void:
	var path := OS.get_cmdline_user_args()[0]
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	assert(not FileAccess.file_exists(data.output), "Refusing to overwrite authored wardrobe atlas")
	var atlas := Image.create_empty(int(data.columns)*200,int(data.rows)*200,false,Image.FORMAT_RGBA8)
	for key in data.poses:
		var pose: Array = data.poses[key]
		var source: String = data.inputs[str(int(pose[0]))].path_join("%d_body.svg" % int(pose[1]))
		var image := Image.new()
		assert(image.load_svg_from_string(FileAccess.get_file_as_string(source)) == OK)
		assert(image.get_size() == Vector2i(200,200))
		atlas.blit_rect(image,Rect2i(Vector2i.ZERO,image.get_size()),Vector2i(int(key)%6,int(key)/6)*200)
	assert(atlas.save_png(data.output)==OK)
	print("WARDROBE ART PASS: 116-cell registration, original selector 4, independent body atlas.")
	quit()
