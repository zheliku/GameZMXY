extends SceneTree
## Headless entry for the existing Collection Res generator; plugin sources stay read-only.

func _initialize() -> void:
    call_deferred("_generate")

func _generate() -> void:
    if not Engine.is_editor_hint():
        printerr("Collection Res requires --editor.")
        quit(1)
        return
    var filesystem = EditorInterface.get_resource_filesystem()
    if not await _wait_for_filesystem(filesystem):
        quit(1)
        return
    var generator = load("res://addons/TopMenu/GameFrameworkTopMenu.cs").new()
    generator.call("CollectionRes")
    generator.free()
    # The generator requests a scan; allow it to start before checking completion.
    for frame in range(10):
        await process_frame
    if not await _wait_for_filesystem(filesystem):
        quit(1)
        return
    quit()

## Wait for the editor scan/import requested by the existing generator, with a bounded timeout.
func _wait_for_filesystem(filesystem: EditorFileSystem) -> bool:
    var deadline = Time.get_ticks_msec() + 30000
    while filesystem.is_scanning() or filesystem.is_importing():
        if Time.get_ticks_msec() >= deadline:
            printerr("Collection Res filesystem scan/import timed out.")
            return false
        await process_frame
    return true
