#!/usr/bin/env python3
"""M6 UI/关卡资产迁移（一次性，可重跑；旧项目只读）。

从 ZMXY_BHYH 拷入 M6 所需素材（不带旧 .import），并生成两份 SpriteFrames：
  * gogo 前进箭头（67 帧，loop，25fps —— 旧 Role_information.tscn 的 SubResource("1")）
  * 关卡出口传送门（Export.png 图集 206×186 × 11 帧，loop，25fps —— 旧 BaseThroughLevel.tscn）

产物全部登记 Docs/LegacyAssetMap.md（见该文档 M6 节）。

Run: uv run --with openpyxl python Tools/LegacyMigration/migrate_m6_ui_assets.py
"""

import os
import shutil
import struct

OLD = r"P:\Godot-Project\ZMXY_BHYH"
NEW = os.path.join(r"P:\Godot-Project\GameZMXY", "Godot", "GodotProject", "TheGame")

# (旧路径, 新路径)
COPY_MAP = [
    # ---- 开始界面（HeroSelectForm 兼作） ----
    (r"Art\MainGame\Bg1.png", r"Sprites\UI\main_menu\main_menu_bg.png"),
    (r"Art\MainGame\ChoosePlayer\ui_juese_wukong01.png", r"Sprites\UI\hero_select\wukong_portrait.png"),
    # ---- HUD（旧 Role_information.tscn role_hp_mp_exp / role_menu / role_head） ----
    (r"Art\HeroPicture\RoleProperiesBox\408.png", r"Sprites\UI\hud\hp_box.png"),       # 血条框
    (r"Art\HeroPicture\RoleProperiesBox\345.png", r"Sprites\UI\hud\hp_fill.png"),      # 血条红条
    (r"Art\HeroPicture\RoleProperiesBox\742.png", r"Sprites\UI\hud\hp_under.png"),     # 血条底层白条(旧 hp_bar2/exp)
    (r"Art\HeroPicture\RoleProperiesBox\748.png", r"Sprites\UI\hud\head_frame.png"),   # 头像框(旧 h_m_e_t)
    (r"Art\HeroPicture\RoleProperiesBox\swk.png", r"Sprites\UI\hud\wukong_head.png"),  # 悟空头像(旧 role_head)
    (r"Art\HeroPicture\RoleProperiesBox\718.png", r"Sprites\UI\hud\ws_frame.png"),     # 无条框(旧 ws_wk)
    (r"Art\HeroPicture\RoleProperiesBox\720.png", r"Sprites\UI\hud\ws_under.png"),     # 无双底(旧 ws_effect.under)
    (r"Art\HeroPicture\RoleProperiesBox\724.png", r"Sprites\UI\hud\ws_fill.png"),      # 无双填充(旧 ws_effect.progress)
    # ---- 结算界面（旧 victory.tscn） ----
    (r"Art\Level\Settlement\623.png", r"Sprites\UI\game_over\game_over_bg.png"),
    (r"Art\Level\Settlement\630.png", r"Sprites\UI\game_over\btn_return_normal.png"),
    (r"Art\Level\Settlement\632.png", r"Sprites\UI\game_over\btn_return_hover.png"),
    (r"Art\Level\Settlement\457.png", r"Sprites\UI\game_over\btn_retry_normal.png"),
    (r"Art\Level\Settlement\459.png", r"Sprites\UI\game_over\btn_retry_hover.png"),
    (r"Art\Level\Settlement\637.png", r"Sprites\UI\game_over\victory_title.png"),
    # ---- 关卡出口传送门图集 ----
    (r"Art\Level\Export.png", r"Sprites\Levels\huaguoshan\level_1_exit.png"),
    # ---- 关卡 BGM ----
    (r"Music\level\1_music.mp3", r"Audios\BGM\level_1.mp3"),
]

GOGO_COUNT = 67


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(26)
    assert head[:8] == b"\x89PNG\r\n\x1a\n", f"not a png: {path}"
    w, h = struct.unpack(">II", head[16:24])
    return w, h


