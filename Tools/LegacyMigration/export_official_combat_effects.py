"""Render only official visual clips; no Flash program is copied into the game.

Role_v7.swf sprite 240 is BaseHero.levelupmc. Monster_v1.swf sprites
5/7/9 are yellow/blue/purple auras. Yellow restores Musou; AS class names
"auraWhile" and "auraRed" do not describe the rendered colors accurately.
Input SWFs stay outside the repository. Requires FFDec 26.3.0 and Java.
"""

import argparse
import hashlib
from pathlib import Path
import re
import shutil
import struct
import subprocess
import tempfile


# Role_v7's bitmap labels sit outside the shape's visible rectangle. FFDec's
# edge sampling leaks their last row into frames 11..28. Crop that row through
# AtlasTexture metadata, restoring the transparent margin to keep registration.
# Values apply only to the hash-checked source; each drawing lasts 2 frames.
LEVEL_UP_CLIP_TOP = (0, 0, 0, 0, 0, 1, 5, 4, 4, 9, 4, 22, 40, 49)
# Preserve the identity assigned when this resource was first imported.
LEVEL_UP_RESOURCE_UID = "uid://bxl6nbctngbl"


def decode(source: Path, expected: str) -> bytes:
    """Restore the loader's byte rotation and reject a different asset revision."""
    content = source.read_bytes()
    if content[:3] not in (b"FWS", b"CWS", b"ZWS"):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != expected:
        raise ValueError(f"Unexpected source revision: {source}")
    return content


def export(java: Path, ffdec: Path, role: Path, monsters: Path, group: str = "all") -> None:
    """Copy rendered frames verbatim and generate external SpriteFrames resources."""
    game = Path(__file__).resolve().parents[2] / "Godot/GodotProject/TheGame"
    groups = [
        (role, "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7",
         [(240, "level_up", 28, False)], "level_up"),
        (monsters, "5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a",
         [(9, "Soul", 19, True), (5, "Musou", 19, True), (7, "Mana", 19, True)], "loot"),
    ]
    with tempfile.TemporaryDirectory(prefix="gamezmxy-effects-") as temporary:
        temp = Path(temporary)
        for source, digest, clips, folder in groups:
            if group != "all" and folder != group:
                continue
            readable = temp / f"{folder}.swf"
            readable.write_bytes(decode(source, digest))
            subprocess.run([str(java), f"-Duser.home={temporary}", "-jar", str(ffdec), "-selectid",
                            ",".join(str(c[0]) for c in clips), "-ignorebackground",
                            "-export", "sprite", temporary, str(readable)], check=True)
            destination = game / "Sprites/Effects" / folder
            destination.mkdir(parents=True, exist_ok=True)
            resource_file = destination / f"{folder}_frames.tres"
            previous_header = resource_file.read_text(encoding="utf-8").splitlines()[0] if resource_file.exists() else ""
            previous_uid = re.search(r'uid="([^"]+)"', previous_header)
            resource_uid = previous_uid[1] if previous_uid else LEVEL_UP_RESOURCE_UID if folder == "level_up" else ""
            uid_attribute = f' uid="{resource_uid}"' if resource_uid else ""
            # Remove only this generator's obsolete XP-named outputs.
            if folder == "loot":
                for stale in destination.glob("experience_*.png*"):
                    stale.unlink()
            resources, regions, animations = [], [], []
            for sprite_id, name, count, loop in clips:
                rendered = next(temp.glob(f"DefineSprite_{sprite_id}_*"))
                frames = []
                for frame in range(1, count + 1):
                    asset = destination / f"{name.lower()}_{frame:02}.png"
                    shutil.copyfile(rendered / f"{frame}.png", asset)
                    key = f"{name}_{frame}"
                    imported = asset.with_suffix(".png.import")
                    texture_uid = re.search(r'^uid="([^"]+)"', imported.read_text(encoding="utf-8"), re.MULTILINE) if imported.exists() else None
                    texture_uid_attribute = f' uid="{texture_uid[1]}"' if texture_uid else ""
                    resources.append(f'[ext_resource type="Texture2D"{texture_uid_attribute} path="res://TheGame/{asset.relative_to(game).as_posix()}" id="{key}"]')
                    texture = f'ExtResource("{key}")'
                    top = LEVEL_UP_CLIP_TOP[(frame - 1) // 2] if folder == "level_up" else 0
                    if top:
                        width, height = struct.unpack(">II", asset.read_bytes()[16:24])
                        regions.append(f'[sub_resource type="AtlasTexture" id="{key}_clip"]\n'
                                       f'atlas = {texture}\n'
                                       f'region = Rect2(0, {top}, {width}, {height - top})\n'
                                       f'margin = Rect2(0, {top}, 0, {top})\n'
                                       'filter_clip = true')
                        texture = f'SubResource("{key}_clip")'
                    frames.append('{"duration": 1.0, "texture": ' + texture + '}')
                animations.append('{"frames": [' + ',\n'.join(frames) + '], "loop": '
                                  + str(loop).lower() + ', "name": &"' + name + '", "speed": 24.0}')
            resource_file.write_text(
                f'[gd_resource type="SpriteFrames" format=3{uid_attribute}]\n\n' + '\n'.join(resources)
                + '\n\n' + '\n\n'.join(regions)
                + '\n\n[resource]\nanimations = [' + ',\n'.join(animations) + ']\n', encoding="utf-8")
            print(f"{folder}: {sum(c[2] for c in clips)} original rendered frames")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("java", "ffdec", "role", "monsters"):
        parser.add_argument(f"--{name}", type=Path, required=True)
    parser.add_argument("--group", choices=("all", "level_up", "loot"), default="all")
    args = parser.parse_args()
    export(args.java, args.ffdec, args.role, args.monsters, args.group)
