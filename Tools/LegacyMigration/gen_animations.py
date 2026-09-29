#!/usr/bin/env python3
"""One-time migration tool: legacy .tscn animation data -> standard SpriteFrames .tres.

Source : P:/Godot-Project/ZMXY_BHYH (read-only legacy project)
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

LEGACY = r"P:\Godot-Project\ZMXY_BHYH"
SPRITES = r"P:\Godot-Project\GameZMXY\Godot\GodotProject\TheGame\Sprites"


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


# --------------------------------------------------------------------------
# huaguoshan_monkey AnimationLibrary（M4）：与悟空同构——AnimationPlayer 轨道驱动一切，AnimationTree 表达式选动画
#
# 旧 Monster_1.tscn 的 mr_player（speed_scale=1，内部时间 = 真实秒）用轨道驱动 AnimatedSprite2D mr_ani：
# animation / frame / offset + 判定开关 HitBox:disabled。SpriteFrames 条目是"重复图片撑时长"
# （hit1：f0×3, f1×3, f2..f5），frame 轨道逐条目推进（0.04s/条目）。
# 新工程 m_Body 是 AnimatedSprite2D（每个动作一张图集、帧尺寸各异，Sprite2D 网格表达不了），
# 复用 huaguoshan_monkey_animations.tres（每动画一图一帧），轨道显式设 animation/frame——与旧 mr_ani 同构。
# 旧条目"重复图片撑时长"在这里合并为同一帧的更长停留，时序与旧项目逐帧一致。
#   * offset：旧 mr_ani 默认面朝左，offset 随父节点 MonsterDir.scale.x 一起镜像；新工程由
#     ActorEntity 对非 Sprite2D 身体层用负 scale 镜像（offset 随之镜像），这里写"朝左"原值。
#   * 判定开关：旧 hit1 0.3~0.4s；其余动画恒关（轨道完备性，同悟空库）。
#   * idle：旧 wait 用 Wait.png 4 帧 0.2s，但新工程只迁移了 1 帧 idle 图（43×63）——保持 1 帧，
#     补全 idle 图集随 M5 素材补齐（LegacyAssetMap 已登记）。
# --------------------------------------------------------------------------
MONKEY_LIB_OUT = os.path.join(SPRITES, "Characters", "Monsters", "huaguoshan_monkey",
                              "huaguoshan_monkey_anim_library.tres")
MONKEY_BODY = "m_Body"
MONKEY_SPRITE_FRAMES = "m_Body:animation"

# 新动画名 → (旧 mr_player 动画 id 对应名, 图片帧序列[(帧, 秒)], 循环, 旧 offset, 判定开关键)
# 帧序列 = 旧 frame 轨道条目按 SpriteFrames 条目→图片帧展开并合并相邻重复（数据见 LegacyAssetMap.md）
MONKEY_ANIMS = [
    ("idle", [(0, 0.8)], True, (4, 0), [(0.0, True)]),
    ("run", [(0, 0.2), (1, 0.2), (2, 0.2), (3, 0.2)], True, (0, 0), [(0.0, True)]),
    # hit1：条目 f0×3(0.12s) f1×3(0.12s) f2..f5 各 0.04s，总长 0.4s；判定 0.30~0.40s
    ("attack_1", [(0, 0.12), (1, 0.12), (2, 0.04), (3, 0.04), (4, 0.04), (5, 0.04)], False, (-13, 0),
     [(0.0, True), (0.3, False), (0.4, True)]),
    # hurt：条目 f0×4(0.16s) f1×3(0.12s)，总长 0.28s
    ("hurt", [(0, 0.16), (1, 0.12)], False, (1.5, -0.5), [(0.0, True)]),
    # death：条目 0..5 各 0.0667s，之后停在末帧到 0.8s（旧 0.3~0.5s 渐隐，由 C# 回收时序替代）
    ("death", [(0, 0.0667), (1, 0.0667), (2, 0.0667), (3, 0.0667), (4, 0.0667), (5, 0.4665)], False, (4, 0),
     [(0.0, True)]),
]


# 猴子判定盒（同 WUKONG_HITBOX 的推导注释；猴子无武器层，直接沿用旧形状：
# 旧 Hit.png 判定窗 f3..f5 视觉范围 x[-43.5,18.5]，旧圆 r25@(-24,-10) 相对根原点在其内）
# 旧 hitbox 相对 BaseDamageBox(0,0) 在 (-24,-10)，旧贴图中心在根原点；新工程身体层在 (0,-13)，
# 所以 Y = -10 - 13 = -23（修复 2026-09-30：此前表值 -10 少减了身体层偏移，判定盒偏低 13px）。
MONKEY_HITBOX = {
    "attack_1": (50.0, 50.0, 24.0, -23.0),
}


def build_monkey_library():
    anims = []
    shape_res = {}
    for name, frames, loop, offset, hit_keys in MONKEY_ANIMS:
        keys, length = _frame_track_keys(frames)
        hit_track = (HITBOX_TRACK, "bool", hit_keys)
        tracks = [
            (MONKEY_SPRITE_FRAMES, "string", [(0.0, name)]),
            (MONKEY_BODY + ":frame", "int", keys),
            (MONKEY_BODY + ":offset", "vector2", [(0.0, (float(offset[0]), float(offset[1])))]),
            hit_track,
        ]
        # 判定盒几何（原生朝左坐标，容器 m_HitBoxRoot 负责镜像；猴子无武器层，沿用旧圆外接矩形）
        tracks.extend(_hitbox_geo_tracks(name, hit_track, MONKEY_HITBOX, MONKEY_HITBOX_REST, shape_res))
        # 出招生命周期（装填/收招）
        tracks.extend(_attack_lifecycle_tracks(name, length, MONKEY_HITBOX))
        anims.append({"name": name, "loop": loop, "length": length, "tracks": tracks})
    return anims, shape_res


def gen_monkey_library():
    emit_anim_library(MONKEY_LIB_OUT, *build_monkey_library())


# --------------------------------------------------------------------------
# wukong 棍气特效层（旧 Action/SpecialEffect 节点）—— 按旧机制 1:1 迁移
#
# 旧项目机制（Role1.tscn 实测，核对工具见 dump_legacy_effect_raw.py）：
#   - 棍气是 AnimatedSprite2D `Action/SpecialEffect`，父节点 Action 承担朝向镜像
#     （旧代码 action.scale.x = ±1，所以特效的 offset 也随朝向一起镜像）。
#   - 旧 AnimationPlayer 的每个攻击动画用四类**属性轨道**驱动它：
#       Action/SpecialEffect:animation  切换特效动画名（切换时 SpriteFrames 的 frame 归 0）
#       Action/SpecialEffect:frame      逐帧；值是"条目序号"，含 null 空白条目
#       Action/SpecialEffect:offset     特效相对身体原点的位移（绝对值，覆盖而非叠加）
#       Action/SpecialEffect:scale      缩放（普攻没有该轨道，沿用之前动画留下的 1）
#   - 特效 SpriteFrames 的动画**末尾是 null 条目**（hit1 6 条、hit2 7 条、hit3 6 条、hit4 3 条），
#     帧号打到末条 = 特效消失 —— 这是收招"棍气散掉"的来源。
#   - 时间轴是内部时间，真实秒 = 按 speed_scale 积分（与身体层同一套换算）。
#
# 新工程表示法（与旧一致，不做等价改写）：
#   - `wukong_effect_animations.tres`：SpriteFrames，条目序列（含 null）与旧逐条对应；
#     frame 由 AnimationPlayer 显式设置，故逐帧 duration 不参与播放（统一 1.0）。
#   - `wukong_effect_library.tres`：AnimationLibrary，动画名与身体层同名（attack_1..4 / empty），
#     轨道原样搬到 `m_EffectRoot/m_Effect:<属性>`，时间由内部时间换算成真实秒。
#   - 场景结构：m_EffectRoot(Node2D，朝向镜像 scale.x=±1) → m_Effect(AnimatedSprite2D)，
#     另有 m_EffectPlayer(AnimationPlayer) 承载这些轨道。
# --------------------------------------------------------------------------

WUKONG_FX_DIR = "res://TheGame/Sprites/Effects/wukong/"
FX_NODE = "m_EffectRoot/m_Effect"

# 旧 fx 动画名 → 新动画名（与身体层同名，状态机可同名播放）。技能特效随技能系统迁移。
WUKONG_FX_MAP = {
    "hit1": "attack_1",
    "hit2": "attack_2",
    "hit3": "attack_3",
    "hit4": "attack_4",
}
# 旧特效图集文件名 → 新文件名（见 Docs/LegacyAssetMap.md）
WUKONG_FX_SHEETS = {
    "Role1Hit1.png": "wukong_hit_1.png",
    "Role1Hit2.png": "wukong_hit_2.png",
    "Role1Hit3.png": "wukong_hit_3.png",
    "Role1Hit4.png": "wukong_hit_4.png",
}

EXT_RES_RE = re.compile(r'\[ext_resource type="Texture2D"[^\]]*path="([^"]+)" id="(\d+)"')
ATLAS_RE = re.compile(
    r'\[sub_resource type="AtlasTexture" id="(\d+)"\]\s*\n'
    r'atlas = ExtResource\(\s*"?(\d+)"?\s*\)\s*\n'
    r'region = Rect2\(([^)]+)\)'
)


def _cut_chunk(chunk):
    m = re.search(r"\n\[sub_resource|\n\[node", chunk)
    return chunk[: m.start()] if m else chunk


def _rect(text_):
    x, y, w, h = [float(v) for v in text_.split(",")]
    return (x, y, w, h)


def _anim_chunk(text, name):
    """取指定 resource_name 的 Animation 段文本；没有则 None。"""
    for c in ANIM_SPLIT.split(text):
        if c.startswith('[sub_resource type="Animation"') and f'resource_name = "{name}"' in c:
            return _cut_chunk(c)
    return None


def parse_fx_entries(text):
    """SpecialEffect 的 SpriteFrames：{动画名: [条目, ...]}；条目 = None(空白) | (图集文件名, rect)。"""
    node = re.search(
        r'\[node name="SpecialEffect"[^\]]*\]\s*\n(?:[^\n]*\n)*?sprite_frames = SubResource\("(\d+)"\)',
        text,
    )
    if not node:
        print("  [warn] 旧场景未找到 SpecialEffect 节点，跳过棍气层")
        return {}

    ext = {m.group(2): m.group(1).rsplit("/", 1)[-1] for m in EXT_RES_RE.finditer(text)}
    atlas = {
        m.group(1): (ext.get(m.group(2), f"ext{m.group(2)}"), _rect(m.group(3)))
        for m in ATLAS_RE.finditer(text)
    }

    block = re.search(
        r'\[sub_resource type="SpriteFrames" id="%s"\](.*?)(?=\n\[sub_resource|\n\[node)'
        % node.group(1),
        text,
        re.S,
    )
    if not block:
        return {}

    result = {}
    for blk in re.split(r'(?=\{\s*"frames")', block.group(1)):
        nm = re.search(r'"name": &"([^"]+)"', blk)
        if not nm:
            continue
        entries = []
        # 注意："texture": null 是空白条目，必须保留（帧号按条目序号解释）
        for fm in re.finditer(r'"texture": (SubResource\(\s*"?(\d+)"?\s*\)|null)', blk):
            entries.append(None if fm.group(1) == "null" else atlas.get(fm.group(2)))
        result[nm.group(1)] = entries
    return result


def _track_kv(chunk, path_expr):
    """取某条轨道的 (内部时间序列, 值序列文本)；没有则 (None, None)。"""
    keys = _keys_for(chunk, path_expr)
    if keys is None:
        return None, None
    tm = TIMES_RE.search(keys)
    vm = VALUES_RE.search(keys)
    if not tm or not vm:
        return None, None
    times = [float(t) for t in tm.group(1).split(",") if t.strip()]
    return times, vm.group(1)


def _real_keys(times, values, segs, lead_value=None):
    """内部时间键 → 真实秒键。

    lead_value 不为 None 时，若首个键晚于 0，则在 t=0 补一个该值 ——
    复刻旧行为：切换特效动画时 frame 归 0、offset/scale 保持上一个动画留下的值。
    """
    out = []
    if times and times[0] > 1e-6 and lead_value is not None:
        out.append((0.0, lead_value))
    for t, v in zip(times, values):
        out.append((_real_time(segs, t), v))
    return out


def _fmt_value(kind, value):
    if kind == "string":
        return f'&"{value}"'
    if kind == "vector2":
        return "Vector2(%g, %g)" % value
    if kind == "bool":
        return "true" if value else "false"
    if kind == "shape":
        return 'SubResource("%s")' % value
    return str(value)


# --------------------------------------------------------------------------
# 判定盒（M4，2026-09-30 审查定稿：几何与开关全部是动画值轨道关键帧，同旧项目）
#
# 旧项目在每个攻击动画里 keyframe HitBox 的 shape/position/disabled（Role1.tscn 实测：
# hit1 胶囊 r76/h186、hit2/3/4 矩形，全部是动画轨道数据），朝向由 base_damagebox.scale.x 翻转。
# 新工程同构——**代码零几何**：
#   * 场景结构 m_HitBoxRoot(朝向镜像容器，C# 只翻 scale.x) → m_HitBox(Area2D，**恒在原点**) →
#     CollisionShape2D——与旧 base_damagebox/HitBox/HitBox 逐级对应；三条轨道全部写在
#     形状节点上（同旧项目），容器负缩放会把形状节点的偏移一并镜像（冒烟实测命中正常；
#     2026-09-30 曾疑其不镜像，实为调试碰撞体垫高沙袋的误诊）。
#   * 库内每个动画带三条值轨道（轨道完备性：缺轨的属性在动画切换时会被写成垃圾值，
#     等效于"reset 轨道"——状态机切到任何动画，首帧就写回安全值）：
#       …CollisionShape2D:shape     换判定盒矩形（库内 RectangleShape2D 子资源；
#                                  攻击动画在判定窗前一帧换本招矩形，其余动画第 0 帧写静止矩形）
#       …CollisionShape2D:position  判定盒中心偏移（相对角色原点），原生朝左坐标（前方 = -X，容器负责镜像）
#       …CollisionShape2D:disabled  判定窗开关（沿用旧 disabled 轨道时序，恒关的动画写 true）
#   * 攻击动画另有 OnAttackBegin/OnAttackEnd **方法轨道**——只负责数值包（属性快照/连段推进），
#     与几何无关。离开攻击动画时 AnimationMixer 自动还原值轨道捕获的初值，
#     受击/死亡打断出招时判定盒随之复位（方法轨道没有这种自愈——几何必须走值轨道）。
#
# 几何推导（2026-09-30，对判定窗内各帧的**武器层**贴图做 alpha 像素包围盒）：
#   * 纵向与后缘贴武器像素（各向外扩 10px）；
#   * **前缘统一放长到 -130（追击线）**：连段期间每次命中受击方被击退 ~17px（60px/s × 0.28s
#     硬直，旧 hurtBack × 30 同值），不放长的话第三段起就够不着——旧项目靠超大方形盒
#     （前缘 121~175）吸收这个漂移让整套连段打满，这里用显式的"追击线"表达同一件事；
#     末段 (6,-5) 击退 ~50px 是有意的收招间距（打完走步接近再开下一套，同原版循环）；
#   * attack_3/4 是旋斩，并集含身后来向帧；attack_4 的正向帧 f55 在判定窗开启**之前**
#     （旧窗 0.067-0.2 只覆盖身后段），几何并把它计入——否则正面打不到人；
#   * 坐标 = 原生朝左（前方 = -X）；Y 相对身体层原点（悟空 m_Body 在根原点）。
# 武器层像素实测（native 朝左）：
#   attack_1  f43 x[-80,12]y[-24,70] + f44 x[-86,38]y[34,48]  → 前缘-130 后缘48 → 178x114 @ (-41,23)
#   attack_2  f38..40 x[-94,32]y[36,45]                       → 前缘-130 后缘42 → 172x43 @ (-44,40.5)
#   attack_3  f49/f51/f52 x[-94,93]y[-4,51]                   → 前缘-130 后缘103 → 233x75 @ (-13.5,23.5)
#   attack_4  f55..f59 x[-93,93]y[-26,50]                     → 前缘-130 后缘103 → 233x96 @ (-13.5,12)
# 猴子沿用旧圆 r25@(-24,-10)（相对旧贴图中心在根原点；新身体层在 (0,-13) → Y=-23，
# 修复：此前表值 -10 漏减身体层偏移，判定盒偏低 13px）。静止值 = 场景 .tscn 默认（原生坐标）。
# --------------------------------------------------------------------------
LEGACY_HITBOX_TRACK = "base_damagebox/HitBox/HitBox:disabled"
HITBOX_TRACK = "m_HitBoxRoot/m_HitBox/CollisionShape2D:disabled"
HITBOX_SHAPE_TRACK = "m_HitBoxRoot/m_HitBox/CollisionShape2D:shape"
HITBOX_POS_TRACK = "m_HitBoxRoot/m_HitBox/CollisionShape2D:position"

# 方法回调比"动画末帧"早一帧：末帧前必收招（工程常数，非玩法数值，根规范 §4.5）。
METHOD_PRE_BEAT = 1.0 / 60.0

# 追击线：判定盒前缘（原生 X，负 = 前方）。
HITBOX_CHASE_X = -130.0

WUKONG_HITBOX = {
    "attack_1": {"size": (178.0, 114.0), "pos": (-41.0, 23.0)},
    "attack_2": {"size": (172.0, 43.0), "pos": (-44.0, 40.5)},
    "attack_3": {"size": (233.0, 75.0), "pos": (-13.5, 23.5)},
    "attack_4": {"size": (233.0, 96.0), "pos": (-13.5, 12.0)},
}
WUKONG_HITBOX_REST = {"size": (90.0, 90.0), "pos": (-55.0, 0.0)}     # 场景静止值（原生坐标）

MONKEY_HITBOX = {
    "attack_1": {"size": (50.0, 50.0), "pos": (-24.0, -23.0)},
}
MONKEY_HITBOX_REST = {"size": (50.0, 50.0), "pos": (-24.0, -23.0)}    # 场景静止值


def _hitbox_geo_tracks(name, hit_track, geometry, rest, shape_res):
    """shape/position 两条值轨道；shape_res[id]=size 由 emit 阶段声明为库内子资源。

    攻击动画在判定窗前一帧换几何（保证先有几何后开判定）；其余动画第 0 帧写静止值。
    """
    geo = geometry.get(name, rest)
    if name in geometry and hit_track is not None:
        window_start = next((t for t, v in hit_track[2] if not v), None)
        at = max(0.0, (window_start if window_start is not None else 0.0) - METHOD_PRE_BEAT)
    else:
        at = 0.0
    sid = "hitbox_%s" % (name if name in geometry else "rest")
    shape_res[sid] = geo["size"]
    return [
        (HITBOX_SHAPE_TRACK, "shape", [(at, sid)]),
        (HITBOX_POS_TRACK, "vector2", [(at, geo["pos"])]),
    ]


def _attack_lifecycle_tracks(name, length, geometry):
    """出招生命周期方法轨道：装填/收招（只管数值包，几何在值轨道上）。非攻击动画返回 []。"""
    if name not in geometry:
        return []
    return [
        ("", "method", [(0.0, "OnAttackBegin", [])]),
        ("", "method", [(max(0.0, length - METHOD_PRE_BEAT), "OnAttackEnd", [])]),
    ]


def _append_reset_animation(anims, shape_res, hero):
    """追加 RESET（默认值动画，运行时不播；编辑器重置/停止预览时恢复默认姿势用，
    同旧项目 Role1.tscn 的 RESET 子资源 278：length=0.001、写各属性默认值）。
    写与常规动画同集合的安全默认值；猴子的 shape 恒不写（见 _hitbox_geo_tracks 注释）。
    """
    if hero:
        tracks = [
            (BODY_NODE + ":frame", "int", [(0.0, 0)]),
            (WEAPON_NODE + ":frame", "int", [(0.0, 0)]),
            (FX_NODE + ":animation", "string", [(0.0, "empty")]),
            (FX_NODE + ":frame", "int", [(0.0, 0)]),
            (FX_NODE + ":offset", "vector2", [(0.0, (0.0, 0.0))]),
            (FX_NODE + ":scale", "vector2", [(0.0, (1.0, 1.0))]),
            (HITBOX_TRACK, "bool", [(0.0, True)]),
            (HITBOX_SHAPE_TRACK, "shape", [(0.0, "hitbox_rest")]),
            (HITBOX_POS_TRACK, "vector2", [(0.0, WUKONG_HITBOX_REST["pos"])]),
        ]
        shape_res["hitbox_rest"] = WUKONG_HITBOX_REST["size"]
    else:
        tracks = [
            (MONKEY_SPRITE_FRAMES, "string", [(0.0, "idle")]),
            (MONKEY_BODY + ":frame", "int", [(0.0, 0)]),
            (MONKEY_BODY + ":offset", "vector2", [(0.0, (4.0, 0.0))]),
            (HITBOX_TRACK, "bool", [(0.0, True)]),
            (HITBOX_POS_TRACK, "vector2", [(0.0, MONKEY_HITBOX_REST["pos"])]),
        ]
    anims.append({"name": "RESET", "loop": False, "length": 0.001, "tracks": tracks})


def _hitbox_track(chunk, segs):
    """旧判定开关轨道 → [(真实秒, bool)]；旧动画没有该轨道时返回恒关。"""
    if chunk is not None:
        times, values = _track_kv(chunk, LEGACY_HITBOX_TRACK)
        if times:
            flags = [v == "true" for v in re.findall(r"true|false", values)]
            return (HITBOX_TRACK, "bool", _real_keys(times, flags, segs, lead_value=True))
    return (HITBOX_TRACK, "bool", [(0.0, True)])


def _fx_tracks(chunk, segs, new_name):
    """把旧的四类特效轨道换算成真实秒键表：[(属性名, 值类型, [(t, v), ...]), ...]。"""
    tracks = []

    a_t, a_v = _track_kv(chunk, "Action/SpecialEffect:animation")
    if a_t:
        names = re.findall(r'&"([^"]+)"', a_v)
        tracks.append(("animation", "string", [(_real_time(segs, t), new_name)
                                               for t in a_t[:1]] or [(0.0, new_name)]))
        if len(names) > 1:
            print(f"  [warn] {new_name}: 旧动画有 {len(names)} 次特效动画切换，只迁移了首次")

    f_t, f_v = _track_kv(chunk, "Action/SpecialEffect:frame")
    if f_t:
        frames = [int(v) for v in re.findall(r"-?\d+", f_v)]
        tracks.append(("frame", "int", _real_keys(f_t, frames, segs, lead_value=0)))

    o_t, o_v = _track_kv(chunk, "Action/SpecialEffect:offset")
    if o_t:
        offs = [tuple(float(x) for x in m.group(1).split(","))
                for m in re.finditer(r"Vector2\(([^)]+)\)", o_v)]
        tracks.append(("offset", "vector2", _real_keys(o_t, offs, segs)))

    s_t, s_v = _track_kv(chunk, "Action/SpecialEffect:scale")
    if s_t:
        scales = [tuple(float(x) for x in m.group(1).split(","))
                  for m in re.finditer(r"Vector2\(([^)]+)\)", s_v)]
        tracks.append(("scale", "vector2", _real_keys(s_t, scales, segs)))

    return tracks


def build_wukong_effect():
    """解析出 (SpriteFrames 动画列表, AnimationLibrary 动画列表)。"""
    src = os.path.join(LEGACY, "Scene", "Hero", "Role_1", "Role1.tscn")
    with open(src, encoding="utf-8") as fh:
        text = fh.read()
    fx_entries = parse_fx_entries(text)

    sf_anims = []
    ap_anims = []
    for old, new in WUKONG_FX_MAP.items():
        entries = fx_entries.get(old)
        if entries is None:
            print(f"  [warn] 旧特效动画 {old} 不存在，跳过 {new}")
            continue

        chunk = _anim_chunk(text, old)
        if chunk is None:
            print(f"  [warn] 旧动画 {old} 不存在，跳过 {new}")
            continue

        len_m = LENGTH_RE.search(chunk)
        length = float(len_m.group(1)) if len_m else 1.0
        segs = _scale_segments(chunk, length)

        # 图集文件名 → 新路径（缺失说明该条目不属于普攻，直接报出来，不静默）
        resolved = []
        for e in entries:
            if e is None:
                resolved.append(None)
                continue
            sheet = e[0]
            if sheet not in WUKONG_FX_SHEETS:
                print(f"  [warn] {new}: 特效条目引用了未迁移的图集 {sheet}")
                resolved.append(None)
                continue
            resolved.append((WUKONG_FX_DIR + WUKONG_FX_SHEETS[sheet], e[1]))

        sf_anims.append({"name": new, "loop": False, "entries": resolved})
        ap_anims.append({
            "name": new,
            "length": _real_time(segs, length),
            "tracks": _fx_tracks(chunk, segs, new),
        })

    # 空白动画：对应旧 "wait"（3 个 null 条目 + 把 scale 复位成 1，与旧一致）
    sf_anims.append({"name": "empty", "loop": False, "entries": [None, None, None]})
    ap_anims.append({
        "name": "empty",
        "length": 0.1,
        "tracks": [
            (FX_NODE + ":animation", "string", [(0.0, "empty")]),
            ("frame", "int", [(0.0, 0)]),
            ("scale", "vector2", [(0.0, (1.0, 1.0))]),
        ],
    })
    return sf_anims, ap_anims


def emit_fx_spriteframes(out_path, anims):
    """特效 SpriteFrames：条目序列原样写出（含 null），逐帧 duration 统一 1.0。"""
    tex_ids = {}
    ext_lines = []
    for a in anims:
        for e in a["entries"]:
            if e is None or e[0] in tex_ids:
                continue
            tid = f"tex{len(tex_ids)}"
            tex_ids[e[0]] = tid
            ext_lines.append(f'[ext_resource type="Texture2D" path="{e[0]}" id="{tid}"]')

    atlas_ids = {}
    sub_lines = []
    for a in anims:
        for e in a["entries"]:
            if e is None:
                continue
            if e not in atlas_ids:
                aid = f"Atlas_{len(atlas_ids)}"
                atlas_ids[e] = aid
                x, y, w, h = e[1]
                sub_lines.append(f'[sub_resource type="AtlasTexture" id="{aid}"]')
                sub_lines.append(f'atlas = ExtResource("{tex_ids[e[0]]}")')
                sub_lines.append(f"region = Rect2({x:g}, {y:g}, {w:g}, {h:g})")
                sub_lines.append("")

    blocks = []
    for a in anims:
        parts = []
        for e in a["entries"]:
            tex = "null" if e is None else f'SubResource("{atlas_ids[e]}")'
            parts.append('{\n"duration": 1.0,\n"texture": %s\n}' % tex)
        blocks.append(
            '{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": 1.0\n}'
            % (", ".join(parts), "true" if a["loop"] else "false", a["name"])
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
    print(f"  wrote {os.path.basename(out_path)}  "
          f"({len(anims)} anims, {sum(len(a['entries']) for a in anims)} entries)")


def emit_fx_library(out_path, anims):
    """AnimationLibrary：轨道原样搬到 FX_NODE 的属性上（离散键，与旧 update=1 一致）。"""
    sub_lines = []
    for a in anims:
        sub_lines.append(f'[sub_resource type="Animation" id="{a["name"]}"]')
        sub_lines.append(f'resource_name = "{a["name"]}"')
        sub_lines.append(f'length = {a["length"]:g}')
        sub_lines.append("loop_mode = 0")
        for i, (prop, kind, keys) in enumerate(a["tracks"]):
            sub_lines.append(f'tracks/{i}/type = "value"')
            sub_lines.append(f"tracks/{i}/imported = false")
            sub_lines.append(f"tracks/{i}/enabled = true")
            sub_lines.append(f'tracks/{i}/path = NodePath("{FX_NODE}:{prop}")')
            sub_lines.append(f"tracks/{i}/interp = 1")
            sub_lines.append(f"tracks/{i}/loop_wrap = true")
            times = ", ".join("%.6g" % t for t, _ in keys)
            values = ", ".join(_fmt_value(kind, v) for _, v in keys)
            sub_lines.append(f"tracks/{i}/keys = {{")
            sub_lines.append(f'"times": PackedFloat32Array({times}),')
            sub_lines.append(f'"transitions": PackedFloat32Array({", ".join(["1"] * len(keys))}),')
            sub_lines.append('"update": 1,')
            sub_lines.append(f'"values": [{values}]')
            sub_lines.append("}")
        sub_lines.append("")

    data = ",\n".join(f'&"{a["name"]}": SubResource("{a["name"]}")' for a in anims)
    lines = [f'[gd_resource type="AnimationLibrary" load_steps={len(anims) + 1} format=3]', ""]
    lines += sub_lines
    lines.append("[resource]")
    lines.append("_data = {")
    lines.append(data)
    lines.append("}")
    lines.append("")

    with open(out_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    n_tracks = sum(len(a["tracks"]) for a in anims)
    print(f"  wrote {os.path.basename(out_path)}  ({len(anims)} anims, {n_tracks} tracks)")


def gen_wukong_effect():
    # 只产出被 m_Effect 引用的 SpriteFrames；effect_library（AnimationPlayer 直驱版）零引用，
    # 2026-09-30 随帧序列参照文件一并删除、停止产出。
    sf_anims, _ = build_wukong_effect()
    out_dir = os.path.join(SPRITES, "Effects", "wukong")
    emit_fx_spriteframes(os.path.join(out_dir, "wukong_effect_animations.tres"), sf_anims)




# --------------------------------------------------------------------------
# wukong 合并版 AnimationLibrary（AnimationPlayer 直驱身体/武器/特效三层 + 方法轨道）
#
# 目标形态（迁移决策 2026-09-28：角色动画迁 AnimationPlayer + AnimationTree）：
#   - 身体/武器层由 AnimatedSprite2D 换成 Sprite2D(hframes=6,vframes=14)，
#     帧号就是 6x14 网格的全局序号（与旧项目 Action/RoleBody:frame 完全同构）。
#   - 每个动画包含：
#       m_Body:frame / m_Weapon:frame   帧轨道（离散键，真实秒时序）
#       m_EffectRoot/m_Effect:*         特效属性轨道（attack_N 有棍气；非攻击动画切空白 empty）
#       "." 方法轨道                     旧 add_music 的等价物（时序取旧轨道，音源查 AttackConfig）
#   - 消费方：Entitys/WukongEntity.tscn 的 m_AnimPlayer（libraries/=本文件），
#     AnimationTree（wukong_animation_tree.tres）做嵌套状态机。
# --------------------------------------------------------------------------

WUKONG_LIB_OUT = "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_anim_library.tres"

BODY_NODE = "m_Body"
WEAPON_NODE = "m_Weapon"

# 旧 `.`（RolePlayer）方法轨道的处理表：hit1..4/death 的 add_music 迁为对应方法调用；
# add_music 的 idx 参数不迁（红线 5：音源在 SoundConfig/AttackConfig，代码按当前动画名查表）。
METHOD_TRACK_MAP = {
    "attack_1": "OnAttackSwingSound",
    "attack_2": "OnAttackSwingSound",
    "attack_3": "OnAttackSwingSound",
    "attack_4": "OnAttackSwingSound",
    "death": "OnDeathVoice",
}

# 旧特效动画名 → 新特效动画名（wukong_effect_animations.tres 内的名字）
FX_NAME_MAP = {"wait": "empty"}


def _fx_tracks_generic(chunk, segs):
    """取任意旧动画的 Action/SpecialEffect:* 属性轨道（不含 SpecialEffect2）。

    animation 切换轨道的值按 WUKONG_FX_MAP / FX_NAME_MAP 改名；
    目标特效动画不存在（技能类未迁）时返回 None 表示整组跳过。
    """
    tracks = []
    for prop, kind, conv in (
        ("animation", "string", "name"),
        ("frame", "int", "int"),
        ("offset", "vector2", "vec"),
        ("scale", "vector2", "vec"),
    ):
        t, v = _track_kv(chunk, "Action/SpecialEffect:" + prop)
        if t is None:
            continue
        if conv == "name":
            names = re.findall(r'&"([^"]+)"', v)
            if not names:
                return None
            old_fx = names[0]
            new_fx = WUKONG_FX_MAP.get(old_fx, FX_NAME_MAP.get(old_fx))
            if new_fx is None:
                print(f"  [skip] {old_fx}: 特效动画未迁移，跳过该动画的特效轨道")
                return None
            tracks.append((FX_NODE + ":" + prop, kind, [(0.0, new_fx)]))
        elif conv == "int":
            frames = [int(x) for x in re.findall(r"-?\d+", v)]
            tracks.append((FX_NODE + ":" + prop, kind, _real_keys(t, frames, segs, lead_value=0)))
        else:
            vals = [tuple(float(x) for x in m.group(1).split(","))
                    for m in re.finditer(r"Vector2\(([^)]+)\)", v)]
            tracks.append((FX_NODE + ":" + prop, kind, _real_keys(t, vals, segs)))
    return tracks


def _method_track(chunk, segs, new_name):
    """旧 `.` 的 add_music 方法轨道 → 新方法轨道；无映射返回 None。

    TRACK_SPLIT 按 keys 段切分，type/path 行不在片段内，所以先按 path 定位轨道号，
    再截取该轨道的完整段（到下一条轨道为止）判断类型。
    """
    for m in re.finditer(r'tracks/(\d+)/path = NodePath\("\."\)', chunk):
        n = m.group(1)
        start = chunk.rfind(f"tracks/{n}/type", 0, m.start())
        if start < 0:
            continue
        nxt = re.search(r"\ntracks/\d+/(?:type|path)", chunk[m.end():])
        seg = chunk[start: m.end() + nxt.start()] if nxt else chunk[start:]
        if 'type = "method"' not in seg:
            continue
        km = KEYS_RE.search(seg)
        if not km:
            continue
        methods = re.findall(r'"method": &"([^"]+)"', km.group(1))
        if not methods:
            continue
        if methods[0] != "add_music":
            print(f"  [skip] {new_name}: 旧方法轨道 {methods[0]}() 不迁移")
            return None
        target = METHOD_TRACK_MAP.get(new_name)
        if target is None:
            print(f"  [skip] {new_name}: add_music 未建立映射（音效随技能系统迁移）")
            return None
        tm = TIMES_RE.search(km.group(1))
        times = [float(t) for t in tm.group(1).split(",") if t.strip()] if tm else [0.0]
        return ("", "method", [(_real_time(segs, t), target) for t in times[:1]])
    return None


def _frame_track_keys(frames):
    """[(帧号, 时长)] → 帧轨道键位表（键=每帧起点，离散值=网格帧号）。"""
    times, values = [], []
    acc = 0.0
    for idx, dur in frames:
        times.append(acc)
        values.append(idx)
        acc += dur
    return list(zip(times, values)), acc


# 起跳动画的收尾姿势：旧项目 jump1/jump2 一播完就 play("drop")（BaseHero.gd:522/529），
# 而上升时间（jump_power=-540 / gravity=-980 ≈ 0.55s）比动画长（jump1 0.458s、jump2 0.225s），
# 所以旧游戏在上升后半段显示的是落姿。我们的状态机按物理切（not Rising），
# 动画若不带落姿，jump/jump_2 播完会保持末帧到最高点（二段跳翻转末帧尤其明显）。
# 这里给起跳动画补一个 drop 姿势的尾帧，让可见姿势与旧项目一致。
JUMP_TAIL_ANIMS = ("jump", "jump_2")
JUMP_TAIL_FRAME = 25      # Role1.tscn 里 drop 动画的身体帧
JUMP_TAIL_HOLD = 0.05

# 收招延长（秒，动画侧节奏修正，同 JUMP_TAIL 一类）：attack_4 补 5 帧（0.0833s）。
# 旧 do_normalhit_4 起手时 Interv=2（0.2s 计时器两拍 → 从起手算 0.4s 内不允许下一招），
# 而动画只有 0.3167s：旧的 83ms 锁在动画结束后仍生效。这里直接把动画补长 83ms（末帧保持），
# 让"动画时长 = 出招节奏"的原则继续成立（不进表、不加代码门，2026-09-30 裁决）。
WUKONG_RECOVERY_EXTRA = {"attack_4": 5.0 / 60.0}


def build_wukong_library():
    """合并版 AnimationLibrary：身体帧 + 武器帧 + 特效轨道 + 判定盒值轨道 + 方法轨道。

    返回 (anims, shape_res)：shape_res 是判定盒子资源 id → 尺寸（emit 时声明进库文件）。
    """
    src = os.path.join(LEGACY, "Scene", "Hero", "Role_1", "Role1.tscn")
    with open(src, encoding="utf-8") as fh:
        text = fh.read()
    legacy = parse_animations(text, "Action/RoleBody:frame")

    anims = []
    shape_res = {}
    for old, (new, loop) in WUKONG_MAP.items():
        info = legacy.get(old)
        if info is None:
            continue
        frames = _apply_override(new, info["frames"])
        if new in JUMP_TAIL_ANIMS:
            frames = frames + [(JUMP_TAIL_FRAME, JUMP_TAIL_HOLD)]
        keys, length = _frame_track_keys(frames)

        chunk = _anim_chunk(text, old)
        segs = [(0.0, length, SPEED_SCALE)]
        if chunk is not None:
            len_m = LENGTH_RE.search(chunk)
            segs = _scale_segments(chunk, float(len_m.group(1)) if len_m else 1.0)

        tracks = [
            (BODY_NODE + ":frame", "int", keys),
            (WEAPON_NODE + ":frame", "int", keys),
        ]

        # 非攻击/特效未迁移的动画：统一"切空白 + 帧归零 + 位置归零 + scale=1"四件套。
        # 轨道完备性：AnimationTree 切到不含某属性轨道的动画时会把该属性重置成垃圾值
        # （实测 scale 被写成 1e-05，棍气不可见），所以每个动画都必须带全部特效属性轨道。
        tracks.extend([
            (FX_NODE + ":animation", "string", [(0.0, "empty")]),
            (FX_NODE + ":frame", "int", [(0.0, 0)]),
            (FX_NODE + ":offset", "vector2", [(0.0, (0.0, 0.0))]),
            (FX_NODE + ":scale", "vector2", [(0.0, (1.0, 1.0))]),
        ])

        if chunk is not None:
            fx = _fx_tracks_generic(chunk, segs)
            if fx:
                # 有真实特效数据的动画（attack_1..4）：以旧轨道覆盖默认空白值。
                # 只替换"同一条特效轨"（完整路径精确匹配）。不得按属性后缀过滤：
                # 曾写成 endswith(":" + prop)，替换 frame 轨时会把 m_Body:frame /
                # m_Weapon:frame 一并删掉——idle1/run/jump/fall/hurt/attack_* 的
                # 身体层因此冻结（walk/idle2/death/skill_* 走无特效分支才幸存）。
                fx_paths = {t[0] for t in fx}
                tracks = [t for t in tracks if t[0] not in fx_paths]
                tracks.extend(fx)
            mt = _method_track(chunk, segs, new)
            if mt:
                tracks.append(mt)

        # 判定盒三条值轨道（开关/形状/位置）：只有普攻段迁移旧开关时序；
        # 其余动画恒关 + 静止几何（技能判定随技能系统迁移）
        hit_track = _hitbox_track(chunk if new.startswith("attack_") else None, segs)
        tracks.append(hit_track)
        tracks.extend(_hitbox_geo_tracks(new, hit_track, WUKONG_HITBOX, WUKONG_HITBOX_REST, shape_res))

        # 特效轨道时间可能超出身体帧跨度（旧 length 尾部收招段），取 max 作总长
        for prop, _kind, tks in tracks:
            if prop.endswith(":frame") and prop.startswith(FX_NODE):
                if tks:
                    length = max(length, max(t for t, _ in tks))

        # 动画侧收招延长（见 WUKONG_RECOVERY_EXTRA 注释）：只加总长，帧键不动，末帧保持自动覆盖
        length += WUKONG_RECOVERY_EXTRA.get(new, 0.0)

        # 出招生命周期（装填/收招，只管数值包）
        tracks.extend(_attack_lifecycle_tracks(new, length, WUKONG_HITBOX))

        anims.append({"name": new, "loop": loop, "length": length, "tracks": tracks})

    # 补充动画（walk/idle2，旧项目无轨道）：身体/武器帧 + 切空白特效 + 判定盒静止值
    for name, idxs, loop in WUKONG_BODY_ONLY:
        frames = _apply_override(name, [(i, DEFAULT_FRAME_DURATION) for i in idxs])
        keys, length = _frame_track_keys(frames)
        hit_track = _hitbox_track(None, None)
        tracks = [
            (BODY_NODE + ":frame", "int", keys),
            (WEAPON_NODE + ":frame", "int", keys),
            (FX_NODE + ":animation", "string", [(0.0, "empty")]),
            (FX_NODE + ":frame", "int", [(0.0, 0)]),
            (FX_NODE + ":offset", "vector2", [(0.0, (0.0, 0.0))]),
            (FX_NODE + ":scale", "vector2", [(0.0, (1.0, 1.0))]),
            hit_track,
        ]
        tracks.extend(_hitbox_geo_tracks(name, hit_track, WUKONG_HITBOX, WUKONG_HITBOX_REST, shape_res))
        anims.append({"name": name, "loop": loop, "length": length, "tracks": tracks})

    _append_reset_animation(anims, shape_res, hero=True)
    return anims, shape_res


def _existing_uid(path):
    """读旧文件头里的 uid=。生成器覆盖 .tres 时必须保留：否则 Godot 打开编辑器时
    会补写 uid（git 出现"凭空多出来"的改动），引用它的场景也只能按路径重新解析。"""
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as fh:
        first = fh.readline()
    m = re.search(r'uid="(uid://[^"]+)"', first)
    return m.group(1) if m else None


def emit_anim_library(out_path, anims, shape_res=None):
    """AnimationLibrary .tres：value/method 轨道混排，离散键（update=1）。

    shape_res: 判定盒 RectangleShape2D 子资源 id → 尺寸（shape 轨道引用它们）。
    """
    shape_res = shape_res or {}
    sub_lines = []
    for sid, size in shape_res.items():
        sub_lines.append(f'[sub_resource type="RectangleShape2D" id="{sid}"]')
        sub_lines.append("size = Vector2(%g, %g)" % size)
        sub_lines.append("")
    for a in anims:
        sub_lines.append(f'[sub_resource type="Animation" id="{a["name"]}"]')
        sub_lines.append(f'resource_name = "{a["name"]}"')
        sub_lines.append(f'length = {a["length"]:.6f}')
        sub_lines.append(f'loop_mode = {1 if a["loop"] else 0}')
        for i, (prop, kind, keys) in enumerate(a["tracks"]):
            is_method = kind == "method"
            if is_method:
                node_path = "."
            else:
                node_path = prop
            sub_lines.append('tracks/%d/type = "%s"' % (i, "method" if is_method else "value"))
            sub_lines.append("tracks/%d/imported = false" % i)
            sub_lines.append("tracks/%d/enabled = true" % i)
            sub_lines.append(f'tracks/{i}/path = NodePath("{node_path}")')
            sub_lines.append("tracks/%d/interp = 1" % i)
            sub_lines.append("tracks/%d/loop_wrap = true" % i)
            sub_lines.append("tracks/%d/keys = {" % i)
            sub_lines.append('"times": PackedFloat32Array(%s),' %
                             ", ".join("%.6f" % key[0] for key in keys))
            sub_lines.append('"transitions": PackedFloat32Array(%s),' %
                             ", ".join(["1"] * len(keys)))
            if is_method:
                def _method_value(key):
                    args = key[2] if len(key) > 2 else []
                    args_s = ", ".join(repr(a) for a in args)
                    return '{\n"args": [%s],\n"method": &"%s"\n}' % (args_s, key[1])
                sub_lines.append('"values": [%s]' % ", ".join(_method_value(k) for k in keys))
            else:
                sub_lines.append('"update": 1,')
                sub_lines.append('"values": [%s]' % ", ".join(_fmt_value(kind, v) for _, v in keys))
            sub_lines.append("}")
        sub_lines.append("")

    data = ",\n".join(f'&"{a["name"]}": SubResource("{a["name"]}")' for a in anims)
    uid = _existing_uid(out_path)
    header = f'[gd_resource type="AnimationLibrary" load_steps={len(anims) + len(shape_res) + 1} format=3'
    if uid:
        header += f' uid="{uid}"'
    header += "]"
    lines = [header, ""]
    lines += sub_lines
    lines.append("[resource]")
    lines.append("_data = {")
    lines.append(data)
    lines.append("}")
    lines.append("")

    with open(out_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    n_tracks = sum(len(a["tracks"]) for a in anims)
    print(f"  wrote {os.path.basename(out_path)}  ({len(anims)} anims, {n_tracks} tracks)")


def gen_wukong_library():
    out = os.path.join(SPRITES, "Characters", "Heroes", "wukong", "wukong_anim_library.tres")
    anims, shape_res = build_wukong_library()
    emit_anim_library(out, anims, shape_res)


if __name__ == "__main__":
    print("wukong merged library (AnimationPlayer direct-drive):")
    gen_wukong_library()
    # 2026-09-30：零引用的帧序列参照文件（wukong_animations.tres / wukong_weapon_*_animations.tres /
    # wukong_effect_library.tres）已删除，对应 gen_wukong / gen_wukong_weapons 步骤停用；
    # 帧数据现由 wukong_anim_library.tres 的帧轨道承载（gen_wukong_library）。
    print("wukong effect (棍气):")
    gen_wukong_effect()
    print("huaguoshan_monkey:")
    gen_monkey()
    print("huaguoshan_monkey library (AnimationPlayer direct-drive):")
    gen_monkey_library()

