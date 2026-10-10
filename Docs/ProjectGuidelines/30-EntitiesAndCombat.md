# 30 实体与战斗

> 状态：已实现部分按当前代码约束；旧项目只提供玩法参考；未实现内容不预设方案。  
> 证据：`Godot/GodotProject/TheGame/GameScripts/Entity/`、`GameScripts/Battle/`（含 `Stats/`）、`TheGame/Entitys/`、`project.godot`、旧项目 `ZMXY_BHYH/Script/Base/BaseHero.gd` 与 `BaseMonster.gd`。

## 已实现

- **实体层级**：`ActorEntity : CharacterBody2D, IEntity` 提供人怪共用能力；`HeroEntity` 与 `MonsterEntity` 分别承载角色和怪物行为；具体角色按类型派生。实体脚本手写，不生成 Ge 半类。
- **基类与具体类**：`MonsterEntity` / `HeroEntity` 是最通用基类，只做"所有怪物/英雄都一样"的事（配置、结算、感知、状态机推进），**不预设任何具体行为**；状态集与参数用抽象成员强制由具体类给出。每个具体怪物/英雄直接继承基类并声明自己的行为：`HuaguoshanMonkeyEntity`、`DemonMonkeyEntity`、`WukongEntity` 各自给出 `CreateBodyStates` / `CreateAiStates` / `InitialBodyStateType` / `InitialAiStateType` / `BuildAiParams` / `BuildAttackSpec` 与 `InHurt` / `InRecovery`；数值与招式一律来自配置表。新增行为（远程、飞行、精英、Boss、双武器）在对应具体类里改这些成员，不新增中间层、不修改基类。测试场地见 `TestArena.tscn`（不进入游戏流程）。
- **生命周期**：实现 `OnInit / OnShow / OnUpdate / OnHide / OnRecycle`，显示和隐藏经 `GF.Entity`；`OnInit` 只做一次性装配。英雄 `OnShow` 只接受同一英雄的 `HeroLoadout`（由档案构建，测试场地用临时档案构建），清空属性来源后应用装配、补满生命魔法、清零无双；怪物接收出生点 `Vector2`，按 `MonsterConfig.Stats` 重建属性并补满。池复用时保持运行时容器实例稳定，重建/销毁状态机并清理攻击数据；不持有显示参数引用。
- **实体只持有战斗运行时**：`ActorEntity` 持有 `StatSheet Stats`（属性汇总）、`Vitals Vitals`（生命魔法）与 `Level`；`HeroEntity` 另持有 `MusouGauge Musou`。等级、经验、金币等持久进度属于档案（见 [70-ProfileAndSave.md](70-ProfileAndSave.md)），实体不持有、不写入。
- **属性汇总**：最终值 = (基础 + Σ固定值) × (1 + Σ同类百分比) × Π(1 + 独立百分比)，下限 0，整数属性四舍五入。基础值来自成长表或怪物配置（`SetBase`）；装备、法宝、被动、丹药、Buff 各自作为一个 `StatSource` 整体登记（`SetSource`）和移除（`RemoveSource`），不直接改基础值。持久来源经 `HeroLoadout` 注入，局内来源（Buff）由实体运行时登记；装配替换只移除上一次装配的持久来源。
- **结算快照**：出招与受击时各取一次 `CombatantStats.From(Side, Level, Stats)`；快照放进攻击包，出招后属性变化不影响已发出的攻击。
- **生命与死亡**：`Vitals` 是生命魔法的唯一所有者，上限随属性汇总同步（`SyncVitalsToStats`）。`ReceiveHit` 是扣血与死亡的唯一入口：结算 → `Vitals.Damage` → 生命归零的那一次置 `Dead` 并触发一次 C# 事件 `Died`（作用域所有者订阅，如关卡运行结算英雄死亡）。治疗经 `Heal`，已死亡不复活；无双由命中收益累计。
- **身体状态机**：使用 `GF.Fsm`，由实体 `_PhysicsProcess` 调用 `BodyFsm.Tick`；每帧顺序为输入采样、状态推进、`MoveAndSlide`。状态类表达动作与打断，不以多个布尔量拼状态。
- **怪物 AI**：AI 状态机按框架帧运行，只写意图；身体状态机按物理帧执行移动和动作。当前状态图为 `Pause / Wander / WalkToTarget / StandAndStrike / PaceBelowTarget / CcLocked / Death`。AI 通过接口访问目标和身体事实，不持有节点引用。
- **出手节奏**：`StandAndStrikeState` 进入攻击范围后先按 `MonsterConfig.AttackFirstDelay`（x=最短、y=最长秒）随机停一拍，之后每 `AttackInterval` 按 `AttackDesire`（0-100 出手概率）掷一次。持续交战的期望出手间隔 ≈ `AttackInterval ÷ (AttackDesire/100)`：欲望越高间隔越短，所以间隔不随欲望二次缩放——两个旋钮正交，便于单独调参；首次延迟只是反应时间，与概率无关。
- **动画与判定**：`AnimationPlayer` 只承载帧、特效和判定盒值轨道，不放方法轨道、不调用玩法代码。身体状态读取动画长度并自行计时；音效由状态钩子触发并按配置 ID 播放。HitBox 几何及启闭由动画值轨道提供，朝向由判定盒根节点镜像。
- **攻击范围**：当前怪物近战范围从攻击动画判定盒轨道读取。`AttackReachReader` 目前只读取直接子节点中的一个 `CollisionShape2D`，并以形状边界矩形估算；多形状、复杂变换和非矩形精确范围尚未验证。
- **战斗结算**：`Battle/` 保存属性快照、攻击包、伤害结果和结算公式；不读取场景树或引擎全局状态。随机值由调用方提供。攻击包用 `ReferencePool` 管理，一招对同一目标只结算一次；物理、魔法、真实伤害及击退系数取现有战斗配置。
- **事件边界**：跨模块结果通过 `GF.Event` 发布池化事件参数。事件只携带值，不持有攻击包或实体；订阅者在分发期间读取，不保存事件对象。转发时创建新参数实例。
- **物理层**：代码使用层名，不写位掩码数字：`World`、`PlayerBody`、`EnemyBody`、`Platform`、`PlayerHitBox`、`EnemyHurtBox`、`EnemyHitBox`、`PlayerHurtBox`、`MagicWeapon`、`Trap`、`Item`、`Exit`、`Detector`。层序由 `project.godot` 定义。

## 旧项目参考

- `ZMXY_BHYH/Script/Base/BaseHero.gd`、`BaseMonster.gd` 可查角色、怪物的旧玩法和数值表现；旧实现含集中式行为和状态标志，不沿用其代码结构。
- 旧项目判定、掉落和音效数据只作核对依据；新逻辑按当前实体、战斗、配置和事件边界重写。

## 待设计

- Boss、飞行怪、远程怪及其新增身体/AI 行为。
- 子弹、掉落实体、法宝攻击等新攻击来源的实体与生命周期约定（落位见 [60-GameplayModules.md](60-GameplayModules.md)）。法宝、宠物等跟随物优先做独立实体并记录拥有者实体 ID：GGF `AttachEntity` 的子实体隐藏时不回组容器。
- 动画标记驱动的中途玩法时点；当前动画库禁止方法轨道。
