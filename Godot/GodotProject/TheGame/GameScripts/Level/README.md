# Level：按功能阅读

按 [10 C#](../../../../../Docs/ProjectGuidelines/10-CSharp.md) 的功能分组规则组织；当前架构、初始化与旧规范差异见 [项目导览](../../../../../Docs/ProjectTour.md)。

目录按职责组织。每个功能的节点、纯规则与资源放在一起，类型后缀区分角色；命名空间统一为 `GameLogic.Level`。

| 位置 | 职责 | 主要类型 |
| --- | --- | --- |
| [LevelModel](LevelModel.cs) | 阶段、计时、统计、结局、成绩与进关回滚副本 | 本局状态所有者 |
| [LevelSystem](LevelSystem.cs) | 阶段迁移、奖励、装备交易、检查点与回滚 | 业务系统，不持有节点 |
| [LevelViewController](LevelViewController.cs) | 场景装配、节点事件、表现与清理 | 场景控制器 |
| [LevelViewController.Loot](LevelViewController.Loot.cs) | 掉落节点生成、接触事实、回池与出口等待 | 同一控制器的表现职责 |
| `Camera/` | 实际取景、跟随与抵达；通用计算见 `Utility/SpatialUtility` | `LevelCameraNode` |
| `Spawning/` | 生成点索引、配方时间和名额、实体显示与回收 | `LevelSpawnPointsNode`、`LevelSpawnSchedule`、`LevelSpawnerNode` |
| `Stages/` | 阶段状态、战斗区域和门、可选物理触发器 | `LevelStageSequence`、`LevelGatesNode`、`LevelTriggersNode` |
| `Markers/` | 生成点与阶段区域共用的空间条目及编辑器绘制 | `LevelMarkerResource`、`LevelMarkersNode` |
| `Exit/` | 清场后显示出口，近处落地按 W 请求通关 | `LevelExitNode` |

从 `ProcedureLevel` 阅读独立注册的 Model/System，再看 `LevelViewController.Initialize → StartSession → Cleanup`。控制器收到击败事实后调用 `LevelSystem.RecordDefeat` 发放经验；灵魂在球实际接触时入账，装备满包时留在地面。出口沿 `LevelExitNode.Entered(hero) → LevelViewController.OnExitContact → OnExitEntered → LevelSystem.Complete` 阅读：先校验实际接触者，再等待飞行掉落，写盘交给 `SaveSystem`，流程等待结果后导航。

地面物品、出口和可选触发区通过自己的碰撞区域识别英雄，不注入或缓存玩家节点；控制器核对会话身份后才接受接触。触发区接受后先 EndWatching，再推进战斗，回调中开启新监听不会被旧接触清除。

`World/FloatingText` 绑定共用的 [FloatingTextPresenter](../FloatingText/FloatingTextPresenter.cs)，由控制器开始和结束会话；伤害/闪避走 GGF 事件，恢复品与蓝球领取后提交恢复提示。Presenter 负责拥有和归还飘字池实例。

后续功能按职责落位。System 通过 Architecture 读取模型，Controller 拥有节点；实体、窗口和池节点保留各自生命周期，不把场景引用放回业务系统。
