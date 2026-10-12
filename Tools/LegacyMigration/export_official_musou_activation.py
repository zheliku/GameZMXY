"""Render the official Role_v7 WsEffect for Wukong; keep SWF code outside the game.

Sprite 59 composes the 24-frame background and 11-frame Wukong entrance.
The official runtime hides Role2Mc and keeps Wukong's final pose until the
background finishes. Extend those display timelines for a faithful PNG export.
"""

import argparse
from copy import deepcopy
from pathlib import Path
import shutil
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from export_official_combat_effects import decode


def export(java: Path, ffdec: Path, role: Path) -> None:
    """Render the original composition, including outlines, text and placement."""
    game = Path(__file__).resolve().parents[2] / "Godot/GodotProject/TheGame"
    with tempfile.TemporaryDirectory(prefix="gamezmxy-musou-") as temporary:
        temp = Path(temporary)
        readable = temp / "role.swf"
        readable.write_bytes(decode(role, "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7"))
        command = [str(java), f"-Duser.home={temporary}", "-jar", str(ffdec)]
        xml = temp / "role.xml"
        subprocess.run(command + ["-swf2xml", str(readable), str(xml)], check=True)
        tree = ET.parse(xml)
        sprites = {e.get("spriteId"): e for e in tree.iter("item") if e.get("type") == "DefineSpriteTag"}
        parent = sprites["59"]
        tags = parent.find("subTags")
        tags.remove(next(e for e in tags if e.get("name") == "Role2Mc"))
        for sprite in [parent, sprites["55"]]:
            timeline = sprite.find("subTags")
            last_frame = next(e for e in reversed(timeline) if e.get("type") == "ShowFrameTag")
            for _ in range(24 - int(sprite.get("frameCount"))): timeline.append(deepcopy(last_frame))
            sprite.set("frameCount", "24")
        # Export the official 940×590 screen at the runtime's (470,295) origin.
        # Sprite bounds include off-screen entrance positions and cannot be used
        # as the final image rectangle without shifting or shrinking the artwork.
        root = tree.getroot()
        root.set("frameCount", "24")
        root.find("displayRect").set("Xmin", "0")
        root.find("displayRect").set("Ymin", "0")
        root.find("displayRect").set("Xmax", "18800")
        root.find("displayRect").set("Ymax", "11800")
        stage = root.find("tags")
        for tag in list(stage):
            if tag.get("type", "").startswith(("PlaceObject", "RemoveObject", "ShowFrame", "DoAction", "DoABC")):
                stage.remove(tag)
        placed = ET.SubElement(stage, "item", type="PlaceObject2Tag", characterId="59", depth="1",
            forceWriteAsLong="false", placeFlagHasCharacter="true", placeFlagHasMatrix="true",
            placeFlagMove="false", placeFlagHasClipActions="false", placeFlagHasClipDepth="false",
            placeFlagHasColorTransform="false", placeFlagHasName="false", placeFlagHasRatio="false")
        ET.SubElement(placed, "matrix", type="MATRIX", hasRotate="false", hasScale="false",
            nRotateBits="0", nScaleBits="0", nTranslateBits="15", translateX="9400", translateY="5900")
        for _ in range(24): ET.SubElement(stage, "item", type="ShowFrameTag", forceWriteAsLong="false")
        tree.write(xml, encoding="utf-8", xml_declaration=True)
        composed = temp / "musou.swf"
        subprocess.run(command + ["-xml2swf", str(xml), str(composed)], check=True)
        subprocess.run(command + ["-ignorebackground", "-export", "frame", temporary, str(composed)], check=True)
        rendered = sorted(temp.rglob("*.png"), key=lambda p: int(re.findall(r"\d+", p.stem)[-1]))
        if len(rendered) != 24: raise ValueError(f"Expected 24 screen frames, found {len(rendered)}")
        destination = game / "Sprites/UI/hud/musou_activation"
        destination.mkdir(parents=True, exist_ok=True)
        resources, frames = [], []
        for frame in range(1, 25):
            asset = destination / f"musou_activation_{frame:02}.png"
            shutil.copyfile(rendered[frame - 1], asset)
            resources.append(f'[ext_resource type="Texture2D" path="res://TheGame/{asset.relative_to(game).as_posix()}" id="f{frame}"]')
            frames.append('{"duration": 1.0, "texture": ExtResource("f' + str(frame) + '")}')
        (destination / "musou_activation_frames.tres").write_text(
            '[gd_resource type="SpriteFrames" format=3]\n\n' + '\n'.join(resources)
            + '\n\n[resource]\nanimations = [{"frames": [' + ',\n'.join(frames)
            + '], "loop": false, "name": &"musou_activation", "speed": 24.0}]\n', encoding="utf-8")
        print("WsEffect: 24 original composed frames, Wukong only")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("java", "ffdec", "role"): parser.add_argument(f"--{name}", type=Path, required=True)
    args = parser.parse_args()
    export(args.java, args.ffdec, args.role)
