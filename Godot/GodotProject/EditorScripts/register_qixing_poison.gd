extends SceneTree
## Attach the verified poison visuals without rebuilding the authored hero scene.

func _initialize() -> void:
	call_deferred("_register")

func _register() -> void:
	var path := "res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn"
	var hero: Node2D = load(path).instantiate(PackedScene.GEN_EDIT_STATE_MAIN)
	var status := hero.get_node_or_null("m_StatusEffects")
	if status == null:
		status = Node2D.new()
		status.name = "m_StatusEffects"
		hero.add_child(status)
		status.owner = hero
	for pair in [["m_PoisonHead", "poison_head", -70], ["m_PoisonPulse", "poison_up", -50]]:
		var sprite: AnimatedSprite2D = status.get_node_or_null(pair[0])
		if sprite == null:
			sprite = AnimatedSprite2D.new()
			sprite.name = pair[0]
			status.add_child(sprite)
			sprite.owner = hero
		sprite.sprite_frames = load("res://TheGame/Sprites/Characters/Heroes/Status/%s_frames.tres" % pair[1])
		sprite.animation = "play"
		sprite.position.y = pair[2]
		sprite.offset = Vector2(0.5, -13.5) if pair[1] == "poison_head" else Vector2(-22, -26.5)
		sprite.visible = false
		hero.set(pair[0], sprite)
	var packed := PackedScene.new()
	assert(packed.pack(hero) == OK)
	assert(ResourceSaver.save(packed, path) == OK)
	hero.free()
	print("QIXING POISON VISUALS PASS: head and pulse bound to hero")
	quit()
