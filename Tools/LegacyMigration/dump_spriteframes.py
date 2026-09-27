#!/usr/bin/env python3
"""Dump a SpriteFrames .tres: per-animation loop/speed/frame-count/frame-indices.

Why: the frame indices tell us exactly which cells of the sheet an animation uses,
which is what we need when folding hand-authored animations back into the generator
and when deriving the weapon layer from the body layer.

Usage: python Tools/LegacyMigration/dump_spriteframes.py <path-to.tres> [frame_w frame_h cols]
"""

import os
import re
import sys


def main():
    path = sys.argv[1]
    fw = int(sys.argv[2]) if len(sys.argv) > 2 else 200
    fh = int(sys.argv[3]) if len(sys.argv) > 3 else 200
    cols = int(sys.argv[4]) if len(sys.argv) > 4 else 6

    text = open(path, encoding="utf-8").read()

    # AtlasTexture id -> frame index (derived from its region)
    atlas = {}
    for m in re.finditer(
        r'\[sub_resource type="AtlasTexture" id="([^"]+)"\]\s*\n[^\n]*\nregion = Rect2\((\d+), (\d+), (\d+), (\d+)\)',
        text,
    ):
        aid, x, y, _w, _h = m.groups()
        atlas[aid] = int(x) // fw + (int(y) // fh) * cols

    # external textures referenced (body / weapon sheets)
    print("textures:")
    for m in re.finditer(r'\[ext_resource type="Texture2D"[^\]]*path="([^"]+)"', text):
        print("  ", m.group(1))

    print(f"atlas subresources: {len(atlas)}")

    body = text.split("[resource]", 1)[1] if "[resource]" in text else text
    print("animations:")
    for m in re.finditer(
        r'"frames": \[(.*?)\],\s*"loop": (\w+),\s*"name": &"([^"]+)",\s*"speed": ([\d.]+)',
        body,
        re.S,
    ):
        frames, loop, name, speed = m.groups()
        ids = re.findall(r'SubResource\("([^"]+)"\)', frames)
        durs = re.findall(r'"duration": ([\d.]+)', frames)
        idx = [atlas.get(i, -1) for i in ids]
        print(f"  {name:14s} loop={loop:5s} speed={speed:5s} n={len(idx):3d} dur={durs} idx={idx}")


if __name__ == "__main__":
    main()
