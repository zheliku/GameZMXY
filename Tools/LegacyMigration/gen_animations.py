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
    return str(value)


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
            ("animation", "string", [(0.0, "empty")]),
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
    sf_anims, ap_anims = build_wukong_effect()
    out_dir = os.path.join(SPRITES, "Effects", "wukong")
    emit_fx_spriteframes(os.path.join(out_dir, "wukong_effect_animations.tres"), sf_anims)
    emit_fx_library(os.path.join(out_dir, "wukong_effect_library.tres"), ap_anims)


if __name__ == "__main__":
    print("wukong body:")
    gen_wukong()
    print("wukong weapons:")
    gen_wukong_weapons()
    print("wukong effect (棍气):")
    gen_wukong_effect()
    print("huaguoshan_monkey:")
    gen_monkey()

