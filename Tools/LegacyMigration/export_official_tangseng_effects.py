"""Export official Role2 detached effects with native frame registration.

FFDec performs the rasterization, including source filters. The SVG export is
used only to record its original frame origin; no replacement artwork is drawn.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import xml.etree.ElementTree as ET
from copy import deepcopy
from PIL import Image

from audit_official_tangseng import ROLE_HASH
from export_official_wukong import timeline_frames
from prepare_official_garuda_level import compact_registered_atlas


CLIPS = [
    ('normal_bolt', 611, 1),
    ('ice_dragon_bolt', 608, 1),
    ('dark_ice', 604, 46),
    ('water_demon_fall', 657, 25),
    ('cure_role', 702, 34),
    ('water_demon_ball', 951, 20),
]

# User-requested visual cleanup: these five imported transition drawings are
# outside the ice-spike clips. They occur on source frames 5 through 11.
DARK_ICE_TRANSITIONS = {'595', '597', '599', '601', '603'}


def clean_placement(placed):
    """Merged display lists must not carry PlaceObject3-only fields into type2."""
    if placed.get('type') == 'PlaceObject2Tag':
        fields = {'type', 'characterId', 'depth', 'forceWriteAsLong', 'name', 'ratio', 'clipDepth',
                  'placeFlagHasCharacter', 'placeFlagHasClipActions', 'placeFlagHasClipDepth',
                  'placeFlagHasColorTransform', 'placeFlagHasMatrix', 'placeFlagHasName',
                  'placeFlagHasRatio', 'placeFlagMove'}
        for key in list(placed.attrib):
            if key not in fields:
                del placed.attrib[key]
        for child in list(placed):
            if child.tag not in ('matrix', 'colorTransform', 'clipActions'):
                placed.remove(child)
    return placed


def install_atlases(entries, output):
    """Reuse registered atlas packing; virtual canvases retain source registration."""
    output.mkdir(parents=True, exist_ok=True)
    packed = []
    for entry in entries:
        destination = output if entry['name'] == 'normal_bolt' else output / 'skills'
        destination.mkdir(parents=True, exist_ok=True)
        frames = entry['frames']
        images = []
        for frame in frames:
            raster = Path(frame['path'])
            if hashlib.sha256(raster.read_bytes()).hexdigest() != frame['sha256']:
                raise ValueError('Reviewed native pixels changed: ' + str(raster))
            image = Image.open(raster).convert('RGBA')
            if entry['name'] == 'cure_role':
                # ADD uses RGB * alpha. Move intensity from RGB into alpha,
                # retaining that light while making the black matte transparent.
                # This is specific to the source 703 blendMode=8 placement.
                pixels = []
                for r, g, b, a in image.getdata():
                    peak = max(r, g, b)
                    pixels.append((round(r * 255 / peak), round(g * 255 / peak),
                                   round(b * 255 / peak), round(a * peak / 255))
                                  if peak else (0, 0, 0, 0))
                image.putdata(pixels)
            images.append(image)
        width, height = images[0].size
        if any(image.size != (width, height) for image in images):
            raise ValueError('Source frames do not share one registration canvas')
        if len(images) == 1:
            # Bolt scenes bind a Texture2D directly, retaining their source canvas.
            images[0].save(destination / ('tangseng_' + entry['name'] + '.png'))
            continue
        unique, cells, hashes = [], [], {}
        for image in images:
            digest = hashlib.sha256(image.tobytes()).hexdigest()
            if digest not in hashes:
                hashes[digest] = len(unique)
                unique.append(image)
            cells.append(hashes[digest])
        columns = min(6, len(unique))
        atlas = Image.new('RGBA', (columns * width, math.ceil(len(unique) / columns) * height))
        prefix = 'tangseng_' + entry['name']
        for index, raster in enumerate(unique):
            atlas.paste(raster, ((index % columns) * width, (index // columns) * height))
        atlas.save(destination / (prefix + '_atlas.png'))
        layer = {'output': str(destination / (prefix + '_atlas.png')), 'cell_size': [width, height],
                 'columns': columns, 'unique_cells': len(unique)}
        compact_registered_atlas(layer)
        lines = ['[gd_resource type="SpriteFrames" format=3]', '',
                 f'[ext_resource type="Texture2D" path="res://TheGame/Sprites/Effects/tangseng/skills/{prefix}_atlas.png" id="atlas"]', '']
        for index in range(len(unique)):
            placement = layer['atlas_regions'][str(index)]
            region = ', '.join(map(str, placement['region']))
            margin = ', '.join(map(str, placement['margin']))
            lines += [f'[sub_resource type="AtlasTexture" id="frame_{index}"]', 'atlas = ExtResource("atlas")',
                      f'region = Rect2({region})', f'margin = Rect2({margin})', 'filter_clip = true', '']
        keys = ', '.join('{"duration": 1.0, "texture": SubResource("frame_' + str(cell) + '")}'
                         for cell in cells)
        lines += ['[resource]', 'animations = [{"frames": [' + keys + '], "loop": false, "name": &"play", "speed": 24.0}]', '']
        (destination / (prefix + '_frames.tres')).write_text('\n'.join(lines), encoding='utf-8')
        packed.append({'name': entry['name'], 'frames': cells, **layer})
    return packed


def export(args):
    """Keep all frame origins and source identities in a reproducible manifest."""
    if hashlib.sha256(args.role.read_bytes()).hexdigest() != ROLE_HASH:
        raise ValueError('Unexpected official Role_v7 revision')
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, APPDATA=str(work / 'profile'))
    selected = ','.join(str(sid) for _, sid, _ in CLIPS)
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec),
               '-selectid', selected, '-ignorebackground']
    source = args.role.resolve()
    clips = CLIPS if not args.only else [clip for clip in CLIPS if clip[0] in args.only]
    render_ids = {name: sid for name, sid, _ in clips}
    if args.xml and 'water_demon_ball' in render_ids:
        # 951 is white source artwork. Retain its actual 965 parent filters and
        # vertical squash instead of approximating the cyan glow in Godot.
        tree = ET.parse(args.xml)
        tags = tree.getroot().find('tags')
        actor = next(tag for tag in tags if tag.get('spriteId') == '965')
        placement = deepcopy(next(tag for display in timeline_frames(actor)
                                  for tag in display.values() if tag.get('name') == 'roundMc'))
        placement.find('matrix').set('translateX', '0')
        placement.find('matrix').set('translateY', '0')
        wrapper_id = max(int(tag.get('spriteId', '0')) for tag in tags) + 1
        wrapper = ET.SubElement(tags, 'item', type='DefineSpriteTag', spriteId=str(wrapper_id),
                                frameCount='20', hasEndTag='true', forceWriteAsLong='true')
        timeline = ET.SubElement(wrapper, 'subTags')
        timeline.append(placement)
        for _ in range(20):
            ET.SubElement(timeline, 'item', type='ShowFrameTag', forceWriteAsLong='false')
        prepared = work / 'role2_ball_parent.xml'
        tree.write(prepared, encoding='utf-8', xml_declaration=True)
        source = work / 'role2_ball_parent.swf'
        subprocess.run([str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec),
                        '-xml2swf', str(prepared), str(source)], env=environment, check=True,
                       stdout=subprocess.DEVNULL)
        render_ids['water_demon_ball'] = wrapper_id
    if args.xml and 'dark_ice' in render_ids:
        tree = tree if 'water_demon_ball' in render_ids else ET.parse(args.xml)
        tags = tree.getroot().find('tags')
        sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('spriteId')}
        source_frames = list(timeline_frames(sprites[593]))
        next_id = max(sprites) + 1
        frozen = []
        # The official ice spike stops at frame30. FFDec's default static frame
        # export loops nested MovieClips and ignores that ActionScript stop.
        for display in source_frames:
            sid = next_id
            next_id += 1
            frozen.append(sid)
            clip = ET.SubElement(tags, 'item', type='DefineSpriteTag', spriteId=str(sid),
                                 frameCount='1', hasEndTag='true', forceWriteAsLong='true')
            timeline = ET.SubElement(clip, 'subTags')
            for tag in display.values():
                placed = clean_placement(deepcopy(tag))
                placed.set('placeFlagMove', 'false')
                placed.set('placeFlagHasCharacter', 'true')
                timeline.append(placed)
            ET.SubElement(timeline, 'item', type='ShowFrameTag', forceWriteAsLong='false')
        displays = list(timeline_frames(sprites[604]))
        timeline = sprites[604].find('subTags')
        timeline.clear()
        born, previous = {}, set()
        for frame, display in enumerate(displays):
            display = {depth: tag for depth, tag in display.items()
                       if tag.get('characterId') not in DARK_ICE_TRANSITIONS}
            for depth in previous:
                ET.SubElement(timeline, 'item', type='RemoveObject2Tag', depth=depth)
            for depth, tag in display.items():
                placed = clean_placement(deepcopy(tag))
                if placed.get('characterId') == '593':
                    born.setdefault(depth, frame)
                    placed.set('characterId', str(frozen[min(frame - born[depth], len(frozen) - 1)]))
                placed.set('placeFlagMove', 'false')
                placed.set('placeFlagHasCharacter', 'true')
                timeline.append(placed)
            previous = set(display)
            ET.SubElement(timeline, 'item', type='ShowFrameTag', forceWriteAsLong='false')
        prepared = work / 'role2_effect_timelines.xml'
        tree.write(prepared, encoding='utf-8', xml_declaration=True)
        source = work / 'role2_effect_timelines.swf'
        subprocess.run([str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec),
                        '-xml2swf', str(prepared), str(source)], env=environment, check=True,
                       stdout=subprocess.DEVNULL)
    command[command.index('-selectid') + 1] = ','.join(str(render_ids[name]) for name, _, _ in clips)
    with (work / 'export.log').open('w', encoding='utf-8') as log:
        for kind in ('svg', 'png'):
            subprocess.run(command + ['-format', 'sprite:' + kind, '-export', 'sprite',
                           str(work / kind), str(source)], env=environment,
                           stdout=log, stderr=subprocess.STDOUT, check=True)
    folders = {}
    for kind in ('svg', 'png'):
        folders[kind] = {int(re.match(r'DefineSprite_(\d+)', p.name)[1]): p
                         for p in (work / kind).iterdir() if p.is_dir()}
    entries = []
    for name, sid, count in clips:
        frames = []
        for frame in range(1, count + 1):
            raster = folders['png'][render_ids[name]] / f'{frame}.png'
            vector = folders['svg'][render_ids[name]] / f'{frame}.svg'
            if not raster.is_file() or not vector.is_file():
                raise ValueError(f'Incomplete official effect: {name}/{frame}')
            width, height = struct.unpack('>II', raster.read_bytes()[16:24])
            svg = ET.parse(vector).getroot()
            group = svg.find('{http://www.w3.org/2000/svg}g')
            transform = [float(value) for value in re.findall(r'[-+]?\d*\.?\d+(?:[Ee][-+]?\d+)?',
                                                             group.get('transform'))]
            if transform[:4] != [1, 0, 0, 1]:
                raise ValueError('Unexpected FFDec canvas transform')
            canvas = [math.ceil(float(svg.get(key).removesuffix('px')))
                      for key in ('width', 'height')]
            padding = [(width - canvas[0]) / 2, (height - canvas[1]) / 2]
            if any(value < 0 for value in padding):
                raise ValueError('Raster canvas is smaller than the audited SVG canvas')
            filename = f'tangseng_{name}_{frame:02}.png'
            output = work / 'native' / filename
            output.parent.mkdir(exist_ok=True)
            shutil.copyfile(raster, output)
            frames.append({'frame': frame, 'path': str(output), 'size': [width, height],
                           'raster_padding': padding,
                           'offset': [width / 2 - transform[4] - padding[0],
                                      height / 2 - transform[5] - padding[1]],
                           'sha256': hashlib.sha256(output.read_bytes()).hexdigest()})
        entries.append({'name': name, 'sprite': sid, 'rendered_sprite': render_ids[name], 'frames': frames,
                        'parent_offset': [-4.5, -24.5] if sid == 702 else [0, 0]})
    manifest = {'source_sha256': ROLE_HASH, 'clips': entries}
    (work / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    if args.install_to:
        packed = install_atlases(entries, args.install_to)
        (work / 'packed_manifest.json').write_text(json.dumps(packed, indent=2) + '\n', encoding='utf-8')
    print('OFFICIAL TANG EFFECTS EXPORTED:', sum(len(entry['frames']) for entry in entries),
          'frames from', len(entries), 'clips; registration recorded; gameplay not certified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('role', 'java', 'ffdec', 'work'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--xml', type=Path, help='Official XML for retaining the ball parent filters and squash')
    parser.add_argument('--only', nargs='+', choices=[name for name, _, _ in CLIPS])
    parser.add_argument('--install-to', type=Path, help='Install only the selected reviewed atlases and SpriteFrames')
    export(parser.parse_args())
