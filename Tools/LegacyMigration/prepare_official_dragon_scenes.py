"""Author the Dragon King's native scene and effect clocks from verified SWF data.

This is a one-time authoring path, not a runtime animation interpreter. The
source uses full-display pixel tests for both remote attacks. The wave follows
its source rectangle; the torrent uses its original 192px clip, never the
1536px bitmap/filter canvas surrounding that clip.
"""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as E

from import_garuda_actor_assets import effect_scene, fmt_float, track_value, vec2, write_text
from prepare_official_garuda_level import _probe_reader

PROJECT = Path(__file__).resolve().parents[2] / 'Godot/GodotProject'
DIRECTORY = 'Bosses/zm1_11_hidden_dragon_palace'
ART = 'res://TheGame/Sprites/Characters/Enemies/' + DIRECTORY + '/'
ANIM = 'res://TheGame/Entitys/Animations/Enemies/' + DIRECTORY + '/'
SCENE = 'res://TheGame/Entitys/Enemies/' + DIRECTORY + '/'


def effect_library(record, bounds):
    """Compress equal source rectangle keys, keeping one native hit shape."""
    shape_ids = {}
    lines = ['[gd_resource type="AnimationLibrary" format=3]']
    for box in bounds:
        if box is None:
            continue
        size = (round(box[2], 5), round(box[3], 5))
        if size not in shape_ids:
            identifier = f'shape_{len(shape_ids)}'
            shape_ids[size] = identifier
            lines.extend([f'[sub_resource type="RectangleShape2D" id="{identifier}"]', f'size = {vec2(size)}'])
    lines.extend(['[sub_resource type="Animation" id="play"]', f'length = {fmt_float(len(bounds)/24)}',
        track_value(0,'Origin/Visual/Sprite:animation',[0],['&"play"']),
        track_value(1,'Origin/Visual/Sprite:offset',[0],[record['layers'][0]['sprite_offset']],'vec2'),
        track_value(2,'Origin/Visual/Sprite:frame',[i/24 for i in range(len(bounds))],list(range(len(bounds))),'int')])
    changed = [i for i, box in enumerate(bounds) if i == 0 or box != bounds[i-1]]
    times = [i/24 for i in changed]
    boxes = [bounds[i] for i in changed]
    path = 'Origin/Visual/HitBox/CollisionShape2D:'
    lines.extend([track_value(3,path+'disabled',times,[box is None for box in boxes],'bool'),
        track_value(4,path+'position',times,[[box[0]+box[2]/2,box[1]+box[3]/2] if box else [0,0] for box in boxes],'vec2'),
        track_value(5,path+'shape',times,[f'SubResource("{shape_ids[(round(box[2],5),round(box[3],5))]}")'
            if box else f'SubResource("{next(iter(shape_ids.values()))}")' for box in boxes]),
        '[resource]','_data = { &"play": SubResource("play") }',''])
    return '\n'.join(lines)


