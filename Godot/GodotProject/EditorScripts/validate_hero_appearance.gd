extends SceneTree
## Cold editor reload, save and re-read preserve every wardrobe mapping.

func _initialize() -> void:
	assert(Engine.is_editor_hint(), "Run with --editor to preserve resource UIDs")
	var path := "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
	var appearance: Resource = load(path)
	assert(appearance.get("m_ArmorTextures").size() == 3)
	assert(appearance.get("m_WeaponTextures").size() == 2)
	assert(ResourceSaver.save(appearance, path) == OK)
	var reloaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	for id in [1, 5, 10]:
		assert(reloaded.get("m_ArmorTextures")[id] is Texture2D)
	for id in [2, 6]:
		assert(reloaded.get("m_WeaponTextures")[id] is Texture2D)
	print("APPEARANCE ROUNDTRIP PASS: 3 armor, 2 weapon; cold load/editor save/reload preserved.")
	quit()
