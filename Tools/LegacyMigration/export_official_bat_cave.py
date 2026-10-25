"""Export the matched ZMXY1 bat cave actors and terrain into a staging directory.

Sources are v25928, Monster_v1 and Monster2_v4; see OfficialReference.md.
Existing Godot scenes and character atlases are never rewritten by this tool.
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

from export_official_battle_menu import shape_sprite
from export_official_wukong import matrix, timeline_frames


def export(args):
    """Retain source registrations and hit windows while excluding debug probes."""
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'profile').mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(work / 'profile'))
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

    def run(*arguments):
        with (work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(arguments), env=environment,
                           stdout=log, stderr=subprocess.STDOUT, check=True)

    # FFDec distributes its public SWF classes in lib/ffdec_lib.jar.
    compiler_classes = work / 'bounds-classes.jar'
    with ZipFile(args.ffdec.parent/'lib/ffdec_lib.jar') as source, ZipFile(compiler_classes, 'w') as target:
        for member in source.infolist():
            if member.filename.upper() != 'META-INF/MANIFEST.MF':
                target.writestr(member, source.read(member.filename))
    classpath = str(compiler_classes)
    subprocess.run([str(args.java.with_name('javac.exe')), '-proc:none', '-cp', str(classpath),
                    '-d', str(work), str(Path(__file__).with_name('FlashSpriteBounds.java'))],
                   env=environment, check=True)
    manifest = {'fps': 24, 'actors': []}
    for name, symbol, source_path, expected in [
        ('bat', 'export.monster.Monster8', args.monsters,
         '5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a'),
        ('bat_cave_statue', 'export.monster.Monster21', args.monster2,
         '42a301a5ae4222aec54dfdc4c14efced48b9ad4f979902471011ff226d8cd09b')]:
        content = source_path.read_bytes()
        if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
            content = content[100:110] + content[:100] + content[110:]
        if hashlib.sha256(content).hexdigest() != expected:
            raise ValueError(f'Unexpected official revision: {symbol}')
        swf = work / f'{name}.swf'
        xml = work / f'{name}.xml'
        swf.write_bytes(content)
        run('-swf2xml', str(swf), str(xml))
        tree = E.parse(xml)
        tags = tree.getroot().find('tags')
        sprites = {int(t.get('spriteId')): t for t in tags if t.get('spriteId')}
        shapes = {int(t.get('shapeId')): t for t in tags if t.get('shapeId')}
        symbols = {}
        for tag in tags:
            if tag.get('type') == 'SymbolClassTag':
                symbols.update(zip((x.text for x in tag.find('names')),
                                   (int(x.text) for x in tag.find('tags'))))

        def rectangle(sid, transform):
            if sid in sprites:
                child = next(iter(next(timeline_frames(sprites[sid])).values()))
                inner = matrix(child)
                a, b, c, d, x, y = transform
                ia, ib, ic, id_, ix, iy = inner
                return rectangle(int(child.get('characterId')),
                    [a*ia+c*ib, b*ia+d*ib, a*ic+c*id_, b*ic+d*id_, a*ix+c*iy+x, b*ix+d*iy+y])
            box = shapes[sid].find('shapeBounds')
            a, b, c, d, tx, ty = transform
            points = [(a*int(box.get(x))/20+c*int(box.get(y))/20+tx,
                       b*int(box.get(x))/20+d*int(box.get(y))/20+ty)
                      for x, y in [('Xmin', 'Ymin'), ('Xmax', 'Ymin'), ('Xmax', 'Ymax'), ('Xmin', 'Ymax')]]
            xs, ys = zip(*points)
            return [min(xs), min(ys), max(xs)-min(xs), max(ys)-min(ys)]

        actor = {'name': name, 'symbol': symbol, 'source_sha256': expected, 'actions': []}
        label = None
        for tag in sprites[symbols[symbol]].find('subTags'):
            if tag.get('type') == 'FrameLabelTag':
                label = tag.get('name')
            elif tag.get('name') == 'colipse' and tag.get('characterId'):
                actor['hurt_bounds'] = rectangle(int(tag.get('characterId')), matrix(tag))
            elif tag.get('name') == 'body' and tag.get('characterId'):
                sid = int(tag.get('characterId'))
                action = {'name': {'walk': 'run', 'wait': 'idle', 'hurt': 'hurt',
                                  'dead': 'death', 'hit1': 'attack_1'}[label],
                          'sprite': sid, 'matrix': matrix(tag),
                          'frames': int(sprites[sid].get('frameCount')), 'hit_bounds': []}
                for display in timeline_frames(sprites[sid]):
                    probe = next((t for t in display.values() if t.get('name') == 'stick'), None)
                    action['hit_bounds'].append(rectangle(int(probe.get('characterId')), matrix(probe))
                                                if probe is not None else None)
                actor['actions'].append(action)
        # Static statue labels keep the preceding body; an alias retains that exact pose.
        if not any(a['name'] == 'hurt' for a in actor['actions']):
            idle = deepcopy(next(a for a in actor['actions'] if a['name'] == 'idle'))
            actor['actions'].append({**idle, 'name': 'hurt'})
        selected = {a['sprite'] for a in actor['actions']}
        for sid in selected:
            children = sprites[sid].find('subTags')
            removed_depths = set()
            for tag in list(children):
                if tag.get('name') == 'stick':
                    removed_depths.add(tag.get('depth'))
                if tag.get('depth') in removed_depths:
                    children.remove(tag)
        art_xml, art_swf = work / f'{name}_art.xml', work / f'{name}_art.swf'
        tree.write(art_xml, encoding='utf-8', xml_declaration=True)
        run('-xml2swf', str(art_xml), str(art_swf))
        png = work / f'{name}_png'
        run('-format', 'sprite:png', '-selectid', ','.join(map(str, sorted(selected))),
            '-ignorebackground', '-export', 'sprite', str(png), str(art_swf))
        runtime_classpath = str(work) + os.pathsep + str(args.ffdec) + os.pathsep + str(args.ffdec.parent/'lib/*')
        bounds_text = subprocess.check_output([str(args.java), '-cp', runtime_classpath,
            'FlashSpriteBounds', str(art_swf), *map(str, sorted(selected))], env=environment, text=True)
        bounds = {int(row[0]): [int(x)/20 for x in row[1:]]
                  for line in bounds_text.splitlines() if len(row := line.split()) == 5}
        folders = {int(p.name.split('_')[1]): p for p in png.iterdir() if p.is_dir()}
        actor['layers'] = [{'name': 'body', 'output': str(work / f'{name}_body.png'),
                            'actions': [{**a, 'bounds': bounds[a['sprite']], 'input': str(folders[a['sprite']])}
                                        for a in actor['actions']]}]
        manifest['actors'].append(actor)
    # New terrain only; existing three levels are outside this export's write scope.
    tree = E.parse(args.main_xml)
    tags = tree.getroot().find('tags')
    tags.insert(len(tags)-1, shape_sprite(107, 32107))
    terrain_xml, terrain_swf = work/'terrain.xml', work/'terrain.swf'
    tree.write(terrain_xml, encoding='utf-8', xml_declaration=True)
    run('-xml2swf', str(terrain_xml), str(terrain_swf))
    run('-selectid', '185,113,32107', '-ignorebackground', '-export', 'sprite', str(work/'terrain_png'), str(terrain_swf))
    # MainGame.newGame selects FloorBg frame 2 (bitmap 18) for the second map.
    run('-selectid', '18', '-export', 'image', str(work/'sky'), str(terrain_swf))
    (work/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('BAT CAVE EXPORT PASS:', [(a['name'], sum(x['frames'] for x in a['actions'])) for a in manifest['actors']])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('monsters', 'monster2', 'main_xml', 'java', 'ffdec', 'work'):
        parser.add_argument('--'+name.replace('_', '-'), type=Path, required=True)
    export(parser.parse_args())
