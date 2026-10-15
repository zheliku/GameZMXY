# 验证

在仓库根执行。实际引擎：`S:/Godot4/Godot4CSharp_console.exe`；可换成本机对应路径。

## 常用命令

```powershell
# 改源表后先导出；AI_MODE=1 跳过脚本末尾 pause
$env:AI_MODE = '1'
cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat
dotnet build Godot/GodotProject --no-restore -v quiet
dotnet test Tests/BattleTests --no-restore -v quiet
dotnet test Tests/ProfileTests --no-restore -v quiet
dotnet test Tests/LevelTests --no-restore -v quiet
python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry

# 真实 UI、物理、流程；写入隔离 Validation 存档
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=ui
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=level
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=frontend
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest
# F6 独立测试场地，不启动正式流程或读写玩家档案
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject --scene res://TheGame/Scenes/TestArena.tscn --quit-after 360
```

按修改范围选用 `--smoketest=level-cancel`、`ai`；规则单测无需打开编辑器。场景装配用 `--headless --script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，编辑器 Tool 再加 `--editor`；触发区用 `validate_level_triggers.gd`，动画库用 `validate_anim_libraries.gd`。有渲染器时，`validate_musou_flash.gd` 检查无双圆形裁剪，`validate_level_up.gd` 检查升级蓝线裁剪、画面和注册尺寸。

XLSX Diff 1.6.4 的解析器无法读取带 `x:` 前缀的 SpreadsheetML 标签，会错误地显示零张表。导表工具仍能读取这类文件。遇到该问题时，用 `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py --write <文件.xlsx>` 将标签改为等价的默认命名空间；脚本逐项验证 XML 内容并原子替换，不重新生成单元格或样式。不带 `--write` 时只检查。已暂存的文件需要重新暂存修复后的版本，工作区修复不改 Git 索引；历史版本若含相同前缀，插件仍无法读取历史侧。

引擎截图写入忽略的 `.godot/validation/`；无双开场及后半段分别为 `musou_activation.png`、`musou_activation_tail.png`，升级为 `level_up.png`、`level_up_tail.png`，原帧/裁剪帧对照为 `level_up_frames.png`，BOSS 条为 `encounter/featured_health_bar.png`。通过 PASS/FAIL、退出码、错误与画面共同判断结果。

战斗背包为 `inventory_form.png`，品质上拉菜单为 `inventory_quality_sale.png` / `inventory_quality_sale_scaled.png`，出售确认框为 `inventory_sale_confirmation.png` / `inventory_sale_confirmation_scaled.png`，多属性详情为 `inventory_tooltip_attributes.png`，失败结算为 `settlement_failure.png`，成功结算为 `encounter/settlement_success.png`。成功页截图在真实检查点完成后立即采集，可能带有短暂的保存成功提示；提示按真实时间淡出，背包和结算暂停不会将它冻结。

装备背包与实际详情为 `inventory_equipment.png`，操作菜单为 `inventory_actions.png`，武器/防具同时穿戴为 `inventory_wearing.png`，绿色/蓝色恢复数字为 `recovery_numbers.png`；无名称的地面恢复品和装备为 `ground_pickups.png`；地图默认边缘闪烁的两端画面为 `frontend/map_current_low.png`、`frontend/map_current.png`。

## 编辑器 GCHandle 修补（2026-10-15）

按第 50 章的用户授权，仅修改 ExportInspector：先读资源依赖及全局脚本登记，只加载候选包标记并校验，文件列表和打包复用已识别标记，避免扫描时提前加载普通资源。ComponentInsoector 的 14 处释放修改按用户最新要求全部撤回，恢复原文件；这项修改处理退出错误，没有解决本次启动异常。未增加业务测试 API。

- 修补前，实际工程同程序集、同布局普通窗口启动：启用 ExportInspector 出现 **4 次 SwapGCHandleForType 和 2 次退出 SetGodotObjectPtr 异常**，仅临时禁用此插件为 **0 错误**，恢复启用再次为 **4 + 2**。插件扫描会提前加载 54 个候选资源、在条目扫描中再加载 37 个资源。该路径与 Godot 官方 [#112067](https://github.com/godotengine/godot/issues/112067) 的资源句柄问题相符，具体失效的原生资源未唯一定位。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：**0 错误、5 个既有框架警告**。正式 ExportInspector 与已验证候选逐字节一致；原版和候选均识别 6 个正式包及 1 个二进制 .res 测试包，**748 项文件清单逐字节一致**，普通动画的提前缓存由 True 变为 False。PCK 在空白工程读回，普通资源存在、二进制标记已排除；副本重建资源索引后的首次扫描也通过。
- 撤回 ComponentInsoector 之前（当时仍包含它的释放修正），在实际工程保留全部插件、原布局和正式缓存，用 `--editor --path Godot/GodotProject --max-fps 60 --quit-after 600` 完成 **3 次普通窗口冷启动：均退出 0，0 错误、0 警告**。另一次旧程序集对照本轮也未复现，说明问题有间歇性；上述结果不代表 Godot 所有句柄缺陷均已修复。未使用最小化、离屏或副本启动替代实际工程验证。
- 最终仅保留 ExportInspector 修补后，再次执行上述 build：**0 错误、5 个既有框架警告**；实际工程普通窗口冷启动 **退出 0，SwapGCHandleForType / SetGodotObjectPtr 目标异常均为 0**。退出时恢复既有 **14 条 Can't free a RefCounted object 和 14 条 Nonexistent function 'free'**，堆栈均指向 ComponentInsoector._ExitTree；这项独立的退出问题按用户要求不纳入本轮修改，不能把本次完整退出日志称为零错误。`git diff --exit-code -- addons/ComponentInsoector/ComponentInsoector.cs` 为 0，确认该文件恢复原版。
- 验证后已恢复修补程序集及原布局，布局按原字节比对通过；project.godot、正式存档及 Git 索引未修改，未清理正式缓存。此前重载验证仍有 8 条断开不存在连接错误和 3 个 GodotBody2D RID 泄漏；本轮只验证启动，不宣称这些既有重载问题已解决。

日志在忽略的 `.godot/validation/`：修补前对照为 `editor_gc_ab_{baseline,no_export,reenabled}.{out,err}.log`；撤回前验证为 `editor_gc_scan_fixed_{1,2,3}.{out,err}.log`，旧程序集对照为 `editor_gc_scan_previous_control.{out,err}.log`，构建为 `editor_gc_scan_fixed_build.log`；最终最小修改的构建与冷启动为 `editor_gc_minimal_build.log`、`editor_gc_minimal_cold.{out,err}.log`；文件清单、首次扫描与打包为 `editor_gc_metadata_{before,probe,fresh}.{out,err}.log`、`editor_gc_bundle_pack.log`。诊断副本、探针、补丁和日志不提交。

## 当前架构与启动流程回归（2026-10-15）

按用户最新要求统一业务 System、实体 ActionData 和 IController 访问方式。飞行球删除英雄成员，由本关控制器逐帧提供目标坐标；UI Data 不再传递共享 Model，存档/设置/技能 Data 删除。Prepare 集中注册工具、模型和系统，Preload 预加载界面，LoadGame 建立档案作用域；Startup 与 AbstractMenuProcedure 删除。此前的物理接触、AI 状态工厂集合、统一输入采样和不可变装配仍保留。所有者、调用链及旧规范差异见 [项目导览](ProjectTour.md)。按第 10/30/40/50 章执行命名、文档、功能分组、UID 与隔离验证；第 50 章仅登记用户要求的必要流程注册项。

- 上述 `dotnet build`：**0 错误、5 个既有框架警告**；三组 `dotnet test` 分别 **145/145、96/96、18/18**，共 **259 项通过**。单测仍有 NU1900 联网告警及既有 nullable 告警。
- OpenGL `--smoketest=frontend`、`ui`、`level`：均 **PASS、退出 0**。前端覆盖 Prepare/LoadGame、新建/读档、选角、地图、回滚、保存提示与设置复用；Get 查询验证未注册时返回 null、同一引用、移除、销毁及 FSM 隔离。UI 覆盖直接读取 Model 的技能页、背包刷新/交易、订退、窗口/实体复用、失败清理、死亡保存和重入；飞行球覆盖暂停、目标位置更新、同场景其他英雄、拾取上限、池复用和死亡回收。关卡覆盖五波、相机、门、守关条、出口等待飞行掉落、检查点和地图重读。
- 上述 F6 命令：**场地就绪、退出 0**，测试入口直接建立资源组与临时 FSM，显示玩家和两只怪物；不启动正式流程或读取玩家档案。
- Godot `--headless --editor --build-solutions --quit --no-window`：文件扫描、脚本注册和构建完成，**退出 0**；仍报告既有 ComponentInsoector 的 RefCounted/free 释放错误。四个脚本迁移保留原 UID；引用检查 **441 路径、291 UID、23 几何节点通过**，场景几何未变。C# 格式、文档本地链接与 `git diff --check` 检查见本轮验证输出，Git 索引保持不变。
- 日志在 `.godot/validation/ui_flow_{scan,frontend,ui,level,arena,format}.log`。HUD 缺绑定、旧档案写入拒绝与损坏存档是预期失败注入；框架关停 WebRequest `ObjectDisposedException` 仍在，frontend/ui/level/F6 分别有 **3/4/4/3** 个退出泄漏告警，未计为玩法验证通过的依据。生成代码、源表和正式存档未改动；Framework 仅更新已登记的 Procedure.Procedures 清单。
- UI 订退简化：HUD、背包、存档、设置、技能五个窗口删除退订委托，打开时 `+=`、关闭时直接 `-=`；流程先关窗再移除 Model。再次执行上述 `dotnet build` 和 OpenGL `--smoketest=frontend`、`--smoketest=ui`：**0 编译错误，两组回归均 PASS、退出 0**；实际事件列表确认存档页关闭后处理器为 0、复用后为 1，设置关闭后不再订阅关卡；HUD/背包/技能关闭后不响应旧状态源，失败清理、暂停恢复、重绑和池复用正常。7 个 C# 文件格式、UTF-8/CRLF 与 `git diff --check` 通过，验证期间未改 Git 索引。日志为 `.godot/validation/ui_subscription_{build,frontend,ui}.log`，既有框架退出错误与前端/UI 的 3/4 个泄漏告警仍在。
- 初始化与 UI 状态简化：Prepare.OnEnter 直接注册资源组、工具、Model 和 System；删除语言初始化、返回标题、目录创建及标记创建等简单包装。核对 GGF 的 IUIForm、UIManager 与 UIComponent：交互直接查询 `GF.UI.HasUIForm(SerialId)`，8 个重复打开标志已删除，退订留在 OnClose，OnRecycle 不再查询已结束的共享模型。再次执行上述 build、frontend/ui 和 F6 命令：**0 编译错误，前端/UI 均 PASS，三组引擎进程均退出 0**；关闭后尚未回池时 HasUIForm/IsValidUIForm 已为 false，迟到存档按钮没有导航，确认提示没有执行旧回调；订退、暂停、失败清理及池复用通过。日志为 `.godot/validation/entry_ui_{build,frontend,ui,arena}.log`。16 个 C# 文件格式、UTF-8/CRLF、直接注册顺序和本地文档链接通过；引用检查仍为 **441 路径、291 UID、23 几何节点**，Git 索引未改。既有 5 个框架编译警告、WebRequest 退出错误及前端/UI/F6 的 3/4/3 个泄漏告警仍在；框架、生成物、源表和正式存档未改动。

## 作用域装配与引用生命周期（2026-10-14）

按用户最新要求借鉴 QFramework 的状态/业务分离，以 GGF 流程 FSM 类型化 SetData/GetData 统一访问；ProcedureLoadProfile 初始化并注册 Profile、SaveSystem、EquipmentSystem，Title 统一移除。删除 GameContext 及两个专用包装，保留纯状态/规则和 UI 子组件的显式参数。第 00/70 章的旧约定差异、初始化表、保存与运行时边界见 [项目导览](ProjectTour.md#所有者与初始化位置)。

- `dotnet build Godot/GodotProject --no-restore -v quiet`：**0 错误、5 个既有框架警告**。`dotnet test Tests/{BattleTests,ProfileTests,LevelTests} --no-restore -v quiet`：**137/137、92/92、18/18**；单测另有既有 NU1900 联网警告。
- OpenGL `--smoketest=frontend`：**PASS，退出 0**。真实双 FSM 验证同类型隔离、重复初始化拒绝、未初始化读取拒绝、移除与包装复用；选角到地图保持档案/模块身份，关卡和 HUD 借用同一状态，离关解除流程引用，回标题移除档案数据，再读档创建新实例并恢复数值。测试通过既有对象、实际 FSM 和反射观测。
- OpenGL `--smoketest=ui` 与 `--smoketest=level`：均 **PASS，退出 0**。覆盖背包刷新、换装、窗口和实体复用、收益回滚、死亡保存、备份回退、加载取消、失败清理，以及五波关卡、守关血条、出口结算、检查点写盘与地图重读。日志仍有既有证书读取错误、框架关停 ObjectDisposedException；frontend/ui/level 分别有 3/4/4 个退出泄漏实例。前端的旧槽写入拒绝、损坏档案和 UI 的 HUD 失败注入为预期用例。
- 新脚本经 `--headless --editor --quit` 扫描，退出 0；日志仍有既有编辑器插件 RefCounted/free 释放错误。25 份 C# 的 `dotnet format whitespace . --folder --include <绝对路径> --verify-no-changes`、`git diff --check` 通过。`validate_game_references.py --compare-staged-geometry`：**441 条路径、290 个 UID、23 个几何节点通过**；文档本地链接检查通过。日志为 `.godot/validation/typed_data_{scan,build,BattleTests,ProfileTests,LevelTests,frontend,ui,level_engine}.log`。

## 命名复查与扩展验证（2026-10-14）

按第 10 章检查手写业务、流程及测试的方法命名，统一组合输入、水平移动、巡逻折返、怪物选招、动画采样、状态机生命周期和窗口清理方法；调用、事件订退和测试反射同步更新。

- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误，5 个既有框架警告和 1 个 NU1900。三个 `dotnet test Tests/{BattleTests,ProfileTests,LevelTests} --no-restore -v quiet`：分别 **137/137、92/92、18/18**；新增一段/三段英雄的配置、循环和空中段号验证，覆盖 60/120Hz。
- OpenGL `--smoketest=ui`：**PASS，退出 0**；覆盖怪物行为、界面清理、实体复用、反射调用、加载失败清理与存档重入。日志仍有既有证书错误、预期 HUD 失败注入、框架关停 `ObjectDisposedException` 及 4 个退出泄漏实例；没有新增玩法失败。日志为 `.godot/validation/readable_names_{build,battle,profile,level,ui}.log`。
- 本轮 21 份 C# 的 `dotnet format whitespace . --folder --include <绝对路径> --verify-no-changes`、`git diff --check` 和引用检查通过（441 条路径、292 个 UID、23 个几何节点）；旧标识符检索无残留。唐僧弹道与飞行怪尚未实现，复用边界及现有落地限制见 [项目导览](ProjectTour.md)。

## 官方输入、状态职责与格式整理（2026-10-14）

按第 00/10/20/30/50 章维护所有权、手写命名、配置导出、身体状态机与验证边界；玩法以用户本轮要求的造一源码为准，规范正文未自行改写。

- Luban 导出成功；构建 0 错误、5 个既有框架警告及 1 个 NU1900 联网警告。BattleTests **133/133**、ProfileTests **92/92**、LevelTests **18/18**；覆盖 60/120Hz 第四段松手、有效输入即时换段、独立冷却、超时、组合键、跳跃、空中方向及受击打断；配置拒绝缺段、非正和非有限参数。另补空中攻击松方向再按回、一帧内松按、受击后续段、不同英雄独立序列及怪物第二招的切换参数；空中松按问题先复现失败再修复。
- `--smoketest` **PASS，退出 0**：真实五段连击、走跑、两次跳跃、空中下劈与命中；新增末轮打到第四段后注入过早按键并彻底松开，收招后不出现第五段。
- OpenGL `--smoketest=level` **PASS，退出 0**：五波战斗、怪物出招/收招、相机、物理门、守关血条、成功结算、检查点写盘及返回地图重读；新增近处 W＋J/K/S 不误触出口，再验证单独 W 通关。
- OpenGL `--smoketest=ui` **PASS，退出 0**：三座单向平台、真实英雄 S＋K 下落；背包刷新、穿戴外观、存档目录、技能目录、无双、受击、实体池与隔离存档重入继续通过。平台夹具按实际脚底和平台表面放置；移位后等待真实物理更新再读取落地事实。
- HeroConfig 的 XLSX Diff 普通/公式模式均识别 **24 格**目标变化，旧缓冲字段已删除，秒制冷却及移动数值可见；默认 XML 命名空间保持可读。引用检查 **441 条路径、292 个 UID、23 个几何节点**通过；`git diff --check` 通过。
- `HeroAttackSequence` 归英雄拥有，状态只调用序列并自行切换；删除基类反查攻击子状态的方法。怪物两处跨状态 `Arm` 调用改用 GGF 切换数据，进入时消费并归还池化参数。全项目手写业务不再有 `GetState<T>().方法` 的跨状态调用。
- 将格式配置集中到仓库根 `.editorconfig`；使用 `dotnet format whitespace . --folder --include <手写文件绝对路径> --verify-no-changes` 分批验证 **216 份**手写 C#，排除框架与生成物。展开拥挤语句，统一四空格缩进和换行；Roslyn 核对格式化前后语法标记一致。新增 C# 已经 Godot 扫描、构建并生成 UID；编辑器插件退出仍报告下文已有的释放错误，扫描退出码为 0。
- 手写玩法代码中的含糊组合输入、普攻起手、成长更新、目录更新、外观应用和背包刷新方法已更名，调用、订退与文档同步；框架契约与生成流程保持原有边界。日志：`.godot/validation/official_input_{build,battle,profile,level_unit,smoke,level_render,ui_render,scan}.log`；仍有下文已登记的证书、故意失败注入及框架关停异常。

## 悟空造一资源复现与逐帧检查

官方源包地址与散列见 [OfficialReference](OfficialReference.md)，需先准备 Java、FFDec 26.3 和原始 Role_v7.swf；工具自行恢复包头并核对 SHA。以下更新悟空基础美术与五段攻击，不重生成其他角色或基础动作时序。重跑基础导出后，继续执行下节技能导出，恢复完整的共享图集与技能特效序列。

```powershell
$workPath = Join-Path $env:TEMP 'gamezmxy-wukong-export'
python Tools/LegacyMigration/export_official_wukong.py `
  --java 'S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe' `
  --ffdec (Join-Path $env:TEMP 'gamezmxy-ffdec/ffdec.jar') `
  --godot 'S:/Godot4/Godot4CSharp_console.exe' `
  --source (Join-Path $env:TEMP 'gamezmxy-official-role.swf') --work $workPath `
  --output Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong `
  --effects-output Godot/GodotProject/TheGame/Sprites/Effects/wukong/wukong_attack_effects.png
