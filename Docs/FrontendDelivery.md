# 入口界面、职责整理与当前存档交付

2026-10-11。按集中规范第 00、10、20、30、40、50、70 章实施，并按用户后续要求修订：小枚举按归属组织、只支持当前存档、不保留历史格式兼容。架构导览见 [ProjectTour.md](ProjectTour.md)，审查记录见 [CodeStyleReview.md](CodeStyleReview.md)，素材来源见 [LegacyAssetMap.md](LegacyAssetMap.md)。

## 当前行为

- F5 进入开始页，开始游戏打开六格存档窗口；新建档案进入四栏选角，已有档案直接进入地图。只有点击花果山才创建英雄和关卡。当前只有悟空可出战，其他角色与地点明确未开放。
- 选角按用户参考图使用官方完整卡片，四栏各 235×590，默认灰度；按最新要求，仅已开放的悟空悬停恢复彩色并显示 1P。未开放角色始终灰色，忽略鼠标输入，不显示 1P 或悬停提示。PNG 已含姓名和笔刷，删除重复组件及返回主菜单按钮；八戒与沙僧保留剪影、问号。完整素材来源、矢量编号与导出脚本见 LegacyAssetMap。窗口仍遵循 GGF 生成绑定与手写 Logic 的分工。
- 标题、选角与地图共用造梦一 `zmxy_1BGM.mp3` 的唯一资源 `map_theme.mp3`（145.76 秒），删除造梦三菜单配乐。关卡音乐、确认音、音量、播放代理、池和淡入淡出仍由 GGF 管理。
- 可点击按钮悬停显示手形光标，不可选角色和禁用控件显示箭头。花果山普通状态按小图透明轮廓命中，悬停后按当前放大图的透明轮廓命中；节点 Scale/Pivot、KeepAspect 和 shader 的 image_scale/黄色轮廓均参与坐标还原。LocationGlow 的缩放仍在场景调整，动画外置且一轮 2.5 秒，离开当前显示图像或关闭时复位。
- 流程键和池化包装集中在 `Startup/FlowData/`；Save 按 Data / Slots 分类，Event 按 Combat / Navigation 分类。小枚举和统计类型随所属主要类型；怪物攻击装配、动画范围推导与生命周期集中在 `MonsterEntity.Attacks.cs`，消除两类猴子的重复重写，共享 ActorEntity 不依赖怪物 AI 类型。
- 存档只支持 v3，出战英雄、拥有英雄及累计经验、金币直接平铺在 GameData。按用户要求删除 ProfileSaveData 及单独 DTO 文件，配套 HeroSaveData 随 GameData；CaptureSaveData / RestoreProfile / RestoreCheckpoint 分别说明快照、读档与回滚意图。业务入口为 ReadSlotsAsync / LoadOrCreateAsync / SaveCheckpointAsync，最近写入名为 PendingWrite。详见 Save/README.md；已有 v2 结构不做兼容，保留 GGF 原子写与备份恢复。
- 已核对并清理本机 `Godot/app_userdata/GameZMXY/GameData/` 两个 v1 档案、目录与备份，共六个文件；清理后该目录文件数为零。后续验证均写随机 Validation 目录，其他设置未清理。

此前结果保持：官方花果山前四波 11 处出生点与重叠战斗区域；无双满值才循环，SpriteFrames 外置且圆形裁剪；框架左上调试按钮隐藏；设置、返回回滚与六槽隔离仍有效。官方坐标和第五波缺口见 [OfficialHuaguoshan.md](OfficialHuaguoshan.md)。

## 验证命令与结果

下表含此前完整基线；本次选角/光标/扁平存档改动的最终结果另列于下方。

命令从仓库根运行，引擎为 `S:/Godot4/Godot4CSharp_console.exe`。引擎回归使用隔离的随机 `user://Validation/<模式>/<ID>`。

