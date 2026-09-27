# Entity/ 目录规范（实体与状态机）

> 根规范：仓库根 `AGENTS.md`（先读）；TheGame 规范：`../../AGENTS.md`；GameScripts 规范：`../AGENTS.md`（战斗结算规则在这里）。冲突时：根规范 > TheGame > GameScripts > 本文件。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 继承树（不要新造中间基类）

实体直接继承 Godot 原生类型 + `IEntity`，无中间框架基类：

```
ActorEntity : CharacterBody2D, IEntity        # 阵营/血量/属性快照/受击入口
  ├── HeroEntity → WukongEntity ...
  └── MonsterEntity → <各怪>
BulletEntity : Node2D, IEntity
DropItemEntity / MagicWeaponEntity ...
```

- 新实体先看现有类（`ActorEntity.cs` / `HeroEntity.cs`）再动手，公共能力下沉到父类，不复制粘贴。

## 生命周期与生成回收

- 只实现框架接口：`OnInit / OnShow / OnUpdate / OnHide / OnRecycle`。
- 生成/回收走 `GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity` / `HideEntitySafe`；`EntityId` 来自 Luban 生成枚举，禁止魔法数字。
- **不要手写脚本骨架**：场景搭好后选中根节点 → Inspector「Generate Script」生成 Ge/Logic 双半类（`Xxx.cs` 生成 + `Xxx.Logic.cs` 手写），生成物放 `GameProto/`。
- 子弹/掉落物/特效等高频生灭对象必须走池（红线 6），禁止裸 `Instantiate + QueueFree`。

## 状态机（GF.Fsm）

- 每状态一个类，单独文件，放 `Entity/.../States/`；基类如 `HeroFsmStateBase`。
- 英雄标准状态：`Idle / Walk / Run / Jump / Fall / Attack1..n / Skill_<SkillId> / Hurt / Stun / Death`。
- 怪物标准状态：`Idle / Patrol / Chase / Attack / CcLocked / Death`；Boss 阶段转换也是**状态**，不写巨型 if 链。
- 状态创建：`GF.Fsm.CreateFsm(this, states...)`（见 `HeroEntity.cs`），销毁配对 `DestroyFsm`。
- 状态内等待动画用 `await ToSignal(m_Anim, AnimationPlayer.SignalName.AnimationFinished)`，不用裸 `await` 轮询。
- 打断规则集中成一张表（哪些状态可被 Hurt/Stun/Skill 打断），禁止散落赋值。
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
