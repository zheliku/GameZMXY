"""Materialize the reviewed Monster14/16/17 atlases and native timelines."""
import argparse
import json
import re
from import_garuda_actor_assets import animation_resource as native_animation
from import_garuda_actor_assets import sprite_frames_resource, effect_scene, track_value, compact_keys
from pathlib import Path
from PIL import Image

FPS = 24.0
PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
ACTORS = {
    'jiao_demon_king': (10, 'JiaoDemonKingEntity', 'Bosses/zm1_08_east_sea', ['attack_1', 'attack_2', 'attack_3']),
    'shrimp_demon': (14, 'ShrimpDemonEntity', 'Monsters/Sea/shrimp_demon', ['attack_1']),
    'tide_demon': (16, 'TideDemonEntity', 'Monsters/Sea/tide_demon', ['attack_1', 'attack_2']),
    'turtle_minister': (17, 'TurtleMinisterEntity', 'Monsters/Sea/turtle_minister', ['attack_1']),
    'lantern_fish': (20, 'LanternFishEntity', 'Monsters/Sea/lantern_fish', ['attack_1']),
    'shark_spawn': (18, 'SharkSpawnEntity', 'Monsters/Sea/shark_spawn', ['attack_1', 'attack_2']),
    'shark_demon_king': (19, 'SharkDemonKingEntity', 'Bosses/zm1_09_shark_whirlpool',
                         ['attack_1', 'attack_2', 'attack_3']),
}


def godot_vec(value):
    return f'Vector2({float(value[0]):.6f}, {float(value[1]):.6f})'


def pack_actor(actor, root):
    """Install a staged atlas that already preserves the complete source origin."""
    layer = actor['layers'][0]
    name = actor['name']
    directory = PROJECT / 'TheGame/Sprites/Characters/Enemies' / ACTORS[name][2]
    directory.mkdir(parents=True, exist_ok=True)
    atlas_path = directory / f'{name}_official_body.png'
    atlas_path.write_bytes(Path(layer['output']).read_bytes())
    for action in actor['actions']:
        action['_cells'] = layer['poses'][action['name']]
    return atlas_path, {**layer, 'offset': layer['sprite_offset']}


def frames_resource(name, atlas_path, actor):
    directory = atlas_path.parent
    if actor['_layer'].get('atlas_regions'):
        path = directory / f'{name}_official_frames.tres'
        path.write_text(sprite_frames_resource(f'res://{atlas_path.relative_to(PROJECT).as_posix()}', actor['_layer']), encoding='utf-8')
        return path
    textures = []
    animations = []
    for action in actor['actions']:
        values = []
        for cell in action['_cells']:
            resource_id = f'AtlasTexture_{len(textures)}'
            x = cell % actor['_layer']['columns'] * actor['_layer']['cell_size'][0]
            y = cell // actor['_layer']['columns'] * actor['_layer']['cell_size'][1]
            textures.append((resource_id, x, y))
            values.append(resource_id)
        animations.append((action['name'], values))
    lines = ['[gd_resource type="SpriteFrames" format=3]',
             f'[ext_resource type="Texture2D" path="res://{atlas_path.relative_to(PROJECT).as_posix()}" id="atlas"]']
    for resource_id, x, y in textures:
        lines += [f'[sub_resource type="AtlasTexture" id="{resource_id}"]',
                  'atlas = ExtResource("atlas")',
                  f'region = Rect2({x}, {y}, {actor["_layer"]["cell_size"][0]}, {actor["_layer"]["cell_size"][1]})']
    entries = []
    for action, values in animations:
        entries.append('{"frames": [%s], "loop": %d, "name": &"%s", "speed": 24.0}' %
                       (', '.join('{"duration": 1.0, "texture": SubResource("%s")}' % value for value in values),
                        1 if action in ('idle', 'run') else 0, action))
    lines += ['', '[resource]', 'animations = [' + ', '.join(entries) + ']', '']
    path = directory / f'{name}_official_frames.tres'
    path.write_text('\n'.join(lines), encoding='utf-8')
    return path