| 检查 | 命令或参数 | 结果 |
| --- | --- | --- |
| 配置 | `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，AI_MODE=1 | 通过，声音源表与产物同步 |
| UI 绑定 | `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/character_form.tscn --namespace GameLogic.UI` | 通过，五个角色/标记绑定 |
| 编译 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误，5 项既有框架过时 API 警告 |
| 战斗 / 输入 | `dotnet test Tests/BattleTests --no-restore -v quiet` | 84 通过 |
| 档案 / 配置 | `dotnet test Tests/ProfileTests --no-restore -v quiet` | 23 通过；历史等级兼容测试已删除 |
| 阶段 / 配方 | `dotnet test Tests/LevelTests --no-restore -v quiet` | 17 通过，共 124 项 |
| 真实鼠标前端 | `--path Godot/GodotProject --rendering-method gl_compatibility --quit-after 2400 -- --smoketest=frontend` | SMOKE PASS，退出码 0；四栏、新旧档分流、透明区拒绝点击、树身进入、悬停像素、六格、声音、设置与返回路径通过 |
| HUD / 存档 | `--headless --path Godot/GodotProject --quit-after 6000 -- --smoketest=ui` | SMOKE PASS，退出码 0；无双、复用、关卡事务、v3 新建、格式拒绝及备份恢复通过 |
| 四波关卡 | `--headless --path Godot/GodotProject --quit-after 12000 -- --smoketest=level` | SMOKE PASS，退出码 0；23 只怪物、63 经验及检查点 |
| 在途取消 | `--headless --path Godot/GodotProject --quit-after 3000 -- --smoketest=level-cancel` | SMOKE PASS，退出码 0；晚到实体回收 |
| 无双 GPU 裁剪 | `--path Godot/GodotProject --rendering-method gl_compatibility --script res://EditorScripts/validate_musou_flash.gd` | VISUAL PASS，退出码 0；10 帧 × 5 组参数逐像素通过 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py` | 174 个序列化路径、199 个 UID 通过 |
| 空白 | `git diff HEAD --check` | 通过 |

最终日志为 `%TEMP%/gamezmxy-current-save-{frontend,ui,level,level-cancel,musou}.log/.err`；配置、导入与资源常量日志为 `gamezmxy-organization-*`。Collection Res 使用原生成器完成；Godot 重扫产生新增资源导入与 UID。

本轮一个前端运行曾在二次入关时鼠标注入未触发设置按钮。补充失败截图和悬停节点诊断后，连续两次完整前端回归通过；未定位稳定复现原因，没有在正式代码或测试中加入重试来跳过断言。

NuGet 的 NU1900 表示受网络限制无法取得漏洞索引，测试实际执行成功。部分引擎关停仍记录既有 `DefaultWebRequestAgentHelper.Reset` 的 ObjectDisposedException 和资源仍被引用提示；编辑器重扫曾发生既有 ComponentInsoector 释放错误及 `0xC0000005`，末次 Collection Res 退出码 0 仍有插件释放错误。这些依赖问题与业务断言分开记录，未修改框架或插件。故意损坏文件、跨槽写入拒绝和 HUD 绑定失败的日志属于相应验证用例。

## 预览与边界

实际 GPU 截图已查看：[四栏选角](../Godot/GodotProject/.godot/validation/frontend/character_form.png)、[地图悬停](../Godot/GodotProject/.godot/validation/frontend/map_hover.png)、[战斗 HUD](../Godot/GodotProject/.godot/validation/frontend/frontend_battle.png)。截图在忽略目录，运行前端回归可以重新生成。

本轮请求已完成。玩法边界仍为悟空与花果山前四波；第五波 Boss、其他角色、完整解锁和尚未实现的背包/技能等不冒充可用内容。没有提交 Git 或改动暂存区。


## 后续选角、光标与 v3 扁平存档验证

按用户最新要求实施，规范依据为第 10/40/50/70 章。`ProfileSaveData` 已删除，存档字段平铺在 `Save/Data/GameData.cs`；本机两个经验/金币均为零的 v2 开发档，经编号、格式、目录及备份核对后按既有授权清理，共六文件。Validation 与声音设置未清理。

