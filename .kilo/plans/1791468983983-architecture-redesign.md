# 架构重设计：GGF 原生分层 + 作用域所有权（草案）

> 状态：**D1–D10 已确认；§7 已按用户审阅意见修订（死亡也写盘；存档读写统一走 GF.Archive，授权修改 ArchiveSystem），用户已审阅通过**。
> 取代：`.kilo/plans/1791383001385-architecture-domain-refactor.md`（Model/System 方案，未实施；用户已要求摒弃 QFramework 式融合）。

## 1. 目标

- 建立清晰、可扩展、可测试的顶层架构与项目规范，覆盖旧项目全部玩法（技能、Buff、背包、装备、法宝、商店、锻造、任务、掉落、世界地图、多存档、多英雄）。
- 以 GGF 为唯一框架；借鉴 StarForce（流程/实体数据-逻辑分离）、Celeste/SuperTux（SaveData/Session/Entity 分层）、GAS-lite（属性修正与效果）、OpenBOR/MUGEN（攻击包）。
- 本轮：落地基础骨架并迁移现有功能；长期系统只定落位、数据形状与规则，不写投机代码。

## 2. 已核实约束（实施必须遵守）

- `Framework/GodotGameFrameworkCore/Base/GF.cs` 写死 `ArchiveSystem<GameCatalogue, GameData>`（全局命名空间）→ 两个类名与命名空间不能改（除非授权改框架）。
- 框架 `UIExtension.cs`/`EntityExtension.cs` 依赖全局 `ConfigSystem` 与 `TbUIFormConfig`/`TbEntityConfig` → `ConfigSystem` 保持全局，由 `Configs/GameConfig/CustomTemplate/ConfigSystem.cs` 生成。
- 流程类型清单在 `Framework/GameFramework.tscn` 的 `Procedures` 数组（Launch/Update/Prelode/Game）→ 新增/改名流程需要改框架场景（需用户明确授权）。
- GGF Archive（现状，P4a 加固）：`SaveAsync(unitId)`/`OverWriteAsync` 不重写 `Catalogue.sav`（槽位显示信息无法随覆盖更新）；无版本/迁移、无原子写/备份；错误只记日志不抛出；`LoadAsync()` 选最后一个槽；首次启动自动建槽；Newtonsoft 默认设置，未知字段忽略。D10 授权修改 `ArchiveSystem`/`EasySave` 补齐原子写、`.bak`、串行化、返回值；版本迁移仍归项目。
- `GF.Event.Fire` 入队、下次轮询分发；`FireNow` 立即；参数分发后自动回池。
- 实体 Attach 后隐藏时子节点不回组容器（框架缺陷）→ 法宝/宠物优先做独立实体并记录拥有者 ID。
- 同步 `OpenUIForm(UIFormId)` 忽略 `PauseCoveredUIForm`；`GetTopUIForm` 返回错误窗口；`SceneTree.Paused` 会停 GGF 轮询 → 暂停用 `GF.Base.PauseGame()`（TimeScale）。
- `ConfigSystem` 按表懒加载，首次使用时读盘与校验分散（`LevelSpawner`、`MonsterEntity` 各自查表）。
- `UiSmokeScenario` 反射读取 `ProcedureGame` 私有字段（`m_SaveTask`、`m_HudSerialId`）。
- 空目录：`GameScripts/Gameplay/`、`Player/`、`Reward/`、`Rewards/`。

## 3. 现状诊断（为什么乱）

1. `HeroEntity` 混合输入/物理/FSM、持久进度（等级/经验/金币）、资源池、属性公式、奖励结算、读档复制，共 9 个可写 Bindable；背包/商店/地图界面没有英雄实体可依附，持久数据却困在池化节点里。
2. 成长：经验查表，HP/攻防写成代码公式 `Base+(L-1)*Grow`；怪物 `Power=0`。
3. 奖励：`LevelSpawner` 持有经验索引，`LevelController` 转发给英雄；金币无来源，掉落无归属。
4. 存档：`ProcedureGame` 手拼快照，每次经验变化写盘；`GameData.Score` 无用；无版本。
5. UI：`BindableProperty.Value` 公开可写、逐字段订阅，控件自行订阅；等级/经验/上限存在中间态风险。
6. `ProcedureGame` 是上帝流程（读档+关卡+英雄+HUD+存档）。
7. 规范混合"规则"与"现状叙述"；命名空间不一致（`GameLogic` / `GameLogic.UI` / 全局）。

