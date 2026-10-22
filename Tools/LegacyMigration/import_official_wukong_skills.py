"""One-time import of verified skill renders and their initial animation geometry.

Run export_official_wukong_skills.py and import_official_wukong_skills.gd first.
The original 87 body/weapon cells are copied verbatim; skill poses append to the
same 200-pixel grid. No sprite is recentered by its per-frame visible bounds.
The installed animations now have authored methods and phase markers; this tool
refuses to overwrite them. Maintain those native resources in the Godot editor.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re

import numpy as np
from PIL import Image

from import_official_wukong_attacks import number, track

PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
HERO = PROJECT / 'TheGame/Sprites/Characters/Heroes/wukong'
EFFECT = PROJECT / 'TheGame/Sprites/Effects/wukong/skills'
ANIMATION = PROJECT / 'TheGame/Entitys/Animations/Heroes/Wukong'
SCENE = PROJECT / 'TheGame/Entitys/Heroes/Wukong'


def registered_native(source, bounds, transform):
    """Apply the parent matrix to FFDec's exact filtered export rectangle."""
    image = Image.open(source).convert('RGBA')
    a, b, c, d, tx, ty = transform
    inverse = np.linalg.inv(np.array([[a, c], [b, d]], dtype=float))
    origin = inverse @ np.array([-400-tx, -428-ty]) - np.array(bounds[:2])
    coefficients = (inverse[0, 0], inverse[0, 1], origin[0],
                    inverse[1, 0], inverse[1, 1], origin[1])
    return image.transform((800, 800), Image.Transform.AFFINE, coefficients, Image.Resampling.BICUBIC)


def points(image, alpha):
    """Return visible pixel centers in the registered local coordinate system."""
    pixels = np.asarray(image)
    ys, xs = np.where(pixels[:, :, 3] >= round(alpha * 255))
    return np.column_stack((xs + .5 - image.width / 2, ys + .5 - image.height / 2))


def fit(images):
    """Fit the smaller of axis-aligned and principal-axis padded rectangles."""
    sets = [points(image, alpha) for image, alpha in images]
    sets = [array for array in sets if len(array)]
    if not sets:
        return None
    pixels = np.concatenate(sets)
    covariance = np.cov(pixels, rowvar=False)
    angle = .5 * math.atan2(2 * covariance[0, 1], covariance[0, 0] - covariance[1, 1])
    rectangles = []
    for rotation in (0, angle):
        axes = np.array([[math.cos(rotation), -math.sin(rotation)],
                         [math.sin(rotation), math.cos(rotation)]])
        projected = pixels @ axes
        low, high = projected.min(axis=0), projected.max(axis=0)
        center = axes @ ((low + high) / 2)
        size = high - low + 6
        size[1] = max(size[1], 14)
        rectangles.append({'position': center.tolist(), 'size': size.tolist(), 'rotation': rotation})
    return min(rectangles, key=lambda rectangle: math.prod(rectangle['size']))


def animation(name, count, body_frames=None, hitboxes=None, frames_path='Sprite:frame'):
    """Build discrete visual/shape tracks; gameplay events belong in config."""
    times = [i/24 for i in range(count)]
    blocks, tracks = [], []
    if body_frames is not None:
        values = list(map(str, body_frames))
        tracks = [('m_Body:frame', times, values), ('m_Weapon:frame', times, values),
            ('m_EffectRoot/m_Effect:animation', [0], [f'&"{name}"']),
            ('m_EffectRoot/m_Effect:frame', times, list(map(str, range(count)))),
            ('m_EffectRoot/m_Effect:offset', [0], ['Vector2(0, 0)']),
            ('m_EffectRoot/m_Effect:scale', [0], ['Vector2(1, 1)'])]
        hit_path = 'm_HitBoxRoot/m_HitBox/CollisionShape2D'
    else:
        tracks = [(frames_path, times, list(map(str, range(count))))]
        hit_path = 'HitBox/CollisionShape2D'
    shapes, positions, rotations, disabled = [], [], [], []
    for index, rectangle in enumerate(hitboxes or [None]*count):
        disabled.append('true' if rectangle is None else 'false')
        positions.append('Vector2(0, 0)' if rectangle is None else 'Vector2(%s)' % ', '.join(map(number, rectangle['position'])))
        rotations.append('0' if rectangle is None else number(rectangle['rotation']))
        if rectangle is None:
            shapes.append('SubResource("rest")')
        else:
            sid = f'{name}_hit_{index}'
            blocks.append(f'[sub_resource type="RectangleShape2D" id="{sid}"]\nsize = Vector2({", ".join(map(number, rectangle["size"]))})\n\n')
            shapes.append(f'SubResource("{sid}")')
    tracks += [(hit_path + ':disabled', times+[count/24], disabled+['true']),
               (hit_path + ':shape', times, shapes), (hit_path + ':position', times, positions),
               (hit_path + ':rotation', times, rotations)]
    blocks.append(f'[sub_resource type="Animation" id="{name}"]\nresource_name = "{name}"\nlength = {number(count/24)}\n' +
                  ''.join(track(index, *data) for index, data in enumerate(tracks)) + '\n')
    return ''.join(blocks)