def actor_scene(actor):
    """Assemble two independent source layers with strongly typed exported bindings."""
    x,y,width,height = actor['hurt_bounds']
    offset = actor['layers'][0]['sprite_offset']
    return f'''[gd_scene format=3]
[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Enemies/{DIRECTORY}/DragonKingEntity.cs" id="script"]
[ext_resource type="Script" path="res://TheGame/GameScripts/Entity/Combat/HurtBoxNode.cs" id="hurt"]
[ext_resource type="SpriteFrames" path="{ART}dragon_king_official_frames.tres" id="body"]
[ext_resource type="SpriteFrames" path="{ART}dragon_king_ice_aura_frames.tres" id="ice"]
[ext_resource type="AnimationLibrary" path="{ANIM}dragon_king_library.tres" id="anim"]
[ext_resource type="PackedScene" path="{SCENE}dragon_king_wave_effect.tscn" id="wave"]
[ext_resource type="PackedScene" path="{SCENE}dragon_king_torrent_effect.tscn" id="torrent"]
[sub_resource type="CapsuleShape2D" id="body_shape"]
radius = {fmt_float(width/2)}
height = {fmt_float(height)}
[sub_resource type="RectangleShape2D" id="hurt_shape"]
size = {vec2([width,height])}
[sub_resource type="RectangleShape2D" id="rest"]
size = Vector2(1,1)
[sub_resource type="RectangleShape2D" id="detector"]
size = Vector2(600,600)
[sub_resource type="CanvasItemMaterial" id="add"]
blend_mode = 1
[node name="DragonKingEntity" type="CharacterBody2D" node_paths=PackedStringArray("m_Detector","m_Body","m_AnimPlayer","m_HurtBox","m_HitBox","m_HitBoxRoot")]
collision_layer = 4
collision_mask = 9
floor_constant_speed = true
floor_max_angle = 0.87266463
floor_snap_length = 12.0
script = ExtResource("script")
m_Detector = NodePath("m_Detector")
m_Body = NodePath("m_Body")
m_AnimPlayer = NodePath("m_AnimPlayer")
m_HurtBox = NodePath("m_HurtBox")
m_HitBox = NodePath("m_HitBoxRoot/m_HitBox")
m_HitBoxRoot = NodePath("m_HitBoxRoot")
m_EnemyEntityId = 26
m_AttackAnimations = PackedStringArray("attack_1","attack_2","attack_3","attack_4")
m_WaveScene = ExtResource("wave")
m_TorrentScene = ExtResource("torrent")
[node name="m_Body" type="AnimatedSprite2D" parent="."]
sprite_frames = ExtResource("body")
animation = &"idle"
offset = {vec2(offset)}
[node name="m_BodyShape" type="CollisionShape2D" parent="."]
position = {vec2([x+width/2,y+height/2])}
shape = SubResource("body_shape")
[node name="m_HurtBox" type="Area2D" parent="." node_paths=PackedStringArray("m_Owner")]
collision_layer = 32
collision_mask = 0
monitoring = false
script = ExtResource("hurt")
m_Owner = NodePath("..")
[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HurtBox"]
position = {vec2([x+width/2,y+height/2])}
shape = SubResource("hurt_shape")
[node name="m_HitBoxRoot" type="Node2D" parent="."]
[node name="m_HitBox" type="Area2D" parent="m_HitBoxRoot"]
collision_layer = 64
collision_mask = 128
monitorable = false
[node name="CollisionShape2D" type="CollisionShape2D" parent="m_HitBoxRoot/m_HitBox"]
shape = SubResource("rest")
disabled = true
[node name="m_IceAura" type="AnimatedSprite2D" parent="m_HitBoxRoot"]
visible = false
sprite_frames = ExtResource("ice")
animation = &"play"
offset = Vector2(1,-4)
material = SubResource("add")
[node name="m_Detector" type="Area2D" parent="."]
collision_layer = 4096
collision_mask = 2
monitorable = false
[node name="CollisionShape2D" type="CollisionShape2D" parent="m_Detector"]
shape = SubResource("detector")
[node name="m_AnimPlayer" type="AnimationPlayer" parent="."]
callback_mode_method = 1
libraries/ = ExtResource("anim")
'''


def main(args):
    """Create only new Dragon Palace scene resources; existing edits are protected."""
    source = json.loads(args.manifest.read_text(encoding='utf-8'))
    tags = E.parse(args.xml).getroot().find('tags')
    frames, bounds = _probe_reader(tags)
    records = {item['name']:item for item in source['effects']}
    clipped = json.loads(args.geometry.read_text(encoding='utf-8')) if args.geometry else None
    report = {}
    for name in ('dragon_king_wave','dragon_king_torrent'):
        record = records[name]
        sid = record['sprite']
        geometry = []
        for i, display in enumerate(frames(sid)):
            if name.endswith('wave'):
                box = bounds(sid,i,[1,0,0,1,0,0]) if i < 40 else None
            else:
                # Source sprite23 is the original fixed 192x192 clipping window.
                clips = [(child,age) for child,age in display if child.get('clipDepth')]
                if len(clips)>1:
                    raise ValueError('Review changed torrent mask hierarchy')
                box = bounds(int(clips[0][0].get('characterId')),0,[1,0,0,1,0,0]) if clips and i<67 else None
                if box and (box[2:] != [193,193] or box[:2] != [-.5,-.5]):
                    raise ValueError(f'Unexpected official torrent clip: {box}')
            geometry.append(box)
        if clipped:
            geometry = clipped['effects'][name]
        report[name]=geometry
        library = PROJECT / 'TheGame/Entitys/Animations/Enemies' / DIRECTORY / (name+'_library.tres')
        write_text(library,effect_library(record,geometry), overwrite=args.update_effects)
        text = effect_scene('',ART+name+'_frames.tres',ANIM+name+'_library.tres',name)
        if record['blend_mode']=='add':
            text=text.replace('[sub_resource type="RectangleShape2D" id="rest"]',
                '[sub_resource type="CanvasItemMaterial" id="add"]\nblend_mode = 1\n[sub_resource type="RectangleShape2D" id="rest"]')
            text=text.replace('sprite_frames = ExtResource("frames")','material = SubResource("add")\nsprite_frames = ExtResource("frames")')
        scene = PROJECT/'TheGame/Entitys/Enemies'/DIRECTORY/(name+'_effect.tscn')
        if not (args.update_effects and scene.exists()):
            write_text(scene,text)
    scene = PROJECT/'TheGame/Entitys/Enemies'/DIRECTORY/'dragon_king_entity.tscn'
    if not (args.update_effects and scene.exists()):
        write_text(scene,actor_scene(source['actors'][0]))
    args.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('DRAGON SCENES AUTHOR PASS: original wave trajectory, fixed torrent clip, two owned effects and additive ice')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ('manifest','xml','output'):
        parser.add_argument('--'+key,type=Path,required=True)
    parser.add_argument('--geometry',type=Path)
    parser.add_argument('--update-effects',action='store_true')
    main(parser.parse_args())
