# 模块入口、命名与战斗表现整理

2026-10-12。本文记录本轮实现与验证；唯一规范是 [ProjectGuidelines](ProjectGuidelines/AGENTS.md)，阅读入口是 [GameplayArchitecture](GameplayArchitecture.md)。

## 模块与命名

`ProcedureLevel` 只持有一个 `LevelModule`，原 `LevelController` 与 `LevelRun` 合并为同一对象。`LevelModule.cs` 协调关卡，`LevelModule.Progress.cs` 处理收益、结局、检查点和回滚；partial 共享状态，不产生第二个 owner。出口调用链为 `LevelExitNode.Entered → LevelModule.SubmitOutcome → ProcedureLevel.OnLevelEnded`，移除只转发的 Completed、MonsterDefeated 等中间事件。

标题流程只持有 `SaveModule`。目录刷新在同一类型的 Directory partial 中；`SaveSlotDirectory` 是模块拥有、交给 UI 读取的状态，没有文件读取任务或第二套生命周期。`GameContext` 继续关联本槽档案与 SaveModule。

Level 内部按功能分组，节点和所属规则放在一起，命名空间统一为 `GameLogic.Level`：

```text
Level/
  LevelModule.cs
  LevelModule.Progress.cs
  Camera/
    LevelCameraNode.cs
    LevelCameraHelper.cs
  Spawning/
    LevelSpawnerNode.cs
    LevelSpawnPointsNode.cs
    LevelSpawnSchedule.cs
  Stages/
    LevelStageSequence.cs
    LevelGatesNode.cs
    LevelTriggersNode.cs
  Markers/
    LevelMarkersNode.cs
    LevelMarkerResource.cs
  Exit/
    LevelExitNode.cs
```

Markers 是生成点、门和触发区共用的空间登记，不拥有关卡流程。类型后缀说明用途：入口 Module、内部节点 Node、资源 Resource、无状态纯方法 Helper；实体、窗口和控件保留 Entity/Form/Hud/Bar/Button。拥有状态的阶段序列、生成调度等使用领域名称。

其他名称已同步：DamageHelper、ConfigHelper、ProfileSaveHelper、BodyFsmHelper（C# 14 extension 块）、SoundHelper、HurtBoxNode、MusouAfterimageNode、DamagePopNode。框架和生成器固定名称保留。场景文件 DamagePop.tscn 保留原路径，脚本类型改名不要求场景机械改名。

按用户要求更新第 00/10/20/30/40/50/60/70 章；脚本迁移保留原 UID，场景、纯 C# 测试链接与编辑器验证路径同步。未修改用户已暂存的地形和 Git 索引；本轮无 Framework、addons、Tools/Luban 或旧项目写入。

## 战斗表现与退出

- 守关提示弹窗和入场动画资源已移除。生命条是被动 `EncounterHealthBar`，HUD 负责订阅生命并传入数值；名称在左侧，白字黑边，右侧使用原笔刷条。没有当前/最大生命数字，也没有数值 Label。实际 OpenGL 截图已检查。
- 字体为旧项目 BossBlood 使用的华康海报体 W12，直接复制原字体，来源、SHA256 和迁入工具见 [LegacyAssetMap](LegacyAssetMap.md)。这不是宣称从官方 SWF 提取的字体。
- 小怪受伤后重置为不透明，按两秒淡出并隐藏；继续受伤重新计时，治疗不延长或唤起隐藏条，暂停停止计时，死亡和池复用复位。依据造梦西游一官方 `BaseMonster.showHpSlip` 的 `TweenMax.to(hpSlip, 2, ...)` 与 TweenLite 默认 Quad.easeOut，剩余透明度为 `(1-t/2)²`。
- W 后等待过长来自旧 `BattleConfig.ClearRestartDelay=3`，通关曾复用了重开延迟。该字段从源表删除，通关在下一流程帧等待检查点后立即回地图；死亡仍等待 `DeathRestartDelay=2.5`。真实关卡测得 **49ms**（从按 W 到地图窗口可见，包含检查点写盘），重读磁盘确认 **204 经验**。耗时是本机实测，不保证所有设备相同。
- `World/Exit` 只保留一个出口子场景实例，未重复建立同名占位节点。实际引擎加载与编辑器 Tool 装配均通过。

