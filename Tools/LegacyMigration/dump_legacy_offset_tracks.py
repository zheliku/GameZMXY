#!/usr/bin/env python3
"""旧 Role1.tscn：逐动画 dump 所有 offset 轨道（身体/武器/特效）与特效帧/缩放轨道。

用于核对"位移类"轨道是否被迁移 —— 这些不是 SpriteFrames 能表达的，
必须显式看到数值才能决定表示法。

Usage: python Tools/LegacyMigration/dump_legacy_offset_tracks.py [动画名]
"""

import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

SRC = r"P:\Godot-Project\ZMXY_BHYH\Scene\Hero\Role_1\Role1.tscn"

TRACK_SPLIT = re.compile(r'(?=tracks/\d+/type)')
KEYS_RE = re.compile(r'tracks/\d+/keys = \{(.*?)\n\}', re.S)
TIMES_RE = re.compile(r'"times":\s*PackedFloat32Array\(([^)]*)\)')
VALUES_RE = re.compile(r'"values":\s*\[([^\]]*)\]')
LENGTH_RE = re.compile(r'^length = ([\d.]+)', re.M)


def track_keys(chunk, path_expr):
    for part in TRACK_SPLIT.split(chunk):
        if f'NodePath("{path_expr}")' not in part:
            continue
        m = KEYS_RE.search(part)
        if m:
            return m.group(1)
    return None


def compact(keys):
    if keys is None:
        return "(无)"
    t = TIMES_RE.search(keys)
    v = VALUES_RE.search(keys)
    ts = t.group(1).strip() if t else "?"
    vs = " ".join(v.group(1).split()) if v else "?"
    return f"times=[{ts}] values=[{vs}]"


def main():
    text = open(SRC, encoding="utf-8").read()
    want = sys.argv[1] if len(sys.argv) > 1 else None

    for chunk in re.split(r'(?=\[sub_resource type="Animation")', text):
        if not chunk.startswith('[sub_resource type="Animation"'):
            continue
        m = re.search(r'resource_name = "([^"]+)"', chunk)
        if not m:
            continue
        name = m.group(1)
        if want and name != want:
            continue
        end = re.search(r"\n\[sub_resource|\n\[node", chunk)
        chunk = chunk[: end.start()] if end else chunk
        lm = LENGTH_RE.search(chunk)
        length = lm.group(1) if lm else "1.0"

        print(f"\n=== {name} (length={length}) ===")
        for expr in (
            "Action/RoleBody:offset",
            "Action/RoleEquipment:offset",
            "Action/SpecialEffect:animation",
            "Action/SpecialEffect:frame",
            "Action/SpecialEffect:offset",
            "Action/SpecialEffect:scale",
        ):
            keys = track_keys(chunk, expr)
            if keys is None:
                continue
            short = expr.split("/")[-1]
            print(f"  {short:22s} {compact(keys)}")


if __name__ == "__main__":
    main()
