extends SceneTree
## Align all boss names to the same right edge, overlapping the brush's left cap.

func _initialize() -> void:
    var path := "res://TheGame/UIs/Battle/encounter_health_bar.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var scene: Control = load(path).instantiate()
    var label: Label = scene.get_node("Name")
    label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
    label.grow_horizontal = Control.GROW_DIRECTION_BEGIN
    var packed := PackedScene.new()
    assert(packed.pack(scene) == OK)
    packed.take_over_path(path)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    scene.free()
    print("BOSS NAME AUTHOR PASS: one right edge for all encounter names")
    quit()
