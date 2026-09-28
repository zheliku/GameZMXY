# GameScripts/ 目录规范（游戏代码架构）

> 根规范：仓库根 `AGENTS.md`（先读）；TheGame 规范：`../AGENTS.md`。冲突时：根规范 > TheGame > 本文件。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 分层与目录职责

| 目录         | 职责                                                                 | 依赖约束                                   |
| ------------ | -------------------------------------------------------------------- | ------------------------------------------ |
| `Battle/`    | 伤害公式、属性快照、Buff 结算、概率判定                               | 【纯 C#，禁引 `Godot.*`】可单测             |
| `Entity/`    | 实体 Logic（Heroes/ Monsters/ Bullets/ Items/）                       | 规范见 `Entity/AGENTS.md`                  |
| `UI/`        | 界面 Logic（`UIs/*.tscn` 对应的 `*.Logic.cs`）                        | 跨模块通信只走事件（根规范 §9）             |
| `Event/`     | 自定义 `GameEventArgs`（`Create()` 工厂 + `Clear()`）                 | 参数走 `ReferencePool`，用完即回收          |
| `Archive/`   | `GameCatalogue` / `GameData` 存档数据类                               | 只放可序列化字段 + `Version`（根规范 §9）   |
| `Manager/`   | 玩法管理器（`SingletonNode<T>`）                                      | 禁止自造 Autoload / 静态全局变量（红线 2）  |
| `Config/`    | Luban 表的强类型包装（读取 `GameProto` 生成代码后的领域对象）         | 只读不写；业务代码从这里拿数值              |
| `GameProto/` | 【生成目录】禁止手改                                                  | 规范见 `GameProto/AGENTS.md`               |

依赖方向：`Entity / UI / Manager → Config / Battle / Event`；`Battle` 不依赖任何 Godot/游戏节点；`GameProto` 只被读取，不反向依赖业务。

## 战斗结算规则（Battle/ + Entity/ 协作）

- 一次攻击的数据包统一为**纯 C# 强类型**（放 `Battle/`）：

  ```csharp
  AttackData { int Power; DamageKind Kind; Vector2 Knockback; int WsGain; BuffSpec[] OnHitBuffs; RollRange Crit; }
  ```

- HitBox / HurtBox 是 `Area2D` 组件节点，命中走引擎 `area_entered`；**判定帧开关由 AnimationPlayer 轨道驱动**（沿用旧项目手调数据），不使用每帧轮询。
- 命中回调里不写公式：把双方属性快照 + `AttackData` 交给 `Battle/DamageCalculator` 结算，再 `GF.Event.Fire(DamageDealtEventArgs)`，飘字 UI 订阅该事件。
- 攻击者识别：HitBox 上挂对宿主的显式引用（生成时注入）或 `IAttacker` 接口，禁止从碰撞对象爬父节点。
- 伤害类型三种：`Physics / Magic / Real`（Real 不吃防御）。减伤曲线沿用旧项目 `x/(x+K)`，但 **K 值、命中/暴击基数进配置表**（`BattleConfig`），不写死；人怪两侧常数不一致的地方在 `DamageCalculator` 注释里显式说明。
- 随机取值直接用 Godot 的 `GD.RandRange` / `RandomNumberGenerator`（不再要求可注入的 `IRandom`）。注意 `Battle/` 是纯 C# 层（红线 1）：那里的函数不自己取随机，暴击/闪避/掉落的随机数由 Godot 层调用方算好当参数传入。

## 通用编码要求

- 命名 / async 时序 / 日志按根规范 §5；数值全部来自 `Config/` 或 Luban 表（红线 5）。
- 高频生灭对象（子弹、飘字、掉落物、特效）必须走 `NodePool` / `GF.ObjectPool`（红线 6）。
- 新增 `.cs` 后跑根规范 §1 的 `--build-solutions` 命令，`dotnet build` 通过才算交付。
