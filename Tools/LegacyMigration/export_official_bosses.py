"""Export registered Monkey King and Tamarin King poses from Monster_v1.swf.

Native parent transforms, complete 24fps actions and stick windows are retained.
Intermediate Flash/XML/PNG files stay in --work. Godot assets are written only by
the companion rasterizer after a common per-character registration is measured.
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

SOURCE_HASH = '5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a'
ACTORS = {'macaque_king': 'export.monster.Monster4', 'tamarin_king': 'export.monster.Monster6'}
FOLDERS = {'macaque_king': 'zm1_02_macaque_king', 'tamarin_king': 'zm1_03_tamarin_king'}
LABELS = {'walk': 'run', 'wait': 'idle', 'hurt': 'hurt', 'dead': 'death',
          'hit1': 'attack_1', 'hit2': 'attack_2', 'hit3': 'attack_3'}


def filter_action(sprite, layer):
    """Separate native damaging visuals from the body without restarting child timelines."""
    tags = sprite.find('subTags')
    original = list(tags)
    tags.clear()
    shown = {}
    for tag in original:
        kind, depth = tag.get('type'), tag.get('depth')
        if kind.startswith('PlaceObject'):
            old = shown.get(depth, (False, 0, None))
            sid = int(tag.get('characterId')) if tag.get('placeFlagHasCharacter') == 'true' else old[1]
            name = tag.get('name') if tag.get('placeFlagHasName') == 'true' else old[2]
            effect = name == 'stick' and sid != 26
            include = sid != 26 and (effect if layer == 'effect' else not effect)
            shown[depth] = (include, sid, name)
            if include:
                placed = deepcopy(tag)
                if not old[0]:
                    placed.set('placeFlagMove', 'false')
                tags.append(placed)
            elif old[0]:
                E.SubElement(tags, 'item', type='RemoveObject2Tag', depth=depth, forceWriteAsLong='false')
        elif kind.startswith('RemoveObject'):
            if shown.pop(depth, (False,))[0]:
                tags.append(deepcopy(tag))
        else:
            tags.append(deepcopy(tag))


def write_sprite_frames(packed, project):
    """Create external SpriteFrames for new artwork without replacing authored resources."""
    for actor in packed['actors']:
        for layer in actor['layers']:
            atlas = Path(layer['output'])
            output = atlas.with_name(atlas.stem + '_frames.tres')
            if output.exists():
                continue
            atlas_path = 'res://' + Path(layer['output']).relative_to(project).as_posix()
            lines = ['[gd_resource type="SpriteFrames" format=3]', '',
                f'[ext_resource type="Texture2D" path="{atlas_path}" id="atlas"]', '']
            cells = sorted({i for values in layer['poses'].values() for i in values})
            width, height = layer['cell_size']
            for cell in cells:
                lines += [f'[sub_resource type="AtlasTexture" id="pose_{cell}"]',
                    'atlas = ExtResource("atlas")',
                    f'region = Rect2({cell % layer["columns"] * width}, {cell // layer["columns"] * height}, {width}, {height})', '']
            animations = []
            for action, sequence in layer['poses'].items():
                frames = ', '.join('{"duration": 1.0, "texture": SubResource("pose_' + str(i) + '")}' for i in sequence)
                animations.append('{"frames": [' + frames + '], "loop": ' + str(action in ('idle', 'run')).lower() +
                    ', "name": &"' + action + '", "speed": ' + str(packed['fps']) + '}')
            lines += ['[resource]', 'animations = [' + ',\n'.join(animations) + ']', '']
            output.write_text('\n'.join(lines), encoding='utf-8', newline='\r\n')


def export(args):
    """Verify source, export full actions, and retain native frame metadata."""
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != SOURCE_HASH:
        raise ValueError('Unexpected Monster_v1.swf revision')
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'profile').mkdir(exist_ok=True)
    env = dict(os.environ, APPDATA=str(work / 'profile'))
    source = work / 'monsters.swf'
    source.write_bytes(content)
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec)]

    def run(*arguments):
        with (work / 'export.log').open('a', encoding='utf-8') as log:
            subprocess.run(command + list(arguments), env=env, stdout=log, stderr=subprocess.STDOUT, check=True)

    xml = work / 'monsters.xml'
    run('-swf2xml', str(source), str(xml))
    root = E.parse(xml).getroot()
    tags = root.find('tags')
    sprites = {int(t.get('spriteId')): t for t in tags if t.get('type') == 'DefineSpriteTag'}
    symbols = {}
    for tag in tags:
        if tag.get('type') == 'SymbolClassTag':
            symbols.update(zip((x.text for x in tag.find('names')), (int(x.text) for x in tag.find('tags'))))
    manifest = {'source_sha256': SOURCE_HASH, 'fps': float(root.get('frameRate')), 'actors': []}
    body_ids = set()
    for name, symbol in ACTORS.items():
        actor = {'name': name, 'symbol': symbol, 'actions': [], 'layers': []}
        label = None
        for tag in sprites[symbols[symbol]].find('subTags'):
            if tag.get('type') == 'FrameLabelTag':
                label = tag.get('name')
            elif tag.get('name') == 'body' and tag.get('characterId'):
                sid = int(tag.get('characterId'))
                body_ids.add(sid)
                windows = []
                for display in timeline_frames(sprites[sid]):
                    windows.append([{'sprite': int(t.get('characterId')), 'matrix': matrix(t)}
                        for t in display.values() if t.get('name') == 'stick'])
                actor['actions'].append({'name': LABELS[label], 'sprite': sid, 'matrix': matrix(tag),
                    'frames': int(sprites[sid].get('frameCount')), 'hit_windows': windows})
        manifest['actors'].append(actor)
    bounds_reader = Path(__file__).with_name('FlashSpriteBounds.java')
    # The compiler needs FFDec classes, not its optional console-library manifest classpath.
    compiler_jar = work / 'bounds-classpath.jar'
    with ZipFile(args.ffdec) as original, ZipFile(compiler_jar, 'w') as destination:
        for member in original.infolist():
            if member.filename.upper() != 'META-INF/MANIFEST.MF':
                destination.writestr(member, original.read(member.filename))
    subprocess.run([str(args.java.with_name('javac.exe')), '-proc:none', '-cp', str(compiler_jar), '-d', str(work),
        str(bounds_reader)], env=env, check=True)
    # Native bitmap masks require FFDec's renderer; separate large clouds/tornadoes from body cells.
    for layer in ('body', 'effect'):
        selected = body_ids if layer == 'body' else {a['sprite'] for actor in manifest['actors'] for a in actor['actions']
            if any(any(t['sprite'] != 26 for t in frame) for frame in a['hit_windows'])}
        variant = deepcopy(root)
        for sprite in variant.find('tags'):
            if sprite.get('type') == 'DefineSpriteTag' and int(sprite.get('spriteId')) in selected:
                filter_action(sprite, layer)
        variant_xml, variant_swf = work / f'{layer}.xml', work / f'{layer}.swf'
        E.ElementTree(variant).write(variant_xml, encoding='utf-8', xml_declaration=True)
        run('-xml2swf', str(variant_xml), str(variant_swf))
        out = work / f'png_{layer}'
        run('-format', 'sprite:png', '-selectid', ','.join(map(str, sorted(selected))),
            '-ignorebackground', '-export', 'sprite', str(out), str(variant_swf))
        folders = {int(p.name.split('_')[1]): p for p in out.iterdir() if p.is_dir()}
        bounds = subprocess.check_output([str(args.java), '-cp', str(work) + os.pathsep + str(args.ffdec),
            'FlashSpriteBounds', str(variant_swf), *map(str, sorted(selected))], env=env, text=True)
        rectangles = {int(line.split()[0]): [int(n) / 20 for n in line.split()[1:]] for line in bounds.splitlines()}
        for actor in manifest['actors']:
            record = {'name': layer, 'actions': [],
                'output': str((args.output / FOLDERS[actor['name']] / f'{actor["name"]}_{layer}.png').resolve())}
            for action in actor['actions']:
                if action['sprite'] not in selected:
                    continue
                if action['matrix'][:4] != [1, 0, 0, 1]:
                    raise ValueError('Nontranslation boss registration needs an affine renderer')
                record['actions'].append({**action, 'input': str(folders[action['sprite']]),
                    'bounds': rectangles[action['sprite']]})
            actor['layers'].append(record)
    manifest_path = work / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.godot:
        project = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
        subprocess.run([str(args.godot), '--headless', '--path', str(project), '--script',
            'res://EditorScripts/import_official_bosses.gd', '--', str(manifest_path)], env=env, check=True)
        packed = json.loads((work / 'rasterized.json').read_text(encoding='utf-8'))
        write_sprite_frames(packed, project)
    print('Exported full original actions:', [(a['name'], sum(x['frames'] for x in a['actions'])) for a in manifest['actors']])


def export_runtime(args):
    """Reuse verified rasterization and export the original independent wind effect."""
    work = args.work.resolve()
    source = work / 'monsters.swf'
    if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_HASH:
        raise ValueError('Unexpected Monster source revision')
    manifest = json.loads((work / 'rasterized.json').read_text(encoding='utf-8'))
    tags = E.parse(work / 'monsters.xml').getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('spriteId')}
    shapes = {int(tag.get('shapeId')): tag for tag in tags if tag.get('shapeId')}

    def bounds(shape, transform):
        box = shapes[shape].find('shapeBounds')
        points = []
        a,b,c,d,tx,ty = transform
        for x,y in [('Xmin','Ymin'),('Xmax','Ymin'),('Xmax','Ymax'),('Xmin','Ymax')]:
            px,py = int(box.get(x))/20,int(box.get(y))/20
            points.append((a*px+c*py+tx,b*px+d*py+ty))
        xs,ys=zip(*points)
        return [min(xs),min(ys),max(xs)-min(xs),max(ys)-min(ys)]

    for actor in manifest['actors']:
        root_id = 534 if actor['name']=='macaque_king' else 461
        collider = next(t for t in next(timeline_frames(sprites[root_id])).values() if t.get('name')=='colipse')
        actor['hurt_bounds'] = bounds(21,matrix(collider))
        for action in actor['actions']:
            action['probe_bounds'] = []
            for window in action['hit_windows']:
                raw = next((entry for entry in window if int(entry['sprite'])==26),None)
                if raw:
                    transform = list(raw['matrix'])
                    transform[4] += action['matrix'][4]
                    transform[5] += action['matrix'][5]
                    action['probe_bounds'].append(bounds(25,transform))
                else:
                    action['probe_bounds'].append(None)
    environment = dict(os.environ, APPDATA=str(work/'profile'))
    output = work/'wind_png'
    subprocess.run([str(args.java),f'-Duser.home={work}','-jar',str(args.ffdec),'-format','sprite:png',
        '-selectid','15','-ignorebackground','-export','sprite',str(output),str(source)],env=environment,check=True)
    raw = subprocess.check_output([str(args.java),'-cp',str(work)+os.pathsep+str(args.ffdec),
        'FlashSpriteBounds',str(source),'15'],env=environment,text=True).split()
    manifest['wind'] = {'input':str(next(output.iterdir())), 'bounds':[int(value)/20 for value in raw[1:]],
        'frames':50, 'renew_frames':[39]}
    (work/'runtime.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Native boss animation data and wind frames prepared.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'java', 'ffdec', 'work', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    parser.add_argument('--godot', type=Path)
    parser.add_argument('--runtime', action='store_true')
    arguments=parser.parse_args()
    export_runtime(arguments) if arguments.runtime else export(arguments)
