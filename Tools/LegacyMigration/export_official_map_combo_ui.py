"""Render OtherMat_v9's map skill button and Batter with original registration.

The map selector stops on each state. Its hover children stop on frame 15;
only the affordable glow loops. Export each complete child timeline and the
save button's original up/over states. Combo digits play five frames and hold.
Only pixels and native Godot SpriteFrames enter the project; SWF code stays in
the validation work directory.
"""

import argparse
from copy import deepcopy
import json
import math
import os
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image

from export_official_combat_effects import decode
from export_official_skill_ui import SOURCE_SHA256

DIGITS = (564, 560, 556, 552, 548, 544, 539, 535, 531, 527)


def display_lists(sprite):
    """Read the three original selector states without changing their matrices."""
    depths = {}
    result = []
    for tag in sprite.find('subTags'):
        kind = tag.get('type')
        if kind.startswith('RemoveObject'):
            depths.pop(tag.get('depth'))
        elif kind.startswith('PlaceObject'):
            if tag.get('placeFlagMove') == 'true':
                raise ValueError('Map selector unexpectedly contains a moved placement')
            depths[tag.get('depth')] = deepcopy(tag)
        elif kind == 'ShowFrameTag':
            result.append([deepcopy(depths[key]) for key in sorted(depths, key=int)])
    return result


def character_placement(character_id):
    """Place one official clip at the caller's registration point."""
    tag = ET.Element('item', type='PlaceObject2Tag', characterId=str(character_id),
                     depth='1', forceWriteAsLong='false', placeFlagHasCharacter='true',
                     placeFlagHasMatrix='true', placeFlagMove='false')
    ET.SubElement(tag, 'matrix', type='MATRIX', hasRotate='false', hasScale='false',
                  translateX='0', translateY='0')
    return tag