## 4. 设计提案（待确认项见 §5）

### 4.1 原则

1. 单一所有者：可变状态只有一个所有者，经方法修改；他人只读并订阅变更。
2. 显式作用域：App → Profile（存档槽）→ Run（一次关卡）→ Entity；上层创建并销毁下层。除 `GF.*` 与只读配置外无全局可变状态。
3. 配置 / 状态 / 规则 / 表现分离。
4. 显式依赖：构造参数、`Initialize`、`userData` 注入；不查场景树、不用服务定位。
5. 表驱动：可调数值与曲线进 Luban；代码只保留"公式本身即设计"的部分（伤害公式），常数在表。
6. 事件分级：状态变化→所有者的 C# 聚合事件；跨模块一次性业务结果→`GF.Event`；作用域内协作→直接调用。
7. 存档即快照：DTO 只含 ID 与可变状态，映射集中一处，版本化迁移，检查点写入。

### 4.2 分层（依赖自上而下）

```
表现层   UI 窗口/控件 | 实体(英雄/怪物/子弹/掉落) | 关卡场景 | 特效
应用层   流程(MainPack/Procedure) + GameContext + LevelRun（作用域所有者、装配、检查点）
领域层   档案状态与规则(PlayerProfile…) + 战斗运行时(StatSheet/Vitals/BuffSet…，纯 C#，由实体持有)
核心规则 DamageCalculator / 属性汇总 / 经验曲线 / 掉落掷骰（确定性，可单测）
配置     Luban 只读表 + 启动期校验后的索引
框架     GGF（GF.*）
```

领域层与核心规则不引用节点、场景树、`GF.UI`/`GF.Entity`。

### 4.3 作用域与所有者（命名约定见 4.10）

| 作用域  | 所有者                                  | 创建者        | 生命周期     | 内容                                  |
| ------- | --------------------------------------- | ------------- | ------------ | ------------------------------------- |
| App     | GGF + 流程                              | 引擎          | 进程         | 只读配置、`GF.Setting` 设置         |
| Profile | `PlayerProfile`（经 `GameContext`） | 选档/新档流程 | 读档→回标题 | 英雄记录、钱包、背包、关卡进度等      |
| Run     | `LevelRun`                            | 关卡流程      | 进关→结算   | 关卡控制器、英雄实体、局内统计、结算  |
| Entity  | `ActorEntity` + 战斗运行时            | `GF.Entity` | Show→Hide   | 生命/魔法/无双、StatSheet、Buff、冷却 |

### 4.4 领域模型（Profile）

```
PlayerProfile
  HeroRecord: HeroId, Progression(Level/TotalExp), SkillBook, Equipment, 被动/丹药/阵法…
  Wallet, Inventory, WorldProgress, QuestLog/Flags（后续）
规则：HeroStatBuilder(成长+装备+被动+丹药→持久修正)、ExperienceCurve、装备/商店/锻造规则
```

### 4.5 战斗运行时（每个角色，纯 C#）

`StatSheet`（基础+按来源修正）、`Vitals`（HP/MP 及上限钳制，英雄另含无双）、`BuffSet`、`SkillSet`/冷却；节点只保留空间与表现（HitBox/HurtBox/动画/FSM）。英雄显示时由档案构建持久修正，局内 Buff 叠加临时修正。

### 4.6 UI 数据流

窗口 `OnOpen` 订阅聚合变更事件并拉取一致快照，`OnClose` 对称退订；控件被动（只接收数值）；冷却/Buff 剩余时间在 `OnUpdate` 读取；一次性反馈（飘字、升级特效、获得物品）走 `GF.Event`。

### 4.7 事件分级

`GF.Event`：DamageDealt、MonsterDied、HeroLeveledUp、ItemObtained、LevelResult 等，有消费者才新增。作用域内（LevelController↔Spawner）用直接调用/C# 事件。

### 4.8 存档

