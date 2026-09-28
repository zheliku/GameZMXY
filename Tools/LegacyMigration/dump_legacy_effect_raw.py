#!/usr/bin/env python3
"""旧 Role1.tscn 的节点与特效 SpriteFrames 的"原始数据"核对（不做任何推断）。

用途：特效层位置/帧数出错时，直接看旧场景里到底是什么，
避免"按语义猜"（例如把 null 空白帧当成不存在、漏掉第二个特效层）。

Usage: python Tools/LegacyMigration/dump_legacy_effect_raw.py
"""

import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

SRC = r"P:\Godot-Project\ZMXY_BHYH\Scene\Hero\Role_1\Role1.tscn"


def dump_nodes(text):
    print("=" * 78)
    print("A. 场景节点（Action 层栈里的可视节点 + 特效节点）")
    print("=" * 78)
    for m in re.finditer(r'^\[node name="([^"]+)" type="([^"]+)"(?: parent="([^"]*)")?([^\]]*)\]', text, re.M):
        name, ntype, parent, rest = m.group(1), m.group(2), m.group(3) or "(root)", m.group(4)
        body_start = m.end()
        body_end = text.find("\n[", body_start)
        body = text[body_start:body_end].strip()
        if ntype not in ("AnimatedSprite2D", "Sprite2D", "Node2D", "AnimationPlayer"):
            continue
        keep = [
            line for line in body.splitlines()
            if re.match(r'\s*(position|scale|offset|centered|flip_h|z_index|sprite_frames|animation|autoplay)\s*=', line)
        ]
        print(f"\n[{name}] type={ntype} parent={parent} {rest.strip()}")
        for line in keep:
            print("   " + line.strip())
        if not keep and ntype in ("AnimatedSprite2D", "Sprite2D"):
            print("   (未显式设置：position=(0,0) scale=(1,1) offset=(0,0) centered=true)")


def dump_effect_frames(text):
    print()
    print("=" * 78)
    print("B. 特效 SpriteFrames 的逐帧条目（含 null 空白帧，按出现顺序）")
    print("=" * 78)

    for sid in ("276", "125"):
        block = re.search(
            r'\[sub_resource type="SpriteFrames" id="%s"\](.*?)(?=\n\[sub_resource|\n\[node)' % sid,
            text, re.S,
        )
        if not block:
            print(f"\n(SubResource {sid} 未找到)")
            continue
        print(f"\n---- SubResource {sid} ----")
        for blk in re.split(r'(?=\{\s*"frames")', block.group(1)):
            nm = re.search(r'"name": &"([^"]+)"', blk)
            if not nm:
                continue
            loop = re.search(r'"loop": (\w+)', blk)
            speed = re.search(r'"speed": ([\d.]+)', blk)
            entries = re.findall(r'"texture": (SubResource\(\s*"?(\d+)"?\s*\)|null)', blk)
            seq = [("null" if e[0] == "null" else f"sub{e[1]}") for e in entries]
            print(f"   {nm.group(1):10s} n={len(seq):2d} loop={loop.group(1) if loop else '?':5s} "
                  f"speed={speed.group(1) if speed else '?'}  entries={seq}")


def dump_track_paths(text):
    print()
    print("=" * 78)
    print("C. 每个动画里与特效相关的轨道 path（确认哪些动画驱动哪个特效层）")
    print("=" * 78)
    for chunk in re.split(r'(?=\[sub_resource type="Animation")', text):
        if not chunk.startswith('[sub_resource type="Animation"'):
            continue
        m = re.search(r'resource_name = "([^"]+)"', chunk)
        if not m:
            continue
        end = re.search(r"\n\[sub_resource|\n\[node", chunk)
        chunk = chunk[: end.start()] if end else chunk
        paths = sorted(set(re.findall(r'NodePath\("([^"]*(?:RoleBody|RoleEquipment|SpecialEffect)[^"]*)"\)', chunk)))
        if paths:
            print(f"   {m.group(1):10s} {paths}")


if __name__ == "__main__":
    text = open(SRC, encoding="utf-8").read()
    dump_nodes(text)
    dump_effect_frames(text)
    dump_track_paths(text)
