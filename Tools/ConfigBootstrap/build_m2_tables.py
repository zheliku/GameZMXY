#!/usr/bin/env python3
"""One-time M2 bootstrap: create/refresh Luban xlsx for the vertical slice.

Design notes (see AGENTS.md 6):
  * every business table carries Id / NameCn / Desc, and every field has a
    Chinese comment; LegacyId only on tables with a real legacy counterpart
  * no derived columns (e.g. wave count is derived from LevelWaveConfig)
  * combat constants that differ per camp live in one BattleConfig row, not
    duplicated into HeroConfig / MonsterConfig
  * values are taken from the legacy project (ZMXY_BHYH); see file:line in the
    comments below wherever a number is not self-evident

Run:  python Tools/ConfigBootstrap/build_m2_tables.py
Then: Configs/GameConfig/gen_code_bin_to_project_lazyload.bat
"""

import os

import openpyxl
from openpyxl import Workbook

ROOT = r"P:\Godot_Project\GameZMXY"
DATAS = os.path.join(ROOT, "Configs", "GameConfig", "Datas")


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------
def new_table(file_name, fields, rows):
    """fields: [(name, type, comment)]; rows: [[value, ...]]"""
    wb = Workbook()
    ws = wb.active
    ws.title = "Sheet1"
    ws.append(["##var"] + [f[0] for f in fields])
    ws.append(["##var"] + [None] * len(fields))
    ws.append(["##type"] + [f[1] for f in fields])
    ws.append(["##group"] + [None] * len(fields))
    ws.append(["##"] + [f[2] for f in fields])
    for r in rows:
        ws.append([None] + list(r))
    wb.save(os.path.join(DATAS, file_name))
    print(f"  wrote {file_name}  ({len(rows)} rows, {len(fields)} cols)")


def refresh_table(file_name, header_rows, rows):
    """Keep rows 1..header_rows, replace all data rows."""
    path = os.path.join(DATAS, file_name)
    wb = openpyxl.load_workbook(path)
    ws = wb.active
    if ws.max_row > header_rows:
        ws.delete_rows(header_rows + 1, ws.max_row - header_rows)
    for r in rows:
        ws.append(r)
    wb.save(path)
    print(f"  updated {file_name}  ({len(rows)} rows)")


# ---------------------------------------------------------------------------
# 1. enums
# ---------------------------------------------------------------------------
ENUM_HEADER = 3
ENUMS = [
    # (full_name, flags, unique, [(name, alias, value, comment)])
    ("UIFormId", False, True, [
        ("HeroSelectForm", "选人界面", None, None),
        ("HudForm", "战斗界面", None, None),
        ("GameOverForm", "结算界面", None, None),
    ]),
    ("Entity.EntityId", False, True, [
        ("Wukong", "悟空", None, None),
        ("HuaguoshanMonkey", "花果山猴子", None, None),
    ]),
    ("Battle.DamageKind", False, True, [
        ("Physics", "物理", None, None),
        ("Magic", "魔法", None, None),
        ("Real", "真实", None, None),
    ]),
]


def build_enums():
    path = os.path.join(DATAS, "__enums__.xlsx")
    wb = openpyxl.load_workbook(path)
    ws = wb.active
    if ws.max_row > ENUM_HEADER:
        ws.delete_rows(ENUM_HEADER + 1, ws.max_row - ENUM_HEADER)
    for full_name, flags, unique, items in ENUMS:
        for i, (name, alias, value, comment) in enumerate(items):
            row = [None] * 12
            if i == 0:
                row[1] = full_name
                row[2] = flags
                row[3] = unique
            row[7] = name
            row[8] = alias
            row[9] = value
            row[10] = comment
            ws.append(row)
    wb.save(path)
    print(f"  updated __enums__.xlsx  ({len(ENUMS)} enums)")


