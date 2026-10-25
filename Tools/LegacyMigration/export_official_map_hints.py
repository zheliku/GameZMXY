"""Export the five original 19-frame current-level hints, including Flash glow."""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as E
from PIL import Image

ICONS = [('huaguoshan', 392, 395, 68, 78), ('shuiliandong', 399, 402, 60, 58),
         ('taohuayuan', 406, 409, 64, 65), ('bianfuku', 413, 416, 57, 30),
         ('qixingdong', 427, 430, 86, 64)]


def export(args):
    """Keep source frames/filters, padding the export before the largest glow occurs."""
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    digest = hashlib.sha256(content).hexdigest()
    if digest != 'fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e':
        raise ValueError('Unexpected OtherMat resource revision')
    args.work.mkdir(parents=True, exist_ok=True)
    profile = args.work / 'profile'
    profile.mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(profile))
    command = [str(args.java), f'-Duser.home={args.work.resolve()}', '-jar', str(args.ffdec)]

    def run(*parts):
        with (args.work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(parts), env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)

    readable = args.work / 'source.swf'
    readable.write_bytes(content)
    xml = args.work / 'source.xml'
    run('-swf2xml', str(readable), str(xml))
    tree = E.parse(xml)
    tags = tree.getroot().find('tags')
    sprites = {int(t.get('spriteId')): t for t in tags if t.get('type') == 'DefineSpriteTag'}
    template = next(t for t in tags if t.get('type') in ('DefineShapeTag', 'DefineShape2Tag', 'DefineShape3Tag')
                    and list(t.iter('color')) and not any(n.get('fillStyleType') == '64' for n in t.iter('item')))
    for index, (name, base, current, width, height) in enumerate(ICONS):
        # FFDec chooses its canvas using the first filter (2px); later blur reaches 6px.
        # A transparent bounds-only shape expands the canvas without altering visible art.
        padding = deepcopy(template)
        padding.set('shapeId', str(32000 + index))
        for color in padding.iter('color'):
            color.set('alpha', '0')
        rect = padding.find('shapeBounds')
        for key, value in zip(('Xmin', 'Ymin', 'Xmax', 'Ymax'),
                              (-width*10-240, -height*10-240, width*10+240, height*10+240)):
            rect.set(key, str(value))
        tags.insert(len(tags)-1, padding)
        placed = E.Element('item', type='PlaceObject2Tag', characterId=str(32000+index), depth='2',
                           placeFlagHasCharacter='true', placeFlagHasMatrix='true', placeFlagMove='false')
        E.SubElement(placed, 'matrix', type='MATRIX', hasRotate='false', hasScale='false',
                     translateX='0', translateY='0')
        sprites[current].find('subTags').insert(0, placed)
    variant = args.work / 'padded.xml'
    tree.write(variant, encoding='utf-8', xml_declaration=True)
    swf = args.work / 'padded.swf'
    run('-xml2swf', str(variant), str(swf))
    run('-selectid', ','.join(str(x[2]) for x in ICONS), '-ignorebackground', '-export', 'sprite',
        str(args.work / 'png'), str(swf))
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = {'source_sha256': digest, 'fps': 24, 'icons': []}
    folders = {int(p.name.split('_')[1]): p for p in (args.work / 'png').iterdir() if p.is_dir()}
    for name, base, current, width, height in ICONS:
        images = [Image.open(folders[current] / f'{i+1}.png').convert('RGBA') for i in range(19)]
        size = images[0].size
        atlas = Image.new('RGBA', (size[0]*5, size[1]*4))
        for i, image in enumerate(images):
            if image.size != size or image.getbbox() is None:
                raise ValueError(f'Invalid frame: {name}/{i+1}')
            left, top, right, bottom = image.getbbox()
            if left == 0 or top == 0 or right == size[0] or bottom == size[1]:
                raise ValueError(f'Clipped source glow: {name}/{i+1}')
            atlas.paste(image, (i%5*size[0], i//5*size[1]))
        atlas.save(args.output / f'map_{name}_current.png')
        manifest['icons'].append({'name': name, 'source': current, 'frames': 19, 'size': size})
    (args.work / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('MAP HINT EXPORT PASS: 5 icons, 95 original glow frames, 24fps, no clipped borders')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'java', 'ffdec', 'work', 'output'):
        parser.add_argument('--'+name, type=Path, required=True)
    export(parser.parse_args())
