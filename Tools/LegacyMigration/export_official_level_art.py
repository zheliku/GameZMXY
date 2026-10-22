"""Render all visible terrain for the first three levels from decoded v25928 XML.

The source revision and official URL are recorded in Docs/OfficialReference.md.
Only PNGs are copied; scenes and SpriteFrames are saved by Godot editor scripts.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as E
from export_official_battle_menu import shape_sprite
from export_official_wukong import matrix


def export(args):
    """Keep native shape bounds, child transforms, filters and frame ordering."""
    args.work.mkdir(parents=True, exist_ok=True)
    profile = args.work / 'profile'
    profile.mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(profile))
    command = [str(args.java), f'-Duser.home={args.work}', '-jar', str(args.ffdec)]
    tree = E.parse(args.xml)
    tags = tree.getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('type') == 'DefineSpriteTag'}
    assert sprites[85].get('frameCount') == '20', 'Unexpected source revision'
    for sid in [56, 60, 88, 97, 101]:
        tags.insert(len(tags) - 1, shape_sprite(sid, 31000 + sid))
    tree.write(args.work / 'level_art.xml', encoding='utf-8', xml_declaration=True)
    with (args.work / 'export.log').open('w', encoding='utf-8') as log:
        subprocess.run(command + ['-xml2swf', str(args.work / 'level_art.xml'), str(args.work / 'level_art.swf')],
                       env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
        subprocess.run(command + ['-selectid', '85,103,161,163,168,31056,31060,31088,31097,31101', '-ignorebackground', '-export', 'sprite',
                                  str(args.work / 'png'), str(args.work / 'level_art.swf')],
                       env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
    def directory(sid):
        return next(path for path in (args.work / 'png').iterdir() if path.name.split('_')[1] == str(sid))
    ground = args.output / 'Levels/zm1_01_huaguoshan'
    ground.mkdir(parents=True, exist_ok=True)
    for sid, name in [(31056, 'huaguoshan_platform'), (31060, 'huaguoshan_ground')]:
        shutil.copyfile(directory(sid) / '1.png', ground / f'{name}.png')
    for area, entries in [
        ('huaguoshan', [(161, 'level_1_front')]),
        ('shuiliandong', [(163, 'shuiliandong_front'), (31088, 'shuiliandong_platform'),
                         (31097, 'shuiliandong_ground')]),
        ('taohuayuan', [(168, 'taohuayuan_front'), (31101, 'taohuayuan_ground'), (103, 'taohuayuan_rock')])]:
        folder = args.output / 'Levels' / {'shuiliandong':'zm1_02_shuiliandong', 'taohuayuan':'zm1_03_taohuayuan'}[area]
        folder.mkdir(parents=True, exist_ok=True)
        for sid, name in entries:
            shutil.copyfile(directory(sid) / '1.png', folder / f'{name}.png')
    exit_folder = args.output / 'Effects/level_exit'
    exit_folder.mkdir(parents=True, exist_ok=True)
    for index in range(20):
        shutil.copyfile(directory(85) / f'{index+1}.png', exit_folder / f'level_exit_{index:02d}.png')
    bounds_command = [str(args.java), f'-Duser.home={args.work}', '--class-path', str(args.ffdec),
                      str(Path(__file__).with_name('FlashSpriteBounds.java')), str(args.work / 'level_art.swf'),
                      '85', '31056', '31060']
    result = subprocess.run(bounds_command, env=environment, capture_output=True, text=True, check=True)
    bounds = {int(values[0]): [int(n)/20 for n in values[1:]] for line in result.stdout.splitlines()
              if len(values := line.split()) == 5}
    placement = next(tag for tag in sprites[86].find('subTags') if tag.get('characterId') == '85')
    offset = matrix(placement)[4:]
    manifest = {'bounds': bounds, 'exit_child_offset': offset, 'exit_top_left':
                [bounds[85][0]+offset[0], bounds[85][1]+offset[1]], 'exit_frames': 20, 'fps': 24}
    (args.work / 'layout.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['xml', 'java', 'ffdec', 'work']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2] /
                        'Godot/GodotProject/TheGame/Sprites')
    export(parser.parse_args())
