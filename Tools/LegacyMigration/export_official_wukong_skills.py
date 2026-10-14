"""Export Wukong's five ZMXY1 skills without changing their registration.

Extends the established two-layer body/weapon pipeline. Source vectors retain
the Role1 parent transform; inline energy and detached effects remain separate.
The first rasterization audits a generously sized canvas before atlas packing.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

import export_official_wukong as base

PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
ROLE_HASH = '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7'
SKILLS = [('rising_dragon', 384), ('flame_thrust', 569),
          ('blazing_flash', 344), ('flame_storm', 417), ('fire_demon_slash', 574)]
DETACHED = [('blazing_flash_trail', 66), ('fire_demon_prelude', 129),
            ('fire_demon_combo', 112), ('fire_demon_descent', 133),
            ('fire_demon_landing', 123)]


def resize_svg(path, width=800, height=800):
    """Keep the actor's Flash origin at local (0, 28), regardless of canvas."""
    tree = ET.parse(path)
    root = tree.getroot()
    root.set('width', f'{width}px')
    root.set('height', f'{height}px')
    root.set('viewBox', f'{-width/2:g} {-height/2-28:g} {width} {height}')
    tree.write(path, encoding='utf-8', xml_declaration=True)


def collision_probe(source, output, transform):
    """Export the invisible Flash stick205 shape as an opaque hit-mask only."""
    root = ET.parse(source).getroot()
    definitions = {node.get('id'): node for node in root.iter() if node.get('id')}
    selected = []

    def find(element, ancestors=()):
        if element.tag.endswith('}use'):
            if element.get('{' + base.FFDEC + '}characterId') == '205':
                node = deepcopy(element)
                for ancestor in reversed(ancestors):
                    group = ET.Element('{' + base.SVG + '}g', {
                        key: value for key, value in ancestor.attrib.items() if key == 'transform'})
                    group.append(node)
                    node = group
                selected.append(node)
                return
            reference = definitions.get(element.get('{' + base.XLINK + '}href', '')[1:])
            if reference is not None:
                for child in reference:
                    find(child, ancestors + (element,))
        elif element.tag.endswith('}g'):
            for child in element:
                find(child, ancestors + (element,))

    definition_nodes = [node for node in root[0] if node.tag.rsplit('}', 1)[-1] in ('defs', 'filter')]
    for node in root[0]:
        if node not in definition_nodes:
            find(node)
    if not selected:
        return
    group = root[0]
    group.clear()
    group.set('transform', 'matrix(' + ','.join(map(str, transform)) + ')')
    group.extend(deepcopy(definition_nodes + selected))
    # The original pixel-hit test adds 255 to alpha. The invisible source probe
    # is geometry, not a missing visual; never add this opacity to game artwork.
    for node in root.iter():
        for key in ('fill-opacity', 'stroke-opacity'):
            if node.get(key) is not None:
                node.set(key, '1')
    ET.ElementTree(root).write(output, encoding='utf-8', xml_declaration=True)
    resize_svg(output)


