extends SceneTree
## Register the transparent official necklace without rebuilding existing appearances.

func _initialize() -> void:
	call_deferred("_register")

func _register() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10): await process_frame
	while filesystem.is_scanning() or filesystem.is_importing(): await process_frame
	var path := "res://TheGame/Sprites/Items/item_appearances.tres"
	var library: Resource = load(path)
	var items: Array = library.get("m_Items")
	if not items.any(func(item): return int(item.get("m_EquipmentId")) == 15):
		var item: Resource = load("res://TheGame/GameScripts/Models/Profile/ItemAppearanceResource.cs").new()
		item.set("m_EquipmentId", 15)
		item.set("m_InventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_heavenly_fiend_necklace.png"))
		item.set("m_GroundIcon", load("res://TheGame/Sprites/Items/equipment/ground_heavenly_fiend_necklace.png"))
		assert(item.get("m_InventoryIcon") != null and item.get("m_GroundIcon") != null)
		items.append(item)
		library.set("m_Items", items)
		assert(ResourceSaver.save(library, path) == OK)
	print("QIXING EQUIPMENT PASS: official heavenly fiend necklace registered")
	quit()
