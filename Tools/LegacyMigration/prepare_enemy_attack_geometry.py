"""Review the new chapter attacks as a few native shapes, not bitmap canvases.

Only the explicitly reviewed five attacks and Peng's three flames are emitted.
Pixels and source vector curves are sampled offline to measure basic shapes;
no pixel polygons, fitting code, or manifest interpreter enters the game.
"""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import xml.etree.ElementTree as E
import zlib

import numpy as np
from PIL import Image, ImageDraw

from export_official_wukong import matrix
from prepare_official_garuda_level import _compose, _probe_reader


def rectangle(points, padding=2):
    """Use one principal-axis rectangle, retaining the smaller axis-aligned fit."""
    if len(points) == 0:
        return None
    covariance = np.cov(points, rowvar=False) if len(points) > 2 else np.eye(2)
    angle = .5 * math.atan2(2 * covariance[0, 1], covariance[0, 0] - covariance[1, 1])
    candidates = []
    for rotation in (0, angle):
        axes = np.array([[math.cos(rotation), -math.sin(rotation)], [math.sin(rotation), math.cos(rotation)]])
        local = points @ axes
        low, high = local.min(axis=0), local.max(axis=0)
        candidates.append(dict(kind='rectangle', position=(axes @ ((low + high) / 2)).tolist(),
                               size=np.maximum(high - low + padding * 2, 4).tolist(), rotation=rotation))
    return min(candidates, key=lambda item: math.prod(item['size']))


