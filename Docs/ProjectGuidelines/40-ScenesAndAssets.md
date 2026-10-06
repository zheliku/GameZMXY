# 40 场景与资源

## 固定目录

`TheGame/` 下资源使用既有目录：`Entitys/`、`Scenes/`、`UIs/`、`Sprites/`、`Audios/`、`DataTables/`；不另建平行 `Assets/`。`MainPack/` 放随应用打包的启动和共享资源，游戏逻辑放 `GameScripts/`。

资源文件使用英文小写 `snake_case`，basename 全树唯一；`.import` 与 `.uid` 随资源提交。移动或重命名资源在 Godot 编辑器内完成。旧项目迁入的素材登记在 `Docs/LegacyAssetMap.md`。

从旧项目迁入 PNG/WAV 时不带旧 `.import`；普通文件名保留并记录映射，战斗图集按 `<entity>_<state>` 命名以避免全树重名。动画从旧场景提取为 `.tres` 或按登记规格用一次性脚本生成；无源 TexturePacker 图集保留原 `.tres`，不重新导入。旧场景及脚本逻辑不直接复制。

## 场景绑定

- 实体场景与实体表 `AssetPath` 一致；节点引用由实体脚本 `[Export]` 字段绑定。
- 怪物实体场景用 `[Export] Entity.EntityId` 枚举字段（如 `MonsterEntityId`）绑定 `MonsterConfig`，编辑器显示为可读枚举下拉框；禁止导出需要反查的裸数字 ID。运行时按 `MonsterConfig.EntityId` 一对一查找，零条或多条命中在实体初始化时失败并交由会话拥有者报告。
- `.tscn` 的外部节点路径在场景头部 `node_paths` 声明，否则 Godot 可能忽略 `NodePath` 导出赋值。
- 动画库位于 `Entitys/Animations/`，资源边界与判定约定见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)。
- `Scenes/DebugArena.tscn` 仍是战斗验证场景；正式关卡使用 `LevelRoot`（当前实现为 `LevelController`）和组合式子节点，契约见 [60-GameplayModules.md](60-GameplayModules.md)。

## 关卡场景

