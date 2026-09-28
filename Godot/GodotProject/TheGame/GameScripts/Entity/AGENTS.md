# Entity/ 目录规范（实体与状态机）

> 根规范：仓库根 `AGENTS.md`（先读）；TheGame 规范：`../../AGENTS.md`；GameScripts 规范：`../AGENTS.md`（战斗结算规则在这里）。冲突时：根规范 > TheGame > GameScripts > 本文件。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 继承树（不要新造中间基类）

实体直接继承 Godot 原生类型 + `IEntity`，无中间框架基类：

```
ActorEntity : CharacterBody2D, IEntity        # 血量/受击入口/通用表现层（身体/特效）+ 动画宿主
  ├── HeroEntity → WukongEntity ...           # 英雄通用机制 + 英雄专属层（武器）
  └── MonsterEntity → <各怪>
BulletEntity : Node2D, IEntity
DropItemEntity / MagicWeaponEntity ...
```

- 新实体先看现有类（`ActorEntity.cs` / `HeroEntity.cs`）再动手，公共能力下沉到父类，不复制粘贴。
- 层的归属：`ActorEntity` 只放英雄/怪物**共用**的东西（含通用表现层 body/effect、朝向翻转，扩展点 `OnFacingChanged`）；
  **英雄专属的武器层在 `HeroEntity`**；角色事实（输入/跑档/连段…）一律放最具体的类，基类不预埋用不上的字段。

## 生命周期与生成回收

- 只实现框架接口：`OnInit / OnShow / OnUpdate / OnHide / OnRecycle`。
- 生成/回收走 `GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity` / `HideEntitySafe`；`EntityId` 来自 Luban 生成枚举，禁止魔法数字。
- **不要手写脚本骨架**：场景搭好后选中根节点 → Inspector「Generate Script」生成 Ge/Logic 双半类（`Xxx.cs` 生成 + `Xxx.Logic.cs` 手写），生成物放 `GameProto/`。
- 子弹/掉落物/特效等高频生灭对象必须走池（红线 6），禁止裸 `Instantiate + QueueFree`。

## 状态与动画（AnimationTree 表达式状态机）

**分工**：C# 维护**角色属性**，动画状态机（该角色自己的 `AnimationTree` 资源）读属性决定播哪个动画。图上没有布尔参数、没有脉冲、C# 里没有"当前状态"枚举、没有"现在该播哪个动画"的判断。

- **属性（表达式事实面）**：写在角色自己的类里（`[Export]` 字段，表达式才读得到），例如 `HeroEntity` 的
  `MoveInput / Running / Airborne / Rising / JumpCount / AttackSegment / Hurt / Emoting / Dead`；
  每物理帧刷新一次（派生事实在 `SyncAnimFacts()` 里算）。**基类不放角色事实**（`ActorEntity` 只有血量/朝向/判定/动画宿主）。
- **状态机资源**：`Entitys/<角色>_animation_tree.tres`，由 `EditorScripts/build_<角色>_anim_tree.gd` 生成；
  节点名 = 动画名，**每条边 = 目标状态的互斥条件**（`advance_expression`，只读上面的属性）。
- **图 = 主图分组 + 子状态机收纳动画**（2026-09-28 定稿，取代单层完全图）：
  - 主图只留"组"和单状态，一眼可读：`Ground`（子机：`Idle`（子机：idle/憨笑）/走/跑）、`Air`（子机：跳/二段跳/落）、
    `Attack`（子机：普攻连段）＋ `Hurt` / `Death`；组间边只有 Ground ⇄ Air、Ground/Air → Attack →（收招）Ground/Air、
    → Hurt → Ground、→ Death（守卫 = 目标组的组谓词，互斥；见生成器 `P_*` 常量）。
  - **进组 = 从 Start 选一个状态**：子机用 **ROOT 类型**（进组 seek 到第 0 帧时重启到 Start；
    NESTED 类型会恢复上次内部状态，那种语义下组内边必须两两直连才能自纠）；每个状态一条 `Start → 状态` 边。
    子机可再嵌套（`Ground → Idle`），每层都按同一规则工作，层级不改变时序语义。
  - **组内只连真实转移**（不是完全图）：连段只 +1 推进就只有 1→2→3→4 链；各组的边表与
    "为什么没有某条边"写在生成器 `GROUND_EDGES` / `AIR_EDGES` / `IDLE_EDGES` / `_attack_edges()` 注释里（改 C# 事实逻辑时同步检查）。
  - **组谓词只写一次**（`P_*` 常量，含 `not Dead` 的优先级链）；组内边只写**组内区分项**（走/跑、跳/二段/落、段序号）——
    组谓词不成立时主图在同一帧就切出本组，组内选择不出现在画面上。
  - 时序（4.7.2 源码 + 冒烟测试）：组内转移（走跑切换、连段推进、跳→落）**同帧生效**；进组那一帧子机
    在同一 process 内完成"启动+选路"（中间混合权重为 0、不产生可见输出），可见延迟与主图直切相同。
  - **进组边必须 `reset=true`**（Transition 默认值）：子机靠"seek 到第 0 帧"启动；缺 reset 子机会停在空 current、永不播放。
