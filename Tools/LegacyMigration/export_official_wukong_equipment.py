"""Export one official equipment layer against the existing shared animation frame map.

Only a new body atlas is created. Existing animations, weapon masks, registration
and compact cell numbers remain untouched, including all five basic attacks,
five skills and the complete 100-frame idle loop.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as E

import export_official_wukong as source


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
    manifest={'source_sha256':hashlib.sha256(original).hexdigest(),'selector':args.selector,'kind':args.kind,'columns':6,'rows':20,
        'poses':poses,'inputs':inputs,'output':str(args.output.resolve())}
    (work/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(f'Prepared {len(poses)} registered {args.kind} cells, selector {args.selector}; existing grid 6x20 retained.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['role','xml','skills','idle','java','ffdec','work','output']:
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--kind', choices=['body', 'weapon'], default='body')
    parser.add_argument('--selector', type=int, default=4)
    export(parser.parse_args())
