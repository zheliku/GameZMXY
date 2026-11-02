extends SceneTree
## Author only the existing fire's native geometry from verified source-frame bounds.

const LIBRARY := "res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire_library.tres"

func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1, "Expected official fire-frame JSON")
    var frames: Array = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var library: AnimationLibrary = load(LIBRARY)
    var animation := library.get_animation("play")
    var disabled_track := animation.find_track("HitBox/Shape:disabled", Animation.TYPE_VALUE)
    while animation.track_get_key_count(disabled_track) > 0:
        animation.track_remove_key(disabled_track, 0)
    animation.track_insert_key(disabled_track, 0.0, true)
    animation.track_insert_key(disabled_track, 1.0, false)
    var position_track := animation.find_track("HitBox/Shape:position", Animation.TYPE_VALUE)
    var size_track := animation.find_track("HitBox/Shape:shape", Animation.TYPE_VALUE)
    var old_track := animation.find_track("HitBox/Shape:shape:size", Animation.TYPE_VALUE)
    if old_track >= 0:
        animation.remove_track(old_track)
        size_track = animation.find_track("HitBox/Shape:shape", Animation.TYPE_VALUE)
    if position_track < 0:
        position_track = animation.add_track(Animation.TYPE_VALUE)
    if size_track < 0:
        size_track = animation.add_track(Animation.TYPE_VALUE)
    for index in [position_track, size_track]:
        while animation.track_get_key_count(index) > 0:
            animation.track_remove_key(index, 0)
        animation.value_track_set_update_mode(index, Animation.UPDATE_DISCRETE)
    animation.track_set_path(position_track, "HitBox/Shape:position")
    animation.track_set_path(size_track, "HitBox/Shape:shape")
    var previous = null
    for frame in 36:
        var value = frames[frame]
        if value == null or value == previous:
            continue
        var time := 0.0 if previous == null else frame / 24.0
        animation.track_insert_key(position_track, time, Vector2(value[0] + value[2] / 2, value[1] + value[3] / 2))
        var shape := RectangleShape2D.new()
        shape.size = Vector2(value[2], value[3])
        animation.track_insert_key(size_track, time, shape)
        previous = value
    var uid := ResourceLoader.get_resource_uid(LIBRARY)
    assert(ResourceSaver.save(library, LIBRARY) == OK)
    assert(ResourceSaver.set_uid(LIBRARY, uid) == OK)
    print("BULL FIRE AUTHOR PASS: 6 source flame stages, frames25..36 check before destroy")
    quit()
