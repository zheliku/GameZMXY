"""Register verified high-density hero atlases at their original resource paths.

Reads the isolated export directory's manifests and pre-install native files.
Verified PNGs are installed first; resource UIDs and unrelated native text remain.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

from PIL import Image
from import_official_wukong_attacks import remap_frame_tracks


def visible_first(work, project):
    """Move Wukong's intentional transparent slot to the end without changing a pose."""
    directory = project / 'TheGame/Sprites/Characters/Heroes/wukong'
    paths = [project / value.removeprefix('res://') for value in re.findall(
        r'path="([^"]+\.png)"', (directory / 'wukong_appearance.tres').read_text(encoding='utf-8'))]
    mapping = list(range(90))
    mapping[0], mapping[1], mapping[85] = 85, 0, 1
    first_box = (0, 0, 400, 400)
    with Image.open(directory / 'wukong_body_bare.png') as image:
        if image.crop(first_box).getchannel('A').getbbox():
            print('WUKONG VISIBLE FIRST: already installed')
            return
    for path in paths:
        with Image.open(path) as image:
            atlas = image.convert('RGBA')
        if atlas.size != (2400, 6000):
            raise ValueError('Expected the current 86-pose Wukong density2 contract')
        cells = {cell: atlas.crop((cell % 6 * 400, cell // 6 * 400,
                 (cell % 6 + 1) * 400, (cell // 6 + 1) * 400)) for cell in (0, 1, 85)}
        if cells[0].getchannel('A').getbbox():
            raise ValueError('Expected the shared transparent slot before moving it')
        for cell, image in cells.items():
            target = mapping[cell]
            atlas.paste(image, (target % 6 * 400, target // 6 * 400))
        atlas.save(path)
    for suffix in ('anim', 'skill'):
        path = project / f'TheGame/Entitys/Animations/Heroes/Wukong/wukong_{suffix}_library.tres'
        path.write_text(remap_frame_tracks(path.read_text(encoding='utf-8'), mapping), encoding='utf-8', newline='\n')
    scene = project / 'TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn'
    text = scene.read_text(encoding='utf-8')
    for field in ('m_Body', 'm_Weapon'):
        pattern = r'(\[node name="'+field+r'"[^\n]*\]\n)(.*?)(?=\n\[|\Z)'
        def remap_scene(match):
            return match[1]+re.sub(r'^frame = (\d+)', lambda value: 'frame = '+str(mapping[int(value[1])]), match[2], flags=re.M)
        text = re.sub(pattern, remap_scene, text, flags=re.S)
    scene.write_text(text, encoding='utf-8', newline='\n')
    # These are the existing source identities used by --frame-map reexports.
    for path in (work / 'installed').glob('wukong_*.json'):
        data = json.loads(path.read_text(encoding='utf-8'))
        if 'poses' not in data:
            continue
        data['poses'] = {str(mapping[int(cell)]): pose for cell, pose in data['poses'].items()}
        data['motion_cells'] = [mapping[int(cell)] for cell in data['motion_cells']]
        data['source_cell_map'] = [mapping[int(cell)] for cell in data['source_cell_map']]
        data['output'] = str(directory / path.with_suffix('.png').name)
        path.write_text(json.dumps(data, indent=2), encoding='utf-8')
    (work / 'visible_first_mapping.json').write_text(json.dumps({'old_to_new': mapping,
        'transparent_cell': 85, 'first_cell': 'original idle pose', 'changed_cells': [0, 1, 85]}, indent=2), encoding='utf-8')
    print('WUKONG VISIBLE FIRST PASS: 13 atlases, identical pixels; transparent skill slot85 and original timing retained')


def install(work, project):
    """Apply only verified texture references, density, grids and equivalent cells."""
    mapping = json.loads((work / 'wukong_hd_manifest.json').read_text())['mapping']
    source_root = work / 'before'

    # Registration must not apply a high-density grid to old or manually edited art.
    for name in ['wukong_hd_manifest.json', 'tangseng_hd_manifest.json', 'preview_hd_manifest.json']:
        report = json.loads((work / name).read_text())
        for variant in report['variants']:
            hero = variant.get('hero', report.get('hero'))
            stem = variant['stem']
            path = project / f'TheGame/Sprites/Characters/Heroes/{hero}'
            path = path / ('preview/' if '_preview_' in stem else '') / (stem + '.png')
            if hashlib.sha256(path.read_bytes()).hexdigest() != variant['packed_sha256']:
                raise ValueError(f'Install the verified PNG at its original path first: {path}')

    def write(relative, text):
        target = project / relative
        original = (source_root / relative).read_bytes()
        newline = '\r\n' if b'\r\n' in original else '\n'
        content = text.replace('\n', newline).encode('utf8')
        if target.read_bytes() != original and target.read_text() != text:
            raise ValueError(f'Refusing to overwrite native edits made after the export baseline: {target}')
        target.write_bytes(content)

    for hero in ['wukong', 'tangseng']:
        for suffix in ['appearance', 'inventory_appearance']:
            relative = Path(f'TheGame/Sprites/Characters/Heroes/{hero}/{hero}_{suffix}.tres')
            text = (source_root / relative).read_text()
            text = text.replace('script = ExtResource("script")', 'script = ExtResource("script")\n_textureDensity = 2')
            write(relative, text)
        for suffix in ['anim', 'skill', 'preview']:
            relative = Path(f'TheGame/Entitys/Animations/Heroes/{hero.title()}/{hero}_{suffix}_library.tres')
            text = (source_root / relative).read_text()
            if hero == 'wukong' and suffix != 'preview':
                text = remap_frame_tracks(text, mapping)
            write(relative, text)

    for relative, hero, inventory in [
        (Path('TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn'), 'wukong', False),
        (Path('TheGame/Entitys/Heroes/Tangseng/tangseng_entity.tscn'), 'tangseng', False),
        (Path('TheGame/UIs/Inventory/hero_preview.tscn'), 'wukong', True),
    ]:
        original = (source_root / relative).read_text()
        fields = ['Body', 'Weapon'] if inventory else ['m_Body', 'm_Weapon']
        text = original
        for field in fields:
            def sprite(match):
                block = match[0]
                path = re.search(r'path="([^"]+)" id="' + re.escape(re.search(r'texture = ExtResource\("([^"]+)"\)', block)[1]) + '"', text)[1]
                height = Image.open(project / path.removeprefix('res://')).height
                rows = height // 400
                if re.search(r'^scale = ', block, re.M):
                    block = re.sub(r'^scale = [^\n]+', 'scale = Vector2(0.5, 0.5)', block, flags=re.M)
                else:
                    block = block.replace('\n', '\nscale = Vector2(0.5, 0.5)\n', 1)
                block = re.sub(r'^vframes = \d+', f'vframes = {rows}', block, flags=re.M)
                if hero == 'wukong' and not inventory:
                    frame = re.search(r'^frame = (\d+)', block, re.M)
                    if frame:
                        block = re.sub(r'^frame = \d+', f'frame = {mapping[int(frame[1])]}', block, flags=re.M)
                    elif mapping[0] != 0:
                        block = block.rstrip('\n') + f'\nframe = {mapping[0]}\n\n'
                return block
            text = re.sub(r'^\[node name="' + field + r'"[^\n]*\]\n.*?(?=^\[|\Z)', sprite, text, flags=re.M | re.S)
        write(relative, text)
    print('HERO HD REGISTER PASS: original UIDs/text structure retained; only textures/density/grids/equivalent frames')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2] / 'Godot/GodotProject')
    parser.add_argument('--visible-first', action='store_true', help='Move the current shared transparent Wukong slot behind visible poses')
    args = parser.parse_args()
    if args.visible_first:
        visible_first(args.work.resolve(), args.project.resolve())
    else:
        install(args.work.resolve(), args.project.resolve())
