"""Verify the complete Role2 wardrobe export before installing native resources.

Joins current equipment identities to the audited official selectors, checks all
source action cells and image bounds, and records the exact reviewed PNG hashes.
This export check does not certify native scene playback or real attack coverage.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

from audit_equipment_appearances import _equipment_rows
from audit_official_tangseng import ROLE_HASH


ACTION_ORDER = ('wait', 'walk', 'run', 'jump1', 'jump2', 'jump3', 'hurt',
                'hit1', 'hit2', 'hit3', 'hit4', 'hit5')


def audit(args):
    """Validate every distinct selector and every configured Tang wearable."""
    source = json.loads(args.audit.read_text(encoding='utf-8'))
    if source['role_sha256'] != ROLE_HASH:
        raise ValueError('Unexpected official Role_v7 source')
    actions = {item['label']: item for item in source['actors']['export.hero.Role2']['actions']}
    expected = [(label, actions[label]['sprite'], frame)
                for label in ACTION_ORDER for frame in range(1, actions[label]['frames'] + 1)]
    selectors = {kind: {1} | {item['selector'] for item in source['equipment'] if item['kind'] == kind}
                 for kind in ('body', 'weapon')}
    variants = []
    images = {}
    for kind, values in selectors.items():
        for selector in sorted(values):
            folder = args.atlases / f'{kind}_{selector}'
            manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8'))
            if (manifest['source_sha256'], manifest['xml_sha256']) != (ROLE_HASH, source['xml_sha256']):
                raise ValueError(f'Source identity mismatch: {folder}')
            actual = [(item['action'], item['source_sprite'], item['source_frame'])
                      for item in manifest['frames']]
            if actual != expected:
                raise ValueError(f'Incomplete or reordered native frames: {folder}')
            selected = manifest['cloth_selector'] if kind == 'body' else manifest['weapon_selector']
            if manifest['kind'] != kind or selected != selector:
                raise ValueError(f'Wrong official selector: {folder}')
            if (manifest['source_origin'], manifest['frame_cell'], manifest['columns']) != ([100, 128], [200, 200], 6):
                raise ValueError(f'Inconsistent registration: {folder}')
            if manifest.get('separate_effect_ids') != ['951']:
                raise ValueError(f'WaterBurst roundMc wrapper still mixed into body art: {folder}')
            bakes = manifest.get('cloth_frame_bakes', [])
            if manifest['cloth_selector'] in (2, 3):
                sprite = 867 if manifest['cloth_selector'] == 2 else 870
                required = [
                    {'action': 'hit1', 'source_sprite': sprite,
                     'source_frames': [1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1]},
                    {'action': 'hit2', 'source_sprite': sprite,
                     'source_frames': [1, 4, 5, 6, 7, 8, 9, 1, 1]},
                ]
                if bakes != required:
                    raise ValueError(f'Attack cloth stop/play scripts not baked: {folder}')
            elif bakes:
                raise ValueError(f'Unexpected basic-cloth animation on another selector: {folder}')
            path = folder / f'tangseng_{kind}_{selector}.png'
            image = Image.open(path).convert('RGBA')
            rows = (len(expected) + 5) // 6
            if image.size != (1200, rows * 200):
                raise ValueError(f'Incomplete six-column atlas: {path}')
            visible = 0
            for cell, pose in enumerate(expected):
                x, y = cell % 6 * 200, cell // 6 * 200
                bounds = image.crop((x, y, x + 200, y + 200)).getchannel('A').getbbox()
                if bounds is None:
                    if kind == 'body':
                        raise ValueError(f'Missing body art {folder.name}: {pose}')
                    continue
                visible += 1
                if min(bounds[:2]) <= 0 or max(bounds[2:]) >= 200:
                    raise ValueError(f'Clipped native artwork {folder.name}: {pose}, {bounds}')
            for cell in range(len(expected), rows * 6):
                x, y = cell % 6 * 200, cell // 6 * 200
                if image.crop((x, y, x + 200, y + 200)).getchannel('A').getbbox():
                    raise ValueError(f'Unexpected art in atlas padding: {path}')
            variants.append({'kind': kind, 'selector': selector, 'path': str(path.resolve()),
                             'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                             'source_cells': len(expected), 'visible_cells': visible})
            images[kind, selector] = image
    equipment = []
    official = {item['legacy_id']: item for item in source['equipment']}
    for item in _equipment_rows(args.repo / 'Configs/GameConfig/Datas'):
        if item['owner'] not in ('唐僧', 'Tangseng') or item['slot'] not in ('防具', '武器'):
            continue
        entry = official[item['legacy_id']]
        kind = 'body' if item['slot'] == '防具' else 'weapon'
        if entry['name'] != item['name'] or entry['kind'] != kind:
            raise ValueError(f'Equipment source identity mismatch: {item}')
        if (kind, entry['selector']) not in images:
            raise ValueError(f'Equipment has no full native action export: {item}')
        equipment.append({**item, 'selector': entry['selector']})
    if len(equipment) != len(official):
        raise ValueError('Source equipment audit is stale relative to Items.xlsx')
    result = {'source_sha256': ROLE_HASH, 'xml_sha256': source['xml_sha256'],
              'actions': list(ACTION_ORDER), 'frames': len(expected),
              'variants': variants, 'equipment': equipment,
              'scope': 'Source identity, frame coverage, baked cloth commands and atlas bounds only.',
              'pending': ['Native appearance bindings', 'All wardrobe combinations and draw-depth comparison',
                          'Real animation playback', 'Real attacks and gameplay']}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for image in images.values():
        image.close()
    print(f'TANGSENG WARDROBE EXPORT PASS: {len(equipment)} equipment identities; '
          f'{len(variants)} selectors x {len(expected)} source frames; no cell-edge clipping.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    for name in ('audit', 'atlases', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    audit(parser.parse_args())
