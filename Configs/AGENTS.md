# Configs/ 目录规范（Luban 配置管线）

> 上级规范：`../AGENTS.md`（根规范）。冲突时：根规范 > 本文件 > 推断。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 目录内容

- `GameConfig/luban.conf` + `Defines/` —— Luban 表定义。
- `GameConfig/Datas/` —— 全部数值源：`__tables__.xlsx` / `__beans__.xlsx` / `__enums__.xlsx` + 各业务表（实体、界面UI、怪物、技能、关卡波次……）。**数值的唯一真相在这里**。
- `GameConfig/CustomTemplate/` —— 生成 `ExternalTypeUtil.cs` / `ConfigSystem.cs` 的自定义模板。
- `GameConfig/gen_code_bin_to_project*.{bat,sh}` —— 导表脚本。
- `Localization/本地化.xlsx` —— 本地化源。

## 导表流程

`Datas/*.xlsx` → 跑 `gen_code_bin_to_project_lazyload.bat`（或编辑器 TopMenu → Generate File → GameConfig File）→ 生成 `TheGame/GameScripts/GameProto/GameConfig/*.cs` + `TheGame/DataTables/GameConfigs/*.bytes` → 运行时 `ConfigSystem.Instance.Tables.TbXxx` 访问。

- 生成物（GameProto/ 与 DataTables/）**禁手改**；发现字段不对就改这里再导表。
- 加新枚举/表的标准动作：改 `__enums__.xlsx`（EntityId / UIFormId 等）或新增业务表并在 `__tables__.xlsx` 登记 → 导表 → 在《实体.xlsx》《界面UI.xlsx》里配 `AssetPath` → 代码里用编译期安全的枚举引用。

## 表结构要求

- 每张业务表必须含 `NameCn`、`Desc`；多行表（`mode=map`，默认）必须含 `Id` 作为索引主键；单行全局表（`mode=one`，如 `BattleConfig`）不设 `Id`。
- **有旧项目对应物**的表必须含 `LegacyId`（旧项目 ID 或键名，用于溯源对照，类型随对应物）。没有对应物的表（如等级曲线、战斗常数、波次/刷怪点）不设该列，避免空列噪音。
- 每个字段必须写中文注释（进生成的 C# XML 文档，IDE 悬停可见）。
- 遗留资产的文件名不参与语义：图标等资源在表里用**显式路径列**（如 `IconPath`）指向，不用字符串拼接派生（旧项目 `load(".../" + name + ".png")` 的做法禁止复刻）。
- 音效（2026-09-28 定稿）：**唯一音频表 `SoundConfig`** 只登记资产（`Id`=枚举值 / `Key`=枚举名 / `NameCn` / `Desc` / `LegacyId` / `Group`=框架声音组名 `Music`/`SFX`/`UI` / `Path`）。**谁在什么时机播，写在触发者那一行**：攻击音 `AttackConfig.SoundId`/`HitSoundId`、角色语音 `HeroConfig`/`MonsterConfig.HurtSoundId`/`DeathSoundId`、关卡 BGM `LevelConfig.BgmSoundId`。不要另建"绑定表"，也不要给资产表加播放相关枚举——框架的统一入口就是 `GF.Sound.PlaySound(资源, 组名)`。
- 音效语义归属：**命中音属于攻击方**（谁打的），**受击/死亡语音属于受害方**（谁挨打）。命名按"声音本身是什么"（如 `WukongImpact`），不按用途（如 `MonsterHurt`）。
- 攻击的"用法"写在攻击行上，**不建关联表**（2026-09-28 定）：`AttackConfig.OwnerId`（属于谁，None=通用）+ `ComboIndex`（连段第几段）+ `AiWeight`（AI 选招权重）。英雄连段 = 过滤 `OwnerId` + 按 `ComboIndex` 排序；怪物 AI = 过滤 `OwnerId` + 按 `AiWeight` 抽招。用**序号**而不是 `NextAttackId` 指针（顺序可见、无入口歧义、无断链/成环）。
- **关联表只在满足其一才建**：① 每链接有独立数据（前置条件、取消窗口、AI 冷却…）；② 多主体共享同一行但各自参数不同。否则一律用主体行上的列/序号表达。
- 枚举值显式写死（不靠自动递增）：中间插入一项会让后续所有 Id 漂移，表与代码一起错。

## 工作约定

- 配表 = 数据，代码 = 逻辑。加怪/加装备/加技能**先加表再写代码**；AI 需要新数值时报告给人类加表，不自行扩表。
- 旧项目的判定帧坐标、伤害数值、掉落表只作参考，**人工录入**，不写脚本自动翻译（根规范 §11.3）。
- 导表成功后确认：`dotnet build` 通过、`GameProto/` 与 `DataTables/` 产物一起提交（素材迁移另见根规范 §12）。
