# 架构重构计划：会话领域核心 + 逐级成长表

> 状态：已与用户确认全部主要决策，待实施。
> 依据：本次会话诊断、`Docs/ProjectGuidelines/00-Architecture.md`（尤其第 40 行现状需修订）、`Docs/ProjectGuidelines/20-Configs.md`、`Docs/Reviews/experience_resource_bars_2026-10-07.md`、`Docs/Reviews/ui_binding_refactor_2026-10-07.md`、`Docs/gpt-1.md`。

## 1. 目标

把当前"实体即数据源"的形态，重构为 **会话领域核心 + 实体适配器**：

- 持久领域数据（累计经验、金币）与跨实体规则（成长、奖励、存档）收敛到纯 C# 的 `PlayerModel` + `System`，由会话级 `GameSession` 持有。
- `HeroEntity` 只保留单场战斗数据与行为（血/蓝/无双/输入/状态机/武器），成为订阅 Model 的适配器。
- 成长改为 **逐级查表**，代码不再持有派生公式。
- 存档收编到 `SaveSystem`，流程只做装配。
- 全项目可重构、清除无用代码，并同步项目规范。

## 2. 非目标（本轮不做，只留扩展点）

- 主菜单/选人界面、多存档槽 UI、多角色切换（`PlayerModel` 预留 `HeroId`）。
- 装备、法宝、技能、Buff、背包、商店、掉落物实体、金币来源玩法。
- 怪物表重构（`MonsterConfig` 本轮不动；公共属性表将来再评估）。
- 全局单例治理（`ConfigSystem` 静态配置加载、`DamagePopManager : SingletonNode` 保持现状，仅文档登记边界）。
- 关卡进度/解锁存档、失败关卡收益规则的完整设计。

## 3. 已确认决策

| # | 决策 | 结论 |
| --- | --- | --- |
| 1 | 架构范式 | A：会话领域核心（`GameSession` 持有 Model + Systems） |
| 2 | 数据真源 | 单一真源 + 全部派生；只保留必要 bind |
| 3 | Model/System 骨架 | `GameSession` 持有 `PlayerModel` + 成长/属性/奖励/存档系统 |
| 4 | 变更通知 | 只绑源：`TotalExperience`/`Gold`/`Hp`/`Mp`/`WsValue`/`TravelAvailable`；派生值普通属性，在变化点刷新（源最后写，订阅者读派生） |
| 5 | 存档策略 | 单槽 + 检查点保存（清关/离开流程）；加 `Version` + 迁移钩子 |
| 6 | 目录/命名 | 领域目录 + `System` 后缀 |
| 7 | 成长表 | 逐级查表；`HeroConfig` 瘦身 + 引用 `GrowthId`；新增 `HeroGrowthConfig`（嵌套 `*Levels`）；删 `HeroLevelConfig`；Monster 不动 |
| 8 | 执行方式 | 分 4 阶段，每阶段可构建/可烟测/可单独提交 |

## 4. 数据归属契约（本重构的核心）

| 数据 | 归属（唯一位置） | 通知方式 |
| --- | --- | --- |
| 累计经验 `TotalExperience` | `PlayerModel`（持久真源） | `BindableProperty<int>` |
| 金币 `Gold` | `PlayerModel`（持久真源） | `BindableProperty<int>` |
| 等级 `Level` / 本级经验 `Experience` / 升级需求 `MaxExperience` | `PlayerModel`（派生，仅 `ProgressionSystem` 写） | 普通属性，不绑 |
| 当前生命 `Hp` | `ActorEntity`（战斗数据） | `BindableProperty<int>` |
| 生命上限 `MaxHp` | `ActorEntity`（派生自等级+表） | 普通属性，不绑 |
| 当前魔法 `Mp` / 上限 `MaxMp` | `HeroEntity`（战斗数据） | `Mp` 绑，`MaxMp` 普通 |
| 无双 `WsValue` / `WsMax` | `HeroEntity`（战斗数据） | `WsValue` 绑 |
| 战斗属性（攻/防/暴击/闪避…） | `StatsSystem` 查表产出 | 不存、不绑 |
| 可前进窗口 `TravelAvailable` | `LevelController` | `BindableProperty<bool>` |

**写者所有权**：`ProgressionSystem` 是成长派生的唯一写者；`SaveSystem` 仅在读档时初始化写入 `PlayerModel`；`RewardSystem` 只调 `ProgressionSystem`；`HeroEntity` 只写自己的战斗数据与事实。UI 只读只订阅。

## 5. 关键时序契约