def write_frames(destination, name, images, animations):
    """Pack original full-canvas frames, retaining existing resource UID on rerun."""
    destination.mkdir(parents=True, exist_ok=True)
    width, height = images[0].size
    atlas = Image.new('RGBA', (width * 5, height * math.ceil(len(images) / 5)))
    for index, image in enumerate(images):
        if image.size != (width, height) or image.getbbox() is None:
            raise ValueError(f'Invalid {name} frame {index + 1}')
        left, top, right, bottom = image.getbbox()
        if min(left, top) <= 0 or right >= width or bottom >= height:
            raise ValueError(f'Clipped original artwork: {name}/{index + 1}')
        atlas.paste(image, (index % 5 * width, index // 5 * height))
    atlas.save(destination / f'{name}.png')
    resource = destination / f'{name}_frames.tres'
    header = resource.read_text(encoding='utf-8').splitlines()[0] if resource.exists() else ''
    uid = re.search(r'uid="([^"]+)"', header)
    game = Path(__file__).resolve().parents[2] / 'Godot/GodotProject/TheGame'
    texture = (destination / f'{name}.png').relative_to(game).as_posix()
    lines = ['[gd_resource type="SpriteFrames" format=3' + (f' uid="{uid[1]}"' if uid else '') + ']',
             '', f'[ext_resource type="Texture2D" path="res://TheGame/{texture}" id="atlas"]']
    for index in range(len(images)):
        lines += ['', f'[sub_resource type="AtlasTexture" id="frame_{index}"]',
                  'atlas = ExtResource("atlas")',
                  f'region = Rect2({index % 5 * width}, {index // 5 * height}, {width}, {height})']
    entries = []
    offset = 0
    for animation, count, loop in animations:
        frames = ', '.join('{"duration": 1.0, "texture": SubResource("frame_' + str(i) + '")}'
                           for i in range(offset, offset + count))
        entries.append('{"frames": [' + frames + '], "loop": ' + str(loop).lower()
                       + ', "name": &"' + animation + '", "speed": 24.0}')
        offset += count
    if offset != len(images):
        raise ValueError(f'Frame count mismatch for {name}')
    resource.write_text('\n'.join(lines) + '\n\n[resource]\nanimations = ['
                        + ',\n'.join(entries) + ']\n', encoding='utf-8')


def export(args):
    """Verify the source identity and render fixed stages with known origins."""
    args.work.mkdir(parents=True, exist_ok=True)
    game = Path(__file__).resolve().parents[2] / 'Godot/GodotProject/TheGame'
    with tempfile.TemporaryDirectory(prefix='map-combo-', dir=args.work) as temporary:
        work = Path(temporary)
        profile = work / 'profile'
        profile.mkdir()
        environment = dict(os.environ, APPDATA=str(profile))
        command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

        def run(*parts):
            """Retain source-renderer diagnostics outside shipped resources."""
            with (args.work / 'export.log').open('a', encoding='utf-8') as log:
                subprocess.run(command + list(parts), env=environment, stdout=log,
                               stderr=subprocess.STDOUT, check=True)

        source = work / 'source.swf'
        source.write_bytes(decode(args.source, SOURCE_SHA256))
        xml = work / 'source.xml'
        run('-swf2xml', str(source), str(xml))
        tree = ET.parse(xml)
        tags = tree.getroot().find('tags')
        sprites = {int(t.get('spriteId')): t for t in tags if t.get('type') == 'DefineSpriteTag'}
        symbols = next(t for t in tags if t.get('type') == 'SymbolClassTag')
        symbols = dict(zip((int(n.text) for n in symbols.find('tags')),
                           (n.text for n in symbols.find('names'))))
        if symbols[567] != 'export.Batter' or any(symbols[sid] != f'num{n}' for n, sid in enumerate(DIGITS)):
            raise ValueError('Official combo symbol identity mismatch')
        selector = next(t for t in sprites[505].find('subTags') if t.get('name') == 'showBuySkill')
        if selector.get('characterId') != '492':
            raise ValueError('Official map skill button identity mismatch')

        def render(name, size, origin, sections):
            """Play nested official clips on a fixed stage, preserving all filters."""
            composed = deepcopy(tree)
            root = composed.getroot()
            root.set('frameCount', str(sum(count for count, _ in sections)))
            rect = root.find('displayRect')
            for key, value in zip(('Xmin', 'Ymin', 'Xmax', 'Ymax'), (0, 0, size[0] * 20, size[1] * 20)):
                rect.set(key, str(value))
            stage = root.find('tags')
            for tag in list(stage):
                if tag.get('type', '').startswith(('PlaceObject', 'RemoveObject', 'ShowFrame', 'DoAction', 'DoABC')):
                    stage.remove(tag)
            previous_depths = []
            for count, placements in sections:
                for depth in previous_depths:
                    ET.SubElement(stage, 'item', type='RemoveObject2Tag', depth=depth, forceWriteAsLong='false')
                previous_depths = []
                for placement in placements:
                    placed = deepcopy(placement)
                    matrix = placed.find('matrix')
                    matrix.set('translateX', str(round(int(matrix.get('translateX', 0)) + origin[0] * 20)))
                    matrix.set('translateY', str(round(int(matrix.get('translateY', 0)) + origin[1] * 20)))
                    stage.append(placed)
                    previous_depths.append(placed.get('depth'))
                for _ in range(count):
                    ET.SubElement(stage, 'item', type='ShowFrameTag', forceWriteAsLong='false')
            output_xml = work / f'{name}.xml'
            composed.write(output_xml, encoding='utf-8', xml_declaration=True)
            swf = work / f'{name}.swf'
            run('-xml2swf', str(output_xml), str(swf))
            output = work / name
            run('-ignorebackground', '-export', 'frame', str(output), str(swf))
            paths = sorted(output.rglob('*.png'), key=lambda p: int(re.findall(r'\d+', p.stem)[-1]))
            if len(paths) != sum(count for count, _ in sections):
                raise ValueError(f'Unexpected rendered frame count: {name}')
            return [Image.open(path).convert('RGBA') for path in paths]

        states = display_lists(sprites[492])
        if len(states) != 3:
            raise ValueError('Expected three map skill selector states')
        map_images = render('map_skills', (256, 160), (128, 80), list(zip((1, 15, 19), states)))
        write_frames(game / 'Sprites/UI/frontend', 'map_skills', map_images,
                     [('idle', 1, False), ('hover', 15, False), ('available', 19, True)])
        # SimpleButton 502 swaps shape499/501; it has no looping timeline.
        save_images = render('map_save', (138, 93), (75, 54.45),
                             [(1, [character_placement(499)]), (1, [character_placement(501)])])
        for state, bitmap in zip(('normal', 'hover'), save_images):
            target = args.work / 'map_save_game_normal.png' if state == 'normal' else \
                game / 'Sprites/UI/frontend/map_save_game_hover.png'
            bitmap.save(target)
        if args.map_only:
            (args.work / 'manifest.json').write_text(json.dumps({
                'source_sha256': SOURCE_SHA256, 'map_selector': 492,
                'skill_frames': [1, 15, 19], 'skill_loops': [False, False, True],
                'hover_stop_scripts': [488, 487], 'save_button': 502,
                'save_shapes': [499, 501], 'save_origin': [75, 54.45],
            }, indent=2), encoding='utf-8')
            print('OFFICIAL MAP EXPORT PASS: static idle, single hover, affordable glow; save up/over')
            return
        digits = render('combo_digits', (320, 320), (160, 160),
                        [(5, [character_placement(sid)]) for sid in DIGITS])
        write_frames(game / 'Sprites/UI/hud', 'combo_digits', digits,
                     [(str(n), 5, False) for n in range(10)])
        caption = render('combo_caption', (224, 80), (16, 16), [(1, [character_placement(567)])])[0]
        caption.save(game / 'Sprites/UI/hud/combo_caption.png')
        manifest = {'source_sha256': SOURCE_SHA256, 'fps': 24,
                    'map': {'selector': 492, 'position': [81.8, 103.95], 'canvas': [256, 160],
                            'origin': [128, 80], 'frames': [1, 15, 19]},
                    'combo': {'caption': 567, 'position': [694.95, 234.95], 'caption_origin': [16, 16],
                              'digits': list(DIGITS), 'canvas': [320, 320], 'origin': [160, 160],
                              'digit_frames': 5, 'digit_spacing': 50,
                              'number_origins': [[4, 28], [-44, 28], [-95, 28]]}}
        (args.work / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
        # A side-by-side sample makes the exact registration and frame transitions reviewable.
        preview = Image.new('RGBA', (1024, 640), (45, 49, 55, 255))
        for index in range(3):
            preview.alpha_composite(map_images[(0, 1, 16)[index]], (index * 256, 0))
        preview.alpha_composite(caption, (160, 280))
        preview.alpha_composite(digits[2 * 5 + 4], (16, 164))
        preview.alpha_composite(digits[2 * 5], (400, 164))
        preview.save(args.work / 'official_ui_preview.png')
    print('OFFICIAL MAP/COMBO EXPORT PASS: map 1/15/19 frames, ten 5-frame digits, source SHA verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'java', 'ffdec', 'work'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--map-only', action='store_true', help='Update map buttons without rewriting combo assets.')
    export(parser.parse_args())
