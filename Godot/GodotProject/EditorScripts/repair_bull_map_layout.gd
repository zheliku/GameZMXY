extends SceneTree
## Restore SelectPlace.s4_1's original registration and one-to-one current icon.

func _initialize() -> void:
    var path := "res://TheGame/UIs/map_form.tscn"
    var form: Control = load(path).instantiate()
    var button: TextureButton = form.get_node("m_Niumogu")
    # OtherMat sprite505 places s4_1 at (283.2, 385.05); sprite441 is 57 x 56.
    button.position = Vector2(283.2, 385.05) - button.size * 0.5
    var current: Sprite2D = button.get_node("CurrentHint")
    current.position = button.size * 0.5
    current.scale = Vector2.ONE
    # Sprite443 bounds (-72.7,-41,152,101), with the existing 12px filter padding.
    var hover: TextureRect = button.get_node("LocationGlow")
    hover.position = button.size * 0.5 + Vector2(-84.7, -53)
    hover.size = Vector2(176, 125)
    hover.pivot_offset = Vector2(84.7, 53)
    var packed := PackedScene.new()
    assert(packed.pack(form) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    form.free()
    print("BULL MAP AUTHOR PASS: official origin, 1:1 current icon and registered hover")
    quit()
