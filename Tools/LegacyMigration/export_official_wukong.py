"""Export independent Wukong body and weapon atlases from official Role_v7 timelines.

Uses FFDec's XML timeline and SVG export, then Godot's SVG renderer. Movement
registration is retained; the five basic attacks use every original source frame.
The manifest retains source hit windows and 24 fps timing; the rasterizer fits
rectangular hitboxes to the actual weapons. Only fixed golden-staff ghosts are
omitted; original curved slashes and spinning effects remain.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from PIL import Image

SVG = 'http://www.w3.org/2000/svg'
XLINK = 'http://www.w3.org/1999/xlink'
FFDEC = 'https://www.free-decompiler.com/flash'
ET.register_namespace('', SVG)
ET.register_namespace('xlink', XLINK)
ET.register_namespace('ffdec', FFDEC)


def split_pose(source, output, transform):
    """Separate body, equipment and effects, retaining Flash draw order."""
    # Reusing an export directory must not retain removed fragment sidecars.
    for previous in output.glob(source.stem + '_*.svg'):
        previous.unlink()
    root = ET.parse(source).getroot()
    definitions = {t.get('id'): t for t in root.iter() if t.get('id')}

    def flatten(element, ancestors=()):
        if element.tag == '{' + SVG + '}use':
            character = element.get('{' + FFDEC + '}characterId')
            if character in WEAPON_IDS:
                return [('weapon', wrap(element, ancestors))]
            if character == '246':
                # This baked red/gold staff cannot follow the equipped weapon.
                return []
            if character in EFFECT_IDS:
                return [('effect', wrap(element, ancestors))]
            if character == '205':  # Invisible collision probe, never artwork.
                return []
            reference = definitions.get(element.get('{' + XLINK + '}href', '')[1:])
            if reference is not None and any(t.get('{' + FFDEC + '}characterId') in WEAPON_IDS for t in reference.iter()):
                return [entry for child in reference for entry in flatten(child, ancestors + (element,))]
        return [('body', wrap(element, ancestors))]

    def wrap(element, ancestors):
        result = deepcopy(element)
        for ancestor in reversed(ancestors):
            group = ET.Element('{' + SVG + '}g', {k: v for k, v in ancestor.attrib.items()
                if k in ('transform', 'opacity', 'filter', 'clip-path', 'mask')})
            group.append(result)
            result = group
        return result

    definitions_in_group = [c for c in root[0] if c.tag.rsplit('}', 1)[-1] in ('filter', 'defs')]
    display = [entry for child in root[0] if child not in definitions_in_group for entry in flatten(child)]
    selections = {'body': [t for layer, t in display if layer == 'body']}
    # Each equipment fragment gets its own foreground mask. A single mask
    # would incorrectly erase later fragments when a hand lies between them.
    for n, (index, element) in enumerate((i, t) for i, (kind, t) in enumerate(display) if kind == 'weapon'):
        selections[f'weapon_{n}'] = [element]
        selections[f'occluder_{n}'] = [t for kind, t in display[index + 1:] if kind == 'body']
    for n, (index, element) in enumerate((i, t) for i, (kind, t) in enumerate(display) if kind == 'effect'):
        selections[f'effect_{n}'] = [element]
        selections[f'effect_occluder_{n}'] = [t for kind, t in display[index + 1:] if kind in ('body', 'weapon')]
    for kind, selected in selections.items():
        result = deepcopy(root)
        is_effect = kind.startswith('effect_')
        result.set('width', '400px' if is_effect else '200px')
        result.set('height', '300px' if is_effect else '200px')
        result.set('viewBox', '-200 -178 400 300' if is_effect else '-100 -128 200 200')
        group = result[0]
        group.clear()
        group.set('transform', 'matrix(' + ','.join(map(str, transform)) + ')')
        group.extend(deepcopy(definitions_in_group + selected))
        ET.ElementTree(result).write(output / (source.stem + '_' + kind + '.svg'), encoding='utf-8', xml_declaration=True)


# Keep the original source registration, but pack only the poses in use.
# The old cell numbers are solely an explicit one-time animation remap.
LEGACY_POSES = {
    **{i: (529, f) for i, f in enumerate([1, 7, 13, 19, 25, 31])},
    6: (529, 85), 7: (529, 90), 8: (553, 15),
    **{12+i: (440, f) for i, f in enumerate([1, 5, 9, 13])},
    **{18+i: (452, f) for i, f in enumerate([1, 3, 5, 7])},
    24: (533, 1), 25: (543, 1),
    **{30+i: (539, f) for i, f in enumerate([1, 3, 5, 7, 9])},
    74: (553, 1),
}
BASE_FRAME_MAP = {old: new for new, old in enumerate(LEGACY_POSES)}
POSES = dict(enumerate(LEGACY_POSES.values()))
CLOTH_IDS = {'143', '152', '160', '186'}
WEAPON_ID = '177'
WEAPON_IDS = {WEAPON_ID}
EFFECT_IDS = {'203', '254', '282'}
ATTACK_SPRITES = [211, 426, 272, 308, 333]
ACTION_IDS = {str(sprite) for sprite, _ in POSES.values()} | set(map(str, ATTACK_SPRITES))


def timeline_frames(sprite):
    """Yield the persistent display list at each ShowFrame, including move tags."""
    display = {}
    for tag in sprite.find('subTags'):
        kind, depth = tag.get('type', ''), tag.get('depth')
        if kind.startswith('PlaceObject'):
            placed = deepcopy(display.get(depth, tag))
            placed.attrib.update(tag.attrib)
            for child in tag:
                previous = placed.find(child.tag)
                if previous is not None:
                    placed.remove(previous)
                placed.append(deepcopy(child))
            display[depth] = placed
        elif kind.startswith('RemoveObject'):
            display.pop(depth, None)
        elif kind == 'ShowFrameTag':
            yield deepcopy(display)


def matrix(tag):
    """Read a Flash transform in pixel units, preserving skew and scale."""
    m = tag.find('matrix')
    return [float(m.get('scaleX', '1')), float(m.get('rotateSkew0', '0')),
            float(m.get('rotateSkew1', '0')), float(m.get('scaleY', '1')),
            int(m.get('translateX', '0')) / 20, int(m.get('translateY', '0')) / 20]


def attack_manifest(root):
    """Retain the original hit windows; geometry will be fitted to weapon art."""
    tags = root.find('tags')
    result, start, effect_start = [], len(POSES), 0
    for index, sid in enumerate(ATTACK_SPRITES):
        sprite = next(t for t in tags if t.get('spriteId') == str(sid))
        windows = [any(tag.get('name') == 'stick' for tag in display.values())
                   for display in timeline_frames(sprite)]
        count = int(sprite.get('frameCount'))
        result.append({'name': f'attack_{index+1}', 'sprite': sid, 'start': start,
                       'effect_start': effect_start, 'count': count, 'hit_windows': windows})
        start += count
        effect_start += count
    return result


def freeze(sprite, frame):
    """Replace an equipment selector timeline with the chosen display state."""
    display = {}
    current = 1
    for tag in sprite.find('subTags'):
        kind = tag.get('type', '')
        depth = tag.get('depth')
        if kind.startswith('PlaceObject'):
            placed = deepcopy(display.get(depth, tag))
            placed.attrib.update(tag.attrib)
            for child in tag:
                previous = placed.find(child.tag)
                if previous is not None:
                    placed.remove(previous)
                placed.append(deepcopy(child))
            placed.set('placeFlagMove', 'false')
            placed.set('placeFlagHasCharacter', 'true')
            display[depth] = placed
        elif kind.startswith('RemoveObject'):
            display.pop(depth, None)
        elif kind == 'ShowFrameTag':
            if current == frame:
                break
            current += 1
    tags = sprite.find('subTags')
    tags.clear()
    for depth in sorted(display, key=int):
        tags.append(display[depth])
    ET.SubElement(tags, 'item', type='ShowFrameTag', forceWriteAsLong='false')
    sprite.set('frameCount', '1')


def export_filtered_attack(original, work, run, java, ffdec, env, transform):
    """Render attack three's filtered energy natively; SVG use filters clip its left edge."""
    tree = ET.parse(original)
    sprite = next(tag for tag in tree.getroot().find('tags') if tag.get('spriteId') == '272')
    depths = {depth for display in timeline_frames(sprite) for depth, tag in display.items()
              if tag.get('characterId') in EFFECT_IDS}
    tags = sprite.find('subTags')
    for tag in list(tags):
        if tag.get('type', '').startswith(('PlaceObject', 'RemoveObject')) and tag.get('depth') not in depths:
            tags.remove(tag)
    xml, swf = work / 'attack_effect.xml', work / 'attack_effect.swf'
    tree.write(xml, encoding='utf-8', xml_declaration=True)
    run('-xml2swf', str(xml), str(swf))
    output = work / 'attack_effect_png'
    run('-format', 'sprite:png', '-selectid', '272', '-ignorebackground', '-export', 'sprite', str(output), str(swf))
    command = [str(java), f'-Duser.home={work}', '--class-path', str(ffdec),
               str(Path(__file__).with_name('FlashSpriteBounds.java')), str(swf), '272']
    bounds_output = subprocess.check_output(command, env=env, text=True)
    bounds = next([int(value) / 20 for value in line.split()[1:]] for line in bounds_output.splitlines()
                  if len(line.split()) == 5 and line.split()[0] == '272')
    registered = work / 'attack_effect_registered'
    registered.mkdir(exist_ok=True)
    a, b, c, d, tx, ty = transform
    determinant = a * d - b * c
    inverse = [d / determinant, -c / determinant, -b / determinant, a / determinant]
    coefficients = (inverse[0], inverse[1], inverse[0] * (-200 - tx) + inverse[1] * (-178 - ty) - bounds[0],
                    inverse[2], inverse[3], inverse[2] * (-200 - tx) + inverse[3] * (-178 - ty) - bounds[1])
    for source in next(output.glob('DefineSprite_272_*')).glob('*.png'):
        image = Image.open(source).convert('RGBA')
        image = image.transform((400, 300), Image.Transform.AFFINE, coefficients, Image.Resampling.BICUBIC)
        image.save(registered / source.name)
    return {'272': {'input': str(registered), 'bounds': bounds}}


