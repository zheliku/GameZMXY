"""Export the complete official 2-3 actors and their dynamic visuals to staging.

Preserves every action's parent registration, named gameplay probes and frame
scripts. The manifest is authoring evidence, never a runtime action interpreter.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as E
from zipfile import ZipFile

from export_official_body_events import frame_events
from export_official_wukong import matrix, timeline_frames

ACTORS = {9: 'peng_demon_king', 15: 'flying_demon'}
LABELS = {'walk': 'run', 'wait': 'idle', 'dead': 'death', 'hurt': 'hurt', 'fly': 'fly',
          'turnToEgg': 'egg', 'reBurn': 'rebirth', **{f'hit{i}': f'attack_{i}' for i in range(1, 5)}}


def export(args):
    """Export both species, the full nested fire timeline and the death explosion."""
    repo = Path(__file__).resolve().parents[2]
    inventory = json.loads(args.inventory.read_text(encoding='utf-8'))
    source = next(item for item in inventory['sources'] if item['name'] == 'monster2')
    swf = repo / source['swf']
    if hashlib.sha256(swf.read_bytes()).hexdigest() != source['sha256']:
        raise ValueError('Monster2 source revision differs from the verified dependency audit')
    tree = E.parse(repo / source['xml'])
    tags = tree.getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('spriteId')}
    symbols = {name.text: int(value.text) for tag in tags if tag.get('type') == 'SymbolClassTag'
               for name, value in zip(tag.find('names'), tag.find('tags'))}
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'profile').mkdir(exist_ok=True)
    env = dict(os.environ, APPDATA=str(work / 'profile'))
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

    def run(*values):
        with (work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(values), env=env, stdout=log, stderr=subprocess.STDOUT, check=True)

    actors = []
    selected = set()
    for number, name in ACTORS.items():
        record = deepcopy(next(item for item in inventory['actors'] if item['id'] == number))
        actor = {'name': name, 'legacy_id': number, 'source_sha256': source['sha256'],
                 'symbol': record['symbol'], 'actions': [], 'behavior_script': record['source_script']}
        script_by_sprite = {item['sprite']: repo / item['path'] for item in record['timeline_scripts']}
        for action in record['actions']:
            if action['label'] not in LABELS:
                raise ValueError(f'Unreviewed actor label: {number}/{action["label"]}')
            sid = action['sprite']
            action['name'] = LABELS[action['label']]
            action['hit_sources'] = [[{'character': int(child.get('characterId')), 'matrix': matrix(child),
                                       'ratio': int(child.get('ratio', '0'))}
                                      for child in display.values() if child.get('name') == 'stick']
                                     for display in timeline_frames(sprites[sid])]
            action['frame_events'] = [{'frame': frame + 1, 'body': code} for frame, code in
                                      frame_events(script_by_sprite[sid].read_text(encoding='utf-8-sig'))] if sid in script_by_sprite else []
            actor['actions'].append(action)
            selected.add(sid)
        actors.append(actor)
    effects = []
    for name, symbol in [('peng_fire', 'Boss4Bullet1')]:
        sid = symbols[symbol]
        frames = int(sprites[sid].get('frameCount'))
        if symbol == 'Boss4Bullet1':
            # The linked clip has one root frame; its three children animate for
            # 15 frames and destroy the BaseBullet from their frame-15 script.
            children = {int(tag.get('characterId')) for tag in sprites[sid].find('subTags') if tag.get('characterId')}
            if frames != 1 or len(children) != 1 or int(sprites[next(iter(children))].get('frameCount')) != 15:
                raise ValueError('Review changed nested Boss4Bullet1 timing before exporting')
            frames = 15
            sub_tags = sprites[sid].find('subTags')
            for _ in range(frames - 1):
                sub_tags.insert(len(sub_tags) - 1, E.Element('item', type='ShowFrameTag', forceWriteAsLong='false'))
            sprites[sid].set('frameCount', str(frames))
        effects.append({'name': name, 'symbol': symbol, 'actions': [{'name': 'play', 'sprite': sid,
                        'frames': frames, 'matrix': [1, 0, 0, 1, 0, 0]}]})
        selected.add(sid)
    xml, art_swf = work / 'garuda_art.xml', work / 'garuda_art.swf'
    tree.write(xml, encoding='utf-8', xml_declaration=True)
    run('-xml2swf', str(xml), str(art_swf))
    run('-format', 'sprite:png', '-selectid', ','.join(map(str, sorted(selected))),
        '-ignorebackground', '-export', 'sprite', str(work / 'png'), str(art_swf))
    compiler = work / 'bounds-classpath.jar'
    with ZipFile(args.ffdec.parent / 'lib/ffdec_lib.jar') as source_jar, ZipFile(compiler, 'w') as target:
        for item in source_jar.infolist():
            if item.filename.upper() != 'META-INF/MANIFEST.MF':
                target.writestr(item, source_jar.read(item.filename))
    subprocess.run([str(args.java.with_name('javac.exe')), '-proc:none', '-cp', str(compiler), '-d', str(work),
                    str(Path(__file__).with_name('FlashSpriteBounds.java'))], env=env, check=True)
    bounds_text = subprocess.check_output([str(args.java), '-cp', os.pathsep.join([str(work), str(args.ffdec),
        str(args.ffdec.parent / 'lib/*')]), 'FlashSpriteBounds', str(art_swf), *map(str, sorted(selected))], env=env, text=True)
    bounds = {int(row[0]): [int(value) / 20 for value in row[1:]] for line in bounds_text.splitlines()
              if len(row := line.split()) == 5}
    folders = {int(path.name.split('_')[1]): path for path in (work / 'png').iterdir() if path.is_dir()}
    for actor in actors + effects:
        for action in actor['actions']:
            sid = action['sprite']
            files = list(folders[sid].glob('*.png'))
            if len(files) != action['frames']:
                raise ValueError(f'Incomplete source export: {actor["name"]}/{action["name"]}')
            action['bounds'] = bounds[sid]
            action['input'] = str(folders[sid])
        actor['layers'] = [{'name': 'body', 'actions': actor['actions'],
                            'output': str(work / (actor['name'] + '_body.png'))}]
    # Aloader resolves this shared death clip from Monster1, not Monster2.
    death_source = next(item for item in inventory['sources'] if item['name'] == 'monster1')
    death_swf = repo / death_source['swf']
    if hashlib.sha256(death_swf.read_bytes()).hexdigest() != death_source['sha256']:
        raise ValueError('Shared boss-death source revision differs')
    death_tags = E.parse(repo / death_source['xml']).getroot().find('tags')
    death_symbols = {name.text: int(value.text) for tag in death_tags if tag.get('type') == 'SymbolClassTag'
                     for name, value in zip(tag.find('names'), tag.find('tags'))}
    death_id = death_symbols['BossDead']
    death_sprite = next(tag for tag in death_tags if tag.get('spriteId') == str(death_id))
    run('-format', 'sprite:png', '-selectid', str(death_id), '-ignorebackground', '-export',
        'sprite', str(work / 'death_png'), str(death_swf))
    death_bounds_text = subprocess.check_output([str(args.java), '-cp', os.pathsep.join([str(work), str(args.ffdec),
        str(args.ffdec.parent / 'lib/*')]), 'FlashSpriteBounds', str(death_swf), str(death_id)], env=env, text=True)
    death_bounds = [int(value) / 20 for value in death_bounds_text.strip().split()[1:]]
    death_folder = next((work / 'death_png').iterdir())
    death_frames = int(death_sprite.get('frameCount'))
    if len(list(death_folder.glob('*.png'))) != death_frames:
        raise ValueError('Incomplete shared boss-death export')
    death_action = {'name': 'play', 'sprite': death_id, 'frames': death_frames, 'matrix': [1, 0, 0, 1, 0, 0],
                    'bounds': death_bounds, 'input': str(death_folder)}
    effects.append({'name': 'boss_death', 'symbol': 'BossDead', 'source_sha256': death_source['sha256'],
                    'actions': [death_action], 'layers': [{'name': 'body', 'actions': [death_action],
                    'output': str(work / 'boss_death_body.png')}]})
    result = {'fps': 24, 'source_sha256': source['sha256'], 'actors': actors, 'effects': effects,
              'notes': ['Named probe records retain source transforms and morph ratios; native gameplay geometry still needs authoring.',
                        'Peng fire has a separate 0.6-second fade before its 15-frame nested timeline starts.']}
    (work / 'manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('GARUDA SOURCE ART PASS:', [(item['name'], sum(a['frames'] for a in item['actions'])) for item in actors + effects])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('inventory', 'java', 'ffdec', 'work'):
        parser.add_argument('--' + name, type=Path, required=True)
    export(parser.parse_args())