**升级（保证无中间态）**
1. `RewardSystem` → `ProgressionSystem.AddExperience(amount)`。
2. `ProgressionSystem` 用 `HeroGrowthTable.Evaluate(newTotal)` 得 `(Level, Experience, MaxExperience)`，**先写全部派生普通属性**。
3. `HeroEntity` 与 HUD 都在 `OnShow`/`OnOpen` 订阅 `PlayerModel.TotalExperience.Changed`；实体先订阅（流程中先显示英雄、后开 HUD），故实体回调先执行：比较自身缓存等级，若变化则 `StatsSystem` 重算 `MaxHp/MaxMp`，**先设上限、再补满当前值**。
4. 最后由 `ProgressionSystem` 写 `TotalExperience.Value = newTotal`（**单次通知**）。HUD 回调读取已更新的 `Level/Experience/MaxExperience` 刷新等级标签与经验条；实体回调完成上限/补满（`Hp.Value` 变化会同步通知 HUD 血条，此时新上限已就绪）。

**奖励链路**：`MonsterEntity.OnDied` 发 `MonsterDiedEventArgs`（不变）→ `LevelSpawner` 去重后（本关所有权）抛 `MonsterKilled`（值数据，不含经验）→ `LevelController` 转发同名事件 → `ProcedureGame` 接线到 `RewardSystem`。`RewardSystem` 按 `MonsterConfig` 查经验/金币（会话开始时校验表非负）→ 调 `ProgressionSystem`。`LevelSpawner` 删除经验索引，只保留所有权与清波计数。

**存档**：`ProcedureGame.OnEnter` 建 `SaveSystem` → `LoadAsync` → 迁移 → 建 `PlayerModel`/`ProgressionSystem` 初始化 → 建 `GameSession` → 关卡装配。检查点在 `LevelController.Completed` 与 `OnLeave` 调 `SaveSystem.FlushAsync`。不再"经验一变就存"，流程不再手工拼快照。

## 6. 目标目录与命名（GameScripts 相对 `TheGame/`）

```
GameScripts/
  Session/      GameSession.cs
  Player/       PlayerModel.cs, ProgressionSystem.cs, StatsSystem.cs, HeroGrowthTable.cs
  Reward/       RewardSystem.cs
  Archive/      GameData.cs, GameCatalogue.cs, PlayerSaveData.cs, SaveSystem.cs
  Entity/       （不变；HeroEntity 改为适配器）
  Level/        （清理：Spawner 去经验、Controller 去掉经验转发）
  UI/           （重绑：只绑源；ResourceBar 改单值+外部上限）
  Event/        （可选新增 PlayerLevelUpEventArgs，供世界特效）
  Battle/ Config/ GameProto/ Bindable/ （基本不变）
```

- 命名：领域规则类用 `XxxSystem`；数据类用 `XxxModel`；含 IO 的持久化用 `SaveSystem`（放 `Archive/`）。
- 删除空目录 `GameScripts/Attributes/`；`Player/` 启用。

## 7. 配置表变更（源表 + 生成）

- `HeroConfig.xlsx`：删除全部战斗/成长列（`BaseHp/BaseMp/BasePower/BaseDef/BaseMdef`、`Grow*`、`Crit/Miss/Lucky/Toughness/Htarget/CritReduce/Ar/Sp`、`Vampirism/RHp/RMp`）；新增引用列 `GrowthId`（可读标识）；保留身份/表现/手感列与音效引用。
- 新增 `HeroGrowthConfig.xlsx`：map 表，主键 `GrowthId`；bean 含 `GrowthId` + 嵌套 `*Levels` 列表，每级字段：`Level`、`MaxExp`、`MaxHp`、`MaxMp`、`Power`、`Def`、`Mdef`、`Crit`、`Miss`、`Lucky`、`Toughness`、`Htarget`、`CritReduce`、`Ar`、`Sp`、`Vampirism`、`RHp`、`RMp`。建模方式与 `LevelConfig` 的 `*Stages/*Recipes` 嵌套一致（标题行合并需覆盖嵌套宽度）。
- 删除 `HeroLevelConfig.xlsx` 及其生成类/`.bytes`（`MaxExp` 并入 `HeroGrowthConfig.*Levels`）。
- 更新 `__beans__.xlsx`（新增 bean、删除旧 bean）、`__tables__.xlsx`。
- 生成：`Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`；生成物 `TheGame/GameScripts/GameProto/GameConfig/Hero/*`、`TheGame/DataTables/GameConfigs/*` 只由流程更新。
- **约束**：源表为 `.xlsx`。若执行代理无法可靠编辑 xlsx，需：由用户改表，或用 Python `openpyxl` 脚本改（实施前先确认 `openpyxl` 可用）。不得手改生成 C# 或 `.bytes`。

## 8. 分阶段任务清单

