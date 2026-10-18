"""Apply only Wukong's five basic attack timelines from the official export manifest.

Base frame indices are remapped once to the compact atlas; their timing and all
other editor adjustments stay intact. Re-running this migration is idempotent.
"""
import argparse
import json
from pathlib import Path
import re

PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'


def number(value):
    """Write Godot real numbers with sufficient precision for source-frame timing."""
    return f'{value:.9f}'.rstrip('0').rstrip('.') or '0'


def track(index, path, times, values):
    """Emit a discrete value track without methods or interpolation."""
    return f'''tracks/{index}/type = "value"
tracks/{index}/imported = false
tracks/{index}/enabled = true
tracks/{index}/path = NodePath("{path}")
tracks/{index}/interp = 1
tracks/{index}/loop_wrap = true
tracks/{index}/keys = {{
"times": PackedFloat32Array({', '.join(number(t) for t in times)}),
"transitions": PackedFloat32Array({', '.join('1' for _ in times)}),
"update": 1,
"values": [{', '.join(values)}]
}}
'''


def compact_base_frames(text, mapping):
    """Remap base frame values once; never regenerate editor-authored tracks."""
    hurt = re.search(r'^\[sub_resource type="Animation" id="hurt"\]\n.*?(?=^\[|\Z)', text, re.M | re.S).group()
    body_track = re.search(r'path = NodePath\("m_Body:frame"\).*?"values": \[(.*?)\]', hurt, re.S)
    hurt_frame = int(body_track.group(1))
    if hurt_frame == mapping[74]:
        return text
    if hurt_frame != 74:
        raise ValueError('Unknown base frame layout; refusing to remap editor changes')

    def animation(match):
        block = match.group()
        if re.search(r'resource_name = "(?:attack_|skill_)', block):
            return block

        def frame_values(track_match):
            indices = [int(value.strip()) for value in track_match[2].split(',')]
            return track_match[1] + ', '.join(str(mapping[value]) for value in indices) + track_match[3]

        return re.sub(r'(path = NodePath\("(?:m_Body|m_Weapon):frame"\).*?"values": \[)(.*?)(\])', frame_values, block, flags=re.S)

    return re.sub(r'^\[sub_resource type="Animation"[^\n]*\n.*?(?=^\[|\Z)', animation, text, flags=re.M | re.S)


def reset_hit_rotation(text):
    """Every nonattack action resets the hit rectangle's animated angle."""
    path = 'm_HitBoxRoot/m_HitBox/CollisionShape2D:rotation'

    def animation(match):
        block = match.group()
        if f'NodePath("{path}")' in block:
            return block
        index = max(map(int, re.findall(r'tracks/(\d+)/type', block))) + 1
        return block.rstrip() + '\n' + track(index, path, [0], ['0']) + '\n'

    return re.sub(r'^\[sub_resource type="Animation"[^\n]*\n.*?(?=^\[|\Z)', animation, text, flags=re.M | re.S)


def method_tracks(text, name, first_index):
    """Preserve native editor-authored callbacks when replacing visual tracks."""
    animation = re.search(r'^\[sub_resource type="Animation" id="' + re.escape(name) +
                          r'"\]\n.*?(?=^\[|\Z)', text, re.M | re.S)
    if animation is None:
        return ''
    tracks = re.findall(r'^tracks/\d+/type = "method"\n.*?(?=^tracks/\d+/type|\Z)',
                        animation.group(), re.M | re.S)
    return ''.join(re.sub(r'tracks/\d+/', f'tracks/{first_index + index}/', value)
                   for index, value in enumerate(tracks))