def sprite_frames(path, texture_path, animations, cell_size, columns):
    """Keep blank source frames in the timeline without wasting atlas cells."""
    width, height = cell_size
    blocks = ['[gd_resource type="SpriteFrames" format=3]\n',
              f'[ext_resource type="Texture2D" path="{texture_path}" id="atlas"]\n']
    entries = []
    for name, cells in animations:
        frames = []
        for index, cell in enumerate(cells):
            if cell is None:
                frames.append('{"duration": 1.0, "texture": null}')
                continue
            sid = f'{name}_{index}'
            blocks.append(f'[sub_resource type="AtlasTexture" id="{sid}"]\natlas = ExtResource("atlas")\nregion = Rect2({cell%columns*width}, {cell//columns*height}, {width}, {height})\n\n')
            frames.append(f'{{"duration": 1.0, "texture": SubResource("{sid}")}}')
        entries.append(f'{{"name": &"{name}", "loop": false, "speed": 24.0, "frames": [{", ".join(frames)}]}}')
    blocks.append('[resource]\nanimations = [' + ',\n'.join(entries) + ']\n')
    path.write_text('\n'.join(blocks), encoding='utf-8')


def apply(path):
    """Append skills, preserve basic animation edits, and prepare detached FX."""
    library = ANIMATION / 'wukong_skill_library.tres'
    if library.exists() and 'type = "method"' in library.read_text(encoding='utf-8'):
        raise ValueError('正式技能动画已含人工编排的方法轨道与阶段标记；本一次性导入工具不得重写，请在 Godot 编辑器维护。')
    manifest = json.loads(path.read_text(encoding='utf-8'))
    manifest['source_count'] = 87 + sum(int(action['count']) for action in manifest['skills'])
    for action in manifest['skills']:
        action['count'], action['start'] = int(action['count']), int(action['start'])
        action['sprite'] = int(action['sprite'])
    for effect in manifest['detached']:
        effect['count'] = int(effect['count'])
        effect['sprite'] = int(effect['sprite'])
    if manifest['source_sha256'] != '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7':
        raise ValueError('Unexpected source revision')
    if manifest['audit']['body'] != [-91, -44, 88, 96]:
        raise ValueError('Recheck body/weapon bounds before changing the shared canvas')
    work = path.parent
    for directory in (EFFECT, ANIMATION, SCENE):
        directory.mkdir(parents=True, exist_ok=True)
    layers = [('body_bare', 'unarmed', 'body'), ('body_pilgrim', 'armored', 'body'),
              ('weapon_default', 'unarmed', 'weapon'), ('weapon_pilgrim', 'armed', 'weapon')]
    crops, cell_keys, mapping, unique_keys = {}, {}, {}, []
    for key in manifest['poses']:
        images = []
        for name, source, layer in layers:
            image = Image.open(work / 'raster' / source / f'{key}_{layer}.png').convert('RGBA')
            crop = image.crop((300, 300, 500, 500))
            if sum(image.getchannel('A').getdata()) != sum(crop.getchannel('A').getdata()):
                raise ValueError(f'Skill {key} {name} would be clipped')
            images.append(crop)
        signature = hashlib.sha256(b''.join(image.tobytes() for image in images)).digest()
        if signature not in cell_keys:
            cell_keys[signature] = 87+len(unique_keys)
            unique_keys.append(key)
            crops[key] = images
        mapping[key] = cell_keys[signature]
    packed_count = 87+len(unique_keys)
    rows = math.ceil(packed_count/6)
    base_hashes, weapons = {}, {}
    for layer_index, (name, source, layer) in enumerate(layers):
        destination = HERO / f'wukong_{name}.png'
        old = Image.open(destination).convert('RGBA')
        atlas = Image.new('RGBA', (1200, rows*200))
        old_pixels, new_pixels = [], []
        for index in range(87):
            x, y = index%6*200, index//6*200
            crop = old.crop((x, y, x+200, y+200))
            atlas.paste(crop, (x, y))
            old_pixels.append(crop.tobytes())
        for key in unique_keys:
            index = mapping[key]
            atlas.paste(crops[key][layer_index], (index%6*200, index//6*200))
        for index in range(87):
            x, y = index%6*200, index//6*200
            new_pixels.append(atlas.crop((x, y, x+200, y+200)).tobytes())
        if old_pixels != new_pixels:
            raise ValueError('Existing pose pixels changed')
        base_hashes[name] = hashlib.sha256(b''.join(new_pixels)).hexdigest()
        atlas.save(destination)
        if layer == 'weapon':
            weapons[name] = atlas
    effect_images, effect_animations = [], []
    for action in manifest['skills']:
        cells, hitboxes = [], []
        action['frame_cells'] = [mapping[str(action['start']+frame)] for frame in range(action['count'])]
        sid = str(action['sprite'])
        for frame in range(action['count']):
            key = str(action['start']+frame)
            effect = Image.new('RGBA', (800, 800))
            if sid in manifest['inline']:
                effect = registered_native(Path(manifest['inline'][sid]) / f'{frame+1}.png',
                    manifest['inline_bounds'][sid], manifest['transforms'][sid])
            crop = effect.crop((0, 200, 800, 600))
            if sum(effect.getchannel('A').getdata()) != sum(crop.getchannel('A').getdata()):
                raise ValueError(f'Inline effect {action["name"]}/{frame+1} is clipped')
            if crop.getbbox():
                cells.append(len(effect_images))
                effect_images.append(crop)
            else:
                cells.append(None)
            rectangle = None
            if action['hit_windows'][frame]:
                probe = work / 'raster/unarmed' / f'{key}_probe.png'
                if probe.exists():
                    rectangle = fit([(Image.open(probe).convert('RGBA'), .25)])
                elif crop.getbbox():
                    cell = mapping[key]
                    weapon_crops = [atlas.crop((cell%6*200, cell//6*200, cell%6*200+200, cell//6*200+200)) for atlas in weapons.values()]
                    rectangle = fit([(image, .25) for image in weapon_crops] + [(crop, .1)])
            hitboxes.append(rectangle)
        action['hitboxes'], action['effect_cells'] = hitboxes, cells
        effect_animations.append((action['name'], cells))
    inline_atlas = Image.new('RGBA', (3200, math.ceil(len(effect_images)/4)*400))
    for index, image in enumerate(effect_images):
        inline_atlas.paste(image, (index%4*800, index//4*400))
    inline_path = EFFECT / 'wukong_skill_inline_effects.png'
    inline_atlas.save(inline_path)
    inline_frames = work / 'inline_frames.tres'
    sprite_frames(inline_frames, 'res://' + inline_path.relative_to(PROJECT).as_posix(), effect_animations, (800, 400), 4)
    # Merge skill frame definitions into the existing shared AnimatedSprite2D.
    # Keep basic attack frames, AtlasTexture rectangles and null frames intact.
    old_frames = PROJECT / 'TheGame/Sprites/Effects/wukong/wukong_effect_animations.tres'
    old_text = old_frames.read_text(encoding='utf-8')
    text = inline_frames.read_text(encoding='utf-8')
    skill_ext = re.search(r'^\[ext_resource[^\n]+', text, re.M)[0].replace('id="atlas"', 'id="skills"')
    resource_offset = old_text.index('[sub_resource')
    if 'id="skills"' not in old_text:
        old_text = old_text[:resource_offset] + skill_ext + '\n\n' + old_text[resource_offset:]
    old_text = re.sub(r'^\[sub_resource type="AtlasTexture" id="(?:rising_dragon|flame_thrust|blazing_flash|flame_storm|fire_demon_slash)_\d+"\]\n.*?(?=^\[|\Z)', '', old_text, flags=re.M|re.S)
    # Split the resource array at top-level dictionaries, not inner frame maps.
    entries_start = old_text.index('animations = [') + len('animations = [')
    old_entries = old_text[entries_start:].rstrip().removesuffix(']')
    depth, start, entries = 0, None, []
    for index, char in enumerate(old_entries):
        if char == '{':
            if depth == 0:
                start = index
            depth += 1
        elif char == '}':
            depth -= 1
            if depth == 0 and start is not None:
                entry = old_entries[start:index+1]
                if not any(f'"name": &"{name}"' in entry for name, _ in effect_animations):
                    entries.append(entry)
    additions = text[text.index('[sub_resource'):text.index('[resource]')].replace('ExtResource("atlas")', 'ExtResource("skills")')
    new_entries = text.split('animations = [', 1)[1].rstrip().removesuffix(']')
    old_text = old_text[:old_text.index('[resource]')] + additions + '[resource]\nanimations = [' + ',\n'.join(entries) + ',\n' + new_entries + ']\n'
    old_frames.write_text(old_text, encoding='utf-8')
    blocks = ['[gd_resource type="AnimationLibrary" format=3]\n', '[sub_resource type="RectangleShape2D" id="rest"]\nsize = Vector2(1, 1)\n\n']
    for action in manifest['skills']:
        blocks.append(animation(action['name'], action['count'], action['frame_cells'], action['hitboxes']))
    blocks.append('[resource]\n_data = {\n' + ',\n'.join(f'&"{action["name"]}": SubResource("{action["name"]}")' for action in manifest['skills']) + '\n}\n')
    library = ANIMATION / 'wukong_skill_library.tres'
    library.write_text(''.join(blocks), encoding='utf-8')
    for relative in ('TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn', 'TheGame/UIs/Inventory/hero_preview.tscn'):
        scene = PROJECT / relative
        text = re.sub(r'(hframes = 6\nvframes = )\d+', lambda m: m[1]+str(rows), scene.read_text(encoding='utf-8'))
        if relative.endswith('wukong_entity.tscn'):
            if 'id="skills"' not in text:
                text = text.replace('[sub_resource', '[ext_resource type="AnimationLibrary" path="res://TheGame/Entitys/Animations/Heroes/Wukong/wukong_skill_library.tres" id="skills"]\n\n[sub_resource', 1)
            if 'libraries/skills' not in text:
                text = text.replace('libraries/ = ExtResource("5_lib")', 'libraries/ = ExtResource("5_lib")\nlibraries/skills = ExtResource("skills")')
        scene.write_text(text, encoding='utf-8')
    for effect in manifest['detached']:
        name, count, sid = effect['name'], effect['count'], str(effect['sprite'])
        bounds = manifest['detached_bounds'][sid]
        sample = Image.open(Path(effect['input']) / '1.png').convert('RGBA')
        width, height = sample.size
        columns = min(6, count)
        cells, hitboxes, visible_images = [], [], []
        offset = [width/2+bounds[0], height/2+bounds[1]+28]
        for frame in range(count):
            image = Image.open(Path(effect['input']) / f'{frame+1}.png').convert('RGBA')
            cells.append(len(visible_images) if image.getbbox() else None)
            if image.getbbox():
                visible_images.append(image)
            rectangle = fit([(image, .1)]) if name in ('fire_demon_combo', 'fire_demon_descent', 'fire_demon_landing') else None
            if rectangle is not None:
                rectangle['position'] = [rectangle['position'][i]+offset[i] for i in range(2)]
            hitboxes.append(rectangle)
        atlas = Image.new('RGBA', (columns*width, math.ceil(len(visible_images)/columns)*height))
        for index, image in enumerate(visible_images):
            atlas.paste(image, (index%columns*width, index//columns*height))
        atlas_path = EFFECT / f'wukong_{name}_atlas.png'
        atlas.save(atlas_path)
        frames = EFFECT / f'wukong_{name}_frames.tres'
        sprite_frames(frames, 'res://' + atlas_path.relative_to(PROJECT).as_posix(), [('play', cells)], (width, height), columns)
        if name == 'fire_demon_prelude':
            # Pure animation lives on Wukong; no empty attack area or pooled scene.
            effect['hitboxes'], effect['offset'], effect['effect_cells'] = hitboxes, offset, cells
            continue
        effect_library = ANIMATION / f'wukong_{name}_library.tres'
        effect_library.write_text('[gd_resource type="AnimationLibrary" format=3]\n\n[sub_resource type="RectangleShape2D" id="rest"]\nsize = Vector2(1, 1)\n\n' + animation('play', count, hitboxes=hitboxes) + '[resource]\n_data = {&"play": SubResource("play")}\n', encoding='utf-8')
        effect_scene = SCENE / f'wukong_{name}_effect.tscn'
        effect_scene.write_text(f'''[gd_scene format=3]

[ext_resource type="SpriteFrames" path="res://{frames.relative_to(PROJECT).as_posix()}" id="frames"]
[ext_resource type="AnimationLibrary" path="res://{effect_library.relative_to(PROJECT).as_posix()}" id="animation"]

[sub_resource type="RectangleShape2D" id="rest"]
size = Vector2(1, 1)

[node name="{name.title().replace('_', '')}" type="Node2D"]

[node name="Sprite" type="AnimatedSprite2D" parent="."]
sprite_frames = ExtResource("frames")
animation = &"play"
offset = Vector2({', '.join(map(number, offset))})

[node name="HitBox" type="Area2D" parent="."]
collision_layer = 16
collision_mask = 32
monitorable = false

[node name="CollisionShape2D" type="CollisionShape2D" parent="HitBox"]
shape = SubResource("rest")
disabled = true

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
libraries/ = ExtResource("animation")
''', encoding='utf-8')
        effect['hitboxes'], effect['offset'], effect['effect_cells'] = hitboxes, offset, cells
    manifest.update({'count': packed_count, 'columns': 6, 'rows': rows, 'cell_size': [200, 200],
                     'base_cell_sha256': base_hashes, 'inline_count': len(effect_images)})
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Imported 5 skill actions (122 source frames, {packed_count-87} shared cells), {len(effect_images)} inline effect cells and 5 detached FX; 87 existing cells unchanged')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    apply(parser.parse_args().manifest)
