#!/usr/bin/env python3
"""One-time migration tool: legacy .tscn animation data -> standard SpriteFrames .tres.

Source : P:/Godot_Project/ZMXY_BHYH (read-only legacy project)
Target : TheGame/Sprites/.../<entity>_animations.tres

Rationale: AGENTS.md 9.3 item 4 -- do not re-slice sheets by hand; derive frame
sequences from the legacy scene data once, then commit the generated .tres.

动画来源分两类：
  1. WUKONG_MAP      —— 旧项目 Role1.tscn 里真实存在的 21 个动画，帧序列由
                        Action/RoleBody:frame 轨道解析得到（时序也取自轨道）。
  2. WUKONG_BODY_ONLY —— 旧项目从未使用、但素材里确实有的动作（原版有、旧项目缺）。
                        帧号是从 6x14 网格上人工核对的，写在下面。
武器层（RoleEquipment 图集）与身体层共用同一张 6x14 网格，所以武器动画
**直接复用身体层的帧序列**：名称与帧一一对应，不存在两套映射走偏的可能。

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
    """anims: list of dict(name, loop, tex, fw, fh, cols, frames=[(idx,dur)], speed)"""
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
            '{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": %s\n}'
            % (body, "true" if a["loop"] else "false", a["name"], a.get("speed", 1.0))
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
LENGTH_RE = re.compile(r'^length = ([\d.]+)', re.M)
KEYS_RE = re.compile(r'tracks/\d+/keys = \{(.*?)\n\}', re.S)

SPEED_SCALE = 2.0  # RolePlayer 节点上的初始 speed_scale（仅在没有 speed_scale 轨道时兜底）


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


def _keys_for(chunk, path_expr):
    """取某条轨道的 keys 段文本（含 times/values）；没有则 None。"""
    for part in TRACK_SPLIT.split(chunk):
        if f'NodePath("{path_expr}")' in part:
            rest = chunk[chunk.index(part):]
            m = KEYS_RE.search(rest)
            if m:
                return m.group(1)
    return None


def _scale_segments(chunk, length):
    """把 speed_scale 轨道切成 [(起, 止, 速度)]，覆盖 [0, length]；没有轨道则恒定 SPEED_SCALE。"""
    fallback = [(0.0, length, SPEED_SCALE)]
    keys = _keys_for(chunk, "RolePlayer:speed_scale")
    if keys is None:
        return fallback

    times_m = TIMES_RE.search(keys)
    values_m = VALUES_RE.search(keys)
    if not times_m or not values_m:
        return fallback

    times = [float(t) for t in times_m.group(1).split(",") if t.strip()]
    values = [float(v) for v in values_m.group(1).split(",") if v.strip()]
    if not times or not values:
        return fallback

    segs = []
    if times[0] > 0:
        segs.append((0.0, times[0], values[0]))
    for i, t in enumerate(times):
        nxt = times[i + 1] if i + 1 < len(times) else length
        v = values[i] if i < len(values) else values[-1]
        segs.append((t, nxt, v if v > 0 else SPEED_SCALE))
    return segs


def _real_time(segs, t):
    """内部时间 t 对应的真实秒数：∫₀^t dt' / speed_scale(t')。"""
    real = 0.0
    for start, end, v in segs:
        if t <= start:
            break
        real += (min(t, end) - start) / v
    return real


def parse_animations(text, track_path):
    """返回 {resource_name: {"frames": [(帧号, 时长), ...], "length": 秒}}。

    length 是 Animation 自己的总时长（还要再除以 RolePlayer.speed_scale 才是真实秒数）；
    它通常大于 frame 轨道的键跨距 —— 末尾"保持末帧"的收招时间不在 frame 轨道里。
    """
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

        len_m = LENGTH_RE.search(chunk)

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

        # 逐帧真实时长：把该帧的内部起止时间分别换算成真实秒数再相减。
        # 这样"起手慢、挥出去快"这类 speed_scale 变化会被原样保留 —— 手感复刻的关键。
        length = float(len_m.group(1)) if len_m else 1.0
        segs = _scale_segments(chunk, length)
        frames = []
        for i in range(min(len(times), len(values))):
            t0 = times[i]
            t1 = times[i + 1] if i + 1 < len(times) else length
            dur = max(_real_time(segs, t1) - _real_time(segs, t0), 1.0 / 60.0)
            frames.append((values[i], dur))
        if name_m.group(1) in result:
            print(f"  [warn] 旧项目存在同名动画，取后一条（AnimationLibrary 行为）：{name_m.group(1)}")

        result[name_m.group(1)] = {"frames": dedupe(frames)}
    return result


def dedupe(frames):
    out = []
    for idx, dur in frames:
        if out and out[-1][0] == idx:
            out[-1] = (idx, out[-1][1] + dur)
        else:
            out.append((idx, dur))
    return out


def _mk_anim(name, loop, tex, fw, fh, cols, frames, speed=1.0):
    return {
        "name": name,
        "loop": loop,
        "tex": tex,
        "fw": fw,
        "fh": fh,
        "cols": cols,
        "frames": frames,
        "speed": speed,
    }


# --------------------------------------------------------------------------
# wukong: Role1.tscn body grid 6x14 of 200x200
# --------------------------------------------------------------------------
WUKONG_MAP = {
    # ---- 待机 / 移动 ----
    "wait": ("idle1", True),      # 原版待机（偶发憨笑见 WUKONG_BODY_ONLY.idle2）
    "run": ("run", True),
    "jump1": ("jump", False),
    "jump2": ("jump_2", False),
    "drop": ("fall", False),
    # ---- 普攻连段（旧 objattackDic hit1..hit4）----
    "hit1": ("attack_1", False),
    "hit2": ("attack_2", False),
    "hit3": ("attack_3", False),
    "hit4": ("attack_4", False),
    # ---- 受击 / 死亡 ----
    "hurt": ("hurt", False),
    "death": ("death", False),
    # ---- 技能（旧 Role1.gd 的 do_* 一一对应；skillId 待 SkillConfig 建立后回填）----
    "slz": ("skill_slz", False),
    "hytj": ("skill_hytj", False),
    "lys": ("skill_lys", False),
    "lyfb": ("skill_lyfb", False),
    "hmz__": ("skill_hmz", False),
    "hyjj": ("skill_hyjj", False),
    "jdy": ("skill_jdy", False),
    "jdy_1": ("skill_jdy_2", False),
    "qsez": ("skill_qsez", False),
    "zz": ("skill_zz", False),
}

# 旧项目从未使用、但素材里确实存在的动作（帧号在 6x14 网格上核对过）。
# walk / idle2 这两个是"原版有、旧项目缺"。
#
# idle2（憨笑）素材只有 2 帧，太短，用**重复帧**编排节奏：
# 生成器只需改这一行，武器层是派生出来的（gen_wukong_weapons 复用同一列表），会自动同步。
WUKONG_BODY_ONLY = [
    # 憨笑待机：6/7 交替 4 轮 ≈ 0.8s，播一次即回 idle1（所以非循环）
    ("idle2", (6, 7, 6, 7, 6, 7, 6, 7), False),
    ("walk", (12, 13, 14, 15), True),    # 慢走
]
DEFAULT_FRAME_DURATION = 0.1   # 素材补充动作（walk / idle2）的默认逐帧时长
DEFAULT_FPS = 10.0             # 仅用于 WUKONG_ANIM_DEFAULTS 里写 "fps" 时的换算

# 手调覆盖表：列在这里的动画改用"统一帧时长"，覆盖旧项目算出的逐帧节奏。
# 不列在这里的动画一律使用旧项目轨道算出的**逐帧真实时长**（含 speed_scale 变化）。
WUKONG_ANIM_DEFAULTS = {
    # 慢走：每帧 0.2 秒（你定的手感；旧项目没有 walk，无参照）
    "walk": {"frame_duration": 0.2},
}

WUKONG_TEX_BODY = "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_body.png"
WUKONG_TEX_DIR = "res://TheGame/Sprites/Characters/Heroes/wukong/"
WUKONG_WEAPONS = [
    "wukong_weapon_empty.png",
    "wukong_weapon_golden_cudgel.png",
    "wukong_weapon_wooden_stick.png",
    "wukong_weapon_dragon_blade.png",
    "wukong_weapon_purple_gold_cudgel.png",
]


ANIM_SPEED = 1.0   # 动画级倍率固定 1.0：时序全部由逐帧 duration 表达（见文件头说明）


def _apply_override(name, frames):
    """WUKONG_ANIM_DEFAULTS 里若有该动画，改成统一帧时长（仅用于手调手感）。"""
    spec = WUKONG_ANIM_DEFAULTS.get(name)
    if not spec:
        return frames
    dur = spec.get("frame_duration", 1.0 / spec.get("fps", DEFAULT_FPS))
    return [(i, dur) for i, _ in frames]


def build_wukong_anims(tex):
    """
    按 WUKONG_MAP(旧项目轨道) + WUKONG_BODY_ONLY(素材补充) 组装动画列表。

    时序：直接采用旧项目轨道算出的**逐帧真实时长**（已按各动画自己的 speed_scale
    积分换算），不做任何抹平 —— 旧项目"起手慢、挥出去快"的节奏就靠它保留。
    WUKONG_ANIM_DEFAULTS 里列出的动画例外，改用统一帧时长（手调用）。
    """
    src = os.path.join(LEGACY, "Scene", "Hero", "Role_1", "Role1.tscn")
    with open(src, encoding="utf-8") as fh:
        text = fh.read()
    legacy = parse_animations(text, "Action/RoleBody:frame")

    anims = []
    for old, (new, loop) in WUKONG_MAP.items():
        info = legacy.get(old)
        if info is None:
            print(f"  [warn] WUKONG_MAP 里的旧动画在 Role1.tscn 中不存在：{old}")
            continue

        frames = _apply_override(new, info["frames"])
        anims.append(_mk_anim(new, loop, tex, 200, 200, 6, frames, ANIM_SPEED))

    # 反向检查：旧项目有、但映射表没覆盖的动画（保证"全迁"可验证，不靠人工记忆）
    for name in sorted(set(legacy) - set(WUKONG_MAP)):
        print(f"  [warn] 旧项目动画未纳入映射表：{name}")

    for name, idxs, loop in WUKONG_BODY_ONLY:
        base = [(i, DEFAULT_FRAME_DURATION) for i in idxs]
        frames = _apply_override(name, base)
        anims.append(_mk_anim(name, loop, tex, 200, 200, 6, frames, ANIM_SPEED))

    return anims


def gen_wukong():
    """身体层：按上表生成。"""
    out = os.path.join(SPRITES, "Characters", "Heroes", "wukong", "wukong_animations.tres")
    emit_spriteframes(out, build_wukong_anims(WUKONG_TEX_BODY))


def gen_wukong_weapons():
    """
    武器层：图集与身体共用同一张 6x14 网格，直接复用身体层的动画列表（含 FPS），
    只把贴图换成对应武器 —— 名称、帧、FPS 天然与身体层一一对应。
    """
    body = build_wukong_anims(WUKONG_TEX_BODY)
    out_dir = os.path.join(SPRITES, "Characters", "Heroes", "wukong")
    for weapon_file in WUKONG_WEAPONS:
        anims = []
        for a in body:
            clone = dict(a)
            clone["tex"] = WUKONG_TEX_DIR + weapon_file
            anims.append(clone)
        stem = os.path.splitext(weapon_file)[0]
        emit_spriteframes(os.path.join(out_dir, stem + "_animations.tres"), anims)


# --------------------------------------------------------------------------
# huaguoshan_monkey: fixed-grid sheets, one file per action
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
