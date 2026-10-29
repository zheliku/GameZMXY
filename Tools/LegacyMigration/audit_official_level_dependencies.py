"""Inventory ZMXY1 level dependencies from verified FFDec exports and source XLSX.

This is an authoring audit, not runtime configuration. It reads the original
display lists and scripts, follows spawned enemies, and reports source evidence
alongside missing project registrations. No game resource or workbook is edited.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as E
from zipfile import ZipFile

from export_official_wukong import matrix, timeline_frames
from extract_official_levels import extract, labeled_frames


NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main',
      'r': 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
SOURCE_HASHES = {
    'main': 'f8e3e1f5b545bc2423a02f5c6155f87215e01c14d21554d77681f32a7defb75b',
    'monster1': '5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a',
    'monster2': '42a301a5ae4222aec54dfdc4c14efced48b9ad4f979902471011ff226d8cd09b',
    'monster3': '5f6478c13cb57a46d46abac6fc70549f517358d727c3f2641aa75754c14f33c7',
    'music': '5cad2f1ef3ce592c2e9e44c631748e042d8cacae2c7571e57b1a0546998a7578',
}


def xlsx_rows(path):
    """Read current source values without reserializing ZIP or XML namespaces."""
    with ZipFile(path) as source:
        strings = []
        if 'xl/sharedStrings.xml' in source.namelist():
            strings = [''.join(x.itertext()) for x in
                       E.fromstring(source.read('xl/sharedStrings.xml')).findall('s:si', NS)]
        rels = {x.get('Id'): x.get('Target') for x in
                E.fromstring(source.read('xl/_rels/workbook.xml.rels'))}
        sheets = {}
        for sheet in E.fromstring(source.read('xl/workbook.xml')).findall('s:sheets/s:sheet', NS):
            target = rels[sheet.get('{%s}id' % NS['r'])]
            target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            rows = []
            for row in E.fromstring(source.read(target)).findall('s:sheetData/s:row', NS):
                cells = {}
                for cell in row:
                    value = cell.find('s:v', NS)
                    kind = cell.get('t')
                    if kind == 'inlineStr':
                        value = ''.join(cell.find('s:is', NS).itertext())
                    elif value is not None:
                        value = strings[int(value.text)] if kind == 's' else value.text
                    else:
                        value = None
                    cells[cell.get('r')] = value
                rows.append(cells)
            sheets[sheet.get('name')] = rows
        return sheets


def spawn_recipe(spawn):
    """Keep each official stream's identity and first/later timing for authoring audits."""
    match = re.fullmatch(r'Stage(\d+)([A-Z]+)', spawn['projectNode'])
    if match is None:
        raise ValueError(f"Unrecognized project spawn point: {spawn['projectNode']}")
    values = spawn['properties']
    return {'spawn_point_id': f'stage_{match[1]}_{match[2].lower()}',
            'stage': int(values['stopPointIdx']) + 1,
            'enemy_legacy_id': int(values['enemyType']), 'count': int(values['totalNum']),
            'first_spawn_seconds': float(spawn['firstSpawnSeconds']),
            'interval_seconds': float(values['interval'])}


def source_pack(name, swf, xml, scripts):
    """Verify the exact pack revision before inspecting extracted metadata."""
    digest = hashlib.sha256(swf.read_bytes()).hexdigest()
    if digest != SOURCE_HASHES[name]:
        raise ValueError(f'Unexpected {name} revision: {digest}')
    root = E.parse(xml).getroot()
    tags = root.find('tags')
    symbols = {}
    for item in tags:
        if item.get('type') == 'SymbolClassTag':
            symbols.update(zip((x.text for x in item.find('names')),
                               (int(x.text) for x in item.find('tags'))))
    return {'name': name, 'swf': swf, 'xml': xml, 'scripts': scripts,
            'sha256': digest, 'symbols': symbols, 'root': root,
            'sprites': {int(t.get('spriteId')): t for t in tags if t.get('spriteId')}}


