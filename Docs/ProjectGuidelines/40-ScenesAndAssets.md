# 40 场景与资源

## 固定目录

`TheGame/` 下资源使用既有目录：`Entitys/`、`Scenes/`、`UIs/`、`Sprites/`、`Audios/`、`DataTables/`；不另建平行 `Assets/`。`MainPack/` 放随应用打包的启动和共享资源，游戏逻辑放 `GameScripts/`。

资源文件使用英文小写 `snake_case`，basename 全树唯一；`.import` 与 `.uid` 随资源提交。移动或重命名资源在 Godot 编辑器内完成。旧项目迁入的素材登记在 `Docs/LegacyAssetMap.md`。

从旧项目迁入 PNG/WAV 时不带旧 `.import`；普通文件名保留并记录映射，战斗图集按 `<entity>_<state>` 命名以避免全树重名。动画从旧场景提取为 `.tres` 或按登记规格用一次性脚本生成；无源 TexturePacker 图集保留原 `.tres`，不重新导入。旧场景及脚本逻辑不直接复制。

## 场景绑定

- 实体场景与实体表 `AssetPath` 一致；节点引用由实体脚本 `[Export]` 字段绑定。
- 怪物实体场景用 `[Export] Entity.EntityId` 枚举字段（如 `m_MonsterEntityId`）绑定 `MonsterConfig`，编辑器显示为可读枚举下拉框；禁止导出需要反查的裸数字 ID。运行时按 `MonsterConfig.EntityId` 一对一查找，零条或多条命中在实体初始化时失败并交由会话拥有者报告。
- `.tscn` 的外部节点路径在场景头部 `node_paths` 声明，否则 Godot 可能忽略 `NodePath` 导出赋值。
- 动画库位于 `Entitys/Animations/`，资源边界与判定约定见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)。
- `Scenes/DebugArena.tscn` 仍是战斗验证场景；正式关卡使用 `LevelRoot`（当前实现为 `LevelModule`）和组合式子节点，契约见 [60-GameplayModules.md](60-GameplayModules.md)。

## 关卡场景

