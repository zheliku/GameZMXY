# Entity/ 目录规范（实体与状态机）

> 裁决顺序见根规范 §2；战斗结算见 `../AGENTS.md`。

## 继承树（不新造中间基类）

```
ActorEntity : CharacterBody2D, IEntity        # 抽象，共用：血量/受击/朝向翻转/身体层/判定盒/动画宿主启停/招式装配
  ├── HeroEntity → WukongEntity ...           # 英雄机制 + 专属层（武器层、特效层 m_EffectRoot 在 HeroEntity）
  └── MonsterEntity（抽象）→ <各怪>           # 身体与默认怪物 AI 宿主
BulletEntity : Node2D, IEntity   DropItemEntity / MagicWeaponEntity ...
```

- 直接继承 Godot 原生类型 + `IEntity`；新实体先看现有类（`ActorEntity` / `HeroEntity`）再动手，公共能力下沉父类。
- 角色事实（输入/连段…）放最具体的类，基类不预埋用不上的字段；**场景节点引用同理**——只有所有子类都有的节点才进 `ActorEntity`（2026-09-30：特效层下沉到 HeroEntity，怪物检查器不再出现空的 `m_EffectRoot`）。
- 基类不给"静默默认值"掩盖漏实现：结算侧别 `Side`、属性快照 `GetCombatStats` 都是 abstract。

## 生命周期与生成回收

- 只实现 `OnInit / OnShow / OnUpdate / OnHide / OnRecycle`；生成/回收走 `GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity(Safe)`，`EntityId` 用 Luban 枚举，禁魔法数字。
- 实体脚本手写，不跑「Generate Script」（Ge 半类会与基类重复声明 `IEntity` 成员，不兼容本继承树）；节点引用 `[Export]` + `m_` 手工声明（场景侧 `node_paths` 见 `Entitys/AGENTS.md`）。
- 子弹/掉落物/特效等高频生灭对象走池（红线 6）。

## 状态与动画（代码身体状态机 + 动画纯数据，2026-10-01 人类裁决）

**分工（A 方案）**：身体状态机是唯一的逻辑执行者与唯一的时钟；动画是纯表现数据，从不调用代码。参考：Unity 社区对"事件驱动核心玩法"的警示（打断时收尾事件不触发、末帧事件可能不触发）、Celeste（全代码计时）、Godot 动画标记（作者权在动画编辑器、代码读时间自己计时）。

```
英雄：HeroInput(采样+缓冲) ─→ 身体状态机（唯一逻辑 + 唯一时钟）─→ Velocity / MoveAndSlide
                                   └─→ PlayAnim / RestartAnim ─→ AnimationPlayer（纯播放）
怪物：AI 状态机(GF.Fsm, 框架帧) ─意图→ 身体状态机(GF.Fsm, 物理帧) ─→ 同上
动作时长：OnInit 从动画资源读长度（ActorEntity.GetAnimLength）→ *BodyParams 快照 → 状态用物理 dt 计时
音效：状态钩子触发（BeginAttack 含起手音 / PlayHurtSound / OnDied），音源 ID 查表
判定盒窗口：动画值轨道（M4 裁决——"哪一帧的几何"是数据）
将来中途时点（第 N 帧放子弹/脚步声）：Godot 4.3+ 动画标记（Animation.GetMarkerTime），代码读时间自己计时
```

