"""Export matched ZMXY1 2-2 artwork without touching existing authored assets.

The manifest preserves full source registration, action frames and stick windows.
It is authoring input for native Godot resources, not a runtime action interpreter.
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

from export_official_wukong import matrix, timeline_frames


ACTORS = [
    ('cave_brute', 'export.monster.Monster7', 1),
    ('knife_demon', 'export.monster.Monster13', 2),
    ('lion_camel_king', 'export.monster.Monster11', 2),
]
SOURCE_HASHES = {
    1: '5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a',
    2: '42a301a5ae4222aec54dfdc4c14efced48b9ad4f979902471011ff226d8cd09b',
}
LABELS = {'walk': 'run', 'wait': 'idle', 'hurt': 'hurt', 'dead': 'death',
          'hit1': 'attack_1', 'hit2': 'attack_2', 'hit3': 'attack_3'}


def transformed_bounds(bounds, transform):
    """Use one enclosing rectangle for a source gameplay region, in actor coordinates."""
    a, b, c, d, tx, ty = transform
    points = [(a*x+c*y+tx, b*x+d*y+ty)
              for x in (bounds[0], bounds[2]) for y in (bounds[1], bounds[3])]
    xs, ys = zip(*points)
    return [min(xs), min(ys), max(xs)-min(xs), max(ys)-min(ys)]


def export(args):
    """Verify both official revisions and export the three actors and independent effects."""
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    profile = work/'profile'
    profile.mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(profile))
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

    def run(*arguments):
        with (work/'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command+list(arguments), env=environment, stdout=log,
                           stderr=subprocess.STDOUT, check=True)

    compiler = work/'bounds-classpath.jar'
    with ZipFile(args.ffdec.parent/'lib/ffdec_lib.jar') as source, ZipFile(compiler, 'w') as destination:
        for member in source.infolist():
            if member.filename.upper() != 'META-INF/MANIFEST.MF':
                destination.writestr(member, source.read(member.filename))
    subprocess.run([str(args.java.with_name('javac.exe')), '-proc:none', '-cp', str(compiler),
                    '-d', str(work), str(Path(__file__).with_name('FlashSpriteBounds.java'))],
                   env=environment, check=True)
    runtime_classpath = os.pathsep.join([str(work), str(args.ffdec), str(args.ffdec.parent/'lib/*')])
    manifests = {'fps': 24, 'actors': [], 'effects': []}
    for number, source_path in [(1, args.monsters), (2, args.monster2)]:
        content = source_path.read_bytes()
        if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
            content = content[100:110]+content[:100]+content[110:]
        if hashlib.sha256(content).hexdigest() != SOURCE_HASHES[number]:
            raise ValueError(f'Unexpected Monster source revision: {number}')
        swf, xml = work/f'monster{number}.swf', work/f'monster{number}.xml'
        swf.write_bytes(content)
        run('-swf2xml', str(swf), str(xml))
        tree = E.parse(xml)
        tags = tree.getroot().find('tags')
        sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('spriteId')}
        symbols = {}
        for tag in tags:
            if tag.get('type') == 'SymbolClassTag':
                symbols.update(zip((x.text for x in tag.find('names')),
                                   (int(x.text) for x in tag.find('tags'))))
        actors = []
        selected = set()
        bounds_ids = set()
        for name, symbol, source_number in ACTORS:
            if source_number != number:
                continue
            actor = {'name': name, 'symbol': symbol, 'source_sha256': SOURCE_HASHES[number], 'actions': []}
            label = None
            for tag in sprites[symbols[symbol]].find('subTags'):
                if tag.get('type') == 'FrameLabelTag':
                    label = tag.get('name')
                elif tag.get('name') == 'colipse' and tag.get('characterId') and 'hurt_source' not in actor:
                    actor['hurt_source'] = {'sprite': int(tag.get('characterId')), 'matrix': matrix(tag)}
                    bounds_ids.add(int(tag.get('characterId')))
                elif tag.get('name') == 'body' and tag.get('characterId'):
                    sid = int(tag.get('characterId'))
                    action = {'name': LABELS[label], 'sprite': sid, 'matrix': matrix(tag),
                              'frames': int(sprites[sid].get('frameCount')), 'hit_sources': []}
                    for display in timeline_frames(sprites[sid]):
                        sticks = [value for value in display.values() if value.get('name') == 'stick']
                        action['hit_sources'].append([{'sprite': int(stick.get('characterId')), 'matrix': matrix(stick)}
                                                      for stick in sticks])
                        for stick in sticks:
                            bounds_ids.add(int(stick.get('characterId')))
                    actor['actions'].append(action)
                    selected.add(sid)
            actors.append(actor)
        effects = []
        if number == 2:
            for name, symbol in [('knife_demon_blade', 'Monster13Bullet1'),
                                 ('lion_camel_poison_cloud', 'Boss6Bullet1'),
                                 ('poison_head', 'poisonHead'), ('poison_up', 'poisonUp')]:
                sid = symbols[symbol]
                action = {'name': 'play', 'sprite': sid, 'matrix': [1, 0, 0, 1, 0, 0],
                          'frames': int(sprites[sid].get('frameCount'))}
                if name == 'knife_demon_blade':
                    action['hit_sources'] = []
                    for display in timeline_frames(sprites[sid]):
                        sources = []
                        for child in display.values():
                            child_id = child.get('characterId')
                            if child_id:
                                child_id = int(child_id)
                                bounds_ids.add(child_id)
                                sources.append({'sprite': child_id, 'matrix': matrix(child)})
                        action['hit_sources'].append(sources)
                effects.append({'name': name, 'actions': [action]})
                selected.add(sid)
        # Monster7 uses an invisible debug probe. The second pack's named sticks are visible weapon art.
        variant = deepcopy(tree)
        variant_sprites = {int(tag.get('spriteId')): tag for tag in variant.getroot().find('tags') if tag.get('spriteId')}
        if number == 1:
            for sid in selected:
                children = variant_sprites[sid].find('subTags')
                removed = set()
                for tag in list(children):
                    if tag.get('name') == 'stick' and tag.get('characterId') == '26':
                        removed.add(tag.get('depth'))
                    if tag.get('depth') in removed:
                        children.remove(tag)
        art_xml, art_swf = work/f'monster{number}_art.xml', work/f'monster{number}_art.swf'
        variant.write(art_xml, encoding='utf-8', xml_declaration=True)
        run('-xml2swf', str(art_xml), str(art_swf))
        png = work/f'monster{number}_png'
        run('-format', 'sprite:png', '-selectid', ','.join(map(str, sorted(selected))),
            '-ignorebackground', '-export', 'sprite', str(png), str(art_swf))
        # Probe bounds come from the unmodified file; pose bounds from the exported artwork file.
        def read_bounds(source_swf, ids):
            result = subprocess.check_output([str(args.java), '-cp', runtime_classpath, 'FlashSpriteBounds',
                                              str(source_swf), *map(str, sorted(ids))], env=environment, text=True)
            return {int(row[0]): [int(value)/20 for value in row[1:]]
                    for line in result.splitlines() if len(row := line.split()) == 5}
        bounds = read_bounds(swf, bounds_ids)
        art_bounds = read_bounds(art_swf, selected)
        folders = {int(path.name.split('_')[1]): path for path in png.iterdir() if path.is_dir()}
        for actor in actors:
            collider = actor.pop('hurt_source')
            actor['hurt_bounds'] = transformed_bounds(bounds[collider['sprite']], collider['matrix'])
            for action in actor['actions']:
                boxes = []
                for sticks in action.pop('hit_sources'):
                    if not sticks:
                        boxes.append(None)
                    else:
                        regions = []
                        for stick in sticks:
                            transform = list(stick['matrix'])
                            transform[4] += action['matrix'][4]
                            transform[5] += action['matrix'][5]
                            regions.append(transformed_bounds(bounds[stick['sprite']], transform))
                        x = min(box[0] for box in regions)
                        y = min(box[1] for box in regions)
                        boxes.append([x, y, max(box[0]+box[2] for box in regions)-x,
                                      max(box[1]+box[3] for box in regions)-y])
                action['hit_bounds'] = boxes
        for actor in effects:
            if actor['name'] != 'knife_demon_blade':
                continue
            for action in actor['actions']:
                boxes = []
                for sources in action.pop('hit_sources'):
                    regions = [transformed_bounds(bounds[source['sprite']], source['matrix'])
                               for source in sources]
                    regions = [box for box in regions if box[2] > 0 and box[3] > 0]
                    if not regions:
                        boxes.append(None)
                    else:
                        x = min(box[0] for box in regions)
                        y = min(box[1] for box in regions)
                        boxes.append([x, y, max(box[0] + box[2] for box in regions) - x,
                                      max(box[1] + box[3] for box in regions) - y])
                action['hit_bounds'] = boxes
        for actor in actors+effects:
            actor['layers'] = [{'name': 'body', 'output': str(work/(actor['name']+'_body.png')),
                                'actions': [{**action, 'bounds': art_bounds[action['sprite']],
                                             'input': str(folders[action['sprite']])} for action in actor['actions']]}]
        manifests['actors'].extend(actors)
        manifests['effects'].extend(effects)
    (work/'manifest.json').write_text(json.dumps(manifests, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('QIXING ART EXPORT PASS:', [(actor['name'], sum(a['frames'] for a in actor['actions']))
                                       for actor in manifests['actors']+manifests['effects']])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('monsters', 'monster2', 'java', 'ffdec', 'work'):
        parser.add_argument('--'+name, type=Path, required=True)
    export(parser.parse_args())
