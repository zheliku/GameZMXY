"""Audit ZMXY1 Role2 identities, source frames, transforms and frame scripts.

Reads the restored official Role_v7 SWF and its FFDec XML/AS exports. It does
not write gameplay assets or configuration. The report is an input to the
Tangseng exporter and to the same wardrobe/action gates already used by Wukong.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

from export_official_wukong import matrix, timeline_frames
from audit_official_level_dependencies import xlsx_rows

ROLE_HASH = '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7'
IDENTITY = [1, 0, 0, 1, 0, 0]


def compose(parent, child):
    """Compose Flash matrices in source pixel coordinates."""
    a, b, c, d, x, y = parent
    e, f, g, h, u, v = child
    return [a*e+c*f, b*e+d*f, a*g+c*h, b*g+d*h, a*u+c*v+x, b*u+d*v+y]


def frame_scripts(text):
    """Keep each exported frame callback verbatim, keyed by 1-based frame."""
    matches = list(re.finditer(r'internal function frame(\d+)\(\)', text))
    return {int(match.group(1)): text[match.start():matches[i+1].start() if i+1 < len(matches) else len(text)].strip()
            for i, match in enumerate(matches)}


def audit(role, xml, scripts, repo=None, equipment_source=None):
    """Build an authoritative source inventory before authoring scenes."""
    digest = hashlib.sha256(role.read_bytes()).hexdigest()
    if digest != ROLE_HASH:
        raise ValueError('Expected the restored official Role_v7 SWF')
    tags = ET.parse(xml).getroot().find('tags')
    sprites = {int(tag.get('spriteId')): tag for tag in tags if tag.get('spriteId')}
    shapes = {int(tag.get('shapeId')): tag for tag in tags if tag.get('shapeId')}
    symbols = {name.text: int(sid.text) for tag in tags if tag.get('type') == 'SymbolClassTag'
               for name, sid in zip(tag.find('names'), tag.find('tags'))}
    script_map = {}
    for path in scripts.rglob('*.as'):
        text = path.read_text(encoding='utf-8-sig')
        match = re.search(r'symbol="symbol(\d+)"', text)
        if match:
            script_map[int(match.group(1))] = {'source': str(path), 'text': text}

    def bounds(sid, transform=IDENTITY, ancestry=()):
        if sid in ancestry:
            raise ValueError(f'Recursive source symbol {sid}')
        if sid in shapes:
            box = shapes[sid].find('shapeBounds')
            a, b, c, d, tx, ty = transform
            return [(a*x+c*y+tx, b*x+d*y+ty)
                    for x in (float(box.get('Xmin'))/20, float(box.get('Xmax'))/20)
                    for y in (float(box.get('Ymin'))/20, float(box.get('Ymax'))/20)]
        if sid not in sprites:
            return []
        return [point for child in next(timeline_frames(sprites[sid])).values()
                if child.get('characterId') for point in bounds(int(child.get('characterId')),
                    compose(transform, matrix(child)), ancestry+(sid,))]

    def rectangle(points):
        if not points:
            return None
        xs, ys = zip(*points)
        return [min(xs), min(ys), max(xs)-min(xs), max(ys)-min(ys)]

    actors = {}
    reachable = set()
    for name in ['export.hero.Role2', 'export.hero.Role2Shadow']:
        sid = symbols[name]
        actor = sprites[sid]
        first = next(timeline_frames(actor))
        col = next(node for node in first.values() if node.get('name') == 'colipse')
        actions, label = [], None
        for tag in actor.find('subTags'):
            if tag.get('type') == 'FrameLabelTag':
                label = tag.get('name')
            elif tag.get('name') == 'body' and tag.get('characterId'):
                action_id = int(tag.get('characterId'))
                action = sprites[action_id]
                actions.append({'label':label, 'sprite':action_id, 'frames':int(action.get('frameCount')),
                    'matrix':matrix(tag), 'frame_scripts':frame_scripts(script_map.get(action_id, {}).get('text',''))})
        actors[name] = {'sprite':sid, 'hurt_bounds':rectangle(bounds(int(col.get('characterId')), matrix(col))),
                        'actions':actions}
        pending = [sid]
        while pending:
            current = pending.pop()
            if current in reachable or current not in sprites:
                continue
            reachable.add(current)
            pending.extend(int(node.get('characterId')) for node in sprites[current].find('subTags')
                           if node.get('characterId'))

    selectors = []
    for sid in sorted(reachable):
        source = script_map.get(sid)
        if source and re.search(r'gotoAndPlay\([^;]*\.curWeaponId', source['text']):
            selectors.append({'kind':'weapon', 'sprite':sid, 'frames':int(sprites[sid].get('frameCount')),
                              'source':source['source']})
        if source and re.search(r'gotoAndPlay\([^;]*\.curClothId', source['text']):
            threshold = re.search(r'curClothId > (\d+)', source['text'])
            offset = re.search(r'curClothId - (\d+)', source['text'])
            selectors.append({'kind':'armor_component', 'sprite':sid,
                'frames':int(sprites[sid].get('frameCount')), 'source':source['source'],
                'minimum_selector':int(threshold[1])+1 if threshold else 1,
                'frame_offset':-int(offset[1]) if offset else 0})
        for node in sprites[sid].find('subTags'):
            if node.get('name') == 'bodyEquip' and node.get('characterId'):
                selector = int(node.get('characterId'))
                entry = {'kind':'armor', 'sprite':selector, 'frames':int(sprites[selector].get('frameCount')),
                         'maximum_selector':3}
                if entry not in selectors:
                    selectors.append(entry)

    effects = {}
    for name in ['Role2Bullet1','Role2Bullet2','Role2Bullet3','Role2Bullet4','hit4FallDown','cureRoleMc']:
        sid = symbols[name]
        effects[name] = {'sprite':sid, 'frames':int(sprites[sid].get('frameCount')),
                         'frame_scripts':frame_scripts(script_map.get(sid, {}).get('text','')),
                         'first_bounds':rectangle(bounds(sid))}
    nested_scripts = {sid:{'source':script_map[sid]['source'], 'frames':int(sprites[sid].get('frameCount')),
                          'frame_scripts':frame_scripts(script_map[sid]['text'])}
                      for sid in sorted(reachable | {656,702}) if sid in script_map and frame_scripts(script_map[sid]['text'])}
    equipment = []
    if equipment_source is not None:
        official = {legacy:{'selector':int(selector),'name':name,'kind':'body' if kind=='zbfj' else 'weapon'}
            for selector,name,legacy,kind in re.findall(
                r'new MyEquipObj\((\d+),"([^"]*)","([^"]*)","(zbfj|zbwq)","法师"',
                equipment_source.read_text(encoding='utf-8-sig'))}
        for row in xlsx_rows(repo/'Configs/GameConfig/Datas/Items.xlsx')['EquipmentConfig']:
            cells = {re.sub(r'\d+$','',key):value for key,value in row.items()}
            if cells.get('A') or cells.get('F') not in ('唐僧','Tangseng') or cells.get('G') not in ('武器','防具','Weapon','Armor'):
                continue
            legacy = cells['C']
            if legacy not in official:
                raise ValueError(f'Missing official selector for Tangseng equipment {legacy}')
            entry = official[legacy]
            if entry['name'] != cells['D']:
                raise ValueError(f'Official equipment identity/name mismatch: {legacy}')
            equipment.append({'equipment_id':cells['B'], 'legacy_id':legacy, **entry})
    return {'role_sha256':digest, 'xml_sha256':hashlib.sha256(xml.read_bytes()).hexdigest(),
            'actors':actors, 'selectors':selectors, 'effects':effects, 'nested_scripts':nested_scripts,
            'equipment':equipment}


def main():
    """Write the evidence manifest and print only a compact identity summary."""
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['role','xml','scripts','output']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--equipment-source', type=Path)
    args = parser.parse_args()
    report = audit(args.role, args.xml, args.scripts, args.repo, args.equipment_source)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    for name, actor in report['actors'].items():
        print(name, actor['sprite'], 'hurt', actor['hurt_bounds'],
              [(action['label'], action['sprite'], action['frames'], list(action['frame_scripts'])) for action in actor['actions']])
    print('selectors', report['selectors'])
    print('effects', {name:(item['sprite'],item['frames']) for name,item in report['effects'].items()})
    print('equipment', [(entry['legacy_id'],entry['kind'],entry['selector']) for entry in report['equipment']])
    print('TANGSENG-SOURCE-AUDIT PASS:', args.output)


if __name__ == '__main__':
    main()
