"""Export registered coastal art; preserve native glow sampling at high density."""
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
        if args.only and name not in args.only:
            continue
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
    rendered_by_density = {}
    sampling = []
    for density in sorted({1, args.density}):
        render_source = swf
        if density > 1:
            dense_tree = deepcopy(tree)
            # FFDec Glow delegates to the same drop-shadow primitive with zero distance.
            # Match its native integer kernel and pixel-center phase at the new density.
            # Only the temporary raster input is adapted; source art and keyframes stay intact.
            for sprite in dense_tree.getroot().find('tags'):
                if sprite.get('spriteId') not in ('458', '459', '465', '467'):
                    continue
                frame = 0
                for placement in sprite.find('subTags'):
                    if placement.get('type') == 'ShowFrameTag':
                        frame += 1
                    for node in placement.iter():
                        if node.get('type') != 'GLOWFILTER':
                            continue
                        original = dict(node.attrib)
                        rx = math.floor(float(node.get('blurX')) + 0.5)
                        ry = math.floor(float(node.get('blurY')) + 0.5)
                        assert rx == ry and node.get('passes') == '1'
                        assert node.get('innerGlow') == 'false' and node.get('knockout') == 'true'
                        # An even native kernel is centered half a native pixel away.
                        # An even dense kernel is centered half an output pixel away.
                        phase = (0.5 if rx % 2 == 0 else 0) - 0.5 / density
                        angle = math.radians(45 if phase > 0 else 225)
                        # SWF FIXED quantization must retain FFDec's integer-degree angle.
                        angle = math.ceil(angle * 65536) / 65536
                        node.set('type', 'DROPSHADOWFILTER')
                        node.set('id', '0')
                        node.set('blurX', str(rx))
                        node.set('blurY', str(ry))
                        node.set('angle', str(angle))
                        node.set('distance', str(math.sqrt(2) * abs(phase)))
                        node.set('innerShadow', node.attrib.pop('innerGlow'))
                        node.find('glowColor').tag = 'dropShadowColor'
                        sampling.append({'sprite': sprite.get('spriteId'), 'frame': frame,
                            'source_glow': original, 'native_kernel_pixels': [rx, ry],
                            'phase_logical_pixels': [phase, phase],
                            'color_strength_passes_knockout_preserved': True})
            dense_xml = args.work / f'registered_{density}x.xml'
            dense_tree.write(dense_xml, encoding='utf-8', xml_declaration=True)
            render_source = args.work / f'registered_{density}x.swf'
            run('-xml2swf', str(dense_xml), str(render_source))
        png = args.work / ('png' if density == 1 else f'png_{density}x')
        # The work cap is proportional to radiusX*radiusY*width*height.
        # Preserve the native per-logical-pixel budget; no global config is changed.
        run('-config', f'boxBlurPixelsLimit={1000 * density ** 4}',
            '-selectid', ','.join(map(str, exports)), '-zoom', str(density),
            '-ignorebackground', '-export', 'sprite', str(png), str(render_source))
        rendered_by_density[density] = {int(p.name.split('_')[1]): p for p in png.iterdir() if p.is_dir()}
    for location in locations:
        rendered = {}
        native = {}
        bounds = []
        for state, data in location['states'].items():
            for density in sorted({1, args.density}):
                folder = rendered_by_density[density][data['id']]
                frames = [Image.open(folder / f'{i + 1}.png').convert('RGBA') for i in range(data['frames'])]
                # Keep the exact 1x registration; higher density changes only sampling.
                width, height = 1000 * density, 600 * density
                frames = [image.crop(((image.width - width) // 2, (image.height - height) // 2,
                                      (image.width + width) // 2, (image.height + height) // 2)) for image in frames]
                if density == 1:
                    native[state] = frames
                    bounds.extend(image.getbbox() for image in frames if image.getbbox())
                if density == args.density:
                    rendered[state] = frames
        box = (min(b[0] for b in bounds) - 12, min(b[1] for b in bounds) - 12,
               max(b[2] for b in bounds) + 12, max(b[3] for b in bounds) + 12)
        logical_size = (box[2] - box[0], box[3] - box[1])
        size = tuple(value * args.density for value in logical_size)
        pixel_box = tuple(value * args.density for value in box)
        location['size'] = size
        location['logical_size'] = logical_size
        location['density'] = args.density
        location['offset'] = [box[0] - 500, box[1] - 300]
        name = location['name']
        # Input geometry remains at native logical density, independent of artwork density.
        native['hit'][0].crop(box).save(args.work / f'{name}_hit.png')
        # The invisible normal and its input mask keep their native logical density.
        Image.new('RGBA', logical_size).save(args.output / f'map_{name}_normal.png')
        rendered['hover'][0].crop(pixel_box).save(args.output / f'map_{name}_hover.png')
        current_frames = [frame.crop(pixel_box) for frame in rendered['current']]
        crop = (0, 0, size[0], size[1])
        if args.density == 2:
            # One common transparent crop preserves every frame's original registration.
            # Four output pixels protect bilinear sampling; mipmaps remain disabled.
            visible = [frame.getbbox() for frame in current_frames]
            if any(box is None for box in visible):
                raise ValueError(f'Empty current frame: {name}')
            crop = [min(box[0] for box in visible) - 4, min(box[1] for box in visible) - 4,
                    max(box[2] for box in visible) + 4, max(box[3] for box in visible) + 4]
            crop[2] += (crop[2] - crop[0]) % 2
            crop[3] += (crop[3] - crop[1]) % 2
            if crop[0] < 0 or crop[1] < 0 or crop[2] > size[0] or crop[3] > size[1]:
                raise ValueError(f'Current crop exceeds registered canvas: {name}')
        cells = [frame.crop(crop) for frame in current_frames]
        cell_width, cell_height = cells[0].size
        # Keep all 19 color frames. East Sea needs two native animation texture pages.
        columns = 2 if args.density == 2 and name == 'donghai' else 5
        rows = 5 if args.density == 2 and name == 'donghai' else math.ceil(len(cells) / columns)
        capacity = columns * rows
        pages = []
        for first in range(0, len(cells), capacity):
            atlas = Image.new('RGBA', (cell_width * columns, cell_height * rows))
            for index, frame in enumerate(cells[first:first + capacity]):
                atlas.paste(frame, (index % columns * cell_width, index // columns * cell_height))
            suffix = '' if first == 0 else f'_{len(pages) + 1}'
            filename = f'map_{name}_current{suffix}.png'
            atlas.save(args.output / filename)
            pages.append({'file': filename, 'first_source_frame': first,
                          'frames': min(capacity, len(cells) - first), 'size': atlas.size,
                          'sha256': hashlib.sha256((args.output / filename).read_bytes()).hexdigest()})
        location['current'] = {
            'columns': columns, 'rows': rows, 'cell_size': [cell_width, cell_height],
            'crop': crop, 'offset': [(crop[0] + crop[2] - size[0]) / 2,
                                    (crop[1] + crop[3] - size[1]) / 2],
            'pages': pages,
            'frames': [{'page': index // capacity, 'cell': index % capacity}
                       for index in range(len(cells))]}
    (args.work / 'manifest.json').write_text(json.dumps({'source_sha256': digest, 'density': args.density,
                                                        'ffdec_box_blur_pixels_limit': 1000 * args.density ** 4,
                                                        'glow_native_sampling': sampling,
                                                        'fps': 24, 'locations': locations}, indent=2), encoding='utf-8')
    print(f'OFFICIAL COAST EXPORT PASS: {len(locations)} locations, density {args.density}, registered states, parent scale, native filters and original matrices')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ('source', 'java', 'ffdec', 'work', 'output'):
        parser.add_argument('--' + option, type=Path, required=True)
    parser.add_argument('--density', type=int, choices=(1, 2), default=1,
                        help='Rasterize the source at this density; 1 preserves the original export contract')
    parser.add_argument('--only', nargs='+', choices=('qianan', 'donghai', 'shazhiwoliu'),
                        help='Export only the named original locations')
    args = parser.parse_args()
    if args.density == 2 and (not args.only or 'shazhiwoliu' in args.only):
        parser.error('Density 2 is approved only for qianan/donghai; pass --only explicitly')
    export(args)
