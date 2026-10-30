"""Apply reviewed action-level registration corrections without moving actor roots.

Original source matrices remain in the review manifest. Frame-local stride,
rotation, weapons and hit effects retain the source placement; no frame is
individually centered. Five visually reviewed actors receive action-level corrections.
"""
import argparse
from copy import deepcopy
import json
import math
import re
from pathlib import Path
from PIL import Image

from prepare_official_garuda_level import _pack_layer, compact_registered_atlas
from import_official_sea_monsters import frames_resource, ACTORS, PROJECT


def prepare(args):
    """Repack reviewed actions on a shared canvas and install only their artwork."""
    source = json.loads(args.manifest.read_text(encoding='utf-8'))
    result = {'fps': source['fps'], 'source_sha256': source['source_sha256'], 'actors': []}
    args.output.mkdir(parents=True, exist_ok=True)
    originals = list(source['actors'])
    for extra in args.extra_manifest:
        originals.extend(json.loads(extra.read_text(encoding='utf-8'))['actors'])
    directories = {name: spec[2] for name, spec in ACTORS.items()}
    directories.update({name: f'Monsters/Ground/{name}' for name in ('huaguoshan_monkey', 'gorilla')})
    for original in originals:
        name = original['name']
        if name not in ('shrimp_demon', 'turtle_minister', 'tide_demon', 'huaguoshan_monkey', 'gorilla'):
            continue
        actor = deepcopy(original)
        # The older ground manifest keeps source raster paths on its layer actions.
        if not actor['actions'][0].get('input'):
            actor['actions'] = deepcopy(actor['layers'][0]['actions'])
        floor = actor['hurt_bounds'][1] + actor['hurt_bounds'][3]
        for action in actor['actions']:
            correction = 0
            reason = ''
            if name == 'shrimp_demon' and action['name'] in ('idle', 'death'):
                correction = 8
                reason = 'Reviewed boot/abdomen anchor; antenna tips are not the ground contact.'
            elif name == 'turtle_minister' and action['name'] == 'death':
                final = Image.open(Path(action['input']) / f'{action["frames"]}.png').convert('RGBA')
                box = final.getchannel('A').point(lambda value: 255 if value >= 26 else 0).getbbox()
                if box is None:
                    raise ValueError('Expected a visible final turtle shell')
                bottom = math.floor(action['bounds'][1] + action['matrix'][5]) + box[3]
                correction = round(floor - bottom)
                reason = 'Reviewed final shell contact; keep the complete death motion on one registration.'
            elif action['name'] == 'death' and name in ('huaguoshan_monkey', 'gorilla', 'tide_demon'):
                correction = {'huaguoshan_monkey': 8, 'gorilla': -8, 'tide_demon': -16}[name]
                reason = 'Reviewed settled corpse contact against the native physical ground; retain the initial impact pose.'
            action['source_matrix'] = list(action['matrix'])
            action['registration_correction'] = [0, correction]
            if correction:
                if action['name'] == 'death':
                    # Keep the initial impact at its source registration, then settle
                    # the collapsed corpse/shell during the original rotation phase.
                    action['frame_registration_offsets'] = {
                        str(frame): [0, round(correction * min(1, max(0, (frame - 3) / 7)))]
                        for frame in range(1, action['frames'] + 1)}
                else:
                    action['matrix'][5] += correction
                action['registration_reason'] = reason
        layer = {'name': 'body', 'actions': actor['actions'], 'output': str(args.output / f'{name}.png')}
        _pack_layer(layer)
        compact_registered_atlas(layer)
        actor['layers'] = [layer]
        actor['_layer'] = layer
        directory = PROJECT / 'TheGame/Sprites/Characters/Enemies' / directories[name]
        atlas = directory / f'{name}_official_body.png'
        atlas.write_bytes(Path(layer['output']).read_bytes())
        previous_path = directory / f'{name}_official_frames.tres'
        previous_identity = re.search(r'uid="([^"]+)"', previous_path.read_text(encoding='utf-8-sig')) if previous_path.exists() else None
        frames = frames_resource(name, atlas, actor)
        # Keep the stable SpriteFrames identity from the installed scene; changing
        # packing must not strand existing scene UID references on a fresh import.
        scene_path = PROJECT / 'TheGame/Entitys/Enemies' / directories[name] / f'{name}_entity.tscn'
        scene_text = scene_path.read_text(encoding='utf-8-sig')
        identity = re.search(r'\[ext_resource type="SpriteFrames" uid="([^"]+)" path="'+re.escape(f'res://{frames.relative_to(PROJECT).as_posix()}')+r'"', scene_text)
        identity = identity or previous_identity
        if identity:
            text = frames.read_text(encoding='utf-8')
            frames.write_text(text.replace('[gd_resource type="SpriteFrames" format=3]',
                f'[gd_resource type="SpriteFrames" format=3 uid="{identity[1]}"]',1),encoding='utf-8')
        actor['installed_frames'] = f'res://{frames.relative_to(PROJECT).as_posix()}'
        actor['installed_scene'] = f'res://TheGame/Entitys/Enemies/{directories[name]}/{name}_entity.tscn'
        actor.pop('_layer')
        result['actors'].append(actor)
        print('GROUNDED ART:', name, [(a['name'], a['registration_correction']) for a in actor['actions']])
    (args.output / 'manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('GROUNDED ART PASS:', len(result['actors']), 'actors, reviewed source corrections, common root preserved')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--extra-manifest', type=Path, nargs='*', default=[])
    prepare(parser.parse_args())
