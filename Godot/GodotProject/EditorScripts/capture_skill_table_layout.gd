extends SceneTree
## Render the editable scene at 1:1 with preview text, without starting gameplay.

func _initialize() -> void:
    _capture.call_deferred()

func _capture() -> void:
    root.size = Vector2i(940, 590)
    var form: Control = load("res://TheGame/UIs/skill_form.tscn").instantiate()
    form.set_script(null)
    var active: Control = form.get_node("Content/m_ActivePage")
    var passive: Control = form.get_node("Content/m_PassivePage")
    active.set_script(null)
    passive.set_script(null)
    root.add_child(form)
    form.show()
    var output := "res://.godot/validation/ui_feedback_20261029/skill_layout"
    DirAccess.make_dir_recursive_absolute(output)
    for name in ["wukong", "sanzang", "passive"]:
        active.visible = name != "passive"
        passive.visible = name == "passive"
        active.get_node("m_WukongRows").visible = name == "wukong"
        active.get_node("m_TangsengRows").visible = name == "sanzang"
        for frame in 3:
            await process_frame
        await RenderingServer.frame_post_draw
        assert(root.get_texture().get_image().save_png(output + "/" + name + ".png") == OK)
    form.queue_free()
    await process_frame
    print("SKILL TABLE CAPTURE PASS: three native 940x590 previews")
    quit()