def evidence_path(path, repo):
    """Keep evidence locations readable and portable within the workspace."""
    return path.resolve().relative_to(repo).as_posix()


def find_script(symbol, packs):
    """Main definitions load first, then resource packs in Aloader's order."""
    for pack in packs:
        if pack['scripts'] is None:
            continue
        path = pack['scripts'] / (symbol.replace('.', '/') + '.as')
        if path.is_file():
            return path, path.read_text(encoding='utf-8-sig')
    raise ValueError(f'Missing source script: {symbol}')


def dynamic_dependencies(code, packs):
    """Resolve direct constructors as well as string-created visual objects.

    Only classes with an actual asset linkage are followed. Importing a utility
    or constructing a Point must not pull the whole framework into each actor.
    """
    symbols = {name for pack in packs for name in pack['symbols']}
    objects = set(re.findall(
        r'(?:getNewObj|SpecialEffectBullet|EnemyMoveBullet)\("([^"]+)"', code))
    imports = re.findall(r'import\s+([\w.*]+)\s*;', code)
    package = re.search(r'package\s+([\w.]+)', code)
    for constructor in re.findall(r'\bnew\s+([\w.]+)\s*\(', code):
        candidates = {constructor}
        if package:
            candidates.add(package[1] + '.' + constructor)
        for imported in imports:
            if imported.endswith('.*'):
                candidates.add(imported[:-1] + constructor)
            elif imported.rsplit('.', 1)[-1] == constructor:
                candidates.add(imported)
        matches = candidates & symbols
        if len(matches) > 1:
            raise ValueError(f'Ambiguous linked constructor: {constructor}: {sorted(matches)}')
        objects.update(matches)
    return objects


def direct_equipment_rewards(code):
    """Find direct inventory awards that do not appear in a fallList.

    Keep this a source dependency, not a runtime drop probability: the caller
    must inspect ownership checks, bag capacity and the award's execution point.
    """
    rewards = set(re.findall(r'findByName\("([^"]+)"\)', code))
    for variable in re.findall(r'findByName\(([A-Za-z_][\w]*)\)', code):
        rewards.update(re.findall(r'\b' + re.escape(variable) + r'\s*=\s*"([^"]+)"', code))
    return rewards


