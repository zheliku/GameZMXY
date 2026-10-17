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

按修改范围选用 `--smoketest=level-cancel`、`ai`；规则单测无需打开编辑器。场景装配用 `--headless --script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，编辑器 Tool 再加 `--editor`；触发区用 `validate_level_triggers.gd`，动画库用 `validate_anim_libraries.gd`。有渲染器时，`validate_musou_flash.gd` 检查原版无双 19 帧透明动画；`validate_skill_layout.gd` 检查技能面板坐标、45px 图标、造三五入口及无双黑底板的空/半满/满值装配，`validate_level_up.gd` 检查升级蓝线裁剪、画面和注册尺寸。

XLSX Diff 1.6.4 的解析器无法读取带 `x:` 前缀的 SpreadsheetML 标签，会错误地显示零张表。导表工具仍能读取这类文件。遇到该问题时，用 `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py --write <文件.xlsx>` 将标签改为等价的默认命名空间；脚本逐项验证 XML 内容并原子替换，不重新生成单元格或样式。不带 `--write` 时只检查。已暂存的文件需要重新暂存修复后的版本，工作区修复不改 Git 索引；历史版本若含相同前缀，插件仍无法读取历史侧。

引擎截图写入忽略的 `.godot/validation/`；无双开场及后半段分别为 `musou_activation.png`、`musou_activation_tail.png`，升级为 `level_up.png`、`level_up_tail.png`，原帧/裁剪帧对照为 `level_up_frames.png`，BOSS 条为 `encounter/featured_health_bar.png`。通过 PASS/FAIL、退出码、错误与画面共同判断结果。

战斗背包为 `inventory_form.png`，品质上拉菜单为 `inventory_quality_sale.png` / `inventory_quality_sale_scaled.png`，出售确认框为 `inventory_sale_confirmation.png` / `inventory_sale_confirmation_scaled.png`，多属性详情为 `inventory_tooltip_attributes.png`，失败结算为 `settlement_failure.png`，成功结算为 `encounter/settlement_success.png`。成功页截图在真实检查点完成后立即采集，可能带有短暂的保存成功提示；提示按真实时间淡出，背包和结算暂停不会将它冻结。

装备背包与实际详情为 `inventory_equipment.png`，操作菜单为 `inventory_actions.png`，武器/防具同时穿戴为 `inventory_wearing.png`，绿色/蓝色恢复数字为 `recovery_numbers.png`；无名称的地面恢复品和装备为 `ground_pickups.png`；地图默认边缘闪烁的两端画面为 `frontend/map_current_low.png`、`frontend/map_current.png`。

## 左下菜单与无双底板（2026-10-17）

依据第 40 章资源装配、第 50 章原工具生成流程及用户明确的版本边界，保留造三左侧五入口，恢复 718.png 黑色长底板和旧条缩放；造一技能栏移至它上方。默认隐藏满值动画，运行时继续由 BattleMenuPanel.SetMusou 控制。本次没有修改战斗规则或新增生产观测 API。

| 验证命令 | 结果 |
| --- | --- |
| `python Godot/GodotProject/.godot/validation/battle_menu_review/audit_menu.py` | PASS：五个入口及题签节点与暂存版本一致；15 张入口图片与旧工程字节相同；底图、五题签与造三官方 OtherMatv3570 导出像素相同；黑底板与旧 718.png 字节相同 |
| Godot `--path Godot/GodotProject/.godot/validation/skills_runtime/project --rendering-method gl_compatibility --script res://EditorScripts/validate_skill_layout.gd` | PASS、退出 0：技能页、45px 空槽/已学图标、五入口、黑底板及空/半满/满值真实渲染；逐张查看画面 |
| 隔离编辑器 `--headless --editor --script res://EditorScripts/regenerate_collection.gd` | 原有 CollectionRes 生成器登记 462 项，新增黑底板资源常量；退出 0，仍有既有插件 Free/RefCounted 和资源泄漏日志 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | PASS：472 条路径、288 个 UID、23 个关卡几何节点 |

官方造一、造三完整 RoleInfo 导出、像素核对报告和引擎日志位于 `Godot/GodotProject/.godot/validation/battle_menu_review/`。本次画面位于隔离工程 `.godot/validation/skills_layout_hud_0.png`、`skills_layout_hud_1.png`、`skills_layout_hud_full.png`；只去掉测试实例业务脚本，按空/半满/满值设置显示，不读写玩家存档。这是场景装配验证，不替代下节已运行的真实技能学习专项；未重新运行无关的整套规则测试。

## 数据与战斗窗口统一命名（2026-10-17）

