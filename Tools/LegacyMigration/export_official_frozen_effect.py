"""Export the original 35-frame ice overlay used by BaseAddEffect.showIce."""
import argparse
from pathlib import Path
from types import SimpleNamespace

from export_official_sea_monsters import exporter


def main(args):
    """Reuse the source hash, matrix, bounds and compact atlas exporter."""
    request = SimpleNamespace(source=args.source, xml=args.xml, inventory=args.inventory,
        java=args.java, ffdec=args.ffdec, work=args.work, only=[])
    exporter(request, {}, [('hero_frozen', 'ice')], compact=True,
             effect_blend_modes={'hero_frozen': 'add'})
    print('FROZEN EFFECT SOURCE PASS: original ice symbol114, full 35-frame non-looping timeline')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'xml', 'inventory', 'java', 'ffdec', 'work'):
        parser.add_argument('--' + name, type=Path, required=True)
    main(parser.parse_args())
