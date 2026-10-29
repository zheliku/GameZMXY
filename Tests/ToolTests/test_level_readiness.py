"""Regression checks for false positives in the level authoring gate."""

import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Tools/LegacyMigration"))
from audit_remaining_level_readiness import (
    _drop_audit,
    _playable_wardrobe_audit,
    _required_actors,
    _recipe_errors,
    _validation_evidence,
)


class LevelReadinessTests(unittest.TestCase):
    def test_spawn_recipe_keeps_initial_interval_after_source_delay(self):
        source = [{'spawn_point_id': 'stage_1_a', 'stage': 1, 'enemy_legacy_id': 14,
                   'count': 5, 'first_spawn_seconds': 4.0, 'interval_seconds': 2.0}]
        actual = {'stage_1_a': {'stage': 1, 'entity': 'ShrimpDemon', 'count': 5,
                              'first_spawn_seconds': 2.0, 'interval_seconds': 2.0}}
        errors = _recipe_errors(source, actual, {14: 'ShrimpDemon'})
        self.assertEqual(1, len(errors))
        self.assertIn('first_spawn_seconds', errors[0])
        actual['stage_1_a']['first_spawn_seconds'] = 4.0
        self.assertEqual([], _recipe_errors(source, actual, {14: 'ShrimpDemon'}))
        actual['stage_1_a']['interval_seconds'] = 4.0
        self.assertIn('interval_seconds', _recipe_errors(source, actual, {14: 'ShrimpDemon'})[0])

    def test_equal_total_counts_do_not_hide_wrong_enemy_or_missing_stream(self):
        source = [{'spawn_point_id': 'stage_1_a', 'stage': 1, 'enemy_legacy_id': 14,
                   'count': 5, 'first_spawn_seconds': 4.0, 'interval_seconds': 2.0}]
        actual = {'stage_1_a': {'stage': 1, 'entity': 'TideDemon', 'count': 5,
                              'first_spawn_seconds': 4.0, 'interval_seconds': 2.0}}
        self.assertIn('entity', _recipe_errors(source, actual, {14: 'ShrimpDemon'})[0])
        self.assertIn('missing', _recipe_errors(source, {}, {14: 'ShrimpDemon'})[0])

    def test_old_inventory_cannot_satisfy_exact_spawn_recipe_audit(self):
        self.assertIn('regenerate', _recipe_errors(None, {}, {})[0])

    def test_dynamic_summons_and_all_boss_forms_are_required(self):
        actors = {22: {"spawned_enemies": [23]}, 23: {"spawned_enemies": [24]},
                  24: {"spawned_enemies": [22]}, 25: {"spawned_enemies": []}}
        self.assertEqual([22, 23, 24, 25], _required_actors({"enemy_counts": {"22": 1, "25": 30}}, actors))

    def test_existing_items_do_not_satisfy_a_new_boss_drop(self):
        actors = {9: {"equipment_drops": ["tsyj", "tsyp"]}}
        result = _drop_audit([9], actors, [{"legacy_id": "ptxzg", "id": 6}], {}, Path("."))
        self.assertEqual(["tsyj", "tsyp"], result["missing_equipment"])
        self.assertEqual(2, len(result["errors"]))

    def test_appearance_entry_requires_both_actual_textures(self):
        actors = {9: {"equipment_drops": ["tsyj"]}}
        result = _drop_audit([9], actors, [{"legacy_id": "tsyj", "id": 16}],
                             {16: {"textures": {}}}, Path("."))
        self.assertEqual(2, len(result["errors"]))

    def test_playable_wearables_require_identity_specific_motion_audit(self):
        actors = {9: {"equipment_drops": ["tsyj", "tsyp", "dslj"]}}
        equipment = [
            {"legacy_id": "tsyj", "id": 16, "name": "天煞月戟", "owner": "悟空", "slot": "武器"},
            {"legacy_id": "tsyp", "id": 17, "name": "天煞羽袍", "owner": "唐僧", "slot": "防具"},
            {"legacy_id": "dslj", "id": 9, "name": "地煞灵戒", "owner": "通用", "slot": "饰品"},
        ]
        result = _playable_wardrobe_audit([9], actors, equipment)
        self.assertEqual(["悟空"], result["required_owners"])
        self.assertEqual(["tsyj"], [item["legacy_id"] for item in result["required_equipment"]])
        self.assertEqual([], result["errors"])

    def test_unknown_wearable_owner_cannot_bypass_wardrobe_audit(self):
        actors = {9: {"equipment_drops": ["new_item"]}}
        equipment = [{"legacy_id": "new_item", "id": 99, "owner": "新英雄", "slot": "武器"}]
        result = _playable_wardrobe_audit([9], actors, equipment)
        self.assertTrue(result["errors"])

    def test_receipts_require_success_current_inputs_and_scoped_logs(self):
        gate = "music_sound_events_and_audio_assets"
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            folder = work / "validation/2-3"
            folder.mkdir(parents=True)
            log = folder / "audio.log"
            log.write_text("GARUDA AUDIO 2-3 PASS\n", encoding="utf-8")
            receipt = {"official_label": "2-3", "project_sha256": "current", "inventory_sha256": "source",
                       "checks": {gate: {"command": ["godot", "--garuda-audio"], "exit_code": 0,
                                         "log": log.name, "log_sha256": hashlib.sha256(log.read_bytes()).hexdigest(),
                                         "pass_marker": "GARUDA AUDIO 2-3 PASS"}}}
            path = folder / "evidence.json"
            path.write_text(json.dumps(receipt), encoding="utf-8")
            accepted, errors = _validation_evidence(work, "2-3", "current", "source")
            self.assertIn(gate, accepted)
            self.assertEqual([], errors)
            self.assertEqual({}, _validation_evidence(work, "2-3", "changed", "source")[0])
            receipt["checks"][gate]["exit_code"] = 1
            path.write_text(json.dumps(receipt), encoding="utf-8")
            self.assertEqual({}, _validation_evidence(work, "2-3", "current", "source")[0])
            receipt["checks"][gate]["exit_code"] = 0
            path.write_text(json.dumps(receipt), encoding="utf-8")
            log.write_text("CHAPTER MUSIC PASS\n", encoding="utf-8")
            self.assertEqual({}, _validation_evidence(work, "2-3", "current", "source")[0])

    def test_wardrobe_receipt_must_run_identity_and_motion_validators(self):
        gate = "playable_equipment_identity_and_motion"
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            folder = work / "validation/2-3"
            folder.mkdir(parents=True)
            log = folder / "wardrobe.log"
            log.write_text("GARUDA WARDROBE 2-3 PASS\n", encoding="utf-8")
            path = folder / "evidence.json"
            receipt = {"official_label": "2-3", "project_sha256": "current", "inventory_sha256": "source",
                       "checks": {gate: {"command": ["python", "audit_official_wukong_wardrobe.py",
                                                                 "validate_wukong_wardrobe_motion.gd",
                                                                 "validate_official_wukong_attacks.gd",
                                                                 "--equipment-only"],
                                         "exit_code": 0, "log": log.name,
                                         "log_sha256": hashlib.sha256(log.read_bytes()).hexdigest(),
                                         "pass_marker": "GARUDA WARDROBE 2-3 PASS"}}}
            path.write_text(json.dumps(receipt), encoding="utf-8")
            owners = ["悟空"]
            self.assertIn(gate, _validation_evidence(work, "2-3", "current", "source", owners)[0])
            receipt["checks"][gate]["command"] = ["python", "audit_official_wukong_wardrobe.py"]
            path.write_text(json.dumps(receipt), encoding="utf-8")
            accepted, errors = _validation_evidence(work, "2-3", "current", "source", owners)
            self.assertNotIn(gate, accepted)
            self.assertIn("validate_wukong_wardrobe_motion.gd", errors[0])
            receipt["checks"][gate]["command"].append("validate_wukong_wardrobe_motion.gd")
            path.write_text(json.dumps(receipt), encoding="utf-8")
            accepted, errors = _validation_evidence(work, "2-3", "current", "source", owners)
            self.assertNotIn(gate, accepted)
            self.assertIn("validate_official_wukong_attacks.gd", errors[0])
            receipt["checks"][gate]["command"].append("validate_official_wukong_attacks.gd")
            path.write_text(json.dumps(receipt), encoding="utf-8")
            accepted, errors = _validation_evidence(work, "2-3", "current", "source", owners)
            self.assertNotIn(gate, accepted)
            self.assertIn("--equipment-only", errors[0])


if __name__ == "__main__":
    unittest.main()
