# 40 场景与资源

## 固定目录

`TheGame/` 下资源使用既有目录：`Entitys/`、`Scenes/`、`UIs/`、`Sprites/`、`Audios/`、`DataTables/`；不另建平行 `Assets/`。`MainPack/` 放随应用打包的启动和共享资源，游戏逻辑放 `GameScripts/`。

资源文件使用英文小写 `snake_case`，basename 全树唯一；`.import` 与 `.uid` 随资源提交。移动或重命名资源在 Godot 编辑器内完成。旧项目迁入的素材登记在 `Docs/LegacyAssetMap.md`。

从旧项目迁入 PNG/WAV 时不带旧 `.import`；普通文件名保留并记录映射，战斗图集按 `<entity>_<state>` 命名以避免全树重名。动画从旧场景提取为 `.tres` 或按登记规格用一次性脚本生成；无源 TexturePacker 图集保留原 `.tres`，不重新导入。旧场景及脚本逻辑不直接复制。

## 场景绑定

- 实体场景与实体表 `AssetPath` 一致；节点引用由实体脚本 `[Export]` 字段绑定。
- `.tscn` 的外部节点路径在场景头部 `node_paths` 声明，否则 Godot 可能忽略 `NodePath` 导出赋值。
- 动画库位于 `Entitys/Animations/`，资源边界与判定约定见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)。
- 当前 `Scenes/DebugArena.tscn` 是战斗验证场景；正式关卡节点、阶段和出口规范待设计，见 [60-GameplayModules.md](60-GameplayModules.md)。

## Bundle

既有六个 bundle 标记为 `AudioBundle`、`EntityBundle`、`DataTableBundle`、`SpritesBundle`、`UIsBundle`、`ScenesBundle`。标记驱动资源包导出；不新增嵌套 bundle 标记，不以发布前复制资源代替资源归属。

改变 bundle 结构前先核实框架扫描路径与导出插件约束。Collection Res 生成要求全树 basename 唯一。
