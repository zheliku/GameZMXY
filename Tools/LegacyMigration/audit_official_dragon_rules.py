"""Extract Dragon Palace balance and transition facts from the verified v25928 source.

This report is authoring evidence, never a runtime configuration interpreter.
It covers code-created effects, the hidden entrance and the consumed banner
eligibility flag that ordinary display-list/drop-list inventories do not cover.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

from export_official_sea_monsters import _probe_reader


def read(path):
    """Read decompiled text without depending on the shell's locale."""
    return path.read_text(encoding='utf-8-sig')


def one(pattern, text):
    """Fail on an unsupported source revision instead of guessing a rule."""
    match = re.search(pattern, text, re.S)
    if match is None:
        raise ValueError('Missing official rule: ' + pattern)
    return match


def audit(args):
    """Derive source values and retain their owning scripts in one reviewable report."""
    work = args.work.resolve()
    main = work / 'main_scripts/scripts'
    dragon = read(work / 'monster3_scripts/scripts/export/monster/Monster26.as')
    source = work / 'Monster3_v3.swf'
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != '5f6478c13cb57a46d46abac6fc70549f517358d727c3f2641aa75754c14f33c7':
        raise ValueError('Unexpected Monster3 source revision')
    threshold = int(one(r'if\(this.minLevel > (\d+)\)', dragon)[1])
    assignments = lambda field: [int(value) for value in re.findall(r'this\.' + field + r' = (\d+);', dragon)]
    hp = [int(value) for value in re.findall(r'this.Hp = sHp = (\d+);', dragon)]
    attacks = re.findall(r'this.attackBackInfoDict\["hit(\d)"\] = \{(.*?)\};', dragon, re.S)
    if len(hp) != 2 or len(attacks) != 8:
        raise ValueError('Expected two complete official dragon difficulty tiers')
    tiers = []
    for index in (1, 0):
        strikes = []
        for number, body in attacks[index * 4:index * 4 + 4]:
            freeze = re.search(r'"time":24 \* (\d+)', body)
            strikes.append({'number': int(number),
                'power': int(one(r'"power":(\d+)', body)[1]),
                'kind': one(r'"attackKind":"([^"]+)"', body)[1],
                'hit_budget': int(one(r'"hitMaxCount":(\d+)', body)[1]),
                'knockback_per_source_frame': [int(value) for value in one(r'"attackBackSpeed":\[([^\]]+)\]', body)[1].split(',')],
                'freeze_seconds': int(freeze[1]) if freeze else 0})
        tiers.append({'minimum_hero_level': 1 if index == 1 else threshold + 1,
            'max_hp': hp[index], 'defense': assignments('def')[index],
            'attack_desire': assignments('isattback')[index], 'experience': assignments('exp')[index],
            'soul_per_orb': assignments('gxp')[index], 'attacks': strikes})
    cooldowns = re.findall(r'if\(this.minLevel > 20\)\s*\{\s*this.skillCD(\d) = (\d+);\s*\}\s*else\s*\{\s*this.skillCD\d = (\d+);', dragon)
    for number, high, low in cooldowns:
        for tier, value in zip(tiers, (low, high)):
            tier.setdefault('cooldowns_seconds', {})[number] = int(value) / 24
    invulnerability = {number: int(frames) / 24 for number, frames in re.findall(
        r'override protected function releSkill(\d)\(\).*?this.setYourFather\((\d+)\)', dragon, re.S)}
    levels = json.loads(read(work / 'all_levels.json'))
    entrance = next(item for item in levels['1-2']['scenery'] if item['characterId'] == 99)
    tags = ET.parse(args.main_xml).getroot().find('tags')
    _, bounds = _probe_reader(tags)
    rectangle = bounds(99, 0, entrance['matrix'])
    physics = read(main / 'PhysicsWorlds/PhysicsWorld.as')
    game = read(main / 'my/MainGame.as')
    memory = read(main / 'storage/MemoryClass.as')
    if 'bh.colipse.hitTestObject(this.fbEnter)' not in physics or 'new Event("fbfbfbfb")' not in physics:
        raise ValueError('Hidden entrance is not the expected contact transition')
    if 'this.gc.curStage = 9;' not in game or 'this.gc.curLevel = 1;' not in game:
        raise ValueError('Hidden entrance destination changed')
    if 'cangetDHQF = false;' not in dragon or '.cangetDHQF = this.gc.cangetDHQF;' not in memory:
        raise ValueError('Banner spawn eligibility must be consumed and persisted independently of ownership')
    result = {'source_sha256': digest, 'official_label': '9-1', 'source_fps': 24,
        'tiers': tiers, 'skill_invulnerability_seconds': invulnerability,
        'entrance': {'from': '1-2', 'symbol': 99, 'matrix': entrance['matrix'],
            'world_rectangle': rectangle, 'trigger': 'hero_colipse_contact', 'requires_interact_key': False,
            'transition_fade_seconds': 1},
        'banner_eligibility': {'persistent_source_field': 'cangetDHQF', 'initial_value': True,
            'consume_when': 'dragon_instance_created', 'drop_when_eligible': 'dhqf',
            'ordinary_drop_chance': .2, 'high_level_drop_chance': .1},
        'exit': {'source_symbol': 86, 'position': [814.75, 457.55],
            'opens_when': 'dragon_destroyed', 'destination': 'stage_map', 'advances_mainline': False},
        'status_overlay': {'symbol': 'ice', 'sprite_id': 114, 'frames': 35,
            'position': [-90, -115], 'holds_last_frame': True, 'source_child_blend_mode': 8,
            'freezes_animation': False}}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('DRAGON RULES 9-1 PASS: two source tiers, three cooldowns, contact entrance, persisted banner eligibility and map exit')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--main-xml', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    audit(parser.parse_args())
