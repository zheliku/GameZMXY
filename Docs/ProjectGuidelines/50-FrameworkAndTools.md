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
- 关卡纯回归：`dotnet test Tests/LevelTests`；真实流程：`-- --smoketest=level`，在途取消：`-- --smoketest=level-cancel`。编辑器装配与绘制：`EditorScripts/validate_level_scene.gd`（`--editor` 检查 Tool，`-- --snapshot --resize` 检查实际像素与宽屏取景）；路径与 UID：`python Tools/ProjectMaintenance/validate_game_references.py`。
- 保留的可选区域触发器通过 `--script res://EditorScripts/validate_level_triggers.gd` 验证真实物理重叠、当前区监听、玩家过滤、单次激活及停止/重启；独立验证不改正式场景和源表。

### 本轮边界登记（2026-10-06）

- 用户要求改关卡源表并整理业务脚本目录；本轮生成代码/二进制仅由既有 Luban 流程更新，删除已移除 schema 类型遗留的 UID 元数据。未手改生成 C# 或二进制。
- 本轮只迁移手写 C# 及其 UID，并修复业务场景/资源引用，再运行 Godot 重扫与装配验证；保留素材和 bundle 根目录。详细结果见 [关卡重构记录](../Reviews/level_engineering_2026-10-06.md)，不以该记录替代规范。
- 用户已明确授权本轮插件例外：仅在 `addons/TopMenu/GameFrameworkTopMenu.Generate.cs` 的 Collection Res 过滤链增加一行排除 Markdown，防止导航文档 `AGENTS.md` 被误当作同名资源；资源常量由原生成器重生成。`Framework/` 和 `Tools/Luban/` 未修改；其他插件仍遵守只读边界。编辑器构建日志权限和依赖退出异常按验证限制记录，不在业务目录绕过。

## Git

- `main` 受保护；工作分支使用 `feature/xxx`、`fix/xxx` 或 `art/xxx`。提交前缀用 `feat:`、`fix:`、`refactor:`、`art:`、`config:`、`docs:`、`chore:`。
- 素材迁移与逻辑改动分开提交；不提交引擎/构建产物、`user://` 数据和本机编辑器配置。`.gitignore` 保持覆盖 `.godot/`、`bin/`、`obj/`、`*.tmp`、`.vs/`、`*.user`。

## AI 与编辑器配置

`Godot/CLAUDE.md`、`.claude/`、`.kilo/` 中的配置用于其对应工具，不构成本项目规范来源。与 `Docs/ProjectGuidelines/` 冲突时，以根入口裁决规则为准。
