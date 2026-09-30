# Configs/ 目录规范（Luban 配置管线）

> 裁决顺序见根规范 §2；本文件只写本目录特有约定。

- `GameConfig/luban.conf` + `Defines/external_types.xml`：表定义与外部类型映射（vector2 → Godot 类型）。
- `GameConfig/Datas/`：全部数值源（`__tables__` / `__beans__` / `__enums__` + 各业务表）。**数值的唯一真相在这里**。
- `GameConfig/CustomTemplate/`：`ConfigSystem.cs` / `ExternalTypeUtil.cs` 生成模板。
- `Localization/本地化.xlsx`：本地化源。

## 导表流程

`Datas/*.xlsx` → `gen_code_bin_to_project_lazyload.bat`（或编辑器 TopMenu → Generate File → GameConfig File）→ `GameProto/GameConfig/*.cs` + `DataTables/GameConfigs/*.bytes` → 运行时 `ConfigSystem.Instance.Tables.TbXxx`。

- 生成物禁手改；字段不对改表再导。
- 新枚举/表：改 `__enums__.xlsx` 或新增表并在 `__tables__.xlsx` 登记 → 导表 → 《实体.xlsx》《界面UI.xlsx》配 `AssetPath`。

## 表结构要求

- 业务表必含 `NameCn`、`Desc`；多行表（`mode=map`，默认）必含 `Id` 主键，单行全局表（`mode=one`，如 `BattleConfig`）不设。
- 有旧项目对应物的表必含 `LegacyId` 溯源列；没有对应物的表不设。
- 每字段写中文注释（进生成代码的 XML 文档）。
- 资源引用用显式路径列（如 `IconPath`），禁止字符串拼接派生路径。
- 音效（定稿）：唯一资产表 `SoundConfig`（`Id`=显式枚举值 / `Key` / `NameCn` / `Desc` / `LegacyId` / `Group`=Music|SFX|UI / `Path`）；**谁在何时播什么写在触发者行上**——攻击音 `AttackConfig.SoundId/HitSoundId`，受击/死亡语音 `HeroConfig`/`MonsterConfig`，BGM `LevelConfig.BgmSoundId`；不建绑定表，统一入口 `GF.Sound.PlaySound(资源, 组名)`。语义归属：命中音属攻击方、受击/死亡语音属受害方；命名按声音本身（如 `WukongImpact`），不按用途。
- 攻击"用法"写在攻击行（`OwnerId` + `ComboIndex` + `AiWeight`），连段用序号不用 `NextAttackId` 指针；**关联表只在每链接有独立数据或多主体参数不同时才建**。
- 怪物 AI（M5）：决策节奏在 `MonsterConfig`（`SightRange / AttackRange / AttackRangeSlack / AttackDesire / AttackInterval / PatrolInterval / PatrolIdleChance / PatrolRadius / BehitCalmTime`，阶级 `Rank`、霸体 `SuperArmor`）；招式用法在 `AttackConfig`（`AiPriority` 0=普攻池 / >0=技能，`AiRange / AiCooldown / AiInitCooldown`）。普攻池的 `AiRange` 上限须 ≥ `AttackRange + AttackRangeSlack`（否则站定后无招可出，运行时会告警）。精英/Boss 的差异优先用数据表达，语义见 `GameScripts/Entity/AGENTS.md`「怪物 AI」。
- 枚举值显式写死，不靠自动递增。

## 工作约定

- 先加表再写代码；AI 需要新数值时报告人类，不自行扩表。
- 表种子/修复用 `Tools/ConfigBootstrap/build_m2_tables.py`（不覆盖手工数值；结构变更须 `force_rows`）。
- 旧项目数值只作参考，人工录入（根规范 §11）。
- 导表后确认 `dotnet build` 通过；`GameProto/` 与 `DataTables/` 产物一起提交。