依据第 10 章命名、第 30 章攻击池所有权和第 50 章生成/验证流程，按用户本轮要求将 AttackInstance / EquipmentInstance 改为 AttackData / EquipmentData，并贯通参数、持有字段、集合及测试反射。InventoryBagPanel 使用 m_EquipmentDatas；ProfileModel 的唯一背包为 EquipmentDatas。保存字段、实例 Guid 和格式版本不变；配置范围校验收回 SaveDataUtility。GGF 窗口统一 Form，BattleForm 同步脚本/目录/场景、打开参数、UI 枚举、源表与资源常量；组件继续按 Panel / Bar 命名。规范旧示例未改写。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet test Tests/BattleTests --no-restore -v quiet`、ProfileTests、LevelTests | 186 / 154 / 18，合计 358 项通过 |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 隔离副本 0 错误、5 个既有框架警告及 NU1900；未构建正在使用的正式编辑器输出 |
| `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/BattleForm.tscn --namespace GameLogic.UI`、Luban 原有导表入口、CollectionRes 原有生成器 | Ge 生成五项根绑定；Luban 保留枚举数值 ID；CollectionRes 生成 461 项，正式资源常量只新增名称/路径替换及生成时间 |
| `node Godot/GodotProject/.godot/validation/check_battle_form_xlsx.mjs` | XLSX Diff 1.6.4 的值/公式两种模式共四项通过：界面UI.xlsx 两格、__enums__.xlsx 一格变化；其他 ZIP 部件不变，保留默认命名空间 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=frontend` | SMOKE PASS：真实启动、六格存档、选角、地图、BattleForm、回滚、提示复用与 GGF 声音通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest` | SMOKE PASS、退出 0：五段普攻、第四段松手不补第五段、走跑跳、实际命中与死亡、池化飘字通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui --skills-only` | 技能专项 PASS、退出 0：五项学习即时同步 BattleForm 彩色图标；五招输入、持续升龙、跑攻、火魔斩两分支/命中、声音与资源关联通过，仅隔离副本选取原有技能断言 |
| `python .godot/validation/prepare_battle_icon_check.py` | 正式图集 11 格逐像素等于官方元件 328；命名核对阶段场景仅名称和换行变化，后续底板与布局修复见上节 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 471 路径、288 UID、23 个几何节点通过；改名保留 UID |
| `dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes`、`git diff --check` | 通过；UTF-8 无 BOM、CRLF 检查通过 |

本轮引擎日志在 `Godot/GodotProject/.godot/validation/`：`battle_form_frontend.log`、`data_names_combat.log`、`battle_icons_skills.log`；已查看隔离工程 `.godot/validation/battle_icons_unlearned.png` 与 `battle_icons_learned.png`。官方 RoleInfo.setSkillIcon 对未学习技能保留灰色空槽，学习后切彩色；没有把空槽改成自制灰度技能图。后续已确认底板缺失与技能栏下移，修复及验证见上节。

技能专项按 `SMOKE-BATTLE-ICONS PASS` 判断覆盖范围，驱动器原有整套 UI 汇总文案不代表未执行断言也已通过。三次引擎日志仍有既有 WebRequest 关停 ObjectDisposedException，攻击/技能各有 4 个退出泄漏；前端的坏槽读取告警属于失败注入。CollectionRes 编辑器退出仍有已登记的 ComponentInsoector Free/RefCounted 与资源泄漏告警。完整 UI 的既有超时/恢复数字暂停失败继续保留下节记录；本轮未改 Framework、addons 或 Git 暂存内容。

## 技能输入、激活与具体状态分工（2026-10-17）

依据第 10 章职责与配套类型合并、第 30 章角色与状态分工，以及本轮 [沟通理解](WorkingNotes.md)，输入决策移出 IHeroBody。当前状态 TryGetActionInput 提供输入窗口，实体 MatchActionInput 匹配角色规则，状态消费请求并进行 GGF 转换。TryDropThroughPlatform 保留物理碰撞与位移能力；S+K 识别归输入系统。