def animation_resource(actor, library_path, frames_layer):
    """Reuse the native track writer; the source manifest owns endpoints and probes."""
    if actor['name'] == 'jiao_demon_king':
        for action in actor['actions']:
            if action['name'] == 'attack_2':
                action['native_method_events'] = [{'frame': 5, 'method': 'SpawnDragonWaveAnimEvent'}]
            elif action['name'] == 'attack_3':
                action['native_method_events'] = [{'frame': 1, 'method': 'BeginSpinAnimEvent'}]
    text = native_animation(actor, actor['layers'][0], 'boss' if actor['name'] == 'jiao_demon_king' else 'monster')
    if actor['name'] == 'shrimp_demon':
        block = re.search(r'(\[sub_resource type="Animation" id="Animation_attack_1"\].*?)(?=\n\[)', text, re.S)
        event = '\n'.join(['tracks/6/type = "method"', 'tracks/6/path = NodePath(".")',
            'tracks/6/keys = {"times": PackedFloat32Array(0.083333333),',
            '"transitions": PackedFloat32Array(1),',
            '\"values\": [{"args": [], "method": &"SpawnProjectileAnimEvent"}]}', ''])
        text = text[:block.end()] + event + text[block.end():]
    if actor['name'] in ('shark_spawn', 'shark_demon_king'):
        # Monster18 changes dash speed without refreshing its contact packet;
        # Monster19 creates independent effects at the source frames.
        events = {
            'shark_spawn': {
                'attack_1': [(34, 'DashAnimEvent'), (37, 'DashAnimEvent'),
                             (40, 'DashAnimEvent'), (43, 'DashAnimEvent'), (46, 'DashAnimEvent')],
                'attack_2': [(1, 'BeginTornadoAnimEvent'), (49, 'SpawnTornadoWarningAnimEvent')],
            },
            'shark_demon_king': {
                'attack_1': [],
                'attack_1_low': [],
                'attack_2': [(22, 'SpawnSharkWaveAnimEvent')],
                'attack_2_low': [(22, 'SpawnSharkWaveAnimEvent')],
                'attack_3': [(5, 'SpawnBubbleAnimEvent')],
                'attack_3_low': [(5, 'SpawnBubbleAnimEvent')],
            },
        }[actor['name']]
        for action in actor['actions']:
            action['native_method_events'] = [
                {'frame': frame, 'method': method} for frame, method in events.get(action['name'], [])
            ]
            if actor['name'] == 'shark_demon_king' and action['name'].startswith(('attack_2', 'attack_3')):
                action['native_method_events'].insert(0, {
                    'frame': 1, 'method': 'BeginInvulnerabilityAnimEvent',
                    'args': [(20 if action['name'].startswith('attack_2') else 18) / FPS]})
        text = native_animation(actor, actor['layers'][0], 'boss' if actor['name'] == 'shark_demon_king' else 'monster')
    library_path.write_text(text, encoding='utf-8')
    return library_path


