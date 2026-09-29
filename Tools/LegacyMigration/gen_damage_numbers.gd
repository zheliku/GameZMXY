extends SceneTree
## 一次性迁移：旧项目伤害数字（Art/AllNumber/<样式>/<名>_0..9.png，每位一张）→ 新工程每样式一张横条图集。
## 横条 10 格（0..9），格宽 = 旧单图宽；飘字按格切 AtlasTexture，一张图一个 ext_resource。
## 运行：S:\Godot4\Godot4CSharp_console.exe --headless --script Tools/LegacyMigration/gen_damage_numbers.gd
## 只读旧项目（Image.load_from_file 读 PNG，不经旧工程 import）；产物登记 Docs/LegacyAssetMap.md。

const LEGACY := "P:/Godot-Project/ZMXY_BHYH/Art/AllNumber/"
const OUT := "P:/Godot-Project/GameZMXY/Godot/GodotProject/TheGame/Sprites/Number/"

## 新文件名 ← 旧（目录/前缀）。选型对应旧 DamageNumber.gd 的贴图选择（见 LegacyAssetMap.md）。
const SHEETS := {
	"damage_number_monster_physics.png": "magic/physics_",        # 怪物受物理伤害（旧放在 magic 目录）
	"damage_number_monster_physics_crit.png": "physicscrit/physics_",
	"damage_number_monster_magic.png": "magic/magic_",
	"damage_number_monster_magic_crit.png": "magiccrit/magic_",
	"damage_number_real.png": "real/real_",                       # 真实伤害（人怪共用）
	"damage_number_hero_physics.png": "monster/physics/physics_", # 英雄受物理伤害（旧放在 monster 目录）
	"damage_number_hero_magic.png": "monster/magic/magic_",
}


func _init() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	var ok := true
	for out_name in SHEETS:
		ok = _stitch(SHEETS[out_name], out_name) and ok

	var miss := Image.load_from_file(LEGACY + "miss.png")
	if miss == null:
		push_error("读取失败：miss.png")
		ok = false
	else:
		miss.save_png(OUT + "damage_number_miss.png")
		print("  wrote damage_number_miss.png (%dx%d)" % [miss.get_width(), miss.get_height()])

	quit(0 if ok else 1)


func _stitch(prefix: String, out_name: String) -> bool:
	var digits := []
	for d in 10:
		var img := Image.load_from_file(LEGACY + prefix + str(d) + ".png")
		if img == null:
			push_error("读取失败：%s%d.png" % [prefix, d])
			return false
		img.convert(Image.FORMAT_RGBA8)
		digits.append(img)

	var w: int = digits[0].get_width()
	var h: int = digits[0].get_height()
	var sheet := Image.create(w * 10, h, false, Image.FORMAT_RGBA8)
	for d in 10:
		var img: Image = digits[d]
		if img.get_width() != w or img.get_height() != h:
			push_error("%s 尺寸不一致：%d 号 %dx%d" % [prefix, d, img.get_width(), img.get_height()])
			return false
		sheet.blit_rect(img, Rect2i(0, 0, w, h), Vector2i(d * w, 0))

	sheet.save_png(OUT + out_name)
	print("  wrote %s (%dx%d, cell %dx%d)" % [out_name, w * 10, h, w, h])
	return true
