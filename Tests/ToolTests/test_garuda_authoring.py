"""Test source transforms and held child animation used by Garuda authoring."""

from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Tools/LegacyMigration"))
from prepare_official_garuda_level import _compose, _pack_layer, _probe_reader


class GarudaAuthoringTests(unittest.TestCase):
    def test_complete_affine_parent_transform_is_preserved(self):
        self.assertEqual([1, 4, -5, 1, 3, 29], _compose([2, 1, -1, 3, 4, 5], [1, 1, -2, 1, 3, 7]))

    def test_held_one_frame_wrapper_keeps_nested_animation_advancing(self):
        tags = ET.fromstring('''<tags>
          <item type="DefineShapeTag" shapeId="1">
            <shapeBounds Xmin="0" Ymin="0" Xmax="200" Ymax="100" />
          </item>
          <item type="DefineSpriteTag" spriteId="2"><subTags>
            <item type="PlaceObject2Tag" characterId="1" depth="1"><matrix /></item>
            <item type="ShowFrameTag" />
            <item type="PlaceObject2Tag" depth="1"><matrix translateX="400" translateY="0" /></item>
            <item type="ShowFrameTag" />
          </subTags></item>
          <item type="DefineSpriteTag" spriteId="3"><subTags>
            <item type="PlaceObject2Tag" characterId="2" depth="1"><matrix /></item>
            <item type="ShowFrameTag" />
          </subTags></item>
        </tags>''')
        _, bounds = _probe_reader(tags)
        self.assertEqual([0, 0, 10, 5], bounds(3, 0, [1, 0, 0, 1, 0, 0]))
        self.assertEqual([20, 0, 10, 5], bounds(3, 1, [1, 0, 0, 1, 0, 0]))

    def test_equal_pixels_at_different_origins_are_not_deduplicated(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            for name in ('left', 'right'):
                (folder / name).mkdir()
                Image.new('RGBA', (2, 2), (255, 0, 0, 255)).save(folder / name / '1.png')
            layer = {'output': str(folder / 'atlas.png'), 'actions': [
                {'name': name, 'frames': 1, 'input': str(folder / name), 'bounds': [0, 0, 2, 2],
                 'matrix': [1, 0, 0, 1, offset, 0]} for name, offset in [('left', -10), ('right', 10)]]}
            _pack_layer(layer)
            self.assertEqual(2, layer['unique_cells'])
            self.assertEqual([22, 2], layer['cell_size'])
            self.assertNotEqual(layer['poses']['left'], layer['poses']['right'])
            with Image.open(layer['output']) as atlas:
                self.assertEqual((0, 0, 2, 2), atlas.crop((0, 0, 22, 2)).getbbox())
                self.assertEqual((20, 0, 22, 2), atlas.crop((22, 0, 44, 2)).getbbox())

    def test_clip_depth_excludes_tiled_bitmap_canvas_and_expires(self):
        tags = ET.fromstring('''<tags>
          <item type="DefineShapeTag" shapeId="1"><shapeBounds Xmin="0" Ymin="0" Xmax="200" Ymax="200" /></item>
          <item type="DefineShapeTag" shapeId="2"><shapeBounds Xmin="0" Ymin="0" Xmax="19200" Ymax="11520" /></item>
          <item type="DefineShapeTag" shapeId="3"><shapeBounds Xmin="0" Ymin="0" Xmax="100" Ymax="100" /></item>
          <item type="DefineSpriteTag" spriteId="4"><subTags>
            <item type="PlaceObject2Tag" characterId="1" depth="1" clipDepth="2"><matrix translateX="100" /></item>
            <item type="PlaceObject2Tag" characterId="2" depth="2"><matrix /></item>
            <item type="PlaceObject2Tag" characterId="3" depth="3"><matrix translateX="600" /></item>
            <item type="ShowFrameTag" />
          </subTags></item>
        </tags>''')
        _, bounds = _probe_reader(tags)
        # The 960x576 storage canvas is clipped to x=5..15; depth 3 remains outside the mask.
        self.assertEqual([5, 0, 30, 10], bounds(4, 0, [1, 0, 0, 1, 0, 0]))

    def test_blank_terminal_frame_does_not_enlarge_every_atlas_cell(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            for name in ('visible', 'blank'):
                (folder / name).mkdir()
            Image.new('RGBA', (2, 2), (255, 0, 0, 255)).save(folder / 'visible/1.png')
            Image.new('RGBA', (100, 100)).save(folder / 'blank/1.png')
            layer = {'output': str(folder / 'atlas.png'), 'actions': [
                {'name': name, 'frames': 1, 'input': str(folder / name), 'bounds': [offset, offset, 100, 100],
                 'matrix': [1, 0, 0, 1, 0, 0]} for name, offset in [('visible', 0), ('blank', -1000)]]}
            _pack_layer(layer)
            self.assertEqual([2, 2], layer['cell_size'])
            with Image.open(layer['output']) as atlas:
                self.assertEqual((4, 2), atlas.size)
                self.assertIsNone(atlas.crop((2, 0, 4, 2)).getbbox())


if __name__ == '__main__':
    unittest.main()
