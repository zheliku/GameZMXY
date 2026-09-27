# MainPack/ 目录规范（主包）

> 根规范：仓库根 `AGENTS.md`（先读）；TheGame 规范：`../AGENTS.md`。冲突时：根规范 > TheGame > 本文件。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 定位

主包随应用打包、**不参与热更**，是框架自带的引导与共享层。这里的代码多数来自框架示例，属于"框架可动边界"。

## 目录内容

- `Scripts/Procedure/` —— 流程节点：`ProcedureLaunch` / `ProcedureUpdate` / `ProcedurePrelode` / `ProcedureGame`（文档 `docs/ProcedureSystem.md`）。
- `Scripts/Resources/*.cs` —— 框架资源定义类：`EntityGroupRes` / `UIGroupRes` / `SoundGroupRes` / `ArchiveSetting` / `ScriptGenerateRes` 等，实例在 `Resources/*.tres`，由 `GameFramework.tscn` 注入。
- `Scripts/ObjectPool/` —— `NodePool` 等框架通用组件。
- `Scripts/UI/` —— 共享 UI Logic（`LoadingForm` / `QuestionTips`）。
- `Fonts/` / `Themes/` / `UI/` —— 字体 / 主题 / 共享 UI 场景。

## 使用边界

- `ScriptGenerateRes.cs` 内含 4 个 Ge/Logic 输出路径（框架硬编码链路之一，见根规范 §3.2），**不要改**；需要新输出类型先报告人类。
- 流程（Procedure）是游戏入口链路：改流程逻辑时先读 `docs/ProcedureSystem.md`，保持流程节点职责单一，不在流程里写玩法。
- 新增共享 UI（如通用弹窗）放 `Scripts/UI/` + 场景放 `MainPack/UI/`；游戏专属界面放 `TheGame/UIs/` + `GameScripts/UI/`，不要混。
- 四个 `.tres` 注入链路（EntityGroup/UIGroup/SoundGroup/ArchiveSetting）是框架启动依赖：改字段或换类型后必须在编辑器里验证 `GameFramework.tscn` 能跑通。
