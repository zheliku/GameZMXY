extends SceneTree
## Register the three official cave drops and the complete selector-7 Wukong weapon.

func _initialize() -> void:
	call_deferred("_register")

func _register() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for index in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var path := "res://TheGame/Sprites/Items/item_appearances.tres"
	var library := load(path)
	var items: Array = library.get("m_Items")
	var script := load("res://TheGame/GameScripts/Models/Profile/ItemAppearanceResource.cs")
	for entry in [[12, "bracelet"], [13, "sword"], [14, "clothes"]]:
		if items.any(func(item): return int(item.get("m_EquipmentId")) == entry[0]):
			continue
		var item: Resource = script.new()
		item.set("m_EquipmentId", entry[0])
		item.set("m_InventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_heirloom_%s.png" % entry[1]))
		item.set("m_GroundIcon", load("res://TheGame/Sprites/Items/equipment/ground_heirloom_%s.png" % entry[1]))
		assert(item.get("m_InventoryIcon") != null and item.get("m_GroundIcon") != null)
		items.append(item)
	library.set("m_Items", items)
	assert(ResourceSaver.save(library, path) == OK)
	var appearance_path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
	var appearance := load(appearance_path)
	var weapons: Dictionary = appearance.get("m_WeaponTextures")
	weapons[13] = load(appearance_path.get_base_dir().path_join("wukong_weapon_heirloom.png"))
	assert(weapons[13] != null)
	appearance.set("m_WeaponTextures", weapons)
	assert(ResourceSaver.save(appearance, appearance_path) == OK)
	print("HEIRLOOM EQUIPMENT PASS: 3 bag/ground icons and complete weapon selector 7 registered")
	quit()