`GameData`（全局命名空间）：`SaveVersion` + `ProfileSaveData`；`GameCatalogue`：槽位显示信息（P4a 后随每次覆盖同步写入）；`SaveService`：建档、读档迁移、捕获快照、检查点提交，**只经 `GF.Archive` 读写**；原子写、`.bak`、串行化由框架 `ArchiveSystem` 负责（D10）。

### 4.9 成长与属性表

共享经验曲线表 + 每英雄每级属性表（`StatBlock` bean）；怪物同用 `StatBlock`；代码只查表。

### 4.10 命名约定（替代 Model/System）

- 配置：`XxxConfig`（生成）；存档 DTO：`XxxSaveData`；运行时状态：领域名词（`PlayerProfile`、`Inventory`、`Wallet`、`StatSheet`、`Vitals`）；纯规则：职责名（`DamageCalculator`、`ExperienceCurve`、`LootRoller`、`HeroStatBuilder`）；作用域所有者：`GameContext`、`LevelRun`；节点：`XxxEntity`、`XxxController`、窗口/控件。
- 禁用含义模糊的 `Manager`、`System`、`Model` 后缀（GGF 自身类型除外）。

### 4.11 流程（长期）

Launch → Update → Preload(配置加载+校验) → Menu(标题/选档/选英雄) → World(地图枢纽：背包/商店/锻造/技能/选关) → Level(战斗+结算) → World；调试直达关卡使用临时档案。

### 4.12 长期系统落位

- 技能：`SkillConfig`；档案 `SkillBook`（学习等级、5 个键位）；运行时 `SkillSet`（冷却、耗蓝、状态门控）驱动身体 FSM 技能状态，子弹为 `BulletEntity`。
- Buff：`BuffConfig`（类型、时长、跳频、叠加规则、修正、标签、跳伤）；运行时 `BuffSet` 写 StatSheet 修正并提供控制标签；来源为攻击包、技能、装备、阵法。
- 背包/装备：`ItemConfig`；`Inventory`（可叠加物 itemId+count，唯一实例 instanceId+强化+宝石+五行）；`Equipment` 槽→实例；外观由配置字段给出资源路径。
- 法宝：独立实体（记录拥有者 ID），独立实战槽，耗蓝冷却。
- 商店/锻造/炼丹：对 Inventory+Wallet 的原子事务，成功后检查点保存。
- 掉落：`DropConfig`；`LootRoller` 纯函数；`LevelRun` 生成拾取实体并入账。
- 任务：`QuestConfig` 目标类型枚举；`QuestLog` 订阅领域事件推进，不轮询。
- 关卡进度：`WorldProgress`（解锁、最佳评价）；解锁条件进 `LevelConfig`。

## 5. 决策清单（逐项确认）