### 阶段 1：配置表 + 领域层骨架（不改现有行为）
1. 按 §7 改源表并导表；删除 `HeroLevelConfig` 生成物与 UID 元数据；更新 `HeroConfig`/`__beans__`/`__tables__`。
2. 新增 `Player/PlayerModel.cs`（`TotalExperience`、`Gold` 源属性；`HeroId`、`Level/Experience/MaxExperience` 派生普通属性；无规则）。
3. 新增 `Player/HeroGrowthTable.cs`：由 `HeroGrowthConfig` 构建，提供 `MaxLevel/MaxTotalExperience/Evaluate/Restore/Stats(level)`（承接现 `Progression/ExperienceCurve.cs` 的职责并扩展到属性查表）。
4. 新增 `Player/StatsSystem.cs`：`Get(GrowthId, level) -> HeroStats`（纯查表，`Battle/` 风格无状态）。
5. 新增 `Player/ProgressionSystem.cs`：持有 `PlayerModel` + `HeroGrowthTable`；`InitializeFromSave(PlayerSaveData)`、`AddExperience(int)`、派生写入、`TotalExperience` 最后写。
6. 新增 `Reward/RewardSystem.cs`：`HandleKill(...)` 按 `MonsterConfig` 发经验；表校验非负。
7. 新增 `Archive/SaveSystem.cs`：`LoadAsync`（含迁移）、`Initialize(PlayerModel)`、`MarkDirty`、`FlushAsync`（串行队列、快照）。
8. 新增 `Session/GameSession.cs`：聚合 `PlayerModel` 与各 System；`static CreateAsync(SaveSystem)` 或等效装配。
9. 删除 `Progression/ExperienceCurve.cs`（职责并入 `HeroGrowthTable`）。
10. 更新/新增纯 C# 测试（经验、查表、存档快照/迁移、奖励规则）；`dotnet build` + `dotnet test Tests/BattleTests Tests/LevelTests` 通过。

### 阶段 2：实体适配器 + UI 重绑（只绑源）
1. `HeroEntity`：
   - 删除 `Level/TotalExperience/Experience/MaxExperience/Gold` 与 `ExperienceCurve`、`GainExperience`、`ApplyExperience`、存档复制逻辑。
   - 保留 `Mp/MaxMp`、`WsValue/WsMax`、`Hp/MaxHp`、输入、FSM、武器/特效。
   - `OnShow(userData)` 接收 `PlayerModel`（null = 测试场地默认 level 1 模型）；用 `StatsSystem` 设上限并补满、清零无双；订阅 `TotalExperience.Changed`；`OnHide` 退订。
   - `GetCombatStats()` 改由 `StatsSystem` 产出。
2. `ActorEntity`：`MaxHp` 保持普通属性（可保留，供内部使用）；确认无 UI 绑定依赖。
3. `UI/BattleHudContext.cs` → 携带 `(HeroEntity hero, PlayerModel player, LevelController level)`。
4. `UI/ResourceBar.cs`：改为观察**单个当前值属性** + 外部设置 `Max`（HUD 推入）；保留文本/残影/段位可选表现；上限/初次对齐语义保持。
5. `UI/BattleHud.Logic.cs`：
   - 订阅 `player.TotalExperience`（刷新等级+经验条）、`player.Gold`（如显示）、`hero.Hp`/`hero.Mp`（刷新条，推上限）、`hero.WsValue`、`level.TravelAvailable`。
   - `OnClose` 对称退订；打开先设上限再读初值。
6. `Level/Spawning/LevelSpawner.cs`：删除经验索引；`MonsterDied` 去重后抛 `MonsterKilled`，保留清波计数。
7. `Level/LevelController.cs`：删除 `OnExperienceDropped`；`StartSession` 保留玩家注入（触发器用）；转发 `MonsterKilled`。
8. `ProcedureGame.cs`：接线 `MonsterKilled → RewardSystem`、`Completed → SaveSystem.Flush`；英雄 `ShowEntityAsync(..., session.Player)`；HUD 上下文更新。
9. 更新 `MainPack/Scripts/Debug/*` 与 `--smoketest=ui`；跑 `=ui`、`=level`、`=level-cancel`、`=ai`、无参烟测。

### 阶段 3：存档/流程收编
1. `ProcedureGame` 删除手工快照与 `SavePlayerAsync`，改由 `SaveSystem`；`OnEnter` 走 `LoadAsync + GameSession` 装配；`OnLeave` flush。
2. `GameData`：新增 `int SaveVersion = 1;`，删除无用 `Score`；迁移钩子在 `SaveSystem.LoadAsync` 执行（旧档 `Player.Level` 作为下限，经 `HeroGrowthTable.Restore` 归一）。
3. 校验框架 `LoadAsync` 失败（损坏/密钥变更）时中止会话并报告，不覆盖旧档。
4. 重跑全部烟测；确认经验总量仍与源表一致（现 15），确认无重复奖励。