# ---------------------------------------------------------------------------
# 2. table registry
# ---------------------------------------------------------------------------
TABLE_HEADER = 3
NEW_TABLES = [
    # (full_name, value_type, input_file, mode)
    # mode="one" 单行全局表(无索引)；None 走默认 map，按 Id 索引
    ("Hero.TbHeroConfig", "HeroConfig", "HeroConfig.xlsx", None),
    ("Hero.TbHeroAttackConfig", "HeroAttackConfig", "HeroAttackConfig.xlsx", None),
    ("Hero.TbHeroLevelConfig", "HeroLevelConfig", "HeroLevelConfig.xlsx", None),
    ("Monster.TbMonsterConfig", "MonsterConfig", "MonsterConfig.xlsx", None),
    ("Monster.TbMonsterAttackConfig", "MonsterAttackConfig", "MonsterAttackConfig.xlsx", None),
    ("Battle.TbAttackConfig", "AttackConfig", "AttackConfig.xlsx", None),
    ("Battle.TbBattleConfig", "BattleConfig", "BattleConfig.xlsx", "one"),
    ("Level.TbLevelConfig", "LevelConfig", "LevelConfig.xlsx", None),
    ("Level.TbLevelWaveConfig", "LevelWaveConfig", "LevelWaveConfig.xlsx", None),
    ("Level.TbLevelSpawnConfig", "LevelSpawnConfig", "LevelSpawnConfig.xlsx", None),
]


def build_table_registry():
    path = os.path.join(DATAS, "__tables__.xlsx")
    wb = openpyxl.load_workbook(path)
    ws = wb.active
    if ws.max_row > TABLE_HEADER:
        ws.delete_rows(TABLE_HEADER + 1, ws.max_row - TABLE_HEADER)
    rows = [
        (None, "UI.TbUIFormConfig", "UIFormConfig", True, "界面UI.xlsx", None, None),
        (None, "Entity.TbEntityConfig", "EntityConfig", True, "实体.xlsx", None, None),
    ] + [(None, fn, vt, True, f, None, mode) for fn, vt, f, mode in NEW_TABLES]
    for r in rows:
        ws.append(list(r) + [None] * (11 - len(r)))
    wb.save(path)
    print(f"  updated __tables__.xlsx  ({len(rows)} tables)")


# ---------------------------------------------------------------------------
# 3. entity / UI tables (demo rows are dangling, replace them)
# ---------------------------------------------------------------------------
def build_entity_ui():
    refresh_table("实体.xlsx", 5, [
        (None, 1, "Wukong", "res://TheGame/Entitys/WukongEntity.tscn", "Actor", 0),
        (None, 2, "HuaguoshanMonkey", "res://TheGame/Entitys/HuaguoshanMonkeyEntity.tscn", "Actor", 0),
    ])
    refresh_table("界面UI.xlsx", 5, [
        (None, 1, "HeroSelectForm", "res://TheGame/UIs/HeroSelectForm.tscn", False, "Normal"),
        (None, 2, "HudForm", "res://TheGame/UIs/HudForm.tscn", False, "Normal"),
        (None, 3, "GameOverForm", "res://TheGame/UIs/GameOverForm.tscn", False, "Normal"),
    ])