def scene_for(actor, atlas_path, frames_path, library_path):
    name = actor['name']; entity_id, class_name, relative, attacks = ACTORS[name]
    is_boss = name in ('jiao_demon_king', 'shark_demon_king')
    script_directory = relative
    script_path = f'res://TheGame/GameScripts/Entity/Enemies/{script_directory}/{class_name}.cs'
    frame_res = f'res://{frames_path.relative_to(PROJECT).as_posix()}'
    library_res = f'res://{library_path.relative_to(PROJECT).as_posix()}'
    entity_path = PROJECT / f'TheGame/Entitys/Enemies/{script_directory}/{name}_entity.tscn'
    entity_path.parent.mkdir(parents=True, exist_ok=True)
    hurt = actor['hurt_bounds'] or [-20, -40, 40, 80]
    projectile_path = 'res://TheGame/Entitys/Enemies/Monsters/Sea/shrimp_demon/shrimp_projectile_effect.tscn'
    lines = ['[gd_scene format=3]', f'[ext_resource type="Script" path="{script_path}" id="script"]',
             f'[ext_resource type="SpriteFrames" path="{frame_res}" id="frames"]',
             f'[ext_resource type="AnimationLibrary" path="{library_res}" id="library"]']
    if name == 'shrimp_demon':
        lines.append(f'[ext_resource type="PackedScene" path="{projectile_path}" id="projectile"]')
    if name == 'jiao_demon_king':
        lines += [
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_effect.tscn" id="water"]',
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/boss_death_effect.tscn" id="death"]',
            '[ext_resource type="SpriteFrames" path="res://TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_back_hit_frames.tres" id="back_hit"]',
        ]
    if name == 'shark_demon_king':
        lines += [
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave_effect.tscn" id="shark_wave"]',
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning_effect.tscn" id="bubble_warning"]',
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_effect.tscn" id="bubble"]',
            '[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Bosses/zm1_06_garuda_cave/boss_death_effect.tscn" id="death"]',
        ]
    if name == 'shark_spawn':
        for key in ('slash', 'warning', 'tornado'):
            lines.append(f'[ext_resource type="PackedScene" path="res://TheGame/Entitys/Enemies/Monsters/Sea/shark_spawn/shark_spawn_{key}_effect.tscn" id="{key}"]')
    if not is_boss:
        lines.append('[ext_resource type="PackedScene" path="res://TheGame/UIs/Battle/monster_health_bar.tscn" id="health"]')
    lines += [
             '[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Combat/HurtBoxNode.cs" id="hurt"]',
             '[sub_resource type="CapsuleShape2D" id="body"]', f'radius = {min(hurt[2], hurt[3]) / 2:.6f}', f'height = {max(hurt[3], hurt[2]):.6f}',
             '[sub_resource type="RectangleShape2D" id="hurt_shape"]', f'size = {godot_vec([hurt[2], hurt[3]])}',
             '[sub_resource type="RectangleShape2D" id="hit_shape"]', 'size = Vector2(1, 1)',
             '[sub_resource type="RectangleShape2D" id="detector"]',
             f'size = Vector2({800 if entity_id == 20 else 600}, {2 * {10: 250, 14: 450, 16: 150, 17: 400, 18: 450, 19: 150, 20: 200}[entity_id]})',
             '', f'[node name="{class_name}" type="CharacterBody2D" node_paths=PackedStringArray("m_Detector", "m_Body", "m_AnimPlayer", "m_HurtBox", "m_HitBox", "m_HitBoxRoot"' + (', "m_BackHit"' if name == 'jiao_demon_king' else '') + (', "m_HealthBar"' if not is_boss else '') + ')]',
             'collision_layer = 4', 'collision_mask = 9', 'floor_constant_speed = true', 'floor_max_angle = 0.87266463', 'floor_snap_length = 12.0',
             f'script = ExtResource("script")', f'm_EnemyEntityId = {entity_id}',
             'm_Detector = NodePath("m_Detector")', 'm_Body = NodePath("m_Body")', 'm_AnimPlayer = NodePath("m_AnimPlayer")',
             'm_HurtBox = NodePath("m_HurtBox")', 'm_HitBox = NodePath("m_HitBoxRoot/m_HitBox")', 'm_HitBoxRoot = NodePath("m_HitBoxRoot")',
             *(['m_HealthBar = NodePath("m_HealthBar")'] if not is_boss else []),
             *(['m_ProjectileScene = ExtResource("projectile")'] if name == 'shrimp_demon' else []),
             *(['m_SharkWaveScene = ExtResource("shark_wave")', 'm_BubbleWarningScene = ExtResource("bubble_warning")',
                'm_BubbleScene = ExtResource("bubble")', 'm_DeathScene = ExtResource("death")'] if name == 'shark_demon_king' else []),
             *(['m_SlashScene = ExtResource("slash")', 'm_WarningScene = ExtResource("warning")',
                'm_TornadoScene = ExtResource("tornado")'] if name == 'shark_spawn' else []),
             *(['m_DragonWaveScene = ExtResource("water")', 'm_DeathScene = ExtResource("death")',
                'm_BackHit = NodePath("m_BackHit")'] if name == 'jiao_demon_king' else []),
             'm_AttackAnimations = PackedStringArray(' + ', '.join(f'"{attack}"' for attack in attacks) + ')',
             '', '[node name="m_Body" type="AnimatedSprite2D" parent="."]', 'sprite_frames = ExtResource("frames")', 'animation = &"idle"',
             '[node name="m_BodyShape" type="CollisionShape2D" parent="."]', 'shape = SubResource("body")', f'rotation = {1.57079632679 if hurt[2] > hurt[3] else 0}', f'position = {godot_vec([hurt[0] + hurt[2] / 2, hurt[1] + hurt[3] / 2])}',
             '[node name="m_HurtBox" type="Area2D" parent="." node_paths=PackedStringArray("m_Owner")]', 'collision_layer = 32', 'collision_mask = 0', 'monitoring = false', 'script = ExtResource("hurt")', 'm_Owner = NodePath("..")',
             '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HurtBox"]', 'shape = SubResource("hurt_shape")', f'position = {godot_vec([hurt[0] + hurt[2] / 2, hurt[1] + hurt[3] / 2])}',
             '[node name="m_HitBoxRoot" type="Node2D" parent="."]', '[node name="m_HitBox" type="Area2D" parent="m_HitBoxRoot"]', 'collision_layer = 64', 'collision_mask = 128', 'monitorable = false',
             '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HitBoxRoot/m_HitBox"]', 'shape = SubResource("hit_shape")', 'disabled = true',
             '[node name="m_Detector" type="Area2D" parent="."]', 'collision_layer = 4096', 'collision_mask = 2', 'monitorable = false', '[node name="CollisionShape2D" type="CollisionShape2D" parent="m_Detector"]', 'shape = SubResource("detector")',
             '[node name="m_AnimPlayer" type="AnimationPlayer" parent="."]', 'libraries/ = ExtResource("library")', '']
    if not is_boss:
        lines += ['[node name="m_HealthBar" parent="." instance=ExtResource("health")]',
                  f'offset_left = {hurt[0] + hurt[2] / 2 - 25:.6f}',
                  f'offset_right = {hurt[0] + hurt[2] / 2 + 25:.6f}',
                  f'offset_top = {hurt[1] - 13:.6f}', f'offset_bottom = {hurt[1] - 8:.6f}']
    if name == 'jiao_demon_king':
        lines += ['[node name="m_BackHit" type="AnimatedSprite2D" parent="."]',
                  'visible = false', 'sprite_frames = ExtResource("back_hit")',
                  'animation = &"play"', 'offset = Vector2(-1, -1)']
    entity_path.write_text('\n'.join(lines), encoding='utf-8')