- `LevelController` 根节点绑定生成点、门、刷怪和相机四个必需职责节点；触发区集合按特殊玩法选配。控制器只协调初始化、开始、清波和结束；门集合拥有区域，刷怪服务拥有本关实体，阶段序列拥有唯一当前阶段。
- `TestArena.tscn` 是**独立于游戏流程**的怪物与角色测试场地：在编辑器对场景按 F6“运行当前场景”即可，不需要改 `ProcedureGame` 或配置表。场景自带真实 `Framework/GameFramework.tscn`（去掉 Procedure 流程），根脚本 `TestArenaController` 只补上 `ProcedurePrelode` 的分组注册与节点池启动，并把 `Monsters` 容器里拖入的怪物占位节点替换为 `GF.Entity` 创建的实例。新增怪物时把它的实体场景拖进 `Monsters` 容器、摆好位置即可。
- 集合挂父节点，直接子节点保持纯节点：生成点使用 `Marker2D`，门使用 `StaticBody2D + CollisionShape2D`（World 层），特殊触发区使用 `Area2D + CollisionShape2D`（mask 选择 PlayerBody，初始 monitoring 关闭）。触发区放在对应物理区域内，入口侧形状不得跨前门；会话只启用当前区。不得建立单锚点脚本、块场景或集合场景。
- `Markers/LevelMarkerEntry` 是带 `[Tool]` 的可序列化 Resource，统一导出节点相对路径、稳定 ID 和颜色；所属集合的检查器显示条目列表。为父节点挂脚本并添加子节点后，点击“同步子节点配置”，再填写业务 ID。同步保留已有配置，移除已删除子节点的条目；加载和导入时不自动改写场景。
- 集合绘制所有支持的子节点：生成点/触发区为圆圈，门为竖线，标注可读 ID；未登记节点用洋红色显示并给出配置警告。颜色、尺寸和调试可见性可编辑；标注层使用 `z_index = 200`，避免被地形和实体遮挡。
- 集合与被它读取的 C# Resource 都必须带 `[Tool]`；仅给父集合加 Tool 会导致编辑器里的 Resource 未绑定 C# 实例，不能用“跳过未绑定条目”隐藏这个装配问题。编辑器提示“脚本正在编辑器中运行”正常；编辑器绘制与运行时初始化、事件订阅分开。
- `World/Background`、`Geometry`、`SpawnPoints`、`StageGates`、可选 `StageTriggers` 和 `Exit` 是语义插槽；相机和刷怪服务在根下。稳定业务 ID 用于表外键，节点名和 NodePath 只用于编辑器绑定，不作为运行时业务键。
- 远景天空用 `Parallax2D`（`World/Background/Sky`，`scroll_scale = (0.13, 1)`）：世界位置 = `(1 − 0.13) × 视图左缘`，即比相机慢 13%，全程覆盖视口。取值 = `(天图宽 − 视口宽) ÷ 相机行程 = (1440 − 940) ÷ 3760 ≈ 0.133`，使起点左缘对齐、终点右缘贴合视图右缘；相机行程或关卡右界变化时要按此式重算。近景装饰（`World/Background/Front`）保持世界锁定（`scroll_scale = 1`）。
- 关卡可玩右界裁到地形完整处：`Level_1` 右界为 4700（`Camera2D.limit_right`、右墙、地形贴图宽度一致），地形贴图 `level_1_floor.png` 已裁掉右侧 112px 的圆角收尾（该段右下透明、会露出背景）；最后一阶段的右界门随之左移，保证末段区域仍不小于一屏。
- 清波后到下一场开战之间，`LevelController` 点亮右侧前进提示 `GoHintLayer/GoHint`（`AnimatedSprite2D`，67 帧 / 25fps 循环，对应旧项目 `role_information.gogo`）；开战、结束或清理时隐藏。提示是屏幕空间的 `CanvasLayer` 子节点，不随相机滚动。
- `LevelCamera` 使用中央小死区：设计视口 940×590，死区半宽 40px（总宽 80px）；越界才跟随，Y 固定。已核对造3官方公开资源：画布同为 940×590，普通前进/后退阈值分别约为 626.67px 与 188px，采用非对称窗口。本项目按用户后续的“抵达时人物仍在中央”要求使用更窄的中央窗口；40px 是项目取景参数，不称为原版数值。来源与适用边界见 [原版相机核查](../Reviews/zmxy3_camera_reference_2026-10-06.md)。死区、标注颜色和追近速度上限在场景检查器编辑，玩法生成延迟仍进表。
- 相机中心始终是实际受限中心，禁用引擎位置平滑和拖动，避免脚本位置在墙后累计。清波只放开到紧邻下一阶段右界，保留当前位置与左界；每帧追近位移受 `MaxPanSpeed × delta` 限制，角色在墙前清波也不会立即重居中。
- 相机实际右缘抵达阶段右界时才开战，玩家正常行进时仍在中央死区附近；门集合此时封住前门。相机停止在右界后，角色继续走到物理墙前才停止。收紧左界保留当前视野左缘，避免特殊触发阶段切换时跳变；物理门独立约束玩家。
- 阶段区域至少容纳当前视野，门沿 X 递增；末段可以没有右门，由关卡原始右墙约束。关卡级 `limit_*` 是场景空间事实；实际视野包含 Zoom。窗口使用 `canvas_items + keep` 保持 940×590 构图；`expand` 会在 1920×1080 下扩到 1048px，超出最窄阶段 1020px 的空间契约。新增关卡需运行场景与物理回归，不能仅凭构建通过判断取景正确。
- Node 导出绑定必须保留 `.tscn` 的 `node_paths` 声明；移动 C# 脚本时携带原 `.uid` 并修复场景/资源引用，之后让 Godot 重扫文件系统。资源 bundle 根目录保持稳定。

## Bundle

既有六个 bundle 标记为 `AudioBundle`、`EntityBundle`、`DataTableBundle`、`SpritesBundle`、`UIsBundle`、`ScenesBundle`。标记驱动资源包导出；不新增嵌套 bundle 标记，不以发布前复制资源代替资源归属。

改变 bundle 结构前先核实框架扫描路径与导出插件约束。Collection Res 生成要求全树 basename 唯一。