| #   | 问题                       | 推荐                                                                                                                                                                                                                                                                                                                                                                                                         | 状态             |
| --- | -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------- |
| D1  | 顶层范式                   | GGF 原生分层 + 作用域所有权 + 显式注入                                                                                                                                                                                                                                                                                                                                                                       | **已确认** |
| D2  | 玩家数据所有者与跨流程传递 | `PlayerProfile` 由 `GameContext` 持有，存于流程 FSM 数据（自定义 `Variable<GameContext>` 包装；包装对象会被池回收，`GameContext` 本身不回收）                                                                                                                                                                                                                                                        | **已确认** |
| D3  | UI/HUD 刷新机制            | 聚合变更事件 + 拉取；控件被动；计时轮询；一次性反馈 GF.Event；删除`BindableProperty`                                                                                                                                                                                                                                                                                                                       | **已确认** |
| D4  | 战斗属性汇总               | StatSheet：Luban`StatType` 枚举 + 按来源修正（Flat/PercentAdd/PercentMult）、来源整体移除、脏标记重算；人怪共用；出招快照进攻击包；本轮只接入成长来源                                                                                                                                                                                                                                                      | **已确认** |
| D5  | 成长数据                   | 共享经验曲线表 + 每英雄逐级属性表（`StatBlock` bean，HeroId+Level 查询）；`HeroConfig` 只留身份/手感/表现；怪物无成长，`MonsterConfig` 内嵌单个 `StatBlock`，`Level` 仍为标量（等级压制、经验门槛）；为保持现有伤害，怪物攻击先填 0、继续用招式 `FlatPower`                                                                                                                                      | **已确认** |
| D6  | 存档内容与时机             | 只存 ID+数量+可变状态；`SaveVersion` 逐级迁移（首个迁移：现有 v0 `Player{Level,TotalExperience,Gold}` → v1）；`SaveService` 统一捕获快照并串行写入；只在检查点写盘（关卡结算、返回地图/标题、交易/锻造完成、手动保存）；覆盖前复制 `.bak`                                                                                                                                                           | **已确认** |
| D6b | 关内收益提交 | **关卡事务**：进关时在内存中捕获档案快照；关内照常直接修改档案（即时升级、可装备新掉落、可用药品）。**通关和英雄死亡都写盘提交**（用户审阅修订：死亡保留本关收益）；只有中途放弃（以后的暂停菜单"返回地图"）、运行错误、框架关停才用快照回滚（关停时不写盘即等价回滚）。提交后 `LevelRun` 进入 Ended，不再接收奖励；结算界面另用 `RunStats` 只作展示 | **已确认** |
| D10 | 存档读写入口 | **读和写都只走 `GF.Archive`**（用户授权修改框架 `ArchiveSystem`/`EasySave`）：原子写（临时文件 + 替换）+ `.bak`、主档损坏时回退 `.bak`、写入串行化、覆盖时同步写目录、保存/读取返回 `bool`；版本迁移仍在项目 `SaveService` | **已确认** |
| D7  | 流程与框架场景授权         | **用户授权**修改 `Framework/GameFramework.tscn` 的 `Procedures`/`EnterProcedure`（仅此两项，按集中规范登记例外）。本轮：`ProcedurePrelode`→`ProcedurePreload`（改错字，并负责配置预加载与集中校验）、新增 `ProcedureLoadProfile`（读档/迁移/建 `GameContext` 写入流程数据）、`ProcedureGame`→`ProcedureLevel`（只负责一次关卡运行）；Menu/World 等到有界面时再加，每加一个单独授权 | **已确认** |
| D8  | 目录组织                   | 领域优先，目录 = 命名空间；纯 C# 规则与节点分开；UI 按界面分；删除 Bindable/Progression/Reward/Rewards/Gameplay/Player 空目录或临时目录                                                                                                                                                                                                                                                                      | **已确认** |
| D9  | 本轮范围与阶段             | 骨架 + 迁移现有功能（经验/升级/金币/生命魔法无双/HUD/存档/关卡）+ 成长表 + StatSheet + 存档版本迁移 + 关卡事务与结算（通关保存后重开）+ 规范重写；技能/Buff/背包/装备只在规范落位，不写代码                                                                                                                                                                                                                  | **已确认** |

## 6. 旧项目功能清单（设计覆盖面）

- 英雄 5 名，存档绑定 1 名（`Myself`）；属性 16 项（HP/MP/攻/防/魔防/暴击/闪避/命中/暴抗/暴伤/韧性/破甲/破魔/吸血/回血/回蓝）；最终值 = 成长 + 装备 + 被动 + 丹药 + 阵法（全加法），Buff 只在结算时作为倍率/标志。
- 等级上限 55，升级补满；经验 1–19 查表、之后公式；溢出经验丢失（新项目已采用累计经验保留余量）。
- 装备 7 槽（武器/防具/饰品/时装/头衔/翅膀/法宝），10 级品质，强化（品质限上限）、宝石镶嵌、幻化、外观按名称拼路径。
- 法宝：独立实战槽，H 键，耗蓝、冷却，生成子弹，0–10 级，五行。
- 技能：每英雄 9 主动 + 1 专属被动，5 个键位，灵魂学习，耗蓝、冷却；6 个共享被动（纯属性加成）。
- Buff 26 种：控制/持续伤害/属性/保护，叠加规则多样，图标显示层数与剩余时间。
- 背包 3 类 × 10 页 × 35 格；商店（灵魂）、神秘商店；炼丹炉（强化/合成/镶嵌/分解/打造）；丹药 6 类 × 5 级。
- 任务（成就式目标与奖励领取）；掉落（金币球、物品、药品，自动拾取）。
- 关卡 55 个、5 张地图、线性解锁 + 境界门槛、四阶段、Boss、出口、评价（时间 + 剩余血量）、失败界面（回地图/重试）、塔类关卡。
- 存档多槽（≥6），每次操作都写盘；只有版本号无迁移。
- 无双人、无宠物；称号存在。

