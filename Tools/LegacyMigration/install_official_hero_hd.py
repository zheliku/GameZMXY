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
    args = parser.parse_args()
    install(args.work.resolve(), args.project.resolve())
