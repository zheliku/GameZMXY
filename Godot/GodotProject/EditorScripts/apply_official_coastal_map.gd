extends SceneTree
## Register coastal exports with native animation pages; retain scene text and UIDs.

func _initialize() -> void:
    _apply.call_deferred()

func _node_properties(text: String, name: String, parent: String, properties: Dictionary) -> String:
    var regex := RegEx.new()
    regex.compile('(?ms)^\\[node name="' + name + '"[^\\n]*parent="' + parent + '"[^\\n]*\\]\\n.*?(?=^\\[|\\z)')
    var match := regex.search(text)
    assert(match != null, "Missing coastal scene node: " + parent + "/" + name)
    var block := match.get_string()
    for property in properties:
        var value := var_to_str(properties[property])
        var line := RegEx.new()
        line.compile('(?m)^' + property + ' = [^\\n]*')
        if line.search(block):
            block = line.sub(block, property + " = " + value)
        else:
            block = block.trim_suffix("\n\n") + "\n" + property + " = " + value + "\n\n"
    return text.substr(0, match.get_start()) + block + text.substr(match.get_end())

func _write(path: String, text: String, crlf: bool) -> void:
    var file := FileAccess.open(path, FileAccess.WRITE)
    file.store_string(text.replace("\n", "\r\n") if crlf else text)

func _apply() -> void:
    var args := OS.get_cmdline_user_args()
    assert(args.size() == 1, "Pass the coastal export manifest after copying its PNGs")
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
    var density := int(manifest.density)
    var path := "res://TheGame/UIs/map_form.tscn"
    var original := FileAccess.get_file_as_string(path)
    var text := original.replace("\r\n", "\n")
    for location in manifest.locations:
        var key: String = location.name
        var node_name := "m_" + key.capitalize()
        var size := Vector2(location.logical_size[0], location.logical_size[1])
        var origin := Vector2(location.position[0], location.position[1])
        var offset := Vector2(location.offset[0], location.offset[1])
        var current: Dictionary = location.current
        var art_offset := Vector2(current.offset[0], current.offset[1])
        if density == 1:
            text = _node_properties(text, node_name, "Frame", {
                "offset_left": origin.x + offset.x, "offset_top": origin.y + offset.y,
                "offset_right": origin.x + offset.x + size.x, "offset_bottom": origin.y + offset.y + size.y,
                "pivot_offset": -offset})
            text = _node_properties(text, "LocationGlow", "Frame/" + node_name, {
                "offset_right": size.x, "offset_bottom": size.y, "pivot_offset": -offset})
        text = _node_properties(text, "CurrentHint", "Frame/" + node_name, {
            "position": size * .5, "scale": Vector2.ONE / density,
            "offset": art_offset, "hframes": int(current.columns), "vframes": int(current.rows)})
        var art_path := "res://TheGame/Sprites/UI/frontend/"
        for page in current.pages:
            assert(FileAccess.get_sha256(art_path + str(page.file)) == page.sha256,
                "Copy the verified atlas page to its original resource path before registration")
        var library_path := art_path + "map_" + key + "_hover_animation.tres"
        var library_original := FileAccess.get_file_as_string(library_path)
        var library_text := library_original.replace("\r\n", "\n")
        var library: AnimationLibrary = load(library_path)
        var animation := library.get_animation("current")
        var frame_track := animation.find_track(NodePath(node_name + "/CurrentHint:frame"), Animation.TYPE_VALUE)
        assert(frame_track >= 0 and animation.track_get_key_count(frame_track) == current.frames.size())
        var frames: PackedStringArray = []
        for index in current.frames.size():
            var previous := int(animation.track_get_key_value(frame_track, index))
            assert(previous == index or previous == int(current.frames[index].cell),
                "Unexpected source frame mapping")
            frames.append(str(int(current.frames[index].cell)))
        var regex := RegEx.new()
        regex.compile('(?s)(tracks/' + str(frame_track) + '/keys = \\{.*?"values": \\[)[^\\]]*(\\])')
        var frame_path := 'tracks/' + str(frame_track) + '/path = NodePath("' + node_name + '/CurrentHint:frame")'
        var frame_path_start := library_text.find(frame_path)
        assert(frame_path_start >= 0)
        var keys := regex.search(library_text, frame_path_start)
        assert(keys != null)
        library_text = library_text.substr(0, keys.get_start()) + keys.get_string(1) + ", ".join(frames) + keys.get_string(2) + library_text.substr(keys.get_end())
        if current.pages.size() > 1:
            var declarations := ""
            var times: PackedStringArray = []
            var values: PackedStringArray = []
            for index in current.pages.size():
                var page: Dictionary = current.pages[index]
                var texture_path: String = art_path + page.file
                var texture: Texture2D = load(texture_path)
                assert(texture.get_size() == Vector2(page.size[0], page.size[1]))
                var uid := ResourceUID.id_to_text(ResourceLoader.get_resource_uid(texture_path))
                if not library_text.contains('id="page' + str(index) + '"'):
                    declarations += '[ext_resource type="Texture2D" uid="' + uid + '" path="' + texture_path + '" id="page' + str(index) + '"]\n'
                times.append(var_to_str(animation.track_get_key_time(frame_track, int(page.first_source_frame))))
                values.append('ExtResource("page' + str(index) + '")')
            var first := library_text.find("[sub_resource")
            if not declarations.is_empty():
                library_text = library_text.insert(first, declarations + "\n")
            var property := node_name + "/CurrentHint:texture"
            var track := animation.find_track(NodePath(property), Animation.TYPE_VALUE)
            var has_texture_track := track >= 0
            if not has_texture_track:
                track = animation.get_track_count()
            var extra := "tracks/%d/type = \"value\"\ntracks/%d/imported = false\ntracks/%d/enabled = true\ntracks/%d/path = NodePath(\"%s\")\ntracks/%d/interp = 1\ntracks/%d/loop_wrap = true\ntracks/%d/keys = {\n\"times\": PackedFloat32Array(%s),\n\"transitions\": PackedFloat32Array(1, 1),\n\"update\": 1,\n\"values\": [%s]\n}\n\n" % [track, track, track, track, property, track, track, track, ", ".join(times), ", ".join(values)]
            var section := RegEx.new()
            section.compile('(?s)\\[sub_resource[^\\n]*\\]\\nresource_name = "current".*?(?=\\[sub_resource|\\[resource\\])')
            var found := section.search(library_text)
            assert(found != null)
            var current_text := found.get_string()
            if has_texture_track:
                var previous_track := RegEx.new()
                previous_track.compile('(?ms)^tracks/' + str(track) + '/type = .*?(?=^tracks/\\d+/type|\\z)')
                current_text = previous_track.sub(current_text, "")
            library_text = library_text.substr(0, found.get_start()) + current_text + extra + library_text.substr(found.get_end())
        _write(library_path, library_text, "\r\n" in library_original)
    _write(path, text, "\r\n" in original)
    print("OFFICIAL COAST APPLY PASS: original logical bounds/UIDs, native animation pages, density/transparent-crop compensation")
    quit()
