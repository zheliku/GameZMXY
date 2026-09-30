# GameScripts/ 目录规范（游戏代码架构）

> 裁决顺序见根规范 §2。

| 目录 | 职责 | 依赖约束 |
| --- | --- | --- |
| `Battle/` | 伤害公式、属性快照、Buff 结算（按需创建，M4） | 值类型可引 Godot（2026-09-30 裁决），禁运行时引擎状态；可单测 |
| `Entity/` | 实体 Logic（Heroes/ Monsters/ Bullets/ Items/）；`Entity/AI/` 怪物 AI 状态机与技能书 | 见 `Entity/AGENTS.md`；`AI/` 纯 C#、可单测 |
| `UI/` | 界面 Logic（`UIs/*.tscn` 对应的 `*.Logic.cs`，按需） | 跨模块只走事件（根规范 §9） |
| `Event/` | 自定义 `GameEventArgs`（`Create()` + `Clear()`，按需） | 参数走 `ReferencePool`，用完即回收 |
| `Archive/` | `GameCatalogue` / `GameData` 存档数据类 | 只放可序列化字段；已发布字段禁改 |
| `Manager/` | 玩法管理器（`SingletonNode<T>`，按需） | 禁 Autoload / 可变静态全局（红线 2） |
| `Config/` | Luban 表强类型包装 | 只读；含生成物 `ExternalTypeUtil.cs`（禁手改）；业务从这里拿数值 |
| `GameProto/` | 【生成目录】禁手改 | 见 `GameProto/AGENTS.md` |

依赖方向：`Entity / UI / Manager → Config / Battle / Event`；`Battle` 不依赖任何 Godot/游戏节点；`GameProto` 只被读取。

## 战斗结算（Battle/ + Entity/ 协作，M4 目标形态）

- 攻击数据包为纯 C# 强类型（放 `Battle/`）：`AttackData { int Power; DamageKind Kind; Vector2 Knockback; int WsGain; BuffSpec[] OnHitBuffs; RollRange Crit; }`。
- HitBox/HurtBox 是 `Area2D`，命中走引擎 `area_entered`；**判定帧开关由 AnimationPlayer 轨道驱动**，不每帧轮询。
- 命中回调不写公式：双方属性快照 + `AttackData` 交 `Battle/DamageCalculator` 结算，再 `GF.Event.Fire(DamageDealtEventArgs)`，飘字 UI 订阅。
- 攻击者识别：HitBox 挂宿主显式引用（生成时注入）或 `IAttacker` 接口，禁止爬父。
- 伤害类型 `Physics / Magic / Real`（Real 不吃防御）；减伤曲线 `x/(x+K)`，K 与命中/暴击基数进 `BattleConfig`，人怪不一致处在 `DamageCalculator` 注释说明。
- 随机数在 Godot 层取好当参数传入；`Battle/` 不自己取随机（保持结算确定、可精确单测）。

## 通用

- 命名/async/日志按根规范 §5；数值全部来自 `Config/` 或 Luban 表（根规范 §4.5）；高频生灭对象走池（红线 6）。
- 新增 .cs 跑 `--build-solutions`（根规范 §1），`dotnet build` 通过才算交付。
