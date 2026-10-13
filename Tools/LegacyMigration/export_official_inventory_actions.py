"""Copy the official ZMXY3 single-player equipment action artwork verbatim."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

IMAGES = {
    'inventory_actions_background.png': ('shapes/369.png',),
    'inventory_action_equip.png': ('sprites/DefineSprite_376*', '1.png'),
    'inventory_action_equip_disabled.png': ('sprites/DefineSprite_376*', '2.png'),
    'inventory_action_gift_disabled.png': ('sprites/DefineSprite_367*', '2.png'),
    'inventory_action_sell.png': ('sprites/DefineSprite_381*', '1.png'),
}


def main(args):
    """Export the known Common revision without copying any official game logic."""
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[300:325] + content[:300] + content[325:]
    if hashlib.sha256(content).hexdigest() != '53722c6c8615d2542de2e3cc255a68c5d80712c6b8097aaecae6bc7be0a40ab2':
        raise ValueError('Unexpected Commonv3720 revision')
    destination = Path(__file__).resolve().parents[2] / 'Godot/GodotProject/TheGame/Sprites/UI/inventory'
    with tempfile.TemporaryDirectory(prefix='gamezmxy-inventory-actions-') as temporary:
        work = Path(temporary)
        (work / 'profile').mkdir()
        source = work / 'common.swf'
        source.write_bytes(content)
        subprocess.run([str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec),
            '-format', 'sprite:png,shape:png', '-selectid', '369,376,367,381', '-ignorebackground',
            '-export', 'sprite,shape', str(work / 'export'), str(source)],
            env=dict(os.environ, APPDATA=str(work / 'profile')), check=True)
        for name, components in IMAGES.items():
            exported = next((work / 'export').glob(components[0]))
            if len(components) > 1:
                exported /= components[1]
            target = destination / name
            if target.exists() and target.read_bytes() != exported.read_bytes():
                raise ValueError(f'Refusing to replace edited image: {target}')
            shutil.copyfile(exported, target)
            print(name)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('java', 'ffdec', 'source'):
        parser.add_argument('--' + name, type=Path, required=True)
    main(parser.parse_args())
