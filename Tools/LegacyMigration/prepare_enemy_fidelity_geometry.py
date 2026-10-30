"""Extract separate Jiao ice regions from the verified source display list.

This is authoring data for native AnimationLibrary tracks, never a runtime
interpreter. Each source pillar retains its own slot and full parent transform.
"""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as E

from export_official_sea_monsters import compose, _probe_reader
from export_official_wukong import matrix


def prepare(args):
    """Preserve the four independently animated source children and their gaps."""
    tags = E.parse(args.xml).getroot().find('tags')
    frames, bounds = _probe_reader(tags)
    manifest = json.loads(args.manifest.read_text(encoding='utf-8'))
    actor = next(a for a in manifest['actors'] if a['name'] == 'jiao_demon_king')
    action = next(a for a in actor['actions'] if a['name'] == 'attack_3')
    rows = []
    for display in frames(action['sprite']):
        row = [None] * 4
        for stick, age in display:
            if stick.get('name') != 'stick':
                continue
            nested = list(frames(int(stick.get('characterId'))))
            if age >= len(nested):
                continue
            for child, child_age in nested[age]:
                # Monster10's four staggered ice children occupy depths 1/3/5/7.
                depth = int(child.get('depth'))
                if depth not in (1, 3, 5, 7):
                    raise ValueError(f'Unexpected ice child depth: {depth}')
                transform = compose(action['matrix'], compose(matrix(stick), matrix(child)))
                row[depth // 2] = bounds(int(child.get('characterId')), child_age, transform)
        rows.append(row)
    if len(rows) != action['frames'] or max(sum(v is not None for v in row) for row in rows) != 4:
        raise ValueError('Expected all 45 frames and four independent ice pillars')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({'source_sha256': manifest['source_sha256'],
        'actor': actor['name'], 'animation': action['name'], 'fps': 24,
        'regions': rows}, indent=2) + '\n', encoding='utf-8')
    print(f'ICE SOURCE PASS: {len(rows)} frames, four separate regions, no union box')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--xml', type=Path, required=True)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    prepare(parser.parse_args())