- **身体状态机 = GF.Fsm，按物理帧驱动**：`GF.Fsm` 由框架在 `_Process`（渲染帧）里轮询（`Fsm.Update` 为 internal），所以 `BodyState<T>` 把框架帧的 `OnUpdate` 封成空实现，由实体在 `_PhysicsProcess` 里调用 `BodyFsm.Tick(fsm, dt)`，内部取 `IFsm.CurrentState`（public）调用 `PhysicsTick`。状态登记、进入/离开、`ChangeState` 全用框架自带的，**框架零改动**。不走 `UpdateDriver.AddFixedUpdateListener`：实体本身就是 `CharacterBody2D`，原生 `_PhysicsProcess` 才是显式且有序的物理帧入口。
- **状态机单入口**：Tick 是进状态机的唯一通道。物理/结算来源的事实（受击击退、死亡、出招请求、输入）都只写进宿主，状态在 Tick 里自己消费（`TakePendingHurt` / `TakeAttackRequest` / `Input.Consume*`）——没有事件通道、没有动画回调。
- **每物理帧顺序**：采样输入 → `BodyFsm.Tick` → `MoveAndSlide`。AnimationPlayer 是子节点、回调模式为 Physics，同一帧稍后消费播放请求。同一帧内状态切换后，新状态**当帧接着执行**（`BodyFsm.MaxHopsPerFrame` 防死循环），不留"晚一帧"。
- **动作时长 = 动画长度（数据）**：OnInit 时读动画长度进 `*BodyParams`（AttackTimes / HurtTime / DeathTime / EmoteTime），状态用物理 dt 自己计时、`BodyState.Elapsed` 判到点（含浮点容差）。编辑器里改动画长度，下次运行自动同步、不可能失步。**禁止用 `fsm.CurrentStateTime`**（按渲染帧累计）。与动画无关的表数值（收招硬直 `AiRecovery`）同样状态计时。
- **状态按"能做什么"拆，动画在状态内部选**：
  - 英雄：`Ground`（idle1 / walk / run，以及憨笑 idle2 计时器）、`Air`（jump / jump_2 / fall）、`Attack`、`Hurt`、`Death`；
  - 怪物：`Move`（idle / run）、`Attack`、`Recovery`（收招硬直）、`Hurt`、`Death`。
  - 状态类加 `Hero` / `Monster` 前缀（与 AI 行为类区分）；状态名 = 类名去掉前缀与 `State`。
- **打断优先级集中在基类**（`HeroBodyState` / `MonsterBodyState.Interrupt`）：死亡 > 受击（各状态用 `HurtInterrupts` 声明是否立即打断，比如英雄 Attack 把受击挂起到收招）> 本状态自身决策。禁止在派生状态里散落打断判断。
- **宿主接口，纯 C#**：公共契约 `IActorBody`（速度、死亡、攻击段、`BeginAttack` / `EndAttack`、受击事实、播放、朝向、重力、`PlayHurtSound` / `OnDied`）由 `ActorEntity` 统一提供；`IHeroBody` / `IMonsterBody` 只留角色专属成员（英雄：输入层、跳跃/连段、随机源；怪物：移动意图、出招请求、回收）。状态不碰节点、不碰 `GD.*` / `GF.*`；Godot 纯值类型（`Vector2`）可以用，与 Battle/ 裁决一致。单测用框架真实 `FsmManager` + 假宿主（`Tests/BattleTests/HeroBodyTests.cs` / `MonsterBodyTests.cs`）。
- **播放接口**（`ActorEntity`，直接驱动 `m_AnimPlayer`）：
  - `PlayAnim(名)`：本帧已请求同名动画时不打断，否则 `Play`（从头播；非循环动画播完停在末帧，逐帧同名请求不会重播）；
  - `RestartAnim(名)`：`Stop` 后 `Play`，攻击段、受击、死亡等必须从第 0 帧播的动作；
  - `CurrentAnim` 读 `AssignedAnimation`（调试/冒烟观测）。
- **动画资源只放表现数据，无方法轨道**：帧轨道、特效轨道、判定盒值轨道（`Entitys/Animations/<角色>_anim_library.tres`，编辑器为真相源，`validate_anim_libraries.gd` 校验**禁止一切方法轨道**）。**值轨道完备性**：库内任一动画写过的值轨道路径，每个动画（含 RESET）都必须带——直驱播放不回卷轨道，缺轨的属性会残留上一个动画写入的值。
- **音效由代码触发、ID 在表里**：起手音随 `BeginAttack`（`AttackConfig.SoundId`）、受击音随受击生效（`HurtSoundId`）、死亡音随死亡生效（`DeathSoundId`/`OnDied`）；命中音是结算结果（`AttackData.HitSoundId`）。现有时点全部在动作开始处，等价于状态进入时播。
- **判定盒**：形状/位置/开关是动画**值轨道关键帧**（原生朝左坐标，朝向由 `m_HitBoxRoot` 容器 scale.x 镜像），代码不写任何几何；打断出招时的复位靠轨道完备性（切到任何动画首帧写回安全值）。
- **池复用**：身体状态机在 `OnShow` 创建、`OnHide` 销毁（同怪物 AI）。初始状态进入时请求的动画把播放器从上次的任意动画（含死亡）拉回来，**不需要"死亡 → 地面"回拉**。出招状态 `Leave` 时经 `EndAttack` 归还攻击包（收招、打断、隐藏都走这里）。
- **输入层** `HeroInput`：双击跑 + 带缓冲窗口的一次性请求（跳 / 攻，窗口 `HeroConfig.InputBufferTime`，0 = 只在按下那一帧有效）。缓冲是手感设计：Godot 4 在物理帧里查 `IsActionJustPressed` 不会漏键。受击硬直进入时清空缓冲。
- **旧资产注意**：起跳上升时间比动画长，生成动画库时给 jump/jump_2 补 drop 落姿尾帧（`gen_animations.py` 的 `JUMP_TAIL_*`）。
- **命名标准**：
  - 英雄动画 `idle1/idle2 / walk / run / jump / jump_2 / fall / attack_1..n / hurt / death`（代码常量 `HeroAnims`），技能动画 `skill_<SkillId>` 随技能系统；
  - 怪物动画 `idle / run / attack_<i> / hurt / death`（`MonsterAnims`）；
  - 攻击动画名来自 `AttackConfig.Animation`；角色专属动画名只出现在该角色的动画库资源与类覆写里（如 `WukongEntity.IdleFlavorAnim`）；
  - 怪物 AI 状态按具体行为命名：`Pause / Wander / WalkToTarget / StandAndStrike / PaceBelowTarget / CcLocked / Death`；Boss 阶段转换也是**状态**，不写巨型 if 链。
