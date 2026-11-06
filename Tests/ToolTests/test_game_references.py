"""Keep project settings from silently retaining deleted UID-only assets."""

from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Tools/ProjectMaintenance"))
from validate_game_references import missing_project_uids


class ProjectSettingsReferenceTests(unittest.TestCase):
    def test_deleted_icon_is_missing_even_when_import_metadata_remains(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            (project / "project.godot").write_text('config/icon="uid://icon123"', encoding="utf-8")
            icon = project / "icon.png"
            icon.write_bytes(b"icon fixture")
            (project / "icon.png.import").write_text('[remap]\nuid="uid://icon123"', encoding="utf-8")
            self.assertEqual([], missing_project_uids(project))
            icon.unlink()
            self.assertEqual(["uid://icon123"], missing_project_uids(project))

    def test_autoload_requires_its_own_source_uid(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            (project / "project.godot").write_text('Driver="*uid://driver123"', encoding="utf-8")
            (project / "view.tscn").write_text(
                '[gd_scene format=3]\n[ext_resource type="Script" uid="uid://driver123" path="res://driver.cs" id="1"]',
                encoding="utf-8")
            cache = project / ".godot"
            cache.mkdir()
            (cache / "stale.tscn").write_text('[gd_scene uid="uid://driver123"]', encoding="utf-8")
            self.assertEqual(["uid://driver123"], missing_project_uids(project))
            (project / "driver.cs").write_text("// source fixture", encoding="utf-8")
            (project / "driver.cs.uid").write_text("uid://driver123", encoding="utf-8")
            self.assertEqual([], missing_project_uids(project))