- **表达式红线**：
  - 读 C# 成员必须走 `[Export]` 字段（普通属性引擎侧不可见），基对象由 `ActorEntity.OnInit` 指到实体节点；
  - 引用引擎自带数据必须用引擎名：`is_on_floor()` / `velocity.y` 可用，写成 C# 风格 `IsOnFloor()` / `Velocity` 会静默求值为 null → 条件恒假（迁移期"跑/跳动画失效"的根因）；
  - 边必须 `advance_mode=AUTO`；转移在触发它的那次 process 同帧生效。
- 打断规则集中写进条件链（如"出招期间受击不打断"= 受击条件要求 `AttackSegment < 0`），禁止散落赋值。
- **不要手改 `.tres`**：改状态机 = 改生成器的状态表/边表（`GROUND_STATES` / `AIR_STATES` / `IDLE_STATES` / 攻击段展开 / `ROOT_EDGES`）
  + 重跑生成器（`--script res://EditorScripts/build_wukong_anim_tree.gd`）。
- **起跳动画带"落姿"尾帧**：旧项目 jump1/jump2 播完就切 drop（`BaseHero.gd:522/529`），而上升时间（≈0.55s）比动画长；
  我们按物理切（`not Rising`），所以生成器给 jump/jump_2 补了 drop 姿势尾帧（`gen_animations.py` 的 `JUMP_TAIL_*`），
  使可见姿势在动画播完时即变落姿，与旧项目一致。
- 角色专属动画名只允许出现在两个地方：该角色的动画库/状态机资源、该角色的类覆写（如 `WukongEntity.IdleFlavorAnim`）。
- 英雄标准状态：`Idle / Walk / Run / Jump / Fall / Attack1..n / Skill_<SkillId> / Hurt / Stun / Death`。
- 怪物标准状态：`Idle / Patrol / Chase / Attack / CcLocked / Death`；Boss 阶段转换也是**状态**，不写巨型 if 链。
- 纯逻辑状态机（如怪物 AI 决策）可用 `GF.Fsm`（`Fsm<T>` + 每状态一个类）；动画选择不用它。
- 禁止用多个 bool 标志位拼状态（红线 7，旧项目最大教训）。

## 物理层（13 层，写进 `project.godot`）

代码只允许按层名引用：`LayerMask.LayerToMask2D("名字")`，禁止魔法数字。

| 层 | 层名          | 用途                               |
| -- | ------------- | ---------------------------------- |
| 1  | World         | 地面/墙体                          |
| 2  | PlayerBody    | 玩家本体                           |
| 3  | EnemyBody     | 怪物本体                           |
| 4  | Platform      | 单向平台（按下键临时关 mask 穿过） |
| 5  | PlayerHitBox  | 玩家攻击判定（扫 EnemyHurtBox）    |
| 6  | EnemyHurtBox  | 怪物受击判定                       |
| 7  | EnemyHitBox   | 怪物攻击判定（扫 PlayerHurtBox）   |
| 8  | PlayerHurtBox | 玩家受击判定                       |
| 9  | MagicWeapon   | 法宝攻击判定                       |
| 10 | Trap          | 陷阱与陷阱检测                     |
| 11 | Item          | 掉落物拾取检测                     |
| 12 | Exit          | 关卡出口                           |
| 13 | Detector      | AI 索敌/射线检测                   |

- 命中/伤害结算流程见 `GameScripts/AGENTS.md`；AI 参数、数值全部读 `MonsterConfig` / `HeroConfig`（红线 5）。
- 跨场景树穿透禁止（红线 8）：不写 `GetParent().GetParent()`，模块间用事件或显式注入。
