"""Export one official Role2 body or weapon atlas variant for native review.

Unlike a hand-drawn replacement, this follows every labeled Role2 body frame,
retains the action's parent matrix, freezes the exact upstream wardrobe
selectors, and splits the weapon at each source depth with a per-fragment
foreground mask. All intermediate SWFs, SVGs and rasters stay in --work.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

import export_official_wukong as base
from audit_official_tangseng import ROLE_HASH


ACTION_ORDER = ['wait','walk','run','jump1','jump2','jump3','hurt',
                'hit1','hit2','hit3','hit4','hit5']


def freeze_hidden(sprite):
    """Replace one non-applicable selector with an explicit empty frame."""
    timeline = sprite.find('subTags')
    timeline.clear()
    ET.SubElement(timeline,'item',type='ShowFrameTag',forceWriteAsLong='false')
    sprite.set('frameCount','1')


def freeze_wardrobe(tree,audit,cloth,weapon):
    """Freeze every source placement controlled by Role2's actual IDs."""
    sprites={int(tag.get('spriteId')):tag for tag in tree.getroot().find('tags')
             if tag.get('type')=='DefineSpriteTag'}
    for selector in audit['selectors']:
        sprite=sprites[selector['sprite']]
        if selector['kind']=='weapon':
            base.freeze(sprite,weapon)
        elif selector['kind']=='armor':
            if cloth<=selector['maximum_selector']:
                base.freeze(sprite,cloth)
            else:
                freeze_hidden(sprite)
        elif selector['kind']=='armor_component':
            if cloth<selector['minimum_selector']:
                freeze_hidden(sprite)
            else:
                base.freeze(sprite,cloth+selector['frame_offset'])


def source_actions(audit):
    """Select labeled body timelines in the official player action order."""
    actor=audit['actors']['export.hero.Role2']
    actions={item['label']:item for item in actor['actions']}
    missing=[name for name in ACTION_ORDER if name not in actions]
    if missing:
        raise ValueError('Official Role2 action missing from source: '+', '.join(missing))
    return [actions[name] for name in ACTION_ORDER]


def bake_attack_cloth(tree, audit):
    """Bake Role2's two explicit cloth play commands, including the frame-1 stop."""
    tags = tree.getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags
               if tag.get('type') == 'DefineSpriteTag'}
    next_id = max(int(value) for tag in tags for key, value in tag.attrib.items()
                  if key.endswith('Id') and value.isdigit()) + 1
    records = []
    for action in source_actions(audit):
        if action['label'] not in ('hit1', 'hit2'):
            continue
        trigger, start = (3, 2) if action['label'] == 'hit1' else (2, 4)
        script = action['frame_scripts'].get(str(trigger), '')
        if f'gotoAndPlay({start})' not in script:
            raise ValueError('Official attack cloth command changed')
        actor = sprites[action['sprite']]
        placement = next(tag for tag in actor.find('subTags')
                         if tag.get('name') == 'bodyEquip' and tag.get('characterId'))
        selector = deepcopy(sprites[int(placement.get('characterId'))])
        selector.set('spriteId', str(next_id))
        placement.set('characterId', str(next_id))
        next_id += 1
        for child in selector.find('subTags'):
            sid = int(child.get('characterId', '0'))
            if sid not in (867, 870):
                continue
            source = sprites[sid]
            stop = audit['nested_scripts'][str(sid)]['frame_scripts'].get('1', '')
            if 'stop();' not in stop:
                raise ValueError('Official cloth no longer stops on its first frame')
            frames = list(base.timeline_frames(source))
            mapped = [1 if frame < trigger else start + frame - trigger
                      for frame in range(1, action['frames'] + 1)]
            mapped = [frame if frame <= len(frames) else 1 for frame in mapped]
            baked = deepcopy(source)
            baked.set('spriteId', str(next_id))
            baked.set('frameCount', str(len(mapped)))
            child.set('characterId', str(next_id))
            next_id += 1
            timeline = baked.find('subTags')
            timeline.clear()
            previous = set()
            for frame in mapped:
                display = frames[frame - 1]
                for depth in sorted(previous, key=int):
                    ET.SubElement(timeline, 'item', type='RemoveObject2Tag', depth=depth)
                for depth in sorted(display, key=int):
                    placed = deepcopy(display[depth])
                    placed.set('placeFlagMove', 'false')
                    placed.set('placeFlagHasCharacter', 'true')
                    timeline.append(placed)
                ET.SubElement(timeline, 'item', type='ShowFrameTag', forceWriteAsLong='false')
                previous = set(display)
            tags.append(baked)
            records.append({'action': action['label'], 'source_sprite': sid,
                            'source_frames': mapped})
        tags.append(selector)
    return records


