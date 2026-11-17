"""Render four SelectRole cards from original vectors and embedded bitmaps.

Input is the read-only OtherMat_v9.swf documented in Docs/LegacyAssetMap.md.
Higher density improves vector parts; embedded bitmaps retain their source detail.
No ActionScript or SWF program is copied into Godot.
"""

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image


CARDS = [("wukong", 511, 9, 509, 507), ("sanzang", 521, 21, 520, 519),
         ("bajie", 515, 15, 514, 513), ("wujing", 518, 22, 517, 516)]
SVG = "http://www.w3.org/2000/svg"
FF = "https://www.free-decompiler.com/flash"
ET.register_namespace("", SVG)
ET.register_namespace("ffdec", FF)
ET.register_namespace("xlink", "http://www.w3.org/1999/xlink")


def readable_swf(swf: Path) -> bytes:
    """Restore Aloader's byte order and enforce the documented source identity."""
    content = swf.read_bytes()
    if content[:3] not in (b"FWS", b"CWS", b"ZWS"):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != "fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e":
        raise ValueError("Expected the documented OtherMat_v9.swf; refusing a different source")
    return content


def export_layers(java: Path, ffdec: Path, swf: Path, godot: Path, density: int,
                  destination: Path, report: Path | None) -> None:
    """Export the approved portrait/ink layers; retain native JPEG detail and source anchors."""
    destination.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="gamezmxy-select-layers-") as temporary:
        work = Path(temporary)
        readable = work / "other_mat_readable.swf"
        readable.write_bytes(readable_swf(swf))
        subprocess.run([str(java), f"-Duser.home={temporary}", "-jar", str(ffdec),
                        "-selectid", "511,521,515,518", "-format", "sprite:svg",
                        "-ignorebackground", "-export", "sprite", str(work / "svg"), str(readable)], check=True)
        subprocess.run([str(java), f"-Duser.home={temporary}", "-jar", str(ffdec),
                        "-selectid", "508", "-export", "image", str(work / "bitmaps"), str(readable)], check=True)
        records = []
        for name, sprite, crop_x, body_shape, name_bitmap in CARDS:
            source = work / f"svg/DefineSprite_{sprite}/1.svg"
            root = ET.parse(source).getroot()
            root.set("width", "237px")
            root.set("height", "610px")
            root.set("viewBox", f"{crop_x} 0 237 610")
            portrait = copy.deepcopy(root)
            removed = []
            for parent in portrait.iter():
                for child in list(parent):
                    if child.attrib.get(f"{{{FF}}}fill-bitmapId") in ("508", str(name_bitmap)) or (
                            child.tag == f"{{{SVG}}}use" and child.attrib.get(f"{{{FF}}}characterId") == "510"):
                        removed.append(dict(child.attrib))
                        parent.remove(child)
            assert len(removed) in (3, 4)
            master_width = 237 * 420 / 235
            portrait.set("width", f"{master_width}px")
            portrait.set("viewBox", f"{crop_x - (master_width - 237) / 2} 0 {master_width} 610")
            name_path = next(node for node in root.iter(f"{{{SVG}}}path")
                             if node.attrib.get(f"{{{FF}}}fill-bitmapId") == str(name_bitmap))
            pattern_id = re.search(r"url\(#([^\)]+)\)", name_path.attrib["style"])[1]
            pattern = next(node for node in root.iter(f"{{{SVG}}}pattern") if node.attrib.get("id") == pattern_id)
            name_matrix = [float(n) for n in re.findall(r"-?\d+(?:\.\d+)?", pattern.attrib["patternTransform"])]
            stage_matrix = [float(n) for n in re.findall(r"-?\d+(?:\.\d+)?", root.find(f"{{{SVG}}}g").attrib["transform"])]
            mask_coordinates = [float(n) for n in re.findall(r"-?\d+(?:\.\d+)?",
                                root.find(f".//{{{SVG}}}clipPath")[0].attrib["d"])]
            assert len(mask_coordinates) == 8
            background_mask = [[(mask_coordinates[i] + stage_matrix[4] - crop_x) / 237 * 235,
                                (mask_coordinates[i + 1] + stage_matrix[5]) / 610 * 590]
                               for i in range(0, 8, 2)]
            # Exclude the original name field as a whole, including shared vector strokes.
            for clip in portrait.iter(f"{{{SVG}}}clipPath"):
                clip.clear()
                clip.set("id", "clipPath0")
                ET.SubElement(clip, f"{{{SVG}}}rect", x=str(-master_width), y="-349",
                              width=str(master_width * 2), height=str(name_matrix[5] - .25 + 349))
            ink = copy.deepcopy(root)
            stage = ink.find(f"{{{SVG}}}g")
            for child in list(stage):
                if child.tag != f"{{{SVG}}}use" or child.attrib.get(f"{{{FF}}}characterId") != "510":
                    stage.remove(child)
            assert len(stage) == 1
            for suffix, document in [("portrait", portrait), ("ink", ink)]:
                (work / f"{name}_{suffix}.svg").write_bytes(ET.tostring(document, encoding="utf-8"))
            records.append({"name": name, "sprite": sprite, "body_shape": body_shape, "name_bitmap": name_bitmap,
                            "source_svg_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                            "master_logical_width": 420, "density": density,
                            "name_rect_logical": [(name_matrix[4] + stage_matrix[4] - crop_x) / 237 * 235,
                                                  (name_matrix[5] + stage_matrix[5]) / 610 * 590,
                                                  float(pattern.attrib["width"]) / 237 * 235, 65 / 610 * 590],
                            "background_mask_logical": background_mask})
        # Use the same Godot SVG rasterizer as the approved sample, before importing PNGs.
        raster = work / "raster.gd"
        raster.write_text('extends SceneTree\nfunc _initialize():\n' + "\n".join(
            f'    raster({json.dumps((work / f"{name}_{suffix}.svg").as_posix())})'
            for name, *_ in CARDS for suffix in ("portrait", "ink")) +
            f'\n    quit()\nfunc raster(path):\n    var pixels = Image.new()\n'
            f'    assert(pixels.load_svg_from_string(FileAccess.get_file_as_string(path), {float(density)}) == OK)\n'
            '    assert(not pixels.is_invisible())\n'
            '    assert(pixels.save_png(path.trim_suffix(".svg") + ".png") == OK)\n', encoding="utf-8")
        subprocess.run([str(godot), "--headless", "--path", str(work), "--script", str(raster)], check=True)
        # Wukong and Wujing's ink layers are byte-identical; share that texture.
        assert (work / "wukong_ink.png").read_bytes() == (work / "wujing_ink.png").read_bytes()
        for record in records:
            name = record["name"]
            ink_file = f"character_{name}_ink.png" if name in ("sanzang", "bajie") else "character_card_ink.png"
            record["ink_file"] = ink_file
            shutil.copyfile(work / f"{name}_portrait.png", destination / f"character_{name}_card.png")
            shutil.copyfile(work / f"{name}_ink.png", destination / ink_file)
        background = work / "bitmaps/508.jpg"
        with Image.open(background) as image:
            assert image.size == (940, 590)
        shutil.copyfile(background, destination / "character_card_background.jpg")
        if report:
            report.parent.mkdir(parents=True, exist_ok=True)
            report.write_text(json.dumps(records, indent=2), encoding="utf-8")
        print(f"Exported four portraits, three shared/variant ink textures and native background at density {density}")