- `LevelModule` 根节点绑定生成点、门、刷怪、相机和出口五个必需职责节点；触发区集合按特殊玩法选配。模块统一初始化、开始、清波、收益和结局；门集合拥有区域，刷怪服务拥有本关实体，阶段序列拥有唯一当前阶段。
- `TestArena.tscn` 是**独立于游戏流程**的怪物与角色测试场地：在编辑器对场景按 F6“运行当前场景”即可，不需要改流程或配置表。场景自带真实 `Framework/GameFramework.tscn`（去掉 Procedure 流程），根脚本 `TestArenaController` 与 `ProcedurePreload` 共用 `MainPack/Scripts/Startup/GameResourceGroups` 注册资源组，再启动节点池。场地用建档规则创建仅内存的临时档案构建英雄装配（不读写存档），并把 `Monsters` 容器里拖入的怪物占位节点替换为 `GF.Entity` 创建的实例。新增怪物时把它的实体场景拖进 `Monsters` 容器、摆好位置即可。
- 集合挂父节点，直接子节点保持纯节点：生成点使用 `Marker2D`，门使用 `StaticBody2D + CollisionShape2D`（PlayerBarrier 层，mask 为零；英雄身体 mask 包含 PlayerBarrier，怪物不包含），特殊触发区使用 `Area2D + CollisionShape2D`（mask 选择 PlayerBody，初始 monitoring 关闭）。触发区放在对应物理区域内，入口侧形状不得跨本阶段左界；会话只启用当前区。不得建立单锚点脚本、块场景或集合场景。
- `Level/Markers/LevelMarkerResource` 是带 `[Tool]` 的可序列化 Resource，统一导出节点相对路径、稳定 ID 和颜色；所属集合的检查器显示条目列表。为父节点挂脚本并添加子节点后，点击“同步子节点配置”，再填写业务 ID。同步保留已有配置，移除已删除子节点的条目；加载和导入时不自动改写场景。
- 集合绘制所有支持的子节点：生成点/触发区为圆圈，门为竖线，标注可读 ID；未登记节点用洋红色显示并给出配置警告。颜色、尺寸和调试可见性可编辑；标注层使用 `z_index = 200`，避免被地形和实体遮挡。
- 集合与被它读取的 C# Resource 都必须带 `[Tool]`；仅给父集合加 Tool 会导致编辑器里的 Resource 未绑定 C# 实例，不能用“跳过未绑定条目”隐藏这个装配问题。编辑器提示“脚本正在编辑器中运行”正常；编辑器绘制与运行时初始化、事件订阅分开。
- `World/Background`、`Geometry`、`SpawnPoints`、`StageGates`、可选 `StageTriggers` 和 `Exit` 是语义插槽；相机和刷怪服务在根下。稳定业务 ID 用于表外键，节点名和 NodePath 只用于编辑器绑定，不作为运行时业务键。
- 远景天空用 `Parallax2D`（`World/Background/Sky`，`scroll_scale = (0.13, 1)`）：世界位置 = `(1 − 0.13) × 视图左缘`，即比相机慢 13%，全程覆盖视口。取值 = `(天图宽 − 视口宽) ÷ 相机行程 = (1440 − 940) ÷ 3760 ≈ 0.133`，使起点左缘对齐、终点右缘贴合视图右缘；相机行程或关卡右界变化时要按此式重算。近景装饰（`World/Background/Front`）保持世界锁定（`scroll_scale = 1`）。
- 关卡可玩右界裁到地形完整处：`Level_1` 右界为 4700（`Camera2D.limit_right`、右墙、地形贴图宽度一致），地形贴图 `level_1_floor.png` 已裁掉右侧 112px 的圆角收尾（该段右下透明、会露出背景）。关卡外墙与波次边界独立；当前前四波出口分别为 1139.9、1809.95、2970.15、3413.5，不把最后一个已接入波次延伸到场景右墙。
- 战斗 HUD 是 GGF 界面：场景 `UIs/BattleHud.tscn`（根 `Control`，全屏锚点），脚本 `GameScripts/UI/BattleHud/` 下 `BattleHud.cs`（Ge 样板，命名空间 `GameLogic.UI`，用 `Tools/ProjectMaintenance/regenerate_ui_form.py --namespace GameLogic.UI` 重生成）、`BattleHud.Logic.cs`（业务）与 `BattleHudData.cs`（打开参数）。根节点必须绑定生命/魔法/经验/无双资源条、等级、Go 导出字段并保留 `node_paths`。在 `界面UI.xlsx` 配置为 `UIFormId.BattleHud`（组 `Normal`），`ProcedureLevel` 在关卡模块开始后打开，先订阅失败事件，退出时关闭已打开及加载中的请求。数据流规则见 [00-Architecture.md](00-Architecture.md)「UI 数据流」。
- 清波后到下一场开战之间，`LevelModule.TravelAvailable` 为 true 并发出 `TravelAvailableChanged`；HUD 据此显示右侧前进提示（`m_Go`，67 帧循环，旧 `role_information.gogo`）。初始、开战、完成和清理时为 false。Go 属于屏幕空间 HUD，不放在关卡场景里。
- HUD 贴图在 `Sprites/UI/hud/`（旧 `Art/HeroPicture`）：状态面板/血条/无双/头像/菜单面板等，按旧项目 940×590 坐标 1:1 摆放。`ResourceBar` 是被动控件：`SetValue(current, max)` 即时更新填充和文字，零上限可配置为满条加特殊文字（经验满级显示 `MAX`）。
- `LevelCameraNode` 使用中央小死区：设计视口 940×590，死区半宽 40px（总宽 80px）；越界才跟随，Y 固定。已核对造3官方公开资源：画布同为 940×590，普通前进/后退阈值分别约为 626.67px 与 188px，采用非对称窗口。本项目按用户后续的“抵达时人物仍在中央”要求使用更窄的中央窗口；40px 是项目取景参数，不称为原版数值。来源与适用边界见第 60 章的相机依据。死区、标注颜色和追近速度上限在场景检查器编辑，玩法生成延迟仍进表。

