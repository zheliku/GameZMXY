# 40 场景与资源

## 固定目录

`TheGame/` 下资源使用既有目录：`Entitys/`、`Scenes/`、`UIs/`、`Sprites/`、`Audios/`、`DataTables/`；不另建平行 `Assets/`。`MainPack/` 放随应用打包的启动和共享资源，游戏逻辑放 `GameScripts/`。

资源文件使用英文小写 `snake_case`，basename 全树唯一；`.import` 与 `.uid` 随资源提交。移动或重命名资源在 Godot 编辑器内完成。旧项目迁入的素材登记在 `Docs/LegacyAssetMap.md`。

从旧项目迁入 PNG/WAV 时不带旧 `.import`；普通文件名保留并记录映射，战斗图集按 `<entity>_<state>` 命名以避免全树重名。动画从旧场景提取为 `.tres` 或按登记规格用一次性脚本生成；无源 TexturePacker 图集保留原 `.tres`，不重新导入。旧场景及脚本逻辑不直接复制。

## 场景绑定

- 实体场景与实体表 `AssetPath` 一致；节点引用由实体脚本 `[Export]` 字段绑定。
- `.tscn` 的外部节点路径在场景头部 `node_paths` 声明，否则 Godot 可能忽略 `NodePath` 导出赋值。
- 动画库位于 `Entitys/Animations/`，资源边界与判定约定见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)。
- `Scenes/DebugArena.tscn` 仍是战斗验证场景；正式关卡使用 `LevelRoot`（当前实现为 `LevelController`）和组合式子节点，契约见 [60-GameplayModules.md](60-GameplayModules.md)。

## 关卡场景

- 关卡场景的固定入口是一个带 `LevelController` 的 `Node2D` 根节点。根节点必须绑定 `SpawnPoints`，并保留 `RuntimeActors` 插槽；实体实际仍由 `GF.Entity` 挂到实体组，不以关卡子节点数量统计活跃实体。
- `World/Background`、`World/Geometry`、`World/SpawnPoints`、`World/StageTriggers` 和 `World/Exit` 是推荐的语义插槽。地形、碰撞、相机边界、玩家出生点、生成点和触发器位置属于场景空间事实；背景、平台、出口、机关和拾取物可以按关卡需要增删。
- 生成点使用 `LevelSpawnPoint`，以场景内唯一的 `SpawnPointId` 作为配置外键；阶段触发点使用 `LevelStageTrigger` 和唯一 `TriggerId`。不得用节点名、NodePath 或子节点顺序作为稳定 ID。
- 可复用内容做成小型 PackedScene/组件（生成点、触发点、出口、地形块），不建立承载所有关卡逻辑的巨型 `BaseLevel` 场景。新增导出 NodePath 必须在 `.tscn` 根节点 `node_paths` 中声明。

## Bundle

既有六个 bundle 标记为 `AudioBundle`、`EntityBundle`、`DataTableBundle`、`SpritesBundle`、`UIsBundle`、`ScenesBundle`。标记驱动资源包导出；不新增嵌套 bundle 标记，不以发布前复制资源代替资源归属。

改变 bundle 结构前先核实框架扫描路径与导出插件约束。Collection Res 生成要求全树 basename 唯一。
