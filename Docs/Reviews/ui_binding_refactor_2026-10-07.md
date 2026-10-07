# UI 属性绑定整理与验证（2026-10-07）

## 实施结果与规范依据

按用户确认的方案，在既有实体、流程与 GGF UI 上实现属性通知。集中规范 `00-Architecture`、`30-EntitiesAndCombat`、`40-ScenesAndAssets`、`50-FrameworkAndTools`、`60-GameplayModules` 已同步本次约定；命名和注释遵循 `10-CSharp`。GGF API 依据本地 UI、Event、Archive、Procedure 文档并核对实现。

- `Bindable/` 仅保留项目自实现的 `BindableProperty<T>`：构造初值、`Value`、实例事件 `Changed`；默认相等比较，保存后同步通知。删除只读接口、退订句柄、列表扩展、BindingBag、静默写入与静态可变比较器。
- `ActorEntity` 直接持有生命/上限，英雄直接持有等级/累计经验/金币/无双，关卡直接持有前进状态。属性容器随对象存续并可公开修改 `Value`；伤害、死亡、治疗与命中收益仍由实体业务维护。
- 删除 `GameSession`、`PlayerRuntimeState`、`LevelRuntimeState`。`PlayerSaveData` 移入已有 Archive 并携带原 UID，JSON 的 `GameData.Player` 字段及其普通数值名称保持兼容。
- HUD 与血条具名方法对称订阅、退订，打开时统一初始化；血条只绑定两项生命属性。Godot 编辑器序列化补齐根节点五项导出引用和 `node_paths`，原布局和贴图不变。
- 流程直接读档，将普通快照交给英雄复制；回收前捕获保存快照，再次进入先等待上次保存。加载中退出关闭在途 HUD 请求；HUD 打开失败统一清理，并可恢复进入。
- 新增 `--smoketest=ui`，全部烟测使用唯一 `user://Validation/<模式>/<随机 ID>` 存档目录。新脚本 UID 由编辑器扫描生成。

框架、插件、Luban、源表、生成代码/二进制、地形和已有素材未在本轮修改。执行前已经存在的暂存改动保留，未提交或重置 Git 索引。

## 验证命令与结果

命令从仓库根运行。引擎为 `S:/Godot4/Godot4CSharp_console.exe`，日志位于 `Godot/GodotProject/.godot/validation/`，属于忽略的验证产物。

| 命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore --verbosity quiet` | 最终通过，0 警告、0 错误；完整重新编译时有 5 项既有框架弃用警告。 |
| `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore --verbosity quiet` | 79/79 通过；含 5 项新的属性契约测试。移除废弃 RuntimeState/句柄测试，故数量由原 86 项下降。NuGet 漏洞源查询有 NU1900 网络警告。 |
| `dotnet test Tests/LevelTests/LevelTests.csproj --no-restore --verbosity quiet` | 16/16 通过。 |
| `python Tools/ProjectMaintenance/validate_game_references.py` | 89 项序列化路径、132 项 UID 通过，无孤立或重复 UID。 |
| `git diff --check` | 通过；只有 Git 的既有 CRLF 转換提示。 |
| 引擎 `--headless --editor --build-solutions --path Godot/GodotProject --quit --no-window -q` | 编辑器实际编译完成，新脚本 UID 生成；最终运行在插件退出时报释放错误并停留，本任务进程在确认编译完成后结束。前次编辑器构建返回 0。 |
| 引擎 `--headless --path Godot/GodotProject --max-fps 120 -- --smoketest` | 英雄状态/动画/命中断言 `SMOKE PASS`，退出码 0。 |
| 同上，`--smoketest=ai` | 怪物 AI 断言 `SMOKE PASS`，退出码 0。 |
| 同上，`--smoketest=level` | 四段关卡、相机、左右门、抵达延迟及生成总数 `SMOKE PASS`，退出码 0。 |
| 同上，`--smoketest=level-cancel` | 在途取消与晚到怪物清理 `SMOKE PASS`；断言后退出触发既有框架异常，退出码 `0xC0000005`。 |
| 同上，`--smoketest=ui` | 最终全部 UI/存档断言 `SMOKE PASS`；断言后退出触发同一框架异常，退出码 `0xC0000005`。 |

UI 回归读取实际场景控件，覆盖初始显示、生命/无双/等级/Go 更新、残影追平、关闭后不更新、换角色后旧属性不更新、窗口池复用、英雄与怪物池复用、测试场地 null 默认输入、旧 JSON 兼容和实际保存读回。加载取消用例创建缓存内冷窗口，并只在调试夹具中注入正式流程的请求编号，实际关闭由 `ProcedureGame.OnLeave` 执行；失败用例临时清空池中 HUD 导出引用，观察真实打开失败、统一清理及恢复后重入，不改源表或正式场景。

## 已知限制与未完成项

- 编辑器 `addons/ComponentInsoector` 退出时对 `RefCounted` 调用 `Free`，出现 `Can't free a RefCounted object` 和不存在 `free` 的错误；不属于本轮业务修改，插件保持只读。
- 引擎关停已有 `Framework/GodotGameFrameworkCore/WebRequest/DefaultWebRequestAgentHelper.cs:90` 的 `Reset()` 访问已释放对象问题，UI 和取消回归均在通过断言后观察到。测试通过不代表退出阶段无错误；本轮未修改框架。
- Archive 的覆盖 API 不返回磁盘保存成功标志，继续由框架日志报告，业务只等待调用完成。读档损坏处理沿用框架语义，本轮不扩展存档格式、迁移或框架读写契约。
- MP、经验条、升级、奖励、技能和完整存档玩法按计划继续待设计；本轮授权的属性绑定与最小存档接入已完成。
