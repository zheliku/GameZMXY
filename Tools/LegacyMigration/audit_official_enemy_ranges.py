"""Read all 24 original enemy scripts and export independent AI spatial evidence.

Requires the verified remaining-level source inventory prepared by the existing
legacy migration tools. Never writes game tables, scenes or original assets.
"""
from pathlib import Path
import argparse
import re,json,hashlib,sys
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, required=True)
args=parser.parse_args()
args.output.parent.mkdir(parents=True, exist_ok=True)
sys.path.insert(0,str(root/'Tools/LegacyMigration'))
from audit_official_level_dependencies import SOURCE_HASHES
inventory=json.loads((root/'Godot/GodotProject/.godot/validation/remaining_levels/dependency_inventory.json').read_text(encoding='utf-8-sig'))
for s in inventory['sources']:
    if s['name'] in SOURCE_HASHES:assert hashlib.sha256((root/s['swf']).read_bytes()).hexdigest()==SOURCE_HASHES[s['name']]
base=root/'Godot/GodotProject/.godot/validation/remaining_levels/main_scripts/scripts/base/BaseMonster.as'
text=base.read_text(encoding='utf-8-sig')
assert 'Math.abs(this.y - this.gc.hero1.y) <= this.attackRange' in text
assert 'Math.abs(this.x - this.gc.hero1.x) < this.mysee' in text
assert 'Math.abs(this.y - this.curAttackTarget.y) <= this.attackRange * 2' in text
assert 'x - gc.gameInfo.pointmc.x < -400' in text and 'x - gc.gameInfo.pointmc.x > 400' in text
rows=[]
for actor in inventory['actors']:
    path=root/actor['source_script'];s=path.read_text(encoding='utf-8-sig');identity=actor['id']
    def value(name):return float(re.search(r'this\.'+name+r'\s*=\s*([\d.]+)',s)[1])
    attack=value('attackRange');x=attack;y=attack*2;shape='RectangleShape2D';extra={}
    evidence=['BaseMonster.selectTarget','BaseMonster.hasAttackTarget','BaseMonster.normalWalk']
    if identity in (8,15,18):
        pattern=r'return Math.abs\(this.x - this.curAttackTarget.x\) > (\d+) \|\| Math.abs\(this.y - this.curAttackTarget.y\) > (\d+);'
        m=re.search(pattern.replace('Math.abs',r'Math\.abs'),s);assert m,identity;x=float(m[1]);y=float(m[2]);extra['Tracking']=[attack,attack*2];evidence+=['followHero','outer BaseMonster/Monster18.hasAttackTarget height']
    elif identity==6:
        x=float(re.search(r'Math\.abs\(this.x - this.curAttackTarget.x\) > (\d+)',s)[1]);evidence+=['Monster6.hasAttackTarget']
    elif identity==9:
        assert 'AUtils.GetDisBetweenTwoObj(this,this.curAttackTarget) > this.attackRange' in s
        shape='CircleShape2D';extra['Tracking']=[attack,attack*2];extra['Melee']=[float(re.search(r'GetDisBetweenTwoObj\(this,this.curAttackTarget\) <= (\d+)',s)[1])];evidence+=['Monster9.hasAttackTarget']
    elif identity==14:
        x=float(re.search(r'Math\.abs\(this.x - this.curAttackTarget.x\) >= (\d+)',s)[1]);y=None;shape='SegmentShape2D';extra['Retreat']=[float(re.search(r'Math\.abs\(this.x - this.curAttackTarget.x\) <= (\d+)',s)[1])];evidence+=['Monster14.keepDistanceWithHero']
    elif identity in (23,26):
        values=[float(v) for v in re.findall(r'Math\.abs\(this.x - this.curAttackTarget.x\) <= (\d+)',s)]
        x=values[2 if identity==23 else 1];y=None;shape='SegmentShape2D'
        extra.update({'Swallow':[values[0]],'Stomp':[values[1]],'Capture':[float(re.search(r'Math\.abs\(this.x - _loc2_\.x\) <= (\d+)',s)[1])]} if identity==23 else {'WaterColumn':[values[0]]});evidence+=['exclusive horizontal branches, no target height condition']
    patrol=[-360,400] if identity==22 else [-400,400]
    if identity==22:assert 'x - gc.gameInfo.pointmc.x < -360' in s
    if identity in (21,24):purpose='fixed actor: engage/patrol currently not consumed'
    elif identity==22:purpose='charge patrol only; engage not consumed'
    else:purpose='active decision ranges'
    rows.append({'id':identity,'script':actor['source_script'],'detection':[value('mysee'),attack],'engage':{'shape':shape,'x':x,'y':y},'patrol':patrol,'extra':extra,'purpose':purpose,'evidence':evidence})
args.output.write_text(json.dumps({'base_script':str(base.relative_to(root)),'actors':rows},ensure_ascii=False,indent=2),encoding='utf-8')
print('OFFICIAL SPATIAL REFERENCE',len(rows),'species, source hashes and original branch expressions verified')
