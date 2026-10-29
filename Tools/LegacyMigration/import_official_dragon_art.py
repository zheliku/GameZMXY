"""Install Dragon Palace actor art and native animation clocks from verified sources.

The ice aura remains a separate additive layer. Source canvas origins, full
timelines, attack-ID pulses and projectile end frames are retained explicitly.
Existing edited resources are never overwritten by rerunning this authoring tool.
"""
import argparse
from copy import deepcopy
import json
import re
from pathlib import Path

from import_garuda_actor_assets import (
    animation_resource, copy_atlas, sprite_frames_resource, track_value, write_text,
)

PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
DIRECTORY = 'Bosses/zm1_11_hidden_dragon_palace'
SOURCE_SHA = '5f6478c13cb57a46d46abac6fc70549f517358d727c3f2641aa75754c14f33c7'


def resource_uri(path):
    """Return the resource path without embedding machine-specific locations."""
    return 'res://' + path.relative_to(PROJECT).as_posix()


def install_layer(record, actor=False):
    """Keep every compact cell's original margin and complete source canvas."""
    layer = record['layers'][0]
    root = PROJECT / 'TheGame/Sprites/Characters/Enemies' / DIRECTORY
    name = record['name'] + ('_official_body' if actor else '')
    texture = root / (name + '.png')
    frames = root / (record['name'] + ('_official' if actor else '') + '_frames.tres')
    copy_atlas(Path(layer['output']), texture)
    write_text(frames, sprite_frames_resource(resource_uri(texture), layer))
    return frames


def actor_library(actor, rules, geometry=None):
    """Map this species' pulses without the older writer's attack-2 dash shortcut."""
    data = deepcopy(actor)
    for action in data['actions']:
        if geometry is not None:
            action['hit_bounds'] = geometry['actor'][action['name']]
        action['native_method_events'] = []
        skill_number = {'attack_2': '1', 'attack_3': '2', 'attack_4': '3'}.get(action['name'])
        if action['name'] == 'attack_1':
            action['native_method_events'].append({'frame': 11, 'method': 'SpawnWaveAnimEvent'})
        if action['name'] == 'attack_4':
            action['native_method_events'].append({'frame': 1, 'method': 'SpawnTorrentAnimEvent'})
        for event in action.get('frame_events', []):
            if 'newAttackId()' in event['body']:
                action['native_method_events'].append({'frame': event['frame'], 'method': 'NewAttackAnimEvent'})
                event['body'] = event['body'].replace('newAttackId()', 'sourceAttackIdPulse()')
    text = animation_resource(data, data['layers'][0], 'boss')
    # This parent's 90-frame attack has its own additive child clock at depth34.
    # Other actions and RESET clear it, including interrupted attacks and reuse.
    chunks = text.split('[sub_resource type="Animation" id="')
    result = [chunks[0]]
    for chunk in chunks[1:]:
        identifier = chunk.split('"', 1)[0]
        before, separator, after = chunk.partition('\n[resource]')
        next_track = max(int(value) for value in re.findall(r'^tracks/(\d+)/type', before, re.MULTILINE)) + 1
        tracks = []
        skill_number = {'Animation_attack_2': '1', 'Animation_attack_3': '2', 'Animation_attack_4': '3'}.get(identifier)
        if skill_number:
            # Method tracks have one key per time. Keep the initial invulnerability
            # separate from torrent creation, which is also on source frame one.
            tracks.append('\n'.join([
                f'tracks/{next_track}/type = "method"',
                f'tracks/{next_track}/path = NodePath(".")',
                f'tracks/{next_track}/keys = {{',
                '"times": PackedFloat32Array(0),',
                '"transitions": PackedFloat32Array(1),',
                '"values": [{"args": [%s], "method": &"BeginInvulnerabilityAnimEvent"}]' % rules['skill_invulnerability_seconds'][skill_number],
                '}',
            ]))
            next_track += 1
        if identifier == 'Animation_attack_2':
            tracks.extend([
                track_value(next_track, 'm_HitBoxRoot/m_IceAura:visible', [0], [True], 'bool'),
                track_value(next_track + 1, 'm_HitBoxRoot/m_IceAura:frame', [i / 24 for i in range(90)], list(range(90)), 'int'),
            ])
        else:
            tracks.append(track_value(next_track, 'm_HitBoxRoot/m_IceAura:visible', [0], [False], 'bool'))
        result.append('[sub_resource type="Animation" id="' + before + '\n' + '\n'.join(tracks) + '\n' + separator + after)
    return ''.join(result)


def main(args):
    """Install only the new dragon paths after checking the official revision."""
    source = json.loads(args.manifest.read_text(encoding='utf-8'))
    if source['source_sha256'] != SOURCE_SHA or source['fps'] != 24:
        raise ValueError('Unexpected official Dragon Palace actor revision')
    actor = source['actors'][0]
    if actor['legacy_id'] != 26 or actor['name'] != 'dragon_king':
        raise ValueError('Expected the single official Dragon King actor')
    effects = {item['name']: item for item in source['effects']}
    if effects['dragon_king_ice_aura']['blend_mode'] != 'add' or effects['dragon_king_torrent']['blend_mode'] != 'add':
        raise ValueError('The original ice/torrent additive layers must stay separate')
    install_layer(actor, actor=True)
    for effect in effects.values():
        install_layer(effect)
    library = PROJECT / 'TheGame/Entitys/Animations/Enemies' / DIRECTORY / 'dragon_king_library.tres'
    rules = json.loads(args.rules.read_text(encoding='utf-8'))
    if rules['source_sha256'] != SOURCE_SHA:
        raise ValueError('Dragon timing rules belong to another official source')
    geometry = json.loads(args.geometry.read_text(encoding='utf-8')) if args.geometry else None
    if geometry and geometry['source_sha256'] != SOURCE_SHA:
        raise ValueError('Clipped geometry belongs to a different source revision')
    write_text(library, actor_library(actor, rules, geometry), overwrite=args.update_library)
    if args.frozen:
        frozen = json.loads(args.frozen.read_text(encoding='utf-8'))
        if frozen['source_sha256'] != SOURCE_SHA:
            raise ValueError('Unexpected frozen status source revision')
        effect = frozen['effects'][0]
        layer = effect['layers'][0]
        root = PROJECT / 'TheGame/Sprites/Characters/Heroes/Status'
        atlas = root / 'hero_frozen.png'
        copy_atlas(Path(layer['output']), atlas)
        write_text(root / 'hero_frozen_frames.tres', sprite_frames_resource(resource_uri(atlas), layer))
    print('DRAGON ART AUTHOR PASS: actor and three independent effect atlases, full source clocks and six ice attack-ID pulses')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--frozen', type=Path)
    parser.add_argument('--rules', type=Path, required=True)
    parser.add_argument('--geometry', type=Path, help='Source clip-depth aware geometry report')
    parser.add_argument('--update-library', action='store_true', help='Explicitly refresh the newly authored native library')
    main(parser.parse_args())