python Tools/LegacyMigration/import_official_wukong_attacks.py (Join-Path $workPath 'manifest.json')

New-Item -ItemType Directory -Force Godot/GodotProject/.godot/validation/appdata | Out-Null
$env:APPDATA = (Resolve-Path Godot/GodotProject/.godot/validation/appdata).Path
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --editor --path Godot/GodotProject --script res://EditorScripts/regenerate_collection.gd
dotnet build Godot/GodotProject --no-restore -v quiet
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility --script res://EditorScripts/validate_official_wukong_attacks.gd -- (Join-Path $workPath 'manifest.json')
```

逐帧检查覆盖全部 62 个源攻击帧 × 四种穿戴 × 两个朝向，共 496 次实际绘制；核对 24 fps 秒制关键帧、身体/武器帧号、固定金箍棒棒影已移除、原版弧光保留、朝向、根坐标及矩形覆盖。独立读取武器与剑气像素验证范围、3px 余量和 14px 最小厚度；检查判定窗口未早于可见剑气结束。真实 Area2D 验证近端命中、武器之外的剑气及残留命中、矩形外和关闭窗口不命中。带矩形轮廓的图片在 `.godot/validation/official_attacks/attacks_armor*_weapon*_facing*.png`；轮廓仅由验证脚本绘制。真实输入用 `--smoketest` 检查五段连击、独立输入冷却、无后续输入时按动画长度收招、空中第三段下劈和近距离命中；`--smoketest=ui` 检查全部 13 个玩法动作、四种穿戴及背包。

## 悟空技能资源复现

Python 环境需要 Pillow、NumPy；`--source` 使用同版 Role_v7，音频使用 Music.swf。后三个步骤依次消费前一步 manifest，所有解包文件留在忽略目录。

```powershell
$skillWork = Join-Path (Resolve-Path Godot/GodotProject/.godot/validation).Path 'wukong-skills-export'
$javaPath = 'S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe'
$ffdecPath = Join-Path $env:TEMP 'gamezmxy-ffdec/ffdec.jar'
python Tools/LegacyMigration/export_official_wukong_skills.py `
  --java $javaPath --ffdec $ffdecPath `
  --source (Join-Path $env:TEMP 'gamezmxy-official-role.swf') --work $skillWork
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject `
  --script res://EditorScripts/import_official_wukong_skills.gd -- (Join-Path $skillWork 'manifest.json')
python Tools/LegacyMigration/import_official_wukong_skills.py (Join-Path $skillWork 'manifest.json')
python Tools/LegacyMigration/export_official_skill_audio.py `
  --java $javaPath --ffdec $ffdecPath --source (Join-Path $env:TEMP 'gamezmxy-official-music.swf') `
  --work (Join-Path $skillWork 'audio') --output Godot/GodotProject/TheGame/Audios/SFX/wukong/skills
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --editor --path Godot/GodotProject `
  --script res://EditorScripts/regenerate_collection.gd
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility `
  --script res://EditorScripts/validate_official_wukong_skills.gd -- (Join-Path $skillWork 'manifest.json')
```