本轮按后续反馈继续简化：删除 InputActionState / ButtonInput 快照，InputSystem 直接查询 Godot 的按下、按住和松开；消费仍按角色隔离。InputEventAction 是原生事件，本次物理帧采样无需先查 Input 再构造事件。Skill / SkillInstance / SkillExecution 行为基类已删除，原正式五招未实现其生命周期；只有测试派生类在使用，不能以这些测试为理由保留空壳。SkillData 与 SkillDefinition 随 SkillModel 定义，前者仅保存施放身份与四项数据，后者登记状态、动作和条件。技能工厂、活动集合、后台调度与空生命周期一并移除；ISkillActor 保留节点能力契约并继承原脚本 UID。具体行为和后续输入归 GGF 状态，独立特效由节点推进。未修改配置、存档格式或生成内容。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 隔离副本 0 错误；5 个既有框架警告及 NU1900。当前工程编辑器打开，未构建其输出目录 |
| `dotnet test Tests/BattleTests --no-restore -v normal` | 186/186；第五绑定、消费隔离、普攻规则、真实 GGF 五招与多阶段交接通过；阶段输入直接读取原生查询契约 |
| `dotnet test Tests/ProfileTests --no-restore -v normal` | 154/154；登记所有权、错误类型/重复绑定的原子拒绝、单次扣费、取消和新旧施放身份、同帧双边沿查询、正式五招时间轴与火魔斩旧回调隔离通过 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18；合计 358 项通过，未跳过测试。移除仅验证旧行为壳派生实现的样例，真实玩法回归保留 |
| Godot `--headless --editor --path <隔离副本> --build-solutions --quit --no-window` | 脚本扫描和构建均 DONE；退出触发既有 ComponentInsoector 的 Free/RefCounted 错误并滞留。核对 PID 63776 与完整工程路径后仅停止该验证进程；不记为正常退出通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui --skills-only` | 技能专项 PASS、退出 0：现有技能页/学习断言、五招真实输入、持续 W+J、跑攻、火魔斩两分支/命中/输入不排队、七段 GGF 音效和 47 条事件资源关联均通过。仅隔离副本增加截断条件，保留原断言，不执行其余 UI 测试 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 471 路径、288 UID、23 个几何节点通过 |
| `dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes`、`git diff --check` | 通过；UTF-8 无 BOM / CRLF 检查通过 |

本轮日志在仓库根 `.godot/validation/skills_runtime/`：`skill_data_build.log`、`skill_data_battle.log`、`skill_data_profile.log`、`skill_data_level.log`、`skill_data_scan.log`、`skill_data_engine.log`、`skill_data_format.log`。专项从原 UiSmokeScenario 中仅选择 CheckSkillsAsync；准备脚本为 `.godot/validation/prepare_focused_skills.py`，隔离补丁为 `focused_skills.patch`。正式调试脚本只同步取消方法名称，不增加测试开关或生产 API。

本轮技能专项通过，仍不能声称整套 UI 通过。此前 `input_names_ui.log` 在升级动画 Frame ≥ 22 等待处超时，退出 1；`input_names_ui_headless.log` 在恢复数字暂停断言失败后未正常退出，原因仍未定位。既有 Framework WebRequest ObjectDisposedException 与 4 个 ObjectDB 退出泄漏仍在本轮技能专项退出时出现。没有修补框架或插件，也未修改 Git 暂存内容；规范正文未改写。

唐僧目前只有目录和伤害定义，八戒、沙僧尚未接入正式招式；输入和技能机制测试不等于这些角色已经制作完成。

## 悟空技能实战与左下栏恢复（2026-10-16）

- 五招按官方源时间轴接入 SkillSystem/SkillModel：学习与不足提示、扣蓝、升龙取消和持续 W+J、跑攻、U/I/O、独立特效与火魔斩两分支。火魔斩禁用输入时仍推进游戏秒数，输入不排队。UI 观察唯一技能书，GameData v7 只新增已学技能 ID；无旧版运行时兼容。
- 技能面板对照 OtherMat_v9 完整渲染恢复原坐标与 FZCuYuan 字体；战斗图标来自独立 45px 元件。五个旧菜单按钮、题签、底图和位置恢复，按钮及题签与暂存版本逐块相同，法宝/宠物保留；仅无双与五个主动技能图标换官方资源。移除旧无双 shader/图集及确认未引用的旧页码等素材，保留应用图标。素材表与导出工具同步。
- `dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet` 140/140；BattleTests 165/165；LevelTests 18/18。主工程 `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` 0 错误、5 个框架警告；隔离副本另有 NU1900 网络警告。
- OpenGL `--smoketest=ui`、`level`、`frontend` 均 PASS、退出 0；涵盖购买、HUD/技能页同步、五招真实输入、火魔斩命中/缺失、七段 GGF 声音、五波通关、结算与当前格式重读。怪物前摇夹具只隔离已显示的旁观怪物，避免技能测试后场上其他怪物干扰伤害计时；池中隐藏实体不被冻结。日志位于 `.godot/validation/skills_runtime/`。火魔斩输入锁最终复测同样 PASS、退出 0，期间的 J/U 按键不补发；资源重扫后应用图标 UID 错误已消失。
- `validate_skill_layout.gd` 与 `validate_musou_flash.gd` OpenGL PASS、退出 0，原版 19 帧全部像素 alpha 与 GPU 输出一致。最亮帧角落低 alpha 柔光保留，不当成不透明底色。Collection Res 经既有生成器更新；资源检查为 471 条路径、284 个 UID，23 个几何节点与暂存版本一致；XLSX 默认命名空间检查及 `git diff --check` 通过。主工程与隔离副本扫描完成，既有插件 Free/RefCounted 与关停 WebRequest 错误仍单独记录，不声称无错误运行。
- 正式开发档已按授权备份至 `GameData.backup-before-v7-20261016-183208`，一次性保留累计经验 1428、灵魂 1179、11 件装备与穿戴引用；主档和 `.bak` 均经正式 DTO/SaveDataUtility 校验通过。只添加空学习记录，目录元数据不变。

依据第 10/40/50 章执行代码、资源与验证约定；第 40 章旧 shader、第 70 章旧版本描述由用户后续指令覆盖，规范正文未自行修改。

## 地面物品透明留白贴地（2026-10-16）

`GroundItemNode` 按非透明区域底边与实际碰撞体底边对齐，考虑图标缩放，通过 Offset 保留领取动画的 Position 轨道。袈裟/行者服原先悬空约 4.9 像素；原图、32×40 碰撞盒、接触范围和领取规则不变。规则依据第 40 章的资源与动画职责、第 50 章的真实引擎验证；没有修改规范正文或只读资源。

当前工程编辑器打开，以下构建及引擎检查在 `.godot/validation/ground_alignment/project` 隔离副本执行，避免 C# 热重载；使用烟测独立存档。

| 验证 | 结果 |
| --- | --- |
| `dotnet build <隔离副本> -v quiet` | 0 错误、5 个既有框架警告 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui` | SMOKE PASS、退出 0；逐像素检查全部 8 种装备、3 种恢复品与池复用的可见底边；落地、拾取上移淡出、暂停、满包、过期及背包回归通过 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 441 路径、278 UID、23 几何节点通过 |
| `dotnet format whitespace . --folder --include <本轮两个 C# 文件> --verify-no-changes` | 通过 |

