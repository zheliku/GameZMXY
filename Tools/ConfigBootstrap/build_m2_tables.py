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

就地改表，绝不再重建工作簿：
  * 列宽 / 样式 / 手工改过的数据都必须保住 —— 数值的权威是 xlsx 本身；
  * 脚本里的 rows 只是"首次建表时的种子值"，表里已有数据就一律不动。

Run:  python Tools/ConfigBootstrap/build_m2_tables.py
Then: Configs/GameConfig/gen_code_bin_to_project_lazyload.bat
"""

import os

import openpyxl
from openpyxl import Workbook

ROOT = r"P:\Godot-Project\GameZMXY"
DATAS = os.path.join(ROOT, "Configs", "GameConfig", "Datas")


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------
def new_table(file_name, fields, rows, force_rows=False):
    """
    就地写表头，数据只在空表时播种。

    关键：已存在的工作簿一律 load_workbook 后改单元格，不用 Workbook() 重建
    —— 重建会丢掉列宽与手工样式（用户会手工调列宽）。
    数据行同理：表里已有数据就保留，避免把手工改过的数值盖回种子值。

    force_rows=True 仅用于一次性修复错位数据（保留列宽，重写数据行），
    例如 2026-09-27 表头 schema 与手工合并列对齐前写坏的 HeroConfig / AttackConfig。
    """
    path = os.path.join(DATAS, file_name)
    if os.path.exists(path):
        wb = openpyxl.load_workbook(path)
        ws = wb.active
    else:
        wb = Workbook()
        ws = wb.active
        ws.title = "Sheet1"

    has_data = ws.max_row > 5 and any(
        ws.cell(row=r, column=2).value not in (None, "")
        for r in range(6, ws.max_row + 1)
    )

    # 结构变更防护：只有"在表尾追加列"才能靠回填空格安全迁移；
    # 中间插入/删除/改名/换序会让旧单元格整体错位（纯字符串表 Luban 不会报错，会静默改错数据）。
    # 因此这类变更必须显式 force_rows=True（重写数据行），否则直接拒绝执行。
    if has_data and not force_rows:
        old_names = []
        for c in range(2, ws.max_column + 1):
            old_names.append(ws.cell(row=1, column=c).value)
        while old_names and old_names[-1] in (None, ""):
            old_names.pop()
        new_names = [f[0] for f in fields]
        if old_names != new_names[: len(old_names)]:
            raise SystemExit(
                f"  [拒绝] {file_name}: 表头结构发生变化（不是纯表尾追加），"
                f"回填空格会造成数据错位。\n"
                f"    旧表头: {old_names}\n"
                f"    新表头: {new_names}\n"
                f"    请核对数据后对该表显式传 force_rows=True。"
            )

    header = [
        ["##var"] + [f[0] for f in fields],
        ["##var"] + [None] * len(fields),
        ["##type"] + [f[1] for f in fields],
        ["##group"] + [None] * len(fields),
        ["##"] + [f[2] for f in fields],
    ]
    for r, cells in enumerate(header, start=1):
        for c, val in enumerate(cells, start=1):
            ws.cell(row=r, column=c, value=val)
        # 列数收缩时清掉旧表头的残留单元格，否则 Luban 报"列重复"。
        # 注意 openpyxl 的 cell(..., value=None) 不写入（None 被视为"未提供值"），必须用属性赋值。
        for c in range(len(fields) + 2, ws.max_column + 1):
            ws.cell(row=r, column=c).value = None

    has_data = ws.max_row > 5 and any(
        ws.cell(row=r, column=2).value not in (None, "")
        for r in range(6, ws.max_row + 1)
    )

    if has_data and not force_rows:
        # 已有数据：只补"空单元格"（用于表尾新增列时的回填），已经有值的一律不动
        patched = 0
        for r, seed in enumerate(rows, start=6):
            for i, val in enumerate(seed):
                if val is None:
                    continue
                cell = ws.cell(row=r, column=i + 2)
                if cell.value in (None, ""):
                    cell.value = val
                    patched += 1
        note = f"，回填空单元格 {patched} 个" if patched else ""
        print(f"  {file_name}: 表头已更新，数据保留（{ws.max_row - 5} 行）{note}")
    else:
        if ws.max_row > 5:
            ws.delete_rows(6, ws.max_row - 5)
        for r in rows:
            ws.append([None] + list(r))
        tag = "修复重写" if has_data else "空表，播种"
        print(f"  {file_name}: {tag} {len(rows)} 行，{len(fields)} 列")

    wb.save(path)


def seed_table(file_name, header_rows, rows):
    """
    框架自带的表（实体 / 界面UI）：只在没有数据行时播种。
    已经有我们配好的行（或用户改过的行）就跳过，绝不覆盖。
    """
    path = os.path.join(DATAS, file_name)
    wb = openpyxl.load_workbook(path)
    ws = wb.active

    has_data = ws.max_row > header_rows and any(
        ws.cell(row=r, column=2).value not in (None, "")
        for r in range(header_rows + 1, ws.max_row + 1)
    )
    if has_data:
        print(f"  {file_name}: 已有数据，跳过（{ws.max_row - header_rows} 行）")
        return

    if ws.max_row > header_rows:
        ws.delete_rows(header_rows + 1, ws.max_row - header_rows)
    for r in rows:
        ws.append(list(r))
    wb.save(path)
    print(f"  {file_name}: 空表，播种 {len(rows)} 行")


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
    # 音效引用：None=该动作无音效
    # 值显式写死：自动递增会让"中间插一条音效"导致后续 Id 全部漂移（表与代码一起错）
    # 命名规则：按"声音本身是什么"命名，不按"谁在什么时机用"命名（用途写在各触发者的表里）。
    # WukongImpact = 悟空的棍打中东西的命中音（旧 6_BeattackByRole1，旧名 monster_hurt 是错的）
    ("Sound.SoundId", False, True, [
        ("None", "无", 0, None),
        ("WukongAttack2", "悟空挥棍2", 1, None),
        ("WukongAttack1And3", "悟空挥棍1/3", 2, None),
        ("WukongAttack4", "悟空挥棍4", 3, None),
        ("WukongHurt", "悟空受击语音", 4, None),
        ("WukongDeath", "悟空死亡语音", 5, None),
        ("WukongImpact", "悟空命中音(棍打中东西)", 6, None),
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
    ("Hero.TbHeroLevelConfig", "HeroLevelConfig", "HeroLevelConfig.xlsx", None),
    ("Monster.TbMonsterConfig", "MonsterConfig", "MonsterConfig.xlsx", None),
    ("Battle.TbAttackConfig", "AttackConfig", "AttackConfig.xlsx", None),
    ("Battle.TbBattleConfig", "BattleConfig", "BattleConfig.xlsx", "one"),
    ("Level.TbLevelConfig", "LevelConfig", "LevelConfig.xlsx", None),
    ("Level.TbLevelWaveConfig", "LevelWaveConfig", "LevelWaveConfig.xlsx", None),
    ("Level.TbLevelSpawnConfig", "LevelSpawnConfig", "LevelSpawnConfig.xlsx", None),
    ("Sound.TbSoundConfig", "SoundConfig", "SoundConfig.xlsx", None),
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
    seed_table("实体.xlsx", 5, [
        (None, 1, "Wukong", "res://TheGame/Entitys/WukongEntity.tscn", "Actor", 0),
        (None, 2, "HuaguoshanMonkey", "res://TheGame/Entitys/HuaguoshanMonkeyEntity.tscn", "Actor", 0),
    ])
    seed_table("界面UI.xlsx", 5, [
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
        ("WalkSpeed", "float", "慢走速度 px/s"),
        ("RunSpeed", "float", "跑步(快走)速度 px/s"),
        ("RunDoubleTapWindow", "float", "双击方向键进入跑步的判定窗口(秒)"),
        ("IdleEmoteDelay", "vector2", "待机后隔多久随机播一次憨笑(秒,x=最短,y=最长,写法 6,12)"),
        ("JumpSpeed", "float", "起跳速度(向上为正,旧 jump_power=-540)"),
        ("Gravity", "float", "重力(旧 gravity=980 向下)"),
        ("JumpCountMax", "int", "最多跳跃次数(含地面一段与空中段,旧 jump_count<2 判据)"),
        ("HurtSoundId", "Sound.SoundId", "受击语音(自己挨打时的声音,None=无;按受害者选音,BaseHero.gd:592)"),
        ("DeathSoundId", "Sound.SoundId", "死亡语音(None=无)"),
    ]
    rows = [
        (1, 1, "悟空", "齐天大圣", "Wukong",
         80, 50, 8, 10, 10,
         50, 15, 4, 1, 1,
         0, 0, 0, 0, 0, 0, 0, 0,
         0, 0, 0,
         120, 240, 0.3, "6,12", 540, 980, 2,
         "WukongHurt", "WukongDeath"),
    ]
    # force_rows：修复 2026-09-27 表头与手工合并列错位时写坏的数据行（数值以 git HEAD 版本为准）
    new_table("HeroConfig.xlsx", fields, rows, force_rows=True)

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
        ("HurtSoundId", "Sound.SoundId", "受击语音(旧项目怪物无语音素材,填 None;M5 用)"),
        ("DeathSoundId", "Sound.SoundId", "死亡语音(同上)"),
        # M4 表尾追加：怪物受击击退/落地需要重力（旧 BaseMonster.gd:131 gravity=980，与英雄同语义）
        ("Gravity", "float", "重力 px/s²(旧 gravity=980 向下)"),
    ]
    rows = [
        (1, 1, "花果山猴子", "花果山小怪", "HuaguoshanMonkey", 5,
         60, 50, 80,
         0, 0, 0, 0, 0, 0, 0, 0,
         0, 80, 300, 45, 70, 0, 1,
         "None", "None", 980),
    ]
    # force_rows：移除表尾的音效列后必须重写数据行，否则旧单元格残留会造成列错位
    new_table("MonsterConfig.xlsx", fields, rows, force_rows=True)


def build_battle():
    # legacy keys: Role1.gd objattackDic / Monster_1.gd objattackDic
    # 成对数值采用合并单列（用户整理后的形态）：一格写 "min,max"，由 bean sep 解析
    fields = [
        ("Id", "int", "攻击ID"),
        ("LegacyId", "string", "旧 objattackDic 键名"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("Animation", "string", "播放的动画名"),
        ("PowerScale", "vector2", "攻击力倍率范围(乘英雄攻击,X=下限,Y=上限,写法 1,1.2)"),
        ("FlatPower", "int", "固定攻击力(不与属性挂钩)"),
        ("DamageKind", "Battle.DamageKind", "伤害类型"),
        ("Knockback", "vector2", "击退(X>0=远离攻击者,Y<0=向上;单位同旧 hurtBack,乘 BattleConfig.KnockbackScale* 换算 px/s,写法 2,0)"),
        ("WsGain", "vector2i", "命中获得无双值范围(旧 WSValue,X=下限,Y=上限,写法 3,5)"),
        ("HitProtect", "int", "受击保护累计值(旧 HitProtect)"),
        ("SoundId", "Sound.SoundId", "起手音效(SoundConfig;None=无)"),
        ("HitSoundId", "Sound.SoundId", "命中音效(打中目标时播,None=无;旧项目按攻击者选音)"),
        ("OwnerId", "Entity.EntityId", "这招属于谁(None=通用招,将来多主体共享用)"),
        ("ComboIndex", "int", "连段第几段(0起;同一 OwnerId 内连续,越大越靠后)"),
        ("AiWeight", "int", "AI 选招权重(怪物 AI 用;英雄普攻填 0)"),
        # 2026-09-30 审查裁决：删 Interval / HitBoxOffset / HitBoxSize 三列——
        # 出招时序与判定盒几何全部归动画（gen_animations.py 的方法轨道，同旧项目动画内 keyframe），
        # 表只存数值（威力/击退/无双/保护/音效/归属）。此前表值 -10 还漏减了猴子身体层 (0,-13) 的 Y 偏移。
    ]
    # 为什么"归属/连段顺序/AI 权重"写在攻击行上，而不是 HeroAttackConfig/MonsterAttackConfig 关联表：
    #   关联表里每行唯一独有的信息只有"顺序"或"权重"，NameCn/Desc 与攻击行完全重复；
    #   而归属、序号、权重本来描述的就是"这招被谁、怎么用"——属于攻击行的用法数据。
    #   序号（而非 NextAttackId 指针）的好处：顺序在表里一眼可见、无入口歧义、无断链/成环风险。
    #   出现"每链接独立数据"（如前置条件/取消窗口）或"多主体共享同一招且参数不同"时，才需要关联表。
    # 音效接线以旧代码为准（add_music 的 method 轨道），不是文件名的字面意思：
    # hit1→39、hit2→40、hit3→39、hit4→38（Role1.tscn method 轨道实测）
    rows = [
        (1001, "role1.hit1", "悟空普攻1", "普攻第一段", "attack_1", "1,1.2", 0, "Physics", "2,0", "3,5", 0, "WukongAttack1And3", "WukongImpact", "Wukong", 0, 0),
        (1002, "role1.hit2", "悟空普攻2", "普攻第二段", "attack_2", "0.9,1.1", 0, "Physics", "2,0", "3,5", 0, "WukongAttack2", "WukongImpact", "Wukong", 1, 0),
        (1003, "role1.hit3", "悟空普攻3", "普攻第三段", "attack_3", "1,1.2", 0, "Physics", "2,0", "3,5", 0, "WukongAttack1And3", "WukongImpact", "Wukong", 2, 0),
        (1004, "role1.hit4", "悟空普攻4", "普攻第四段(击退收招)", "attack_4", "1.2,1.4", 0, "Physics", "6,-5", "3,5", 0, "WukongAttack4", "WukongImpact", "Wukong", 3, 0),
        # 猴子击退 X：旧值 -3 是为抵消旧项目朝向符号写的负数；新语义"X>0=远离攻击者"统一为正（2026-09-29 M4）
        (2001, "monster1.hit1", "猴子普攻", "猴子唯一攻击", "attack_1", "0,0", 10, "Physics", "3,-6", "0,0", 10, "None", "None", "HuaguoshanMonkey", 0, 100),
    ]
    # force_rows：修复 2026-09-27 表头与手工合并列错位时写坏的数据行
    new_table("AttackConfig.xlsx", fields, rows, force_rows=True)

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
        # M4 表尾追加（2026-09-29）：旧公式里写死的封顶与击退换算系数
        # (BaseHero.gd:757-759/806-810, BaseMonster.gd:736-770/902-915, 击退 BaseHero.gd:818 / BaseMonster.gd:475)
        ("LvMissCap_HeroDef", "float", "等级压制-英雄防守闪避系数封顶"),
        ("LvMissCap_MonsterDef", "float", "等级压制-怪物防守闪避系数封顶"),
        ("LvCritCap", "float", "等级压制-暴击系数封顶(人怪两侧一致)"),
        ("LvLuckyCap", "float", "等级压制-幸运系数封顶(人怪两侧一致)"),
        ("LvDamageCapLv_HeroDef", "int", "等级压制-英雄防守时伤害最多按几级算"),
        ("LvDamageCapLv_MonsterDef", "int", "等级压制-怪物防守时伤害最多按几级算"),
        ("KnockbackScaleX_HeroDef", "float", "英雄被击退横向换算: px/s = Knockback.X × 本值"),
        ("KnockbackScaleX_MonsterDef", "float", "怪物被击退横向换算: px/s = Knockback.X × 本值"),
        ("KnockbackScaleY", "float", "击退纵向换算: px/s = Knockback.Y × 本值(人怪一致)"),
    ]
    rows = [
        ("战斗常数", "沿用旧项目人怪两侧不一致的基数,集中在此处",
         250, 250, 100, 100, 100, 70, 100, 100, 50, 2,
         0.03, 0.07, 0.04, 0.11, 0.07, 0.05,
         1, 0.9, 1, 0.7, 5, 2, 25, 30, 15),
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


def build_sound():
    """
    音效资产表：SoundId 枚举 → 音频资源 + 播放组。**这是唯一的音频表**。

    为什么有 Key 列：表内 Id 是枚举值（int），单看 `Id=4` 不知道是哪个音效；
    Key 直接写枚举名（`WukongHurt`），读表/搜代码的人不用去翻 __enums__.xlsx。
    Key 与枚举的一致性由建表脚本保证（同一份列表生成），不会被手改漂移。

    为什么有 Group 列：**框架的统一入口就是 `GF.Sound.PlaySound(资源, 组名)`**
    （GodotGameFrameworkCore/Sound/SoundComponent.cs:153），组名 = 启动时从
    SoundGroupRes.tres 注册的 `Music` / `SFX` / `UI`（各自映射到 Godot 总线，
    带独立的音量/静音/代理数）。所以这里直接存框架的组名，代码一句
    `GF.Sound.PlaySound(cfg.Path, cfg.Group)` 就能播任意组的一次性声音，
    不需要我们自己再搞枚举或路由 switch。合法值就是那三个（由校验工具把关）。

    LegacyId 是旧项目文件名；注意旧文件名与实际用途不一致（如 40_Role1_hit1AndHit2
    实际只被 hit2 使用），接线以 Role1.tscn method 轨道的 add_music 实测为准。
    """
    fields = [
        ("Id", "int", "音效ID(与 SoundId 枚举值一致)"),
        ("Key", "string", "枚举名(与 SoundId 一致,便于读表/搜索)"),
        ("NameCn", "string", "中文名"),
        ("Desc", "string", "描述"),
        ("LegacyId", "string", "旧音频文件名"),
        ("Group", "string", "框架声音组名(Music/SFX/UI,对应 SoundGroupRes)"),
        ("Path", "string", "音频资源路径"),
    ]
    rows = [
        (1, "WukongAttack2", "悟空挥棍2", "普攻第2段起手；旧 40_Role1_hit1AndHit2 实际只被 hit2 用", "40_Role1_hit1AndHit2.mp3", "SFX", "res://TheGame/Audios/SFX/wukong/wukong_attack_2.mp3"),
        (2, "WukongAttack1And3", "悟空挥棍1/3", "普攻1、3段共用起手", "39_Role1_hit3AndHit4.mp3", "SFX", "res://TheGame/Audios/SFX/wukong/wukong_attack_1_3.mp3"),
        (3, "WukongAttack4", "悟空挥棍4", "普攻第4段起手", "38_Role1_hit5.mp3", "SFX", "res://TheGame/Audios/SFX/wukong/wukong_attack_4.mp3"),
        (4, "WukongHurt", "悟空受击语音", "悟空自己挨打时的语音(按受害者选音,BaseHero.gd:592)", "49_Role1_beAttack.mp3", "SFX", "res://TheGame/Audios/SFX/wukong/wukong_hurt.mp3"),
        (5, "WukongDeath", "悟空死亡语音", "悟空死亡", "59_Role1_dead.mp3", "SFX", "res://TheGame/Audios/SFX/wukong/wukong_death.mp3"),
        (6, "WukongImpact", "悟空命中音", "悟空的棍打中东西的命中音(按攻击者选音,BaseMonster.gd:652)", "6_BeattackByRole1.mp3", "SFX", "res://TheGame/Audios/SFX/wukong/wukong_hit_impact.mp3"),
    ]
    new_table("SoundConfig.xlsx", fields, rows, force_rows=True)


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
    print("sound:")
    build_sound()
    print("done.")

