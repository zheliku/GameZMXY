# TheGame/ 目录规范（游戏本体）

> 裁决顺序见根规范 §2；`MainPack/`、`GameScripts/`、`Sprites/`、`Audios/`、`Entitys/` 有各自的 AGENTS.md。

## bundle 目录（本文件管辖）

| 目录 | 内容 | 备注 |
| --- | --- | --- |
| `DataTables/` | Luban `.bytes` + 本地化 `.txt` | 生成物，禁手改；改配置去 `Configs/` 再导表 |
| `Sprites/` | 贴图/图集 | 见 `Sprites/AGENTS.md` |
| `Audios/` | 音频 | 见 `Audios/AGENTS.md` |
| `Entitys/` | 实体 `.tscn`（实体表 `AssetPath` 指向） | 见 `Entitys/AGENTS.md` |
| `UIs/` | 游戏界面 `.tscn`（`UIFormId` 对应，Logic 同名） | |
| `Scenes/` | 关卡与调试场景（`GF.Scene` 加载，如 `DebugArena.tscn`） | |

- 资源只放这 6 个预置目录（框架路径常量与 Collection Res 扫描围绕它们，根规范 §3.2），不自建 `Assets/`。
- snake_case 英文小写、basename 全树唯一；`.import`/`.uid` 提交 git；移动/重命名只在编辑器内做；旧项目资源归位并登记 `LegacyAssetMap.md`（根规范 §11）。

## bundle（导出子包）机制

- 目录内放 `AssetBundle` 类型 `.tres` 标记即成导出单元：构建时打入 `<ExportDir>/subpackages/<标记名>.pck`。现有 6 个标记：`AudioBundle / EntityBundle / DataTableBundle / SpritesBundle / UIsBundle / ScenesBundle`。
- 标记属性默认 `export_only_imported=true`、`pack_external_dependencies=true`。
- 禁止新增套娃标记（bundle 按路径前缀判定，内外层冲突），确需再分包走 §13；开发期 `ResourceMode.Package` + `EnableEditorResLoad` 下 bundle 不影响调试，禁止"放 A 拷 B"的两步搬法。

## 提交前检查

- 跑一次 TopMenu「Generate File → Collection Res」确保通过（全树同名会中止）。
- 改过 `Configs/` 则重跑导表，`DataTables/` 与 `GameProto/` 一起提交。
