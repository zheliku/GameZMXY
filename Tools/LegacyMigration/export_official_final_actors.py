"""Stage the final ZMXY1 levels without changing any runtime asset.

The bull's third form separates the heart, hands, fire and backdrop because
their visibility, motion and collision lifetimes belong to different states.
All transforms remain in the source manifest; none are recentered by alpha.
"""
import argparse
import json
from pathlib import Path
from types import SimpleNamespace

from export_official_sea_monsters import exporter


SPECS = {
    'bull': {
        22: ('bull_demon_beast', {'walk': 'charge', 'wait': 'idle', 'dead': 'death'}),
        23: ('bull_demon_king', {'walk': 'run', 'wait': 'idle', 'hurt': 'hurt', 'dead': 'death',
                               **{f'hit{i}': f'attack_{i}' for i in range(1, 6)}}),
        24: ('bull_demon_heart', {'wait': 'idle', 'dead': 'death'}),
    },
    'guard': {
        25: ('bull_guard', {'walk': 'run', 'wait': 'idle', 'hit1': 'attack_1',
                            'hurt': 'hurt', 'dead': 'death'}),
    },
    'dragon': {
        26: ('dragon_king', {'walk': 'run', 'wait': 'idle', 'hurt': 'hurt', 'dead': 'death',
                             **{f'hit{i}': f'attack_{i}' for i in range(1, 5)}}),
    },
}
PARTS = {
    'bull': [('bull_heart', 'export.monster.Monster23Child.Heart'),
             ('bull_hands', 'export.monster.Monster23Child.Hands'),
             ('bull_fire', 'export.monster.Monster23Child.Fire'),
             ('bull_background', 'Monster24BG')],
    'dragon': [('dragon_king_wave', 'Monster26Bullet1'),
               ('dragon_king_torrent', 'Monster26Bullet2')],
}
SOURCE_PACKS = {'bull': 'monster2', 'guard': 'monster1', 'dragon': 'monster3'}


def export(args):
    """Resolve each official pack once and preserve independently animated children."""
    repo = Path(__file__).resolve().parents[2]
    inventory = json.loads(args.inventory.read_text(encoding='utf-8'))
    sources = {item['name']: item for item in inventory['sources']}
    groups = ('bull', 'guard', 'dragon') if args.group == 'all' else (args.group,)
    for group in groups:
        source = sources[SOURCE_PACKS[group]]
        destination = args.work / ('bull_art' if group in ('bull', 'guard') else 'dragon_art')
        request = SimpleNamespace(source=repo / source['swf'], xml=repo / source['xml'],
            inventory=args.inventory, java=args.java, ffdec=args.ffdec,
            work=destination / ('monsters' if group == 'guard' else 'actors'), only=list(SPECS[group]))
        exporter(request, SPECS[group], PARTS.get(group, ()),
                 hidden_children=[(736, 'heart')] if group == 'bull' else (), compact=True,
                 action_splits=[(50, 34, 'dragon_king_ice_aura')] if group == 'dragon' else (),
                 effect_blend_modes={'dragon_king_ice_aura': 'add', 'dragon_king_torrent': 'add'},
                 selector_frames=[(917, 1)] if group == 'bull' else ())
    print(f'FINAL ACTORS SOURCE PASS: {", ".join(groups)}; independent registrations and original source frames')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('inventory', 'java', 'ffdec', 'work'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--group', choices=('all', *SPECS), default='all')
    export(parser.parse_args())
