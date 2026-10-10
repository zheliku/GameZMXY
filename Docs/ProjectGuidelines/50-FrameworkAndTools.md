# 50 框架与工具

## GGF 框架

通过 `Framework/GodotGameFrameworkCore/Base/GF.cs` 的 `GF.*` 门面访问框架模块。常用模块与参考文档：

| 模块 | 门面 | 参考 |
| --- | --- | --- |
| 实体 | `GF.Entity` | `Godot/docs/EntitySystem.md` |
| UI | `GF.UI` | `Godot/docs/UISystem.md` |
| 事件 | `GF.Event` | `Godot/docs/EventSystem.md` |
| 状态机 | `GF.Fsm` | `Godot/docs/FsmSystem.md` |
| 音频 | `GF.Sound` | `Godot/docs/SoundSystem.md` |
| 资源 | `GF.Resource` | `Godot/docs/ResourceSystem.md` |
| 场景 | `GF.Scene` | `Godot/docs/SceneSystem.md` |
| 对象池 | `GF.ObjectPool` | `Godot/docs/ObjectPoolSystem.md` |
| 存档 | `GF.Archive` | `Godot/docs/ArchiveSystem.md` |

文档用于定位框架 API；如文档与实现不一致，以代码为事实并报告差异。不得在 `Framework/` 或 `addons/` 直接修补业务问题。

## 工具与验证

- 技术栈：Godot 4.7 .NET / SDK 4.7.2、C# `net10.0`、Newtonsoft.Json NuGet 13.0.4；渲染器 `gl_compatibility`、内置 2D 物理、视口 940×590、`canvas_items + keep` 拉伸。
- 默认 Godot 命令使用 `S:\Godot4\Godot4CSharp.exe`；headless 使用 `S:\Godot4\Godot4CSharp_console.exe`。编辑器命令：`"S:\Godot4\Godot4CSharp.exe" --path Godot/GodotProject --editor`。
- `Tools/Luban/` 为 vendored 工具；通过 `Configs/` 的脚本导表。
- `Tools/LegacyMigration/` 仅用于可追溯的一次性旧资源迁移；迁移结果登记在 `Docs/LegacyAssetMap.md`。
- 日常构建：在 `Godot/GodotProject/` 执行 `dotnet build`。新增 C# 后再运行 `"S:\Godot4\Godot4CSharp_console.exe" --headless --build-solutions --path Godot/GodotProject --quit --no-window -q`；使用显式 `--quit` 让验证编辑器在构建完成后退出。
- 导表命令：`Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`；也可由编辑器 TopMenu → Generate File → GameConfig File 执行。
- 改表时先导表再构建；涉及实体/动画资源时运行 `EditorScripts/validate_anim_libraries.gd` 与 Collection Res 生成器。
- 按改动风险运行相关单测或冒烟场景；验证完成后检查 `git diff` 与 `git diff --check`。
- 纯 C# 单测：`dotnet test Tests/BattleTests`（伤害公式、属性汇总、生命魔法、无双）、`dotnet test Tests/LevelTests`（阶段与调度）、`dotnet test Tests/ProfileTests`（档案、成长、出战装配、存档迁移与映射、启动校验；读取正式 `.bytes` 产物）。
- 关卡真实流程：`-- --smoketest=level`（四段清波、档案经验、通关检查点），在途取消：`-- --smoketest=level-cancel`。编辑器装配与绘制：`EditorScripts/validate_level_scene.gd`（`--editor` 检查 Tool，`-- --snapshot --resize` 检查实际像素与宽屏取景）；路径与 UID：`python Tools/ProjectMaintenance/validate_game_references.py`。
- UI 与存档回归：`-- --smoketest=ui`，使用真实 GGF 窗口/实体池及流程验证 HUD 聚合刷新与退订、实体与窗口复用、升级注入、关卡事务（中途离开回滚、死亡写盘与自动重开）、主文件损坏回退 `.bak`、v0 旧档迁移、加载取消和失败恢复；全部烟测在 `SmokeTestDriver._Ready` 选择唯一 `user://Validation/<模式>/<随机 ID>` 存档目录，不触碰玩家进度。以 `SMOKE PASS/FAIL` 判定断言，框架退出异常单独登记，不能用通过断言掩盖异常退出。
- 已知框架退出异常（与业务无关，单独登记）：关停时 `DefaultWebRequestAgentHelper.Reset` 访问已释放节点（`ObjectDisposedException`）；`level-cancel`/`ui` 退出时 GC 终结器 `0xC0000005`；`--build-solutions` 退出时 `ComponentInsoector._ExitTree` 释放 RefCounted 报错。
- 保留的可选区域触发器通过 `--script res://EditorScripts/validate_level_triggers.gd` 验证真实物理重叠、当前区监听、玩家过滤、单次激活及停止/重启；独立验证不改正式场景和源表。

