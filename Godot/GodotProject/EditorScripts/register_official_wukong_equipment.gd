extends SceneTree
## Bind existing base looks and the original Earth Fiend armor by equipment identity.

func _initialize() -> void:
	call_deferred("_register")

func _register() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
	var resource := load(path)
	var armor: Dictionary[int, Texture2D] = {}
	var weapon: Dictionary[int, Texture2D] = {}
	var root := path.get_base_dir()
	armor[1] = load(root.path_join("wukong_body_pilgrim.png"))
	armor[5] = armor[1]
	armor[10] = load(root.path_join("wukong_body_earth_fiend.png"))
	weapon[2] = load(root.path_join("wukong_weapon_pilgrim.png"))
	weapon[6] = weapon[2]
	resource.set("m_ArmorTextures",armor)
	resource.set("m_WeaponTextures",weapon)
	assert(ResourceSaver.save(resource,path)==OK)
	print("WARDROBE BINDINGS PASS: two base armor IDs, Earth Fiend armor, two base weapon IDs.")
	quit()
