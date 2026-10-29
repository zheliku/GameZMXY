"""Catch official dependencies outside display lists and ordinary drop arrays."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'Tools/LegacyMigration'))
from audit_official_level_dependencies import direct_equipment_rewards, dynamic_dependencies


class OfficialDependencyTests(unittest.TestCase):
    def test_direct_fire_constructor_is_resolved_without_importing_utilities(self):
        packs = [{'symbols': {'export.monster.Monster23Child.Fire': 7, 'Monster24BG': 8}}]
        source = '''package export.monster {
            import export.monster.Monster23Child.*;
            import flash.geom.Point;
            var fire:Fire = new Fire(this);
            var point:Point = new Point(1, 2);
            var bg:* = AUtils.getNewObj("Monster24BG");
        }'''
        self.assertEqual({'export.monster.Monster23Child.Fire', 'Monster24BG'},
                         dynamic_dependencies(source, packs))

    def test_direct_inventory_award_does_not_need_a_fall_list(self):
        source = '''var fillName:*;
            fillName = "jgz";
            equip = gc.allEquip.findByName(fillName);
            gc.player1.zblist.push(equip);
            var unrelated:String = "not_an_equipment";'''
        self.assertEqual({'jgz'}, direct_equipment_rewards(source))
        self.assertEqual({'dhqf'}, direct_equipment_rewards('findByName("dhqf");'))


if __name__ == '__main__':
    unittest.main()
