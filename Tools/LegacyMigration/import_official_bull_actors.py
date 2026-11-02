"""Install verified bull valley actors with independent native child timelines.

Only the new valley paths are authored. Existing hero, sea and cave animations
are not rebuilt. Re-running refuses to overwrite a changed authored file.
"""

import argparse
from copy import deepcopy
import json
from pathlib import Path
import xml.etree.ElementTree as ET

from export_official_wukong import timeline_frames
from prepare_official_garuda_level import _probe_reader
from import_garuda_actor_assets import (
    animation_resource, copy_atlas, fmt_float, sprite_frames_resource,
    track_value, vec2, write_text,
)


PROJECT = Path(__file__).resolve().parents[2] / "Godot/GodotProject"
BOSS = "Bosses/zm1_10_bull_demon_valley"
GUARD = "Monsters/Ground/bull_guard"
ACTORS = {
    "bull_demon_beast": (22, "BullDemonBeastEntity", BOSS, ["charge"]),
    "bull_demon_king": (23, "BullDemonKingEntity", BOSS, [f"attack_{i}" for i in range(1, 6)]),
    "bull_demon_heart": (24, "BullDemonHeartEntity", BOSS, ["idle", "idle"]),
    "bull_guard": (25, "BullGuardEntity", GUARD, ["attack_1"]),
}


def uri(path):
    return "res://" + path.relative_to(PROJECT).as_posix()


def install_layer(record, relative, suffix=""):
    """Keep the exporter registration, transparent margins and source identity."""
    layer = record["layers"][0]
    directory = PROJECT / "TheGame/Sprites/Characters/Enemies" / relative
    atlas = directory / (record["name"] + suffix + ".png")
    frames = directory / (record["name"] + ("_official" if suffix else "") + "_frames.tres")
    copy_atlas(Path(layer["output"]), atlas)
    write_text(frames, sprite_frames_resource(uri(atlas), layer))
    return frames


def swallow_frames(xml):
    """Map Timeline_55's actual five pulses through its placement in hit5."""
    tags = ET.parse(xml).getroot().find("tags")
    action = next(tag for tag in tags if tag.get("spriteId") == "922")
    placed_at = [index + 1 for index, display in enumerate(timeline_frames(action))
                 if any(node.get("characterId") == "918" for node in display.values())]
    if not placed_at:
        raise ValueError("Missing official swallowing child 918 in action 922")
    start = min(placed_at)
    result = [start + frame - 1 for frame in (5, 21, 37, 54, 71)]
    if result[-1] >= 130:
        raise ValueError("Swallowing pulses exceed source release frame")
    return result


def actor_library(actor, pulses):
    """Map native methods explicitly; generic frame parsing retains refreshes."""
    data = deepcopy(actor)
    for action in data["actions"]:
        action["native_method_events"] = []
        name = action["name"]
        if actor["name"] == "bull_demon_beast" and name == "death":
            action["native_method_events"].append({"frame": 25, "method": "CompleteTransformAnimEvent"})
        if actor["name"] != "bull_demon_king":
            continue
        if name in ("attack_2", "attack_3", "attack_4"):
            action["native_method_events"].append({"frame": 1, "method": "BeginInvulnerabilityAnimEvent",
                                                   "args": [{"attack_2": 60, "attack_3": 67, "attack_4": 54}[name] / 24]})
        if name == "attack_2":
            action["native_method_events"].append({"frame": 30, "method": "BeginDashAnimEvent"})
        if name == "attack_5":
            action["native_method_events"] = [{"frame": 28, "method": "CaptureHeroAnimEvent"}]
            action["native_method_events"] += [{"frame": frame, "method": "DrainCaptiveAnimEvent"} for frame in pulses]
    text = animation_resource(data, data["layers"][0], "monster" if actor["name"] == "bull_guard" else "boss")
    if actor["name"] == "bull_demon_beast":
        text = text.replace('[sub_resource type="Animation" id="Animation_charge"]',
                            '[sub_resource type="Animation" id="Animation_charge"]\nloop_mode = 1')
    return text