def export_cards(java: Path, ffdec: Path, swf: Path, density: int = 1,
                 output_directory: Path | None = None) -> None:
    """Render complete cards, including their masks, ink frames and baked names."""
    game = Path(__file__).resolve().parents[2] / "Godot/GodotProject/TheGame"
    destination_directory = output_directory or game / "Sprites/UI/frontend"
    destination_directory.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="gamezmxy-select-role-") as temporary:
        # Aloader places bytes 100..109 before bytes 0..99, then appends the rest.
        content = readable_swf(swf)
        readable = Path(temporary) / "other_mat_readable.swf"
        readable.write_bytes(content)
        subprocess.run([
            str(java), f"-Duser.home={temporary}", "-jar", str(ffdec),
            "-selectid", "511,515,518,521", "-ignorebackground",
            "-zoom", str(density),
            "-export", "sprite", temporary, str(readable),
        ], check=True)
        for name, sprite_id, canvas in [("wukong", 511, (334, 610)), ("sanzang", 521, (317, 617)),
                                        ("bajie", 515, (357, 617)), ("wujing", 518, (351, 617))]:
            source = Path(temporary) / f"DefineSprite_{sprite_id}/1.png"
            destination = destination_directory / f"character_{name}_card.png"
            with Image.open(source) as rendered:
                size = (canvas[0] * density, canvas[1] * density)
                if rendered.size == size:
                    shutil.copyfile(source, destination)
                else:
                    # FFDec rounds vector bounds per density. Keep the original
                    # logical canvas by padding only the transparent right/bottom.
                    if any(actual > expected or expected - actual >= density
                           for actual, expected in zip(rendered.size, size)):
                        raise ValueError(f"Unexpected source canvas for {name}: {rendered.size}, expected {size}")
                    padded = Image.new("RGBA", size)
                    padded.paste(rendered, (0, 0))
                    padded.save(destination)
            print(f"DefineSprite {sprite_id} at density {density} -> {destination}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--java", type=Path, required=True)
    parser.add_argument("--ffdec", type=Path, required=True, help="FFDec 26.3.0 ffdec.jar")
    parser.add_argument("--swf", type=Path, required=True, help="Original or decoded OtherMat_v9.swf, kept outside the project")
    parser.add_argument("--density", type=int, choices=(1, 2, 3), default=3, help="Raster density from original vectors")
    parser.add_argument("--mode", choices=("layers", "complete"), default="layers",
                        help="Formal layered cards, or historical complete-card reproduction in an isolated directory")
    parser.add_argument("--godot", type=Path, help="Godot executable, required for layered source rasterization")
    parser.add_argument("--report", type=Path, help="Offline source/anchor record for layered exports")
    parser.add_argument("--output-directory", type=Path, help="Isolated candidate directory; defaults to the formal frontend directory")
    args = parser.parse_args()
    if args.mode == "layers":
        if not args.godot:
            parser.error("--godot is required for --mode layers")
        destination = args.output_directory or Path(__file__).resolve().parents[2] / "Godot/GodotProject/TheGame/Sprites/UI/frontend"
        export_layers(args.java, args.ffdec, args.swf, args.godot, args.density, destination, args.report)
    else:
        if not args.output_directory:
            parser.error("Historical complete cards require an isolated --output-directory")
        export_cards(args.java, args.ffdec, args.swf, args.density, args.output_directory)