def inventory_actor(number, packs, repo):
    """Retain every label/body action and explicit dynamic dependency for review."""
    symbol = f'export.monster.Monster{number}'
    script, code = find_script(symbol, packs)
    actor_code = code
    pack = next((p for p in packs if symbol in p['symbols']), None)
    if pack is None:
        raise ValueError(f'Missing visual linkage: {symbol}')
    sprite = pack['sprites'][pack['symbols'][symbol]]
    actions = []
    for label, display in labeled_frames(sprite).items():
        candidates = [tag for tag in display.values() if tag.get('name') == 'body']
        if not candidates:
            # Monster14's moving/attacking children are unnamed at depth 1.
            # Record the native layer instead of silently dropping four actions.
            candidates = [tag for tag in display.values() if tag.get('depth') == '1']
        for tag in candidates:
            sid = int(tag.get('characterId'))
            actions.append({'label': label, 'sprite': sid,
                            'frames': int(pack['sprites'][sid].get('frameCount')),
                            'matrix': matrix(tag), 'named_body': tag.get('name') == 'body'})
    # Resolve move-only placements and reused child timelines before collecting
    # frame scripts/audio; inspecting only explicit PlaceObject IDs loses actions.
    visited = set()
    pending = [pack['symbols'][symbol]]
    while pending:
        sid = pending.pop()
        if sid in visited or sid not in pack['sprites']:
            continue
        visited.add(sid)
        pending.extend(int(t.get('characterId')) for t in pack['sprites'][sid].find('subTags')
                       if t.get('characterId'))
    timeline_scripts = []
    linked_scripts = []
    embedded_sounds = set()
    for sid in sorted(visited):
        for item in pack['sprites'][sid].find('subTags'):
            if item.get('type', '').startswith('StartSound'):
                embedded_sounds.add(item.get('soundId') or item.get('soundClassName'))
        for name, identifier in pack['symbols'].items():
            if identifier != sid:
                continue
            try:
                path, text = find_script(name, packs)
            except ValueError:
                continue
            linked_scripts.append(evidence_path(path, repo))
            code += '\n' + text
            if re.search(r'function frame\d+\(', text):
                timeline_scripts.append({'sprite': sid, 'symbol': name,
                                         'path': evidence_path(path, repo)})
    # A directly constructed Fire is not a child of the actor's display list.
    # Follow linked dynamic classes with the same main-before-asset precedence.
    dynamic_objects = set()
    dependency_scripts = []
    pending_code = [code]
    seen_scripts = {script.resolve(), *(repo / path for path in linked_scripts)}
    while pending_code:
        for name in sorted(dynamic_dependencies(pending_code.pop(), packs)):
            dynamic_objects.add(name)
            try:
                path, text = find_script(name, packs)
            except ValueError:
                continue
            if path.resolve() in seen_scripts:
                continue
            seen_scripts.add(path.resolve())
            dependency_scripts.append(evidence_path(path, repo))
            code += '\n' + text
            pending_code.append(text)
    # These remain source expressions: conditional HP, random values, and unit
    # conversions must be reviewed in context before becoming planner data.
    fields = {}
    for field in ('monsterName', 'horizenSpeed', 'Hp', 'def', 'exp', 'gxp',
                  'isBoss', 'isFly', 'graity', 'probability'):
        fields[field] = re.findall(r'this\.' + field + r'\s*=\s*([^;]+);', actor_code)
    spawned = sorted({int(x) for x in re.findall(r'createMonster\((\d+)', code)})
    rewards = direct_equipment_rewards(code)
    drops = sorted(set(re.findall(r'"name"\s*:\s*"([^"]+)"\s*,\s*"bigtype"\s*:\s*"zb"', code)) | rewards)
    sounds = sorted(set(re.findall(r'SoundManager\.play\("([^"]+)"', code)))
    return {'id': number, 'symbol': symbol, 'source_pack': pack['name'],
            'source_script': evidence_path(script, repo), 'fields': fields,
            'actions': actions, 'spawned_enemies': spawned,
            'dynamic_objects': sorted(dynamic_objects), 'equipment_drops': drops,
            'direct_equipment_rewards': sorted(rewards),
            'dependency_scripts': sorted(dependency_scripts),
            'script_sounds': sounds, 'embedded_sounds': sorted(x for x in embedded_sounds if x),
            'timeline_scripts': timeline_scripts, 'linked_scripts': sorted(set(linked_scripts))}


