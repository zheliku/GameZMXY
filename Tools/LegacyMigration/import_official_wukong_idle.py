"""Apply the original 100-frame wait timeline after the append-only idle importer.

Run import_official_wukong_idle.gd with the registered attack export manifest
first. This changes only idle tracks and atlas grid dimensions, preserving all
other animations and the independently replaceable body and weapon layers.
"""
import argparse
import json
from pathlib import Path
import re

PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
SOURCE_SHA256 = '11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7'


def keys(values, fps):
    """Build discrete keys retaining every original frame, including holds."""
    times = ', '.join(format(i / fps, '.9f').rstrip('0').rstrip('.') or '0' for i in range(len(values)))
    return ('{\n"times": PackedFloat32Array(' + times + '),\n'
            '"transitions": PackedFloat32Array(' + ', '.join(['1'] * len(values)) + '),\n'
            '"update": 1,\n"values": [' + ', '.join(map(str, values)) + ']\n}')


def replace_track(animation, index, values, fps):
    """Replace an existing track's keys without touching its path or settings."""
    pattern = rf'tracks/{index}/keys = \{{.*?\n\}}'
    result, count = re.subn(pattern, f'tracks/{index}/keys = {keys(values, fps)}', animation, flags=re.S)
    if count != 1:
        raise ValueError(f'Expected one track {index}, got {count}')
    return result


def apply(manifest_path):
    """Install the complete wait loop for combat and the inventory preview."""
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest['source_sha256'] != SOURCE_SHA256 or manifest['source_sprite'] != 529:
        raise ValueError('Expected verified Role_v7.swf wait sprite 529')
    frames = [int(value) for value in manifest['frames']]
    fps, rows = int(manifest['fps']), int(manifest['rows'])
    if len(frames) != 100 or fps != 24 or max(frames) >= rows * 6:
        raise ValueError('Invalid original wait timeline or atlas grid')
    duration = len(frames) / fps
    for relative, name in [
        ('TheGame/Entitys/Animations/wukong_anim_library.tres', 'idle1'),
        ('TheGame/Sprites/UI/inventory/inventory_preview_animation.tres', 'idle'),
    ]:
        path = PROJECT / relative
        text = path.read_text(encoding='utf-8')
        pattern = rf'\[sub_resource type="Animation" id="{name}"\].*?(?=\n\[|\Z)'
        match = re.search(pattern, text, re.S)
        if not match:
            raise ValueError(f'Missing {name} in {path}')
        animation = re.sub(r'length = [^\n]+', f'length = {duration:.9f}', match[0], count=1)
        for index in (0, 1):
            animation = replace_track(animation, index, frames, fps)
        if name == 'idle':
            # Aura keeps its existing 15 fps independently of the character.
            aura = [3 + i % 6 for i in range(int(duration * 15) + 1)]
            animation = replace_track(animation, 2, aura, 15)
        text = text[:match.start()] + animation + text[match.end():]
        if name == 'idle1':
            text = re.sub(r'\[sub_resource type="Animation" id="idle2"\].*?(?=\n\[)', '', text, flags=re.S)
            text = text.replace('&"idle2": SubResource("idle2"),\n', '')
        path.write_text(text, encoding='utf-8')
    for relative in ('TheGame/Entitys/wukong_entity.tscn', 'TheGame/UIs/Widgets/hero_preview.tscn'):
        path = PROJECT / relative
        text = re.sub(r'hframes = 6\nvframes = \d+', f'hframes = 6\nvframes = {rows}', path.read_text(encoding='utf-8'))
        path.write_text(text, encoding='utf-8')
    print(f'IDLE TIMELINE PASS: 100 frames / 24 fps = {duration:.9f}s; combat + preview; grid 6x{rows}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    apply(parser.parse_args().manifest)
