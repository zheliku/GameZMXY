# Godot/ 目录规范

> 裁决顺序与阅读规则见根规范 §2；本文件只写本目录特有约定。

- `GodotProject/Framework/` —— GGF 框架：`GameFramework/`（纯 C# 层）+ `GodotGameFrameworkCore/`（桥接层）。
- `GodotProject/addons/` —— 框架自带编辑器插件（TopMenu 等）。
- `docs/` —— 框架系统文档（`<模块>System.md`），查 API 先查这里；完整索引 `docs/README.md`；断言以代码为准，缺失/不符时报告，不猜 API。
- `CLAUDE.md` / `.claude/` / `.mcp.json` —— 框架作者的 AI 约定与工具配置，仅参考，冲突以根规范为准。
- `exported_bundles/` —— bundle 导出产物（构建产物，不提交，根规范 §12）。
- `production/` —— 工作流状态文件（stage/review-mode/session-logs），非规范依据。

## 只读规则

- `Framework/` 与 `addons/` 原则上只读；确需修改走根规范 §13 例外流程，禁止直接改。
- `GameFramework/` 额外受红线 1 约束：禁止引用 `Godot.*`。

## 验证

- C# 改动后 `dotnet build`（工作目录 `Godot/GodotProject`）；新增 .cs 另跑 `--build-solutions`（根规范 §1）。
- `project.godot` 的框架注入配置（`GameFramework.tscn` 注入 EntityGroup/UIGroup/SoundGroup/UpdateSetting 四个 `.tres`）是启动依赖，改动须人类确认并在编辑器验证。