- 相机中心始终是实际受限中心，禁用引擎位置平滑和拖动，避免脚本位置在墙后累计。清波只放开到紧邻下一阶段右界，保留当前位置与左界；每帧追近位移受 `MaxPanSpeed × delta` 限制，角色在墙前清波也不会立即重居中。
- 相机实际右缘抵达阶段右界时才开战，玩家正常行进时仍在中央死区附近；门集合此时启用本阶段独立左门并停用其他阶段门。相机停止在右界后，角色继续走到物理墙前才停止。收紧左界保留当前视野左缘，避免特殊触发阶段切换时跳变；物理门独立约束玩家。
- 阶段区域由独立左右门给定，允许相互重叠；右界沿阶段顺序递增，宽度至少容纳当前视野。首段左界可使用场景左墙，末段右界可使用场景右墙；是否为空取决于实际区域，不能仅凭列表末项省略右门。关卡级 `limit_*` 是场景空间事实；实际视野包含 Zoom。窗口使用 `canvas_items + keep` 保持 940×590 构图；`expand` 会在 1920×1080 下扩到 1048px，改变既定视野和原版波次构图。新增关卡需运行场景与物理回归，不能仅凭构建通过判断取景正确。
- Node 导出绑定必须保留 `.tscn` 的 `node_paths` 声明；移动 C# 脚本时携带原 `.uid` 并修复场景/资源引用，之后让 Godot 重扫文件系统。资源 bundle 根目录保持稳定。

### 无双满值提示

`BattleHud.tscn/MenuPanel/m_WsMax` 引用外置 `Sprites/UI/hud/hud_ws_full_frames.tres`，使用 `AnimatedSprite2D` 与 `hud_ws_flash.gdshader`；满值播放，消费、关闭或改绑后停止并复位。shader 以首帧轮廓去掉图集黄色底色，再用可调圆形范围限制闪烁：`clip_radius` 默认 32 原图像素、`clip_center` 默认 (0.5, 0.5)、`clip_feather` 默认向内 1 像素。参数在材质检查器编辑，GPU 验证入口为 `EditorScripts/validate_musou_flash.gd`。

## 入口界面

- `UIs/start_form.tscn`、`character_form.tscn`、`map_form.tscn`、`settings_form.tscn`、`save_form.tscn` 均为正式 GGF 窗口，在 `界面UI.xlsx` 注册，设计尺寸 940×590。菜单共用 `legacy_menu_theme.tres`；选角按用户四栏参考图使用官方完整卡片，来源与导出编号见 LegacyAssetMap；每栏位于 Roles 下，姓名与笔刷框已在贴图中，不再叠加组件。默认灰度，悬停当前栏恢复彩色、1P 移至该栏上方，离开隐藏；关闭/复用清理悬停。只有悟空可点击，其他角色完全禁用交互，悬停不变红、不恢复彩色、不显示 1P，八戒/沙僧保留剪影与问号。按用户要求不保留返回主菜单按钮。可点击按钮显示手形光标，禁用和不可选控件显示箭头。
- 菜单脚本按既有 Ge 半类约定生成绑定，业务写在 `.Logic.cs`。打开参数只引用所需的 `GameContext`、`LevelModule` 或 `SaveSlotDirectory`，无状态源的开始页、静态选角和地图不创建空 Data。按钮通过具名 `GF.Event` 请求导航，当前流程在事件回调中直接切换；不使用菜单命令轮询或 UI 按键回调。事件携带窗口打开编号，流程核对后处理，避免旧周期排队事件影响复用窗口。窗口不直接切流程或创建关卡。
- 地图地点使用旧项目普通/悬停配对图片。`MapLocationButton._HasPoint` 按当前显示图片的 alpha 轮廓判断：普通状态按居中普通图，悬停状态逆转 LocationGlow 的缩放、Pivot、KeepAspect 和 shader 变换，包含可见黄色轮廓；不设置固定 `TextureClickMask`。`LocationGlow` 子节点不接收鼠标。动画库外置在 `map_location_hover.tres`，RESET 同时恢复显示与参数；放大后在可见悬停图轮廓内仍保持悬停，离开其轮廓才复位。用户要求放大光效：只改 LocationGlow 的 Scale，当前为 (1.15, 1.15)，Pivot Offset 固定 (80, 87)，动画不会覆盖该尺寸；按钮本身不缩放。HUD 保留旧项目左下角技能、背包、设置、法宝与宠物图片；设置打开 `SettingsForm`，其中的“返回地图”由关卡流程执行。未实现图标明确禁用，不加右上角返回入口。设置窗口用 `GF.Base.PauseGame/ResumeGame` 对称管理暂停，池复用时按 `OnDepthChanged` 设置 `ZIndex`。
- 菜单背景与肖像位于 `Sprites/UI/frontend/`，音乐位于 `Audios/Music/`，确认音位于 `Audios/UI/`；来源登记在 [LegacyAssetMap.md](../LegacyAssetMap.md)。音乐循环由资源导入设置控制，加载、音量、代理池、淡入淡出及取消归 GGF。
- 当前只有悟空可出战、花果山五波可进入；未实现角色与地图地点明确显示未开放。菜单视觉参考原项目素材与官方竖幅肖像选择布局，不把预览素材冒充已实现玩法。