当前结果：122 个源动作帧 × 四种穿戴 × 两朝向，共 976 次检查 PASS、退出 0；五个独立特效逐帧双朝向及真实 Area2D 近端/范围外/关闭窗口 PASS。验证直接读取导入后的武器、能量或透明探针像素，检查实际矩形覆盖；截图带青色判定轮廓，位于 `.godot/validation/official_skills/`。技能资源更新后，原五段普攻 496 次检查再通过；四张图的原有 348 格像素逐一与 HEAD 对比相同。只有根证书读取错误，未发生资源解析或断言错误。

## 完整待机、领取动画与角色页签（2026-10-14）

完成上面的基础与技能导入后，使用基础导出的完整 529 SVG 补齐待机。只追加姿势并定向更新待机轨道，不重生成其他动作：

```powershell
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject `
  --script res://EditorScripts/import_official_wukong_idle.gd -- `
  (Join-Path $workPath 'manifest.json') res://.godot/validation/wukong_idle_manifest.json
python Tools/LegacyMigration/import_official_wukong_idle.py `
  Godot/GodotProject/.godot/validation/wukong_idle_manifest.json
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --editor --path Godot/GodotProject `
  --script res://EditorScripts/regenerate_collection.gd
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility `
  --script res://EditorScripts/validate_official_wukong_idle.gd -- `
  (Join-Path $workPath 'manifest.json') res://.godot/validation/wukong_idle_manifest.json