- **扩展**：新动作（冲刺、技能、落地硬直）= 新增一个身体状态类，登记进 `CreateBody` 的状态列表，在需要的状态里 `ChangeState`，时长加进 `*BodyParams`；新的可打断规则写进基类 `Interrupt`。禁止 bool 拼状态（红线 7，旧项目最大教训）。

## 目录（2026-10-01 整理）



```
Entity/
├─ ActorEntity.cs  HurtBox.cs              GameLogic.Entity           英雄与怪物共用（含 PlayAnim/RestartAnim）
├─ Body/                                   GameLogic.Entity.Body（纯 C#）身体状态机共用层
│  ├─ IActorBody.cs  BodyState.cs  BodyFsm.cs
├─ Heroes/HeroEntity.cs                    GameLogic.Entity.Heroes    英雄宿主（输入采样/物理/音效）
│  ├─ Body/                                GameLogic.Entity.Heroes.Body（纯 C#）
│  │  ├─ IHeroBody.cs  HeroBodyParams.cs  HeroInput.cs  HeroAnims.cs  HeroBodyState.cs（打断规则）
│  │  └─ HeroGroundState / HeroAirState / HeroAttackState / HeroHurtState / HeroDeathState
│  └─ <英雄>/<英雄>Entity.cs                如 Wukong/WukongEntity.cs
└─ Monsters/                               GameLogic.Entity.Monsters
   ├─ MonsterEntity.cs  AttackReachReader.cs          两台状态机的宿主（所有怪共用）
   ├─ Body/                                GameLogic.Entity.Monsters.Body（纯 C#）
   │  ├─ IMonsterBody.cs  MonsterBodyParams.cs  MonsterAnims.cs  MonsterBodyState.cs（打断规则）
   │  └─ MonsterMoveState / MonsterAttackState / MonsterRecoveryState / MonsterHurtState / MonsterDeathState
   ├─ AI/                                  GameLogic.Entity.Monsters.AI（纯 C#；当前只实现猴子状态图）
   │  ├─ Core/     状态基类、宿主接口、参数、AiBox
   │  ├─ Attacks/  招式用法 MonsterAttackSpec 与招式书 MonsterAttackBook（选招/够不够得着）
   │  └─ States/                           GameLogic.Entity.Monsters.AI.States
   │     ├─ Roam/       无目标规则：RoamState / PauseState / WanderState
   │     ├─ Engage/     交战规则：EngageState / WalkToTargetState / StandAndStrikeState / PaceBelowTargetState
   │     └─ Interrupt/  打断状态：CcLockedState / DeathState
   └─ <种类>/<种类>Entity.cs                如 HuaguoshanMonkey/；该怪独有的行为也放这里（<种类><行为>State）
```

按**种类**分文件夹，不按小怪/精英/Boss 分。当前怪物 AI 由 `MonsterEntity` 直接创建一套固定状态，不预建原型、角色槽或额外状态装配；新怪物出现后再根据真实行为决定是否增加差异。

## 怪物 AI（M5，`Monsters/AI/`）

AI 状态机使用框架帧，只写移动、朝向和出招意图；身体状态机按物理帧执行移动、受击、攻击、硬直和死亡；动画只提供表现数据。身体不依赖 AI，AI 通过 `IMonsterAiAgent` 读取目标与身体结果。