def export(args):
    """Freeze the chosen upstream selectors and export every source frame."""
    raw=args.role.read_bytes()
    if hashlib.sha256(raw).hexdigest()!=ROLE_HASH:
        raise ValueError('Unexpected official Role_v7 revision')
    audit=json.loads(args.audit.read_text(encoding='utf-8'))
    if audit['role_sha256']!=ROLE_HASH or hashlib.sha256(args.xml.read_bytes()).hexdigest()!=audit['xml_sha256']:
        raise ValueError('Role2 audit and official inputs do not match')
    actions=source_actions(audit)
    tree=ET.parse(args.xml)
    freeze_wardrobe(tree,audit,args.cloth,args.weapon)
    cloth_bakes=bake_attack_cloth(tree,audit)
    args.work.mkdir(parents=True,exist_ok=True)
    profile=args.work/'profile'
    profile.mkdir(exist_ok=True)
    environment=dict(os.environ,APPDATA=str(profile.resolve()))
    command=[str(args.java),f'-Duser.home={args.work.resolve()}','-jar',str(args.ffdec)]
    filtered=args.work/'role2_variant.xml'
    source_sprites={int(tag.get('spriteId')):tag for tag in tree.getroot().find('tags')
                    if tag.get('spriteId')}
    actor=source_sprites[audit['actors']['export.hero.Role2']['sprite']]
    by_label={}
    label=None
    for tag in actor.find('subTags'):
        if tag.get('type')=='FrameLabelTag': label=tag.get('name')
        elif tag.get('name')=='body' and tag.get('characterId'):
            by_label[label]=int(tag.get('characterId'))
    for label_name, action in zip(ACTION_ORDER,actions):
        if by_label.get(label_name)!=action['sprite']:
            raise ValueError(f'Role2 label identity differs from audit: {label_name}')
    tree.write(filtered,encoding='utf-8',xml_declaration=True)
    swf=args.work/'role2_variant.swf'
    selected=','.join(str(action['sprite']) for action in actions)
    with (args.work/'export.log').open('w',encoding='utf-8') as log:
        subprocess.run(command+['-xml2swf',str(filtered),str(swf)],env=environment,
                       stdout=log,stderr=subprocess.STDOUT,check=True)
        subprocess.run(command+['-format','sprite:svg','-selectid',selected,'-ignorebackground',
                       '-export','sprite',str(args.work/'vectors'),str(swf)],env=environment,
                       stdout=log,stderr=subprocess.STDOUT,check=True)
    folders={int(match.group(1)):path for path in (args.work/'vectors').iterdir() if path.is_dir()
             if (match:=re.match(r'DefineSprite_(\d+)',path.name))}
    if set(int(action['sprite']) for action in actions)-folders.keys():
        raise ValueError('FFDec did not export each Role2 action sprite')
    base.WEAPON_IDS={'723'}
    # The root hit4 timeline names wrapper 951 roundMc; 656 is only a nested
    # sound/control timeline. Keep the entire wrapper out of body/weapon art.
    base.EFFECT_IDS={tag.get('characterId') for action in actions
        for tag in source_sprites[action['sprite']].find('subTags')
        if tag.get('name')=='roundMc' and tag.get('characterId')}
    frames=[]
    for action in actions:
        sid=int(action['sprite'])
        transform=action['matrix']
        (args.work/'layers'/action['label']).mkdir(parents=True,exist_ok=True)
        sources=sorted(folders[sid].glob('*.svg'),key=lambda p:int(re.findall(r'\d+',p.stem)[-1]))
        if len(sources)!=int(action['frames']):
            raise ValueError(f'Incomplete native frames for {action["label"]}: {len(sources)}')
        for frame,source in enumerate(sources,1):
            base.split_pose(source,args.work/'layers'/action['label'],transform)
            stem=source.stem
            body=args.work/'layers'/action['label']/(stem+'_body.svg')
            weapon_count=len(list((args.work/'layers'/action['label']).glob(stem+'_weapon_*.svg')))
            for kind,path in [('body',body)]+[('weapon',args.work/'layers'/action['label']/(stem+f'_weapon_{i}.svg')) for i in range(weapon_count)]:
                if kind=='body' and not path.is_file():
                    raise ValueError(f'Missing source body frame {action["label"]}/{frame}')
                if kind=='weapon' and not path.is_file():
                    raise ValueError(f'Missing source weapon depth mask {action["label"]}/{frame}')
            frames.append({'action':action['label'],'source_sprite':sid,'source_frame':frame,
                           'body_svg':str(body.relative_to(args.work).as_posix()),'weapon_svg_count':weapon_count})
    manifest={'source_sha256':ROLE_HASH,'xml_sha256':audit['xml_sha256'],'role':'Role2',
        'kind':args.kind,'cloth_selector':args.cloth,'weapon_selector':args.weapon,
        'source_origin':[100,128],'frame_cell':[200,200],'columns':6,'action_order':ACTION_ORDER,'frames':frames,
        'cloth_frame_bakes':cloth_bakes,'separate_effect_ids':sorted(base.EFFECT_IDS,key=int),
        'layer_root':str((args.work/'layers').resolve())}
    (args.work/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'TANGSENG FRAME EXPORT PASS: {len(frames)} frames; {len(actions)} actions; '
          f'cloth {args.cloth}, weapon {args.weapon}; layer kind {args.kind}')