## 7. 实施阶段（每阶段结束：构建 + 相关单测/烟测通过，再进下一阶段）

### P0 目录与命名空间归一（纯机械，无行为变化）

目标目录（`GameScripts/` 下，命名空间 = `GameLogic.` + 目录路径）：

```
Config/    启动校验 ConfigValidator、ExternalTypeUtil
Battle/    DamageCalculator、AttackData、CombatantStats、DamageResult
  Stats/   StatSheet、StatModifier、StatSource、Vitals、MusouGauge（P2 新增）
Profile/   PlayerProfile、HeroRecord、HeroProgression、Wallet、ExperienceCurve、HeroStatBuilder（P2）
Save/      SaveService、ProfileSaveData、HeroSaveData、PlayerSaveDataV0、SaveMigrator、ProfileMapper（P2/P4）
           + 全局命名空间 GameData / GameCatalogue（框架约束）
Session/   GameContext、GameContextVariable、LevelRun、HeroLoadout（P4）
Entity/    现状保留
Level/     现状保留；子目录命名空间补齐（Stage/Camera/Spawning/Markers）
UI/        BattleHud/（窗口两件套+数据）、Widgets/、Damage/
Event/     GF 事件参数
```

- 移动：`Progression/ExperienceCurve.cs` → `Profile/`；`Archive/*` → `Save/`；`UI/BattleHud*.cs` → `UI/BattleHud/`；删除 `Bindable/`（P3 删类型）与空目录 `Gameplay/ Player/ Reward/ Rewards/ Progression/ Archive/`。
- `BattleHud` 命名空间改 `GameLogic.UI`，用 `Tools/ProjectMaintenance/regenerate_ui_form.py --namespace GameLogic.UI` 重生成 Ge 部分。
- 流程类保持全局命名空间（`ProcedureComponent` 用 `Type.GetType("ProcedureXxx")` 按短名加载）。
- 移动后修 `.uid`/场景引用；跑 `validate_game_references.py`。

### P1 配置（Luban）

- 新一次性脚本 `Tools/ConfigBootstrap/build_growth_tables.py`（按 `build_m2_tables.py` 写法就地改 xlsx，不重跑旧种子脚本）：
  - `__enums__`：`Stat.StatType` = MaxHp, MaxMp, Power, Def, Mdef, Crit, Miss, Lucky, Toughness, Htarget, CritReduce, Ar, Sp, Vampirism, HpRegen, MpRegen。
  - `__beans__`：`Stat.StatBlock`（上述 16 项列展开）。
  - 新表 `Hero.TbHeroGrowthConfig`（list + 联合索引 `HeroId, Level`，生成 `Get(heroId, level)`）；数值由现公式 `Base+(L-1)*Grow` 生成 1..55 级，保证现有手感不变。
  - `HeroConfig` 删除 Base*/Grow*/Crit…RMp 属性列；`MonsterConfig` 属性列改为 `Stats: StatBlock`（Power=0），保留 `Level`、`AddExp`、`Rank`。
  - 新单行表 `Profile.TbProfileConfig`：`StartHeroId`、`StartGold`（建档规则）。
  - `BattleConfig` 增 `DeathRestartDelay`（秒）。
- 导表：`Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`；不手改生成代码。
- `build_m2_tables.py` 已不能安全重复运行（它会整表重写 `__tables__.xlsx`，而注册列表里没有 `TbLevelConfig`）；在文件头标注"历史种子脚本，勿再运行"，新脚本同样只运行一次。
- `Config/ConfigValidator.ValidateAll(Tables)`：经验曲线连续、每英雄成长表覆盖 1..MaxLevel、怪物 MaxHp>0、关卡引用有效、`StartHeroId` 存在；由 `ProcedurePreload` 调用，失败即致命停止（同资源组失败处理）。

### P2 纯领域层 + 单测（不引用节点 / `GF.*`）

