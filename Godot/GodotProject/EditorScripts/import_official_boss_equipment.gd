extends SceneTree
## Register original equipment art and activate the already positioned accessory slot.

func _initialize() -> void:
	call_deferred("_build")

## Save native resources after the newly exported PNGs and C# types are imported.
func _build() -> void:
	var filesystem := EditorInterface.get_resource_filesystem()
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var library_path := "res://TheGame/Sprites/Items/item_appearances.tres"
	var library := load(library_path)
	var items: Array = library.get("m_Items")
	var item_script := load("res://TheGame/GameScripts/Equipment/ItemAppearanceResource.cs")
	for entry in [[9, "earth_fiend_ring"], [10, "earth_fiend_ape_armor"], [11, "earth_fiend_scepter"]]:
		for item in items:
			if int(item.get("m_EquipmentId")) == entry[0]:
				push_error("Boss equipment already registered")
				quit(1)
				return
		var item: Resource = item_script.new()
		item.set("m_EquipmentId", entry[0])
		item.set("m_InventoryIcon", load("res://TheGame/Sprites/Items/equipment/equipment_%s.png" % entry[1]))
		item.set("m_GroundIcon", load("res://TheGame/Sprites/Items/equipment/ground_%s.png" % entry[1]))
		items.append(item)
	library.set("m_Items", items)
	assert(ResourceSaver.save(library, library_path) == OK)
	var form_path := "res://TheGame/UIs/inventory_form.tscn"
	var form: Control = load(form_path).instantiate()
	var panel := form.get_node("Window/CharacterPanel")
	var slots := panel.get_node("EquipmentSlots")
	var placeholder: TextureRect = slots.get_node("Slotaccessory")
	var cell: TextureRect = load("res://TheGame/UIs/Inventory/equipment_cell.tscn").instantiate()
	cell.name = "m_AccessorySlot"
	cell.position = placeholder.position
	cell.size = placeholder.size
	cell.texture = placeholder.texture
	cell.material = slots.get_node("m_ArmorSlot").material.duplicate()
	slots.add_child(cell)
	cell.owner = form
	slots.move_child(cell, placeholder.get_index())
	placeholder.free()
	panel.set("m_AccessorySlot", cell)
	var packed := PackedScene.new()
	assert(packed.pack(form) == OK)
	assert(ResourceSaver.save(packed, form_path) == OK)
	form.free()
	print("BOSS EQUIPMENT PASS: original art registered; accessory slot uses shared equipment cell.")
	filesystem.scan()
	for frame in range(10):
		await process_frame
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	quit()
