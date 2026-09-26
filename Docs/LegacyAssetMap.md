# LegacyAssetMap —— 旧项目素材迁移映射

> 来源：`P:\Godot_Project\ZMXY_BHYH`（只读素材库，见 AGENTS.md §9）
> 目标：`Godot/GodotProject/TheGame/`
> 迁移原则见 AGENTS.md §8.2 / §9.3：仅搬当前阶段所需；战斗图集必须重命名为 `<entity>_<state>`（Collection Res 全树 basename 唯一，§8.4）。

## 当前阶段范围（M1）

`wukong` + `huaguoshan_monkey`（旧 Monster1）+ `Level_1`（花果山）。

## 贴图映射

| 旧路径 | 新路径 | 规格 / 说明 |
| --- | --- | --- |
| `Art/HeroPicture/Role1AllEquipment/Role_1_Body_Empty.png` | `Sprites/Characters/Heroes/wukong/wukong_body.png` | 1200×2800，6 列 × 14 行，200×200 / 帧（84 帧） |
| `Art/HeroPicture/Role1AllEquipment/Role_1_Eq_Empty.png` | `Sprites/Characters/Heroes/wukong/wukong_weapon_empty.png` | 同上网格；**武器层**（空手），见下「武器层」 |
| `Art/Monster/Monster1/Wait.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_idle.png` | 43×63，1 帧 |
| `Art/Monster/Monster1/Walk.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_run.png` | 64×88 × 4 帧 |
| `Art/Monster/Monster1/Hit.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_attack.png` | 91×83 × 6 帧 |
| `Art/Monster/Monster1/Hurt.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_hurt.png` | 68×86 × 2 帧 |
| `Art/Monster/Monster1/Death.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_death.png` | 91×88 × 6 帧 |
| `Art/Level/Level_1/19_1.png` | `Sprites/Levels/huaguoshan/level_1_bg_end.png` | 1440×690；旧 `BackGround/End/end2` 背景端块 |
| `Art/Level/Level_1/48.png` | `Sprites/Levels/huaguoshan/level_1_front.png` | 4957×633；旧 `BackGround/front` 前景视差层 |
| `Art/Level/Level_1/183.png` | `Sprites/Levels/huaguoshan/level_1_floor.png` | 4812×170；旧 `BackGround/floor3/floor2` 地板视差层 |

不拷贝旧 `.import`，由新工程重新导入（AGENTS.md §9.3.1）。

## 武器层（换装测试用）

旧项目武器是**独立叠加层**：节点 `Action/RoleEquipment` ← `Role_1_Eq_<武器键>.png`，运行时由 `BaseHero.onEqchange()` → `ChangeEq()` → `Global.LoadRole1EQ()` 按当前装备切换（同网格 6×14，动画帧与身体层对齐）。本次先迁 5 张代表性图，其余 15 把按需再迁：

| 旧文件 | 新文件 | 旧键 / 名称 |
| --- | --- | --- |
| `Role_1_Eq_Empty.png` | `wukong_weapon_empty.png` | 空手 |
| `Role_1_Eq_ryjgb.png` | `wukong_weapon_golden_cudgel.png` | `ryjgb` 如意金箍棒 |
| `Role_1_Eq_wkjdyhwq.png` | `wukong_weapon_wooden_stick.png` | `wkjdyhwq` 经典原画·木棍 |
| `Role_1_Eq_qld.png` | `wukong_weapon_dragon_blade.png` | `qld` 青龙刀 |
| `Role_1_Eq_zjbtg.png` | `wukong_weapon_purple_gold_cudgel.png` | `zjbtg` 紫金镔铁棍 |

每张武器图集对应一个 `wukong_weapon_<name>_animations.tres`（9 个动画，帧序列取自 `Action/RoleEquipment:frame`，与身体层同帧对齐）。

## 动画映射

源数据为旧 `.tscn` 内嵌 `Animation`/`AtlasTexture`，由 `Tools/LegacyMigration/gen_animations.py` 一次性解析生成，禁止手工重切片（AGENTS.md §9.3.4）。

### wukong —— `Sprites/Characters/Heroes/wukong/wukong_animations.tres`

源：`Scene/Hero/Role_1/Role1.tscn`，轨道 `Action/RoleBody:frame`，`RolePlayer.speed_scale = 2.0`。

| 旧动画名 | 新动画名 | 循环 |
| --- | --- | --- |
| `wait` | `idle` | 是 |
| `run` | `run` | 是 |
| `jump1` | `jump` | 否 |
| `hit1` | `attack_1` | 否 |
| `hit2` | `attack_2` | 否 |
| `hit3` | `attack_3` | 否 |
| `hit4` | `attack_4` | 否 |
| `hurt` | `hurt` | 否 |
| `death` | `death` | 否 |

暂缓（技能/特效类，M1 不迁）：`drop` `hmz__` `hyjj` `hytj` `jdy` `jdy_1` `lyfb` `lys` `qsez` `slz` `zz`。

### huaguoshan_monkey —— `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_animations.tres`

源：`Scene/Monster/Monster_1.tscn` 的 `AtlasTexture` 切分规格，按固定网格重新生成。

| 旧动画名 | 新动画名 | 帧数 | 循环 |
| --- | --- | --- | --- |
| `Wait` | `idle` | 1 | 是 |
| `Walk` | `run` | 4 | 是 |
| `Hit` | `attack_1` | 6 | 否 |
| `Hurt` | `hurt` | 2 | 否 |
| `Death` | `death` | 6 | 否 |

## 复现方式

```
python Tools/LegacyMigration/gen_animations.py
```

脚本读取旧项目 `.tscn`，输出到 `TheGame/Sprites/...`；旧项目保持只读，不写入任何文件。

## 待办

- `wukong` 的 `hurt` / `death` 在旧动画中仅单帧（旧工程另有 `RoleDeath.png` 等独立节点），后续接入死亡表现时再评估。
- `huaguoshan_monkey` 帧时长当前统一取 0.1s（旧 `AnimatedSprite2D.speed` 语义），M5 调 AI 时按手感回填。
- 武器层已迁 5 张（含空手）用于换装测试；其余 15 把武器与 25 套防具图集待装备系统阶段按 Luban 表按需迁入。
- 身体层 `wukong_body.png` 的部分动作帧自带默认棍（旧美术遗留）；接武器层后需确认是否与 `wukong_weapon_*` 叠加导致重复，必要时清理身体层里的武器像素。
