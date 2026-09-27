# Godot/ 目录规范

> 根规范：仓库根 `AGENTS.md`（先读）。冲突时：根规范 > 本文件 > 推断。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 目录内容

- `GodotProject/Framework/` —— GGF 框架本体：`GameFramework/`（纯 C# 层）+ `GodotGameFrameworkCore/`（Godot 桥接层）。
- `GodotProject/addons/` —— 框架自带编辑器插件（TopMenu / ComponentInsoector 等）。
- `docs/` —— GGF 框架系统文档（`<模块>System.md`），查框架 API 先查这里。
- `CLAUDE.md` —— 框架原作者的 AI 约定，可参考；与根规范冲突时以根规范为准。

## 只读规则

- `Framework/` 与 `addons/` **原则上只读**。确需修改框架时，走根规范 §13 框架例外流程（先确认不是用法问题 → 例外记录 → 最小化改动 + `// [MODIFIED] 原因` 注释），禁止直接改。
- `GameFramework/`（纯 C# 层）额外受根规范红线 1 约束：**禁止引用 `Godot.*`**。

## 文档索引（docs/）

常用：`EntitySystem.md` / `UISystem.md` / `EventSystem.md` / `FsmSystem.md` / `SoundSystem.md` / `ResourceSystem.md` / `SceneSystem.md` / `ObjectPoolSystem.md` / `NodePoolSystem.md` / `SettingSystem.md` / `ArchiveSystem.md` / `ProcedureSystem.md` / `DataTableSystem.md`。

- 文档断言以代码为准（如 `GF.cs`、`EntityExtension.cs`），文档缺失或与实现不符时报告，不猜 API。
- `engine-reference/` 是引擎参考，仅供查阅。

## 框架常用扩展方法位置

- `GodotGameFrameworkCore/Entity/EntityExtension.cs`：`ShowEntity(EntityId)` / `ShowEntityAsync<T>` / `HideEntitySafe`。
- `GodotGameFrameworkCore/UI/UIExtension.cs`：`OpenUIForm(UIFormId)` / `OpenUIFormAsync<T>` / `CloseUIForm` / `HasUIForm`。
- `GodotGameFrameworkCore/Sound/SoundExtension.cs`：`PlayBGM` / `PlaySFX` / `PlayUISound` / `StopBGM` / `SetVolume`。
- `GodotGameFrameworkCore/Base/GF.cs`：全部门面属性（`GF.Entity` / `GF.UI` / `GF.Event` / ...）。

## 本目录修改的验证

- 任何 C# 改动后：`dotnet build`（工作目录 `Godot/GodotProject`）+ 新增 .cs 后跑根规范 §1 的 `--build-solutions` 命令。
- 禁止改 `project.godot` 的框架注入配置（四个 .tres 注入链路），除非人类明确要求并验证。