```

- 待机逐帧 PASS、退出 0：400 个身体/武器源图层帧与正式 AnimationPlayer 取样像素一致，另核对引擎透明边缘导入处理、100 个背包帧与完整 100/24 秒循环。四种穿戴 × 双朝向截图 `official_idle_contact.png` 已查看。前 108 格 × 四张图与修改前暂存像素相同，仅追加 8 个姿势；完整动画库验证通过。
- `HeroConfig.xlsx` 删除无消费者的 IdleEmoteDelay，经 Luban 导出；其余单元格值与暂存版本逐项相同。命名空间检查及已安装 XLSX Diff worker 的普通/公式模式均能读出新表头与变化；没有运行时兼容代码。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个既有框架警告；BattleTests 106/106、ProfileTests 85/85。Godot 扫描及构建日志显示完成，编辑器关停停留在既有插件释放问题后，终止该验证进程；未终止用户编辑器。
- OpenGL `--smoketest=ui` PASS、退出 0：外置领取动画按 0.8 秒上移 100px 并二次缓出淡出，暂停冻结、收益只入账一次、满包不播、结束回池与复用重置通过；`pickup_rising.png` 已查看。技能窗口仅显示出战角色，禁用角色的模拟信号也不能切页；主动/被动、暂停、余额订退和复用通过。日志 `form_pickup_skills_ui.log`。
- OpenGL `--smoketest=frontend` 与 `--smoketest` 均 PASS、退出 0：六格存档、损坏格、建档读档、导航，以及真实输入走跑跳和五段攻击通过。日志 `saveform_frontend_final.log`、`idle_input_final.log`。仍有下文登记的根证书与框架关停异常，UI/基础输入报告 4 个、frontend 报告 3 个 ObjectDB 泄漏。
- 引用验证：441 条路径、291 个 UID，23 个关卡几何节点与暂存版本一致。本轮 `git diff --check` 通过；`git diff --cached --check` 报既有 Luban 技能生成文件的空白，未手改生成物或 Git 索引。

## 技能准备、通用飘字与 UI 组件（2026-10-14）

本轮按第 00/10/50 章的作用域、显式会话与 GGF 池约定实现。飘字由 `World/FloatingText` 开始/结束会话，共用一个 `FloatingTextNode` 池；UI 已按后续用户要求收敛到完整面板，见下节。第 00/10/60 章旧 Presenter/UI 目录示例及第 40 章 HUD 根绑定示例未自行改写；当前用户要求与规范差异见 [ProjectTour](ProjectTour.md)。

| 检查 | 当前结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误；5 个框架既有警告及 NU1900 网络警告 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 85/85；两角色目录、费用、被动总效果、声音、11 个伤害阶段、五个动作/特效与 47 条事件的普通/无双位移、目标分支、独立攻击编号及非法数据拒绝 |
| `--smoketest=ui` | 最终 OpenGL PASS、退出 0；九种数字样式各 100 组相邻组合共 900 组，独立读取真实 PNG alpha 核对比例字宽、描边、完整高度及居中/左起；同池实例跨生命/十位暴击/魔法/闪避复用、暂停、作用域隔离、旧事件过滤、实际恢复品/蓝球领取通过 |
| UI 组件回归 | 最新背包根绑定 5、存档 3、HUD 5、技能 8；原背包出售/穿戴/详情、技能切页/暂停/余额/退订/复用、HUD 满值连续闪烁与失败清理继续通过 |
| 技能资源关联 | 五个正式动作、五个独立特效与 47 条事件的所属局部时间/跳转位置均处于真实动画长度内；七段 MP3 经 GGF SFX 代理播放、按编号停止；未接入施放 |
| `--smoketest=frontend` | 组件接线后 OpenGL PASS、退出 0；六格目录、建档/读档、选角、地图悬停、保存提示和放弃回滚通过 |
| `--smoketest` | 通用飘字合并后 OpenGL PASS、退出 0；真实五段连击、命中、死亡及池化伤害文字通过；TestArena 活动恢复节点退出场景通过 |
| XLSX Diff 1.6.4 | 四张技能战斗源表及两个 Luban 定义表在实际 worker 的普通/公式两种模式均可读；受控副本 SkillHit F6 单格变化准确识别。新事件表 905 个单元格；源表默认命名空间及 Luban 产物同步 |
| 资源登记 | 技能行展开、删除三个行子场景并新增装备槽 shader 后，既有 Collection Res 生成器在 editor 模式收集 440 个资源，无 basename 冲突，含事件表 bytes；入口脚本检查 editor 模式并等待扫描/导入完成后退出 |
| `validate_game_references.py --compare-staged-geometry` / `git diff --check` | 最新 450 条序列化路径、309 个 UID 通过；23 个关卡几何节点与暂存版本一致，diff 无空白错误 |

已经实际检查 `.godot/validation/floating_text_spacing.png`：九种 `111 / 108 / 888` 及 `10、17、24、36、59、70、99、1234567890`。宽字额外间隙默认 0px，窄字侧留白比例默认 1/6；统一公式覆盖 0–9，既有 PNG 不重写，首次使用后缓存字宽。背包、六格存档、悟空/唐僧/被动页截图亦已查看。

最终 UI 日志为 `.godot/validation/skills-source/final_ui.log`，组件 frontend 日志为 `combat_text/all_components_frontend.log`，Collection 为 `skills-source/final_collection.log`。系统根证书读取错误、编辑器插件 RefCounted/free 及关停 RID/ObjectDB 泄漏仍存在；UI 退出的 WebRequest ObjectDisposedException 和 4 个 ObjectDB 实例泄漏仍存在。HUD 缺绑定和 frontend 跨槽/损坏档属于故意失败注入；业务 PASS 与这些异常分开记录，未修改只读框架。

本轮已完成技能目录/面板、悟空动作/判定/特效/音效、伤害/动作/特效/条件分支与位移配置。学习记录、商店购买、扣费及实战施放依用户要求留待后续接入，当前不改存档格式。数据形状和源规则见 [OfficialReference](OfficialReference.md)。

### 技能面板直编与穿戴槽底图

- 将十五行全部展开到 `skill_form.tscn`，主动为五列 GridContainer、被动为六列；删除三个行子场景。预填全部名称、费用、按键、说明和图标；后续合并行脚本，由两个完整页统一绑定数据。编辑位置见 [ProjectTour](ProjectTour.md)。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个框架既有警告。`--smoketest=ui`：PASS、退出 0；新增所有行属于主场景、同页列对齐、列无重叠/溢出及长说明高度检查；将十项主动和五项被动的预填文字/图标扰动后重新绑定，全部恢复为真实表值。
- 武器与防具槽各用独立的局部 shader 材质；真实穿戴武器时只清除武器题签，穿戴防具后两槽均清除，分别卸装/关闭复用后恢复空槽题签，背包图标透明度保留。完整背包交易和旧 UI 回归继续通过。
- 独立 OpenGL 预览与像素检查 PASS、退出 0：不调用运行时数据绑定也可渲染三页；两张原槽图只清掉 765 个字样像素，渐变颜色与边框保持，occupied 从 true 切回 false 后逐像素恢复。截图 `skills_editor_{wukong,tangseng,passive}.png`、`equipment_slot_backgrounds.png`、实际 `inventory_wearing.png` 已查看。
- 日志在 `.godot/validation/inline_grids_{build,collection,preview,ui}.log`。UI 日志的 HUD 缺绑定仍是预期故障注入；根证书读取、编辑器插件关停及 WebRequest 关停异常/4 个 ObjectDB 泄漏仍存在，未修改只读依赖。

### 背包出售控件颜色（2026-10-14）

- 按用户最终参考，一键出售与确认/取消常态使用原版棕金渐变底图，悬停/按下保留原深棕纯色；禁用保持暗色，焦点只描边。品质上拉菜单为接近背包面板的深棕底与浅金字。主题与五个 StyleBox 外置到 `MainPack/Themes/` 及其 `Styles/` 子目录，重复状态共用样式；按第 40/50 章处理，未改业务或新增生产测试接口。
- `--smoketest=ui`：PASS、退出 0；背包出售、取消/Esc、确认、防重复、关窗复用及既有 UI 回归通过。日志 `.godot/validation/inventory_colors_ui.log`，实际背包、品质选项和确认框截图已查看。
- 一次性 OpenGL 预览：PASS、退出 0；真实鼠标悬停、按下、组合按下、焦点与禁用状态和菜单高亮截图已查看。脚本与图片仅在忽略目录 `.godot/validation/`，日志 `inventory_colors_preview.log`。预览仍有系统根证书读取错误；UI 回归的预期 HUD 缺绑定、WebRequest 关停异常及 4 个 ObjectDB 泄漏仍存在。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个框架既有警告；通过既有 `EditorScripts/regenerate_collection.gd` 收集 446 个资源，新增主题与样式六项，无 basename 冲突。日志 `inventory_theme_{build,collection}.log`；编辑器既有 RefCounted/free 与关停泄漏仍在。
- `validate_game_references.py --compare-staged-geometry`：457 条序列化路径、309 个 UID 通过，23 个关卡几何节点与暂存版本一致；`git diff --check` 通过。
- 样式审计：新主题与五个外置 StyleBox 均有正式场景引用，确认框 panel 通过局部覆盖使用；装备格的 tooltip_theme / StyleBoxEmpty 用于隐藏引擎外壳，保留。删除 PopupMenu 不支持的 `font_pressed_color`，其他颜色与描边保留用户调整；实际 Godot 默认主题查询确认该项无效，清理后无未知颜色项。检查脚本仅在忽略目录，日志 `audit_ui_theme.log`。

### UI 职责合并与目录整理（2026-10-14）

- 按用户要求删除 18 个过细脚本及对应 UID：背包身份/装备/属性列/经验、分类/格子/底栏/分页/品质按钮，存档 Grid/Button，技能 Header/Footer/行组/行脚本，以及仅含三个处理方法的 InventoryForm.Bag partial。背包保留个人资料、背包和出售确认三个完整面板；技能保留主动、被动两页；SaveForm 直接管理六格。
- `GameScripts/UI/Widgets/` 中九个保留控件连同 UID 归入 InventoryForm、BattleHud、MapForm 的 Components，场景引用同步更新。检查全部 Form，其余简单表单无需增加或合并包装。保留场景分组、外置主题及自定义 tooltip，不新增生产测试接口；目录与职责依据第 00/10 章，生成与验证依据第 50 章。
- SaveForm、SkillForm 根绑定经 `regenerate_ui_form.py <场景> --namespace GameLogic.UI` 生成。Godot `--headless --build-solutions --quit --no-window` 完成扫描及构建；`dotnet build Godot/GodotProject --no-restore -v quiet` 为 0 错误、1 个 NU1900 网络警告。
- `--smoketest=ui` 与 `--smoketest=frontend` 均 PASS、退出 0：完整背包交易、弹层定位、技能数据覆盖/切页/暂停/复用、HUD、六格目录/建档/读档/损坏格及菜单导航回归通过。背包、技能和存档截图已查看。日志 `.godot/validation/ui_consolidation_{smoke,frontend}.log`。
- `validate_game_references.py --compare-staged-geometry` 为 440 条路径、291 个 UID 通过，23 个关卡节点与暂存版本一致；`git diff --check` 通过。另对本轮所有 UI 场景的节点树和非脚本属性与暂存版本逐项比对，布局及样式一致。
- 已有根证书读取错误、编辑器插件 RefCounted/free、运行关停 WebRequest ObjectDisposedException 仍出现；UI / frontend 分别报告 4 / 3 个 ObjectDB 泄漏。HUD 缺绑定及跨槽/损坏档报错是预期失败注入，与本轮业务断言分开记录，未修改只读框架。

## 最近完整战斗结果（2026-10-13）

| 检查 | 结果 |
| --- | --- |
| Luban 导出、dotnet build | 通过；装备/掉落/恢复品改动保留；本轮补齐造一第五段攻击、击退与声音分组，更新三张源表及对应产物；构建 0 错误、5 个框架既有警告 |
| BattleTests | 106/106；含五段连击循环、空中第三段及落地重置、无双秒制一致性和妖猴范围边缘回归 |
| ProfileTests / LevelTests | 63/63、18/18；装备具名属性、官方随机端点、互斥/独立掉落概率、满包、穿脱替换、异角色限制、v6 穿戴引用读档与回滚、出售价格/重复/穿戴中/灵魂溢出均通过；跨三页整批品质出售、排除穿戴物、一次通知及整批溢出拒绝通过；成长表 55 级魔抗全为 0 |
| Godot OpenGL UI | PASS、退出 0；四分类切换、75 格三页边界、第三页选中与品质确认后整包出售；取消/Esc、背后输入阻挡、数据变更取消、重复确认拒绝、关窗复用清除旧交易及出售放弃回滚通过；上拉菜单在 940×590 和 1175×738 下对齐；1/3 项属性提示保持固定头部与正确价格位置；地面真实落地、接触恢复、上限、满包、同件领取、10 秒过期、暂停和池复用；恢复飘字颜色、间距、秒制上升与回池；八件透明图标、造三详情/操作菜单、真实穿脱和出售、C 开关及重映射/长按、订阅复用、死亡保存/放弃回滚均通过；保留战斗与失败清理回归 |
| 悟空美术与两层动画 | 本轮四张独立身体/武器图集、特效图及特效时序逐字节不变；9 个基础动作和 RESET 的完整轨道不变。仍只滤掉固定棒影元件 246，保留 203/254/282 的弧光；四个角色动画库契约通过 |
| 矩形判定与残影逐帧 | OpenGL PASS、退出 0；完整 8/8/11/20/15 源帧 × 四种穿戴 × 两个朝向，共 496 次绘制；矩形覆盖武器与剑气并集，外扩 3px、最小厚度 14px。150 次真实剑气区域命中（第三段 72、第四段 32），含 78 次后续残留帧命中；近端、范围外及关闭窗口检查通过，原有效时序保持 |
| 造一真实输入攻击 | `--smoketest` PASS、退出 0；按动画长度完成五段连击、空中第三段下劈、落地后从第一段重开；真实猴子命中与死亡链路通过 |
| Godot OpenGL level | PASS、退出 0；真实五波、守关怪、W 出口 → 成功结算 → 返回地图 → 读盘，经验 204，检查点收益保留 |
| Godot OpenGL frontend | PASS、退出 0；新增地图默认闪烁、悬停切换、移开恢复、透明角不误点和放大轮廓命中；保留开始、六格存档、选角、放弃回滚、保存提示复用与 GGF 声音检查 |
| 妖猴瞬间举棒 | OpenGL UI PASS；左右追击、平台踱步及范围边缘往返共 48 个绘制帧均保持 run |
| 大立绘后半段 / 顶部 BOSS 条 | OpenGL UI / level 均 PASS、退出 0；末段动态火焰完整、名称与条体整体顶部居中 |
| 升级动画蓝线 | OpenGL 逐像素 PASS、退出 0；14 组原画只移除 362 个边缘像素，28 帧保持 122×136、24 fps |
| 资源、配置与文档 | 382 条序列化路径、253 个 UID 通过，Level_1 的 23 个几何节点与暂存版本相同；同款粗糙/普通装备保留官方相同透明前景；非目标表格值保持一致，本地文档链接有效 |
| XLSX Diff 格式修复 | 最新全部 18 张游戏配置经已安装插件的 worker 在普通/公式两种模式验证，工作表、单元格计数及中文/新字段值均符合 XML；受控副本 H9 单格变化准确识别为 1 格；命名空间修复不改变 XML 语义和其他 ZIP 部件，重复执行无需修改 |
| 背包表 XLSX Diff | ProfileConfig 普通/公式两种模式只识别 F5/F6 两格变化；__enums__ 两种模式识别新增四个品质的 12 格，既有枚举值和 ID 不变；源表与 Luban 产物同步 |
| AI 原生几何与受击朝向 | AiBox 替换为 Godot.Rect2，106 项战斗单测通过；真实 AI 巡逻/追击/出招/收招锁向/高处守候/落地恢复/丢失目标/死亡回收通过（测试侧临时固定 AttackDesire=100，结束恢复，生产概率不变）；OpenGL UI 验证悟空和三类怪物双朝向/双攻击方向均保持受击前朝向、击退方向正确 |
| 攻击表 XLSX Diff | 本轮 AttackConfig / SoundConfig / __enums__ 保持默认命名空间；已安装 XLSX Diff worker 的普通/公式两种模式均正确识别 33/9/6 个目标单元格变化，其余单元格值与修改前一致 |
| 正式开发档 | 已按用户授权备份原件至 GameData.backup-before-v6-20261013-025550，一次性转存原累计经验 1020、灵魂 835；无运行时兼容代码。最终只读反序列化主档/备份并经 ProfileSaveHelper 重建均通过 v6；主档已被用户后续游玩推进为经验 1224、灵魂 1008，未覆盖该进度 |

## 已知限制

- 构建有框架既有的弃用警告；测试 NU1900 表示无法连接 NuGet 漏洞服务，与断言失败分开记录。
- AI 烟测曾在落地等待 6 秒内未出手而失败；复测中的范围与落地读数正常、行为恢复并通过。现测试夹具临时固定出手概率，只验证恢复能力，概率规则由确定性单测验证。
- 当前隔离环境的 Godot 进程会报告系统根证书存储读取失败；独立攻击渲染断言仍 PASS、退出 0，该错误不作为无错误运行忽略。
- UI 回归故意注入 HUD 缺绑定以检查失败清理，该段错误日志是预期夹具；必须继续到 SMOKE PASS 才算通过。
- frontend 回归故意注入跨槽写盘和损坏档案；拒绝写入与读取失败日志为预期。烟测使用隔离目录，不改正式档；用户授权的正式开发档一次性维护单独登记在上表。当前格式 v6，无旧格式运行时兼容。
- 引擎关停的 `DefaultWebRequestAgentHelper.Reset` 仍抛 ObjectDisposedException，并报告 ObjectDB 实例未释放。业务断言 PASS、进程退出 0，不能称为无错误退出；独立升级渲染检查无此异常。
- 历史关停还出现过 GC `0xC0000005` 与编辑器 `ComponentInsoector._ExitTree` 释放错误；依赖只读，未在业务层绕过或修改。
- 当前悟空实战仍只有基础动作与造一五段普攻；五项造一技能已准备完整动作、特效、判定和声音，尚未接入施放；造三攻击未迁入。规范第 20/30/50/60/70 章仍有旧属性名、存档版本和装配描述，规范正文未自行改写；本轮资源遵循第 40 章既有美术规则。

规范依据和依赖修改授权仅在 [第 50 章](ProjectGuidelines/50-FrameworkAndTools.md) 维护；本页只保留可执行命令、当前结果和限制。
