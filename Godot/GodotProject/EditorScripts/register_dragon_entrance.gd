extends SceneTree
## Place the source automatic entrance beneath the existing Water Curtain Cave surface.

func _initialize() -> void:
    _register.call_deferred()

func _register() -> void:
    assert(Engine.is_editor_hint())
    var path := "res://TheGame/Scenes/Levels/zm1_02_shuiliandong/level_2.tscn"
    var uid := ResourceLoader.get_resource_uid(path)
    var scene: Node = load(path).instantiate()
    assert(scene.get("m_HiddenEntrance") == null, "Entrance already authored; review before replacing")
    var root := scene.get_node("World")
    var entrance := Area2D.new()
    entrance.name = "DragonPalaceEntrance"
    entrance.position = Vector2(2151.024558265, 615.010125734)
    entrance.collision_layer = 0
    entrance.collision_mask = 2
    entrance.monitoring = false
    entrance.monitorable = false
    root.add_child(entrance)
    entrance.owner = scene
    var shape := CollisionShape2D.new()
    shape.name = "Contact"
    var rectangle := RectangleShape2D.new()
    rectangle.size = Vector2(499.95119367, 50.00032979)
    shape.shape = rectangle
    entrance.add_child(shape)
    shape.owner = scene
    var player := AnimationPlayer.new()
    player.name = "EntranceFade"
    scene.add_child(player)
    player.owner = scene
    var animation := Animation.new()
    animation.length = 1
    var track := animation.add_track(Animation.TYPE_VALUE)
    animation.track_set_path(track, NodePath(".:EntranceOpacity"))
    animation.track_insert_key(track, 0, 1.0)
    animation.track_insert_key(track, 1, 0.0)
    var library := AnimationLibrary.new()
    library.add_animation("fade", animation)
    var library_path := "res://TheGame/Scenes/Levels/zm1_02_shuiliandong/hidden_entrance_fade.tres"
    assert(ResourceSaver.save(library, library_path) == OK)
    player.add_animation_library("", load(library_path))
    player.assigned_animation = "fade"
    scene.set("m_HiddenEntrance", entrance)
    scene.set("m_EntranceFade", player)
    assert(scene.get("m_HiddenEntrance") == entrance and scene.get("m_EntranceFade") == player)
    var packed := PackedScene.new()
    assert(packed.pack(scene) == OK)
    assert(ResourceSaver.save(packed, path) == OK)
    assert(ResourceSaver.set_uid(path, uid) == OK)
    scene.free()
    print("DRAGON ENTRANCE AUTHOR PASS: source rectangle beneath water, automatic contact and native one-second fade")
    quit()
