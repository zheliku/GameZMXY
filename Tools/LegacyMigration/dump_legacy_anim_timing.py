#!/usr/bin/env python3
"""旧项目 Hero 动画的真实时序核算（含 speed_scale 自身轨道）。

关键事实：每个 Animation 里都有一条 `NodePath("RolePlayer:speed_scale")` 轨道，
动画在播放过程中会改自己的播放速度（起手慢、挥出去快）。
所以真实时长不是 length/2，而是对速度积分：

    真实时长 = ∫ dt / speed_scale(t)

本工具并排打印"按恒定 2.0 折算"和"按 speed_scale 轨道积分"，用于核对迁移后的 FPS。

Usage: python Tools/LegacyMigration/dump_legacy_anim_timing.py
"""

import re
import sys

SRC = r"P:\Godot-Project\ZMXY_BHYH\Scene\Hero\Role_1\Role1.tscn"

ANIM_SPLIT = re.compile(r'(?=\[sub_resource type="Animation")')
TRACK_SPLIT = re.compile(r'(?=tracks/\d+/\w+)')
LENGTH_RE = re.compile(r'^length = ([\d.]+)', re.M)
KEYS_RE = re.compile(r'tracks/\d+/keys = \{(.*?)\n\}', re.S)
TIMES_RE = re.compile(r'"times":\s*PackedFloat32Array\(([^)]*)\)')
VALUES_RE = re.compile(r'"values":\s*\[([^\]]*)\]')


def _keys_for(chunk, path_expr):
    """取某条轨道的 keys 段文本（含 times/values）。"""
    for part in TRACK_SPLIT.split(chunk):
        if f'NodePath("{path_expr}")' in part:
            rest = chunk[chunk.index(part):]
            m = KEYS_RE.search(rest)
            if m:
                return m.group(1)
    return None


def _floats(text):
    return [float(t) for t in text.split(",") if t.strip()]


def _ints(text):
    return [int(v) for v in text.split(",") if v.strip()]


def _segments(scale_keys_text, length, base_scale):
    """把 speed_scale 轨道解析成 [(起, 止, 速度)]；没有则恒定 base_scale。"""
    if not scale_keys_text:
        return [(0.0, length, base_scale)]
    t_m = TIMES_RE.search(scale_keys_text)
    v_m = VALUES_RE.search(scale_keys_text)
    if not t_m or not v_m:
        return [(0.0, length, base_scale)]
    times = _floats(t_m.group(1))
    values = _floats(v_m.group(1))
    if not times or not values:
        return [(0.0, length, base_scale)]
    segs = []
    if times[0] > 0:
        segs.append((0.0, times[0], values[0]))
    for i, t in enumerate(times):
        nxt = times[i + 1] if i + 1 < len(times) else length
        v = values[i] if i < len(values) else values[-1]
        segs.append((t, nxt, v if v > 0 else base_scale))
    return segs


def _real(segs, t):
    """内部时间 t 对应的真实秒数。"""
    real = 0.0
    for start, end, v in segs:
        if t <= start:
            break
        real += (min(t, end) - start) / v
    return real


def main():
    text = open(SRC, encoding="utf-8").read()
    want = sys.argv[1] if len(sys.argv) > 1 else None

    m = re.search(
        r'\[node name="RolePlayer"[^\]]*\]\s*\n(?:[^\n]*\n)*?speed_scale = ([\d.]+)', text
    )
    base_scale = float(m.group(1)) if m else 1.0
    print(f"RolePlayer.speed_scale(初始) = {base_scale}")

    if want:
        for chunk in ANIM_SPLIT.split(text):
            if not chunk.startswith('[sub_resource type="Animation"'):
                continue
            end = re.search(r'\n\[sub_resource|\n\[node', chunk)
            if end:
                chunk = chunk[: end.start()]
            nm = re.search(r'resource_name = "([^"]+)"', chunk)
            if not nm or nm.group(1) != want:
                continue
            len_m = LENGTH_RE.search(chunk)
            length = float(len_m.group(1)) if len_m else 1.0
            print(f"\n=== {want}: length={length} ===")

            sk = _keys_for(chunk, "RolePlayer:speed_scale")
            print(f"speed_scale keys: {sk.strip() if sk else '(none)'}")

            fk = _keys_for(chunk, "Action/RoleBody:frame")
            if not fk:
                print("(no frame track)")
                return
            tm = TIMES_RE.search(fk)
            vm = VALUES_RE.search(fk)
            times = _floats(tm.group(1))
            values = _ints(vm.group(1))

            segs = _segments(sk, length, base_scale)
            print(f"{'内部时间':>10s} {'真实时间':>10s} {'帧号':>5s} {'该帧真实时长':>12s}")
            for i, (t, v) in enumerate(zip(times, values)):
                t1 = times[i + 1] if i + 1 < len(times) else length
                r0, r1 = _real(segs, t), _real(segs, t1)
                print(f"{t:10.3f} {r0:10.3f} {v:5d} {r1 - r0:12.4f}")
            return
        print(f"(未找到 {want})")
        return

    rows = []
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
        length = float(len_m.group(1)) if len_m else 1.0

        frame_keys = _keys_for(chunk, "Action/RoleBody:frame")
        if not frame_keys:
            continue
        values_m = VALUES_RE.search(frame_keys)
        n = len(_ints(values_m.group(1))) if values_m else 0

        # speed_scale 轨道：动画自己改播放速度
        scale_keys = _keys_for(chunk, "RolePlayer:speed_scale")
        scale_times, scale_values = [], []
        if scale_keys:
            t_m = TIMES_RE.search(scale_keys)
            v_m = VALUES_RE.search(scale_keys)
            if t_m:
                scale_times = _floats(t_m.group(1))
            if v_m:
                scale_values = _floats(v_m.group(1))

        # 真实时长 = ∫ dt / speed_scale(t)：按 key 分段积分
        real = 0.0
        if scale_times and scale_values:
            for i, t in enumerate(scale_times):
                nxt = scale_times[i + 1] if i + 1 < len(scale_times) else length
                v = scale_values[i] if i < len(scale_values) else scale_values[-1]
                if v <= 0:
                    v = base_scale
                real += (nxt - t) / v
        else:
            real = length / base_scale

        rows.append((name_m.group(1), length, real, scale_values, n))

    print(f"{'name':12s} {'length':>8s} {'real(s)':>8s} {'n':>3s} {'FPS':>9s}  speed_scale keys")
    for name, length, real, scale_values, n in sorted(rows):
        fps = n / real if real > 0 else 0.0
        print(f"{name:12s} {length:8.3f} {real:8.3f} {n:3d} {fps:9.4f}  {scale_values}")


if __name__ == "__main__":
    main()
