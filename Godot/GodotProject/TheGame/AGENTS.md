# TheGame/ 目录规范（游戏本体）

> 根规范：仓库根 `AGENTS.md`（先读）；Godot 规范：`../../AGENTS.md`。冲突时：根规范 > Godot > 本文件。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 目录总览

- `MainPack/` —— 主包，规范见 `MainPack/AGENTS.md`。
- `GameScripts/` —— 游戏代码，规范见 `GameScripts/AGENTS.md`（其中 `Entity/`、`GameProto/` 有各自的 AGENTS.md）。
- 其余 6 个目录是框架预置的 **bundle（导出子包）目录**，本文件管辖。

## bundle 目录（DataTables / Sprites / Audios / Entitys / UIs / Scenes）

| 目录        | 内容                                                   | 备注                                       |
| ----------- | ------------------------------------------------------ | ------------------------------------------ |
| `DataTables/` | Luban 导表产物 `.bytes` + 本地化 `.txt`              | **生成物，禁手改**；改配置一律改 `Configs/` 再导表 |
| `Sprites/`    | 全部贴图/图集                                        | 规范见 `Sprites/AGENTS.md`（命名/Collection Res） |
| `Audios/`     | 音频：`BGM/`、`SFX/<HeroId>/`、`SFX/Monster/`        | 命名同 Sprites 规则：snake_case、语义完整   |
| `Entitys/`    | 实体 `.tscn`（实体表 `AssetPath` 指向这里）          | 场景根节点挂实体 Logic 脚本                  |
| `UIs/`        | 游戏界面 `.tscn`（`UIFormId` 枚举对应）              | Logic 脚本与场景同名                         |
| `Scenes/`     | 关卡等场景 `.tscn`                                   | `GF.Scene` 加载的对象                        |

## 资源放置规则

- 资源**只放这 6 个预置目录**，禁止自建 `Assets/`：框架路径常量、`Collection Res` 生成器都围绕 `res://TheGame/` 下这些目录工作（见 `GameFolderConstant.cs`），再开一层只会制造冗余结构。
- 新制作/新整理的资源：snake_case 英文小写、语义完整、**basename 全树唯一**（Collection Res 硬约束，详见 `Sprites/AGENTS.md`）。
- `.import` / `.uid` 边车文件提交进 git；资源移动/重命名**只在 Godot 编辑器 FileSystem 面板内做**（引用自动修复）。
- 从旧项目搬来的资源：文件名可保留原样（根规范 §11.3），但目录必须按上表归位，并在 `Docs/LegacyAssetMap.md` 登记。

## bundle（导出子包）机制

- 任意目录放一个 `AssetBundle` 类型 `.tres` 标记（右击 → Create New → Resource → 搜 `AssetBundle`，命名惯例 `<DirName>Bundle.tres`），该目录即成导出单元：构建时目录下文件从主 pck 剔除、打进 `<ExportDir>/subpackages/<标记名>.pck`。
- 标记属性：`export_only_imported=true`（默认，pck 只放导入产物）、`pack_external_dependencies=true`（目录外被引用的依赖一并打入）。
- 当前 6 个标记已覆盖分类粒度；**禁止**新增套娃标记（bundle 判定按路径前缀，内外层互相打架）。确需"按英雄分包"再走根规范 §13 例外流程。
- 开发期 `ResourceMode.Package` + `EnableEditorResLoad` 下 bundle 与否不影响调试；**禁止"资源放 A 目录、发布前拷到 B"的两步搬法**。

## 提交前检查

- 跑一次 TopMenu「Generate File → Collection Res」确保通过（全树同名会中止生成）。
- 改了 `Configs/` 后重跑导表，确认 `DataTables/` 与 `GameProto/` 同步更新（两者都是生成物，一起提交）。
