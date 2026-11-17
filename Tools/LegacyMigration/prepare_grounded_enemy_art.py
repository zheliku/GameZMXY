"""Apply reviewed action-level registration corrections without moving actor roots.

Original source matrices remain in the review manifest. Frame-local stride,
rotation, weapons and hit effects retain the source placement; no frame is
individually centered. Five visually reviewed actors receive action-level corrections.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import math
import re
from pathlib import Path
from PIL import Image

from prepare_official_garuda_level import _pack_layer, compact_registered_atlas
from import_official_sea_monsters import frames_resource, ACTORS, PROJECT


def install_density_sample(args):
    """Install authorized registered density samples without regenerating gameplay tracks."""
    sample = json.loads(args.approved_density_sample.read_text(encoding='utf-8'))
    contracts = {'huaguoshan_monkey': (69, 23, [178, 144]),
                 'demon_monkey': (78, 20, [256, 230]),
                 'gorilla': (85, 23, [402, 328])}
    name = sample['actor']
    if sample.get('original_slots_preserved') or sample.get('registered_layout'):
        # Category batches already verified every source frame against the
        # installed registration; pagination only changes storage rectangles.
        visual_node = sample.get('visual_node','m_Body')
        frames_path = Path(sample['frames_path'])
        scene_path = Path(sample['scene_path'])
        atlas_path = Path(sample['atlas_path'])
        baseline = args.output.parent / 'before'
        repo = PROJECT.parents[1]
        baseline_scene = (baseline / scene_path.relative_to(repo)).read_text(encoding='utf-8-sig')
        library_path = Path(sample['library_path']) if sample.get('library_path') else PROJECT / re.search(r'type="AnimationLibrary"[^\n]*path="res://([^\"]+)"', baseline_scene)[1]
        text = (baseline / frames_path.relative_to(repo)).read_text(encoding='utf-8-sig')
        pages = sample.get('atlas_pages', [{'output': sample['output']}])
        texture_line = re.search(r'\[ext_resource type="Texture2D"[^\n]*id="([^"]+)"\]', text)
        texture_id = texture_line[1]
        page_paths = [atlas_path if page == 0 else atlas_path.with_name(atlas_path.stem + f'_{page + 1}.png')
                      for page in range(len(pages))]
        extra_textures = ''.join('\n[ext_resource type="Texture2D" path="res://' +
            path.relative_to(PROJECT).as_posix() + '" id="' + texture_id + f'_{page + 1}' + '"]'
            for page, path in enumerate(page_paths) if page > 0)
        text = text[:texture_line.end()] + extra_textures + text[texture_line.end():]
        if len(pages) > 1:
            text = re.sub(r'load_steps=(\d+)', lambda match: 'load_steps=' + str(int(match[1]) + len(pages) - 1), text, count=1)
        for identity, entry in sample['atlas_regions'].items():
            pattern = r'(\[sub_resource type="AtlasTexture" id="'+re.escape(identity)+r'"\]\n)(.*?)(?=\n\[|\Z)'
            match = re.search(pattern,text,re.S)
            block = match[2]
            page = entry.get('page', 0)
            page_id = texture_id if page == 0 else texture_id + f'_{page + 1}'
            block = re.sub(r'^atlas = ExtResource\("[^"]+"\)', 'atlas = ExtResource("' + page_id + '")', block, flags=re.M)
            for key in ['region','margin']:
                value = key+' = Rect2('+', '.join(map(str,entry[key]))+')'
                block,count = re.subn(r'^'+key+r' = Rect2\([^\n]+\)',value,block,flags=re.M)
                if not count:
                    block = block.rstrip('\n')+'\n'+value+'\n'
            text = text[:match.start(2)]+block+text[match.end(2):]
        scene = (baseline / scene_path.relative_to(repo)).read_text(encoding='utf-8-sig')
        match = re.search(r'(\[node name="'+visual_node+r'"[^\n]*\]\n)(.*?)(?=\n\[)',scene,re.S)
        body = match[2]
        body = re.sub(r'^offset = Vector2\(([^)]+)\)',
            lambda v:'offset = Vector2('+', '.join(format(float(x)*2,'g') for x in v[1].split(','))+')',body,flags=re.M)
        scene = scene[:match.start(2)]+'scale = Vector2(0.5, 0.5)\n'+body+scene[match.end(2):]
        for child_path in sample.get('child_visual_paths', []):
            parent, child_name = child_path.rsplit('/', 1)
            pattern = r'(\[node name="' + re.escape(child_name) + r'"[^\n]*parent="' + re.escape(parent) + r'"[^\n]*\]\n)'
            scene, count = re.subn(pattern, r'\g<1>scale = Vector2(2, 2)\n', scene)
            if count != 1:
                raise ValueError('Expected the registered child visual ' + child_path)
        library = (baseline / library_path.relative_to(repo)).read_text(encoding='utf-8-sig')
        pattern = r'(tracks/\d+/path = NodePath\("[^\"]*'+visual_node+r':offset"\).*?"values": \[)([^\]]+)(\])'
        def double_offsets(match):
            values = re.sub(r'Vector2\(([^)]+)\)',lambda v:'Vector2('+', '.join(format(float(x)*2,'g') for x in v[1].split(','))+')',match[2])
            return match[1]+values+match[3]
        library,offset_tracks = re.subn(pattern,double_offsets,library,flags=re.S)
        for path, page in zip(page_paths, pages):
            path.write_bytes(Path(page['output']).read_bytes())
        for path,content in [(frames_path,text),(scene_path,scene),(library_path,library)]:
            path.write_text(content,encoding='utf-8',newline='\n')
        args.output.mkdir(parents=True,exist_ok=True)
        (args.output/'installation.json').write_text(json.dumps({'actor':name,'density':2,
            'frames':sample['frames'],'shared_poses':sample['poses'],'offset_tracks_updated':offset_tracks,
            'atlas_sha256':hashlib.sha256(atlas_path.read_bytes()).hexdigest(),
            'installed_files':[str(p) for p in [atlas_path,frames_path,scene_path,library_path] + page_paths[1:]],
            'runtime_root_or_physics_changed':False},indent=2),encoding='utf-8')
        print(f'{name.upper()} BATCH DENSITY INSTALL PASS: {len(pages)} atlas pages, registration, timing and identities')
        return
    if name not in contracts or sample['source_sha256'] != '5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a':
        raise ValueError('This density route is only registered for the three Monster1 ground actors')
    frame_count, pose_count, canvas = contracts[name]
    layer = sample['layers']['2']
    directory = PROJECT / 'TheGame/Sprites/Characters/Enemies/Monsters/Ground' / name
    atlas_path = directory / f'{name}_official_body.png'
    frames_path = directory / f'{name}_official_frames.tres'
    scene_path = PROJECT / f'TheGame/Entitys/Enemies/Monsters/Ground/{name}/{name}_entity.tscn'
    library_path = PROJECT / f'TheGame/Entitys/Animations/Enemies/Monsters/Ground/{name}/{name}_anim_library.tres'
    text = frames_path.read_text(encoding='utf-8-sig')
    cells = {}
    for entry in re.finditer(r'"frames": \[(.*?)\],\n"loop": (\d+),\n"name": &"([^"]+)",\n"speed": ([\d.]+)', text, re.S):
        ids = re.findall(r'SubResource\("([^"]+)"\)', entry[1])
        poses = layer['poses'][entry[3]]
        if len(ids) != len(poses) or float(entry[4]) != 24:
            raise ValueError('Reviewed density sample does not match the installed animation timing')
        for identity, cell in zip(ids, poses):
            if identity in cells and cells[identity] != cell:
                raise ValueError('Reviewed sample changes an existing shared pose identity')
            cells[identity] = cell
    if sum(len(poses) for poses in layer['poses'].values()) != frame_count or len(cells) != pose_count:
        raise ValueError(f'Expected the complete {frame_count}-frame, {pose_count}-pose {name} sample')
    for identity, cell in cells.items():
        pattern = r'(\[sub_resource type="AtlasTexture" id="'+re.escape(identity)+r'"\]\n)(.*?)(?=\n\[|\Z)'
        match = re.search(pattern, text, re.S)
        block = match[2]
        for key in ['region', 'margin']:
            if key == 'margin' and not any(layer['atlas_regions'][str(cell)][key]):
                block = re.sub(r'^margin = Rect2\([^\n]+\)\n?', '', block, flags=re.M)
                continue
            value = 'Rect2('+', '.join(map(str, layer['atlas_regions'][str(cell)][key]))+')'
            block, count = re.subn(r'^'+key+r' = Rect2\([^\n]+\)', key+' = '+value, block, flags=re.M)
            if key == 'margin' and count == 0:
                # The original demon monkey uses full grid cells, without margins.
                block = block.rstrip('\n')+'\nmargin = '+value+'\n'
                count = 1
            if count != 1:
                raise ValueError('Expected an existing registered '+key)
        text = text[:match.start(2)]+block+text[match.end(2):]
    offset = 'Vector2('+', '.join(format(value, 'g') for value in layer['sprite_offset'])+')'
    scene = scene_path.read_text(encoding='utf-8-sig')
    match = re.search(r'(\[node name="m_Body"[^\n]*\]\n)(.*?)(?=\n\[)', scene, re.S)
    body = re.sub(r'^scale = [^\n]*\n', '', match[2], flags=re.M)
    body, count = re.subn(r'^offset = Vector2\([^\n]+\)', 'offset = '+offset, body, flags=re.M)
    if count != 1:
        raise ValueError('Expected the current body registration offset')
    scene = scene[:match.start(2)]+'scale = Vector2(0.5, 0.5)\n'+body+scene[match.end(2):]
    library = library_path.read_text(encoding='utf-8-sig')
    pattern = r'(tracks/\d+/path = NodePath\("m_Body:offset"\).*?"values": \[)([^\]]+)(\])'
    library, offset_tracks = re.subn(pattern, lambda item: item[1]+offset+item[3], library, flags=re.S)
    if offset_tracks != 6:
        raise ValueError('Expected RESET and five existing body-offset tracks')
    source = Path(layer['output'])
    with Image.open(source) as image:
        if list(image.size) != layer['storage_size'] or layer['cell_size'] != canvas:
            raise ValueError('Density sample dimensions differ from its registered canvas')
    # Complete all contract checks before writing the four authorized resources.
    atlas_path.write_bytes(source.read_bytes())
    frames_path.write_text(text, encoding='utf-8', newline='\n')
    scene_path.write_text(scene, encoding='utf-8', newline='\n')
    library_path.write_text(library, encoding='utf-8', newline='\n')
    args.output.mkdir(parents=True, exist_ok=True)
    result = {'actor': sample['actor'], 'density': 2, 'source_sha256': sample['source_sha256'],
        'approved_sample': str(args.approved_density_sample.resolve()), 'frames': frame_count, 'shared_poses': pose_count,
        'atlas_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'offset_tracks_updated': offset_tracks,
        'runtime_root_or_physics_changed': False, 'installed_files': [str(path) for path in [atlas_path,frames_path,scene_path,library_path]]}
    (args.output / 'installation.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(f'{name.upper()} DENSITY INSTALL PASS: {frame_count} frames, {pose_count} shared poses, original paths and resource identities')


def prepare(args):
    """Repack reviewed actions on a shared canvas and install only their artwork."""
    source = json.loads(args.manifest.read_text(encoding='utf-8'))
    result = {'fps': source['fps'], 'source_sha256': source['source_sha256'], 'actors': []}
    args.output.mkdir(parents=True, exist_ok=True)
    originals = list(source['actors'])
    for extra in args.extra_manifest:
        originals.extend(json.loads(extra.read_text(encoding='utf-8'))['actors'])
    directories = {name: spec[2] for name, spec in ACTORS.items()}
    directories.update({name: f'Monsters/Ground/{name}' for name in ('huaguoshan_monkey', 'gorilla')})
    for original in originals:
        name = original['name']
        if name not in ('shrimp_demon', 'turtle_minister', 'tide_demon', 'huaguoshan_monkey', 'gorilla'):
            continue
        actor = deepcopy(original)
        # The older ground manifest keeps source raster paths on its layer actions.
        if not actor['actions'][0].get('input'):
            actor['actions'] = deepcopy(actor['layers'][0]['actions'])
        floor = actor['hurt_bounds'][1] + actor['hurt_bounds'][3]
        for action in actor['actions']:
            correction = 0
            reason = ''
            if name == 'shrimp_demon' and action['name'] in ('idle', 'death'):
                correction = 8
                reason = 'Reviewed boot/abdomen anchor; antenna tips are not the ground contact.'
            elif name == 'turtle_minister' and action['name'] == 'death':
                final = Image.open(Path(action['input']) / f'{action["frames"]}.png').convert('RGBA')
                box = final.getchannel('A').point(lambda value: 255 if value >= 26 else 0).getbbox()
                if box is None:
                    raise ValueError('Expected a visible final turtle shell')
                bottom = math.floor(action['bounds'][1] + action['matrix'][5]) + box[3]
                correction = round(floor - bottom)
                reason = 'Reviewed final shell contact; keep the complete death motion on one registration.'
            elif action['name'] == 'death' and name in ('huaguoshan_monkey', 'gorilla', 'tide_demon'):
                correction = {'huaguoshan_monkey': 8, 'gorilla': -8, 'tide_demon': -16}[name]
                reason = 'Reviewed settled corpse contact against the native physical ground; retain the initial impact pose.'
            action['source_matrix'] = list(action['matrix'])
            action['registration_correction'] = [0, correction]
            if correction:
                if action['name'] == 'death':
                    # Keep the initial impact at its source registration, then settle
                    # the collapsed corpse/shell during the original rotation phase.
                    action['frame_registration_offsets'] = {
                        str(frame): [0, round(correction * min(1, max(0, (frame - 3) / 7)))]
                        for frame in range(1, action['frames'] + 1)}
                else:
                    action['matrix'][5] += correction
                action['registration_reason'] = reason
        layer = {'name': 'body', 'actions': actor['actions'], 'output': str(args.output / f'{name}.png')}
        _pack_layer(layer)
        compact_registered_atlas(layer)
        actor['layers'] = [layer]
        actor['_layer'] = layer
        directory = PROJECT / 'TheGame/Sprites/Characters/Enemies' / directories[name]
        atlas = directory / f'{name}_official_body.png'
        atlas.write_bytes(Path(layer['output']).read_bytes())
        previous_path = directory / f'{name}_official_frames.tres'
        previous_identity = re.search(r'uid="([^"]+)"', previous_path.read_text(encoding='utf-8-sig')) if previous_path.exists() else None
        frames = frames_resource(name, atlas, actor)
        # Keep the stable SpriteFrames identity from the installed scene; changing
        # packing must not strand existing scene UID references on a fresh import.
        scene_path = PROJECT / 'TheGame/Entitys/Enemies' / directories[name] / f'{name}_entity.tscn'
        scene_text = scene_path.read_text(encoding='utf-8-sig')
        identity = re.search(r'\[ext_resource type="SpriteFrames" uid="([^"]+)" path="'+re.escape(f'res://{frames.relative_to(PROJECT).as_posix()}')+r'"', scene_text)
        identity = identity or previous_identity
        if identity:
            text = frames.read_text(encoding='utf-8')
            frames.write_text(text.replace('[gd_resource type="SpriteFrames" format=3]',
                f'[gd_resource type="SpriteFrames" format=3 uid="{identity[1]}"]',1),encoding='utf-8')
        actor['installed_frames'] = f'res://{frames.relative_to(PROJECT).as_posix()}'
        actor['installed_scene'] = f'res://TheGame/Entitys/Enemies/{directories[name]}/{name}_entity.tscn'
        actor.pop('_layer')
        result['actors'].append(actor)
        print('GROUNDED ART:', name, [(a['name'], a['registration_correction']) for a in actor['actions']])
    (args.output / 'manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('GROUNDED ART PASS:', len(result['actors']), 'actors, reviewed source corrections, common root preserved')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--manifest', type=Path)
    source.add_argument('--approved-density-sample', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--extra-manifest', type=Path, nargs='*', default=[])
    args = parser.parse_args()
    if args.approved_density_sample:
        install_density_sample(args)
    else:
        prepare(args)