def actor_scene(actor, frames, library):
    """Author the standard enemy slots plus only this actor's actual dependencies."""
    name = actor["name"]
    identity, class_name, relative, attacks = ACTORS[name]
    heart = name == "bull_demon_heart"
    hurt = actor["hurt_bounds"]
    center = [hurt[0] + hurt[2] / 2, hurt[1] + hurt[3] / 2]
    bindings = ["m_Detector", "m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot"]
    if name == "bull_guard":
        bindings.append("m_HealthBar")
    extra = (["m_Heart", "m_HeartFade", "m_Presentation", "m_LeftHand", "m_RightHand"] if heart else [])
    lines = ['[gd_scene format=3]',
             f'[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Enemies/{relative}/{class_name}.cs" id="script"]',
             f'[ext_resource type="SpriteFrames" path="{uri(frames)}" id="frames"]',
             f'[ext_resource type="AnimationLibrary" path="{uri(library)}" id="library"]',
             '[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Combat/HurtBoxNode.cs" id="hurt"]']
    if name == "bull_guard":
        lines.append('[ext_resource type="PackedScene" path="res://TheGame/UIs/Battle/monster_health_bar.tscn" id="health"]')
    if name == "bull_demon_king":
        lines.append('[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/boss_death_effect.tscn" id="death"]')
    if heart:
        for part in ("heart", "hands", "background"):
            lines.append(f'[ext_resource type="SpriteFrames" path="res://TheGame/Sprites/Characters/Enemies/{BOSS}/bull_{part}_frames.tres" id="{part}"]')
        for key, script in (("hand_script", "BullDemonHandNode"),):
            lines.append(f'[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Enemies/{BOSS}/{script}.cs" id="{key}"]')
        for key, filename in (("fade", "bull_heart_fade_library"), ("presentation", "bull_presentation_library")):
            lines.append(f'[ext_resource type="AnimationLibrary" path="res://TheGame/Entitys/Animations/Enemies/{BOSS}/{filename}.tres" id="{key}"]')
        lines.append(f'[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/{BOSS}/bull_fire_effect.tscn" id="fire"]')
        lines += ['[sub_resource type="RectangleShape2D" id="hand_shape"]', 'size = Vector2(280, 153)']
    if name == "bull_guard":
        detection_half_height = 110
    elif name == "bull_demon_beast":
        detection_half_height = 450
    else:
        detection_half_height = 400
    lines += ['[sub_resource type="CapsuleShape2D" id="body_shape"]',
              f'radius = {fmt_float(min(hurt[2], hurt[3]) / 2)}', f'height = {fmt_float(max(hurt[2], hurt[3]))}',
              '[sub_resource type="RectangleShape2D" id="hurt_shape"]', f'size = {vec2(hurt[2:])}',
              '[sub_resource type="RectangleShape2D" id="empty"]', 'size = Vector2(1, 1)',
              '[sub_resource type="RectangleShape2D" id="detector"]',
              f'size = Vector2(600, {2 * detection_half_height})',
              f'[node name="{class_name}" type="CharacterBody2D" node_paths=PackedStringArray(' + ', '.join('"' + key + '"' for key in bindings + extra) + ')]',
              'collision_layer = 4', f'collision_mask = {0 if heart else 9}', 'floor_constant_speed = true',
              'floor_max_angle = 0.87266463', 'floor_snap_length = 12.0', 'script = ExtResource("script")',
              f'm_EnemyEntityId = {identity}', 'm_Detector = NodePath("m_Detector")', 'm_Body = NodePath("m_Body")',
              'm_AnimPlayer = NodePath("m_AnimPlayer")', 'm_HurtBox = NodePath("m_HurtBox")',
              'm_HitBox = NodePath("m_HitBoxRoot/m_HitBox")', 'm_HitBoxRoot = NodePath("m_HitBoxRoot")',
              'm_AttackAnimations = PackedStringArray(' + ', '.join('"' + value + '"' for value in attacks) + ')']
    if name == "bull_demon_king":
        lines.append('m_DeathScene = ExtResource("death")')
    if name == "bull_guard":
        lines.append('m_HealthBar = NodePath("m_HealthBar")')
    if heart:
        lines += [f'{key} = NodePath("{key}")' for key in extra]
        lines.append('m_FireScene = ExtResource("fire")')
    lines += ['[node name="m_Body" type="AnimatedSprite2D" parent="."]',
              'sprite_frames = ExtResource("frames")', 'animation = &"idle"',
              f'offset = {vec2(actor["layers"][0]["sprite_offset"])}',
              '[node name="m_BodyShape" type="CollisionShape2D" parent="."]', 'shape = SubResource("body_shape")',
              f'position = {vec2(center)}', f'rotation = {1.57079632679 if hurt[2] > hurt[3] else 0}',
              '[node name="m_HurtBox" type="Area2D" parent="." node_paths=PackedStringArray("m_Owner")]',
              'collision_layer = 32', 'collision_mask = 0', 'monitoring = false', 'script = ExtResource("hurt")', 'm_Owner = NodePath("..")',
              '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HurtBox"]',
              'shape = SubResource("hurt_shape")', f'position = {vec2(center)}',
              '[node name="m_HitBoxRoot" type="Node2D" parent="."]',
              '[node name="m_HitBox" type="Area2D" parent="m_HitBoxRoot"]',
              'collision_layer = 64', 'collision_mask = 128', 'monitorable = false',
              '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HitBoxRoot/m_HitBox"]',
              'shape = SubResource("empty")', 'disabled = true',
              '[node name="m_Detector" type="Area2D" parent="."]', 'collision_layer = 4096', 'collision_mask = 2', 'monitorable = false',
              '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_Detector"]', 'shape = SubResource("detector")',
              '[node name="m_AnimPlayer" type="AnimationPlayer" parent="."]', 'libraries/ = ExtResource("library")']
    if heart:
        lines += heart_nodes()
    if name == "bull_guard":
        lines += ['[node name="m_HealthBar" parent="." instance=ExtResource("health")]',
                  f'offset_left = {fmt_float(center[0] - 25)}', f'offset_right = {fmt_float(center[0] + 25)}',
                  f'offset_top = {fmt_float(hurt[1] - 13)}', f'offset_bottom = {fmt_float(hurt[1] - 8)}']
    lines.append('')
    return '\n'.join(lines)