源表只把已废弃的 ClearRestartDelay 替换为 MonsterHealthBarFadeDuration=2。Luban C# 与 `.bytes` 由 `gen_code_bin_to_project_lazyload.bat` 更新，HUD 绑定由 `regenerate_ui_form.py` 更新，资源常量由原 Collection Res 生成器更新；未手改生成代码。

## 验证记录

命令从仓库根执行；引擎路径为 `S:/Godot4/Godot4CSharp_console.exe`，项目参数为 `--path Godot/GodotProject`。烟测均使用唯一 `user://Validation/<模式>/<随机 ID>` 目录，不触碰玩家存档。

| 命令 / 入口 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误；5 个既有框架过时 API 警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 86/86，通过 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18，通过；覆盖纯相机算法、阶段序列、生成调度 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 33/33，通过；读取最新配置产物 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 197 个资源字符串路径、200 个 UID、23 个暂存地形节点，通过 |
| `--headless --editor --import --build-solutions --quit` | 文件扫描、C# 构建与类型重扫完成；退出异常见下文 |
| `--script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，headless | 场景、唯一出口、Tool 资源、地形、宽屏取景及分数边界断言通过；退出 0 |
| `--headless --editor --script res://EditorScripts/validate_level_scene.gd` | `editor_hint=true` 全部装配断言通过；退出异常见下文 |
| `--headless --script res://EditorScripts/validate_level_triggers.gd` | 初始化停用、当前区监听、玩家过滤、单次激活和停止重启通过；退出 0 |
| `-- --smoketest=level`，OpenGL | 五波、真实移动、守关血条、W 出口、49ms、磁盘重读 204 经验全部通过；退出 0 |
| `-- --smoketest=ui`，OpenGL | HUD 数值/退订、两秒淡出/续伤/暂停/治疗、实体复用、升级、回滚、死亡写盘重开、备份回退及取消失败清理通过；退出 0 |
| `--headless -- --smoketest=frontend` | 六格存档、选角、锁定角色、地图悬停/保存提示、返回回滚、窗口复用及 GGF 声音通过；退出 0 |
| `--headless -- --smoketest=level-cancel` | 在途显示取消、晚到实体回收、不再推进关卡通过；退出 0 |
| `git diff --check` | 通过 |

最终目录移动后重新完成构建、137 项单测、Godot 重扫、场景双模式、触发区和取消检查。完整 level/ui/frontend 业务回归在此之前已通过；目录移动只改变脚本位置与引用，没有改变这些行为。

截图位于忽略目录 `.godot/validation/encounter/gorilla_attack.png`。本轮最终日志位于系统临时目录的 `gamezmxy-module-*.log/.err`，不是项目资源。

## 验证限制与未完成项

- NuGet 的漏洞信息查询因网络不可达产生 NU1900，编译和 137 项测试使用已缓存依赖完成。
- 真实游戏烟测在关停时仍记录既有 `DefaultWebRequestAgentHelper.Reset` 访问已释放节点的 ObjectDisposedException。四项业务烟测的断言通过、进程退出 0，但不称为无错误关停。
- 编辑器重扫和 editor 模式场景断言通过后，既有 `ComponentInsoector._ExitTree` 释放 RefCounted 报错，随后原生终结阶段退出 `-1073741819`。这不是正常编辑器退出；本轮未修改只读插件或框架。
- ui/frontend 的损坏文件、绑定缺失和跨槽拒写日志来自刻意失败注入；正式关卡最终日志没有资源路径缺失。较早一次烟测发现伤害飘字场景路径被误随类型改名，修复后已复跑受影响回归。

请求范围内的业务改动已完成；以上框架/插件退出问题仍保留，登记在 [第 50 章](ProjectGuidelines/50-FrameworkAndTools.md)。
