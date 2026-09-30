# Entity/ 目录规范（实体与状态机）

> 裁决顺序见根规范 §2；战斗结算见 `../AGENTS.md`。

## 继承树（不新造中间基类）

```
ActorEntity : CharacterBody2D, IEntity        # 抽象，共用：血量/受击/朝向翻转/身体层/判定盒/动画宿主启停/招式装配
  ├── HeroEntity → WukongEntity ...           # 英雄机制 + 专属层（武器层、特效层 m_EffectRoot 在 HeroEntity）
  └── MonsterEntity（抽象）→ <各怪>           # 身体层；每怪覆写 CreateBrain
BulletEntity : Node2D, IEntity   DropItemEntity / MagicWeaponEntity ...
```

- 直接继承 Godot 原生类型 + `IEntity`；新实体先看现有类（`ActorEntity` / `HeroEntity`）再动手，公共能力下沉父类。
- 角色事实（输入/连段…）放最具体的类，基类不预埋用不上的字段；**场景节点引用同理**——只有所有子类都有的节点才进 `ActorEntity`（2026-09-30：特效层下沉到 HeroEntity，怪物检查器不再出现空的 `m_EffectRoot`）。
- 基类不给"静默默认值"掩盖漏实现：结算侧别 `Side`、属性快照 `GetCombatStats`、怪物大脑 `CreateBrain` 都是 abstract。

## 生命周期与生成回收

- 只实现 `OnInit / OnShow / OnUpdate / OnHide / OnRecycle`；生成/回收走 `GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity(Safe)`，`EntityId` 用 Luban 枚举，禁魔法数字。
- 实体脚本手写，不跑「Generate Script」（Ge 半类会与基类重复声明 `IEntity` 成员，不兼容本继承树）；节点引用 `[Export]` + `m_` 手工声明（场景侧 `node_paths` 见 `Entitys/AGENTS.md`）。
- 子弹/掉落物/特效等高频生灭对象走池（红线 6）。

## 状态与动画（AnimationTree 表达式状态机）

**分工**：C# 只维护**角色属性**（`[Export]` 事实面），动画状态机（该角色自己的 `AnimationTree` 资源）用 `advance_expression` 只读属性决定播放。图上没有布尔参数、没有脉冲，C# 里没有"当前状态"枚举。**基类不放角色事实**。

- **属性事实面**：写在角色自己的类里，如 `HeroEntity` 的 `MoveInput / Running / JumpCount / AttackSegment / Hurt / Emoting`（`Dead` 在 `ActorEntity`，唯一置位点在 `ReceiveHit` 扣血扣到 0）——全部**单一事实源**：直接事实是输入/物理写入的 `[Export]` 字段，事件事实在状态进入/离开处翻转（无"每帧同步"步骤）；派生事实（如 `Rising` / `Airborne`）是 **`[Export]` 计算属性**：getter 实时计算、setter 为空实现（只读属性不可 `[Export]`（GD0103），不导出对引擎又不可见——空 setter 是让表达式能读到属性的唯一途径）。
- **出招时序与判定盒**（2026-09-30 审查定稿）：攻击动画的方法轨道回调 C# `OnAttackBegin`/`OnAttackEnd`（只管数值包：属性快照/连段推进）；判定盒形状/位置/开关是动画**值轨道关键帧**（同旧项目 keyframe shape/position/disabled，原生朝左坐标，朝向由 `m_HitBoxRoot` 容器 scale.x 镜像）——代码零几何，打断出招时 AnimationMixer 自动还原。
- **动画资源**：动画库 `Entitys/Animations/<角色>_anim_library.tres` + 状态机 `Entitys/Animations/<角色>_animation_tree.tres`；节点名 = 动画名。首版由 `Tools/LegacyMigration/gen_animations.py` / `EditorScripts/build_<角色>_anim_tree.gd` 生成，**落地后以编辑器保存的版本为准**（2026-09-30 人类裁决），生成器默认只校验不覆盖（见 `EditorScripts/AGENTS.md`）。改完跑一次树生成器做校验 + 冒烟测试。
- **图结构**：主图只留状态组 + 单状态（`Ground` / `Air` / `Attack` 子机可嵌套 ＋ `Hurt` / `Death`）：
  - 组间边 = 目标组**组谓词**（`P_*` 常量，互斥完备、只写一次）；组内边只写**组内区分项**（走/跑、跳/二段/落、段序号）。
  - 子状态机用 **ROOT 类型** + 进组边 `reset=true`（缺 reset 子机不播放）；每个状态一条 `Start → 状态` 边。
  - **组内只连真实转移**（连段只有 1→2→3→4 链）；边表与"为什么没有某条边"写生成器注释，改 C# 事实逻辑时同步检查。
  - 打断规则集中写进条件链（如"出招受击不打断" = 受击条件要求 `AttackSegment < 0`），禁止散落赋值。