def import_shark_effect(effect):
    """Give each warning and damaging effect its own native clock and pool scene."""
    name = effect['name']
    relative = 'Bosses/zm1_09_shark_whirlpool' if name.startswith('shark_king_') else 'Monsters/Sea/shark_spawn'
    layer = effect['layers'][0]
    artwork = PROJECT / 'TheGame/Sprites/Characters/Enemies' / relative
    artwork.mkdir(parents=True, exist_ok=True)
    atlas = artwork / f'{name}.png'
    atlas.write_bytes(Path(layer['output']).read_bytes())
    atlas_uri = f'res://{atlas.relative_to(PROJECT).as_posix()}'
    frames = artwork / f'{name}_frames.tres'
    frames.write_text(sprite_frames_resource(atlas_uri, layer), encoding='utf-8')
    action = effect['actions'][0]
    # These source frame scripts destroy their bullet before all nested artwork
    # would have finished. Preserve destruction timing rather than atlas length.
    terminal = {'shark_spawn_slash': 44, 'shark_spawn_tornado': 17,
                'shark_king_shark_wave': 51, 'shark_king_bubble': 12}.get(name, action['frames'])
    count = int(terminal)
    warning = name.endswith('warning')
    boxes = []
    for frame in range(1, count + 1):
        with Image.open(Path(action['input']) / f'{frame}.png') as image:
            box = image.convert('RGBA').getchannel('A').point(lambda value: 255 if value >= 26 else 0).getbbox()
        origin = action['bounds'][:2]
        boxes.append([box[0] + origin[0], box[1] + origin[1], box[2] - box[0], box[3] - box[1]]
                     if box and not warning and frame < count else None)
    shapes = {}
    for box in boxes:
        if box:
            shapes.setdefault(tuple(round(value, 6) for value in box[2:]), f'phase_{len(shapes)}')
    lines = ['[gd_resource type="AnimationLibrary" format=3]',
             '[sub_resource type="RectangleShape2D" id="empty"]', 'size = Vector2(1, 1)']
    for size, identifier in shapes.items():
        lines += [f'[sub_resource type="RectangleShape2D" id="{identifier}"]', f'size = {godot_vec(size)}']
    times = [frame / FPS for frame in range(count)]
    values = [f'SubResource("{shapes[tuple(round(value, 6) for value in box[2:])]}")'
              if box else 'SubResource("empty")' for box in boxes]
    positions = [[box[0] + box[2] / 2, box[1] + box[3] / 2] if box else [0, 0] for box in boxes]
    lines += ['[sub_resource type="Animation" id="play"]', f'length = {count / FPS}',
              track_value(0, 'Origin/Visual/Sprite:animation', [0], ['&"play"']),
              track_value(1, 'Origin/Visual/Sprite:offset', [0], [layer['sprite_offset']], 'vec2'),
              track_value(2, 'Origin/Visual/Sprite:frame', times, list(range(count)), 'int'),
              track_value(3, 'Origin/Visual/HitBox/CollisionShape2D:disabled',
                          *compact_keys(times, [box is None for box in boxes]), 'bool'),
              track_value(4, 'Origin/Visual/HitBox/CollisionShape2D:position',
                          *compact_keys(times, positions), 'vec2'),
              track_value(5, 'Origin/Visual/HitBox/CollisionShape2D:shape', *compact_keys(times, values))]
    methods = [(count - 1, 'FinishAnimationAnimEvent')]
    if name == 'shark_spawn_slash':
        methods = [(12, 'NewAttackAnimEvent'), (20, 'NewAttackAnimEvent'),
                   (30, 'NewAttackAnimEvent'), *methods]
    lines += ['tracks/6/type = "method"', 'tracks/6/path = NodePath(".")',
              'tracks/6/keys = {"times": PackedFloat32Array(' + ', '.join(str(frame / FPS) for frame, _ in methods) + '),',
              '"transitions": PackedFloat32Array(' + ', '.join('1' for _ in methods) + '),',
              '"values": [' + ', '.join('{"args": [], "method": &"%s"}' % method for _, method in methods) + ']}',
              '[resource]', '_data = {&"play": SubResource("play")}']
    library = PROJECT / 'TheGame/Entitys/Animations/Enemies' / relative / f'{name}_library.tres'
    library.parent.mkdir(parents=True, exist_ok=True)
    library.write_text('\n'.join(lines), encoding='utf-8')
    scene = PROJECT / 'TheGame/Entitys/Enemies' / relative / f'{name}_effect.tscn'
    scene.parent.mkdir(parents=True, exist_ok=True)
    scene.write_text(effect_scene(atlas_uri, f'res://{frames.relative_to(PROJECT).as_posix()}',
                                  f'res://{library.relative_to(PROJECT).as_posix()}', name), encoding='utf-8')


