"""Extract the first three original stages from FFDec XML and GameSence.as.

The source is the decoded v25928.swf documented in OfficialReference.md.
This reads native display-list placement and component properties; it does not
copy ActionScript behavior or overwrite authored Godot scenes.
"""
import argparse
import json
from pathlib import Path
import re
import xml.etree.ElementTree as E
from export_official_wukong import matrix, timeline_frames


def component_properties(source, frame):
    """Resolve Flash component inspector values for this scene frame."""
    result = {}
    for function in re.split(r'\n\s+(?:internal|private|public) function ', source)[1:]:
        interval = re.search(r'param1 >= (\d+) && param1 <= (\d+)', function)
        if not interval or not int(interval[1]) <= frame <= int(interval[2]):
            continue
        for name, field, value in re.findall(r'this\.(__id\d+_)\.(\w+) = ([^;]+);', function):
            if value in ('true', 'false'):
                value = value == 'true'
            elif re.fullmatch(r'-?\d+(?:\.\d+)?', value):
                value = float(value) if '.' in value else int(value)
            else:
                raise ValueError(f'Unrecognized component value: {value}')
            result.setdefault(name, {})[field] = value
    return result


def extract(args):
    """Retain source coordinates, identifiers, count, timing and battle bounds."""
    root = E.parse(args.xml).getroot()
    tags = root.find('tags')
    symbols = {}
    for item in tags:
        if item.get('type') == 'SymbolClassTag':
            symbols.update(zip((x.text for x in item.find('names')), (int(x.text) for x in item.find('tags'))))
    scene_id = symbols['export.GameSence']
    scene = next(t for t in tags if t.get('type') == 'DefineSpriteTag' and int(t.get('spriteId')) == scene_id)
    shapes = {int(t.get('shapeId')): t for t in tags if t.get('shapeId')}
    sprites = {int(t.get('spriteId')): t for t in tags if t.get('type') == 'DefineSpriteTag'}
    source = args.script.read_text(encoding='utf-8-sig')
    objects, frames, frame, label = {}, {}, 1, None
    for item in scene.find('subTags'):
        kind = item.get('type')
        if kind.startswith('PlaceObject'):
            depth = item.get('depth')
            previous = objects.get(depth, {}).copy() if item.get('placeFlagMove') == 'true' else {}
            if item.get('placeFlagHasCharacter') == 'true':
                previous['characterId'] = int(item.get('characterId'))
            previous['depth'] = int(depth)
            if item.get('placeFlagHasName') == 'true':
                previous['name'] = item.get('name')
            if item.find('matrix') is not None:
                previous['matrix'] = matrix(item)
                previous['position'] = previous['matrix'][4:]
                previous['scale'] = [previous['matrix'][0], previous['matrix'][3]]
            objects[depth] = previous
        elif kind.startswith('RemoveObject'):
            objects.pop(item.get('depth'), None)
        elif kind == 'FrameLabelTag':
            label = item.get('name')
        elif kind == 'ShowFrameTag':
            if label in ('1-1', '1-2', '1-3') and label not in frames:
                properties = component_properties(source, frame)
                entries = [{**value, 'properties': properties.get(value.get('name'), {})} for value in objects.values()]
                spawns = [x for x in entries if x['characterId'] == symbols['base.MonsterAppearPoint']]
                gates = [x for x in entries if x['characterId'] == symbols['export.StopPoint']]
                for gate in gates:
                    # StopPoint constructor sets a 1450px region for stage 1.
                    right = gate['position'][0]
                    gate['battleBounds'] = [round(max(0, right - 1450), 2), round(right, 2)]
                for spawn in spawns:
                    p = spawn['properties']
                    spawn['firstSpawnSeconds'] = p['delay'] + p['interval']
                if label == '1-1':
                    # Retain existing stable recipe IDs while replacing legacy coordinates.
                    names = ['__id1_', '__id2_', '__id3_', '__id5_', '__id12_', '__id14_', '__id15_',
                             '__id10_', '__id7_', '__id8_', '__id9_', '__id13_', '__id18_', '__id17_']
                    nodes = ['Stage1A', 'Stage2B', 'Stage2A', 'Stage2C', 'Stage3A', 'Stage3B', 'Stage3C',
                             'Stage3D', 'Stage4A', 'Stage4B', 'Stage4C', 'Stage5A', 'Stage5B', 'Stage5C']
                    node_names = dict(zip(names, nodes))
                    for spawn in spawns:
                        spawn['projectNode'] = node_names[spawn['name']]
                else:
                    counts = {}
                    for spawn in spawns:
                        stage = int(spawn['properties']['stopPointIdx']) + 1
                        index = counts.get(stage, 0)
                        counts[stage] = index + 1
                        spawn['projectNode'] = f'Stage{stage}{chr(65+index)}'
                geometry = []
                for entry in entries:
                    sid = entry['characterId']
                    if sid not in (53, 57, 61):
                        continue
                    part = next(x for x in sprites[sid].find('subTags') if x.get('characterId') == '52')
                    bounds = shapes[52].find('shapeBounds')
                    inner, outer = matrix(part), entry['matrix']
                    corners = []
                    for x, y in [('Xmin', 'Ymin'), ('Xmax', 'Ymin'), ('Xmax', 'Ymax'), ('Xmin', 'Ymax')]:
                        px, py = float(bounds.get(x)) / 20, float(bounds.get(y)) / 20
                        lx, ly = inner[0]*px + inner[2]*py + inner[4], inner[1]*px + inner[3]*py + inner[5]
                        corners.append([round(outer[0]*lx + outer[2]*ly + outer[4], 5),
                                        round(outer[1]*lx + outer[3]*ly + outer[5], 5)])
                    geometry.append({'sourceCharacter': sid, 'oneWay': sid == 57, 'corners': corners})
                backgrounds = list(timeline_frames(sprites[symbols['bgClips']]))
                background = next(iter(backgrounds[{'1-1': 0, '1-2': 5, '1-3': 10}[label]].values()))
                child = next(x for x in sprites[int(background.get('characterId'))].find('subTags')
                             if x.get('characterId'))
                bg_shape = shapes[int(child.get('characterId'))].find('shapeBounds')
                bg_matrix = matrix(background)
                container = next(entry for entry in entries if entry.get('name') == 'bgContainer')['matrix']
                child_matrix = matrix(child)
                # GameSence adds bgClips at (0,65); retain the nested shape registration too.
                bg_position = [round(container[4] + bg_matrix[4] + child_matrix[4] + float(bg_shape.get('Xmin'))/20, 4),
                               round(container[5] + 65 + bg_matrix[5] + child_matrix[5] + float(bg_shape.get('Ymin'))/20, 4)]
                artwork = []
                visible = {56: 'huaguoshan_platform', 60: 'huaguoshan_ground',
                           88: 'shuiliandong_platform', 97: 'shuiliandong_ground',
                           101: 'taohuayuan_ground', 103: 'taohuayuan_rock'}
                for entry in entries:
                    sid = entry['characterId']
                    if sid not in visible:
                        continue
                    shape = shapes[sid] if sid in shapes else shapes[int(next(
                        tag for tag in sprites[sid].find('subTags') if tag.get('characterId')).get('characterId'))]
                    bounds = shape.find('shapeBounds')
                    artwork.append({'asset': visible[sid], 'sourceCharacter': sid, 'depth': entry['depth'],
                                    'matrix': entry['matrix'], 'bounds':
                                    [float(bounds.get(key))/20 for key in ['Xmin', 'Ymin', 'Xmax', 'Ymax']]})
                frames[label] = {'sourceFrame': frame, 'spawns': spawns, 'gates': gates,
                    'geometry': geometry, 'backgroundPosition': bg_position,
                    'backgroundCharacter': int(background.get('characterId')), 'artwork': artwork,
                    'skyPosition': [0, 0],
                    'scenery': [x for x in entries if x not in spawns and x not in gates]}
                if label == '1-2':
                    def rectangle(sid):
                        bounds = shapes[sid].find('shapeBounds')
                        return [float(bounds.get(k))/20 for k in ('Xmin', 'Ymin', 'Xmax', 'Ymax')]
                    frames[label]['river'] = {'maskBounds': rectangle(90), 'waveBounds': rectangle(92),
                        'fps': float(root.get('frameRate')),
                        'positions': [matrix(next(iter(display.values())))[4:] for display in timeline_frames(sprites[94])]}
            frame += 1
    if len(frames) != 3:
        raise ValueError('Expected all three original stages')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(frames, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for label, data in frames.items():
        print(label, 'frame', data['sourceFrame'], 'monsters', sum(x['properties']['totalNum'] for x in data['spawns']),
              'enemyTypes', sorted({x['properties']['enemyType'] for x in data['spawns']}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--xml', required=True, type=Path)
    parser.add_argument('--script', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    extract(parser.parse_args())
