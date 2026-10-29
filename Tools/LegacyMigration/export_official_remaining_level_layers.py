"""Stage original ZMXY1 later-level layers and music for native Godot authoring.

Keeps individual terrain pieces, backgrounds and source registrations separate.
Traps, exits, hidden entrances and dynamic boss effects are not baked into art.
Writes only --work; it never replaces an existing game scene or asset.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as E

from export_official_battle_menu import shape_sprite
from audit_official_level_dependencies import SOURCE_HASHES


LEVELS = ('2-3', '3-1', '3-2', '3-3', '4-1', '9-1')
UTILITY = {53, 54, 57, 61, 86, 99, 113}


def export(args):
    """Render exact native layers and extract unmodified MP3 payloads."""
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    inventory = json.loads(args.inventory.read_text(encoding='utf-8'))
    repo = Path(__file__).resolve().parents[2]
    sources = {p['name']: p for p in inventory['sources']}
    for key in ('main', 'music'):
        swf = repo/sources[key]['swf']
        if hashlib.sha256(swf.read_bytes()).hexdigest() != SOURCE_HASHES[key]:
            raise ValueError(f'Unexpected {key} source revision')
    main = E.parse(repo/sources['main']['xml'])
    tags = main.getroot().find('tags')
    definitions = {int(t.get('shapeId')): t for t in tags if t.get('shapeId')}
    definitions.update({int(t.get('spriteId')): t for t in tags if t.get('spriteId')})
    levels = json.loads(args.inventory.with_name('all_levels.json').read_text(encoding='utf-8'))
    selected = set()
    images = set()
    records = []
    wrappers = {}
    for level in inventory['levels']:
        label = level['label']
        if label not in LEVELS:
            continue
        entries = []
        for item in levels[label]['scenery']:
            sid = item['characterId']
            if sid in UTILITY:
                continue
            definition = definitions[sid]
            if definition.get('shapeId'):
                if sid not in wrappers:
                    wrapper = 31000 + sid
                    if wrapper in definitions:
                        raise ValueError(f'Wrapper collision: {wrapper}')
                    tags.insert(len(tags)-1, shape_sprite(sid, wrapper))
                    wrappers[sid] = wrapper
                export_id = wrappers[sid]
                box = definition.find('shapeBounds')
                registration = [int(box.get(k))/20 for k in ('Xmin', 'Ymin')]
            else:
                raise ValueError(f'Review dynamic scene child before baking it: {label}/{sid}')
            selected.add(export_id)
            entries.append({'source_character': sid, 'export_sprite': export_id,
                            'matrix': item['matrix'], 'local_top_left': registration,
                            'file': f'sprite_{export_id}.png'})
        selected.add(level['front_sprite'])
        images.update(level['floor_characters'])
        records.append({'label': label, 'front_sprite': level['front_sprite'],
                        'front_position': level['front_position'],
                        'front_file': f"sprite_{level['front_sprite']}.png",
                        'floor_characters': level['floor_characters'],
                        'music_symbol': level['music_symbol'], 'terrain': entries})
    main.write(work/'layers.xml', encoding='utf-8', xml_declaration=True)
    env = dict(os.environ, APPDATA=str(work/'ffdec_profile'))
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

    def run(*params):
        with (work/'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command+list(params), env=env, stdout=log,
                           stderr=subprocess.STDOUT, check=True)

    run('-xml2swf', str(work/'layers.xml'), str(work/'layers.swf'))
    run('-format', 'sprite:png', '-selectid', ','.join(map(str, sorted(selected))),
        '-ignorebackground', '-export', 'sprite', str(work/'png'), str(work/'layers.swf'))
    run('-selectid', ','.join(map(str, sorted(images))), '-export', 'image',
        str(work/'images'), str(work/'layers.swf'))
    output = work/'layers'
    output.mkdir(exist_ok=True)
    for sid in sorted(selected):
        folder = next(p for p in (work/'png').iterdir() if int(p.name.split('_')[1]) == sid)
        files = list(folder.glob('*.png'))
        if len(files) != 1:
            raise ValueError(f'Static layer {sid} exported {len(files)} frames')
        shutil.copyfile(files[0], output/f'sprite_{sid}.png')
    music_ids = inventory['music_linkages']
    run('-selectid', ','.join(str(music_ids[name]) for name in ('bg2', 'bg3', 'bg4')),
        '-export', 'sound', str(work/'sounds'), str(repo/sources['music']['swf']))
    music = []
    for name in ('bg2', 'bg3', 'bg4'):
        source = next((work/'sounds').glob(f'{music_ids[name]}_*.mp3'))
        target = output/(name+'.mp3')
        shutil.copyfile(source, target)
        music.append({'symbol': name, 'sound_id': music_ids[name],
                      'file': target.name, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
    (work/'layout.json').write_text(json.dumps({'levels': records, 'music': music},
        ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f'LATER LEVEL LAYERS: {len(records)} levels, {len(selected)} native PNG layers, '
          f'{len(images)} source sky images, {len(music)} MP3s; {output}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('inventory', 'java', 'ffdec', 'work'):
        parser.add_argument('--'+name, type=Path, required=True)
    export(parser.parse_args())
