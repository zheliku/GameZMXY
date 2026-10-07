extends SceneTree
## Headless entry for the existing Collection Res generator; plugin sources stay read-only.

func _initialize() -> void:
    call_deferred("_generate")

func _generate() -> void:
    var generator = load("res://addons/TopMenu/GameFrameworkTopMenu.cs").new()
    generator.call("CollectionRes")
    generator.free()
    # Let the requested filesystem scan settle before editor shutdown.
    for frame in range(10):
        await process_frame
    quit()
