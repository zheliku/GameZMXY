"""Create new native Role2 art resources from the reviewed wardrobe export.

Existing resources require an explicit replacement option. Identical reruns are
safe; gameplay callbacks/effects remain the responsibility of their owning states.
"""
import argparse
import hashlib
from io import BytesIO
import json
from pathlib import Path

from PIL import Image

from audit_official_tangseng import ROLE_HASH
from import_official_wukong_attacks import number, track


APPEARANCES = {
    'body': {1: 'bare', 2: 'cassock', 3: 'excellent', 4: 'heirloom',
             5: 'kirin', 6: 'heavenly_fiend'},
    'weapon': {1: 'default', 2: 'pine', 3: 'excellent', 4: 'earth_fiend',
               5: 'white_tiger', 6: 'blood_sea', 7: 'nine_ring'},
}


def compact_atlases(variants, frame_count=311):
    """Deduplicate each layer across every wardrobe choice, retaining 311 source keys."""
    outputs, mappings, counts = {}, {}, {}
    for kind in ('body', 'weapon'):
        selected = [(key, item) for key, item in variants.items() if key[0] == kind]
        images = {}
        for key, item in selected:
            raw = Path(item['path']).read_bytes()
            if hashlib.sha256(raw).hexdigest() != item['sha256']:
                raise ValueError(f'Atlas changed since review: {item["path"]}')
            images[key] = Image.open(BytesIO(raw)).convert('RGBA')
        signatures, poses, mapping = {}, [], []
        for frame in range(frame_count):
            box = (frame % 6 * 200, frame // 6 * 200,
                   (frame % 6 + 1) * 200, (frame // 6 + 1) * 200)
            cells = {}
            for key, image in images.items():
                cell = image.crop(box)
                cells[key] = cell if cell.getchannel('A').getbbox() else Image.new('RGBA', (200, 200))
            signature = b''.join(hashlib.sha256(cell.tobytes()).digest() for cell in cells.values())
            if signature not in signatures:
                signatures[signature] = len(poses)
                poses.append(cells)
            mapping.append(signatures[signature])
        empty = next((i for i, cells in enumerate(poses)
                      if all(cell.getchannel('A').getbbox() is None for cell in cells.values())), None)
        if empty is not None:
            order = [empty] + [i for i in range(len(poses)) if i != empty]
            remap = {old: new for new, old in enumerate(order)}
            poses = [poses[i] for i in order]
            mapping = [remap[cell] for cell in mapping]
        mappings[kind], counts[kind] = mapping, len(poses)
        for key, _ in selected:
            atlas = Image.new('RGBA', (1200, ((len(poses) + 5) // 6) * 200))
            for frame, cells in enumerate(poses):
                atlas.paste(cells[key], (frame % 6 * 200, frame // 6 * 200))
            output = BytesIO()
            atlas.save(output, format='PNG')
            outputs[key] = output.getvalue()
            images[key].close()
    return outputs, mappings, counts


def library(animations):
    """Use the existing discrete-track authoring format for every source frame."""
    blocks = ['[gd_resource type="AnimationLibrary" format=3]\n']
    for name, frames, loop, markers, extras in animations:
        block = f'[sub_resource type="Animation" id="{name}"]\nresource_name = "{name}"\n'
        count = len(frames['body']) if frames else 1
        block += f'length = {number(count / 24)}\n'
        if loop:
            block += 'loop_mode = 1\n'
        if markers:
            block += 'markers = [' + ', '.join(
                '{"color": Color(1, 1, 1, 1), "name": &"' + label +
                '", "time": ' + number(time) + '}' for label, time in markers.items()) + ']\n'
        tracks = []
        if frames:
            for kind, layer in [('body', 'm_Body'), ('weapon', 'm_Weapon')]:
                tracks.append((layer + ':frame', [i / 24 for i in range(count)],
                               [str(frame) for frame in frames[kind]]))
        tracks.extend((path, [0], [value]) for path, value in extras)
        block += ''.join(track(index, *data) for index, data in enumerate(tracks))
        blocks.append(block)
    blocks.append('[resource]\n_data = {\n' + ',\n'.join(
        f'&"{name}": SubResource("{name}")' for name, *_ in animations) + '\n}\n')
    return '\n'.join(blocks).encode('utf-8')


def install(args):
    """Bind current stable equipment IDs to one independent atlas per selector."""
    report = json.loads(args.report.read_text(encoding='utf-8'))
    if report['source_sha256'] != ROLE_HASH or report['frames'] != 311:
        raise ValueError('Expected the complete reviewed Role2 wardrobe export')
    variants = {(item['kind'], item['selector']): item for item in report['variants']}
    compact, mapping, counts = compact_atlases(variants)
    art = 'TheGame/Sprites/Characters/Heroes/tangseng'
    animation_root = 'TheGame/Entitys/Animations/Heroes/Tangseng'
    files = {}
    appearance = ['[gd_resource type="Resource" script_class="HeroAppearanceResource" format=3]\n',
        '[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Heroes/HeroAppearanceResource.cs" id="script"]']
    for (kind, selector), item in variants.items():
        path = f'{art}/tangseng_{kind}_{APPEARANCES[kind][selector]}.png'
        files[path] = compact[kind, selector]
        appearance.append(f'[ext_resource type="Texture2D" path="res://{path}" id="{kind}_{selector}"]')
    appearance.append(f'[ext_resource type="AnimationLibrary" path="res://{animation_root}/tangseng_preview_library.tres" id="preview"]')
    appearance.append(f'[ext_resource type="Resource" path="res://{art}/tangseng_inventory_appearance.tres" id="inventory_appearance"]')
    appearance.append('\n[resource]\nscript = ExtResource("script")\nm_PreviewAnimations = ExtResource("preview")\nm_PreviewAppearance = ExtResource("inventory_appearance")')
    for kind, base, field in [('body', 'm_BaseBody', 'm_ArmorTextures'),
                              ('weapon', 'm_BaseWeapon', 'm_WeaponTextures')]:
        appearance.append(f'{base} = ExtResource("{kind}_1")')
        entries = [f'{item["id"]}: ExtResource("{kind}_{item["selector"]}")'
                   for item in report['equipment'] if ('body' if item['slot'] == '防具' else 'weapon') == kind]
        appearance.append(f'{field} = Dictionary[int, Texture2D]({{\n' + ',\n'.join(entries) + '\n})')
    files[f'{art}/tangseng_appearance.tres'] = ('\n'.join(appearance) + '\n').encode('utf-8')
    source = json.loads((Path(variants['body', 1]['path']).parent / 'manifest.json').read_text(encoding='utf-8'))
    frames = {label: {kind: [mapping[kind][i] for i, item in enumerate(source['frames'])
                            if item['action'] == label] for kind in ('body', 'weapon')}
              for label in report['actions']}
    reset = [('m_Body:visible', 'true'), ('m_Weapon:visible', 'true'),
             ('CollisionShape2D:disabled', 'false'), ('m_HurtBox/CollisionShape2D:disabled', 'false'),
             ('m_HitBoxRoot/m_HitBox/CollisionShape2D:disabled', 'true')]
    main = [('RESET', {kind: [mapping[kind][0]] for kind in mapping}, False, {}, reset)]
    for name, label, loop in [('idle1', 'wait', True), ('walk', 'walk', True), ('run', 'run', True),
                             ('jump', 'jump1', True), ('jump_2', 'jump2', False), ('fall', 'jump3', True),
                             ('hurt', 'hurt', False), ('attack_1', 'hit1', False)]:
        markers = {} if loop else {'complete': (len(frames[label]['body']) - 1) / 24}
        main.append((name, frames[label], loop, markers, []))
    main.append(('death', [], False, {}, [
        ('m_Body:visible', 'false'), ('m_Weapon:visible', 'false'),
        ('CollisionShape2D:disabled', 'true'), ('m_HurtBox/CollisionShape2D:disabled', 'true'),
        ('m_HitBoxRoot/m_HitBox/CollisionShape2D:disabled', 'true')]))
    files[f'{animation_root}/tangseng_anim_library.tres'] = library(main)
    skills = []
    for name, label in [('ice_dragon', 'hit2'), ('dark_ice', 'hit3'),
                        ('water_burst', 'hit4'), ('heavenly_dew', 'hit5')]:
        markers = {'complete': (len(frames[label]['body']) - 1) / 24}
        if label == 'hit2':
            markers.update(charge=0, release=1 / 24)
        if label == 'hit4':
            markers.update(timeout=41 / 24, release=42 / 24)
        skills.append((name, frames[label], False, markers, []))
    files[f'{animation_root}/tangseng_skill_library.tres'] = library(skills)
    for relative, content in files.items():
        path = args.project / relative
        if path.exists() and path.read_bytes() != content and not args.replace_generated:
            raise FileExistsError(f'Refusing to overwrite authored resource: {path}')
    if args.apply:
        for relative, content in files.items():
            path = args.project / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            if not path.exists() or args.replace_generated:
                path.write_bytes(content)
    if args.manifest:
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps({
            'source_sha256': ROLE_HASH, 'source_frames': source['frames'],
            'frame_cells': mapping, 'unique_cells': counts,
            'variants': [{'kind': kind, 'selector': selector,
                'path': f'{art}/tangseng_{kind}_{APPEARANCES[kind][selector]}.png',
                'sha256': hashlib.sha256(compact[kind, selector]).hexdigest()}
                for kind, selector in variants],
        }, indent=2) + '\n', encoding='utf-8')
    print(f'TANGSENG NATIVE ART {"INSTALLED" if args.apply else "PREVIEW"}: '
          f'{len(variants)} independent atlases ({counts["body"]} body/{counts["weapon"]} weapon cells), '
          '15 equipment bindings, 3 animation libraries; '
          'gameplay/effects and real playback are not certified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--project', type=Path,
                        default=Path(__file__).resolve().parents[2] / 'Godot/GodotProject')
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--replace-generated', action='store_true',
                        help='Replace this tool\'s reviewed generated art and animation resources')
    parser.add_argument('--manifest', type=Path)
    install(parser.parse_args())