- `StatSheet`：`SetBase(StatBlock)`；`AddModifier(StatSource, StatModifier)` / `RemoveSource(StatSource)`；`Get(StatType)`（脏标记缓存）；`GetInt` 四舍五入（AwayFromZero）；`Changed` 事件。最终值 = (Base+ΣFlat)×(1+ΣPercentAdd)×Π(1+PercentMult)。
- `Vitals`：Hp/MaxHp/Mp/MaxMp；`SetMaximums(maxHp, maxMp, refill)`、`Damage(int)→bool died`、`Heal`、`SpendMp`、`RestoreMp`；一次变更一次 `Changed`。
- `MusouGauge`：Value/Max，`Add/Reset`，`Changed`。
- `HeroProgression`：TotalExperience 为唯一事实，Level/Experience/MaxExperience 经 `ExperienceCurve` 派生；`AddExperience(int)→int levelsGained`；`Changed`。
- `Wallet`：Gold，`Add`/`TrySpend`，`Changed`。`HeroRecord`：HeroId + Progression。`PlayerProfile`：ActiveHeroId、Heroes、Wallet。
- `HeroStatBuilder`：`HeroLoadout Build(HeroRecord, Tables)` → 等级、成长 StatBlock、持久修正（本轮为空列表）。
- Save DTO：`ProfileSaveData{ActiveHeroId, Heroes[], Gold}`、`HeroSaveData{HeroId, TotalExperience}`、`PlayerSaveDataV0{Level, TotalExperience, Gold}`；`ProfileMapper`（Profile↔DTO，唯一映射点）；`SaveMigrator`（v0→v1：用 `ExperienceCurve.Restore` 合并旧等级）。
- 测试：新 `Tests/ProfileTests`（Profile + Save 映射/迁移）；`Tests/BattleTests` 增 StatSheet / Vitals / MusouGauge；删 `BindablePropertyTests`，`ExperienceCurveTests` 迁到 ProfileTests。

### P3 实体与 HUD 迁移

- `ActorEntity` 持有 `StatSheet Stats`、`Vitals Vitals`；`ReceiveHit` 用 Vitals 扣血/判死；`GetCombatStats` 改为 `CombatantStats.From(Stats, Side, Level)`；新增 C# 事件 `Died`（在 `Dead` 唯一置位点触发一次），供 `LevelRun` 订阅英雄死亡。
- `HeroEntity`：`OnShow(userData)` 只接受 `HeroLoadout`（非空，否则抛异常）；`ApplyLoadout(HeroLoadout, refill)` 供升级调用；持有 `MusouGauge`；删除等级/经验/金币/存档字段、`GainExperience`、`ApplyExperience`、内联成长公式。
- `MonsterEntity`：`Stats.SetBase(Config.Stats)`；Vitals 由 MaxHp 初始化。
- `LevelSpawner`：去掉经验字典，仍按自有实体去重，改发 `MonsterDefeated(monsterId, killerEntityId, position)`；`LevelController` 转发同名事件；`TravelAvailable` 改为只读属性 + `TravelAvailableChanged`。
- HUD：`BattleHudData{Hero, Progression, Level}`；`OnOpen` 订阅 `Vitals/Musou/Progression.Changed` 与 `TravelAvailableChanged` 后拉取刷新；`OnClose` 对称退订；`ResourceBar` 改为被动 `SetValue(current, max)`（残影由控件内部比较旧值决定）。
- 删除 `BindableProperty`。`DamagePopManager`（SingletonNode）→ 普通节点 `DamagePopPresenter`，由 `LevelRun`/测试场地创建并拥有。

### P4 应用层：流程、作用域、存档