def prepare(args):
    """Verify source revisions, freeze equipment selectors and export vectors."""
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != ROLE_HASH:
        raise ValueError('Unexpected Role_v7 revision')
    readable = work / 'role.swf'
    readable.write_bytes(content)
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]
    environment = dict(os.environ, APPDATA=str(work / 'profile'))
    (work / 'profile').mkdir(exist_ok=True)

    def run(*arguments):
        with (work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(arguments), env=environment,
                           stdout=log, stderr=subprocess.STDOUT, check=True)

    original = work / 'role.xml'
    if not original.exists():
        run('-swf2xml', str(readable), str(original))
    root = ET.parse(original).getroot()
    tags = root.find('tags')
    hero = next(tag for tag in tags if tag.get('spriteId') == '575')
    transforms = {tag.get('characterId'): base.matrix(tag) for tag in hero.find('subTags')
                  if tag.get('name') == 'body' and tag.get('characterId')}
    actions, poses, start = [], {}, 87
    for name, sprite_id in SKILLS:
        sprite = next(tag for tag in tags if tag.get('spriteId') == str(sprite_id))
        windows = [any(tag.get('name') == 'stick' for tag in display.values())
                   for display in base.timeline_frames(sprite)]
        count = int(sprite.get('frameCount'))
        actions.append({'name': name, 'sprite': sprite_id, 'count': count,
                        'start': start, 'hit_windows': windows})
        poses.update({str(start + i): [sprite_id, i + 1] for i in range(count)})
        start += count
    manifest = {'source_sha256': ROLE_HASH, 'fps': float(root.get('frameRate')),
                'base_count': 87, 'count': start, 'transforms': transforms,
                'skills': actions, 'poses': poses, 'variants': [], 'detached': []}
    # 338 is the original blurred dash silhouette, not a giant body rectangle.
    # Render it natively with the inline energy because SVG filters differ.
    base.EFFECT_IDS |= {'371', '416', '568', '338'}
    for name, cloth, weapon in [('unarmed', 1, 1), ('armed', 1, 2), ('armored', 2, 1)]:
        tree = ET.parse(original)
        for sprite in tree.getroot().find('tags'):
            if sprite.get('type') != 'DefineSpriteTag':
                continue
            if sprite.get('spriteId') in base.CLOTH_IDS:
                base.freeze(sprite, cloth)
            if sprite.get('spriteId') == base.WEAPON_ID:
                base.freeze(sprite, weapon)
        xml, swf = work / f'{name}.xml', work / f'{name}.swf'
        tree.write(xml, encoding='utf-8', xml_declaration=True)
        if not swf.exists():
            run('-xml2swf', str(xml), str(swf))
        raw = work / name
        ids = [str(sprite) for _, sprite in SKILLS]
        if not all(list(raw.glob(f'DefineSprite_{sprite}_*/*.svg')) for sprite in ids):
            run('-format', 'sprite:svg', '-selectid', ','.join(ids), '-ignorebackground',
                '-export', 'sprite', str(raw), str(swf))
        directories = {path.name.split('_')[1]: path for path in raw.iterdir() if path.is_dir()}
        split = work / ('split_' + name)
        inputs = {}
        for _, sprite in SKILLS:
            target = split / str(sprite)
            target.mkdir(parents=True, exist_ok=True)
            inputs[str(sprite)] = str(target)
            for source in directories[str(sprite)].glob('*.svg'):
                base.split_pose(source, target, transforms[str(sprite)])
                for path in target.glob(source.stem + '_*.svg'):
                    resize_svg(path)
                collision_probe(source, target / (source.stem + '_probe.svg'), transforms[str(sprite)])
        manifest['variants'].append({'name': name, 'input': inputs})
    inline = ET.parse(original)
    for sprite in inline.getroot().find('tags'):
        if sprite.get('spriteId') not in {'344', '384', '417', '569'}:
            continue
        keep_depths = {depth for display in base.timeline_frames(sprite) for depth, tag in display.items()
                       if tag.get('characterId') in {'371', '416', '568', '338'}}
        sub_tags = sprite.find('subTags')
        for tag in list(sub_tags):
            if tag.get('type', '').startswith(('PlaceObject', 'RemoveObject')) and tag.get('depth') not in keep_depths:
                sub_tags.remove(tag)
    inline_xml, inline_swf = work / 'inline.xml', work / 'inline.swf'
    inline.write(inline_xml, encoding='utf-8', xml_declaration=True)
    run('-xml2swf', str(inline_xml), str(inline_swf))
    inline_png = work / 'inline_png'
    run('-format', 'sprite:png', '-selectid', '344,384,417,569', '-ignorebackground',
        '-export', 'sprite', str(inline_png), str(inline_swf))
    manifest['inline'] = {path.name.split('_')[1]: str(path) for path in inline_png.iterdir() if path.is_dir()}
    bounds_command = [str(args.java), f'-Duser.home={work}', '--class-path', str(args.ffdec),
                      str(Path(__file__).with_name('FlashSpriteBounds.java'))]

    def bounds(source, ids):
        output = subprocess.check_output(bounds_command + [str(source)] + list(map(str, ids)), env=environment, text=True)
        return {line.split()[0]: [int(value) / 20 for value in line.split()[1:]]
                for line in output.splitlines() if len(line.split()) == 5 and line.split()[0].isdigit()}

    manifest['inline_bounds'] = bounds(inline_swf, [344, 384, 417, 569])
    # Native PNGs retain bitmap fills and glow filters that SVG import omits.
    raw_fx = work / 'detached_png'
    svg_fx = work / 'detached_svg'
    ids = ','.join(str(sprite) for _, sprite in DETACHED)
    if not raw_fx.exists():
        run('-format', 'sprite:png', '-selectid', ids, '-ignorebackground',
            '-export', 'sprite', str(raw_fx), str(readable))
    if not svg_fx.exists():
        run('-format', 'sprite:svg', '-selectid', ids, '-ignorebackground',
            '-export', 'sprite', str(svg_fx), str(readable))
    for name, sprite in DETACHED:
        png = next(raw_fx.glob(f'DefineSprite_{sprite}_*'))
        svg = next(svg_fx.glob(f'DefineSprite_{sprite}_*'))
        sample = ET.parse(svg / '1.svg').getroot()
        manifest['detached'].append({'name': name, 'sprite': sprite,
            'count': int(next(t for t in tags if t.get('spriteId') == str(sprite)).get('frameCount')),
            'input': str(png), 'svg_transform': sample[0].get('transform'),
            'width': sample.get('width'), 'height': sample.get('height')})
    manifest['detached_bounds'] = bounds(readable, [sprite for _, sprite in DETACHED])
    path = work / 'manifest.json'
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Prepared {start-87} skill poses and 5 detached effects: {path}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ('source', 'work', 'java', 'ffdec'):
        parser.add_argument('--' + option, type=Path, required=True)
    prepare(parser.parse_args())