已人工检查 `ground_alignment/ground_pickups.png`，各图标贴地；日志为同目录 `build.log`、`ui.log`。UI 缺绑定仍为预期失败注入；既有 Framework WebRequest 退出释放异常及 4 个 ObjectDB 泄漏仍在，本轮未修改框架。贴地修复无未完成项。

## Bindable 简化与 QFramework / GGF 核对（2026-10-16）

依据用户最新要求，删除全部手动补发通知 API 和旧/新双值回调；HP、最终属性、无双、换装、出售、回滚均使用正常赋值和集合操作。Property、List、Dictionary 的 Register / RegisterWithInitValue 统一为单参数，整数 priority 默认 0，越大越先执行，同优先级按注册顺序。只共用内部订阅列表，没有 BindableEvent、优先级枚举或第二个全局事件系统。

对照 QFramework 官方核心和 BindableKit 源码恢复列表标准操作、Move 和逐项通知，补充泛型 BindableDictionary、键比较器、转换方法及实时只读回调视图。集合不再为每次通知复制前后快照；字典输入复制，避免外部写入绕过通知。不移植 Unity/EasyEvent、旧格式化序列化或 QFramework IOC。Model/System/Utility 仍由 GGF 主流程 FSM GetData/SetData 持有与访问。

HeroSystem 成长/穿戴同步的优先级为 100，UI 默认 0；新增 UI 先注册的实测，证明派生属性先同步。删空 AbstractUtility，工具直接实现 IUtility；SaveDataUtility.Capture 显式接收 ProfileModel，不查询 Architecture；ExperienceCurve 独立注册，不在 ConfigurationModel 重复持有。扫描 195 份手写游戏 C#，核对 5 个 Model、10 个 System、4 个 Utility 及流程注册/移除：未发现 Model 查询 Model/System 或 Utility 反向查询架构。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 165/165；死亡、无双重置、属性重算与动作规则通过 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 118/118；整数极值优先级、稳定顺序、重复退订、分发中增删/嵌套赋值、初始化失败清理、标准列表/字典操作、只读视图、坏输入和真实换装/回滚通过 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18；共 301 项通过 |
| OpenGL `--smoketest=ui` | SMOKE PASS、退出 0；数值、换装、出售、无双、暂停、退订与池复用通过 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；启动、六格档案、地图、保存、回滚、作用域与生命周期通过 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、结局导航、自动保存与地图重读通过 |
| F6 `--headless --scene res://TheGame/Scenes/TestArena.tscn --quit-after 360` | 玩家与两只怪物就绪，独立装配正常，退出 0 |
| `validate_game_references.py --compare-staged-geometry` | 441 路径、278 UID、23 几何节点通过 |
| `dotnet format whitespace . --folder --include <手写文件清单> --verify-no-changes` | 214 份手写 C# 格式通过；UTF-8/CRLF、文档链接与 git diff --check 通过 |