def heart_nodes():
    """Keep the independently fading heart and hands under their real owner."""
    lines = ['[node name="m_Background" type="AnimatedSprite2D" parent="."]',
             'z_index = -1', 'sprite_frames = ExtResource("background")', 'animation = &"play"', 'offset = Vector2(2.5, 2)',
             '[node name="m_Heart" type="AnimatedSprite2D" parent="."]',
             'sprite_frames = ExtResource("heart")', 'animation = &"play"', 'offset = Vector2(18.5, 118.5)',
             '[node name="m_HeartFade" type="AnimationPlayer" parent="."]', 'libraries/ = ExtResource("fade")',
             '[node name="m_Presentation" type="AnimationPlayer" parent="."]', 'libraries/ = ExtResource("presentation")']
    for name, mirrored in (("m_LeftHand", False), ("m_RightHand", True)):
        lines += [f'[node name="{name}" type="Node2D" parent="." node_paths=PackedStringArray("m_HitBox")]',
                  'visible = false', 'script = ExtResource("hand_script")', 'm_HitBox = NodePath("HitBox")',
                  f'[node name="Visual" type="AnimatedSprite2D" parent="{name}"]',
                  'sprite_frames = ExtResource("hands")', 'animation = &"play"', 'offset = Vector2(0, -0.5)',
                  f'flip_h = {str(mirrored).lower()}', f'[node name="HitBox" type="Area2D" parent="{name}"]',
                  'collision_layer = 64', 'collision_mask = 128', 'monitorable = false',
                  f'[node name="CollisionShape2D" type="CollisionShape2D" parent="{name}/HitBox"]',
                  'shape = SubResource("hand_shape")']
    return lines


