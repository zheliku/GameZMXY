"""Export the original ZMXY1 skill panel artwork without copying SWF code.

Uses the documented OtherMat_v9 revision. Icons retain the official learned
and unlearned states; headers, background and navigation keep their original
pixels. UI layout and gameplay values are maintained separately in Godot/Luban.
"""

import argparse
from copy import deepcopy
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

SOURCE_SHA256 = 'fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e'
ICONS = {
    'rising_dragon': 594, 'flame_thrust': 599, 'blazing_flash': 604,
    'flame_storm': 609, 'fire_demon_slash': 614,
    'ice_dragon_wave': 624, 'dark_ice_thorn': 629, 'water_clone': 634,
    'heavenly_dew': 639, 'water_demon_burst': 644,
}


def button_sprite(button, sprite_id, hover):
    """Render a SimpleButton's original up/over display list as a one-frame sprite."""
    sprite = ET.Element('item', type='DefineSpriteTag', spriteId=str(sprite_id),
                        frameCount='1', forceWriteAsLong='true')
    sub_tags = ET.SubElement(sprite, 'subTags')
    state = 'buttonStateOver' if hover else 'buttonStateUp'
    for record in button.find('characters'):
        if record.get(state) != 'true':
            continue
        placement = ET.SubElement(sub_tags, 'item', type='PlaceObject2Tag',
                                  characterId=record.get('characterId'),
                                  depth=record.get('placeDepth'),
                                  placeFlagHasCharacter='true', placeFlagHasMatrix='true',
                                  placeFlagMove='false', forceWriteAsLong='true')
        placement.append(deepcopy(record.find('placeMatrix')))
        placement[-1].tag = 'matrix'
    ET.SubElement(sub_tags, 'item', type='ShowFrameTag', forceWriteAsLong='false')
    return sprite


def export(java, ffdec, source, destination):
    """Verify source provenance and export native artwork through FFDec's renderer."""
    content = source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != SOURCE_SHA256:
        raise ValueError('Unexpected OtherMat resource revision')
    destination.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='gamezmxy-skill-ui-') as temporary:
        work = Path(temporary)
        environment = dict(os.environ, APPDATA=str(work / 'profile'))
        (work / 'profile').mkdir()
        readable = work / 'other.swf'
        readable.write_bytes(content)
        command = [str(java), f'-Duser.home={work}', '-jar', str(ffdec)]

        def run(*arguments):
            """Keep detailed exporter output in the disposable source work directory."""
            with (work / 'export.log').open('a', encoding='utf-8') as log:
                subprocess.run(command + list(arguments), env=environment, stdout=log,
                               stderr=subprocess.STDOUT, check=True)

        xml = work / 'other.xml'
        run('-swf2xml', str(readable), str(xml))
        tree = ET.parse(xml)
        tags = tree.getroot().find('tags')
        buttons = {t.get('buttonId'): t for t in tags if t.get('type') == 'DefineButton2Tag'}
        wrappers = {}
        for index, (name, original) in enumerate([('back', '295'), ('active', '657'), ('passive', '653')]):
            for state in range(2):
                sid = 30000 + index * 2 + state
                tags.insert(len(tags) - 1, button_sprite(buttons[original], sid, bool(state)))
                wrappers[f'skills_{name}_{"hover" if state else "normal"}'] = sid
        modified = work / 'buttons.xml'
        tree.write(modified, encoding='utf-8', xml_declaration=True)
        rendered = work / 'buttons.swf'
        run('-xml2swf', str(modified), str(rendered))
        selection = list(ICONS.values()) + [382, 386, 585] + list(wrappers.values())
        png = work / 'png'
        run('-selectid', ','.join(map(str, selection)), '-ignorebackground',
            '-export', 'sprite', str(png), str(rendered))
        run('-selectid', '375,586', '-export', 'image', str(work / 'images'), str(readable))

        def copy_sprite(sid, frame, name):
            """Copy the renderer result verbatim, preserving the source alpha channel."""
            directory = next(p for p in png.iterdir() if p.name.split('_')[1] == str(sid))
            shutil.copyfile(directory / f'{frame}.png', destination / f'{name}.png')

        for name, sid in ICONS.items():
            copy_sprite(sid, 1, f'skill_{name}_unlearned')
            copy_sprite(sid, 2, f'skill_{name}_learned')
        for hero, sid in [('wukong', 382), ('sanzang', 386)]:
            for state, frame in [('normal', 3), ('selected', 2)]:
                copy_sprite(sid, frame, f'skills_{hero}_{state}')
        for frame, name in [(1, 'active'), (2, 'passive')]:
            copy_sprite(585, frame, f'skills_table_{name}')
        for name, sid in wrappers.items():
            copy_sprite(sid, 1, name)
        for sid, name in [(375, 'skills_background'), (586, 'skills_soul_badge')]:
            original = next((work / 'images').glob(f'{sid}.*'))
            shutil.copyfile(original, destination / (name + original.suffix.lower()))
    print(f'Exported 34 original skill UI images; source SHA-256 {SOURCE_SHA256}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ['java', 'ffdec', 'source']:
        parser.add_argument('--' + argument, type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2] /
                        'Godot/GodotProject/TheGame/Sprites/UI/skills')
    args = parser.parse_args()
    export(args.java, args.ffdec, args.source, args.output)
