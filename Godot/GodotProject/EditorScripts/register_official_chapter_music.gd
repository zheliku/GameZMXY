extends SceneTree
## Import the three official chapter tracks as loops and assign chapter two to both cave levels.

const TRACKS := ["res://TheGame/Audios/Music/cave_theme.mp3",
    "res://TheGame/Audios/Music/sea_theme.mp3", "res://TheGame/Audios/Music/bull_demon_valley_theme.mp3"]
const LEVELS := ["res://TheGame/Scenes/Levels/zm1_04_bat_cave/level_4.tscn",
    "res://TheGame/Scenes/Levels/zm1_05_qixing_cave/level_5.tscn"]

func _initialize() -> void:
    _run.call_deferred()

## Wait for native import metadata before changing only the loop setting.
func _run() -> void:
    var filesystem := EditorInterface.get_resource_filesystem()
    filesystem.scan()
    for frame in range(10):
        await process_frame
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    for path in TRACKS:
        var options := ConfigFile.new()
        assert(options.load(path + ".import") == OK, "Missing native import: " + path)
        options.set_value("params", "loop", true)
        options.set_value("params", "loop_offset", 0.0)
        assert(options.save(path + ".import") == OK)
    filesystem.reimport_files(PackedStringArray(TRACKS))
    while filesystem.is_scanning() or filesystem.is_importing():
        await process_frame
    for path in LEVELS:
        # Repacking the whole scene expands inherited spike/exit defaults. Keep
        # every authored property and UID intact when changing this one scalar.
        var text := FileAccess.get_file_as_string(path)
        assert(text.count("m_MusicId = 102") + text.count("m_MusicId = 106") == 1)
        var file := FileAccess.open(path, FileAccess.WRITE)
        file.store_string(text.replace("m_MusicId = 102", "m_MusicId = 106")) # SoundId.CaveTheme.
        file.close()
    for path in TRACKS:
        var stream: AudioStreamMP3 = ResourceLoader.load(path, "AudioStreamMP3", ResourceLoader.CACHE_MODE_IGNORE)
        assert(stream != null and stream.loop and stream.get_length() > 10, "Chapter music is not a valid loop: " + path)
    print("OFFICIAL CHAPTER MUSIC AUTHOR PASS: three native loops, level 4/5 use bg2")
    quit()
