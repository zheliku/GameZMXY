#!/usr/bin/env python3
"""核对旧项目某个 AnimatedSprite 的 SpriteFrames 条目结构。

为什么需要：旧动画的 `Action/RoleBody:frame` 轨道值是"**动画条目序号**"，
不是图集格号。只有当条目表是"一格一条目、无 null"的恒等映射时，才能直接用
格号切图。本工具把每个动画的条目数、null 数、重复引用数、首末 region 打出来验证。

身体层定义在基类场景里（Role1.tscn 的 RoleBody 继承自 BaseHero.tscn），
所以默认目标就是 BaseHero.tscn 的 RoleBody。

Usage:
  python Tools/LegacyMigration/dump_legacy_body_frames.py [场景相对路径] [节点名]
  # 默认：Scene/Base/BaseHero.tscn RoleBody
"""

import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

LEGACY = r"P:\Godot_Project\ZMXY_BHYH"


def main():
    rel = sys.argv[1] if len(sys.argv) > 1 else r"Scene\Base\BaseHero.tscn"
    node_name = sys.argv[2] if len(sys.argv) > 2 else "RoleBody"
    src = LEGACY + "\\" + rel
    text = open(src, encoding="utf-8").read()
    print(f"源: {rel}  节点: {node_name}")

    # Godot 3 的 AnimatedSprite 用 frames=，Godot 4 用 sprite_frames=
    node = re.search(
        r'\[node name="%s"[^\]]*\]\s*\n((?:[^\n]*\n)*?)' % node_name,
        text,
    )
    if not node:
        print("未找到该节点")
        return
    block = node.group(1)
    m = re.search(r'(?:sprite_frames|frames) = SubResource\("(\d+)"\)', block)
    if not m:
        print(f"节点块内未设置 sprite_frames/frames；节点块内容：\n{block.strip()}")
        return
    sid = m.group(1)
    print(f"{node_name}.frames = SubResource({sid})")

    sf = re.search(
        r'\[sub_resource type="SpriteFrames" id="%s"\](.*?)(?=\n\[sub_resource|\n\[node)' % sid,
        text,
        re.S,
    ).group(1)

    atlas = {
        mm.group(1): tuple(float(v) for v in mm.group(3).split(","))
        for mm in re.finditer(
            r'\[sub_resource type="AtlasTexture" id="(\d+)"\]\s*\n'
            r'atlas = ExtResource\(\s*"?(\d+)"?\s*\)\s*\n'
            r'region = Rect2\(([^)]+)\)',
            text,
        )
    }

    print(f"{'动画':10s} {'条目数':>5s} {'null':>5s} {'重复':>5s}  首个 / 末个 region")
    for b in re.split(r'(?=\{\s*"frames")', sf):
        nm = re.search(r'"name": &"([^"]+)"', b)
        if not nm:
            continue
        entries = re.findall(r'"texture": (SubResource\(\s*"?(\d+)"?\s*\)|null)', b)
        subs = [e[1] for e in entries if e[0] != "null"]
        nulls = len(entries) - len(subs)
        dup = len(subs) - len(set(subs))
        first = atlas.get(subs[0], "?") if subs else "-"
        last = atlas.get(subs[-1], "?") if subs else "-"
        print(f"{nm.group(1):10s} {len(entries):5d} {nulls:5d} {dup:5d}  {first} / {last}")


if __name__ == "__main__":
    main()