# ---------------------------------------------------------------------------
# 4. gameplay tables
# ---------------------------------------------------------------------------
def build_hero():
    # legacy BaseRoleProperies.set_basic_prop (Myself == 1), BaseHero.walk_speed,
    # BaseObject.jump_power, Role1.gd gravity
    fields = [
        ("Id", "int", "英雄ID"),
        ("LegacyId", "int", "旧 role 编号"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("EntityId", "Entity.EntityId", "实体枚举"),
        ("BaseHp", "int", "1级生命(旧 SHp)"),
        ("BaseMp", "int", "1级魔法(旧 SMp)"),
        ("BasePower", "int", "1级攻击"),
        ("BaseDef", "int", "1级物防"),
        ("BaseMdef", "int", "1级魔防"),
        ("GrowHp", "int", "每级生命成长"),
        ("GrowMp", "int", "每级魔法成长"),
        ("GrowPower", "int", "每级攻击成长"),
        ("GrowDef", "int", "每级物防成长"),
        ("GrowMdef", "int", "每级魔防成长"),
        ("Crit", "int", "暴击"),
        ("Miss", "int", "闪避"),
        ("Lucky", "int", "幸运"),
        ("Toughness", "int", "韧性(减对方幸运)"),
        ("Htarget", "int", "命中(破闪)"),
        ("CritReduce", "int", "暴击抵抗"),
        ("Ar", "int", "破甲(减对方物防)"),
        ("Sp", "int", "破魔(减对方魔防)"),
        ("Vampirism", "float", "吸血系数(物理伤害转化回血)"),
        ("RHp", "float", "每秒回血"),
        ("RMp", "float", "每秒回魔"),
        ("MoveSpeed", "float", "移动速度 px/s(旧 walk_speed=240)"),
        ("JumpSpeed", "float", "起跳速度(向上为正,旧 jump_power=-540)"),
        ("Gravity", "float", "重力(旧 gravity=980 向下)"),
    ]
    rows = [
        (1, 1, "悟空", "齐天大圣", "Wukong",
         80, 50, 8, 10, 10,
         50, 15, 4, 1, 1,
         0, 0, 0, 0, 0, 0, 0, 0,
         0, 0, 0,
         240, 540, 980),
    ]
    new_table("HeroConfig.xlsx", fields, rows)

    fields = [
        ("Id", "int", "连段记录ID"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("HeroId", "int", "英雄ID"),
        ("ComboIndex", "int", "连段序号(0起)"),
        ("AttackId", "int", "攻击ID(AttackConfig.Id)"),
    ]
    rows = [
        (1, "悟空普攻1", "普攻第一段", 1, 0, 1001),
        (2, "悟空普攻2", "普攻第二段", 1, 1, 1002),
        (3, "悟空普攻3", "普攻第三段", 1, 2, 1003),
        (4, "悟空普攻4", "普攻第四段(击退收招)", 1, 3, 1004),
    ]
    new_table("HeroAttackConfig.xlsx", fields, rows)

    # legacy BaseRoleProperies: max_exp_list + 5000+5000*(lv-19) beyond 19
    exp_list = [140, 160, 180, 200, 220, 300, 400, 500, 600, 700,
                800, 900, 1200, 1400, 1600, 2000, 2400, 3000, 4000, 5000]
    rows = []
    for lv in range(1, 56):
        max_exp = exp_list[lv - 1] if lv < 20 else 5000 + 5000 * (lv - 19)
        rows.append((lv, f"{lv}级", "该级升下一级所需经验", max_exp))
    fields = [
        ("Id", "int", "等级(即ID)"),
        ("NameCn", "string", "等级名"),
        ("Desc", "string", "描述"),
        ("MaxExp", "int", "升到下一级所需经验(旧 max_exp)"),
    ]
    new_table("HeroLevelConfig.xlsx", fields, rows)


def build_monster():
    fields = [
        ("Id", "int", "怪物ID"),
        ("LegacyId", "int", "旧怪物编号"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("EntityId", "Entity.EntityId", "实体枚举"),
        ("Level", "int", "等级(参与等级压制)"),
        ("Hp", "int", "生命(旧 SHp)"),
        ("Def", "int", "物防"),
        ("Mdef", "int", "魔防"),
        ("Crit", "int", "暴击"),
        ("Miss", "int", "闪避"),
        ("Lucky", "int", "幸运"),
        ("Toughness", "int", "韧性(减对方幸运)"),
        ("Htarget", "int", "命中(破闪)"),
        ("CritReduce", "int", "暴击抵抗"),
        ("Ar", "int", "破甲(减对方物防)"),
        ("Sp", "int", "破魔(减对方魔防)"),
        ("RHp", "float", "每秒回血(旧 self_rhp)"),
        ("MoveSpeed", "float", "移动速度 px/s(旧 speed=8,代码里×10)"),
        ("SightRange", "int", "索敌距离(旧 mysee)"),
        ("AttackRange", "int", "进入攻击的距离(旧 attackRange)"),
        ("AttackDesire", "int", "攻击欲望 0-100(旧 attackDesire)"),
        ("BehitCalmTime", "float", "受击后僵直秒数(旧 behit_calmtime)"),
        ("AddExp", "int", "击杀给英雄的经验(旧 add_exp)"),
    ]
    rows = [
        (1, 1, "花果山猴子", "花果山小怪", "HuaguoshanMonkey", 5,
         60, 50, 80,
         0, 0, 0, 0, 0, 0, 0, 0,
         0, 80, 300, 45, 70, 0, 1),
    ]
    new_table("MonsterConfig.xlsx", fields, rows)

    fields = [
        ("Id", "int", "关联记录ID"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("MonsterId", "int", "怪物ID"),
        ("AttackId", "int", "攻击ID(AttackConfig.Id)"),
        ("Weight", "int", "AI 选择该攻击的权重"),
    ]
    rows = [(1, "猴子普攻", "猴子唯一攻击", 1, 2001, 100)]
    new_table("MonsterAttackConfig.xlsx", fields, rows)


def build_battle():
    # legacy keys: Role1.gd objattackDic / Monster_1.gd objattackDic
    fields = [
        ("Id", "int", "攻击ID"),
        ("LegacyId", "string", "旧 objattackDic 键名"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("Animation", "string", "播放的动画名"),
        ("PowerScaleMin", "float", "攻击力倍率下限(乘英雄攻击)"),
        ("PowerScaleMax", "float", "攻击力倍率上限(乘英雄攻击)"),
        ("FlatPower", "int", "固定攻击力(不与属性挂钩)"),
        ("DamageKind", "Battle.DamageKind", "伤害类型"),
        ("KnockbackX", "float", "击退横向分量(旧 hurtBack[0])"),
        ("KnockbackY", "float", "击退纵向分量(旧 hurtBack[1])"),
        ("WsGainMin", "int", "命中获得无双值下限(旧 WSValue)"),
        ("WsGainMax", "int", "命中获得无双值上限(旧 WSValue)"),
        ("HitProtect", "int", "受击保护累计值(旧 HitProtect)"),
        ("HitInterval", "float", "同招连击间隔秒(旧 HitInterv)"),
    ]
    rows = [
        (1001, "hit1", "悟空普攻1", "普攻第一段", "attack_1", 1.0, 1.2, 0, "Physics", 2, 0, 3, 5, 0, 1),
        (1002, "hit2", "悟空普攻2", "普攻第二段", "attack_2", 0.9, 1.1, 0, "Physics", 2, 0, 3, 5, 0, 1),
        (1003, "hit3", "悟空普攻3", "普攻第三段", "attack_3", 1.0, 1.2, 0, "Physics", 2, 0, 3, 5, 0, 1),
        (1004, "hit4", "悟空普攻4", "普攻第四段(击退收招)", "attack_4", 1.2, 1.4, 0, "Physics", 6, -5, 3, 5, 0, 2),
        (2001, "hit1", "猴子普攻", "猴子唯一攻击", "attack_1", 0.0, 0.0, 10, "Physics", -3, -6, 0, 0, 10, 0),
    ]
    new_table("AttackConfig.xlsx", fields, rows)

    # legacy constants: hero-as-defender K=250 (BaseHero.gd:703/707),
    # monster-as-defender K=100 (BaseMonster.gd:790/793),
    # miss K hero 100 / monster 70, crit K 100, lucky K heroAtk 100 / monsterAtk 50,
    # level suppression coefficients (BaseHero.gd:744-746, BaseMonster.gd:737-770)
    # mode=one 的单行全局表，按 AGENTS 6 不设 Id
    fields = [
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("DefK_Hero", "float", "英雄作防守方时物防减伤K: x/(x+K)"),
        ("MdefK_Hero", "float", "英雄作防守方时魔防减伤K"),
        ("MissK_Hero", "float", "英雄作防守方时被命中K: Miss/(Miss+K)"),
        ("DefK_Monster", "float", "怪物作防守方时物防减伤K"),
        ("MdefK_Monster", "float", "怪物作防守方时魔防减伤K"),
        ("MissK_Monster", "float", "怪物作防守方时被命中K"),
        ("CritK", "float", "暴击率基数: Crit/(Crit+K)"),
        ("LuckyK_HeroAtk", "float", "英雄攻击时暴击倍率幸运K"),
        ("LuckyK_MonsterAtk", "float", "怪物攻击时暴击倍率幸运K"),
        ("CritBase", "float", "暴击倍率基数: CritBase + Lucky/(Lucky+K)"),
        ("LvMissCoef_HeroDef", "float", "等级压制-英雄防守闪避系数"),
        ("LvCritCoef_HeroDef", "float", "等级压制-英雄防守暴击系数"),
        ("LvMissCoef_MonsterDef", "float", "等级压制-怪物防守闪避系数"),
        ("LvCritCoef_MonsterDef", "float", "等级压制-怪物防守暴击系数"),
        ("LvLuckyCoef", "float", "等级压制-幸运系数"),
        ("LvDamageCoef", "float", "等级压制-每级伤害系数"),
    ]
    rows = [
        ("战斗常数", "沿用旧项目人怪两侧不一致的基数,集中在此处",
         250, 250, 100, 100, 100, 70, 100, 100, 50, 2,
         0.03, 0.07, 0.04, 0.11, 0.07, 0.05),
    ]
    new_table("BattleConfig.xlsx", fields, rows)


def build_level():
    fields = [
        ("Id", "int", "关卡ID"),
        ("LegacyId", "int", "旧关卡编号"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("ScenePath", "string", "关卡场景路径"),
        ("BgmPath", "string", "背景音乐路径(待迁)"),
        ("MaxAlive", "int", "场上怪物上限(旧项目全局硬编码6)"),
        ("SpawnInterval", "float", "补怪间隔秒(旧设置默认1.2)"),
    ]
    rows = [
        (1, 1, "花果山", "第一关", "res://TheGame/Scenes/Level_1.tscn", "", 6, 1.2),
    ]
    new_table("LevelConfig.xlsx", fields, rows)

    fields = [
        ("Id", "int", "波次ID"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("LevelId", "int", "关卡ID"),
        ("WaveIndex", "int", "波次序号(1起)"),
        ("TriggerX", "float", "玩家 x 达到该值时触发本波"),
    ]
    rows = [
        (1, "第1波", "进入关卡即触发", 1, 1, 0),
        (2, "第2波", "玩家 x>=1600 触发", 1, 2, 1600),
        (3, "第3波", "玩家 x>=2700 触发", 1, 3, 2700),
        (4, "第4波", "玩家 x>=4000 触发", 1, 4, 4000),
    ]
    new_table("LevelWaveConfig.xlsx", fields, rows)

    # legacy Level_1.gd Monster_group / Monster_position_x / _y
    waves = [
        (1, [1] * 9, [500, 400, 700, 700, 700, 700, 700, 700, 700],
         [350, 320, 300, 300, 300, 300, 300, 300, 300]),
        (2, [2] * 14, [2000] * 14, [300] * 14),
        (3, [2] * 15, [3000] * 15, [300] * 15),
        (4, [2, 2, 2, 2, 2, 3], [4500] * 6, [300] * 6),
    ]
    rows = []
    sid = 1
    for wave_id, monsters, xs, ys in waves:
        for i, mid in enumerate(monsters):
            rows.append((sid, f"波{wave_id}-{i + 1}", f"第{wave_id}波第{i + 1}只",
                         wave_id, mid, xs[i], ys[i]))
            sid += 1
    fields = [
        ("Id", "int", "刷怪点ID"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("WaveId", "int", "波次ID(LevelWaveConfig.Id)"),
        ("MonsterId", "int", "怪物ID(MonsterConfig.Id;2/3待迁)"),
        ("X", "float", "出生坐标 x(旧 Monster_position_x)"),
        ("Y", "float", "出生坐标 y(旧 Monster_position_y)"),
    ]
    new_table("LevelSpawnConfig.xlsx", fields, rows)


if __name__ == "__main__":
    print("enums:")
    build_enums()
    print("table registry:")
    build_table_registry()
    print("entity / ui:")
    build_entity_ui()
    print("hero:")
    build_hero()
    print("monster:")
    build_monster()
    print("battle:")
    build_battle()
    print("level:")
    build_level()
    print("done.")