def audit(args):
    """Create a complete source matrix and a current project registration snapshot."""
    repo = args.repo.resolve()
    base = repo / 'Godot/GodotProject/.godot/validation'
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    packs = [
        source_pack('main', base/'stages_2_3/main_3.swf', base/'stages_2_3/main.xml', work/'main_scripts/scripts'),
        source_pack('monster1', base/'stages_2_3/bosses/monsters.swf', base/'stages_2_3/bosses/monsters.xml', work/'monster1_scripts/scripts'),
        source_pack('monster2', base/'official_continuation/Monster2_v4.swf', base/'official_continuation/monster2_full.xml', work/'monster2_scripts/scripts'),
        source_pack('monster3', work/'Monster3_v3.swf', work/'monster3.xml', work/'monster3_scripts/scripts'),
        source_pack('music', base/'skills-source/official_music_readable.swf', base/'skills-source/music.xml', None),
    ]
    main = packs[0]
    labels = [t.get('name') for t in main['sprites'][main['symbols']['export.GameSence']].find('subTags')
              if t.get('type') == 'FrameLabelTag' and t.get('name') != 'vs']
    # Re-run the native extractor; do not trust an old, manually trimmed manifest.
    extracted = work/'all_levels.json'
    extract(argparse.Namespace(xml=main['xml'], script=main['scripts']/'export/GameSence.as',
                               levels=labels, output=extracted))
    levels = json.loads(extracted.read_text(encoding='utf-8'))
    actor_ids = {s['properties']['enemyType'] for level in levels.values() for s in level['spawns']}
    actors = {}
    pending = sorted(actor_ids)
    while pending:
        number = pending.pop(0)
        if number in actors:
            continue
        actor = inventory_actor(number, packs, repo)
        actors[number] = actor
        pending.extend(n for n in actor['spawned_enemies'] if n not in actors)
    floor_frames = list(timeline_frames(main['sprites'][main['symbols']['export.FloorBg']]))
    records = []
    for label, level in levels.items():
        by_type = Counter()
        for spawn in level['spawns']:
            by_type[spawn['properties']['enemyType']] += spawn['properties']['totalNum']
        chapter = int(label.split('-')[0])
        floor = floor_frames[2 if chapter == 9 else chapter - 1]
        stages = []
        for gate in sorted(level['gates'], key=lambda x: x['properties']['idx']):
            index = gate['properties']['idx']
            recipes = [x for x in level['spawns'] if x['properties']['stopPointIdx'] == index]
            stages.append({'index': index + 1, 'bounds': gate['battleBounds'],
                           'recipes': len(recipes),
                           'configured_count': sum(x['properties']['totalNum'] for x in recipes)})
        records.append({'label': label, 'source_frame': level['sourceFrame'],
                        'spawn_streams': len(level['spawns']), 'stages': stages,
                        'spawn_recipes': [spawn_recipe(spawn) for spawn in level['spawns']],
                        'enemy_counts': dict(sorted(by_type.items())),
                        'geometry': len(level['geometry']),
                        'scene_characters': dict(sorted(Counter(x['characterId'] for x in level['scenery']).items())),
                        'front_sprite': level['backgroundCharacter'],
                        'front_position': level['backgroundPosition'],
                        'floor_characters': [int(x.get('characterId')) for x in floor.values()],
                        'music_symbol': f'bg{min(chapter, 4)}'})
    config = repo/'Configs/GameConfig/Datas'
    current = {name: xlsx_rows(config/(name+'.xlsx'))
               for name in ('LevelConfig', 'Enemy', 'Combat', 'Resources', 'Items', '__enums__')}
    result = {
        'sources': [{k: evidence_path(p[k], repo) if isinstance(p[k], Path) else p[k]
                     for k in ('name', 'swf', 'xml', 'sha256')} for p in packs],
        'levels': records, 'actors': [actors[n] for n in sorted(actors)],
        'music_linkages': {name: packs[-1]['symbols'][name] for name in ('bg1', 'bg2', 'bg3', 'bg4')},
        'current_source_workbooks': current,
        'notes': [
            'Configured counts include the original 999 bat streams; they are not mandatory kill counts.',
            '9-1 is the hidden branch entered from 1-2. vs is excluded as a separate multiplayer mode.',
            '4-2 and 4-3 map controls have no matching scene/background label.',
            'Actor fields are unevaluated source expressions. Do not collapse conditional variants.',
            'Visual actions alone do not prove behavior: frame scripts, collisions, sounds, AI and lifecycle need separate implementation and validation.',
        ],
    }
    output = work/'dependency_inventory.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    for level in records:
        print(f"{level['label']}: {level['spawn_streams']} streams, {len(level['stages'])} stages, "
              f"enemies={level['enemy_counts']}, front={level['front_sprite']}, music={level['music_symbol']}")
    print(f'DEPENDENCY INVENTORY: {len(records)} levels, {len(actors)} linked enemy types; {output}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--work', type=Path, required=True,
                        help='Staging directory containing fresh main/monster script exports.')
    audit(parser.parse_args())
