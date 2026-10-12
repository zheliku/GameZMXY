"""Import Gorilla and the clear marker from read-only legacy assets.

Only missing outputs are written. Animation tracks contain values, never methods.
Run with the same Python environment as gen_animations.py.
"""
from pathlib import Path
import re
import shutil
import hashlib
import gen_animations as anim

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'Godot/GodotProject/TheGame'
OLD = Path(anim.LEGACY)


def import_gorilla():
    """Use the original sheet regions and body timing, with a readable punch windup."""
    source = (OLD / 'Scene/Monster/Monster_3.tscn').read_text(encoding='utf-8')
    sheets = {'idle': ('Wait', 5, 1 / 9, (-8.5, 0)),
              'run': ('Walk', 4, .2, (3, 2)),
              'attack_1': ('Hit', 4, 1 / 15, (-35, -17)),
              'hurt': ('Hurt', 1, .28, (0, -10)),
              'death': ('Death', 4, 1 / 12, (-14, 4))}
    folder = GAME / 'Sprites/Characters/Monsters/gorilla'
    folder.mkdir(parents=True, exist_ok=True)
    sprite_anims, library_anims, shapes = [], [], {}
    for name, (old_name, count, duration, offset) in sheets.items():
        old_texture = f'Art/Monster/Monster3/{old_name}.png'
        destination = folder / f'gorilla_{name}.png'
        if not destination.exists():
            shutil.copyfile(OLD / old_texture, destination)
        texture_id = re.search(r'path="res://' + re.escape(old_texture) + r'" id="(\d+)"', source)[1]
        rectangle = re.search(r'atlas = ExtResource\( "' + texture_id + r'" \)\s*region = Rect2\(([^)]+)\)', source)[1]
        _, _, width, height = map(float, rectangle.split(','))
        frames = [(i, duration) for i in range(count)]
        if name == 'attack_1':
            frames[0] = (0, .25)  # Raising the fist is visible before the impact frame.
            frames[-1] = (count - 1, .25)
        elif name == 'death':
            frames[-1] = (count - 1, .5)
        sprite_anims.append(dict(name=name, loop=name in ('idle', 'run'),
                                 tex='res://' + destination.relative_to(GAME.parent).as_posix(),
                                 fw=int(width), fh=int(height), cols=count, frames=frames))
        keys, length = anim._frame_track_keys(frames)
        hit_keys = [(0, True), (.383333, False), (length, True)] if name == 'attack_1' else [(0, True)]
        tracks = [(anim.MONKEY_SPRITE_FRAMES, 'string', [(0, name)]),
                  (anim.MONKEY_BODY + ':frame', 'int', keys),
                  (anim.MONKEY_BODY + ':offset', 'vector2', [(0, offset)]),
                  (anim.HITBOX_TRACK, 'bool', hit_keys)]
        geometry = {'size': (144.75, 39), 'pos': (-63.375, -4)}
        tracks.extend(anim._hitbox_geo_tracks(name, tracks[-1], {'attack_1': geometry}, geometry, shapes))
        library_anims.append(dict(name=name, loop=name in ('idle', 'run'), length=length, tracks=tracks))
    # The editor reset uses the same complete safe track set as a resting pose.
    reset = dict(library_anims[0], name='RESET', length=.001, loop=False)
    reset['tracks'] = [(path, kind, [values[0]]) for path, kind, values in reset['tracks']]
    library_anims.append(reset)
    anim.emit_spriteframes(str(folder / 'gorilla_animations.tres'), sprite_anims)
    anim.emit_anim_library(str(GAME / 'Entitys/Animations/gorilla_anim_library.tres'), library_anims, shapes)


def import_exit():
    """Preserve the legacy clear marker's eleven-frame loop and sheet order."""
    folder = GAME / 'Sprites/Effects/level_exit'
    folder.mkdir(parents=True, exist_ok=True)
    destination = folder / 'level_exit_sheet.png'
    if not destination.exists():
        shutil.copyfile(OLD / 'Art/Level/Export.png', destination)
    anim.emit_spriteframes(str(folder / 'level_exit_frames.tres'), [dict(
        name='exit', loop=True, tex='res://TheGame/Sprites/Effects/level_exit/level_exit_sheet.png',
        fw=206, fh=186, cols=9, frames=[(i, 1) for i in (5, 6, 7, 8, 9, 10, 0, 1, 2, 3, 4)], speed=25)])


def import_boss_name_font():
    """Copy the exact font referenced by the legacy BossBlood name label."""
    source = OLD / 'Font/华康海报体W12 &amp; 华康海报体W12(P).ttc'
    digest = 'b47e00483c38ec74c63601c9200faadb27809374a6a0b720c2f029723342fe3d'
    if hashlib.sha256(source.read_bytes()).hexdigest() != digest:
        raise ValueError('Unexpected legacy boss font; check the source recorded in LegacyAssetMap')
    destination = GAME / 'MainPack/Fonts/df_poster_w12.ttc'
    if not destination.exists():
        shutil.copyfile(source, destination)


if __name__ == '__main__':
    import_gorilla()
    import_exit()
    import_boss_name_font()
