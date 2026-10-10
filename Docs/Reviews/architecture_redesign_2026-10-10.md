# 架构重构记录（2026-10-10）

> 计划：`.kilo/plans/1791468983983-architecture-redesign.md`（决策 D1–D10 均经用户确认）。取代未实施的 `.kilo/plans/1791383001385-architecture-domain-refactor.md`（Model/System 方案）。
> 规范：本轮重写 `Docs/ProjectGuidelines/00/10/20/30/40/50/60`，新增 `70-ProfileAndSave.md`。本文件只记录本轮事实，不替代规范。

## 动因

- `HeroEntity` 同时持有输入/状态机、持久进度（等级/经验/金币）、资源池、成长公式、奖励结算与读档复制；背包、商店、地图等没有英雄实体的界面无处取数。
- 成长分裂：经验查表、HP/攻防为代码公式 `Base + (L-1) × Grow`（两处重复）；怪物攻击固定 0。
- 奖励链 `LevelSpawner`（持有经验索引）→ `LevelController` → `HeroEntity`；存档由 `ProcedureGame` 手拼快照、每次经验变化写盘、无版本。
- `BindableProperty.Value` 公开可写、逐字段订阅，等级/经验/上限存在中间态。
- 命名空间混用（`GameLogic` / `GameLogic.UI` / 全局），临时目录 `Progression/`、`Reward/` 等。

## 决策摘要

| # | 决策 |
| --- | --- |
| D1 | GGF 原生分层 + 作用域所有权 + 显式注入；摒弃 QFramework 式 Model/System |
| D2 | `PlayerProfile` 由 `GameContext` 持有，经流程状态机数据传递 |
| D3 | 状态源聚合事件 + 拉取快照；控件被动；删除 `BindableProperty` |
| D4 | `StatSheet`：Luban `StatType` + 按来源修正（Flat/PercentAdd/PercentMult） |
| D5 | 共享经验曲线 + 每英雄逐级成长表（`StatBlock`）；怪物内嵌 `StatBlock` |
| D6 | 存档只含 ID + 可变状态、版本逐级迁移、检查点写盘 |
| D6b | 关卡事务：通关与死亡提交（死亡也保存，用户审阅修订），中途离开/错误回滚 |
| D7 | 授权修改 `GameFramework.tscn` 流程清单：Preload / LoadProfile / Level |
| D8 | 目录 = 命名空间，领域优先，纯 C# 与节点分开 |
| D9 | 本轮：骨架 + 迁移现有功能 + 规范；技能/Buff/背包/装备只定落位 |
| D10 | 存档读写统一经 `GF.Archive`；授权加固 `ArchiveSystem` / `EasySave` |

## 改动

### 配置（P1）

- 一次性脚本 `Tools/ConfigBootstrap/migrate_stat_tables.py`（已执行，勿再运行）：
  - `__enums__`：`Stat.StatType`（16 项）、`Stat.StatModifierKind`；`__beans__`：`Stat.StatBlock`、`Hero.HeroGrowthConfig`、`Monster.MonsterConfig`。
  - 新表 `HeroGrowthConfig.xlsx`（`TbHeroGrowthConfig`，list，联合索引 `HeroId+Level`）：按旧公式逐级展开 1..55 级，手感不变。
  - `HeroConfig` 删除 Base*/Grow*/战斗属性列；`MonsterConfig` 属性改为内嵌 `Stats`（`Power=0`，伤害仍由招式 `FlatPower` 决定），记录类型改由 bean 定义。
  - 新单行表 `ProfileConfig.xlsx`（`StartHeroId=1`、`StartGold=0`）；`BattleConfig` 追加 `DeathRestartDelay=2.5`、`ClearRestartDelay=3.0`。
- 导表：`gen_code_bin_to_project_lazyload.bat`（`AI_MODE=1`）；生成代码与 `.bytes` 只由导表更新。
- `Config/ConfigValidator`：启动期集中校验经验曲线、成长覆盖、建档规则、怪物属性、战斗常数；由 `ProcedurePreload` 调用。

### 领域层（P2，纯 C#）

- `Battle/Stats/`：`StatSheet`、`StatModifier`、`StatSource`、`Vitals`、`MusouGauge`；`CombatantStats.From(side, level, StatSheet)`。
- `Profile/`：`PlayerProfile`、`HeroRecord`、`HeroProgression`（累计经验唯一事实）、`Wallet`、`ExperienceCurve`（迁入）、`HeroStatBuilder`、`HeroLoadout`。
- `Save/`：`ProfileSaveData`、`HeroSaveData`、`PlayerSaveDataV0`（原 `PlayerSaveData`，只读迁移）、`SaveMigrator`、`ProfileMapper`（捕获/重建/回滚）、`SaveService`；`GameData` 改为 `SaveVersion/Profile/Player`，删除 `Score`，字段不设初始值。

### 实体与 HUD（P3）

