#!/usr/bin/env python3
"""旧项目 Role1.tscn 的"棍气特效层 + 音效触发"数据解析。

旧项目结构（取证结论）：
- 棍气 = 场景里的第三个 AnimatedSprite2D 节点 `Action/SpecialEffect`，
  SpriteFrames 用独立特效图集（Art/HeroPicture/Role1SpecialEffect/Role1Hit1..4.png 等）。
- 旧 AnimationPlayer 的每个攻击动画里有四类轨道同步驱动它：
    Action/SpecialEffect:animation  （切换特效动画名）
    Action/SpecialEffect:frame      （逐帧）
    Action/SpecialEffect:offset     （特效相对身体的位置）
    Action/SpecialEffect:scale
- 音效 = method 轨道调脚本 add_music(idx)，idx 映射到 Music/Hero/*.mp3。

本工具输出：每个动画的 add_music 触发点、SpecialEffect 各轨道 keys，
以及特效 SpriteFrames 的动画→图集区域映射。

Usage: python Tools/LegacyMigration/dump_legacy_effect_tracks.py
"""

import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

SRC = r"P:\Godot-Project\ZMXY_BHYH\Scene\Hero\Role_1\Role1.tscn"

ANIM_SPLIT = re.compile(r'(?=\[sub_resource type="Animation")')
TRACK_SPLIT = re.compile(r'(?=tracks/\d+/\w+)')
LENGTH_RE = re.compile(r'^length = ([\d.]+)', re.M)
KEYS_RE = re.compile(r'tracks/\d+/keys = \{(.*?)\n\}', re.S)
EXT_RES_RE = re.compile(
    r'\[ext_resource type="Texture2D"[^\]]*path="([^"]+)" id="(\d+)"'
)


def _cut(chunk):
    m = re.search(r"\n\[sub_resource|\n\[node", chunk)
    return chunk[: m.start()] if m else chunk


def _keys_for(chunk, path_expr):
    """取某条轨道的 keys 段文本（含 times/values）。"""
    for part in TRACK_SPLIT.split(chunk):
        if f'NodePath("{path_expr}")' in part:
            rest = chunk[chunk.index(part):]
            m = KEYS_RE.search(rest)
            if m:
                return m.group(1)
    return None


def dump_effect_spriteframes(text):
    """特效层 SpriteFrames：动画名 → [(图集文件, region)]。"""
    print("=" * 70)
    print("A. 特效 SpriteFrames（Role1SpecialEffect 图集引用）")
    print("=" * 70)
    ext = {m.group(2): m.group(1) for m in EXT_RES_RE.finditer(text)}
    fx_ext = {k: v for k, v in ext.items() if "Role1SpecialEffect" in v}
    if not fx_ext:
        print("(未找到 Role1SpecialEffect 引用)")
        return
    ext = {k: v.rsplit("/", 1)[-1] for k, v in ext.items()}

    # 第一层：AtlasTexture id → (图集文件名, region)
    atlas = {}
    for m in re.finditer(
        r'\[sub_resource type="AtlasTexture" id="(\d+)"\]\s*\n'
        r'atlas = ExtResource\(\s*"?(\d+)"?\s*\)\s*\n'
        r'region = Rect2\(([^)]+)\)',
        text,
    ):
        rid, eid, rect = m.group(1), m.group(2), m.group(3)
        atlas[rid] = (fx_ext.get(eid, ext.get(eid, f"ext{eid}")), rect.strip())

    # 第二层：SpriteFrames 动画 → frame 序列 → atlas
    for chunk in re.split(r'(?=\[sub_resource type="SpriteFrames")', text):
        if not chunk.startswith('[sub_resource type="SpriteFrames"'):
            continue
        chunk = _cut(chunk)
        sid = re.search(r'\[sub_resource type="SpriteFrames" id="(\d+)"', chunk).group(1)
        # 动画块按 "name": &"xxx" 分段
        blocks = re.split(r'(?=\{\s*"frames")', chunk)
        rows = []
        for blk in blocks:
            nm = re.search(r'"name": &"([^"]+)"', blk)
            if not nm:
                continue
            frames = []
            for fm in re.finditer(r'"texture": SubResource\(\s*"?(\d+)"?\s*\)', blk):
                info = atlas.get(fm.group(1))
                frames.append(f"{info[0]}@({info[1]})" if info else f"sub{fm.group(1)}")
            if frames:
                rows.append((nm.group(1), frames))
        if rows:
            print(f"\n-- SubResource {sid}")
            for name, frames in rows:
                print(f"   {name:12s} {len(frames)}帧")
                for f in frames:
                    print(f"        {f}")


def dump_anim_tracks(text):
    print()
    print("=" * 70)
    print("B. 各动画的 add_music 音效触发 + SpecialEffect 特效轨道")
    print("=" * 70)
    for chunk in ANIM_SPLIT.split(text):
        if not chunk.startswith('[sub_resource type="Animation"'):
            continue
        chunk = _cut(chunk)
        nm = re.search(r'resource_name = "([^"]+)"', chunk)
        if not nm:
            continue
        name = nm.group(1)
        len_m = LENGTH_RE.search(chunk)
        length = float(len_m.group(1)) if len_m else 1.0

        music = None
        fx_anim = _keys_for(chunk, "Action/SpecialEffect:animation")
        fx_frame = _keys_for(chunk, "Action/SpecialEffect:frame")
        fx_offset = _keys_for(chunk, "Action/SpecialEffect:offset")
        fx_scale = _keys_for(chunk, "Action/SpecialEffect:scale")

        # method 轨道：按"每条轨道"分块（type→keys 同块），找 path="." 且方法名 add_music 的
        for part in re.split(r'(?=tracks/\d+/type)', chunk):
            if 'NodePath(".")' not in part or '"method": &"add_music"' not in part:
                continue
            m = KEYS_RE.search(part)
            if m:
                music = m.group(1)

        if not any([fx_anim, fx_frame, fx_offset, fx_scale, music]):
            continue

        print(f"\n=== {name}  (length={length}) ===")
        if music:
            print(f"  add_music: {music.strip()}")
        for label, keys in (("animation", fx_anim), ("frame", fx_frame),
                            ("offset", fx_offset), ("scale", fx_scale)):
            if keys is None:
                continue
            compact = " ".join(keys.split())
            print(f"  fx.{label:9s}: {compact}")


def main():
    text = open(SRC, encoding="utf-8").read()
    dump_effect_spriteframes(text)
    dump_anim_tracks(text)


if __name__ == "__main__":
    main()
