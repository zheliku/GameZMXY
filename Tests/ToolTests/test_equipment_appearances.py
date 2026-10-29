"""Prevent new wearable rows from bypassing the existing atlas audit."""

from pathlib import Path
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Tools/LegacyMigration"))
from audit_equipment_appearances import _texture_report, _wardrobe_binding_errors


class EquipmentAppearanceTests(unittest.TestCase):
    def test_opaque_icon_cannot_pass_as_transparent_equipment_art(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            with Image.new("RGB", (52, 54), "white") as image:
                image.save(project / "icon.png")
            self.assertIn("opaque background", _texture_report(project, "res://icon.png")["error"])
            with Image.new("RGBA", (52, 54)) as image:
                image.putpixel((26, 27), (255, 255, 255, 255))
                image.save(project / "icon.png")
            self.assertNotIn("error", _texture_report(project, "res://icon.png"))

    def test_icons_cannot_replace_a_playable_motion_binding(self):
        items = [{"id": 23, "name": "血海魔甲", "owner": "悟空", "slot": "防具"}]
        errors = _wardrobe_binding_errors(items, {"body": {0: "bare.png"}, "weapon": {0: "default.png"}})
        self.assertEqual(1, len(errors))
        self.assertIn("EquipmentId 23", errors[0])
        self.assertIn("body atlas", errors[0])

    def test_wrong_slot_and_orphaned_bindings_are_rejected(self):
        items = [{"id": 23, "name": "血海魔甲", "owner": "悟空", "slot": "防具"}]
        errors = _wardrobe_binding_errors(items, {"body": {0: "bare.png"}, "weapon": {23: "armor.png"}})
        self.assertEqual(2, len(errors))

    def test_nonplayable_owners_and_accessories_do_not_use_wukong_layers(self):
        items = [
            {"id": 23, "name": "血海魔甲", "owner": "悟空", "slot": "防具"},
            {"id": 24, "name": "血海邪皇", "owner": "唐僧", "slot": "武器"},
            {"id": 22, "name": "血海妖壳", "owner": "通用", "slot": "饰品"},
        ]
        self.assertEqual([], _wardrobe_binding_errors(
            items, {"body": {0: "bare.png", 23: "armor.png"}, "weapon": {0: "default.png"}}))


if __name__ == "__main__":
    unittest.main()
