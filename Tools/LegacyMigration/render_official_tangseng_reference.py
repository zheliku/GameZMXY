"""Render source-only Role2 idle wardrobe samples with FFDec's native renderer.

The fixed 200px canvas retains the raw Flash origin. Outputs remain validation
references, not game atlases; full motion/frame-script baking is a later gate.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw
from audit_official_tangseng import ROLE_HASH
from export_official_wukong import freeze


def render(args):
    """Render every distinct current wardrobe selector without Godot inputs."""
    audit = json.loads(args.audit.read_text(encoding='utf-8'))
    if hashlib.sha256(args.role.read_bytes()).hexdigest() != ROLE_HASH or audit['role_sha256'] != ROLE_HASH:
        raise ValueError('Unexpected official Role_v7 revision')
    if hashlib.sha256(args.xml.read_bytes()).hexdigest() != audit['xml_sha256']:
        raise ValueError('Audit and XML source do not match')
    original = ET.parse(args.xml)
    tags = original.getroot().find('tags')
    actor = next(tag for tag in tags if tag.get('spriteId') == '1045')
    idle_id = next(a['sprite'] for a in audit['actors']['export.hero.Role2']['actions'] if a['label'] == 'wait')
    placement = next(tag for tag in actor.find('subTags') if tag.get('name') == 'body' and tag.get('characterId') == str(idle_id))
    args.work.mkdir(parents=True, exist_ok=True)
    profile = args.work/'profile'
    profile.mkdir(exist_ok=True)
    command = [str(args.java), f'-Duser.home={args.work.resolve()}', '-jar', str(args.ffdec)]
    environment = dict(os.environ, APPDATA=str(profile.resolve()))
    variants = [('body', selector, 1) for selector in sorted({1}|{e['selector'] for e in audit['equipment'] if e['kind'] == 'body'})]
    variants += [('weapon', 1, selector) for selector in sorted({1}|{e['selector'] for e in audit['equipment'] if e['kind'] == 'weapon'})]
    records, previews = [], []
    for kind, cloth, weapon in variants:
        name = f'{kind}_{cloth if kind == "body" else weapon}'
        tree = deepcopy(original)
        stage = tree.getroot().find('tags')
        sprites = {int(t.get('spriteId')): t for t in stage if t.get('spriteId')}
        for item in audit['selectors']:
            sprite = sprites[item['sprite']]
            if item['kind'] == 'weapon':
                freeze(sprite, weapon)
            elif item['kind'] == 'armor' and cloth <= item['maximum_selector']:
                freeze(sprite, cloth)
            elif item['kind'] == 'armor_component' and cloth >= item['minimum_selector']:
                freeze(sprite, cloth+item['frame_offset'])
            else:
                sprite.find('subTags').clear()
                ET.SubElement(sprite.find('subTags'), 'item', type='ShowFrameTag')
                sprite.set('frameCount', '1')
        root = tree.getroot()
        root.set('frameCount', '1')
        for key, value in zip(('Xmin', 'Ymin', 'Xmax', 'Ymax'), (0, 0, 4000, 4000)):
            root.find('displayRect').set(key, str(value))
        for tag in list(stage):
            if tag.get('type', '').startswith(('PlaceObject', 'RemoveObject', 'ShowFrame', 'DoAction', 'DoABC')):
                stage.remove(tag)
        placed = deepcopy(placement)
        placed.set('depth', '1')
        placed.set('placeFlagMove', 'false')
        placed.set('placeFlagHasCharacter', 'true')
        transform = placed.find('matrix')
        transform.set('translateX', str(int(transform.get('translateX', '0'))+2000))
        transform.set('translateY', str(int(transform.get('translateY', '0'))+2560))
        stage.append(placed)
        ET.SubElement(stage, 'item', type='ShowFrameTag')
        xml, swf = args.work/f'{name}.xml', args.work/f'{name}.swf'
        tree.write(xml, encoding='utf-8', xml_declaration=True)
        with (args.work/'render.log').open('a', encoding='utf-8') as log:
            for parts in [['-xml2swf', str(xml), str(swf)], ['-ignorebackground', '-export', 'frame', str(args.work/name), str(swf)]]:
                subprocess.run(command+parts, env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
        paths = list((args.work/name).rglob('*.png'))
        if len(paths) != 1:
            raise ValueError(f'Unexpected native output: {name}')
        image = Image.open(paths[0]).convert('RGBA')
        bounds = image.getbbox()
        if image.size != (200, 200) or bounds is None or min(bounds[:2]) <= 0 or max(bounds[2:]) >= 200:
            raise ValueError(f'Incomplete reference: {name}')
        image.save(args.work/f'{name}.png')
        records.append({'name': name, 'cloth_selector': cloth, 'weapon_selector': weapon, 'bounds': bounds})
        previews.append(image)
        print('NATIVE TANGSENG REFERENCE', name, bounds, flush=True)
    preview = Image.new('RGBA', (7*200, 2*224), (38, 42, 49, 255))
    draw = ImageDraw.Draw(preview)
    for entry, image in zip(records, previews):
        row = int(entry['name'].startswith('weapon'))
        col = (entry['cloth_selector'] if row == 0 else entry['weapon_selector'])-1
        preview.alpha_composite(image, (col*200, row*224))
        draw.text((col*200+8, row*224+200), entry['name'], fill='white')
    preview.save(args.work/'wardrobe_reference.png')
    (args.work/'reference_manifest.json').write_text(json.dumps({'source_sha256': ROLE_HASH, 'origin': [100, 128],
        'pose': 'wait/source-frame-1', 'variants': records}, indent=2), encoding='utf-8')
    print('TANGSENG NATIVE WARDROBE REFERENCE PASS:', len(records))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['role', 'xml', 'audit', 'java', 'ffdec', 'work']:
        parser.add_argument('--'+name, type=Path, required=True)
    render(parser.parse_args())