- **`Monsters/AI/` 是纯 C#**：状态只经 `IMonsterAiAgent` 读感知、写意图，随机数由宿主提供；禁止在状态里碰节点、`GD.*`、`GF.*`、Godot 类型（几何用 `AiBox`）。单测用框架真实 `FsmManager` + 假宿主驱动（`Tests/BattleTests/MonsterAiTests.cs`）。
- **宿主接口最小化**：`TargetBox` 空盒即"没有目标"、中心 X 即方位——不另设 `HasTarget` / `TargetDeltaX` 两个同源属性。
- **基类分工**：`MonsterAiState` 集中打断优先级（死亡 > 受控 > 自身决策）；`RoamState` 统一发现目标后进入 `WalkToTarget`；`EngageState` 统一忙碌时不决策、目标失效后进入 `Wander`。派生状态只写自身行为。
- **攻击范围**：`AttackReachReader` 在 OnInit 从攻击动画的判定盒值轨道读取范围，并与目标受击盒求交（含高度）。
  - `WalkToTargetState` 只根据水平距离决定追击或接近攻击范围；
  - `StandAndStrikeState` 处理滞回与出招；目标水平范围内但高度不符时转入 `PaceBelowTargetState`；
  - `PaceBelowTargetState` 按 `PaceRange` 在高处目标下方往返。
- **丢失目标**：目标水平距离持续超出 `SightRange` 达 `LoseTargetTime` 秒，实体清掉目标；无目标时进入 `Wander`，离出生点超出 `PatrolRadius` 会先折返。
- **出招节奏**：AI 请求 → 身体 `Move` 状态在下一物理帧提交（进入 `Attack`：转向目标、锁定朝向、计冷却）→ 招式时长（= 动画长度）走完 → `Recovery` 收招硬直 `AttackConfig.AiRecovery` 秒（不动、不转身、不出招；`IsAttacking` 覆盖整段）→ `Move`。受击打断出招与硬直（旧项目手感）；霸体由宿主不登记受击来表达。
- **招式用法**：`MonsterAttackBook` 在站定时按 `AiPriority=0` 的 `AiWeight` 加权抽选普攻；`AiPriority>0` 的优先招在追击和守候时，只要冷却就绪且可命中便先释放。`AiRange` 仅用于没有实体判定盒的远程/突进招，逐招冷却由 `AiCooldown / AiInitCooldown` 控制；站定普攻节奏用 `MonsterConfig.AttackInterval / AttackDesire`，收招硬直用 `AttackConfig.AiRecovery`。
- **感知**：`m_Detector`（Area2D，Detector 层 → mask PlayerBody，宽 = 2×SightRange）无目标时锁敌；被打直接锁定攻击方；目标只在死亡/回收时失效（同旧 has_target）。
- **生命周期**：状态机在 `OnShow` 创建、`OnHide` 销毁；`SetAiEnabled(false)` 退化为沙包（调试用）。死亡时广播 `MonsterDiedEventArgs`。

新怪物出现后，先实现最小可验证行为；第二种怪物真实复用已有规则时再抽共享状态或策略。Boss 阶段、飞行、远程攻击等行为不提前在猴子状态图中预留入口。

## 物理层（13 层，写进 `project.godot`）

代码只按层名引用：`LayerMask.LayerToMask2D("层名")`，禁止魔法数字。

| 层 | 层名 | 用途 |
| -- | --- | --- |
| 1 | World | 地面/墙体 |
| 2 | PlayerBody | 玩家本体 |
| 3 | EnemyBody | 怪物本体 |
| 4 | Platform | 单向平台（按下键临时关 mask 穿过） |
| 5 | PlayerHitBox | 玩家攻击判定（扫 EnemyHurtBox） |
| 6 | EnemyHurtBox | 怪物受击判定 |
| 7 | EnemyHitBox | 怪物攻击判定（扫 PlayerHurtBox） |
| 8 | PlayerHurtBox | 玩家受击判定 |
| 9 | MagicWeapon | 法宝攻击判定 |
| 10 | Trap | 陷阱与陷阱检测 |
| 11 | Item | 掉落物拾取检测 |
| 12 | Exit | 关卡出口 |
| 13 | Detector | AI 索敌/射线检测 |

> `project.godot` 另有 `layer_32="Ignore"` 预留位；代码只用上表 13 个语义层名。
