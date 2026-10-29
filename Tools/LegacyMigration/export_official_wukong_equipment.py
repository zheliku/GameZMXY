"""Export one official equipment layer against the existing shared animation frame map.

Only a new body or weapon atlas is created. Existing animations, weapon masks,
registration and compact cell numbers remain untouched. All live basic motion
frames are included alongside attacks, skills and the complete idle loop.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as E

import export_official_wukong as source


PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
MOTIONS = {'walk': (440, 16), 'run': (452, 8), 'jump': (533, 1),
           'jump_2': (539, 10), 'fall': (543, 1), 'hurt': (553, 15)}


def motion_poses(library):
    """Map each complete official motion to the cells used by both live layers."""
    text = library.read_text(encoding='utf-8-sig')
    result = {}
    for name, (sprite, count) in MOTIONS.items():
        animation = re.search(r'\[sub_resource type="Animation" id="' + name +
                              r'"\].*?(?=\n\[|\Z)', text, re.S)
        if animation is None:
            raise ValueError(f'Missing live motion: {name}')
        layers = []
        for layer in ('m_Body', 'm_Weapon'):
            track = re.search(r'tracks/(\d+)/path = NodePath\("' + layer + r':frame"\)', animation[0])
            if track is None:
                raise ValueError(f'Missing motion layer: {name}/{layer}')
            keys = re.search(r'tracks/' + track[1] + r'/keys = \{.*?"values": \[([^\]]*)\]',
                             animation[0], re.S)
            if keys is None:
                raise ValueError(f'Missing motion frame keys: {name}/{layer}')
            frames = [int(value.strip()) for value in keys[1].split(',')]
            if len(frames) != count:
                raise ValueError(f'{name}/{layer}: expected all {count} official frames, got {len(frames)}')
            layers.append(frames)
        if layers[0] != layers[1]:
            raise ValueError(f'Body and weapon motion maps differ: {name}')
        for frame, cell in enumerate(layers[0], 1):
            pose = (sprite, frame)
            if cell in result and result[cell] != pose:
                raise ValueError(f'Motion cell {cell} aliases different source frames')
            result[cell] = pose
    return result


def export(args):
    """Freeze one equipment selector and retain all cells currently used by the game."""
    original = args.role.read_bytes()
    if hashlib.sha256(original).hexdigest() != '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7':
        raise ValueError('Unexpected Role_v7 revision')
    root = E.parse(args.xml).getroot()
    symbols = {}
    for tag in root.find('tags'):
        if tag.get('type') == 'SymbolClassTag':
            symbols.update(zip((x.text for x in tag.find('names')), (int(x.text) for x in tag.find('tags'))))
    role_id = symbols['export.hero.Role1']
    role = next(t for t in root.find('tags') if t.get('spriteId') == str(role_id))
    transforms = {int(t.get('characterId')): source.matrix(t) for t in role.find('subTags')
                  if t.get('name') == 'body' and t.get('characterId')}
    poses = {i: pose for i, pose in enumerate(source.LEGACY_POSES.values())}
    for action in source.attack_manifest(root):
        for frame in range(action['count']):
            poses[action['start'] + frame] = (action['sprite'], frame + 1)
    skills = json.loads(args.skills.read_text(encoding='utf-8-sig'))
    for action in skills['skills']:
        for frame, cell in enumerate(action['frame_cells']):
            poses.setdefault(int(cell), (int(action['sprite']), frame + 1))
    idle = json.loads(args.idle.read_text(encoding='utf-8-sig'))
    for frame, cell in enumerate(idle['frames']):
        poses.setdefault(int(cell), (529, frame + 1))
    # A new appearance must cover the native animation's complete frame map;
    # using another item's exported layer silently loses its equipment identity.
    motions = motion_poses(args.motion_library)
    for cell, pose in motions.items():
        if cell in poses and poses[cell] != pose:
            raise ValueError(f'Motion cell {cell} conflicts with an existing pose')
        poses[cell] = pose
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, APPDATA=str(work/'profile'))
    (work/'profile').mkdir(exist_ok=True)
    for sprite in root.find('tags'):
        if sprite.get('type') == 'DefineSpriteTag':
            if sprite.get('spriteId') in source.CLOTH_IDS:
                source.freeze(sprite, args.selector if args.kind == 'body' else 1)
            if args.kind == 'weapon' and sprite.get('spriteId') == source.WEAPON_ID:
                source.freeze(sprite, args.selector)
    xml, swf = work/'appearance.xml',work/'appearance.swf'
    E.ElementTree(root).write(xml,encoding='utf-8',xml_declaration=True)
    command=[str(args.java),f'-Duser.home={work}','-jar',str(args.ffdec)]
    with (work/'export.log').open('w',encoding='utf-8') as log:
        subprocess.run(command+['-xml2swf',str(xml),str(swf)],env=environment,stdout=log,stderr=subprocess.STDOUT,check=True)
        subprocess.run(command+['-format','sprite:svg','-selectid',','.join(map(str,sorted({sid for sid,_ in poses.values()}))),
            '-ignorebackground','-export','sprite',str(work/'vectors'),str(swf)],env=environment,stdout=log,stderr=subprocess.STDOUT,check=True)
    folders={int(p.name.split('_')[1]):p for p in (work/'vectors').iterdir() if p.is_dir()}
    split=work/'split'
    inputs={}
    # Skills share their existing independent energy layer. Filtered dash
    # silhouettes cannot be rendered as body SVGs and would become rectangles.
    source.EFFECT_IDS |= {'371', '416', '568', '338'}
    for sid,folder in folders.items():
        destination=split/str(sid)
        destination.mkdir(parents=True,exist_ok=True)
        inputs[sid]=str(destination)
        for svg in folder.glob('*.svg'):
            source.split_pose(svg,destination,transforms[sid])
    rows = (max(poses) + 6) // 6
    manifest={'source_sha256':hashlib.sha256(original).hexdigest(),'selector':args.selector,'kind':args.kind,'columns':6,'rows':rows,
        'poses':poses,'motion_cells':sorted(motions),'inputs':inputs,'output':str(args.output.resolve())}
    (work/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(f'Prepared {len(poses)} registered {args.kind} cells, selector {args.selector}; '
          f'{len(motions)} complete motion frames; grid 6x{rows}.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['role','xml','skills','idle','java','ffdec','work','output']:
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--kind', choices=['body', 'weapon'], default='body')
    parser.add_argument('--selector', type=int, default=4)
    parser.add_argument('--motion-library', type=Path,
                        default=PROJECT/'TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres')
    export(parser.parse_args())