## Bundle

既有六个 bundle 标记为 `AudioBundle`、`EntityBundle`、`DataTableBundle`、`SpritesBundle`、`UIsBundle`、`ScenesBundle`。标记驱动资源包导出；不新增嵌套 bundle 标记，不以发布前复制资源代替资源归属。

改变 bundle 结构前先核实框架扫描路径与导出插件约束。Collection Res 生成要求全树 basename 唯一。

### 菜单图片与存档页（2026-10-11）

- 地图保留地点原图，左栏使用原项目的“保存游戏”图片和“返回主菜单”图片；返回标题前等待最近一次检查点。原图的空白题签按旧场景叠加文字，不增加独立文字导航按钮或档案信息页脚。尚未实现的商城、技能等入口可以暂不摆放。
- `map_form.tscn/m_LocationAnimation` 的 `speed_scale = 0.4`，外置动画长 1 秒，实际一轮 2.5 秒。调慢/调快编辑该 AnimationPlayer 的 Speed Scale；图像缩放、轮廓幅度和关键帧时点编辑 `map_location_hover.tres`。
- 设置卷轴按用户提供的造梦西游一官方截图：设计坐标 `(307,75)`、大小 `326×341`，无额外标题，五项为继续、声音、地图、帮助、主菜单。标题模式隐藏地图和主菜单；帮助复用同一卷轴，只说明当前已实现操作。
- 关闭叉使用 41×41 原图，悬停和按下使用 `menu_close_hover.png` 白色光晕；声音项统一控制 GGF Music/SFX/UI，并保存静音前音量用于恢复。战斗设置关闭时对称恢复时间，返回地图或标题均由关卡流程回滚和清理。
- 存档页是 `ProcedureTitle` 拥有的独立 GGF 模态窗口，不新增流程注册。按用户提供的造梦西游二六格布局，使用旧 `87.png` 框与 `BasicPicCd_1/2.png` 普通/悬停槽图：两列三行，空档只显示编号，已有档显示角色、等级、保存时间；不可读档禁用并提示，不作为空档覆盖。

### 守关生命条

`UIs/Widgets/encounter_health_bar.tscn` 是被动控件，HUD 提供名称与生命值。设计坐标 x=245、y=31，名称与血条横向拼接；白字黑边，字体沿用旧项目 BossBlood 的华康海报体 W12（MainPack/Fonts/df_poster_w12.ttc），右侧使用 boss_health_under/fill 原图。无进场弹窗、无生命数字、无额外背景面板。

关卡的 World/Exit 只保留一个出口子场景实例，不再同时建立同名占位节点；NodePath 与 node_paths 同步。资源检查同时覆盖重复节点路径、导出路径和资源字符串常量。
