"""Regression checks for translated planner-facing enum identities."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'Tools/ProjectMaintenance'))
from audit_config_readability import enum_definitions, inspect_sources


class ConfigReadabilityTests(unittest.TestCase):
    def test_every_formal_source_enum_cell_uses_its_chinese_alias(self):
        result = inspect_sources(Path(__file__).resolve().parents[2] / 'Configs/GameConfig/Datas')
        self.assertEqual([], result['english_enum_cells'])
        self.assertEqual([], result['missing_aliases'])
        self.assertEqual([], result['errors'])
        self.assertGreater(len(result['enum_columns']), 20)

    def test_same_enum_name_can_have_different_aliases_in_different_types(self):
        rows = [
            {'B4': 'Entity.EEntityId', 'H4': 'None', 'I4': '无实体', 'J4': '-1'},
            {'B5': 'Equipment.EEquipmentId', 'H5': 'None', 'I5': '无装备', 'J5': '0'},
        ]
        definitions = enum_definitions(rows)
        self.assertEqual('无实体', definitions['Entity.EEntityId']['None'])
        self.assertEqual('无装备', definitions['Equipment.EEquipmentId']['None'])