def fade_libraries():
    """TweenMax default quad ease-out is an exact native Bezier alpha track."""
    def alpha_track(index, path, start, finish):
        return '\n'.join([f'tracks/{index}/type = "bezier"', f'tracks/{index}/path = NodePath("{path}")',
            f'tracks/{index}/keys = {{"times": PackedFloat32Array(0, 2), "points": PackedFloat32Array({start}, 0, 0, 0.6666667, {(finish-start)*2/3}, {finish}, -0.6666667, 0, 0, 0)}}'])
    fade = ['[gd_resource type="AnimationLibrary" format=3]']
    for name, start, finish in (("show", 0, 1), ("hide", 1, 0)):
        fade += [f'[sub_resource type="Animation" id="{name}"]', 'length = 2.0', alpha_track(0, 'm_Heart:modulate:a', start, finish)]
    fade += ['[resource]', '_data = {&"show": SubResource("show"), &"hide": SubResource("hide")}']
    presentation = ['[gd_resource type="AnimationLibrary" format=3]',
                    '[sub_resource type="Animation" id="RESET"]', 'length = 0.001']
    for index, node in enumerate(("m_Body", "m_LeftHand", "m_RightHand", "m_Background", "m_Heart")):
        presentation.append(track_value(index, node + ':modulate:a', [0], [0 if node in ('m_Background', 'm_Heart') else 1]))
    presentation += ['[sub_resource type="Animation" id="intro"]', 'length = 2.0', alpha_track(0, 'm_Background:modulate:a', 0, 1),
                     '[sub_resource type="Animation" id="death"]', 'length = 2.0']
    for index, node in enumerate(("m_Body", "m_LeftHand", "m_RightHand", "m_Background", "m_Heart")):
        presentation.append(alpha_track(index, node + ':modulate:a', 1, 0))
    presentation += ['[resource]', '_data = {&"RESET": SubResource("RESET"), &"intro": SubResource("intro"), &"death": SubResource("death")}']
    root = PROJECT / 'TheGame/Entitys/Animations/Enemies' / BOSS
    write_text(root / 'bull_heart_fade_library.tres', '\n'.join(fade) + '\n')
    write_text(root / 'bull_presentation_library.tres', '\n'.join(presentation) + '\n')


def fire_hit_bounds(xml):
    """Fire.step checks frames 25..36, then destroys after the last check."""
    tags = ET.parse(xml).getroot().find('tags')
    _, bounds = _probe_reader(tags)
    return [bounds(755, frame, [1, 0, 0, 1, 0, 0]) if 24 <= frame <= 35 else None
            for frame in range(36)]