def apply(manifest):
    """Replace only the named attack blocks; update the shared effect resource."""
    path = PROJECT / 'TheGame/Entitys/Animations/wukong_anim_library.tres'
    # The anchored boundary avoids treating key arrays as resource boundaries.
    original = path.read_text(encoding='utf-8')
    text = re.sub(r'^\[sub_resource type="(?:Animation|RectangleShape2D|ConvexPolygonShape2D)" id="(?:hitbox_)?attack_\d+(?:_\d+)?"\]\n.*?(?=^\[|\Z)', '', original, flags=re.M | re.S)
    # These unmapped legacy skills mixed guessed poses with another game's
    # timing. They have no consumer; future skills need their own full migration.
    text = re.sub(r'^\[sub_resource type="(?:Animation|RectangleShape2D)" id="(?:hitbox_)?skill_[^"]+"\]\n.*?(?=^\[|\Z)', '', text, flags=re.M | re.S)
    text = re.sub(r'^&"skill_[^"]+": SubResource\("[^"]+"\),?\n', '', text, flags=re.M)
    text = compact_base_frames(text, {int(old): int(new) for old, new in manifest['base_frame_map'].items()})
    blocks = []
    effects = ['[gd_resource type="SpriteFrames" format=3 uid="uid://boxumpj2pyv2s"]\n',
               '[ext_resource type="Texture2D" path="res://TheGame/Sprites/Effects/wukong/wukong_attack_effects.png" id="effects"]\n']
    animations = []
    for attack in manifest['attacks']:
        name, count, fps = attack['name'], int(attack['count']), float(manifest['fps'])
        times = [i/fps for i in range(count)]
        shapes, disabled, positions, rotations = [], [], [], []
        for i, rectangle in enumerate(attack['hitboxes']):
            disabled.append('true' if rectangle is None else 'false')
            positions.append('Vector2(0, 0)' if rectangle is None else 'Vector2(%s)' % ', '.join(number(n) for n in rectangle['position']))
            rotations.append('0' if rectangle is None else number(rectangle['rotation']))
            if rectangle is None:
                shapes.append('SubResource("hitbox_rest")')
            else:
                sid = f'hitbox_{name}_{i}'
                size = ', '.join(number(n) for n in rectangle['size'])
                blocks.append(f'[sub_resource type="RectangleShape2D" id="{sid}"]\nsize = Vector2({size})\n\n')
                shapes.append(f'SubResource("{sid}")')
        block = f'[sub_resource type="Animation" id="{name}"]\nresource_name = "{name}"\nlength = {number(count/fps)}\n'
        values = [str(int(attack['start'])+i) for i in range(count)]
        tracks = [('m_Body:frame', times, values), ('m_Weapon:frame', times, values),
                  ('m_EffectRoot/m_Effect:animation', [0], [f'&"{name}"']),
                  ('m_EffectRoot/m_Effect:frame', times, [str(i) for i in range(count)]),
                  ('m_EffectRoot/m_Effect:offset', [0], ['Vector2(0, 0)']),
                  ('m_EffectRoot/m_Effect:scale', [0], ['Vector2(1, 1)']),
                  ('m_HitBoxRoot/m_HitBox/CollisionShape2D:disabled', times+[count/fps], disabled+['true']),
                  ('m_HitBoxRoot/m_HitBox/CollisionShape2D:shape', times, shapes),
                  ('m_HitBoxRoot/m_HitBox/CollisionShape2D:position', times, positions),
                  ('m_HitBoxRoot/m_HitBox/CollisionShape2D:rotation', times, rotations)]
        block += ''.join(track(i, *data) for i, data in enumerate(tracks))
        block += method_tracks(original, name, len(tracks)) + '\n'
        blocks.append(block)
        frames = []
        for i in range(count):
            index = attack['effect_cells'][i]
            if index is None:
                frames.append('{"duration": 1.0, "texture": null}')
                continue
            index = int(index)
            sid = f'effect_{name}_{i}'
            effects.append(f'[sub_resource type="AtlasTexture" id="{sid}"]\natlas = ExtResource("effects")\nregion = Rect2({index%6*400}, {index//6*300}, 400, 300)\n\n')
            frames.append(f'{{"duration": 1.0, "texture": SubResource("{sid}")}}')
        animations.append(f'{{"name": &"{name}", "loop": false, "speed": {number(fps)}, "frames": [{", ".join(frames)}]}}')
    text = text.replace('[resource]', ''.join(blocks) + '[resource]', 1)
    if '&"attack_5":' not in text:
        text = text.replace('&"attack_4": SubResource("attack_4"),', '&"attack_4": SubResource("attack_4"),\n&"attack_5": SubResource("attack_5"),')
    text = reset_hit_rotation(text)
    path.write_text(text, encoding='utf-8', newline='\n')
    animations.append('{"name": &"empty", "loop": false, "speed": 1.0, "frames": [{"duration": 1.0, "texture": null}]}')
    effects.append('[resource]\nanimations = [' + ',\n'.join(animations) + ']\n')
    (PROJECT / 'TheGame/Sprites/Effects/wukong/wukong_effect_animations.tres').write_text('\n'.join(effects), encoding='utf-8', newline='\n')
    for relative in ['TheGame/Entitys/wukong_entity.tscn', 'TheGame/UIs/Widgets/hero_preview.tscn']:
        scene = PROJECT / relative
        scene_text = scene.read_text(encoding='utf-8')
        scene_text = re.sub(r'(hframes = 6\nvframes = )\d+', lambda match: match[1] + str(int(manifest['rows'])), scene_text)
        scene.write_text(scene_text, encoding='utf-8', newline='\n')
    print('Imported five complete attacks, independent effects and weapon-plus-energy rectangles')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    apply(json.loads(parser.parse_args().manifest.read_text(encoding='utf-8')))