### 本轮边界登记（2026-10-06）

- 用户要求改关卡源表并整理业务脚本目录；本轮生成代码/二进制仅由既有 Luban 流程更新，删除已移除 schema 类型遗留的 UID 元数据。未手改生成 C# 或二进制。
- 本轮只迁移手写 C# 及其 UID，并修复业务场景/资源引用，再运行 Godot 重扫与装配验证；保留素材和 bundle 根目录。详细结果见 [关卡重构记录](../Reviews/level_engineering_2026-10-06.md)，不以该记录替代规范。
- 用户已明确授权本轮插件例外：仅在 `addons/TopMenu/GameFrameworkTopMenu.Generate.cs` 的 Collection Res 过滤链增加一行排除 Markdown，防止导航文档 `AGENTS.md` 被误当作同名资源；资源常量由原生成器重生成。`Framework/` 和 `Tools/Luban/` 未修改；其他插件仍遵守只读边界。编辑器构建日志权限和依赖退出异常按验证限制记录，不在业务目录绕过。

### 本轮边界登记（2026-10 架构重构）

用户明确授权以下框架例外（仅此范围，其余 `Framework/`、`addons/`、`Tools/Luban/` 未改）：

- `Framework/GameFramework.tscn`：只改 `Procedure` 节点的 `Procedures` 清单为 Launch、Level、LoadProfile、Preload、Update（`ProcedurePrelode` 改名 `ProcedurePreload`、`ProcedureGame` 改名 `ProcedureLevel`、新增 `ProcedureLoadProfile`）；`EnterProcedure` 不变。以后新增菜单/地图流程须再次授权。
- `Framework/GodotGameFrameworkCore/Json/EasySave.cs`：新增原子写与备份 API（`Serialize`、`WriteUserTextAtomicAsync`、`LoadFromUserWithBackupAsync`、`ExistsInUserOrBackup`、`DeleteInUserWithBackupAsync`、`BackupSuffix`）；原有方法未改，`ProcedureUpdate`/`DownloadComponent` 继续使用原方法。
- `Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs`：所有写入原子化并保留 `.bak`、读写按调用顺序串行、调用时刻序列化、读取回退 `.bak`、首次建档改为"目录与备份都不存在"、覆盖时同步重写目录、`SaveAsync/OverWriteAsync/LoadAsync/Delete` 返回 `Task<bool>`；`ArchiveCatalogue`/`ArchiveData`、文件布局、加密与 `GF.cs` 泛型参数未改。`Godot/docs/ArchiveSystem.md` 已同步。
- 一次性配置迁移脚本 `Tools/ConfigBootstrap/migrate_stat_tables.py` 已执行；生成代码与 `.bytes` 只由导表流程更新。详细记录见 [架构重构记录](../Reviews/architecture_redesign_2026-10-10.md)。

## Git

- `main` 受保护；工作分支使用 `feature/xxx`、`fix/xxx` 或 `art/xxx`。提交前缀用 `feat:`、`fix:`、`refactor:`、`art:`、`config:`、`docs:`、`chore:`。
- 素材迁移与逻辑改动分开提交；不提交引擎/构建产物、`user://` 数据和本机编辑器配置。`.gitignore` 保持覆盖 `.godot/`、`bin/`、`obj/`、`*.tmp`、`.vs/`、`*.user`。

## AI 与编辑器配置

`Godot/CLAUDE.md`、`.claude/`、`.kilo/` 中的配置用于其对应工具，不构成本项目规范来源。与 `Docs/ProjectGuidelines/` 冲突时，以根入口裁决规则为准。