代码全仓确认没有手动通知 API、双值绑定、BindableEvent、AbstractUtility 或旧字段反射依赖；烟测反射适配在测试侧完成，没有新增生产测试接口。459 个框架、插件和生成文件 SHA-256 未变；没有暂存或提交。第 00/10 章仍描述旧架构及整批 Changed 约定，本轮依据用户最新要求采用 Model/System 和单值同步绑定，未自行修改规范正文。多个独立状态源依业务顺序通知，不再承诺它们在每个属性回调中都同时改变。

日志在忽略的 `.godot/validation/qf_binding_review/`。既有 NU1900、CS8632 与 Framework WebRequest 关停 ObjectDisposedException 仍在；UI/frontend/level/arena 分别有 4/3/4/3 个退出对象泄漏。编辑器扫描退出 0，仍有 ComponentInsoector 的 RefCounted 释放报错；未修改插件。坏存档、旧档案写入拒绝和 HUD 缺绑定为失败注入，不将完整日志称为零错误。

以下条目保留历史验证结果；当前绑定契约以上节和 ProjectTour 为准。

## Model 写入职责与全项目 C# 整理（2026-10-16）

Model 只保存数据与查询，System 直接修改绑定属性。删除 Profile/Hero/Level/Save/Configuration Models 中的业务修改方法与对应纯转发；保留完整事务提交顺序。关卡规则测试调用正式 LevelSystem，仅用测试侧 SaveSystem 替身隔离磁盘；未增加生产测试 API。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 165/165；新增无双三字段提交后通知与相等值不重复通知 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 106/106；正式 System 奖励/穿戴/回滚、关卡计时与冻结、重复开始/结局保护 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18；合计 289 项通过 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 脚本扫描及构建完成，退出 0 |
| OpenGL `--smoketest=frontend` / `ui` / `level` / `level-cancel` | 四组 SMOKE PASS、退出 0；保存回滚、背包绑定与复用、整关结算和加载取消通过 |
| F6 `--headless --scene res://TheGame/Scenes/TestArena.tscn --quit-after 360` | 玩家与两只怪物就绪，独立模型/系统装配正常，退出 0 |
| `validate_game_references.py --compare-staged-geometry` | 441 路径、278 UID、23 几何节点通过 |
| `dotnet format whitespace . --folder --include <手写文件清单> --verify-no-changes` | 212 份手写 C# 全部通过；UTF-8 无 BOM、CRLF 通过 |

成员按字段、事件、属性、构造、生命周期、其他方法与嵌套类型归类，共整理 78 个类型。Roslyn 保留成员内容并核对语法标记；11 组跨字段/属性初始化顺序变化均无依赖（测试实例独立初始化、SaveModel 常量），构建与运行验证通过。相对暂存版本，180 个文件语法标记不变，31 个为上述业务及测试调整，1 个为测试磁盘替身；459 个框架/插件/生成文件的 SHA-256 与 Git 索引均不变。

日志与手写清单在忽略的 `.godot/validation/model_writes_{build,battle,profile,levelrules,scan,frontend,ui,level,cancel,arena,format,editorconfig,token_audit}.log`、`layout_manifest.json`。UI 测试同步改读现有内部关卡属性，不再反射已移除的旧字段。

仍有既有 NU1900 网络警告、BattleTests CS8632 警告、ComponentInsoector 编辑器退出释放错误及 Framework WebRequest 关停 ObjectDisposedException；frontend/UI/level/cancel/F6 分别报告 3/4/4/4/3 个退出泄漏。HUD 装配失败、坏存档和跨槽拒绝是预期失败注入，不将完整日志称为零错误。本轮遵循集中规范的只读边界和验证要求；第 00/10/70 章旧 Model 修改方法、架构及绑定约定与用户最新方向冲突，按用户指令实现，规范正文未自行更改。

## Model 按生命周期归并与系统职责（2026-10-16）

本轮实现以用户最新确认的合并方向为准：本局属性、HP/MP/WS 合入 HeroModel；成长、穿戴、背包、灵魂直接合入 ProfileModel，不保留子 Model 或兼容别名。ConfigurationModel、SaveModel、LevelModel 因数据归属和生命周期独立而保留。主流程 FSM 仍是唯一注册持有者。

