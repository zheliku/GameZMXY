"""Build independent native Flash panoramas for the first three ZMXY1 levels.

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
    for number in [1, 2, 3]:
        label = f'1-{number}'
        sprite = E.Element('item', type='DefineSpriteTag', spriteId=str(32000+number), frameCount='1')
        children = E.SubElement(sprite, 'subTags')
        container = next(tag for tag in scenes[label].values() if tag.get('name') == 'bgContainer')
        background = deepcopy(next(iter(backgrounds[label].values())))
        background.set('depth', '1')
        background.set('placeFlagMove', 'false')
        matrix = background.find('matrix')
        offset = container.find('matrix')
        matrix.set('translateX', str(int(matrix.get('translateX')) + int(offset.get('translateX'))))
        matrix.set('translateY', str(int(matrix.get('translateY')) + int(offset.get('translateY')) + 65*20))
        children.append(background)
        for depth, tag in sorted(scenes[label].items(), key=lambda item: int(item[0])):
            if int(tag.get('characterId')) not in [56, 60, 88, 97, 101, 103]:
                continue
            placed = deepcopy(tag)
            placed.set('depth', str(int(depth)+2))
            placed.set('placeFlagMove', 'false')
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
        subprocess.run(command + ['-selectid', '32001,32002,32003', '-ignorebackground', '-export',
                                  'sprite', str(args.work/'png'), str(args.work/'reference.swf')],
                       env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
    print('OFFICIAL REFERENCE EXPORT PASS: direct GameSence/bgClips, original frame labels and placements.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['xml', 'java', 'ffdec', 'work']:
        parser.add_argument('--' + name, type=Path, required=True)
    render(parser.parse_args())
