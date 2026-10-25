"""Build independent native Flash panoramas for selected ZMXY1 levels.

Reads original GameSence/bgClips display lists directly. Excludes debug markers,
the dynamic river and exit animation; no Godot scene or extracted manifest is read.
"""
import argparse
from copy import deepcopy
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as E
from export_official_wukong import timeline_frames


def labeled_frames(sprite):
    """Map each original label to its actual display-list frame."""
    labels, index = {}, 0
    for tag in sprite.find('subTags'):
        if tag.get('type') == 'FrameLabelTag':
            labels[tag.get('name')] = index
        elif tag.get('type') == 'ShowFrameTag':
            index += 1
    frames = list(timeline_frames(sprite))
    return {label: frames[frame] for label, frame in labels.items()}


def render(args):
    """Compose original objects at original depths, then export with FFDec's renderer."""
    tree = E.parse(args.xml)
    tags = tree.getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('type') == 'DefineSpriteTag'}
    scenes = labeled_frames(sprites[128])
    backgrounds = labeled_frames(sprites[253])
    exported = []
    next_id = max(32000, max(sprites)) + 1
    for label in args.levels:
        if label not in scenes or label not in backgrounds:
            raise ValueError(f'Unknown original stage label: {label}')
        exported.append((label, next_id))
        sprite = E.Element('item', type='DefineSpriteTag', spriteId=str(next_id), frameCount='1')
        next_id += 1
        children = E.SubElement(sprite, 'subTags')
        container = next(tag for tag in scenes[label].values() if tag.get('name') == 'bgContainer')
        background = deepcopy(next(iter(backgrounds[label].values())))
        background.set('depth', '1')
        background.set('placeFlagMove', 'false')
        background.set('placeFlagHasCharacter', 'true')
        matrix = background.find('matrix')
        offset = container.find('matrix')
        matrix.set('translateX', str(int(matrix.get('translateX')) + int(offset.get('translateX'))))
        matrix.set('translateY', str(int(matrix.get('translateY')) + int(offset.get('translateY')) + 65*20))
        children.append(background)
        for depth, tag in sorted(scenes[label].items(), key=lambda item: int(item[0])):
            if int(tag.get('characterId')) not in [56, 60, 88, 97, 101, 103, 107, 113,
                                                  115, 116, 117, 118, 121, 122, 123, 125, 127]:
                continue
            placed = deepcopy(tag)
            placed.set('depth', str(int(depth)+2))
            placed.set('placeFlagMove', 'false')
            # The resolved frame may come from a move-only tag. In this new
            # one-frame sprite it must place the resolved character explicitly.
            placed.set('placeFlagHasCharacter', 'true')
            children.append(placed)
        E.SubElement(children, 'item', type='ShowFrameTag')
        tags.insert(len(tags)-1, sprite)
    args.work.mkdir(parents=True, exist_ok=True)
    tree.write(args.work / 'reference.xml', encoding='utf-8', xml_declaration=True)
    profile = args.work / 'profile'
    profile.mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(profile))
    command = [str(args.java), f'-Duser.home={args.work}', '-jar', str(args.ffdec)]
    with (args.work / 'export.log').open('w', encoding='utf-8') as log:
        subprocess.run(command + ['-xml2swf', str(args.work/'reference.xml'), str(args.work/'reference.swf')],
                       env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
        selected = ','.join(str(sprite_id) for _, sprite_id in exported)
        subprocess.run(command + ['-selectid', selected, '-ignorebackground', '-export',
                                  'sprite', str(args.work/'png'), str(args.work/'reference.swf')],
                       env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
    print('OFFICIAL REFERENCE EXPORT PASS:', ', '.join(f'{label} = sprite {sid}' for label, sid in exported),
          '(direct GameSence/bgClips, original labels and placements).')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['xml', 'java', 'ffdec', 'work']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--levels', nargs='+', default=['1-1', '1-2', '1-3'],
                        help='Original map-stage labels; defaults to the first three levels.')
    render(parser.parse_args())
