"""Audit every current Wukong weapon/armor against its official selector.

Reads source workbooks and the native appearance resource. The output is test
input only: it does not edit gameplay data, atlases, or resource bindings.
"""
import argparse
import json
from pathlib import Path
import re

from audit_official_level_dependencies import xlsx_rows

SOURCE_SHA = '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7'


def audit(args):
    """Fail when a playable item has no matching independently exported layer."""
    repo = args.repo.resolve()
    project = repo / 'Godot/GodotProject'
    config = repo / 'Configs/GameConfig/Datas'
    text = (project / 'TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres').read_text(encoding='utf-8')
    resources = {identifier: path for path, identifier in re.findall(
        r'\[ext_resource[^\n]*path="([^"]+)" id="([^"]+)"', text)}
    bindings = {}
    for kind, field in [('body', 'm_ArmorTextures'), ('weapon', 'm_WeaponTextures')]:
        values = re.search(field + r' = Dictionary\[int, Texture2D\]\(\{(.*?)\}\)', text, re.S)
        if values is None:
            raise ValueError(f'Missing native wardrobe dictionary: {field}')
        bindings[kind] = {int(key): resources[value] for key, value in re.findall(
            r'(\d+): ExtResource\("([^"]+)"\)', values[1])}
        field = 'm_BaseBody' if kind == 'body' else 'm_BaseWeapon'
        base = re.search(field + r' = ExtResource\("([^"]+)"\)', text)
        bindings[kind][0] = resources[base[1]]

    manifests = {}
    for path in args.manifest:
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        if data['source_sha256'] != SOURCE_SHA:
            raise ValueError(f'Unexpected Role revision: {path}')
        atlas = Path(data['output'])
        name = atlas.stem.removeprefix('wukong_')
        if name in manifests:
            raise ValueError(f'Duplicate exported appearance: {name}')
        manifests[name] = {'name': name, 'manifest': str(path.resolve()),
                           'kind': data['kind'], 'selector': int(data['selector'])}

    # Read the stable numeric identity from its source enum, not a copied ID list.
    identities = {}
    active = None
    for row in xlsx_rows(config / '__enums__.xlsx')['Sheet1']:
        cells = {re.sub(r'\d+$', '', key): value for key, value in row.items()}
        active = cells.get('B') or active
        if active == 'Equipment.EEquipmentId' and cells.get('H') and cells.get('J'):
            for name in (cells['H'], cells.get('I')):
                if name:
                    identities[name] = int(cells['J'])
    official = {legacy: int(selector) for selector, legacy in re.findall(
        r'new MyEquipObj\((\d+),"[^"]*","([^"]+)"', args.equipment_source.read_text(encoding='utf-8-sig'))}
    equipment = []
    unavailable = []
    for row in xlsx_rows(config / 'Items.xlsx')['EquipmentConfig']:
        cells = {re.sub(r'\d+$', '', key): value for key, value in row.items()}
        if not cells.get('C') or cells.get('A') or cells.get('G') not in ('武器', '防具'):
            continue
        identity = identities[cells['B']]
        if cells.get('F') != '悟空':
            unavailable.append({'id': identity, 'name': cells['D'], 'owner': cells['F'],
                                'reason': 'Owner is not a playable character yet; no Wukong layer is required.'})
            continue
        kind = 'body' if cells['G'] == '防具' else 'weapon'
        if identity not in bindings[kind]:
            raise ValueError(f'Playable equipment has no native {kind} binding: {cells["D"]}')
        if cells['C'] not in official:
            raise ValueError(f'Missing official equipment selector: {cells["C"]}')
        path = bindings[kind][identity]
        variant = manifests[Path(path).stem.removeprefix('wukong_')]
        if variant['kind'] != kind or variant['selector'] != official[cells['C']]:
            raise ValueError(f'{cells["D"]} uses another equipment selector: {variant}')
        equipment.append({'id': identity, 'name': cells['D'], 'legacy_id': cells['C'],
                          'kind': kind, 'selector': official[cells['C']], 'texture': path})
    # Default layers participate in every naked/unequipped combination as well.
    for kind in bindings:
        for identity, path in bindings[kind].items():
            name = Path(path).stem.removeprefix('wukong_')
            if name not in manifests or not (project / path.removeprefix('res://')).is_file():
                raise ValueError(f'Missing official source or native texture: {path}')
            if identity == 0:
                if manifests[name]['selector'] != 1 or manifests[name]['kind'] != kind:
                    raise ValueError(f'Wrong default {kind} selector')
            elif not any(item['id'] == identity and item['kind'] == kind for item in equipment):
                raise ValueError(f'Wardrobe binding has no matching playable equipment: {identity}')
    result = {'source_sha256': SOURCE_SHA, 'variants': list(manifests.values()),
              'equipment': equipment, 'not_playable_yet': unavailable}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'WARDROBE IDENTITY PASS: {len(equipment)} playable equipment bindings, '
          f'{len(manifests)} independent atlases; {len(unavailable)} non-playable owner items reported separately.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--manifest', type=Path, action='append', required=True)
    parser.add_argument('--equipment-source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    audit(parser.parse_args())