| 验证 | 命令或参数 | 最终结果 |
| --- | --- | --- |
| 原始包导出复现 | `python Tools/LegacyMigration/export_official_character_cards.py --java <java.exe> --ffdec <ffdec.jar> --swf <OtherMat_v9.swf>` | 四张 PNG 渲染成功，恢复后源包散列匹配 |
| 绑定 / 资源 | `regenerate_ui_form.py ...character_form.tscn --namespace GameLogic.UI`；Godot `--headless --editor --script res://EditorScripts/regenerate_collection.gd` | 五个字段绑定，旧笔刷资源常量移除，Collection 退出码 0 |
| 编译 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误，既有 5 项框架警告 |
| 档案 | `dotnet test Tests/ProfileTests --no-restore -v quiet` | 23 项通过，包括扁平 JSON、必需字段、旧版本拒绝、往返与回滚 |
| 实际 GUI 输入与 GPU 截图 | `--rendering-method gl_compatibility --quit-after 3000 -- --smoketest=frontend` | SMOKE PASS，退出码 0；四栏灰度、1P、不可选角色拒绝导航、复用、手形、光效大小与透明区拒绝命中通过 |
| 实际存档 / 关卡事务 | `--headless --quit-after 6000 -- --smoketest=ui` | SMOKE PASS，退出码 0；v3 新档、写盘、备份恢复、格式拒绝、收益提交及放弃回滚通过 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py` | 172 个序列化路径、197 个 UID 通过 |
| 空白 | `git diff HEAD --check` | 通过 |

最终引擎日志为 `%TEMP%/gamezmxy-hover-{frontend,ui}-final.log/.err`。已查看 [唐僧悬停](../Godot/GodotProject/.godot/validation/frontend/character_sanzang_hover.png)、[悟空悬停](../Godot/GodotProject/.godot/validation/frontend/character_form.png) 和 [地图光效](../Godot/GodotProject/.godot/validation/frontend/map_hover.png)。正式代码没有测试专用 API，本次没有修改框架或插件、没有提交 Git 或改动暂存区。

最初 GUI 回归暴露手形遗漏与窗口坐标输入问题，已修复并最终通过；没有加入失败重试。独立编辑器导入完成后关停仍遇既有 ComponentInsoector 释放错误及 0xC0000005，Collection 最终退出码 0；游戏回归仍有既有 WebRequest 关停异常，业务断言与退出码均单独核对。当前未开放角色、其他地图与第五波 Boss 的玩法边界保持此前登记。

## 未开放角色与地图保存提示（2026-10-11）

按用户最后补充的“不解锁就没有交互”处理：唐僧、八戒、沙僧使用静态 TextureRect，MouseFilter 为 Ignore，不注册悬停回调，不显示颜色、1P 或 tooltip，也不响应点击。悟空保留悬停彩色、1P 和确认进入地图。删除只服务旧悬停逻辑的三个导出字段和当前悬停字段，CharacterForm 仅保留两个必要绑定。第 40 章现有“四栏悬停恢复彩色、1P 跟随”的描述与本次要求有冲突；按用户当前指令优先执行，未自行改写集中规范。其余实现遵循第 10/40/50 章的事件、生命周期和生成边界。

旧项目参考为 `ZMXY_BHYH/Script/Main_menu/Map_1.gd` 的 `_on_bc_game_pressed`、`Global.gd` 的 `AddMessageShow` 及 `Scene/show_text/Message_show.tscn`：中央文字停留 1.5 秒后向上淡出 1 秒。新项目采用地图内的被动文字叠层，忽略鼠标；动画外置到 `Sprites/UI/frontend/map_save_feedback.tres`，由 MapForm 拥有并在关闭/复用时复位。只有当前窗口编号的 ProfileSavedEventArgs 返回实际写盘成功后显示“保存成功！”，失败显示“保存失败，请重试”。再次保存隐藏旧提示并重新播放，未新增空 Data、流程、提示框注册或测试专用 API。

| 验证 | 命令或入口 | 结果 |
| --- | --- | --- |
| UI 绑定 | `python Tools/ProjectMaintenance/regenerate_ui_form.py <scene> --namespace GameLogic.UI` | CharacterForm 两个、MapForm 六个绑定由原模板生成 |
| 资源常量 | Godot `--headless --editor --script res://EditorScripts/regenerate_collection.gd` | 新动画登记成功，退出码 0；仍有既有编辑器插件关停错误 |
| 编译 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误、5 项既有框架警告 |
| 真实前端 | `--rendering-method gl_compatibility --quit-after 3000 -- --smoketest=frontend` | 最终 SMOKE PASS、退出码 0；未开放角色无交互、真实保存成功、自动淡出、模拟失败结果、再次真实保存、关闭清理及旧编号反馈拒绝通过 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py` | 173 个序列化路径、197 个 UID 通过 |
| 空白 | `git diff HEAD --check` | 通过 |

已查看 [未开放唐僧悬停](../Godot/GodotProject/.godot/validation/frontend/character_sanzang_hover.png) 与 [地图保存成功](../Godot/GodotProject/.godot/validation/frontend/map_save_success.png) 的真实 GPU 截图。最终日志为 `%TEMP%/gamezmxy-feedback-frontend-final.log/.err`；失败展示通过现有事件模拟，不破坏存档文件；保存成功和再次保存均执行真实写盘。测试使用隔离 Validation 存档；跨槽拒写的故意错误及既有 WebRequest 关停异常仍单独记录，未修改框架、插件、旧项目或暂存区。本次功能无未完成项，集中规范的旧悬停文字差异已在上文报告。

## 花果山放大图命中（2026-10-11）

用户要求悬停后鼠标留在放大图片内就保持悬停。原先固定普通图的 TextureClickMask 会在鼠标离开小图轮廓时立即收回大图。新增 `UI/Widgets/MapLocationButton.cs`，通过 Godot 的 [_HasPoint 自定义命中契约](https://docs.godotengine.org/en/stable/classes/class_control.html#class-control-private-method-has-point) 判断当前显示图像：普通状态按居中的普通纹理透明度命中；悬停状态逆转 LocationGlow 的节点变换、KeepAspect 和 shader UV 缩放，检测悬停纹理及黄色轮廓。两张源图的 Bitmap 只在就绪时创建一次，不按帧重建。窗口仍只拥有动画与导航事件，控件拥有图片命中职责，不增加测试专用 API。

保留用户当前场景与动画中的视觉参数，不将 Scale 强制改回历史 1.15，也不改写当前 image_scale 关键帧。悬停开始立即应用首帧，使显示与命中使用同一状态。关闭和复用沿用既有 RESET。第 40 章“悬停不扩大命中范围”的旧规则与本次要求有冲突，本次以用户最新要求为准，未自行改写集中规范。回归发现当前选角场景重新带有未开放角色 tooltip 与鼠标接收，恢复此前明确要求的 Ignore 和无提示状态。

| 验证 | 命令或入口 | 结果 |
| --- | --- | --- |
| 绑定生成 | `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/map_form.tscn --namespace GameLogic.UI` | 六个绑定，花果山字段由场景生成到 MapLocationButton 类型 |
| 编译 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误、5 项既有框架警告 |
| 新脚本导入 | Godot `--headless --editor --build-solutions --quit --no-window -q` | 新脚本 UID 已生成；编辑器退出仍触发既有 ComponentInsoector 释放异常，进程退出码 -1073741819；不记为成功验证 |
| 真实前端 | `--rendering-method gl_compatibility --quit-after 3000 -- --smoketest=frontend` | 修复旧场景无交互回退后 SMOKE PASS、退出码 0；普通透明区拒绝输入、整个动画周期内放大区域保持悬停、Scale 放大至按钮矩形外仍命中、离开恢复、放大部分实际点击进关通过 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py` | 174 个序列化路径、198 个 UID 通过 |
| 空白 | `git diff HEAD --check` | 通过 |

已查看 [放大图扩展区域仍保持悬停](../Godot/GodotProject/.godot/validation/frontend/map_hover_expanded_hit.png) 的实际 GPU 截图。最终日志为 `%TEMP%/gamezmxy-location-hit-frontend-fixed.log/.err`；首次前端失败原因是选角场景旧要求回退，修复后完整回归通过。存档、声音及返回路径仍在同一次回归中验证；既有 WebRequest 关停错误与故意跨槽拒写日志单独记录。未修改 Framework、addons、旧项目、视觉关键帧或暂存区。本次功能无未完成项。

2026-10-12 后续调整：保存成功叠层已提取为应用级 `SaveNoticeForm`，外置动画为 `Sprites/UI/frontend/save_feedback.tres`。由 `SaveModule` 在真实写盘成功后统一触发，地图按钮结果事件只更新按钮状态与失败重试提示；详见 [CombatFeedback](CombatFeedback.md)。