def import_jiao_effect(effect):
    """Install Jiao's own visual and a native simple rectangle per visible phase."""
    name = effect['name']
    layer = effect['layers'][0]
    directory = PROJECT / 'TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea'
    directory.mkdir(parents=True, exist_ok=True)
    atlas = directory / f'{name}.png'
    atlas.write_bytes(Path(layer['output']).read_bytes())
    uri = f'res://{atlas.relative_to(PROJECT).as_posix()}'
    frames = directory / f'{name}_frames.tres'
    frames.write_text(sprite_frames_resource(uri, layer), encoding='utf-8')
    if name == 'jiao_back_hit':
        return
    boxes = []
    for frame in range(1, effect['actions'][0]['frames'] + 1):
        image = Image.open(Path(effect['actions'][0]['input']) / f'{frame}.png').convert('RGBA')
        mask = image.getchannel('A').point(lambda value: 255 if value >= 26 else 0)
        box = mask.getbbox()
        origin = effect['actions'][0]['bounds'][:2]
        boxes.append([box[0] + origin[0], box[1] + origin[1], box[2] - box[0], box[3] - box[1]] if box else None)
    # The Flash effect uses its complete visual for complex-hit testing. Keep one
    # readable native rectangle; no alpha polygons or cloud-sized fixed envelope.
    shapes = {}
    resources = []
    for box in boxes:
        if box is None:
            continue
        size = tuple(round(value, 5) for value in box[2:])
        if size not in shapes:
            identifier = f'phase_{len(shapes)}'
            shapes[size] = identifier
            resources += [f'[sub_resource type="RectangleShape2D" id="{identifier}"]', f'size = {godot_vec(size)}']
    times = [frame / FPS for frame in range(len(boxes))]
    values = [f'SubResource("{shapes[tuple(round(v, 5) for v in box[2:])]}")' if box else 'SubResource("empty")' for box in boxes]
    positions = [[box[0] + box[2] / 2, box[1] + box[3] / 2] if box else [0, 0] for box in boxes]
    lines = ['[gd_resource type="AnimationLibrary" format=3]',
             '[sub_resource type="RectangleShape2D" id="empty"]', 'size = Vector2(1, 1)', *resources,
             '[sub_resource type="Animation" id="play"]', f'length = {len(boxes) / FPS}',
             track_value(0, 'Origin/Visual/Sprite:animation', [0], ['&"play"']),
             track_value(1, 'Origin/Visual/Sprite:offset', [0], [layer['sprite_offset']], 'vec2'),
             track_value(2, 'Origin/Visual/Sprite:frame', times, list(range(len(boxes))), 'int'),
             track_value(3, 'Origin/Visual/HitBox/CollisionShape2D:disabled', *compact_keys(times, [box is None for box in boxes]), 'bool'),
             track_value(4, 'Origin/Visual/HitBox/CollisionShape2D:position', *compact_keys(times, positions), 'vec2'),
             track_value(5, 'Origin/Visual/HitBox/CollisionShape2D:shape', *compact_keys(times, values)),
             '[resource]', '_data = {&"play": SubResource("play")}']
    libraries = PROJECT / 'TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea'
    libraries.mkdir(parents=True, exist_ok=True)
    library = libraries / 'jiao_dragon_wave_library.tres'
    library.write_text('\n'.join(lines), encoding='utf-8')
    scene = PROJECT / 'TheGame/Entitys/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_effect.tscn'
    scene.parent.mkdir(parents=True, exist_ok=True)
    scene.write_text(effect_scene(uri, f'res://{frames.relative_to(PROJECT).as_posix()}',
        f'res://{library.relative_to(PROJECT).as_posix()}', name), encoding='utf-8')


