# Level：按功能阅读

规范依据：[00 架构](../../../../../Docs/ProjectGuidelines/00-Architecture.md)的“模块入口与目录”、[10 C#](../../../../../Docs/ProjectGuidelines/10-CSharp.md)的功能分组规则。

目录按职责组织。每个功能的节点、纯规则与资源放在一起，类型后缀区分角色；命名空间统一为 `GameLogic.Level`。

| 位置 | 职责 | 主要类型 |
| --- | --- | --- |
| 根目录 | 关卡唯一入口；装配、阶段推进、收益、结局与清理 | `LevelModule` |
| `LevelModule.Progress.cs` | 进关快照、击败经验与升级、提交检查点、放弃回滚 | 同一 `LevelModule` 的 partial |
| `LevelModule.Loot.cs` | 本关飞行球的生成、接触入账、回池与退出前等待 | 同一 `LevelModule` 的 partial |
| `Camera/` | 实际取景、跟随与抵达；纯相机计算 | `LevelCameraNode`、`LevelCameraHelper` |
| `Spawning/` | 生成点索引、配方时间和名额、实体显示与回收 | `LevelSpawnPointsNode`、`LevelSpawnSchedule`、`LevelSpawnerNode` |
| `Stages/` | 阶段状态、战斗区域和门、可选物理触发器 | `LevelStageSequence`、`LevelGatesNode`、`LevelTriggersNode` |
| `Markers/` | 生成点与阶段区域共用的空间条目及编辑器绘制 | `LevelMarkerResource`、`LevelMarkersNode` |
| `Exit/` | 清场后显示出口，近处落地按 W 请求通关 | `LevelExitNode` |

推荐先读 `LevelModule.Initialize → StartSession → BeginTravel → StartBattle → Cleanup`，再按要改的功能进入子目录。收益沿 `OnMonsterDefeated → AwardExperience` 阅读：经验在击败时立即结算，飞行球只承载灵魂、魔法和无双。通关沿 `LevelExitNode.Entered → LevelModule.OnExitEntered → SubmitOutcome` 阅读，文件读写交给 `SaveModule`，流程负责回地图。

后续新增功能也按职责落位。只有具备独立职责、实际需要的功能才建目录；入口 partial 保持同一对象的状态与生命周期。完整调用图见 [项目导览](../../../../../Docs/ProjectTour.md)。