def main(args):
    """Build two body choices and two independently replaceable weapon choices."""
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'profile').mkdir(exist_ok=True)
    env = dict(os.environ, APPDATA=str(work / 'profile'))
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7':
        raise ValueError('Unexpected Role resource revision')
    readable = work / 'role.swf'
    readable.write_bytes(content)
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

    def run(*arguments):
        with (work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(arguments), env=env, stdout=log, stderr=subprocess.STDOUT, check=True)

    original = work / 'role.xml'
    run('-swf2xml', str(readable), str(original))
    # Child action sprites have different origins. Keep the parent's placement
    # matrix; cropping each sprite independently caused idle/walk/hurt jumps.
    root = ET.parse(original).getroot()
    symbols = next(t for t in root.find('tags') if t.get('type') == 'SymbolClassTag')
    symbol_ids = {n.text: t.text for n, t in zip(symbols.find('names'), symbols.find('tags'))}
    hero = next(t for t in root.find('tags') if t.get('spriteId') == symbol_ids['export.hero.Role1'])
    transforms = {}
    for tag in hero.find('subTags'):
        if tag.get('name') == 'body' and tag.get('characterId'):
            transforms[tag.get('characterId')] = matrix(tag)
    attacks = attack_manifest(root)
    poses = {str(i): list(pose) for i, pose in POSES.items()}
    for attack in attacks:
        poses.update({str(attack['start']+i): [attack['sprite'], i+1] for i in range(attack['count'])})
    manifest = {'source_sha256': hashlib.sha256(content).hexdigest(), 'variants': [],
                'transforms': transforms,
                'poses': poses, 'base_frame_map': BASE_FRAME_MAP,
                'columns': 6, 'rows': (len(poses) + 5) // 6, 'fps': float(root.get('frameRate')),
                'attacks': attacks}
    for name, cloth_frame, weapon_frame in [('unarmed', 1, 1), ('armed', 1, 2), ('armored', 2, 1)]:
        tree = ET.parse(original)
        for sprite in tree.getroot().find('tags'):
            if sprite.get('type') != 'DefineSpriteTag':
                continue
            sid = sprite.get('spriteId')
            if sid in CLOTH_IDS:
                freeze(sprite, cloth_frame)
            if sid == WEAPON_ID:
                freeze(sprite, weapon_frame)
        xml = work / f'{name}.xml'
        swf = work / f'{name}.swf'
        tree.write(xml, encoding='utf-8', xml_declaration=True)
        run('-xml2swf', str(xml), str(swf))
        out = work / name
        run('-format', 'sprite:svg', '-selectid', ','.join(sorted(ACTION_IDS, key=int)),
            '-ignorebackground', '-export', 'sprite', str(out), str(swf))
        directories = {p.name.split('_')[1]: str(p) for p in out.iterdir() if p.is_dir()}
        manifest['variants'].append({'name': name, 'input': directories,
            'output': str(args.output.resolve() / f'wukong_{name}.png')})
    path = work / 'manifest.json'
    path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    project = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
    # Split source vectors before rasterization. The weapon sheet stores only
    # visible segments, matching the legacy two-Sprite2D authoring convention.
    layers = []
    split_directories = {}
    for name, source_name, kind in [('body_bare', 'unarmed', 'body'), ('body_pilgrim', 'armored', 'body'),
                                    ('weapon_default', 'unarmed', 'weapon'), ('weapon_pilgrim', 'armed', 'weapon')]:
        if source_name not in split_directories:
            source_variant = next(v for v in manifest['variants'] if v['name'] == source_name)
            layer_dir = work / ('split_' + source_name)
            layer_dir.mkdir(exist_ok=True)
            directories = {}
            for sid, directory in source_variant['input'].items():
                target = layer_dir / sid
                target.mkdir(exist_ok=True)
                directories[sid] = str(target)
                for svg in Path(directory).glob('*.svg'):
                    split_pose(svg, target, transforms[sid])
            split_directories[source_name] = directories
        directories = split_directories[source_name]
        layers.append({'name': name, 'input': directories, 'kind': kind,
                       'output': str(args.output.resolve() / ('wukong_' + name + '.png'))})
    manifest['variants'] = layers
    manifest['effects_output'] = str((args.effects_output or args.output / 'wukong_attack_effects.png').resolve())
    manifest['effects_input'] = split_directories['unarmed']
    manifest['native_effects'] = export_filtered_attack(original, work, run, args.java, args.ffdec, env, transforms['272'])
    path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    subprocess.run([str(args.godot), '--headless', '--path', str(project), '--script',
        'res://EditorScripts/import_official_wukong.gd', '--', str(path)], env=env, check=True)
    print(f'Imported {len(manifest["variants"])} independent body/weapon atlases preserving Flash display depth; source {manifest["source_sha256"]}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['java', 'ffdec', 'godot', 'source', 'work', 'output']:
        parser.add_argument('--' + name, required=True, type=Path)
    parser.add_argument('--effects-output', type=Path, help='Independent effect atlas path; defaults to the output directory')
    main(parser.parse_args())