def rasterize(args):
    """Reuse the existing wardrobe rasterizer with this character's frame map."""
    manifest=json.loads((args.work/'manifest.json').read_text(encoding='utf-8'))
    if manifest['source_sha256']!=ROLE_HASH or manifest['role']!='Role2':
        raise ValueError('Only audited Role2 frames may be rasterized')
    selector=manifest['cloth_selector'] if manifest['kind']=='body' else manifest['weapon_selector']
    data={
        'kind':manifest['kind'], 'selector':selector, 'columns':manifest['columns'],
        'rows':(len(manifest['frames'])+5)//6,
        'poses':{str(i):[frame['source_sprite'],frame['source_frame']] for i,frame in enumerate(manifest['frames'])},
        'inputs':{str(frame['source_sprite']):str((args.work/'layers'/frame['action']).resolve())
                  for frame in manifest['frames']},
        'output':str((args.work/f'tangseng_{manifest["kind"]}_{selector}.png').resolve()),
    }
    target=args.work/'raster_manifest.json'
    target.write_text(json.dumps(data,indent=2),encoding='utf-8')
    project=Path(__file__).resolve().parents[2]/'Godot/GodotProject'
    with (args.work/'raster.log').open('w',encoding='utf-8') as log:
        subprocess.run([str(args.godot),'--headless','--path',str(project),'--script',
            'res://EditorScripts/import_official_wukong_equipment.gd','--',str(target.resolve())],
            stdout=log,stderr=subprocess.STDOUT,check=True)
    if not Path(data['output']).is_file():
        raise ValueError('Existing wardrobe rasterizer did not save the Role2 atlas')
    print('TANGSENG ATLAS PASS:',data['output'])


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['role','xml','audit','java','ffdec','work']:
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--kind',choices=['body','weapon'],required=True)
    parser.add_argument('--cloth',type=int,default=1)
    parser.add_argument('--weapon',type=int,default=1)
    parser.add_argument('--rasterize-only',action='store_true')
    parser.add_argument('--godot',type=Path)
    args=parser.parse_args()
    if args.rasterize_only:
        if args.godot is None:
            parser.error('--rasterize-only requires --godot')
        rasterize(args)
    else:
        export(args)
