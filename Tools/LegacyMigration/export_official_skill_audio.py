"""Extract original hero sounds from the official Music.swf without reencoding."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess

SOURCE_SHA256 = '5cad2f1ef3ce592c2e9e44c631748e042d8cacae2c7571e57b1a0546998a7578'
SOUND_SETS = {'wukong': {
    6: 'wukong_rising_dragon', 8: 'wukong_flame_thrust', 11: 'wukong_blazing_flash',
    1: 'wukong_flame_storm', 13: 'wukong_fire_demon_start',
    14: 'wukong_fire_demon_combo', 15: 'wukong_fire_demon_landing',
}, 'tangseng': {
    23: 'tangseng_attack', 24: 'skills/tangseng_ice_dragon',
    25: 'skills/tangseng_dark_ice', 20: 'skills/tangseng_water_clone',
    22: 'skills/tangseng_heavenly_dew', 5: 'skills/tangseng_water_demon',
    4: 'tangseng_hit_impact', 27: 'tangseng_hurt',
}}


def export(args):
    """Check source provenance and copy native MP3 payloads without reencoding."""
    content = args.source.read_bytes()
    if content[:3] not in (b'FWS', b'CWS', b'ZWS'):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != SOURCE_SHA256:
        raise ValueError('Unexpected Music.swf revision')
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'profile').mkdir(exist_ok=True)
    source = work / 'music.swf'
    source.write_bytes(content)
    sounds = SOUND_SETS[args.hero]
    command = [str(args.java), f'-Duser.home={work}', '-jar', str(args.ffdec),
               '-selectid', ','.join(map(str, sounds)), '-export', 'sound',
               str(work / 'sounds'), str(source)]
    with (work / 'export.log').open('w', encoding='utf-8') as log:
        subprocess.run(command, env=dict(os.environ, APPDATA=str(work / 'profile')),
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    args.output.mkdir(parents=True, exist_ok=True)
    for identifier, name in sounds.items():
        matches = list((work / 'sounds').glob(f'{identifier}_*.mp3'))
        if len(matches) != 1:
            raise ValueError(f'Expected one native MP3 for sound {identifier}')
        target = args.output / f'{name}.mp3'
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(matches[0], target)
    print(f'Exported {len(sounds)} original {args.hero} MP3s')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'work', 'java', 'ffdec', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    parser.add_argument('--hero', choices=SOUND_SETS, default='wukong')
    export(parser.parse_args())