- `Framework/GameFramework.tscn`（已授权，仅两行）：`EnterProcedure` 不变；`Procedures` = Launch, Update, Preload, LoadProfile, Level。
- `ProcedurePreload`（原 Prelode）：资源组、本地化、节点池 + `ConfigValidator`；→ LoadProfile。
- `ProcedureLoadProfile`：`SaveService.LoadOrCreateAsync()`（读档→版本迁移→映射；新档按 `TbProfileConfig` 建）→ `GameContext` 存入流程数据 `"GameContext"`（`GameContextVariable`）→ Level。
- `ProcedureLevel`（原 Game）：加载关卡场景 → 按 `HeroStatBuilder` 显示英雄 → 创建 `LevelRun` → 打开 HUD；离开时按逆序清理。`HotUpdateSafetyGuard.MarkStartupSuccess()` 移到首次成功开局。
- `LevelRun`（作用域所有者，状态 Running → Ended，结束后忽略后续奖励）：进关捕获 `ProfileSaveData` 快照；订阅 `MonsterDefeated` → `MonsterConfig.AddExp` → `Progression.AddExperience` → 升级则 `hero.ApplyLoadout(…, refill:true)`。
  - 通关（`Completed`）→ `SaveService.CheckpointAsync`（RunStats 记 Cleared）→ 重入 `ProcedureLevel`（GGF `ChangeState` 同状态 = OnLeave+OnEnter，已核对 `Fsm.cs:574`）。
  - 英雄死亡（订阅 `HeroEntity.Died`）→ **同样写盘提交本关收益**（RunStats 记 Defeated）→ `DeathRestartDelay` 后重入。通关与死亡先到者生效，`LevelRun` 只提交一次。
  - 运行错误 / 中途放弃（以后的"返回地图"）→ 用快照回滚档案，不写盘；框架关停不写盘（= 回滚）。
- `SaveService`（项目侧唯一存档入口；**只调用 `GF.Archive`，不直接用 `EasySave`/文件 API**）：`LoadOrCreateAsync`（`GF.Archive.LoadAsync()` → 版本迁移 → 映射；读失败返回错误，不建新档覆盖）；`CheckpointAsync`（把快照写进 `GF.Archive.CurrentData`：`SaveVersion=1`、`Profile`、`Player=null`，再 `await GF.Archive.OverWriteAsync()` 并检查返回值）；写入串行排队；`Pending` 供流程重入等待。
- `GameData`：`SaveVersion`、`Profile`、`Player`（只读旧版）；删 `Score`。**字段一律不设初始值**：Newtonsoft 先调用默认构造再填充 JSON，若 `SaveVersion` 带初始值 1，缺该字段的 v0 旧档会被误判为 v1。规则：`SaveVersion == 0` 且 `Player != null` → 迁移旧数据；`SaveVersion == 0` 且 `Player == null` → 框架首次启动建的空档，按 `TbProfileConfig` 新建档案；迁移后置 `SaveVersion = 1`、`Player = null`。
- 烟测改造：`UiSmokeScenario` 不再反射私有字段，改用 `ProcedureLevel` 的 `internal` 只读诊断属性（`CurrentRun`、`HudSerialId`、`PendingSave`）；覆盖 v0 存档迁移、通关检查点、死亡检查点、中途离开回滚、重入读回、`.bak` 回退。`TestArenaController` 用临时 Profile 构建 `HeroLoadout`。

### P4a 框架存档加固（用户授权修改 `Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs`、`Json/EasySave.cs`）
- `EasySave`：新增两段式 API，序列化与 IO 分离：`string Serialize<T>(data, encrypt, key, salt)`（主线程调用）+ `Task<bool> WriteUserTextAtomicAsync(fileName, text)`：写 `<file>.tmp` → 目标已存在时 `File.Replace(tmp, target, target.bak)`，否则 `File.Move`；失败删除 `.tmp` 并返回 `false`。读取新增 `LoadFromUserWithBackupAsync<T>(fileName, encrypt, key, salt)`：主文件不存在或解析失败时尝试 `<file>.bak`。现有方法保持不变（`ProcedureUpdate`、`DownloadComponent` 继续使用）。
- `ArchiveSystem`：
  - 所有写入（Catalogue 与 Data）改用原子写；`SaveAsync()`/`SaveAsync(unitId)`/`OverWriteAsync()`/`Delete` 返回 `Task<bool>`，`LoadAsync()`/`LoadAsync(unitId)` 返回 `Task<bool>`（原调用方 `await` 写法不变）。
  - 串行化：调用时立即在主线程序列化（快照即刻固定，之后再改 `CurrentData` 不影响已排队的写入）；文件 IO 用内部任务链按调用顺序严格 FIFO 执行（`m_Tail = WriteAfterAsync(m_Tail, …)`），读取同样排在链尾，保证读到最近一次写入。
  - `SaveAsync(unitId)`/`OverWriteAsync` 同步重写 `Catalogue.sav`，使槽位显示信息（如名称、等级、保存时间）随覆盖更新。
  - 读取：主文件不存在或损坏时尝试 `.bak`，成功则记 Warning 并继续；两者都失败才按"拒绝覆盖"返回 `false`（保留现有防吞档语义）。首次建档判定改为"`Catalogue.sav` 与 `Catalogue.sav.bak` 都不存在"，避免主目录文件丢失时误建新档。
