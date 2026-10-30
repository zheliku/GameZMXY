"""Build a reviewable authoring plan for official ZMXY1 level 2-3.

The plan joins the verified Flash level extraction, current source tables,
staged actor manifests and native music. It never edits runtime resources.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import xml.etree.ElementTree as ET

from PIL import Image

from audit_official_level_dependencies import xlsx_rows
from export_official_wukong import matrix, timeline_frames


def _compose(parent, child):
    """Retain scale/skew as well as translation when locating gameplay probes."""
    a, b, c, d, x, y = parent
    ia, ib, ic, id_, ix, iy = child
    return [a * ia + c * ib, b * ia + d * ib, a * ic + c * id_, b * ic + d * id_,
            a * ix + c * iy + x, b * ix + d * iy + y]


def _merge(regions):
    """Produce one source rectangle, without alpha tracing or pixel polygons."""
    regions = [region for region in regions if region is not None]
    if not regions:
        return None
    x, y = min(region[0] for region in regions), min(region[1] for region in regions)
    return [x, y, max(region[0] + region[2] for region in regions) - x,
            max(region[1] + region[3] for region in regions) - y]


def _probe_reader(tags):
    """Read named Flash probes, including their nested animated child frame."""
    definitions = {int(tag.get('shapeId') or tag.get('spriteId')): tag for tag in tags
                   if tag.get('shapeId') or tag.get('spriteId')}
    displays = {}

    def frames(sprite_id):
        if sprite_id not in displays:
            history, starts, previous = [], {}, {}
            for index, display in enumerate(timeline_frames(definitions[sprite_id])):
                starts = {depth: starts.get(depth, index) if
                          previous.get(depth) == child.get('characterId') else index
                          for depth, child in display.items()}
                history.append([(child, index - starts[depth]) for depth, child in display.items()])
                previous = {depth: child.get('characterId') for depth, child in display.items()}
            displays[sprite_id] = history
        return displays[sprite_id]

    def bounds(character_id, frame, transform):
        tag = definitions[character_id]
        shape = tag.find('shapeBounds')
        if shape is not None:
            a, b, c, d, tx, ty = transform
            points = [(a * x + c * y + tx, b * x + d * y + ty)
                      for x in (float(shape.get('Xmin')) / 20, float(shape.get('Xmax')) / 20)
                      for y in (float(shape.get('Ymin')) / 20, float(shape.get('Ymax')) / 20)]
            xs, ys = zip(*points)
            return [min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)]
        if tag.get('type') != 'DefineSpriteTag':
            raise ValueError(f'Unreviewed gameplay probe type: {tag.attrib}')
        history = frames(character_id)
        display = history[frame % len(history)]
        # A one-frame wrapper stays in place while its child timeline advances.
        # In a real multi-frame loop, replacements restart their child instances.
        held_age = frame if len(history) == 1 else 0
        regions, masks = [], []
        for child, age in sorted(display, key=lambda item: int(item[0].get('depth'))):
            if not child.get('characterId'):
                continue
            depth = int(child.get('depth'))
            masks = [(end, box) for end, box in masks if end >= depth]
            box = bounds(int(child.get('characterId')), age + held_age, _compose(transform, matrix(child)))
            if child.get('clipDepth'):
                masks.append((int(child.get('clipDepth')), box))
                continue
            for _, mask in masks:
                if box is None or mask is None:
                    box = None
                    break
                left, top = max(box[0], mask[0]), max(box[1], mask[1])
                right = min(box[0] + box[2], mask[0] + mask[2])
                bottom = min(box[1] + box[3], mask[1] + mask[3])
                box = [left, top, right - left, bottom - top] if right > left and bottom > top else None
            regions.append(box)
        return _merge(regions)

    return frames, bounds


def _pack_layer(layer):
    """Pack occupied poses at one shared origin; no per-frame recentering."""
    images, poses, unique = [], {}, {}
    for action in layer['actions']:
        if action['matrix'][:4] != [1, 0, 0, 1]:
            raise ValueError('A changed parent scale/skew needs native rasterization before packing')
        cells = []
        for frame in range(1, action['frames'] + 1):
            path = Path(action['input']) / f'{frame}.png'
            with Image.open(path) as image:
                rgba = image.convert('RGBA')
                visible = rgba.getchannel('A').getbbox()
                placement = (math.floor(action['bounds'][0] + action['matrix'][4]),
                             math.floor(action['bounds'][1] + action['matrix'][5]))
                correction = action.get('frame_registration_offsets', {}).get(str(frame), [0, 0])
                placement = (placement[0] + correction[0], placement[1] + correction[1])
                if visible:
                    placement = (placement[0] + visible[0], placement[1] + visible[1])
                    rgba = rgba.crop(visible)
                else:
                    rgba = Image.new('RGBA', (1, 1))
                    placement = None
                key = (placement, rgba.size, hashlib.sha256(rgba.tobytes()).digest())
                if key not in unique:
                    unique[key] = len(images)
                    images.append((rgba, placement))
                cells.append(unique[key])
        poses[action['name']] = cells
    # An empty source frame has no spatial extent. Its oversized export canvas
    # must not enlarge every cell (BossDead includes two such terminal frames).
    regions = [[offset[0], offset[1], image.width, image.height]
               for image, offset in images if offset is not None]
    if not regions:
        raise ValueError('Refusing to pack a completely invisible layer')
    x, y, width, height = map(int, _merge(regions))
    columns = min(8, len(images), max(1, 8192 // width))
    rows = math.ceil(len(images) / columns)
    if width * columns > 16384 or height * rows > 16384:
        raise ValueError('Atlas exceeds native texture limit; review packing before registration')
    atlas = Image.new('RGBA', (width * columns, height * rows))
    for cell, (image, offset) in enumerate(images):
        if offset is not None:
            atlas.paste(image, ((cell % columns) * width + offset[0] - x,
                               (cell // columns) * height + offset[1] - y))
    output = Path(layer['output'])
    output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output)
    layer.update(cell_size=[width, height], sprite_offset=[x + width / 2, y + height / 2],
                 columns=columns, rows=rows, poses=poses, unique_cells=len(images))


def compact_registered_atlas(layer):
    """Pack visible storage rectangles while retaining every frame's virtual canvas.

    AtlasTexture.margin restores the same canvas/origin at draw time. This does
    not recenter sprites, alter frame offsets or split a character into layers.
    """
    path = Path(layer['output'])
    original = Image.open(path).convert('RGBA')
    width, height = layer['cell_size']
    pieces = []
    padding = 16  # Keep Godot's alpha-edge processing isolated from other frames.
    for cell in range(layer['unique_cells']):
        x, y = cell % layer['columns'] * width, cell // layer['columns'] * height
        frame = original.crop((x, y, x + width, y + height))
        used = frame.getchannel('A').getbbox() or (0, 0, 1, 1)
        pieces.append((cell, frame.crop(used), used))
    candidates = []
    for atlas_width in (512, 1024, 2048, 4096, 8192):
        if any(image.width + 2 * padding > atlas_width for _, image, _ in pieces):
            continue
        x, y, shelf_height = padding, padding, 0
        regions = {}
        for cell, image, used in sorted(pieces, key=lambda item: (-item[1].height, -item[1].width, item[0])):
            if x + image.width + padding > atlas_width:
                x, y, shelf_height = padding, y + shelf_height + padding * 2, 0
            regions[cell] = [x, y, image.width, image.height]
            x += image.width + padding * 2
            shelf_height = max(shelf_height, image.height)
        atlas_height = y + shelf_height + padding
        if atlas_height <= 16384:
            candidates.append((atlas_width * atlas_height, atlas_width, atlas_height, regions))
    if not candidates:
        raise ValueError('Registered sparse atlas exceeds native texture limits')
    _, atlas_width, atlas_height, regions = min(candidates, key=lambda item: item[0])
    atlas = Image.new('RGBA', (atlas_width, atlas_height))
    entries = {}
    for cell, image, used in pieces:
        rect = regions[cell]
        atlas.paste(image, (rect[0], rect[1]))
        entries[str(cell)] = {'region': rect, 'margin': [used[0], used[1], width - image.width, height - image.height]}
    atlas.save(path)
    layer['atlas_regions'] = entries
    layer['storage_size'] = [atlas_width, atlas_height]


def prepare_native_manifest(repo, inventory, manifest_path):
    """Prepare artwork and explicit native-authoring facts, never runtime instructions."""
    data = json.loads(manifest_path.read_text(encoding='utf-8'))
    source = next(item for item in inventory['sources'] if item['name'] == 'monster2')
    source_path = repo / source['swf']
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != data['source_sha256']:
        raise ValueError('Staged Garuda artwork differs from the verified Monster2 source')
    tags = ET.parse(repo / source['xml']).getroot().find('tags')
    symbols = {name.text: int(value.text) for tag in tags if tag.get('type') == 'SymbolClassTag'
               for name, value in zip(tag.find('names'), tag.find('tags'))}
    frames, bounds = _probe_reader(tags)
    actors = {item['id']: item for item in inventory['actors']}
    for actor in data['actors']:
        root_frames = frames(symbols[actor['symbol']])
        for action in actor['actions']:
            root = next(display for display in root_frames if any(child.get('name') == 'body' and
                        child.get('characterId') == str(action['sprite']) for child, _ in display))
            collider = next(child for child, _ in root if child.get('name') == 'colipse')
            action['hurt_bounds'] = bounds(int(collider.get('characterId')), 0, matrix(collider))
            action['hit_bounds'] = [_merge([bounds(int(child.get('characterId')), age,
                                   _compose(action['matrix'], matrix(child))) for child, age in display
                                   if child.get('name') == 'stick']) for display in frames(action['sprite'])]
            action['attack_refresh_frames'] = [event['frame'] for event in action['frame_events']
                                               if '.newAttackId()' in event['body']]
            action['fire_spawn_frames'] = [event['frame'] for event in action['frame_events']
                                           if '.fireHit()' in event['body']]
            action['complete_frames'] = [event['frame'] for event in action['frame_events']
                                         if re.search(r'curAction\s*=\s*"(?:wait|fly)"', event['body'])]
        actor['hurt_bounds'] = next(action['hurt_bounds'] for action in actor['actions'] if action['name'] == 'idle')
        actor['source_sounds'] = {key: actors[actor['legacy_id']][key]
                                  for key in ('script_sounds', 'embedded_sounds')}
        actor['behavior_sha256'] = hashlib.sha256((repo / actor['behavior_script']).read_bytes()).hexdigest()
    # The fire has no stick clip: SpecialEffectBullet tests its complete visual.
    # Store source shape bounds for review; final native attack phases are authored separately.
    fire = next(effect for effect in data['effects'] if effect['name'] == 'peng_fire')
    fire['visual_bounds'] = [bounds(fire['actions'][0]['sprite'], frame, [1, 0, 0, 1, 0, 0])
                             for frame in range(fire['actions'][0]['frames'])]
    # The three diverging flames are separate effective regions. Keep three
    # simple source rectangles instead of damaging the large empty fan between them.
    wrappers = frames(fire['actions'][0]['sprite'])[0]
    fire['hit_regions'] = [[bounds(int(child.get('characterId')), frame, matrix(child))
                            for child, _ in wrappers] for frame in range(fire['actions'][0]['frames'])]
    fire['fade_seconds'] = 0.6
    fire['damage_disabled_during_fade'] = False
    for actor in data['actors'] + data['effects']:
        for layer in actor['layers']:
            # Layer and gameplay action records were serialized independently.
            for action in layer['actions']:
                action.update(next(item for item in actor['actions'] if item['name'] == action['name']))
            _pack_layer(layer)
    data['notes'] += ['Probe rectangles retain complete parent transforms and nested child time. They still require gameplay review.',
                      'The 0.6s fade delays child animation only: BaseBullet.step/checkAttack remains active; do not invent a damage-free telegraph.',
                      'No dedicated attack voice exists in these actor frame scripts; BeattackByRole is shared hit feedback.']
    output = manifest_path.with_name('native_manifest.json')
    output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return output


def prepare(args):
    """Fail when the level plan is incomplete or current registrations drift."""
    repo = args.repo.resolve()
    inventory = json.loads(args.inventory.read_text(encoding='utf-8'))
    level = next(item for item in inventory['levels'] if item['label'] == '2-3')
    actors = {int(item['legacy_id']): item for item in json.loads(args.garuda_manifest.read_text(encoding='utf-8'))['actors']}
    required = {7: 'CaveBrute', 9: 'PengDemonKing', 13: 'KnifeDemon', 15: 'FlyingDemon'}
    missing_staged = sorted(number for number in required if number not in actors and number not in (7, 13))
    if missing_staged:
        raise ValueError(f'Missing staged actor manifest: {missing_staged}')
    enum_rows = xlsx_rows(repo / 'Configs/GameConfig/Datas/__enums__.xlsx')['Sheet1']
    entity_names = set()
    active_enum = None
    for row in enum_rows:
        cells = {re.sub(r'\d+$', '', key): value for key, value in row.items()}
        active_enum = cells.get('B') or active_enum
        if active_enum == 'Entity.EntityId' and cells.get('H'):
            entity_names.add(cells['H'])
    entity_gaps = {name: number for number, name in required.items() if name not in entity_names}
    resource_rows = xlsx_rows(repo / 'Configs/GameConfig/Datas/Resources.xlsx')['EntityConfig']
    resource_names = {value for row in resource_rows for key, value in row.items()
                      if key.startswith('C') and value}
    resource_gaps = {name: number for number, name in required.items() if name not in resource_names}
    current_levels = xlsx_rows(repo / 'Configs/GameConfig/Datas/LevelConfig.xlsx')['LevelConfig']
    current_ids = {int(value) for row in current_levels for key, value in row.items()
                   if key.startswith('B') and value is not None and str(value).isdigit()}
    music = repo / 'Godot/GodotProject/TheGame/Audios/Music/cave_theme.mp3'
    if not music.is_file():
        raise ValueError('CaveTheme asset is not installed')
    native_manifest = prepare_native_manifest(repo, inventory, args.garuda_manifest.resolve())
    equipment_source = args.inventory.resolve().parent / 'main_scripts/scripts/my/AllEquipment.as'
    equipment_text = equipment_source.read_text(encoding='utf-8-sig')
    required_drops = next(actor['equipment_drops'] for actor in inventory['actors'] if actor['id'] == 9)
    equipment = []
    for legacy_id in required_drops:
        declaration = re.search(r'new MyEquipObj\((\d+),"([^"]*)","' + re.escape(legacy_id) +
                                r'","([^"]*)","([^"]*)".*?\);', equipment_text)
        if declaration is None:
            raise ValueError(f'Missing official equipment constructor: {legacy_id}')
        equipment.append({'legacy_id': legacy_id, 'selector': int(declaration[1]), 'name_cn': declaration[2],
                          'source_slot': declaration[3], 'source_owner': declaration[4],
                          'source_constructor': declaration[0]})
    plan = {
        'official_label': '2-3',
        'level_id': 6,
        'legacy_level_id': 6,
        'name_cn': '迦楼罗之穴',
        'scene_path': 'res://TheGame/Scenes/Levels/zm1_06_garuda_cave/level_6.tscn',
        'music_id': 106,
        'source_frame': level['source_frame'],
        'front_sprite': level['front_sprite'],
        'front_position': level['front_position'],
        'stages': level['stages'],
        'spawn_streams': level['spawn_streams'],
        'enemy_counts': level['enemy_counts'],
        'geometry_count': level['geometry'],
        'scene_characters': level['scene_characters'],
        'required_entities': required,
        'entity_enum_gaps': entity_gaps,
        'entity_resource_gaps': resource_gaps,
        'current_level_ids': sorted(current_ids),
        'actors': [{key: actor.get(key) for key in ('name', 'legacy_id', 'source_sha256', 'actions', 'behavior_script')}
                   for actor in actors.values()],
        'music_sha256': hashlib.sha256(music.read_bytes()).hexdigest(),
        'native_art_manifest': str(native_manifest),
        'equipment_source': str(equipment_source),
        'equipment_source_sha256': hashlib.sha256(equipment_source.read_bytes()).hexdigest(),
        'equipment_dependencies': equipment,
        'acceptance': [
            'Keep the five official stages and parallel recipe streams; do not infer bounds from previous stage exits.',
            'Author Monster9 as a boss branch with ground/fly/egg/rebirth states; do not use generic sky AI.',
            'Author Monster15 as a sky monster with its own vertical tracking and attack state.',
            'Keep attack geometry in native AnimationLibrary tracks; resolve named source probes before enabling damage.',
            'Register all actor scenes and Resource/Entity enum rows together before Luban validation.',
            'Verify music, traps, drops, checkpoint, victory/defeat, retry, map return and save reload in the real level.',
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(plan, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('GARUDA LEVEL PLAN PASS:', plan['spawn_streams'], 'streams,', len(plan['stages']),
          'stages, enemy gaps=', plan['entity_enum_gaps'], plan['entity_resource_gaps'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--garuda-manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    prepare(parser.parse_args())