def main(args):
    data = json.loads(args.manifest.read_text(encoding='utf-8'))
    for actor in data['actors']:
        atlas_path, layer = pack_actor(actor, args.manifest.parent)
        actor['_layer'] = layer
        frames_path = frames_resource(actor['name'], atlas_path, actor)
        directory = ACTORS[actor['name']][2]
        library_path = PROJECT / f'TheGame/Entitys/Animations/Enemies/{directory}/{actor["name"]}_library.tres'
        library_path.parent.mkdir(parents=True, exist_ok=True)
        animation_resource(actor, library_path, layer)
        scene_for(actor, atlas_path, frames_path, library_path)
    for effect in data.get('effects', []):
        if effect['name'].startswith(('shark_spawn_', 'shark_king_')):
            import_shark_effect(effect)
            continue
        if effect['name'].startswith('jiao_'):
            import_jiao_effect(effect)
            continue
        layer = effect['layers'][0]
        output = PROJECT / 'TheGame/Sprites/Effects/Enemies/sea/shrimp_projectile.png'
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(Path(layer['output']).read_bytes())
        bounds = effect['hit_bounds']
        scene = PROJECT / 'TheGame/Entitys/Enemies/Monsters/Sea/shrimp_demon/shrimp_projectile_effect.tscn'
        scene.write_text('\n'.join([
            '[gd_scene format=3]',
            '[ext_resource type="Texture2D" path="res://TheGame/Sprites/Effects/Enemies/sea/shrimp_projectile.png" id="texture"]',
            '[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Enemies/Monsters/Sea/shrimp_demon/ShrimpProjectileEffectNode.cs" id="script"]',
            '[sub_resource type="Animation" id="play"]', 'length = 1.0', 'loop_mode = 1',
            'tracks/0/type = "value"', 'tracks/0/path = NodePath("HitBox/CollisionShape2D:disabled")',
            'tracks/0/keys = {"times": PackedFloat32Array(0), "transitions": PackedFloat32Array(1), "update": 1, "values": [false]}',
            '[sub_resource type="AnimationLibrary" id="library"]', '_data = {&"play": SubResource("play")}',
            '[sub_resource type="RectangleShape2D" id="shape"]', f'size = {godot_vec(bounds[2:])}',
            '[node name="ShrimpProjectileEffect" type="Node2D" node_paths=PackedStringArray("m_Player", "m_HitBox", "m_CancelArea")]',
            'script = ExtResource("script")', 'm_Player = NodePath("AnimationPlayer")',
            'm_HitBox = NodePath("HitBox")',
            'm_CancelArea = NodePath("CancelArea")',
            '[node name="Sprite" type="Sprite2D" parent="."]', 'texture = ExtResource("texture")',
            f'offset = {godot_vec(layer["sprite_offset"])}',
            '[node name="HitBox" type="Area2D" parent="."]', 'collision_layer = 64', 'collision_mask = 128', 'monitorable = false',
            '[node name="CollisionShape2D" type="CollisionShape2D" parent="HitBox"]',
            f'position = {godot_vec([bounds[0]+bounds[2]/2,bounds[1]+bounds[3]/2])}', 'shape = SubResource("shape")', 'disabled = true',
            '[node name="CancelArea" type="Area2D" parent="."]', 'collision_layer = 0', 'collision_mask = 16', 'monitorable = false',
            '[node name="CollisionShape2D" type="CollisionShape2D" parent="CancelArea"]',
            f'position = {godot_vec([bounds[0]+bounds[2]/2,bounds[1]+bounds[3]/2])}', 'shape = SubResource("shape")',
            '[node name="AnimationPlayer" type="AnimationPlayer" parent="."]', 'libraries/ = SubResource("library")', '']), encoding='utf-8')
    print(f'SEA MONSTER IMPORT PASS: {len(data["actors"])} actors, native frames/scenes/libraries')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', type=Path, required=True)
    main(parser.parse_args())
