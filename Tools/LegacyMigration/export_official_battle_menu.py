"""Export ZMXY1 RoleInfo's skill slots and musou art at their source origins."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

from PIL import Image
from export_official_skill_ui import SOURCE_SHA256, button_sprite
from export_official_wukong import timeline_frames, matrix

SKILLS = ['RisingDragon', 'FlameThrust', 'BlazingFlash', 'FlameStorm', 'FireDemonSlash',
          'IceDragonWave', 'DarkIceFormation', 'WaterClone', 'HeavenlyDew', 'WaterDemonBurst']


def shape_sprite(character, sprite_id):
    """Wrap one source shape without changing its coordinates or drawing style."""
    sprite = ET.Element('item', type='DefineSpriteTag', spriteId=str(sprite_id), frameCount='1')
    tags = ET.SubElement(sprite, 'subTags')
    placed = ET.SubElement(tags, 'item', type='PlaceObject2Tag', characterId=str(character),
                           depth='1', placeFlagHasCharacter='true', placeFlagHasMatrix='true',
                           placeFlagMove='false')
    ET.SubElement(placed, 'matrix', type='MATRIX', hasRotate='false', hasScale='false',
                  translateX='0', translateY='0', nTranslateBits='0')
    ET.SubElement(tags, 'item', type='ShowFrameTag')
    return sprite


def export(java, ffdec, source, output, work):
    """Render native source art and retain its registration rectangles for scene authoring."""
    content = source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != SOURCE_SHA256:
        raise ValueError('Unexpected OtherMat resource revision')
    output.mkdir(parents=True, exist_ok=True)
    work.mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, APPDATA=str(work / 'profile'))
    (work / 'profile').mkdir(exist_ok=True)
    readable = work / 'other.swf'
    readable.write_bytes(content)
    command = [str(java), f'-Duser.home={work}', '-jar', str(ffdec)]

    def run(*args):
        """Capture renderer diagnostics in the disposable validation directory."""
        with (work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(args), env=environment, stdout=log,
                           stderr=subprocess.STDOUT, check=True)

    run('-swf2xml', str(readable), str(work / 'other.xml'))
    tree = ET.parse(work / 'other.xml')
    tags = tree.getroot().find('tags')
    sprites = {t.get('spriteId'): t for t in tags if t.get('type') == 'DefineSpriteTag'}
    buttons = {t.get('buttonId'): t for t in tags if t.get('type') == 'DefineButton2Tag'}
    wrappers = {}
    for index, (name, sid) in enumerate([('panel_back', '295'), ('panel_active', '657'), ('panel_passive', '653')]):
        for state in range(2):
            new_id = 31000 + index * 2 + state
            tags.insert(len(tags) - 1, button_sprite(buttons[sid], new_id, bool(state)))
            wrappers[f'battle_menu_{name}_{"hover" if state else "normal"}'] = new_id
    for index, (name, sid) in enumerate([('ws_under', 330), ('ws_fill', 333)]):
        new_id = 31100 + index
        tags.insert(len(tags) - 1, shape_sprite(sid, new_id))
        wrappers[f'battle_menu_{name}'] = new_id
    tree.write(work / 'render.xml', encoding='utf-8', xml_declaration=True)
    run('-xml2swf', str(work / 'render.xml'), str(work / 'render.swf'))
    selection = [328, 340] + list(wrappers.values())
    run('-selectid', ','.join(map(str, selection)), '-ignorebackground', '-export', 'sprite',
        str(work / 'png'), str(work / 'render.swf'))
    run('-selectid', '335', '-export', 'image', str(work / 'images'), str(readable))

    def frame(sid, index=1):
        """Locate a native renderer output by source ID and frame number."""
        directory = next(p for p in (work / 'png').iterdir() if p.name.split('_')[1] == str(sid))
        return directory / f'{index}.png'

    # The battle selector is a separate 45px symbol; the learning panel's 65px icon is unsuitable.
    sheet = Image.new('RGBA', (45 * 11, 45))
    for i in range(11):
        with Image.open(frame(328, i + 1)) as picture:
            if picture.size != (45, 45):
                raise ValueError(f'Unexpected HUD skill slot size: {picture.size}')
            sheet.paste(picture, (45 * i, 0))
    sheet.save(output / 'battle_skill_icons.png')
    resource = '[gd_resource type="SpriteFrames" format=3]\n\n'
    resource += '[ext_resource type="Texture2D" path="res://TheGame/Sprites/UI/hud/battle_skill_icons.png" id="atlas"]\n'
    for i in range(11):
        resource += f'\n[sub_resource type="AtlasTexture" id="slot_{i}"]\natlas = ExtResource("atlas")\nregion = Rect2({45*i}, 0, 45, 45)\n'
    animations = []
    for i, name in enumerate(SKILLS, 1):
        animations.append('{"frames": [{"duration": 1.0, "texture": SubResource("slot_0")}, '
                          f'{{"duration": 1.0, "texture": SubResource("slot_{i}")}}], '
                          f'"loop": false, "name": &"{name}", "speed": 1.0}}')
    resource += '\n[resource]\nanimations = [' + ',\n'.join(animations) + ']\n'
    (output / 'battle_skill_icon_frames.tres').write_text(resource, encoding='utf-8')
    for name, sid in wrappers.items():
        if not name.startswith('battle_menu_panel_'):
            shutil.copyfile(frame(sid), output / f'{name}.png')
    shutil.copyfile(next((work / 'images').glob('335.*')), output / 'battle_menu_ws_idle.png')
    bounds_command = [str(java), f'-Duser.home={work}', '--class-path', str(ffdec),
                      str(Path(__file__).with_name('FlashSpriteBounds.java')), str(work / 'render.swf')]
    result = subprocess.run(bounds_command + list(map(str, selection)), env=environment,
                            capture_output=True, text=True, check=True)
    rectangles = {int(values[0]): [int(n) / 20 for n in values[1:]]
                  for line in result.stdout.splitlines() if len(values := line.split()) == 5}
    manifest = {'bounds': {name: rectangles[sid] for name, sid in wrappers.items()},
                'full_glow_bounds': rectangles[340],
                'placements': {tag.get('name'): matrix(tag)
                               for tag in next(timeline_frames(sprites['374'])).values() if tag.get('name')}}
    (work / 'layout.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    # Source glow already has a true alpha channel; no rectangular-sheet mask is necessary.
    glow_frames = sorted(frame(340).parent.glob('*.png'), key=lambda p: int(p.stem))
    glow = '[gd_resource type="SpriteFrames" format=3]\n\n'
    for i, picture in enumerate(glow_frames):
        name = f'battle_menu_ws_glow_{i:02d}.png'
        shutil.copyfile(picture, output / name)
        glow += f'[ext_resource type="Texture2D" path="res://TheGame/Sprites/UI/hud/{name}" id="frame_{i}"]\n'
    glow += '\n[resource]\nanimations = [{"frames": [' + ', '.join(
        f'{{"duration": 1.0, "texture": ExtResource("frame_{i}")}}' for i in range(len(glow_frames)))
    glow += '], "loop": true, "name": &"full", "speed": 24.0}]\n'
    (output / 'battle_menu_ws_glow_frames.tres').write_text(glow, encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ('java', 'ffdec', 'source', 'work'):
        parser.add_argument('--' + argument, type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2] /
                        'Godot/GodotProject/TheGame/Sprites/UI/hud')
    args = parser.parse_args()
    export(args.java, args.ffdec, args.source, args.output, args.work)
