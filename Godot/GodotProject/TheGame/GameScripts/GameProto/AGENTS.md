# GameProto/ 目录规范（生成代码，禁手改）

> 根规范：仓库根 `AGENTS.md`（先读）；GameScripts 规范：`../AGENTS.md`。冲突时：根规范 > GameScripts > 本文件。

## 本目录是工具生成物

| 子目录          | 内容                                          | 生成来源                                                     |
| --------------- | --------------------------------------------- | ------------------------------------------------------------ |
| `GameConfig/`   | Luban 表数据类 + `Tables.cs` + `EntityId` / `UIFormId` 枚举 | `Configs/GameConfig/` 导表（`gen_code_bin_to_project_lazyload.bat`） |
| `EntityGe/` `UIGe/` | 场景/UI 的 Ge 半类                        | 编辑器「Generate Script」（输出路径由 `ScriptGenerateRes` 决定） |
| `ConfigSystem.cs` / `ExternalTypeUtil.cs` | 自定义模板产物       | `Configs/GameConfig/CustomTemplate/`                         |
| `ResourcesCollectionConstant.cs` | Collection Res 生成常量      | TopMenu「Generate File → Collection Res」                    |

## 禁改清单

- **任何文件都不手改**：重新生成即覆盖，手改必然丢失（根规范红线 3）。`ResourcesCollectionConstant.cs` 永不手改。
- 业务逻辑只写在对应的 `*.Logic.cs`（实体/UI 逻辑半类），或写在业务目录（Entity/ UI/ Manager/ 等）。
- 运行时配表入口：`ConfigSystem.Instance.Tables.TbXxx`；枚举（`EntityId` / `UIFormId` / `DamageKind` 等）一律编译期引用，禁止字符串/魔法数字。

## 再生成 / 出错时怎么办

- 表结构不对 → 改 `Configs/GameConfig/Datas/*.xlsx`（规范见 `Configs/AGENTS.md`），重跑导表。
- 枚举缺项 → 改 `__enums__.xlsx` 或业务表登记，再导表；不要直接在生成的枚举里加值。
- 生成代码与手写 Logic 冲突（重名字段/方法）→ 报告人类，改生成模板（`CustomTemplate/` 或 `ScriptGenerateRes` 输出配置）而不是绕过。
- 生成器本身疑似 bug → 走根规范 §13 框架例外流程。
