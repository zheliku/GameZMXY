"""Render only official visual clips; no Flash program is copied into the game.

Role_v7.swf sprite 240 is BaseHero.levelupmc. Monster_v1.swf sprites
5/7/9 are white/blue/red auras. White is reused visually for the requested
experience pickup; in the original game that clip represents Musou instead.
Input SWFs stay outside the repository. Requires FFDec 26.3.0 and Java.
"""

import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import tempfile


def decode(source: Path, expected: str) -> bytes:
    """Restore the loader's byte rotation and reject a different asset revision."""
    content = source.read_bytes()
    if content[:3] not in (b"FWS", b"CWS", b"ZWS"):
        content = content[100:110] + content[:100] + content[110:]
    if hashlib.sha256(content).hexdigest() != expected:
        raise ValueError(f"Unexpected source revision: {source}")
    return content


def export(java: Path, ffdec: Path, role: Path, monsters: Path) -> None:
    """Copy rendered frames verbatim and generate external SpriteFrames resources."""
    game = Path(__file__).resolve().parents[2] / "Godot/GodotProject/TheGame"
    groups = [
        (role, "11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7",
         [(240, "level_up", 28, False)], "level_up"),
        (monsters, "5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a",
         [(9, "Soul", 19, True), (5, "Experience", 19, True), (7, "Mana", 19, True)], "loot"),
    ]
    with tempfile.TemporaryDirectory(prefix="gamezmxy-effects-") as temporary:
        temp = Path(temporary)
        for source, digest, clips, folder in groups:
            readable = temp / f"{folder}.swf"
            readable.write_bytes(decode(source, digest))
            subprocess.run([str(java), "-jar", str(ffdec), "-selectid",
                            ",".join(str(c[0]) for c in clips), "-ignorebackground",
                            "-export", "sprite", temporary, str(readable)], check=True)
            destination = game / "Sprites/Effects" / folder
            destination.mkdir(parents=True, exist_ok=True)
            resources, animations = [], []
            for sprite_id, name, count, loop in clips:
                rendered = next(temp.glob(f"DefineSprite_{sprite_id}_*"))
                frames = []
                for frame in range(1, count + 1):
                    asset = destination / f"{name.lower()}_{frame:02}.png"
                    shutil.copyfile(rendered / f"{frame}.png", asset)
                    key = f"{name}_{frame}"
                    resources.append(f'[ext_resource type="Texture2D" path="res://TheGame/{asset.relative_to(game).as_posix()}" id="{key}"]')
                    frames.append('{"duration": 1.0, "texture": ExtResource("' + key + '")}')
                animations.append('{"frames": [' + ',\n'.join(frames) + '], "loop": '
                                  + str(loop).lower() + ', "name": &"' + name + '", "speed": 24.0}')
            (destination / f"{folder}_frames.tres").write_text(
                '[gd_resource type="SpriteFrames" format=3]\n\n' + '\n'.join(resources)
                + '\n\n[resource]\nanimations = [' + ',\n'.join(animations) + ']\n', encoding="utf-8")
            print(f"{folder}: {sum(c[2] for c in clips)} original rendered frames")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("java", "ffdec", "role", "monsters"):
        parser.add_argument(f"--{name}", type=Path, required=True)
    args = parser.parse_args()
    export(args.java, args.ffdec, args.role, args.monsters)