### 阶段 4：清理死代码 + 规范文档 + 全量验证
1. 死代码清扫：空目录 `Attributes/`；`GameData.Score`；`HeroEntity` 重复 `<returns>`（`HeroEntity.cs:404-405`）等无用注释/using/字段。
2. 同步规范文档（见 §10）。
3. 全量验证（见 §9），写 `Docs/Reviews/architecture_refactor_2026-XX-XX.md` 记录改动、依据、命令与结果、未完成项。
4. 分阶段提交（`refactor:` / `config:` / `docs:` 前缀；遵循 `Docs/ProjectGuidelines/50-FrameworkAndTools.md` Git 约定）。

## 9. 验证计划

在 `Godot/GodotProject/` 与仓库根按序执行；结果须如实记录，未运行的不得声称通过。

| 项 | 命令 |
| --- | --- |
| 导表 | `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`（改表后必先导表） |
| 编译 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore --verbosity quiet` |
| 单测 | `dotnet test Tests/BattleTests --no-restore`；`dotnet test Tests/LevelTests --no-restore`；新增成长/存档测试 |
| 编辑器构建 | `"S:/Godot4/Godot4CSharp_console.exe" --headless --editor --build-solutions --path Godot/GodotProject --quit --no-window -q --disable-crash-handler` |
| 引用/UID | `python Tools/ProjectMaintenance/validate_game_references.py` |
| 烟测 | `... --max-fps 120 --quit-after <N> -- --smoketest`（无参/`=ai`/`=level`/`=level-cancel`/`=ui`）；断言 `SMOKE PASS`；已知框架退出异常单独登记 |
| 差异检查 | `git diff --check` |

回归要点：升级跨级余量、满级封顶、升级补满血蓝、HUD 只绑源且无中间态、关闭退订、换角色/池复用、自动检查点保存的磁盘读回、旧 JSON 兼容。

## 10. 规范文档更新清单

- `Docs/ProjectGuidelines/00-Architecture.md`：**修订第 40 行**（删除"不增加 Model/Session"旧结论），新增"领域层：`GameSession`/`PlayerModel`/`System`、写者所有权、实体为适配器"；补充 `Session/Player/Reward` 目录职责；保留"UI 数据流"但改为**只绑源**并写明派生刷新时序。
- `60-GameplayModules.md`：更新"英雄数值与存档""经验成长""待设计"；记录奖励链路新归属与检查点存档。
- `20-Configs.md`：新 `HeroConfig` + `HeroGrowthConfig` schema、删除 `HeroLevelConfig`、嵌套列表填写规则、`GrowthId` 引用。
- `30-EntitiesAndCombat.md`：实体不再持有等级/经验/金币；升级重算上限与补满的时序；`GetCombatStats` 经 `StatsSystem`。
- `40-ScenesAndAssets.md`：HUD 绑定改为单值+外部上限；`BattleHudContext` 字段更新。
- 目录级 `AGENTS.md`：仅新增/更新导航（`Session/`、`Reward/`、`Player/`）。
- 新增 `Docs/Reviews/architecture_refactor_2026-XX-XX.md` 记录本轮。

## 11. 风险与缓解

- **xlsx 源表编辑**：代理可能无法可靠改二进制 xlsx → 先确认 `openpyxl`，否则由用户改表后再导表；生成物只由 Luban 流程产出。
- **升级时序依赖订阅顺序**：依赖"先显示英雄、后开 HUD"的既有流程顺序。缓解：文档明确该契约；如担心，可在 `GameSession` 层提供升级后统一刷新钩子（进阶项，非本轮必须）。
- **行为回归**：经验/清波/奖励链路改动面大 → 阶段 1/2 保持旧行为可运行，逐阶段烟测；保留 `=level` 经验总量断言。
- **属性表数值迁移**：现 `HeroConfig` 的 Base/Grow 需换算为逐级行（Excel 公式生成）→ 迁移后须用现有数值核对（旧项目 `BaseRoleProperies.gd` 1–19 级 + 20 级起线性），确保与现运行时一致。
- **公开可写属性**：`BindableProperty.Value` 公开可写，靠"写者所有权"纪律约束；HUD 只读。若实施中发现误写风险，再评估只读视图（现规范已删除只读接口，本轮不引入）。

## 12. 开放项（实施前可确认）

- `PlayerModel.HeroId` 的存档字段是否本轮加入（默认悟空、暂不存档）。
- `PlayerLevelUpEventArgs` 世界特效事件本轮是否需要（默认仅预留）。
- 检查点是否额外包含"升级即时保存"（默认不含）。