- **表达式红线**：
  - 读 C# 成员必须 `[Export]` 字段（普通属性引擎侧不可见）；基对象由 `ActorEntity.OnInit` 指到实体节点。
  - 引擎自带数据必须用引擎名：`is_on_floor()` / `velocity.y` 可用，写成 `IsOnFloor()` / `Velocity` 会静默求值为 null → 条件恒假（迁移期动画失效的根因）。
  - 边必须 `advance_mode=AUTO`（转移同帧生效）。
- **旧资产注意**：起跳上升时间比动画长，生成动画库时给 jump/jump_2 补 drop 落姿尾帧（`gen_animations.py` 的 `JUMP_TAIL_*`）。
- **命名标准**：英雄 `idle1/idle2 / walk / run / jump / jump_2 / fall / attack_1..n / hurt / death`，技能 `skill_<SkillId>` 随技能系统；怪物状态 `Idle / Patrol / Chase / Attack / CcLocked / Death`。角色专属动画名只出现在该角色的动画库/状态机资源与类覆写（如 `WukongEntity.IdleFlavorAnim`）。Boss 阶段转换也是**状态**，不写巨型 if 链。
- 纯逻辑状态机（怪物 AI 决策）用 `GF.Fsm`（每状态一个类），动画选择不用它。禁止 bool 拼状态（红线 7，旧项目最大教训）。

## 目录（2026-09-30 整理）

```
Entity/
├─ ActorEntity.cs  HurtBox.cs          GameLogic.Entity           英雄与怪物共用
├─ Heroes/HeroEntity.cs                GameLogic.Entity.Heroes    英雄机制
│  └─ <英雄>/<英雄>Entity.cs            GameLogic.Entity.Heroes    如 Wukong/WukongEntity.cs
└─ Monsters/MonsterEntity.cs  AttackReachReader.cs   GameLogic.Entity.Monsters   怪物身体层
   ├─ AI/                              GameLogic.Entity.Monsters.AI         AI 框架（纯 C#）
   │  └─ States/                       GameLogic.Entity.Monsters.AI.States  默认六状态
   └─ <怪物>/<怪物>Entity.cs            GameLogic.Entity.Monsters  如 HuaguoshanMonkey/，该怪专属状态也放这里
```

按**种类**分文件夹，不按小怪/精英/Boss 分——阶级是数据（`MonsterConfig.Rank`），同一种怪换数值即可成精英。

## 怪物 AI（M5，`Monsters/AI/`）

**三层分工**：AI 状态机（`GF.Fsm<IMonsterAiAgent>`）只写**意图**（`Move / Face / RequestAttack`）→ `MonsterEntity` 把意图提交为**事实**（`MoveInput / AttackSegment`，出招请求在安全帧提交并计冷却）并负责物理/受击/死亡/收招硬直 → `AnimationTree` 只读事实选动画。AI 状态名与动画状态互不耦合，改 AI 不动动画图。

