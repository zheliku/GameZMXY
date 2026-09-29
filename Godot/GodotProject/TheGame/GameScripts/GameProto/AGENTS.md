# GameProto/ 目录规范（生成代码，禁手改）

> 裁决顺序见根规范 §2。

| 内容 | 生成来源 |
| --- | --- |
| `GameConfig/`（Luban 数据类 + `Tables.cs` + `EntityId`/`UIFormId` 等枚举） | 导表（`gen_code_bin_to_project_lazyload.bat`） |
| `EntityGe/` `UIGe/`（Ge 半类，首次生成时创建；本项目实体手写，见 `Entity/AGENTS.md`） | 编辑器「Generate Script」（输出路径由 `ScriptGenerateRes` 决定） |
| `ConfigSystem.cs`（同模板另生成 `ExternalTypeUtil.cs` 到 `GameScripts/Config/`，同样禁改） | `Configs/GameConfig/CustomTemplate/` |
| `ResourcesCollectionConstant.cs` | TopMenu「Generate File → Collection Res」 |

- **任何文件都不手改**，重新生成即覆盖（红线 3）；业务逻辑写在 `*.Logic.cs` 或业务目录（Entity/ UI/ Manager/ 等）。
- 运行时配表入口 `ConfigSystem.Instance.Tables.TbXxx`；枚举编译期引用，禁字符串/魔法数字。
- 出错处理：表结构不对改 `Datas/*.xlsx` 重导；枚举缺项改 `__enums__.xlsx`；与手写 Logic 冲突或生成器疑似 bug → 报告人类（走 §13）。