def fire_scene(effect, frames, hit_bounds):
    """Keep the warning harmless and follow the six actual erupting-flame sizes."""
    root = PROJECT / 'TheGame/Entitys/Animations/Enemies' / BOSS
    library = root / 'bull_fire_library.tres'
    bounds = hit_bounds[24]
    stages = [(index / 24, value) for index, value in enumerate(hit_bounds) if value is not None
              and (index == 24 or value != hit_bounds[index - 1])]
    times = [0] + [time for time, _ in stages[1:]]
    positions = [[value[0] + value[2] / 2, value[1] + value[3] / 2] for _, value in stages]
    sizes = [value[2:] for _, value in stages]
    shape_lines = []
    for index, size in enumerate(sizes):
        shape_lines += [f'[sub_resource type="RectangleShape2D" id="flame_{index}"]', f'size = {vec2(size)}']
    shape_values = [f'SubResource("flame_{index}")' for index in range(len(sizes))]
    lines = ['[gd_resource type="AnimationLibrary" format=3]', *shape_lines,
             '[sub_resource type="Animation" id="play"]', 'length = 1.5',
             track_value(0, 'Sprite:frame', [i / 24 for i in range(36)], list(range(36)), 'int'),
             track_value(1, 'HitBox/Shape:disabled', [0, 1], [True, False], 'bool'),
             'tracks/2/type = "method"', 'tracks/2/path = NodePath(".")',
             'tracks/2/keys = {"times": PackedFloat32Array(1.458333333), "transitions": PackedFloat32Array(1),',
             '"values": [{"args": [], "method": &"FinishAnimationAnimEvent"}]}',
             track_value(3, 'HitBox/Shape:position', times, positions, 'vec2'),
             track_value(4, 'HitBox/Shape:shape', times, shape_values),
             '[resource]', '_data = {&"play": SubResource("play")}']
    write_text(library, '\n'.join(lines) + '\n')
    scene = PROJECT / 'TheGame/Entitys/Enemies' / BOSS / 'bull_fire_effect.tscn'
    write_text(scene, '\n'.join(['[gd_scene format=3]',
        f'[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Enemies/{BOSS}/BullDemonFireNode.cs" id="script"]',
        f'[ext_resource type="SpriteFrames" path="{uri(frames)}" id="frames"]',
        f'[ext_resource type="AnimationLibrary" path="{uri(library)}" id="library"]',
        '[sub_resource type="RectangleShape2D" id="shape"]', f'size = {vec2(bounds[2:])}',
        '[node name="BullDemonFire" type="Node2D" node_paths=PackedStringArray("m_Player", "m_HitBox", "m_Shape")]',
        'script = ExtResource("script")', 'm_Player = NodePath("AnimationPlayer")', 'm_HitBox = NodePath("HitBox")', 'm_Shape = NodePath("HitBox/Shape")',
        '[node name="Sprite" type="AnimatedSprite2D" parent="."]', 'sprite_frames = ExtResource("frames")', 'animation = &"play"',
        f'offset = {vec2(effect["layers"][0]["sprite_offset"])}',
        '[node name="HitBox" type="Area2D" parent="."]', 'collision_layer = 64', 'collision_mask = 128', 'monitorable = false',
        '[node name="Shape" type="CollisionShape2D" parent="HitBox"]', 'shape = SubResource("shape")', 'disabled = true',
        f'position = {vec2([bounds[0]+bounds[2]/2, bounds[1]+bounds[3]/2])}',
        '[node name="AnimationPlayer" type="AnimationPlayer" parent="."]', 'libraries/ = ExtResource("library")', '']))


def main(args):
    data = json.loads(args.bosses.read_text(encoding='utf-8'))
    guards = json.loads(args.monsters.read_text(encoding='utf-8'))
    if data['source_sha256'] != '42a301a5ae4222aec54dfdc4c14efced48b9ad4f979902471011ff226d8cd09b':
        raise ValueError('Unexpected official bull pack')
    pulses = swallow_frames(args.xml)
    for effect in data['effects']:
        layer_frames = install_layer(effect, BOSS)
        if effect['name'] == 'bull_fire':
            fire_scene(effect, layer_frames, fire_hit_bounds(args.xml))
    fade_libraries()
    for actor in data['actors'] + guards['actors']:
        relative = ACTORS[actor['name']][2]
        frames = install_layer(actor, relative, '_official_body')
        library = PROJECT / 'TheGame/Entitys/Animations/Enemies' / relative / (actor['name'] + '_library.tres')
        write_text(library, actor_library(actor, pulses))
        scene = PROJECT / 'TheGame/Entitys/Enemies' / relative / (actor['name'] + '_entity.tscn')
        write_text(scene, actor_scene(actor, frames, library))
    print(f'BULL NATIVE AUTHOR PASS: four actors, independent heart/hands/fire/backdrop; swallowing source pulses={pulses}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('bosses', 'monsters', 'xml'):
        parser.add_argument('--' + name, type=Path, required=True)
    main(parser.parse_args())
