"""Install the reviewed 2-3 actor/effect atlases and native timelines.

The staged manifest is authoring evidence. This importer only materializes the
reviewed atlas, SpriteFrames, AnimationLibrary and minimal pooled effect/entity
scenes; gameplay values remain in the formal tables and concrete C# states.
"""

import argparse
import json
import re
import shutil
from pathlib import Path


FPS = 24.0
HIT_PATH = "m_HitBoxRoot/m_HitBox/CollisionShape2D"


def fmt_float(value):
    return f"{float(value):.8f}".rstrip("0").rstrip(".")


def vec2(value):
    return f"Vector2({fmt_float(value[0])}, {fmt_float(value[1])})"


def gd_string(value):
    return '"' + str(value).replace('"', '\\"') + '"'


def copy_atlas(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.read_bytes() != source.read_bytes():
        raise ValueError(f"Refusing to replace changed formal atlas: {destination}")
    if not destination.exists():
        shutil.copy2(source, destination)


def write_text(path, text, overwrite=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and not overwrite and path.read_text(encoding="utf-8") != text:
        raise ValueError(f"Refusing to replace changed authored resource: {path}")
    if overwrite or not path.exists():
        path.write_text(text, encoding="utf-8")


def sprite_frames_resource(atlas_path, layer):
    ext = "atlas"
    lines = [
        '[gd_resource type="SpriteFrames" format=3]',
        f'[ext_resource type="Texture2D" path={gd_string(atlas_path)} id="{ext}"]',
    ]
    cell_width, cell_height = layer["cell_size"]
    textures = {}
    for cell in sorted({cell for values in layer["poses"].values() for cell in values}):
        resource_id = f"AtlasTexture_{cell:04d}"
        textures[cell] = resource_id
        x = (cell % int(layer["columns"])) * cell_width
        y = (cell // int(layer["columns"])) * cell_height
        entry = layer.get('atlas_regions', {}).get(str(cell))
        region = entry['region'] if entry else [x, y, cell_width, cell_height]
        lines.extend([
            f'[sub_resource type="AtlasTexture" id="{resource_id}"]',
            f'atlas = ExtResource("{ext}")',
            'region = Rect2(' + ', '.join(fmt_float(value) for value in region) + ')',
            *(['margin = Rect2(' + ', '.join(fmt_float(value) for value in entry['margin']) + ')'] if entry else []),
        ])
    animations = []
    for action, cells in layer["poses"].items():
        frame_lines = ", ".join(
            '{"duration": 1.0, "texture": SubResource("%s")}' % textures[cell]
            for cell in cells
        )
        loop = 1 if action.removesuffix("_low") in ("idle", "run") else 0
        animations.append(
            '{"frames": [%s], "loop": %d, "name": &"%s", "speed": 24.0}'
            % (frame_lines, loop, action)
        )
    lines.extend(["", "[resource]", "animations = [" + ", ".join(animations) + "]", ""])
    return "\n".join(lines)


def shape_resources(actions):
    shapes = {(1.0, 1.0): "shape_empty"}
    for action in actions:
        for bounds in action.get("hit_bounds", []):
            if bounds:
                size = (round(float(bounds[2]), 6), round(float(bounds[3]), 6))
                shapes.setdefault(size, f"shape_{len(shapes):03d}")
    result = []
    for size, resource_id in shapes.items():
        result.extend([
            f'[sub_resource type="RectangleShape2D" id="{resource_id}"]',
            f"size = Vector2({fmt_float(size[0])}, {fmt_float(size[1])})",
            "",
        ])
    return shapes, result


def track_value(index, path, times, values, value_kind="value"):
    transitions = ", ".join("1" for _ in times)
    if value_kind == "bool":
        rendered = ", ".join("true" if value else "false" for value in values)
    elif value_kind == "int":
        rendered = ", ".join(str(int(value)) for value in values)
    elif value_kind == "vec2":
        rendered = ", ".join(vec2(value) for value in values)
    else:
        rendered = ", ".join(str(value) for value in values)
    return "\n".join([
        f"tracks/{index}/type = \"value\"",
        f"tracks/{index}/imported = false",
        f"tracks/{index}/enabled = true",
        f'tracks/{index}/path = NodePath("{path}")',
        f"tracks/{index}/interp = 1",
        f"tracks/{index}/loop_wrap = true",
        f"tracks/{index}/keys = {{",
        f'"times": PackedFloat32Array({", ".join(fmt_float(x) for x in times)}),',
        f'"transitions": PackedFloat32Array({transitions}),',
        '"update": 1,',
        f'"values": [{rendered}]',
        "}",
    ])


def compact_keys(times, values):
    result_times, result_values = [], []
    for time, value in zip(times, values):
        if result_values and value == result_values[-1]:
            continue
        result_times.append(time)
        result_values.append(value)
    return result_times, result_values


def animation_resource(actor, layer, actor_kind):
    actions = actor["actions"]
    shapes, shape_text = shape_resources(actions)
    lines = ['[gd_resource type="AnimationLibrary" format=3]', ""]
    lines.extend(shape_text)
    animation_entries = []
    offset = layer["sprite_offset"]
    for action in actions:
        name = action["name"]
        frames = int(action["frames"])
        length = frames / FPS
        complete = action.get("complete_frames") or []
        marker_time = ((int(complete[-1]) - 1) / FPS) if complete else None
        lines.extend([f'[sub_resource type="Animation" id="Animation_{name}"]', f"length = {fmt_float(length)}"])
        if name.removesuffix("_low") in ("idle", "run"):
            lines.append("loop_mode = 1")
        if marker_time is not None:
            lines.extend([
                "markers = [{",
                '"color": Color(1, 1, 1, 1),',
                '"name": &"complete",',
                f'"time": {fmt_float(marker_time)}',
                "}]",
            ])
        times = [index / FPS for index in range(frames)]
        lines.append(track_value(0, "m_Body:animation", [0.0], [f'&"{name}"']))
        lines.append(track_value(1, "m_Body:offset", [0.0], [offset], "vec2"))
        lines.append(track_value(2, "m_Body:frame", times, list(range(frames)), "int"))
        boxes = action.get("hit_bounds", [])
        disabled = [not bool(box) for box in boxes]
        positions = [
            [float(box[0]) + float(box[2]) * 0.5, float(box[1]) + float(box[3]) * 0.5]
            if box else [0.0, 0.0]
            for box in boxes
        ]
        shape_values = [
            f'SubResource("{shapes[(round(float(box[2]), 6), round(float(box[3]), 6))]}")'
            if box else 'SubResource("shape_empty")'
            for box in boxes
        ]
        d_times, d_values = compact_keys(times, disabled)
        p_times, p_values = compact_keys(times, positions)
        s_times, s_values = compact_keys(times, shape_values)
        lines.append(track_value(3, f"{HIT_PATH}:disabled", d_times, d_values, "bool"))
        lines.append(track_value(4, f"{HIT_PATH}:position", p_times, p_values, "vec2"))
        lines.append(track_value(5, f"{HIT_PATH}:shape", s_times, s_values))
        method_events = []
        for event in action.get("native_method_events", []):
            method_events.append(((int(event["frame"]) - 1) / FPS, event["method"], event.get("args", [])))
        for event in action.get("frame_events", []):
            frame = int(event["frame"])
            body = event["body"]
            if "fireHit()" in body:
                y = 83.8 if name == "attack_1" else -125.95
                method_events.append(((frame - 1) / FPS, "SpawnFireAnimEvent", [y]))
            invulnerability = re.search(r"setYourFather\((\d+)\)", body)
            if invulnerability:
                method_events.append(((frame - 1) / FPS, "BeginInvulnerabilityAnimEvent", [int(invulnerability[1]) / FPS]))
            if "newAttackId()" in body:
                if actor_kind == "boss" and name == "attack_2":
                    method_events.append(((frame - 1) / FPS, "BeginDashAnimEvent", []))
                else:
                    method_events.append(((frame - 1) / FPS, "NewAttackAnimEvent", []))
            if "BossDead" in body:
                method_events.append(((frame - 1) / FPS, "SpawnBossDeathAnimEvent", []))
        next_track = 6
        if actor_kind == "boss" and name == "death":
            visibility = [(int(event["frame"]) - 1) / FPS for event in action["frame_events"]]
            visible_values = ["visible = true" in event["body"] and "body.visible = false" not in event["body"]
                              for event in action["frame_events"]]
            lines.append(track_value(next_track, "m_Body:visible", visibility, visible_values, "bool"))
            next_track += 1
        if method_events:
            index = next_track
            times = ", ".join(fmt_float(time) for time, _, _ in method_events)
            transitions = ", ".join("1" for _ in method_events)
            values = []
            for _, method, args in method_events:
                args_text = ", ".join(fmt_float(value) for value in args)
                values.append('{"args": [%s], "method": &"%s"}' % (args_text, method))
            lines.extend([
                f"tracks/{index}/type = \"method\"",
                f"tracks/{index}/imported = false",
                f"tracks/{index}/enabled = true",
                f'tracks/{index}/path = NodePath(".")',
                f"tracks/{index}/interp = 1",
                f"tracks/{index}/loop_wrap = true",
                f"tracks/{index}/keys = {{",
                f'"times": PackedFloat32Array({times}),',
                f'"transitions": PackedFloat32Array({transitions}),',
                f'"values": [{", ".join(values)}]',
                "}",
            ])
        lines.append("")
        animation_entries.append(f'&"{name}": SubResource("Animation_{name}")')
    # RESET is deliberately last in the resource text; its stable name is what
    # the shared enemy body states request when an action is interrupted.
    lines.extend([
        '[sub_resource type="Animation" id="Animation_RESET"]',
        "length = 0.001",
        track_value(0, "m_Body:animation", [0.0], ['&"idle"']),
        track_value(1, "m_Body:offset", [0.0], [offset], "vec2"),
        track_value(2, "m_Body:frame", [0.0], [0], "int"),
        track_value(3, f"{HIT_PATH}:disabled", [0.0], [True], "bool"),
        track_value(4, f"{HIT_PATH}:position", [0.0], [[0.0, 0.0]], "vec2"),
        track_value(5, f"{HIT_PATH}:shape", [0.0], ['SubResource("shape_empty")']),
        track_value(6, "m_Body:visible", [0.0], [True], "bool"),
        "",
        "[resource]",
        '_data = {',
        '&"RESET": SubResource("Animation_RESET"),',
        ",\n".join(animation_entries),
        "}",
        "",
    ])
    return "\n".join(lines)


def effect_animation_resource(effect, layer):
    """Build the local clock used by SkillEffectNode pooled scenes."""
    action = effect["actions"][0]
    frames = int(action["frames"])
    fade = float(effect.get("fade_seconds", 0))
    length = fade + frames / FPS
    times = [fade + index / FPS for index in range(frames)]
    if fade:
        times = [0.0] + times
    frame_values = ([0] if fade else []) + list(range(frames))
    regions = effect.get("hit_regions", [])
    shapes = {}
    shape_text = []
    for boxes in regions:
        for box in boxes:
            size = (round(box[2], 6), round(box[3], 6))
            if size not in shapes:
                key = f"flame_{len(shapes)}"
                shapes[size] = key
                shape_text.extend([f'[sub_resource type="RectangleShape2D" id="{key}"]',
                                   f"size = {vec2(size)}", ""])
    lines = [
        '[gd_resource type="AnimationLibrary" format=3]',
        "",
        *shape_text,
        '[sub_resource type="Animation" id="Animation_play"]',
        f"length = {fmt_float(length)}",
        track_value(0, "Origin/Visual/Sprite:animation", [0.0], ['&"play"']),
        track_value(1, "Origin/Visual/Sprite:offset", [0.0], [layer["sprite_offset"]], "vec2"),
        track_value(2, "Origin/Visual/Sprite:frame", times, frame_values, "int"),
    ]
    track = 3
    if regions:
        for index in range(len(regions[0])):
            path = "Origin/Visual/HitBox/CollisionShape2D" + (str(index + 1) if index else "")
            boxes = ([regions[0][index]] if fade else []) + [boxes[index] for boxes in regions]
            positions = [[box[0] + box[2] / 2, box[1] + box[3] / 2] for box in boxes]
            values = [f'SubResource("{shapes[(round(box[2], 6), round(box[3], 6))]}")' for box in boxes]
            lines.extend([track_value(track, path + ":disabled", [0.0], [False], "bool"),
                          track_value(track + 1, path + ":position", times, positions, "vec2"),
                          track_value(track + 2, path + ":shape", times, values)])
            track += 3
    else:
        lines.append(track_value(track, "Origin/Visual/HitBox/CollisionShape2D:disabled", [0.0], [True], "bool"))
        track += 1
    if fade:
        lines.append(track_value(track, "Origin/Visual/Sprite:modulate", [0, fade],
                                 ["Color(1, 1, 1, 0)", "Color(1, 1, 1, 1)"]).replace('"update": 1', '"update": 0')
                     .replace('"transitions": PackedFloat32Array(1, 1)',
                              '"transitions": PackedFloat32Array(0.5, 1)'))
    lines.extend([
        "",
        "[resource]",
        '_data = {',
        '&"play": SubResource("Animation_play")',
        "}",
        "",
    ])
    return "\n".join(lines)


def effect_scene(atlas_path, frames_path, library_path, name):
    return "\n".join([
        '[gd_scene format=3]',
        f'[ext_resource type="SpriteFrames" path={gd_string(frames_path)} id="frames"]',
        f'[ext_resource type="AnimationLibrary" path={gd_string(library_path)} id="animation"]',
        '[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Effects/SkillEffectNode.cs" id="script"]',
        '[sub_resource type="RectangleShape2D" id="rest"]',
        "size = Vector2(1, 1)",
        "",
        f'[node name="{name}" type="Node2D" node_paths=PackedStringArray("m_Player", "m_HitBox")]',
        'script = ExtResource("script")',
        'm_Player = NodePath("AnimationPlayer")',
        'm_HitBox = NodePath("Origin/Visual/HitBox")',
        '',
        '[node name="Origin" type="Node2D" parent="."]',
        '[node name="Visual" type="Node2D" parent="Origin"]',
        f'[node name="Sprite" type="AnimatedSprite2D" parent="Origin/Visual"]',
        'sprite_frames = ExtResource("frames")',
        'animation = &"play"',
        '[node name="HitBox" type="Area2D" parent="Origin/Visual"]',
        'collision_layer = 64',
        'collision_mask = 128',
        'monitorable = false',
        '[node name="CollisionShape2D" type="CollisionShape2D" parent="Origin/Visual/HitBox"]',
        'shape = SubResource("rest")',
        'disabled = true',
        *([f'[node name="CollisionShape2D{index}" type="CollisionShape2D" parent="Origin/Visual/HitBox"]\n'
           'shape = SubResource("rest")\ndisabled = true' for index in (2, 3)] if name == "peng_fire" else []),
        '[node name="AnimationPlayer" type="AnimationPlayer" parent="."]',
        'libraries/ = ExtResource("animation")',
        '',
    ])


def actor_scene(repo, actor, kind):
    """Create the entity scene while keeping all gameplay paths explicit."""
    project = repo / "Godot/GodotProject"
    name = actor["name"]
    layer = actor["layers"][0]
    if kind == "boss":
        scene_path = "res://TheGame/GameScripts/Entity/Enemies/Bosses/zm1_06_garuda_cave/PengDemonKingEntity.cs"
        body_path = f"res://TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/{name}_frames.tres"
        library_path = f"res://TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/{name}_library.tres"
        entity_path = project / "TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_entity.tscn"
        entity_id = 9
        attacks = ["attack_1", "attack_2", "attack_3", "attack_4"]
        extra = [
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/peng_fire_effect.tscn" id="6_fire"]',
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/boss_death_effect.tscn" id="7_death"]',
        ]
        script_resource = "1_peng"
        extra_properties = ['m_FireScene = ExtResource("6_fire")', 'm_DeathScene = ExtResource("7_death")']
    else:
        scene_path = "res://TheGame/GameScripts/Entity/Enemies/Monsters/Sky/flying_demon/FlyingDemonEntity.cs"
        body_path = f"res://TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/{name}_frames.tres"
        library_path = f"res://TheGame/Entitys/Animations/Enemies/Monsters/Sky/flying_demon/{name}_library.tres"
        entity_path = project / "TheGame/Entitys/Enemies/Monsters/Sky/flying_demon/flying_demon_entity.tscn"
        entity_id = 15
        attacks = ["attack_1"]
        extra = []
        script_resource = "1_flying"
        extra_properties = []
    hurt = actor["hurt_bounds"]
    hx, hy, hw, hh = map(float, hurt)
    radius = max(1.0, hw * 0.5)
    capsule_height = max(hh, radius * 2.0)
    collision_mask = 9
    detector_y = 0
    body_offset = vec2(layer["sprite_offset"])
    lines = [
        '[gd_scene format=3]',
        f'[ext_resource type="Script" path={gd_string(scene_path)} id="{script_resource}"]',
        f'[ext_resource type="SpriteFrames" path={gd_string(body_path)} id="2_frames"]',
        '[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Combat/HurtBoxNode.cs" id="3_hurt"]',
        f'[ext_resource type="AnimationLibrary" path={gd_string(library_path)} id="4_library"]',
        '[ext_resource type="PackedScene" path="res://TheGame/UIs/Battle/monster_health_bar.tscn" id="5_health"]',
        *extra,
        '',
        '[sub_resource type="CapsuleShape2D" id="body_shape"]',
        f"radius = {fmt_float(radius)}",
        f"height = {fmt_float(capsule_height)}",
        '[sub_resource type="RectangleShape2D" id="hurt_shape"]',
        f"size = {vec2([hw, hh])}",
        '[sub_resource type="RectangleShape2D" id="hit_shape"]',
        'size = Vector2(1, 1)',
        '[sub_resource type="RectangleShape2D" id="detector_shape"]',
        f'size = Vector2(600, {800 if kind == "boss" else 900})',
        '[sub_resource type="Gradient" id="health_gradient"]',
        'colors = PackedColorArray(1, 1, 1, 1, 1, 1, 1, 1)',
        '[sub_resource type="GradientTexture2D" id="health_texture"]',
        'gradient = SubResource("health_gradient")',
        'width = 50',
        'height = 5',
        '',
        f'[node name="{name[0].upper()+name[1:]}Entity" type="CharacterBody2D" node_paths=PackedStringArray("m_Detector", "m_HealthBar", "m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot")]',
        'collision_layer = 4',
        f'collision_mask = {collision_mask}',
        'floor_constant_speed = true',
        'floor_max_angle = 0.87266463',
        'floor_snap_length = 12.0',
        f'script = ExtResource("{script_resource}")',
        'm_Detector = NodePath("m_Detector")',
        f'm_EnemyEntityId = {entity_id}',
        'm_HealthBar = NodePath("m_HealthBar")',
        'm_Body = NodePath("m_Body")',
        'm_AnimPlayer = NodePath("m_AnimPlayer")',
        'm_AttackAnimations = PackedStringArray(' + ", ".join(f'"{x}"' for x in attacks) + ')',
        'm_HurtBox = NodePath("m_HurtBox")',
        'm_HitBox = NodePath("m_HitBoxRoot/m_HitBox")',
        'm_HitBoxRoot = NodePath("m_HitBoxRoot")',
        *extra_properties,
        '',
        '[node name="m_Body" type="AnimatedSprite2D" parent="."]',
        'sprite_frames = ExtResource("2_frames")',
        'animation = &"idle"',
        f'offset = {body_offset}',
        '',
        '[node name="m_BodyShape" type="CollisionShape2D" parent="."]',
        f'position = {vec2([hx + hw * 0.5, hy + hh * 0.5])}',
        'shape = SubResource("body_shape")',
        '',
        '[node name="m_HurtBox" type="Area2D" parent="." node_paths=PackedStringArray("m_Owner")]',
        'collision_layer = 32',
        'collision_mask = 0',
        'monitoring = false',
        'script = ExtResource("3_hurt")',
        'm_Owner = NodePath("..")',
        '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HurtBox"]',
        f'position = {vec2([hx + hw * 0.5, hy + hh * 0.5])}',
        'shape = SubResource("hurt_shape")',
        '',
        '[node name="m_HitBoxRoot" type="Node2D" parent="."]',
        '[node name="m_HitBox" type="Area2D" parent="m_HitBoxRoot"]',
        'collision_layer = 64',
        'collision_mask = 128',
        'monitorable = false',
        '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HitBoxRoot/m_HitBox"]',
        'shape = SubResource("hit_shape")',
        'disabled = true',
        '',
        '[node name="m_Detector" type="Area2D" parent="."]',
        'collision_layer = 4096',
        'collision_mask = 2',
        'monitorable = false',
        '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_Detector"]',
        f'position = Vector2(0, {fmt_float(detector_y)})',
        'shape = SubResource("detector_shape")',
        '',
        '[node name="m_AnimPlayer" type="AnimationPlayer" parent="."]',
        'callback_mode_method = 1',
        'libraries/ = ExtResource("4_library")',
        '',
        '[node name="m_HealthBar" parent="." instance=ExtResource("5_health")]',
        'offset_left = -24.0',
        'offset_top = -71.0',
        'offset_right = 26.0',
        'offset_bottom = -66.0',
        'texture_progress = SubResource("health_texture")',
        '',
    ]
    write_text(entity_path, "\n".join(lines), overwrite=True)


def import_actor(repo, manifest, actor, kind):
    project = repo / "Godot/GodotProject"
    actor_folder = (
        project / "TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave"
        if kind == "boss" else
        project / "TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon"
    )
    animation_folder = (
        project / "TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave"
        if kind == "boss" else
        project / "TheGame/Entitys/Animations/Enemies/Monsters/Sky/flying_demon"
    )
    layer = actor["layers"][0]
    formal_atlas = actor_folder / f"{actor['name']}_body.png"
    copy_atlas(Path(layer["output"]), formal_atlas)
    atlas_path = "res://" + formal_atlas.relative_to(project).as_posix()
    frames_path = "res://" + (actor_folder / f"{actor['name']}_frames.tres").relative_to(project).as_posix()
    library_path = "res://" + (animation_folder / f"{actor['name']}_library.tres").relative_to(project).as_posix()
    write_text(actor_folder / f"{actor['name']}_frames.tres", sprite_frames_resource(atlas_path, layer), overwrite=True)
    write_text(animation_folder / f"{actor['name']}_library.tres", animation_resource(actor, layer, kind), overwrite=True)


def import_effect(repo, manifest, effect, folder_name, scene_name):
    project = repo / "Godot/GodotProject"
    sprite_folder = project / "TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave"
    animation_folder = project / "TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave"
    entity_folder = project / "TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave"
    layer = effect["layers"][0]
    formal_atlas = sprite_folder / f"{folder_name}.png"
    copy_atlas(Path(layer["output"]), formal_atlas)
    atlas_path = "res://" + formal_atlas.relative_to(project).as_posix()
    frames_path = "res://" + (sprite_folder / f"{folder_name}_frames.tres").relative_to(project).as_posix()
    library_path = "res://" + (animation_folder / f"{folder_name}_library.tres").relative_to(project).as_posix()
    write_text(sprite_folder / f"{folder_name}_frames.tres", sprite_frames_resource(atlas_path, layer), overwrite=True)
    write_text(animation_folder / f"{folder_name}_library.tres", effect_animation_resource(effect, layer), overwrite=True)
    write_text(entity_folder / scene_name, effect_scene(atlas_path, frames_path, library_path, folder_name), overwrite=True)


def main(args):
    repo = args.repo.resolve()
    data = json.loads(args.manifest.resolve().read_text(encoding="utf-8"))
    actors = {actor["name"]: actor for actor in data["actors"]}
    import_actor(repo, data, actors["peng_demon_king"], "boss")
    import_actor(repo, data, actors["flying_demon"], "sky")
    actor_scene(repo, actors["peng_demon_king"], "boss")
    actor_scene(repo, actors["flying_demon"], "sky")
    effects = {effect["name"]: effect for effect in data["effects"]}
    import_effect(repo, data, effects["peng_fire"], "peng_fire", "peng_fire_effect.tscn")
    import_effect(repo, data, effects["boss_death"], "boss_death", "boss_death_effect.tscn")
    print("GARUDA ACTOR ART PASS: 2 actors, 2 pooled effects, formal atlases and timelines installed")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--manifest", type=Path, required=True)
    main(parser.parse_args())
