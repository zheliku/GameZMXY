# 50 框架与工具

## GGF API 路由

从 `Framework/GodotGameFrameworkCore/Base/GF.cs` 的 GF 门面访问；先查 `Godot/docs/`，再核对实现。框架资料不覆盖项目规范，文档与代码差异须报告。

| 门面 | 框架资料（Godot/docs） |
| --- | --- |
| GF.Entity | EntitySystem.md |
| GF.UI | UISystem.md |
| GF.Event | EventSystem.md |
| GF.Fsm | FsmSystem.md |
| GF.Sound | SoundSystem.md |
| GF.Resource | ResourceSystem.md |
| GF.Scene | SceneSystem.md |
| GF.ObjectPool | ObjectPoolSystem.md |
| GF.Archive | ArchiveSystem.md |

## 生成与验证

- Godot 4.7 .NET / SDK 4.7.2，C# net10.0，Newtonsoft.Json 13.0.4；gl_compatibility、内置 2D 物理、940×590、canvas_items + keep。
- 源表改动经 `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` 导出，之后构建。Tools/Luban 为只读 vendored 工具。
- UI 绑定经 `Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace GameLogic.UI` 生成；资源常量经既有 Collection Res 生成器更新。不得手改生成物。
- 旧资源迁入工具位于 Tools/LegacyMigration，来源登记在 LegacyAssetMap；已有人工调整的动画不能无差别重生成。
- 新增 C# 后用 Godot `--headless --build-solutions --quit --no-window` 完成扫描；场景/动画改动按风险验证装配、物理和实际渲染，不以构建代替画面检查。
- 单测覆盖纯规则，引擎烟测覆盖真实输入、UI、物理和流程。测试用隔离的 `user://Validation/<模式>/<唯一编号>`，不修改玩家正式存档、不新增生产测试 API。
- 以 PASS/FAIL 判断断言，同时检查进程退出码和错误；预期失败注入与框架关停问题分别记录，不能用 PASS 掩盖异常。
- 可执行命令与当前结果只维护在 [Validation](../Validation.md)，不再复制到每份功能说明。

## 用户授权的只读边界例外

下表保留既有授权，不扩大到其他 Framework、addons、Tools/Luban 或旧工程文件。今后超出此范围仍须用户明确要求。

| 文件 / 范围 | 已授权修改 |
| --- | --- |
| addons/TopMenu/GameFrameworkTopMenu.Generate.cs | Collection Res 过滤链排除 Markdown，防止 AGENTS.md 被作为同名资源收集；仅此一行过滤 |
| addons/ExportInspector/ExportInspector.cs | 2026-10-15 用户授权扫描修补：先读脚本依赖元数据，只加载候选包标记并校验；条目扫描和打包复用已识别标记，避免提前加载普通资源触发编辑器 GCHandle 异常；仅此扫描与排除逻辑 |
| Framework/GodotGameFrameworkCore/Base/BaseComponent.cs | 2026-10-31 用户要求修复框架关停异常：自动关停从 OnPreDestroy 提前至 OnExitTree，并调用既有 GameEntry.Shutdown(None) 清理模块及组件注册；避免在 HttpRequest/声音辅助节点销毁后清理代理，不改其他生命周期 |
| Framework/GameFramework.tscn 的 Procedure.Procedures | Launch、Update、Prepare、Preload、Title、LoadGame、Character、Map、Level；2026-10-15 用户要求整理准备流程并重命名读档流程，登记 Prepare 与 LoadGame；EnterProcedure 不变；新增其他流程另行授权 |
| Framework/GodotGameFrameworkCore/UI/UIExtension.cs、Entity/EntityExtension.cs | 2026-11-07 用户要求全项目枚举类型统一E前缀：仅同步6处项目配置枚举参数类型，UIFormId→EUIFormId、EntityId→EEntityId；方法、配置字段、框架枚举及行为保持 |
| Framework/GodotGameFrameworkCore/Json/EasySave.cs | 增加 Serialize、WriteUserTextAtomicAsync、LoadFromUserWithBackupAsync、ExistsInUserOrBackup、DeleteInUserWithBackupAsync、BackupSuffix；原接口及热更调用不变 |
| Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs | 原子写/.bak、调用顺序串行、调用时序列化、读取备份回退、严格首次建档、覆盖时重写目录、读写/删除返回 Task<bool>；ArchiveCatalogue/ArchiveData、布局、加密和 GF 泛型绑定不变 |
| 生成代码与资源 | 用户要求改源表、UI 或资源登记时，只经上述既有生成流程更新；脚本迁移携带 UID，并修复引用后由 Godot 重扫 |

过去的开发存档清理授权只对应已核对的指定旧档，不代表可随意删除后续存档。格式以 GameData.CurrentVersion 为准；字段与存档契约见第 70 章。

## Git 与工具配置

- main 受保护；分支用 feature/、fix/、art/，提交前缀 feat:、fix:、refactor:、art:、config:、docs:、chore:。
- 素材与逻辑改动分开提交；不提交 .godot、bin、obj、user://、临时文件和本机编辑器配置。保留用户已有工作及 Git 索引。
- Godot/CLAUDE.md、.claude、.kilo 是各工具配置，不是项目规范。

## Stop That Shit

项目按用户要求采用 [Stop That Shit](https://github.com/lennney/stop-that-shit/tree/0.2.5) 的工程决策规则，入口见本目录 [AGENTS.md](AGENTS.md#ai-工作方式)。固定版本为 `0.2.5`，不自动跟随 main；不把插件复制进游戏、框架或 Godot addons。

Codex CLI 的原生安装方式：

```powershell
codex plugin marketplace add lennney/stop-that-shit --ref 0.2.5
codex plugin add stop-that-shit@stop-that-shit
```

安装后启动新的 Codex 会话，插件的 Skill 才能由宿主重新发现。Guard 执行拦截还要求在新的 CLI TUI 输入 `/hooks`，由用户对照安装版本的 `hooks/codex-hooks.json` 审阅并信任命令；不得自行写入信任或绕过此步骤。该版本绑定 UserPromptSubmit、PreToolUse、PostToolUse、SessionEnd，命令均调用包内 `hooks/stop-that-shit.cjs`。IDE 插件支持以当前 [OpenAI Docs](https://developers.openai.com/codex/plugins/) 为准；不能将 CLI 安装成功等同于所有宿主都已加载 Hook。

需 Guard 设置明确任务模式时，用户在消息首个非空行使用：

```text
$stop-that-shit change -- 完成当前授权修改及必要验证。
$stop-that-shit review -- 检查当前 diff，只报告问题。
```

普通项目规范立即适用；Guard 初始为观察模式，只有宿主加载、信任及显式指令到位后才可报告其实际拦截结果。不得伪造用户指令、文件锁或代理预算，不把 Skill 的建议描述成已执行的隔离保证。安装及更新流程遵循对应版本的 [INSTALL_FOR_AGENTS.md](https://github.com/lennney/stop-that-shit/blob/0.2.5/INSTALL_FOR_AGENTS.md)。
