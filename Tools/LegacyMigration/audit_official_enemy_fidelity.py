"""Audit every enemy/configuration column against the verified official source.

Read-only authoring evidence: unmatched extension fields remain visible instead
of being silently advertised as official. Conditional species values are marked
for their concrete runtime overrides, not folded into generic configuration.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

from audit_official_level_dependencies import xlsx_rows, SOURCE_HASHES

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'Godot/GodotProject/.godot/validation/remaining_levels'


def sheet_records(path):
    """Retain all source columns, including the nested attribute leaf headers."""
    for sheet, rows in xlsx_rows(path).items():
        names = {}
        for row in rows[:2]:
            for cell, value in row.items():
                if value and not str(value).startswith('##'):
                    names[re.sub(r'\d+', '', cell)] = value
        for row in rows:
            if any(str(v or '').startswith('##') for k, v in row.items() if re.fullmatch(r'A\d+', k)):
                continue
            identity = next((v for k, v in row.items() if re.fullmatch(r'B\d+', k)), None)
            if not identity:
                continue
            yield sheet, {names[re.sub(r'\d+', '', c)]: v for c, v in row.items() if re.sub(r'\d+', '', c) in names}, {
                names[re.sub(r'\d+', '', c)]: c for c in row if re.sub(r'\d+', '', c) in names}


def audit(args):
    """Join every current enemy with its exact source species and numeric rules."""
    inventory = json.loads((WORK / 'dependency_inventory.json').read_text(encoding='utf-8'))
    sources = {}
    for entry in inventory['sources']:
        if entry['name'] not in SOURCE_HASHES:
            continue
        path = ROOT / entry['swf']
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != SOURCE_HASHES[entry['name']]:
            raise ValueError(f'Source version mismatch: {path}')
        sources[entry['name']] = actual
    actors = {int(a['id']): a for a in inventory['actors']}
    mapped = {'MaxHp': ('Hp', 1), 'Def': ('def', 1), 'MoveSpeed': ('horizenSpeed', 24),
              'AttackDesire': ('isattback', 1), 'AddExp': ('exp', 1),
              'SoulPerOrb': ('gxp', 1)}
    enemies, differences, edits = [], [], []
    for sheet, row, cells in sheet_records(ROOT/'Configs/GameConfig/Datas/Enemy.xlsx'):
        legacy = int(row['LegacyId'])
        record = actors[legacy]
        text = (ROOT/record['source_script']).read_text(encoding='utf-8-sig')
        fields = {}
        for column, value in row.items():
            status = 'project field: identity, extension or shared policy; not an official scalar'
            official = None
            if column in mapped:
                field, scale = mapped[column]
                expressions = re.findall(r'this\.'+field+r'\s*=\s*(?:sHp\s*=\s*)?(-?[\d.]+)\s*;', text)
                if expressions:
                    candidates = [float(x)*scale for x in expressions]
                    if legacy in (22, 23) and column == 'MaxHp':
                        # BaseMonster.__added overrides these constructor values.
                        candidates = [20000.0 if legacy == 22 else 40000.0]
                    if legacy == 24 and column == 'MoveSpeed':
                        # This final form never enters ordinary movement; its hands move independently.
                        candidates = [0.0]
                    official = candidates
                    status = 'matches source constructor/conditional tier' if float(value) in candidates else 'DIFF'
                    if status == 'DIFF':
                        differences.append({'enemy': legacy, 'column': column, 'current': value, 'source': candidates})
            if column == 'Mdef':
                status = 'BaseMonster.getRealHurt magic branch has no base magic defense'
                if float(value) != 0:
                    differences.append({'enemy': legacy, 'column': column, 'current': value, 'source': [0]})
            if column in ('MaxMp', 'Power', 'Crit', 'Miss', 'Lucky', 'Toughness', 'Htarget', 'CritReduce',
                          'Ar', 'Sp', 'Vampirism', 'HpRegen', 'MpRegen'):
                status = 'shared attribute extension; source enemy has no independent value, damage is per attack'
                if float(value) != 0:
                    differences.append({'enemy': legacy, 'column': column, 'current': value, 'source': [0]})
            if column == 'Gravity':
                status = 'BaseObject.graity 1.5 * 24^2; flying/swimming or stationary body bypasses gravity'
                if float(value) not in (0, 864):
                    differences.append({'enemy': legacy, 'column': column, 'current': value, 'source': [0, 864]})
            if column in ('SoulCount', 'ManaCount', 'MusouCount', 'ManaDropChance', 'MusouDropChance'):
                expected = {'SoulCount': '2,4', 'ManaCount': '1,3', 'MusouCount': '1,3',
                            'ManaDropChance': '0.12', 'MusouDropChance': '0.12'}[column]
                status = 'BaseMonster.turnToGXP: 2..4 red orbs; each green/yellow branch 4% for 3/2/1 orbs'
                if str(value) != expected:
                    differences.append({'enemy': legacy, 'column': column, 'current': value, 'source': [expected]})
            if column == 'EquipmentDropChance':
                candidates = [float(v) for v in re.findall(r'(?:this\.)?probability\s*=\s*([\d.]+)', text)] or [.15]
                if legacy in (18, 22, 23, 24):
                    candidates = [0] # No ordinary equipment pool; final heart grants its unique reward directly.
                status = 'source fallEquip probability; concrete form/tier overrides remain in the species'
                if float(value) not in candidates:
                    differences.append({'enemy': legacy, 'column': column, 'current': value, 'source': candidates})
            if column == 'EquipmentDropPoolId':
                status = 'readable project identity for source fallList; source-specific tier/guardian selection stays in concrete code'
            if column == 'SuperArmor':
                status = 'body reaction policy; temporary source invulnerability belongs to concrete state/animation, not this flag'
            if column == 'AttackInterval':
                status = ('source count % 24 / 24fps' if legacy in (13, 14, 16, 18) else
                          'source count % 36 / 24fps' if legacy in (1, 2, 3, 7, 8, 15, 17, 20, 25) else
                          'concrete boss state owns source decision branches; this generic field is not used')
            if legacy == 13 and column == 'AttackInterval' and float(value) != 1:
                edits.append({'book':'Enemy.xlsx','sheet':sheet,'cell':cells[column],'old':float(value),'value':1,
                              'reason':'Monster13.hasAttackTarget count % 24 at 24fps'})
            fields[column] = {'current': value, 'evidence': status, 'official_candidates': official}
        match = re.search(r'this\.attackRange\s*=\s*([\d.]+)',text)
        enemies.append({'enemy_id':int(row['EnemyId']), 'legacy_id':legacy, 'name':row['NameCn'],
            'script':record['source_script'], 'sight_range':float(re.search(r'this\.mysee\s*=\s*([\d.]+)', text)[1]), 'attack_range':float(match[1]) if match else None,
            'fields':fields})
    attacks=[]
    attack_differences=[]
    for sheet,row,cells in sheet_records(ROOT/'Configs/GameConfig/Datas/Combat.xlsx'):
        if sheet != 'AttackConfig':continue
        source = re.search(r'Monster(\d+)\.hit(\d+(?:-\d+)?)(?![\w-])',str(row.get('LegacyId','')), re.I)
        entry={'id':row['Id'],'source':row.get('LegacyId'),'columns':dict(row)}
        if row['Id'] in ('2030','2031','2032'):
            # These records belong to both HP-tier body variants; the unsuffixed
            # hit2/hit3 records are already separate independently spawned effects.
            source=re.search(r'Monster(\d+)\.hit(\d+)',row['LegacyId'])
            source_label='hit'+source[2]+'-1'
        elif row['Id']=='11004':
            # Nested poison data has no independent enemy action or knockback.
            entry['evidence']='Monster11.hit3.addEffect: poison, 10 seconds, fixed 20 damage per second'
            attacks.append(entry)
            continue
        else:
            source_label='hit'+source[2] if source else None
        if source and int(source[1]) in actors:
            script=(ROOT/actors[int(source[1])]['source_script']).read_text(encoding='utf-8-sig')
            bodies=re.findall(r'attackBackInfoDict\["'+source_label+r'"\]\s*=\s*\{(.*?)\};',script,re.S)
            entry['source_definitions']=bodies
            candidates=[]
            for body in bodies:
                budget=re.search(r'"hitMaxCount":(\d+)',body)
                power=re.search(r'"power":(-?[\d.]+)',body)
                kind=re.search(r'"attackKind":"(\w+)"',body)
                knockback=re.search(r'"attackBackSpeed":\[([^\]]+)\]',body)
                if not all((budget,power,kind,knockback)):
                    continue
                expected={'HitBudget':int(budget[1]),'FlatPower':[float(power[1])]*2,
                    'DamageKind':{'physics':'物理','magic':'魔法','real':'真实'}[kind[1]],
                    'Knockback':[float(v)*24 for v in knockback[1].split(',')]}
                if row['Id']=='2042':
                    expected['DamageKind']='物理'
                    entry['evidence']='Monster24 giant hands call publicToBlood; the source magic label is not used'
                candidates.append(expected)
            entry['candidates']=candidates
            if candidates:
                for column in ('HitBudget','FlatPower','DamageKind','Knockback'):
                    current=row[column]
                    if column in ('FlatPower','Knockback'):
                        current=[float(v) for v in current.split(',')]
                    elif column=='HitBudget':current=int(current)
                    if not any(current==value[column] for value in candidates):
                        attack_differences.append({'id':row['Id'],'column':column,'current':current,
                            'source':[value[column] for value in candidates]})
        attacks.append(entry)
    report={'source_hashes':sources,'enemies':enemies,'attacks':attacks,'scalar_differences':differences,
        'attack_differences':attack_differences,'cell_edits':edits}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('ENEMY SOURCE AUDIT:',len(enemies),'species,',sum(len(e['fields']) for e in enemies),'enemy cells,',
          len(attacks),'attacks,',len(differences),'scalar differences,',len(edits),'identified cell corrections')
    for item in differences: print('SOURCE DIFF',item)
    for item in attack_differences: print('ATTACK DIFF',item)
    for e in enemies:print('RANGE',e['legacy_id'],e['attack_range'])


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    audit(parser.parse_args())
