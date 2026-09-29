# MainPack/ 目录规范（主包）

> 裁决顺序见根规范 §2。主包随应用打包、不参与热更，多数代码来自框架示例。

- `Scripts/Procedure/` —— `ProcedureLaunch / Update / Prelode / Game`（文档 `docs/ProcedureSystem.md`）；流程是入口链路，职责单一，不写玩法。
- `Scripts/Resources/*.cs` —— `EntityGroupRes / UIGroupRes / SoundGroupRes / UpdateSettingRes / ArchiveSetting / NodePoolConfig / ScriptGenerateRes / EntityGroup / PoolEntry` 等；实例在 `Resources/*.tres`，仅前四个由 `GameFramework.tscn` 注入，其余经 `ResourcesCollectionConstant` 常量加载。
- `Scripts/ObjectPool/` —— `NodePool`；`Scripts/UI/` —— 共享 UI Logic（LoadingForm / QuestionTips）；`Scripts/Debug/` —— `SmokeTestDriver`（`--smoketest`，平时零开销）；`Fonts/ Themes/ UI/`。

## 使用边界

- `ScriptGenerateRes.cs` 的 4 个 Ge/Logic 输出路径是框架硬编码链路（根规范 §3.2），不改。
- 四个注入 `.tres` 是框架启动依赖，改字段后必须在编辑器验证 `GameFramework.tscn` 跑通。
- 共享 UI 放这里（场景 `MainPack/UI/`）；游戏专属界面放 `TheGame/UIs/` + `GameScripts/UI/`，不混。