def copy_all():
    for old_rel, new_rel in COPY_MAP:
        src = os.path.join(OLD, old_rel)
        dst = os.path.join(NEW, new_rel)
        assert os.path.isfile(src), f"旧资源缺失: {old_rel}"
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        print(f"  {old_rel} -> {new_rel}")
    # gogo 67 帧
    gogo_dir = os.path.join(NEW, r"Sprites\UI\hud\gogo")
    os.makedirs(gogo_dir, exist_ok=True)
    for i in range(1, GOGO_COUNT + 1):
        src = os.path.join(OLD, r"Art\Level\Gogo", f"{i}.png")
        dst = os.path.join(gogo_dir, f"gogo_{i}.png")
        assert os.path.isfile(src), f"旧资源缺失: Art/Level/Gogo/{i}.png"
        shutil.copyfile(src, dst)
    print(f"  Art/Level/Gogo/1..67.png -> Sprites/UI/hud/gogo/gogo_1..67.png")


def spriteframes(path, ext_textures, animations):
    """ext_textures: [(res_path, width, height)]; animations 见调用处。"""
    lines = ["[gd_resource type=\"SpriteFrames\" format=3]", ""]
    for i, (res_path, _w, _h) in enumerate(ext_textures):
        lines.append(
            f"[ext_resource type=\"Texture2D\" path=\"{res_path}\" id=\"tex{i}\"]")
    lines.append("")
    atlas_id = 0
    for anim_name, frames, loop, speed in animations:
        for fi, (tex_idx, region, dur) in enumerate(frames):
            lines.append(f"[sub_resource type=\"AtlasTexture\" id=\"Atlas_{atlas_id}\"]")
            lines.append(f"atlas = ExtResource(\"tex{tex_idx}\")")
            lines.append(f"region = Rect2({region[0]}, {region[1]}, {region[2]}, {region[3]})")
            lines.append("")
            atlas_id += 1
    lines.append("[resource]")
    lines.append("animations = [{")
    atlas_id = 0
    for ai, (anim_name, frames, loop, speed) in enumerate(animations):
        if ai > 0:
            lines.append("}, {")
        lines.append("\"frames\": [{")
        for fi, (tex_idx, region, dur) in enumerate(frames):
            if fi > 0:
                lines.append("}, {")
            lines.append(f"\"duration\": {dur},")
            lines.append(f"\"texture\": SubResource(\"Atlas_{atlas_id}\")")
            atlas_id += 1
        lines.append("}],")
        lines.append(f"\"loop\": {int(loop)},")
        lines.append(f"\"name\": &\"{anim_name}\",")
        lines.append(f"\"speed\": {speed}")
    lines.append("}]")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"  wrote {os.path.relpath(path, NEW)}")


def build_gogo_frames():
    frames = []
    for i in range(1, GOGO_COUNT + 1):
        png = os.path.join(NEW, r"Sprites\UI\hud\gogo", f"gogo_{i}.png")
        w, h = png_size(png)
        frames.append((i - 1, (0, 0, w, h), 0.1))
    spriteframes(
        os.path.join(NEW, r"Sprites\UI\hud\gogo\gogo_frames.tres"),
        [(f"res://TheGame/Sprites/UI/hud/gogo/gogo_{i}.png",
          *png_size(os.path.join(NEW, r"Sprites\UI\hud\gogo", f"gogo_{i}.png")))
         for i in range(1, GOGO_COUNT + 1)],
        [("go", frames, True, 25.0)],  # 旧 Role_information.tscn：loop=1, speed=25
    )


def build_exit_frames():
    # 旧 BaseThroughLevel.tscn 的 exit SpriteFrames：Export.png 图集 206×186 × 11 帧，speed 25
    png = os.path.join(NEW, r"Sprites\Levels\huaguoshan\level_1_exit.png")
    w, h = png_size(png)
    fw, fh = 206, 186
    cols = w // fw
    regions = []
    for i in range(11):
        x = (i % cols) * fw
        y = (i // cols) * fh
        regions.append((0, (x, y, fw, fh), 0.1))
    spriteframes(
        os.path.join(NEW, r"Sprites\Levels\huaguoshan\level_1_exit_frames.tres"),
        [("res://TheGame/Sprites/Levels/huaguoshan/level_1_exit.png", w, h)],
        [("exit", regions, True, 25.0)],
    )


if __name__ == "__main__":
    print("copy assets:")
    copy_all()
    print("build SpriteFrames:")
    build_gogo_frames()
    build_exit_frames()
    print("done.")