- **`Monsters/AI/` 是纯 C#**：状态只经 `IMonsterAiAgent` 读感知、写意图，随机数由宿主提供；禁止在状态里碰节点、`GD.*`、`GF.*`、Godot 类型（几何用 `AiBox`）。单测用框架真实 `FsmManager` + 假宿主驱动（`Tests/BattleTests/MonsterAiTests.cs`）。
- **大脑显式声明**：`MonsterEntity` 是抽象身体层，每种怪必须覆写 `CreateBrain()`，从 `MonsterBrains` 取原型（现有 `GroundMelee`），需要时再 Bind / AddExtra。参考 tModLoader `aiStyle`、Unity Game Kit `EnemyController` + 每怪一个 Behaviour。
- **状态按角色跳转**：`MonsterAiRole`（Idle/Patrol/Chase/Attack/CcLocked/Death）是槽位，状态用 `ChangeRole` 跳转，由 `MonsterAiStateSet` 解析到具体实现。打断优先级（死亡 > 受控 > 自身决策）集中在 `MonsterAiState.OnUpdate`，状态可用 `CanBeCcLocked=false` 声明不可打断（Boss 演出）。
- **够不够得着 = 动画判定盒**（2026-09-30 人类裁决）：`AttackReachReader` 在 OnInit 从每招攻击动画（`AttackConfig.Animation`）的判定盒值轨道推导范围（判定窗口内形状并集，原生朝左），与目标受击盒（`HurtBox.GetGlobalBounds`）求交——**含高度**。表里不再写近身距离；`AttackConfig.AiRange` 只给无身体判定盒的远程招（`0,0` = 按判定盒推导）。
  - Chase → Attack：普攻判定盒**水平**吃进目标 ≥ `MonsterAiRules.ReachMargin`（不看高度，目标在头顶也站到下面）；
  - Attack：水平间隙 > `AttackRangeSlack` 回追击；间隙在滞回区内小步贴近；水平够但高度够不着 → 站定面向、不出招。
- **出招节奏**：请求 → 下一安全帧提交（转向目标、锁定朝向）→ 动画 `OnAttackEnd` → **收招硬直** `AttackConfig.AiRecovery` 秒（不动、不转身、不出招；`IsAttacking` 覆盖整段）→ 恢复决策。受击仍打断出招与硬直（旧项目手感），霸体不打断。
- **招式用法是数据**：`MonsterSkillBook` 按 `AttackConfig.AiPriority/AiWeight/AiRange/AiCooldown/AiInitCooldown` 选招与冷却。`AiPriority=0` 普攻池（站定后按 `AttackDesire` 每 `AttackInterval` 掷一次再按权重抽）；`>0` 技能（冷却就绪且够得着即放，优先级高者胜，追击途中也会放）。
- **感知**：`m_Detector`（Area2D，Detector 层 → mask PlayerBody，宽 = 2×SightRange）无目标时锁敌；被打直接锁定攻击方；目标只在死亡/回收时失效（同旧 has_target）。
- **生命周期**：状态机在 `OnShow` 创建、`OnHide` 销毁（池复用即全新 AI）；`SetAiEnabled(false)` 退化为沙包（调试用）。死亡时广播 `MonsterDiedEventArgs`（M6 关卡计数/掉落/经验订阅它）。

**新怪物扩展分层**（能停在上层就不往下走）：

| 层 | 做法 | 适用 |
| -- | --- | --- |
| 1 选原型 + 数据 | 新建 `Monsters/<怪物>/<怪物>Entity.cs`，覆写 `CreateBrain() => MonsterBrains.Xxx()`；MonsterConfig 行 + AttackConfig 行 + 场景 + 动画库（判定盒决定攻击范围） | 绝大多数小怪；精英怪（多技能 + `SuperArmor` + `Rank=Elite`） |
| 2 替换角色 | `CreateBrain` 里拿到原型后 `Bind(new XxxChaseState())`（继承默认状态复用逻辑）；通用的一类行为沉淀为 `MonsterBrains` 新原型 | 飞行怪、远程风筝怪、冲锋怪 |
| 3 额外状态 | `CreateBrain` 里 `AddExtra(...)`，由自定义状态按类型进入、按角色回落；配合覆写 `IsSuperArmor` 等钩子 | Boss 阶段转换、狂暴、召唤演出 |
| 4 招式效果 | 攻击动画加方法轨道回调（子类方法），发射池化子弹实体/上 Buff | 弹幕、召唤物、附加控制 |

- 状态实例不可跨状态机共享（GF.Fsm），`CreateBrain` 每次调用都必须返回全新状态集（原型工厂每次 new）。
- 需要新感知（平台边缘、血量阈值）时加到 `IMonsterAiAgent`，由 `MonsterEntity` 实现，状态里不查场景树。

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