- `ActorEntity`：持有 `Stats`、`Vitals`、`Level`；`ReceiveHit` 经 `Vitals.Damage` 判死并触发一次 `Died`；`GetCombatStats` 统一为快照。
- `HeroEntity`：`OnShow` 只接受同英雄的 `HeroLoadout`；`ApplyLoadout(loadout, refill)` 供升级调用；持有 `Musou`。删除等级/经验/金币/存档字段、`GainExperience`、内联成长公式。
- `MonsterEntity`：`Stats.SetBase(Config.Stats)`；删除各自的属性快照覆写。
- `LevelSpawner` 删除经验索引，改报 `MonsterDefeat`（值）；`LevelController` 转发 `MonsterDefeated`，`TravelAvailable` 改为只读属性 + `TravelAvailableChanged`。
- HUD：`BattleHudData`（英雄、档案成长、关卡）；订阅聚合事件并拉取刷新；`ResourceBar` 改为被动 `SetValue`。Ge 部分重生成到 `GameLogic.UI`。
- 删除 `Bindable/BindableProperty`、`UI/BattleHudContext`；`DamagePopManager`（SingletonNode）→ 普通对象 `DamagePopPresenter`，由关卡流程/测试场地持有。

### 应用层与框架（P4 / P4a）

- 流程：`ProcedurePrelode` → `ProcedurePreload`（+ 配置校验）；新增 `ProcedureLoadProfile`（读档/迁移/建档、创建 `GameContext`）；`ProcedureGame` → `ProcedureLevel`（只装配与拆除一次关卡运行，结局后按配置延迟重开）。
- `Session/`：`GameContext`、`GameContextVariable`、`LevelRun`（关内结算、关卡事务、`RunStats`）。
- 框架（已授权）：`GameFramework.tscn` 流程清单；`EasySave` 新增原子写/备份 API；`ArchiveSystem` 原子写 + `.bak`、串行、调用时刻序列化、读取回退、覆盖同步目录、返回 `Task<bool>`；`Godot/docs/ArchiveSystem.md` 同步。

### 测试与烟测

- 新 `Tests/ProfileTests`（22 项）：成长、钱包、档案校验、出战装配、成长表与旧公式逐级一致、启动校验、v0 迁移（真实旧 JSON 形状）、空档建档、版本拒绝、映射 JSON 往返、回滚。
- `Tests/BattleTests`：新增 `StatSheetTests`（修正顺序、来源替换与移除、钳制与舍入、通知与复制、死亡只报一次、上限钳制、无双）；删除 `BindablePropertyTests`，`ExperienceCurveTests` 迁到 ProfileTests。
- `UiSmokeScenario` 重写：不再反射私有字段（改用 `ProcedureLevel` 的 internal 只读属性），覆盖首档写回、HUD 聚合刷新/退订/改绑、实体复用不残留修正、升级注入、中途离开回滚不写盘、死亡写盘与自动重开、`.bak` 回退、v0 迁移写回、加载取消、打开失败恢复。
- `LevelSmokeScenario` 改为核对档案经验与通关检查点；`TestArenaController` 用临时档案构建装配。

## 验证

| 命令 | 结果 |
| --- | --- |
| `dotnet build`（`Godot/GodotProject`） | 0 错误；5 个警告均为既有框架代码 |
| `Godot4CSharp_console.exe --headless --build-solutions … --quit` | 无编译错误；退出时 `ComponentInsoector._ExitTree` 报错为既有插件问题 |
| `dotnet test Tests/BattleTests` | 81 通过 |
| `dotnet test Tests/LevelTests` | 16 通过 |
| `dotnet test Tests/ProfileTests` | 22 通过 |
| `-- --smoketest=level` | `SMOKE PASS`（四段、15 击败、档案经验 15、通关检查点）；关停时 `DefaultWebRequestAgentHelper.Reset` 既有异常 |
| `-- --smoketest=ui` | `SMOKE PASS`（含 `.bak` 回退日志） |
| `-- --smoketest=level-cancel` | `SMOKE PASS`；退出码 `0xC0000005`（既有 GC 终结器问题，2026-10-07 记录已登记） |
| `-- --smoketest=ai` | `SMOKE PASS` |
| `-- --smoketest`（默认战斗） | `SMOKE PASS`，32 次状态切换 |
| `python Tools/ProjectMaintenance/validate_game_references.py` | `REFERENCE PASS: 91 serialized paths and 161 UIDs` |

## 行为变化

- 关内收益只在通关或死亡时写盘；中途离开、运行错误、关停不保存本关收益（原先每次经验变化即写盘）。
- 英雄死亡后按 `DeathRestartDelay` 自动重开本关；通关后按 `ClearRestartDelay` 重开（原先通关后停在关卡）。
- 旧存档（v0）首次读取时迁移为 v1 并写回；迁移后保留 `.bak`。

## 未完成 / 后续

- 结算界面、失败界面、菜单/选档/地图流程（新增流程需再次授权修改 `GameFramework.tscn`）。
- 技能、Buff、背包、装备、法宝、掉落、商店、任务：落位与数据形状见 `60-GameplayModules.md`，未写代码。
- 多存档槽 UI：`ArchiveSystem` 已支持多槽，`GameCatalogue` 暂无显示字段。
- 既有框架退出异常（WebRequest 关停、GC 终结器、ComponentInsoector）未处理。
- `EditorScripts/validate_level_scene.gd` 本轮未运行（关卡场景未改）。
