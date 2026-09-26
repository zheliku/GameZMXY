#!/usr/bin/env python3
"""One-time migration tool: legacy .tscn animation data -> standard SpriteFrames .tres.

Source : P:/Godot_Project/ZMXY_BHYH (read-only legacy project)
Target : TheGame/Sprites/.../<entity>_animations.tres

Rationale: AGENTS.md 9.3 item 4 -- do not re-slice sheets by hand; derive frame
sequences from the legacy scene data once, then commit the generated .tres.

Run:  python Tools/LegacyMigration/gen_animations.py
"""

import os
import re

LEGACY = r"P:\Godot_Project\ZMXY_BHYH"
SPRITES = r"P:\Godot_Project\GameZMXY\Godot\GodotProject\TheGame\Sprites"


# --------------------------------------------------------------------------
# SpriteFrames .tres writer (supports one texture per animation)
# --------------------------------------------------------------------------
def emit_spriteframes(out_path, anims):
    """anims: list of dict(name, loop, tex, fw, fh, cols, frames=[(idx,dur)])"""
    tex_ids = {}
    ext_lines = []
    for a in anims:
        if a["tex"] not in tex_ids:
            tid = f"tex{len(tex_ids)}"
            tex_ids[a["tex"]] = tid
            ext_lines.append(
                f'[ext_resource type="Texture2D" path="{a["tex"]}" id="{tid}"]'
            )

    atlas_ids = {}
    sub_lines = []
    for a in anims:
        tid = tex_ids[a["tex"]]
        fw, fh, cols = a["fw"], a["fh"], a["cols"]
        for idx, _ in a["frames"]:
            key = (a["tex"], idx)
            if key in atlas_ids:
                continue
            aid = f"Atlas_{len(atlas_ids)}"
            atlas_ids[key] = aid
            col, row = idx % cols, idx // cols
            sub_lines.append(f'[sub_resource type="AtlasTexture" id="{aid}"]')
            sub_lines.append(f'atlas = ExtResource("{tid}")')
            sub_lines.append(f"region = Rect2({col * fw}, {row * fh}, {fw}, {fh})")
            sub_lines.append("")

    blocks = []
    for a in anims:
        body = ", ".join(
            '{\n"duration": %.6f,\n"texture": SubResource("%s")\n}'
            % (dur, atlas_ids[(a["tex"], idx)])
            for idx, dur in a["frames"]
        )
        blocks.append(
            '{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": 1.0\n}'
            % (body, "true" if a["loop"] else "false", a["name"])
        )

    steps = len(ext_lines) + len(atlas_ids) + 1
    lines = [f'[gd_resource type="SpriteFrames" load_steps={steps} format=3]', ""]
    lines += ext_lines
    lines.append("")
    lines += sub_lines
    lines.append("[resource]")
    lines.append("animations = [%s]" % ", ".join(blocks))
    lines.append("")

    with open(out_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    n_frames = sum(len(a["frames"]) for a in anims)
    print(f"  wrote {os.path.basename(out_path)}  ({len(anims)} anims, {n_frames} frames)")


# --------------------------------------------------------------------------
# Legacy Animation-track parsing
# --------------------------------------------------------------------------
ANIM_SPLIT = re.compile(r'(?=\[sub_resource type="Animation")')
TRACK_SPLIT = re.compile(r'(?=tracks/\d+/\w+)')
TIMES_RE = re.compile(r'"times":\s*PackedFloat32Array\(([^)]*)\)')
VALUES_RE = re.compile(r'"values":\s*\[([^\]]*)\]')

SPEED_SCALE = 2.0  # legacy RolePlayer.speed_scale


def _chunk_for_track(chunk, path_expr):
    """Return the `tracks/N/keys` segment whose sibling path equals path_expr."""
    m = re.search(r'tracks/(\d+)/path = NodePath\("' + re.escape(path_expr) + r'"\)', chunk)
    if not m:
        return None
    marker = f"tracks/{m.group(1)}/keys"
    for part in TRACK_SPLIT.split(chunk):
        if part.startswith(marker):
            return part
    return None


def parse_animations(text, track_path):
    """Return {resource_name: [(frame, duration), ...]} for one track path."""
    result = {}
    for chunk in ANIM_SPLIT.split(text):
        if not chunk.startswith('[sub_resource type="Animation"'):
            continue
        end = re.search(r'\n\[sub_resource|\n\[node', chunk)
        if end:
            chunk = chunk[: end.start()]

        name_m = re.search(r'resource_name = "([^"]+)"', chunk)
        if not name_m:
            continue

        keys = _chunk_for_track(chunk, track_path)
        if keys is None:
            continue

        times_m = TIMES_RE.search(keys)
        values_m = VALUES_RE.search(keys)
        if not times_m or not values_m:
            continue

        times = [float(t) for t in times_m.group(1).split(",") if t.strip()]
        values = [int(v) for v in values_m.group(1).split(",") if v.strip()]
        if not times or not values:
            continue

        frames = []
        for i in range(min(len(times), len(values))):
            nxt = times[i + 1] if i + 1 < len(times) else times[i] + 0.1
            dur = max((nxt - times[i]) / SPEED_SCALE, 1.0 / 60.0)
            frames.append((values[i], dur))
        result[name_m.group(1)] = dedupe(frames)
    return result


def dedupe(frames):
    out = []
    for idx, dur in frames:
        if out and out[-1][0] == idx:
            out[-1] = (idx, out[-1][1] + dur)
        else:
            out.append((idx, dur))
    return out


def _mk_anim(name, loop, tex, fw, fh, cols, frames):
    return {
        "name": name,
        "loop": loop,
        "tex": tex,
        "fw": fw,
        "fh": fh,
        "cols": cols,
        "frames": frames,
    }


# --------------------------------------------------------------------------
# wukong: Role1.tscn body grid 6x14 of 200x200
# --------------------------------------------------------------------------
WUKONG_MAP = {
    "wait": ("idle", True),
    "run": ("run", True),
    "jump1": ("jump", False),
    "hit1": ("attack_1", False),
    "hit2": ("attack_2", False),
    "hit3": ("attack_3", False),
    "hit4": ("attack_4", False),
    "hurt": ("hurt", False),
    "death": ("death", False),
}

WUKONG_TEX = "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_body.png"

# Weapon layer sheets (same 6x14 grid): legacy file -> semantic new file.
# Legacy keys traced in Docs/LegacyAssetMap.md; names follow AGENTS 8.2.
WUKONG_WEAPONS = [
    ("Role_1_Eq_Empty.png", "wukong_weapon_empty.png"),
    ("Role_1_Eq_ryjgb.png", "wukong_weapon_golden_cudgel.png"),
    ("Role_1_Eq_wkjdyhwq.png", "wukong_weapon_wooden_stick.png"),
    ("Role_1_Eq_qld.png", "wukong_weapon_dragon_blade.png"),
    ("Role_1_Eq_zjbtg.png", "wukong_weapon_purple_gold_cudgel.png"),
]


def gen_wukong_weapons():
    src = os.path.join(LEGACY, "Scene", "Hero", "Role_1", "Role1.tscn")
    with open(src, encoding="utf-8") as fh:
        text = fh.read()
    body = parse_animations(text, "Action/RoleBody:frame")
    equip = parse_animations(text, "Action/RoleEquipment:frame")

    out_dir = os.path.join(SPRITES, "Characters", "Heroes", "wukong")
    for _legacy, new_file in WUKONG_WEAPONS:
        tex = "res://TheGame/Sprites/Characters/Heroes/wukong/" + new_file
        anims = []
        for old, (new, loop) in WUKONG_MAP.items():
            frames = equip.get(old) or body.get(old)
            if frames:
                anims.append(_mk_anim(new, loop, tex, 200, 200, 6, frames))
        stem = os.path.splitext(new_file)[0]
        emit_spriteframes(os.path.join(out_dir, stem + "_animations.tres"), anims)


def gen_wukong():
    src = os.path.join(LEGACY, "Scene", "Hero", "Role_1", "Role1.tscn")
    with open(src, encoding="utf-8") as fh:
        text = fh.read()
    legacy = parse_animations(text, "Action/RoleBody:frame")

    anims = []
    for old, (new, loop) in WUKONG_MAP.items():
        if old in legacy:
            anims.append(_mk_anim(new, loop, WUKONG_TEX, 200, 200, 6, legacy[old]))
        else:
            print(f"  [warn] legacy animation missing: {old}")

    out = os.path.join(SPRITES, "Characters", "Heroes", "wukong", "wukong_animations.tres")
    emit_spriteframes(out, anims)


# --------------------------------------------------------------------------
# huaguoshan_monkey: fixed-grid sheets, one file per action, merged into one .tres
# --------------------------------------------------------------------------
MONKEY_DIR = "res://TheGame/Sprites/Characters/Monsters/huaguoshan_monkey/"
MONKEY_SHEETS = [
    # (animation, file, frame_w, frame_h, count, loop)
    ("idle", "huaguoshan_monkey_idle.png", 43, 63, 1, True),
    ("run", "huaguoshan_monkey_run.png", 64, 88, 4, True),
    ("attack_1", "huaguoshan_monkey_attack.png", 91, 83, 6, False),
    ("hurt", "huaguoshan_monkey_hurt.png", 68, 86, 2, False),
    ("death", "huaguoshan_monkey_death.png", 91, 88, 6, False),
]


def gen_monkey():
    anims = [
        _mk_anim(name, loop, MONKEY_DIR + fname, fw, fh, count, [(i, 0.1) for i in range(count)])
        for name, fname, fw, fh, count, loop in MONKEY_SHEETS
    ]
    out = os.path.join(
        SPRITES, "Characters", "Monsters", "huaguoshan_monkey", "huaguoshan_monkey_animations.tres"
    )
    emit_spriteframes(out, anims)


if __name__ == "__main__":
    print("wukong body:")
    gen_wukong()
    print("wukong weapons:")
    gen_wukong_weapons()
    print("huaguoshan_monkey:")
    gen_monkey()