- 不改：`ArchiveCatalogue`/`ArchiveData` 字段、文件布局、加密、`GF.cs` 泛型参数。
- 同步更新 `Godot/docs/ArchiveSystem.md`（API 返回值、原子写、`.bak`、串行化），并在 `50-FrameworkAndTools.md` 登记本次框架例外。

### P5 规范与文档

- 重写 `Docs/ProjectGuidelines/`：`00-Architecture`（分层、作用域与所有者表、依赖方向、事件分级、目录 = 命名空间、允许的全局：`GF.*`/`ConfigSystem`/`NodePool`/`LayerMask`）；`10-CSharp`（命名后缀约定、禁用 Manager/System/Model）；`20-Configs`（StatType/StatBlock、成长表、启动校验、可调数值不进代码）；`30-EntitiesAndCombat`（实体只持战斗运行时、`HeroLoadout`、StatSheet 修正来源）；新增 `70-ProfileAndSave`（档案、存档 DTO、版本迁移、检查点、关卡事务）；`60-GameplayModules` 改为 §4.12 长期系统落位与数据形状。
- 规范只写规则；现状叙述与本轮记录写入 `Docs/Reviews/architecture_redesign_<日期>.md`。
- `50-FrameworkAndTools` 登记框架例外（`GameFramework.tscn` 流程清单；`ArchiveSystem.cs`/`EasySave.cs` 存档加固）、新测试项目与命令。
- 旧计划 `1791383001385-architecture-domain-refactor.md` 标注"已被取代"。

## 8. 验证

- 每阶段：`Godot/GodotProject` 下 `dotnet build`；新增/移动 C# 后 `"S:\Godot4\Godot4CSharp_console.exe" --headless --build-solutions --path Godot/GodotProject --quit --no-window -q`。
- 单测：`dotnet test Tests/BattleTests`、`dotnet test Tests/LevelTests`、`dotnet test Tests/ProfileTests`。
- 烟测（P3 起）：`-- --smoketest=level`、`level-cancel`、`ui` 及驱动中其余模式；以 `SMOKE PASS/FAIL` 判定，框架退出异常单独登记。
- 引用/场景：`python Tools/ProjectMaintenance/validate_game_references.py`、`EditorScripts/validate_level_scene.gd`；改表后运行导表 bat。
- 存档：`Tests/ProfileTests` 覆盖 v0 迁移、空档建档、映射往返；烟测覆盖通关写盘、死亡写盘、中途离开不写盘、`.bak` 回退、重入读回（均在 `SmokeTestDriver` 的隔离存档目录）。
- 收尾：`git diff --check`、`git status` 核对只改计划内文件（框架改动仅限 `GameFramework.tscn` 流程清单、`ArchiveSystem.cs`、`EasySave.cs`）。

## 9. 风险与行为变化

- 行为变化（D6b 已确认）：通关和死亡都保存本关收益；只有中途离开、运行错误、关停不保存（现状是每次经验变化即写盘）。死亡后自动重开（新）。
- 旧存档：v0 → v1 迁移有单测与烟测；迁移后首次检查点保留 `.bak`。
- 框架改动（D10 已授权）：`ArchiveSystem` 返回值从 `Task` 改为 `Task<bool>`，现有 `await` 调用兼容；`File.Replace` 要求临时文件与目标在同一卷（同目录，满足）；烟测验证"写入 → `GF.Archive.LoadAsync` 读回"和"主文件损坏 → `.bak` 回退"。
- xlsx 由脚本就地改写：先备份 `Configs/GameConfig/Datas/` 涉及文件；成长表数值与现公式逐级对比测试。
- 同状态重入依赖 GGF `ChangeState` 现行实现（无同状态保护）；烟测覆盖两次以上重入。
- Ge 重生成仅改 `BattleHud.cs` 字段部分；`*.Logic.cs` 手写。
- 范围外：多存档选择 UI、菜单/地图流程、技能/Buff/背包/装备/掉落实现（只在规范落位）。
