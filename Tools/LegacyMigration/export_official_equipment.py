"""Export transparent equipment foregrounds from 4399's OtherMat_v9.swf."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from io import BytesIO
from PIL import Image, ImageFilter

EQUIPMENT = {
    'rough_pilgrim_clothes': (52, 29), 'rough_pilgrim_staff': (60, 16),
    'rough_cassock': (48, 25), 'rough_pine_staff': (55, 14),
    'common_pilgrim_clothes': (51, 28), 'common_pilgrim_staff': (59, 15),
    'common_cassock': (47, 24), 'common_pine_staff': (54, 13),
    'earth_fiend_ring': (44, 21), 'earth_fiend_ape_armor': (114, 113),
    'earth_fiend_scepter': (112, 111),
    'heirloom_bracelet': (42, 19), 'heirloom_sword': (56, 30), 'heirloom_clothes': (6, 5),
    'heavenly_fiend_necklace': (43, 20),
}


def inventory_icon(ground, common):
    """Preserve the complete source foreground and add only the common-quality halo.

    The source bag art has a baked plate. Color-keying it erases gray equipment;
    use the intact transparent source instead. Equal padding keeps both qualities
    registered and leaves space for the white rim shown by the official bag art.
    """
    foreground = Image.new('RGBA', (ground.width+8, ground.height+8))
    foreground.paste(ground, (4, 4))
    if not common:
        return foreground
    glow = foreground.getchannel('A').filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(.65))
    result = Image.new('RGBA', foreground.size, 'white')
    result.putalpha(glow)
    result.alpha_composite(foreground)
    return result


def export(java: Path, ffdec: Path, source: Path, selection=None, destination=None):
    """Use the original transparent foreground in both bag and ground views."""
    content = source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != 'fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e':
        raise ValueError('Unexpected OtherMat resource revision')
    equipment = {name: EQUIPMENT[name] for name in selection} if selection else EQUIPMENT
    destination = destination or Path(__file__).resolve().parents[2] / 'Godot/GodotProject/TheGame/Sprites/Items/equipment'
    destination.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='gamezmxy-equipment-') as temporary:
        temp = Path(temporary)
        readable = temp / 'other.swf'
        readable.write_bytes(content)
        environment = dict(os.environ, APPDATA=str(temp / 'profile'))
        (temp / 'profile').mkdir()
        subprocess.run([str(java), f'-Duser.home={temporary}', '-jar', str(ffdec), '-selectid',
                        ','.join(str(i) for pair in equipment.values() for i in pair),
                        '-export', 'image', temporary, str(readable)], env=environment, check=True)
        for name, ids in equipment.items():
            exported = next(temp.glob(f'{ids[1]}_*.png'))
            original_bag = next(temp.glob(f'{ids[0]}_*.png'))
            for prefix in ('equipment', 'ground'):
                target = destination / f'{prefix}_{name}.png'
                content = exported.read_bytes()
                if prefix == 'equipment' and name.startswith(('rough_', 'common_')):
                    foreground = inventory_icon(Image.open(exported).convert('RGBA'), name.startswith('common_'))
                    buffer = BytesIO()
                    foreground.save(buffer, format='PNG')
                    content = buffer.getvalue()
                if target.exists() and target.read_bytes() not in (exported.read_bytes(), original_bag.read_bytes(), content):
                    raise ValueError(f'Refusing to replace edited image: {target}')
                target.write_bytes(content)
                print(f'transparent symbol {ids[0] if prefix == "equipment" else ids[1]} -> {target.name}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for parameter in ('java', 'ffdec', 'source'):
        parser.add_argument(f'--{parameter}', type=Path, required=True)
    parser.add_argument('--only', nargs='+', choices=EQUIPMENT)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    export(args.java, args.ffdec, args.source, args.only, args.output)