HeroSystem 承担属性汇总、成长/装备联动、资源与无双规则；ProfileSystem 承担奖励、装备归属校验和检查点回滚；SaveSystem 接管手动保存的状态与过期结果校验。Model 保留内部 CRUD 和完整提交，对外只读，UI/Entity 修改经系统。删除正式 Model 中仅测试使用的三个恢复入口，夹具移到 Debug/ProfileTestData.cs，纯测试链接同一文件。导航回调与直接 GGF UI 调用的区别见 ProjectTour。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v:q -nologo` | 0 错误、5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v:q -nologo` | 164/164；含死亡时 HP/无双完整提交、无双倍率开启/结束 |
| `dotnet test Tests/ProfileTests --no-restore -v:q -nologo` | 106/106；含换装、升级、已穿戴实例重复领取拒绝、回滚完整性与绑定身份 |
| `dotnet test Tests/LevelTests --no-restore -v:q -nologo` | 18/18；三组共 288 项通过 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 扫描和构建完成、退出 0 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；六格保存重读、跨槽拒绝、模型同一引用、入关订阅与离关退订、回标题清理 |
| OpenGL `--smoketest=ui` | 最终构建 SMOKE PASS、退出 0；生命/魔法/无双绑定、穿脱与出售、预览、结算重试、取消、失败清理与重入 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、守关条、出口、检查点和地图重读 |
| OpenGL `--smoketest=level-cancel` | SMOKE PASS、退出 0；在途取消及迟到实体回收 |
| F6 `--headless --scene res://TheGame/Scenes/TestArena.tscn --quit-after 360` | 最终构建退出 0；玩家及两只怪物正常显示，无启动/运行空引用 |
| `validate_game_references.py --compare-staged-geometry` | 441 条序列化路径、278 个 UID、23 个几何节点通过 |
| 本地 Markdown 链接、UTF-8/CRLF、`git diff --check` / `git diff --cached --check` | 通过；本轮未修改 Framework/addons/生成物/配置/规范正文，原暂存区保留 |

F6 初次验证发现合并注册块时漏掉配置、输入、战斗、AI、掉落系统，导致玩家 OnShow 空引用；补回注册并修正清理分支的括号后，重跑通过。日志保留最终成功运行。

日志：`Godot/GodotProject/.godot/validation/model_merge_{build,battle,profile,levelrules,scan,frontend,ui_final,level,cancel,arena}.log`。

已知异常单独记录：UI 故意破坏 HUD 绑定以验证失败清理；frontend 故意注入坏档和跨槽旧档请求；编辑器退出仍有 ComponentInsoector Free/RefCounted 错误，运行退出仍有 DefaultWebRequestAgentHelper.Reset ObjectDisposedException 和 ObjectDB 泄漏警告。这些不是零错误日志。包源不可达时单测有 NU1900，BattleTests 另有原有 CS8632。未修改只读框架或插件处理上述问题。第 00/10/70 章旧架构描述与本轮用户指令冲突，已报告并保留规范正文。

## 英雄逐项属性与 UI 回调（2026-10-16，历史记录）

按用户最新要求，HeroRuntimeModel 更名为 HeroAttributeModel，删除 ActorAttributes、HeroLoadout 和装配工厂。等级、HP/MP/WS 与全部 16 项最终数值各自可绑定；Model 管来源、重算与钳制，HeroAttributeSystem 协调成长和穿戴。升级/回滚同时更新基础和装备，只重算一次；属性通知读取完整结果。Entity 即时 GetModel，Show 不传数据，Hide 仅清理表现。SaveDataUtility.Capture / RestoreCheckpoint 改为调用期间借用 IArchitecture。

删除 MutateWithoutEvent；保留内部静默提交与显式通知，用于事务一致性。UiRequestKind / UiRequestEventArgs 删除，窗口通过具名回调交给流程；仍由 GGF 打开窗口。关闭清空回调，拒绝迟到通知重新绑定，池复用保持唯一绑定。未添加生产测试接口，测试通过现有属性和反射检查生命周期。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v:q -nologo` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v:q -nologo` | 162/162；16 项属性通知、相等值、修正公式、完整重算与已发攻击冻结 |
| `dotnet test Tests/ProfileTests --no-restore -v:q -nologo` | 105/105；换装/升级自动同步、Buff 保留、检查点回滚观察一致性；同时恢复等级/装备时属性只发布最终结果 |
| `dotnet test Tests/LevelTests --no-restore -v:q -nologo` | 18/18；三组共 285 项通过 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 扫描、脚本注册及编译完成，退出 0 |
| OpenGL `--smoketest=ui` | SMOKE PASS、退出 0；最终代码下的穿脱、升级、资源绑定、结算重试、失败清理与重入通过 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；补充关闭零回调、重开唯一回调、迟到绑定拒绝；属性系统入关唯一订阅、离关完全退订 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、守关条、出口、结算保存与地图重读 |
| OpenGL `--smoketest=level-cancel` | SMOKE PASS、退出 0；在途显示取消与迟到回收 |
| F6 `--headless --scene res://TheGame/Scenes/TestArena.tscn --quit-after 360` | 独立 FSM 的属性模型/系统就绪，玩家和两只怪物正常显示，退出 0 |
| `validate_game_references.py --compare-staged-geometry` | 441 条序列化路径、278 个 UID、23 个关卡几何节点通过 |
| 本地 Markdown 链接、UTF-8/CRLF、`git diff --check` / `git diff --cached --check` | 通过；暂存区保留，Framework/addons/生成代码与配置无本轮改动 |

