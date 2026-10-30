"""Export the three sea map locations with their original registration and hit states."""
import argparse
from copy import deepcopy
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as E

from PIL import Image


def export(args):
    """Render original state matrices on one canvas, then crop all states together."""
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    digest = hashlib.sha256(content).hexdigest()
    if digest != 'fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e':
        raise ValueError('Unexpected official OtherMat revision')
    args.work.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    profile = args.work / 'profile'
    profile.mkdir(exist_ok=True)
    command = [str(args.java), f'-Duser.home={args.work.resolve()}', '-jar', str(args.ffdec)]

    def run(*parts):
        """Keep native renderer diagnostics with the manifest."""
        with (args.work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(parts), env=dict(os.environ, APPDATA=str(profile)),
                           stdout=log, stderr=subprocess.STDOUT, check=True)

    source = args.work / 'source.swf'
    source.write_bytes(content)
    xml = args.work / 'source.xml'
    run('-swf2xml', str(source), str(xml))
    tree = E.parse(xml)
    tags = tree.getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('type') == 'DefineSpriteTag'}
    # Shape 453 is the intentionally invisible hit area of both coastal locations.
    hit = next(tag for tag in tags if tag.get('shapeId') == '453')
    for color in hit.iter('color'):
        color.set('alpha', '255')
        for channel in ('red', 'green', 'blue'):
            color.set(channel, '255')
    padding = deepcopy(hit)
    padding.set('shapeId', '40000')
    for color in padding.iter('color'):
        color.set('alpha', '0')
    for key, value in zip(('Xmin', 'Ymin', 'Xmax', 'Ymax'), (-10000, -6000, 10000, 6000)):
        padding.find('shapeBounds').set(key, str(value))
    tags.insert(len(tags) - 1, padding)
    exports = []
    locations = []
    for index, (name, field, wrapper_id) in enumerate([('qianan', 's3_1', 460), ('donghai', 's3_2', 468), ('shazhiwoliu', 's3_3', 478)]):
        placed = next(item for item in sprites[505].find('subTags') if item.get('name') == field)
        position = placed.find('matrix')
        parent_scale = (float(position.get('scaleX', '1')), float(position.get('scaleY', '1')))
        states = [item for item in sprites[wrapper_id].find('subTags') if item.get('characterId')]
        location = {'name': name, 'position': [int(position.get('translateX')) / 20,
                                              int(position.get('translateY')) / 20], 'parent_scale': parent_scale, 'states': {}}
        for state_index, (state, placement) in enumerate(zip(('hit', 'hover', 'current'), states)):
            sid = 40001 + index * 3 + state_index
            count = int(sprites[int(placement.get('characterId'))].get('frameCount')) if state == 'current' else 1
            helper = E.Element('item', type='DefineSpriteTag', spriteId=str(sid), frameCount=str(count), forceWriteAsLong='true')
            sub = E.SubElement(helper, 'subTags')
            bound = E.SubElement(sub, 'item', type='PlaceObject2Tag', characterId='40000', depth='1',
                                 placeFlagHasCharacter='true', placeFlagHasMatrix='true', placeFlagMove='false')
            E.SubElement(bound, 'matrix', type='MATRIX', hasRotate='false', hasScale='false', translateX='0', translateY='0')
            original = deepcopy(placement)
            original.set('depth', '2')
            # SelectPlace.s3_3's Y scale compensates the stretched source lettering.
            # Bake the parent linear transform before rendering any child timeline.
            matrix = original.find('matrix')
            matrix.set('hasScale', 'true')
            matrix.set('scaleX', str(float(matrix.get('scaleX', '1')) * parent_scale[0]))
            matrix.set('scaleY', str(float(matrix.get('scaleY', '1')) * parent_scale[1]))
            matrix.set('translateX', str(round(int(matrix.get('translateX', '0')) * parent_scale[0])))
            matrix.set('translateY', str(round(int(matrix.get('translateY', '0')) * parent_scale[1])))
            sub.append(original)
            for _ in range(count):
                E.SubElement(sub, 'item', type='ShowFrameTag', forceWriteAsLong='false')
            tags.insert(len(tags) - 1, helper)
            location['states'][state] = {'id': sid, 'frames': count}
            exports.append(sid)
        locations.append(location)
    modified = args.work / 'registered.xml'
    tree.write(modified, encoding='utf-8', xml_declaration=True)
    swf = args.work / 'registered.swf'
    run('-xml2swf', str(modified), str(swf))
    png = args.work / 'png'
    run('-selectid', ','.join(map(str, exports)), '-ignorebackground', '-export', 'sprite', str(png), str(swf))
    folders = {int(p.name.split('_')[1]): p for p in png.iterdir() if p.is_dir()}
    for location in locations:
        rendered = {}
        bounds = []
        for state, data in location['states'].items():
            frames = [Image.open(folders[data['id']] / f'{i + 1}.png').convert('RGBA') for i in range(data['frames'])]
            # FFDec adds symmetric filter margins even around the invisible bounds.
            # Normalize the registration before combining states with different filters.
            frames = [image.crop(((image.width - 1000) // 2, (image.height - 600) // 2,
                                   (image.width + 1000) // 2, (image.height + 600) // 2)) for image in frames]
            rendered[state] = frames
            bounds.extend(image.getbbox() for image in frames if image.getbbox())
        box = (min(b[0] for b in bounds) - 12, min(b[1] for b in bounds) - 12,
               max(b[2] for b in bounds) + 12, max(b[3] for b in bounds) + 12)
        size = (box[2] - box[0], box[3] - box[1])
        location['size'] = size
        location['offset'] = [box[0] - 500, box[1] - 300]
        name = location['name']
        rendered['hit'][0].crop(box).save(args.work / f'{name}_hit.png')
        Image.new('RGBA', size).save(args.output / f'map_{name}_normal.png')
        rendered['hover'][0].crop(box).save(args.output / f'map_{name}_hover.png')
        atlas = Image.new('RGBA', (size[0] * 5, size[1] * math.ceil(len(rendered['current']) / 5)))
        for i, frame in enumerate(rendered['current']):
            atlas.paste(frame.crop(box), ((i % 5) * size[0], (i // 5) * size[1]))
        atlas.save(args.output / f'map_{name}_current.png')
    (args.work / 'manifest.json').write_text(json.dumps({'source_sha256': digest, 'locations': locations}, indent=2), encoding='utf-8')
    print('OFFICIAL COAST EXPORT PASS: 3 locations, registered states, parent scale, native filters and original matrices')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ('source', 'java', 'ffdec', 'work', 'output'):
        parser.add_argument('--' + option, type=Path, required=True)
    export(parser.parse_args())
