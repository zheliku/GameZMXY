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

- 技术栈：Godot 4.7 .NET / SDK 4.7.2、C# `net10.0`、Newtonsoft.Json NuGet 13.0.4；渲染器 `gl_compatibility`、内置 2D 物理、视口 940×590、`canvas_items` 拉伸。
- 默认 Godot 命令使用 `S:\Godot4\Godot4CSharp.exe`；headless 使用 `S:\Godot4\Godot4CSharp_console.exe`。编辑器命令：`"S:\Godot4\Godot4CSharp.exe" --path Godot/GodotProject --editor`。
- `Tools/Luban/` 为 vendored 工具；通过 `Configs/` 的脚本导表。
- `Tools/LegacyMigration/` 仅用于可追溯的一次性旧资源迁移；迁移结果登记在 `Docs/LegacyAssetMap.md`。
- 日常构建：在 `Godot/GodotProject/` 执行 `dotnet build`。新增 C# 后再运行 `"S:\Godot4\Godot4CSharp_console.exe" --build-solutions --path Godot/GodotProject --no-window -q`。
- 导表命令：`Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`；也可由编辑器 TopMenu → Generate File → GameConfig File 执行。
- 改表时先导表再构建；涉及实体/动画资源时运行 `EditorScripts/validate_anim_libraries.gd` 与 Collection Res 生成器。
- 按改动风险运行相关单测或冒烟场景；验证完成后检查 `git diff` 与 `git diff --check`。

## Git

- `main` 受保护；工作分支使用 `feature/xxx`、`fix/xxx` 或 `art/xxx`。提交前缀用 `feat:`、`fix:`、`refactor:`、`art:`、`config:`、`docs:`、`chore:`。
- 素材迁移与逻辑改动分开提交；不提交引擎/构建产物、`user://` 数据和本机编辑器配置。`.gitignore` 保持覆盖 `.godot/`、`bin/`、`obj/`、`*.tmp`、`.vs/`、`*.user`。

## AI 与编辑器配置

`Godot/CLAUDE.md`、`.claude/`、`.kilo/` 中的配置用于其对应工具，不构成本项目规范来源。与 `Docs/ProjectGuidelines/` 冲突时，以根入口裁决规则为准。