日志位于 `Godot/GodotProject/.godot/validation/attributes_{build,battle,profile,levelrules,scan,ui,frontend,level,cancel,arena}.log`。测试仍有 NU1900 网络警告；编辑器既有 ComponentInsoector 退出释放错误、游戏关停 Framework WebRequest ObjectDisposedException 和 ObjectDB 泄漏未纳入修改。HUD 缺绑定、跨槽和损坏档案仍为预期失败注入，不将完整日志称为零错误。第 00/10/70 章旧架构约定与最新要求冲突，按用户指令实施，规范正文未自行改写。

## Model 归并与数据所有权（2026-10-16，历史记录）

以下是上一轮结果；ActorAttributes 容器、HeroLoadout 和 UI 请求事件已由上方实现取代。

按用户最新要求将英雄最终属性、等级、HP/MP/WS 合并为关卡注册的 HeroRuntimeModel，删除 Vitals、MusouGauge 和私有 ActorModel。档案分别注册 HeroProgressionModel、InventoryModel、HeroEquipmentModel；灵魂并入背包资产，不再保留 PlayerModel 聚合层或 Wallet。怪物保留普通实例状态，扣血直接刷新自身血条。初始化与借用边界见 [ProjectTour](ProjectTour.md#所有者与初始化位置)。

集合采用 BindableList，换装、出售和回滚先提交全部状态再通知；新增数量不变的装备替换和跨模型回滚一致性测试。SaveDataUtility 与 ExperienceCurve 为共享 Utility；加载只返回 DTO 与来源，删除 SaveLoadResult。十二个导航/保存结果事件归并，持续状态走绑定，一次性跨职责请求/事实走 GGF Event。地面物品改为场景预设 32×40 盒；蓝球只回蓝，地面恢复品继续显示数字。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v:q -nologo` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/ProfileTests --no-restore -v:q -nologo` | 106/106，含集合身份与事务观察一致性 |
| `dotnet test Tests/BattleTests --no-restore -v:q -nologo` | 145/145，英雄资源与无双秒制规则通过 |
| `dotnet test Tests/LevelTests --no-restore -v:q -nologo` | 18/18 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 完成扫描与编译，退出 0 |
| OpenGL `--smoketest=ui` | SMOKE PASS、退出 0；背包穿脱/出售/分页、资源绑定、怪物血条、恢复品/蓝球、池复用和失败清理通过 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；新档/读档、地图保存提示、放弃回滚、模型移除与重建通过 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、守关怪、W 出口、结算保存和地图重读通过 |
| OpenGL `--smoketest=level-cancel` | SMOKE PASS、退出 0；在途显示取消、迟到怪物回收通过 |
| F6 `--headless --scene res://TheGame/Scenes/TestArena.tscn --quit-after 360` | 玩家与两只怪物就绪，退出 0；独立 FSM 注册模型、系统和工具 |
| `validate_game_references.py --compare-staged-geometry` | 441 条路径、279 个 UID、23 个关卡几何节点通过 |
| `git diff --check`、`git diff --cached --check` | 均通过；保留原暂存区，Framework/addons/生成代码无本轮改动 |

日志位于忽略目录 `.godot/validation/models_{build,scan,ui,frontend,level,cancel,arena}.log`。已查看最新 `ground_pickups.png`，图标底部贴地且没有名称或阴影。纯测试仍有 NU1900 网络警告和既有可空注释警告。编辑器退出仍有 ComponentInsoector 的 RefCounted/free 错误；游戏关停仍有 Framework WebRequest ObjectDisposedException 和 ObjectDB 泄漏。HUD 缺绑定、跨槽和损坏档属于预期失败注入，不能将完整日志称为零错误。第 00/10/70 章旧架构条款与本轮指令冲突，按用户当前要求实施，规范正文未自行修改。

## 地面物品与 Model 绑定（2026-10-15，历史记录）

以下记录当时实现；动态图标碰撞盒、Vitals 与聚合 PlayerModel 已由上方 2026-10-16 的实现取代。

按用户最新要求，用一个按图标非透明范围生成的矩形同时处理地形碰撞与拾取查询，删除额外 Area2D、脚底小盒、阴影及动画阴影轨。DropConfig 源表移除失效 GroundPickupRadius，再经既有 Luban 命令生成；其余单元格和 XLSX 包内文件保持不变，命名空间检查无需修复。

参考 QFramework 原源码实现当前使用的 BindableProperty：Value、Register、RegisterWithInitValue、UnRegister、默认相等比较及事务静默提交/通知。模型对外只读，钱包、成长、装备集合、穿戴、存档目录和关卡状态改为绑定；窗口关闭对称解绑。关联字段使用不可变快照，跨背包/穿戴/余额交易先提交再通知；存档 DTO 不变。没有移植 EasyEvent、注销句柄链或增加生产测试接口。第 00/10 章的禁用 BindableProperty / 统一 Changed 条款与本轮指令冲突，按最新要求实现，规范正文未改。

- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个既有框架警告。新增脚本经 Godot `--headless --editor --build-solutions --quit --no-window` 扫描、构建，退出 0；仍有既有 ComponentInsoector 的 RefCounted/free 退出错误。
- `dotnet test Tests/ProfileTests --no-restore -v quiet`：104/104；BattleTests：145/145；LevelTests：18/18。覆盖绑定初始化、相等值与 null、退订重绑、初始化失败清理、初始化期间更新、成长一致性、目录集合所有权、穿戴及出售事务和结局发布顺序。NuGet 漏洞服务不可达的 NU1900 与既有可空注释警告分别保留。
- 正式工程 `--smoketest=ui`：SMOKE PASS、退出 0。验证唯一形状贴合图标非透明范围且与拾取查询共用同一 Shape2D；实际落地、非英雄/异会话拒绝、满包保留及原地重试、暂停、0.8 秒上移淡出、过期和池复用均通过。背包实时灵魂、穿脱出售、HUD 成长/Go、技能钱包及窗口解绑重开通过。`ground_pickups.png` 已查看，物品底部贴地、无阴影。
- `--smoketest=frontend` 与最终表产物下的 `--smoketest=level`：均 SMOKE PASS、退出 0；覆盖存档目录关闭零绑定/重开一份绑定、设置关闭退订、读档重入、整关五波、前进提示、结局、检查点与地图重读。所有烟测使用隔离 Validation 存档。
- `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry`：441 路径、292 UID、23 关卡几何节点通过；`normalize_xlsx_namespaces.py Configs/GameConfig/Datas/DropConfig.xlsx`：0 文件需要修复。32 个手写 C# 格式化前后 token 哈希一致；`git diff --check` 通过。

烟测中的坏备份、过期会话、HUD 装配失败为既有失败注入；正常关停仍报告 Framework WebRequest 的 ObjectDisposedException 和 3/4 个 ObjectDB 泄漏，不能将完整日志称为零错误。最终 `--verbose --smoketest=ui` 再次 PASS，退出日志仍为 4 个 RefCounted 实例，没有报告 PhysicsShapeQueryParameters2D 泄漏。本轮未改 Framework 或 addons。日志在忽略的 `.godot/validation/bindable_{scan,ui,ui_final,frontend,level}.log`；使用方法见 [ProjectTour](ProjectTour.md#model-的属性绑定)。

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
- `GameScripts/UI/Widgets/` 中九个保留控件连同 UID 归入 InventoryForm、BattleForm、MapForm 的 Components，场景引用同步更新。检查全部 Form，其余简单表单无需增加或合并包装。保留场景分组、外置主题及自定义 tooltip，不新增生产测试接口；目录与职责依据第 00/10 章，生成与验证依据第 50 章。
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
- frontend 回归故意注入跨槽写盘和损坏档案；拒绝写入与读取失败日志为预期。烟测使用隔离目录，不改正式档；用户授权的正式开发档一次性维护单独登记在上表。当前格式 v7，无旧格式运行时兼容。
- 引擎关停的 `DefaultWebRequestAgentHelper.Reset` 仍抛 ObjectDisposedException，并报告 ObjectDB 实例未释放。业务断言 PASS、进程退出 0，不能称为无错误退出；独立升级渲染检查无此异常。
- 历史关停还出现过 GC `0xC0000005` 与编辑器 `ComponentInsoector._ExitTree` 释放错误；依赖只读，未在业务层绕过或修改。
- 当前悟空实战含造一五段普攻与五项主动技能；唐僧技能和被动购买尚未实现，造三攻击未迁入。规范第 20/30/50/60/70 章仍有旧属性名、存档版本和装配描述，规范正文未自行改写；本轮资源遵循第 40 章既有美术规则。

规范依据和依赖修改授权仅在 [第 50 章](ProjectGuidelines/50-FrameworkAndTools.md) 维护；本页只保留可执行命令、当前结果和限制。
