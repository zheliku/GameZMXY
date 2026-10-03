# M5 架构审查

日期：2026-10-04
基准：`f9f2daf`（M5），目标工作树：`main` 上反转 M6 后的未提交状态。

## 结论

当前候选可支撑一个英雄、近战怪和战斗验证，主观工程评级约 **6.8/10**。分项等权取均值后四舍五入，仅用于安排审查优先级，不是与外部项目的客观量化比较。伤害计算与英雄身体状态机边界较清楚；怪物 AI 帧交错、攻击资源支持范围和启动流程测试仍需补强。

| 领域 | 评分 | 依据 |
| --- | ---: | --- |
| 战斗结算 | 7.5/10 | `Battle/` 为确定性逻辑并有手算测试；随机性留在宿主。 |
| 英雄身体与动画 | 7/10 | 物理帧单入口，动画只保留表现数据；动作和输入已拆为可测状态。 |
| 怪物 AI | 7/10 | 固定状态图减少了预置组装层；保留旧项目使用的优先招/远程范围/逐招冷却能力，AI 计时统一在框架帧，身体仍按物理帧推进。 |
| 配置边界 | 6/10 | 已移除未接入 M5 调试流程的关卡配置三表和失效 UI 表单路径；保留有旧玩法依据的远程/优先招、逐招冷却与无双值上限。英雄成长和若干回复/经验字段仍无消费者。 |
| 流程与生命周期 | 6.5/10 | M5 调试流程仍承担实体生成，但实体/场景已有成对清理和异步代次保护；预加载失败会停止进入游戏。流程回归测试仍缺失。 |
| 测试与移植 | 7/10 | 有 73 项纯 C# 测试及真实 Godot AI 冒烟；GodotSharp 已改为 NuGet 引用，仍缺真实宿主帧交错测试。 |

## 外部参照

