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
| Framework/GameFramework.tscn 的 Procedure.Procedures | Launch、Update、Prepare、Preload、Title、LoadGame、Character、Map、Level；2026-10-15 用户要求整理准备流程并重命名读档流程，登记 Prepare 与 LoadGame；EnterProcedure 不变；新增其他流程另行授权 |
| Framework/GodotGameFrameworkCore/Json/EasySave.cs | 增加 Serialize、WriteUserTextAtomicAsync、LoadFromUserWithBackupAsync、ExistsInUserOrBackup、DeleteInUserWithBackupAsync、BackupSuffix；原接口及热更调用不变 |
| Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs | 原子写/.bak、调用顺序串行、调用时序列化、读取备份回退、严格首次建档、覆盖时重写目录、读写/删除返回 Task<bool>；ArchiveCatalogue/ArchiveData、布局、加密和 GF 泛型绑定不变 |
| 生成代码与资源 | 用户要求改源表、UI 或资源登记时，只经上述既有生成流程更新；脚本迁移携带 UID，并修复引用后由 Godot 重扫 |

过去的开发存档清理授权只对应已核对的指定旧档，不代表可随意删除后续存档。当前只支持 v4；字段和存档契约见第 70 章。

## Git 与工具配置

- main 受保护；分支用 feature/、fix/、art/，提交前缀 feat:、fix:、refactor:、art:、config:、docs:、chore:。
- 素材与逻辑改动分开提交；不提交 .godot、bin、obj、user://、临时文件和本机编辑器配置。保留用户已有工作及 Git 索引。
- Godot/CLAUDE.md、.claude、.kilo 是各工具配置，不是项目规范。
