extends SceneTree
## 补齐已有 BOSS 的特效复位轨道，保留招式原有关键帧和 UID。
func _initialize() -> void:
    assert(Engine.is_editor_hint(), "Run with --editor to preserve resource UIDs")
    for name in ["macaque_king", "tamarin_king"]:
        var path := "res://TheGame/Entitys/Animations/Enemies/Bosses/%s/%s_library.tres" % ["zm1_02_macaque_king" if name == "macaque_king" else "zm1_03_tamarin_king", name]
        var library: AnimationLibrary = load(path)
        var frames: SpriteFrames = load("res://TheGame/Sprites/Characters/Enemies/Bosses/%s/%s_effect_frames.tres" % ["zm1_02_macaque_king" if name == "macaque_king" else "zm1_03_tamarin_king", name])
        var added := 0
        for action in library.get_animation_list():
            var animation := library.get_animation(action)
            for item in [["m_Body/Effect:animation", StringName(frames.get_animation_names()[0])], ["m_Body/Effect:frame", 0]]:
                if animation.find_track(NodePath(item[0]), Animation.TYPE_VALUE) >= 0:
                    continue
                var track := animation.add_track(Animation.TYPE_VALUE)
                animation.track_set_path(track, NodePath(item[0]))
                animation.value_track_set_update_mode(track, Animation.UPDATE_DISCRETE)
                animation.track_insert_key(track, 0, item[1])
                added += 1
        assert(ResourceSaver.save(library, path) == OK)
        print("BOSS RESET PASS: ", name, ", added=", added)
    quit()
