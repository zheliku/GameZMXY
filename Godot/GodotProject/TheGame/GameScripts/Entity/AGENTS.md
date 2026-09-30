# Entity/ 目录规范（实体与状态机）

> 裁决顺序见根规范 §2；战斗结算见 `../AGENTS.md`。

## 继承树（不新造中间基类）

```
ActorEntity : CharacterBody2D, IEntity        # 共用：血量/受击/朝向翻转/表现层/动画宿主
  ├── HeroEntity → WukongEntity ...           # 英雄机制 + 专属（武器层在 HeroEntity）
  └── MonsterEntity → <各怪>
BulletEntity : Node2D, IEntity   DropItemEntity / MagicWeaponEntity ...
```

- 直接继承 Godot 原生类型 + `IEntity`；新实体先看现有类（`ActorEntity` / `HeroEntity`）再动手，公共能力下沉父类。
- 角色事实（输入/连段…）放最具体的类，基类不预埋用不上的字段。

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