def prepare(args):
    """Retain named probe time, complete parent transforms, and clip-depth masks."""
    root = Path(__file__).resolve().parents[2]
    source = root / 'Godot/GodotProject/.godot/validation/official_continuation/monster2_full.xml'
    tags = E.parse(source).getroot().find('tags')
    definitions = {int(t.get('shapeId') or t.get('spriteId')): t for t in tags if t.get('shapeId') or t.get('spriteId')}
    bitmaps = {int(t.get('characterID')): t for t in tags if t.get('characterID')}
    frames, bounds = _probe_reader(tags)
    cache = {}

    def transform(points, value):
        a, b, c, d, x, y = value
        return points @ np.array([[a, b], [c, d]]) + [x, y]

    def shape_points(character):
        if character in cache:
            return cache[character]
        tag = definitions[character]
        fill = next((item for item in tag.iter('item') if item.get('bitmapId') not in (None, '65535')), None)
        if fill is not None:
            bitmap = bitmaps[int(fill.get('bitmapId'))]
            if bitmap.get('type') == 'DefineBitsLossless2Tag':
                assert bitmap.get('bitmapFormat') == '5'
                pixels = np.frombuffer(zlib.decompress(bytes.fromhex(bitmap.get('zlibBitmapData'))), np.uint8)
                alpha = pixels.reshape(int(bitmap.get('bitmapHeight')), int(bitmap.get('bitmapWidth')), 4)[:, :, 0]
            else:
                image = Image.open(io.BytesIO(bytes.fromhex(bitmap.get('imageData'))))
                alpha = np.frombuffer(zlib.decompress(bytes.fromhex(bitmap.get('bitmapAlphaData'))), np.uint8).reshape(image.height, image.width)
            ys, xs = np.where(alpha >= 26)
            item = fill.find('bitmapMatrix')
            value = [float(item.get(key, default)) for key, default in
                     [('scaleX', '1'), ('rotateSkew0', '0'), ('rotateSkew1', '0'),
                      ('scaleY', '1'), ('translateX', '0'), ('translateY', '0')]]
            value[4:] = [item / 20 for item in value[4:]]
            value[:4] = [item / 20 for item in value[:4]]
            points = transform(np.column_stack((xs + .5, ys + .5)), value)
            rect = bounds(character, 0, [1, 0, 0, 1, 0, 0])
            points = points[(points[:, 0] >= rect[0]) & (points[:, 0] <= rect[0] + rect[2]) &
                            (points[:, 1] >= rect[1]) & (points[:, 1] <= rect[1] + rect[3])]
        else:
            # The Flying Demon probe is a thin lens made of two quadratic arcs.
            # Sampling it measures a rotated rectangle; the curves never become physics polygons.
            values, current = [], np.zeros(2)
            for record in tag.findall('.//shapeRecords/item'):
                kind = record.get('type')
                if kind == 'StyleChangeRecord' and record.get('stateMoveTo') == 'true':
                    current = np.array([float(record.get('moveDeltaX', 0)), float(record.get('moveDeltaY', 0))]) / 20
                    values.append(current.copy())
                elif kind == 'StraightEdgeRecord':
                    current += np.array([float(record.get('deltaX', 0)), float(record.get('deltaY', 0))]) / 20
                    values.append(current.copy())
                elif kind == 'CurvedEdgeRecord':
                    control = current + np.array([float(record.get('controlDeltaX')), float(record.get('controlDeltaY'))]) / 20
                    end = control + np.array([float(record.get('anchorDeltaX')), float(record.get('anchorDeltaY'))]) / 20
                    for t in np.linspace(0, 1, 33):
                        values.append((1-t)**2 * current + 2*t*(1-t)*control + t*t*end)
                    current = end
            points = np.array(values)
        cache[character] = points
        return points

    def probe_points(character, age, value):
        if definitions[character].get('type') != 'DefineSpriteTag':
            return transform(shape_points(character), value)
        history = frames(character)
        held = age if len(history) == 1 else 0
        output, masks = [], []
        for child, child_age in sorted(history[age % len(history)], key=lambda pair: int(pair[0].get('depth'))):
            depth = int(child.get('depth'))
            masks = [(end, box) for end, box in masks if end >= depth]
            child_id, child_matrix = int(child.get('characterId')), _compose(value, matrix(child))
            if child.get('clipDepth'):
                masks.append((int(child.get('clipDepth')), bounds(child_id, child_age + held, child_matrix)))
                continue
            points = probe_points(child_id, child_age + held, child_matrix)
            for _, box in masks:
                points = points[(points[:, 0] >= box[0]) & (points[:, 0] <= box[0]+box[2]) &
                                (points[:, 1] >= box[1]) & (points[:, 1] <= box[1]+box[3])] if box else np.empty((0, 2))
            if len(points):
                output.append(points)
        return np.concatenate(output) if output else np.empty((0, 2))

    base = root / 'Godot/GodotProject/.godot/validation/remaining_levels'
    specs = [('garuda_art/native_manifest.json', 'flying_demon', 'attack_1'),
             ('garuda_art/native_manifest.json', 'peng_demon_king', 'attack_2'),
             ('garuda_art/native_manifest.json', 'peng_demon_king', 'attack_4'),
             ('sea_art/actors_registered/manifest.json', 'tide_demon', 'attack_2'),
             ('east_sea_art/actors/manifest.json', 'lantern_fish', 'attack_1')]
    report = {'source_xml_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'attacks': []}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    for file, name, action_name in specs:
        data = json.loads((base/file).read_text(encoding='utf-8'))
        actor = next(item for item in data['actors'] if item['name'] == name)
        action = next(item for item in actor['actions'] if item['name'] == action_name)
        shapes, point_sets = [], []
        for index, display in enumerate(frames(action['sprite'])):
            sets = [probe_points(int(child.get('characterId')), age, _compose(action['matrix'], matrix(child)))
                    for child, age in display if child.get('name') == 'stick']
            points = np.concatenate(sets) if sets else np.empty((0, 2))
            point_sets.append(points)
            shape = rectangle(points)
            if shape and name in ('lantern_fish', 'tide_demon'):
                # One rounded shape excludes the square's empty corners without splitting the arc into many boxes.
                low, high = points.min(axis=0), points.max(axis=0)
                center = (low + high) / 2
                radius = max(np.linalg.norm(points - center, axis=1).max(), 2) + 1
                shape = dict(kind='circle', position=center.tolist(), radius=float(radius), rotation=0)
            shapes.append([shape] if shape else [])
        report['attacks'].append(dict(actor=name, animation=action_name, frames=action['frames'], shapes=shapes))
        _preview(args.output.parent, name, action, shapes)
        print(name, action_name, 'active frames', sum(bool(x) for x in shapes),
              'max extent', max((max(s[0].get('size', [s[0].get('radius', 0)*2])) for s in shapes if s), default=0))

    data = json.loads((base/'garuda_art/native_manifest.json').read_text(encoding='utf-8'))
    effect = next(item for item in data['effects'] if item['name'] == 'peng_fire')
    action = effect['actions'][0]
    wrappers = frames(action['sprite'])[0]
    shapes = [[rectangle(probe_points(int(child.get('characterId')), index, matrix(child)), padding=1)
               for child, _ in wrappers] for index in range(action['frames'])]
    report['attacks'].append(dict(actor='peng_fire', animation='play', frames=action['frames'], fade_seconds=.6, shapes=shapes))
    _preview(args.output.parent, 'peng_fire', action, shapes)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('ENEMY ATTACK REVIEW: 5 actions and three independent fire regions; native authoring plan only')


def _preview(output, name, action, frames):
    """Keep source registration while overlaying the proposed simple shapes."""
    active = [i for i, shapes in enumerate(frames) if shapes]
    chosen = sorted(set(active[round(t * (len(active)-1) / 7)] for t in range(8)))
    result = Image.new('RGB', (1600, 740), (35, 38, 43))
    draw = ImageDraw.Draw(result)
    for column, index in enumerate(chosen):
        image = Image.open(Path(action['input'])/f'{index+1}.png').convert('RGBA')
        canvas = Image.new('RGBA', (2000, 1600))
        origin = np.array([1300, 850])
        offset = np.floor(np.array(action['bounds'][:2]) + action['matrix'][4:]).astype(int)
        canvas.alpha_composite(image, tuple(origin + offset))
        overlay = ImageDraw.Draw(canvas)
        for shape in frames[index]:
            if not shape:
                continue
            center = np.array(shape['position']) + origin
            if shape['kind'] == 'circle':
                r = shape['radius']; overlay.ellipse((*tuple(center-r), *tuple(center+r)), outline='#40ff80', width=2)
            else:
                x, y = np.array(shape['size']) / 2
                angle = shape['rotation']; rotation = np.array([[math.cos(angle), math.sin(angle)], [-math.sin(angle), math.cos(angle)]])
                corners = np.array([[-x,-y],[x,-y],[x,y],[-x,y]]) @ rotation + center
                overlay.line([tuple(p) for p in np.vstack((corners, corners[0]))], fill='#40ff80', width=2)
        used = canvas.getbbox()
        crop = canvas.crop(used); crop.thumbnail((380, 325))
        x, y = column%4*400+(400-crop.width)//2, column//4*370+30+(325-crop.height)//2
        result.paste(crop, (x, y), crop)
        draw.text((column%4*400+8, column//4*370+8), f'{name} {action["name"]} frame {index+1}', fill='white')
    result.save(output/f'{name}_{action["name"]}_review.jpg')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    prepare(parser.parse_args())