- [Godot 官方 2D Platformer 示例](https://github.com/godotengine/godot-demo-projects/blob/master/2d/platformer/player/player.gd)：单个 `CharacterBody2D` 直接按物理帧处理输入、速度、移动和动画选择。这个规模的示例支持保持玩家宿主直接；本项目的连击、打断和受击时序足以支持身体 FSM，但不需要再用动画树承担玩法逻辑。
- [OpenRA Actor.cs](https://github.com/OpenRA/OpenRA/blob/bleed/OpenRA.Game/Actor.cs)：成熟 RTS 以 trait 组合 actor 能力，并缓存能力视图，适合单位行为数量巨大且可由模组组合的项目。GameZMXY 当前只有一个怪物种类，不应照搬这套组合规模。
- 旧项目 `ZMXY_BHYH/Script/Base/BaseMonster.gd` 及 `Monster_2/10/12.gd` 仅作玩法参照：除基础发现目标、追击、按欲望普攻外，还存在追击途中出招、技能独立冷却和飞行状态下的远程/突进招，因此保留逐招权重、优先级、范围和冷却字段。`BaseRoleProperies.gd` 将无双值限制在 0..100；故保留 `BattleConfig.WsMax=100` 并封顶 `HeroEntity.WsValue`。旧脚本结构本身不是新架构的依据。

## 主要问题

1. **AI/身体帧交错缺少宿主级测试。** AI FSM、逐招冷却和目标丢失计时都按框架帧推进；身体状态和移动按 Godot 物理帧推进，符合实体规范的分工。现有烟测验证典型时序，但没有在不同渲染/物理步长下检查意图消费延迟。
2. **攻击距离推导的数据约束要明确。** `AttackReachReader` 只读取第一个直接碰撞形状，并用 `Shape2D.GetRect()` 取边界矩形。AI 烟测已从真实猴子场景/动画读取并断言矩形范围；多形状、额外变换和非矩形区域目前没有覆盖，支持它们前需补资源集成测试或明确限制。
3. **成长配置仍未落地到玩法。** `HeroLevelConfig` 没有运行时代码读取；`GameData` 与 `GameCatalogue` 只有占位字段，M5 流程也没有存档消费者。Hero/Monster 的回复与经验字段也有未使用项，应等这些玩法进入实际阶段再定去留。
4. **测试集成覆盖仍不足。** 运行时冒烟验证猴子状态图和当前攻击动画资源解析，但没有真实宿主帧交错测试，也没有通用攻击资源解析单测。
5. **热更新流程类过大。** `ProcedureUpdate.cs` 约 915 行，同时处理路径选择、HTTP 重试、完整性校验、下载、回滚、清单和状态切换。它属于主包 M0 启动链路，当前没有行为回归测试；应先为纯决策/校验逻辑补测试，再只抽有独立职责的部分，避免把流程切成转发包装。

## 已落实

- 回到 `main`，反转 M6 提交；没有创建提交。`ProcedureBattle`、`LevelDirector`、M6 界面和 M6 资产均不再进入 M5 工作树。
- 按 Luban 源表管线移除未接入 M5 流程的 Level/Wave/Spawn 配置三表及生成物；M2 bootstrap 不再重建它们或指向已删除场景的 UI 表单。
- 实体显示时由 `ActorEntity` 统一清除死亡事实；隐藏时只经一个入口释放攻击包。
- AI 只有存在可用招式或随机区间非零宽度时才消耗随机数。
- 移除只转发一次的 `SoundConfigQuery` 和当前无调用点的事件复制重载。
- 移除 `ProcedureLaunch`、`ProcedurePrelode` 的静态流程结果状态；资源组初始化失败不再继续进入游戏。M5 暂不无条件加载无消费者的存档。
- `NodePool` 的容器、场景和节点索引改为池实例状态；调用点统一经 `NodePool.Instance`。
- `LoadingForm` 的进度不再每次从零开始，活动表单由 UI 管理器按资源路径查询。
- `ProcedureGame` 保留 M5 调试场地；实体结果按进入代次保存在局部变量，只由场景所有者释放资源。
- 测试项目不再依赖个人引擎安装目录，使用 GodotSharp 4.7.2 NuGet 包。
- 怪物 AI 固定为 Pause/Wander/WalkToTarget/StandAndStrike/PaceBelowTarget/CcLocked/Death 状态图；删除原型、角色槽、额外状态注册、Sentry 以及两个无消费者状态。
- 保留 `AttackConfig` 优先级、远程范围和逐招冷却：旧项目 `Monster_10`、`Monster_12` 有追击中释放和独立冷却的特殊招式，这些字段服务于已知玩法，不是移植旧架构。
- 保留有旧项目依据的 `WsMax=100`，在 `HeroEntity` 中把攻击收益限制在 `0..WsMax`。

## 待裁决

- 补充不同渲染/物理步长下的宿主集成测试，验证 AI 意图、冷却和目标丢失时序。
- 是否从 M5 数据设计中移除未使用的 `HeroLevelConfig`、Archive 占位字段和未消费战斗属性，并由人类更新根规范的 M2 表清单。
- 是否保留基于动画判定盒的攻击范围推导；若保留，应把支持的形状、层级和变换限制写入资源校验及测试。

本次环境可读取 Godot 官方示例和 OpenRA 源码；tModLoader 示例读取未成功，因此不据此作架构结论。

验证：`dotnet test Tests/BattleTests` 通过 73 项；`dotnet build` 通过。Godot `--smoketest=ai --quit-after 2400` 输出 `SMOKE PASS`，完整覆盖猴子 AI 运行时序列；本机 `user://` 日志/设置目录不可写，退出仍出现 GGF `DefaultWebRequestAgentHelper.Reset()` 已释放对象异常，未修改框架处理。NuGet 漏洞索引不可达产生 NU1900 警告。外部参照限于 Godot 官方示例和 OpenRA；tModLoader 示例读取未成功，因此不据此作架构结论。
