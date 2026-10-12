# 50 框架与工具

## GGF 框架

通过 `Framework/GodotGameFrameworkCore/Base/GF.cs` 的 `GF.*` 门面访问框架模块。常用模块与参考文档：

| 模块 | 门面 | 参考 |
| --- | --- | --- |
| 实体 | `GF.Entity` | `Godot/docs/EntitySystem.md` |
| UI | `GF.UI` | `Godot/docs/UISystem.md` |
| 事件 | `GF.Event` | `Godot/docs/EventSystem.md` |
| 状态机 | `GF.Fsm` | `Godot/docs/FsmSystem.md` |
| 音频 | `GF.Sound` | `Godot/docs/SoundSystem.md` |
| 资源 | `GF.Resource` | `Godot/docs/ResourceSystem.md` |
| 场景 | `GF.Scene` | `Godot/docs/SceneSystem.md` |
| 对象池 | `GF.ObjectPool` | `Godot/docs/ObjectPoolSystem.md` |
| 存档 | `GF.Archive` | `Godot/docs/ArchiveSystem.md` |

文档用于定位框架 API；如文档与实现不一致，以代码为事实并报告差异。不得在 `Framework/` 或 `addons/` 直接修补业务问题。

## 工具与验证

- 技术栈：Godot 4.7 .NET / SDK 4.7.2、C# `net10.0`、Newtonsoft.Json NuGet 13.0.4；渲染器 `gl_compatibility`、内置 2D 物理、视口 940×590、`canvas_items + keep` 拉伸。
- 默认 Godot 命令使用 `S:\Godot4\Godot4CSharp.exe`；headless 使用 `S:\Godot4\Godot4CSharp_console.exe`。编辑器命令：`"S:\Godot4\Godot4CSharp.exe" --path Godot/GodotProject --editor`。
- `Tools/Luban/` 为 vendored 工具；通过 `Configs/` 的脚本导表。
- `Tools/LegacyMigration/` 仅用于可追溯的一次性旧资源迁移；迁移结果登记在 `Docs/LegacyAssetMap.md`。
- 日常构建：在 `Godot/GodotProject/` 执行 `dotnet build`。新增 C# 后再运行 `"S:\Godot4\Godot4CSharp_console.exe" --headless --build-solutions --path Godot/GodotProject --quit --no-window -q`；使用显式 `--quit` 让验证编辑器在构建完成后退出。
- 导表命令：`Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`；也可由编辑器 TopMenu → Generate File → GameConfig File 执行。
- 改表时先导表再构建；涉及实体/动画资源时运行 `EditorScripts/validate_anim_libraries.gd` 与 Collection Res 生成器。
- 按改动风险运行相关单测或冒烟场景；验证完成后检查 `git diff` 与 `git diff --check`。
- 纯 C# 单测：`dotnet test Tests/BattleTests`（伤害公式、属性汇总、生命魔法、无双）、`dotnet test Tests/LevelTests`（阶段与调度）、`dotnet test Tests/ProfileTests`（档案、成长、出战装配、当前存档映射与格式拒绝、启动校验；读取正式 `.bytes` 产物）。
- 关卡真实流程：`-- --smoketest=level`（五波清场、守关血条、档案经验、W 出口到地图耗时、通关检查点），在途取消：`-- --smoketest=level-cancel`。编辑器装配与绘制：`EditorScripts/validate_level_scene.gd`（`--editor` 检查 Tool，`-- --snapshot --resize` 检查实际像素与宽屏取景）；路径与 UID：`python Tools/ProjectMaintenance/validate_game_references.py`，可加 `--compare-staged-geometry` 确认重构未修改用户已暂存的地形。
- 菜单与声音回归：`-- --smoketest=frontend`，通过真实鼠标覆盖标题→六格存档选择→读档→选角→地图→关卡、地点悬停与动画复位、声音开关持久化、设置暂停/继续/返回地图、放弃回滚、窗口池复用层级、过期点击事件、调试入口隐藏及 Music 组独占循环。非 headless 模式保存界面和光晕两次采样截图至 `.godot/validation/frontend/`。
- UI 与存档回归：`-- --smoketest=ui`，使用真实 GGF 窗口/实体池及流程验证 HUD 聚合刷新与退订、实体与窗口复用、升级注入、关卡事务（中途离开回滚、死亡写盘与自动重开）、主文件损坏回退 `.bak`、当前格式校验与不支持格式拒绝、加载取消和失败恢复；全部烟测在 `SmokeTestDriver._Ready` 选择唯一 `user://Validation/<模式>/<随机 ID>` 存档目录，不触碰玩家进度。以 `SMOKE PASS/FAIL` 判定断言，框架退出异常单独登记，不能用通过断言掩盖异常退出。
- 已知框架退出异常（与业务无关，单独登记）：关停时 `DefaultWebRequestAgentHelper.Reset` 访问已释放节点（`ObjectDisposedException`）；`level-cancel`/`ui` 退出时 GC 终结器 `0xC0000005`；`--build-solutions` 退出时 `ComponentInsoector._ExitTree` 释放 RefCounted 报错。
- 保留的可选区域触发器通过 `--script res://EditorScripts/validate_level_triggers.gd` 验证真实物理重叠、当前区监听、玩家过滤、单次激活及停止/重启；独立验证不改正式场景和源表。

### 本轮边界登记（2026-10-06）

- 用户要求改关卡源表并整理业务脚本目录；本轮生成代码/二进制仅由既有 Luban 流程更新，删除已移除 schema 类型遗留的 UID 元数据。未手改生成 C# 或二进制。
- 本轮只迁移手写 C# 及其 UID，并修复业务场景/资源引用，再运行 Godot 重扫与装配验证；保留素材和 bundle 根目录。历史独立评审文档已在文档整理中移除，边界登记保留于本节。
- 用户已明确授权本轮插件例外：仅在 `addons/TopMenu/GameFrameworkTopMenu.Generate.cs` 的 Collection Res 过滤链增加一行排除 Markdown，防止导航文档 `AGENTS.md` 被误当作同名资源；资源常量由原生成器重生成。`Framework/` 和 `Tools/Luban/` 未修改；其他插件仍遵守只读边界。编辑器构建日志权限和依赖退出异常按验证限制记录，不在业务目录绕过。

### 本轮边界登记（2026-10 架构重构）

用户明确授权以下框架例外（仅此范围，其余 `Framework/`、`addons/`、`Tools/Luban/` 未改）：

- `Framework/GameFramework.tscn`：只改 `Procedure` 节点的 `Procedures` 清单为 Launch、Level、LoadProfile、Preload、Update（`ProcedurePrelode` 改名 `ProcedurePreload`、`ProcedureGame` 改名 `ProcedureLevel`、新增 `ProcedureLoadProfile`）；`EnterProcedure` 不变。以后新增菜单/地图流程须再次授权。
- `Framework/GodotGameFrameworkCore/Json/EasySave.cs`：新增原子写与备份 API（`Serialize`、`WriteUserTextAtomicAsync`、`LoadFromUserWithBackupAsync`、`ExistsInUserOrBackup`、`DeleteInUserWithBackupAsync`、`BackupSuffix`）；原有方法未改，`ProcedureUpdate`/`DownloadComponent` 继续使用原方法。
- `Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs`：所有写入原子化并保留 `.bak`、读写按调用顺序串行、调用时刻序列化、读取回退 `.bak`、首次建档改为"目录与备份都不存在"、覆盖时同步重写目录、`SaveAsync/OverWriteAsync/LoadAsync/Delete` 返回 `Task<bool>`；`ArchiveCatalogue`/`ArchiveData`、文件布局、加密与 `GF.cs` 泛型参数未改。`Godot/docs/ArchiveSystem.md` 已同步。
- 一次性配置迁移脚本 `Tools/ConfigBootstrap/migrate_stat_tables.py` 已执行；生成代码与 `.bytes` 只由导表流程更新，框架修改范围保留在本节。

### 本轮业务整理（2026-10-10）

- 用户要求同步文档、整理目录并消除冗余。共享资源组注册集中到 `MainPack/Scripts/Startup/GameResourceGroups.cs`；流程数据包装 `ExperienceCurveVariable` 按独立类型移到 `GameScripts/Session/`，UID 由 Godot 扫描生成。
- `ResourceBar` 命名空间对齐为 `GameLogic.UI.Widgets`，`BattleHud.cs` 仅通过 `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/BattleHud.tscn --namespace GameLogic.UI` 重生成；未手改生成文件。
- 无双裁剪范围的 GPU 验证使用 `--script res://EditorScripts/validate_musou_flash.gd`，不加 `--headless`；对比图写入忽略的 `.godot/validation/`。其余框架、插件、源表和生成配置保持既有边界。

### 本轮入口界面（2026-10-11）

- 用户明确要求新增开始、选角、地图流程与 GGF 声音管理；框架例外仅修改 `Framework/GameFramework.tscn` 的 `Procedure.Procedures` 清单，加入 `ProcedureTitle`、`ProcedureCharacter`、`ProcedureMap`，入口仍为 Launch。其余框架代码、插件及 Luban 工具保持只读。
- 新界面与声音登记修改源表 `界面UI.xlsx`、`SoundConfig.xlsx`、`__enums__.xlsx`，生成配置和二进制仅经既有 Luban 命令更新。用户随后明确选择恢复官方出生点与战斗区域：同步修改 `LevelConfig.xlsx`、`__beans__.xlsx`，为阶段增加独立左界、为关卡增加音乐引用，修订第 20/30/40/60 章相关规则和事实。界面 `.cs` 样板仅由 `regenerate_ui_form.py` 从正式场景绑定生成。
- 旧项目仅复制本轮菜单素材和音乐，来源见 `Docs/LegacyAssetMap.md`；无双动画从场景内嵌资源提取为独立 SpriteFrames，保留当前材质参数。纯输入规则 HeroInput 与 Godot 输入适配共同归入 Entity/Heroes/Input，迁移携带原 UID，身体接口按依赖引用输入规则；未加入正式测试专用 API。

## Git

- `main` 受保护；工作分支使用 `feature/xxx`、`fix/xxx` 或 `art/xxx`。提交前缀用 `feat:`、`fix:`、`refactor:`、`art:`、`config:`、`docs:`、`chore:`。
- 素材迁移与逻辑改动分开提交；不提交引擎/构建产物、`user://` 数据和本机编辑器配置。`.gitignore` 保持覆盖 `.godot/`、`bin/`、`obj/`、`*.tmp`、`.vs/`、`*.user`。

## AI 与编辑器配置

`Godot/CLAUDE.md`、`.claude/`、`.kilo/` 中的配置用于其对应工具，不构成本项目规范来源。与 `Docs/ProjectGuidelines/` 冲突时，以根入口裁决规则为准。

### 本轮视觉与事件整理（2026-10-11）

- 按用户要求用旧项目图片/字体恢复主菜单、地图地点和左下角图标；删除右上角返回按钮及 UI 按键导航，修订第 40/60/70 章的导航规则和返回路径。删除 `MenuPage/MenuCommand` 轮询、无状态的 StartFormData 和仅转发采样的 GodotHeroInput；保留档案/关卡 Data 引用，动作名称集中到 `GameScripts/Input/InputActions.cs`。
- 设置登记只修改源表 `界面UI.xlsx` 和 `__enums__.xlsx`；生成配置、二进制、UI 绑定和资源常量均经原生成流程更新。新增 `SettingsForm` 使用现有流程清单，本轮未新增框架、插件或旧工程修改。

### 本轮存档页与代码风格（2026-10-11）

- 用户确认入口整理代码作为后续标准，规则补入第 10 章；共享提示/加载窗口、预加载生命周期、存档所有权和类型落位按同一职责约定整理。独立类型拆分保留原命名空间；生成 UI 绑定仍从场景重生成。
- 六格窗口由标题流程持有，不改 Framework 的流程清单；本轮没有新增框架、插件、旧工程写入。只在源表追加 `SaveForm`，经 Luban 更新配置和二进制，经 Collection Res 更新资源常量；复制素材登记在 LegacyAssetMap。
- 前端回归新增六槽隔离、手动保存、旧空目录映射、损坏槽禁用、旧档案跨槽拒写、统一声音开关、确认音停止、音乐长度、关闭悬停、帮助和共享提示回调释放检查。异常注入在验证目录内，不增加生产测试 API。

### 本轮职责归类与当前存档（2026-10-11，后续裁决）

- 按用户要求将流程键与包装集中到 `Startup/FlowData/`，存档按 `Data/`、`Slots/`，事件按 `Combat/`、`Navigation/` 分类；手写脚本迁移携带原 UID，Godot 重扫。配套小枚举随所属类型，怪物攻击归同一实体的 partial；第 00/10/30 章同步维护。
- 四栏选角使用旧角色长图与笔刷框，普通地图图像的 alpha 决定鼠标命中，悬停图独立显示；菜单与地图复用造梦一配乐。资源/绑定/配置分别通过原迁入脚本、UI 生成器、Luban 与 Collection Res 更新，无新增框架或插件修改。
- 用户明确取消旧存档兼容：移除 v0 DTO、迁移器、历史等级补偿与旧目录映射，只支持 v2；档案映射归 `Save/Data/`。格式校验、空槽新建和备份恢复都有验证，测试通过反射与现有状态机检查实际状态。
- 按用户要求，核对并清理本机 `Godot/app_userdata/GameZMXY/GameData/` 的两个 v1 档及目录、备份，共六个文件；只删除已验证编号与格式的文件，未清理 Validation 或其他设置。此处取代早先保留历史格式的规则和验证范围。

### 本轮选角悬停与扁平存档（2026-10-11，后续裁决）

- 用户要求复原参考图：完整选角卡片经 `export_official_character_cards.py` 从官方界面包渲染为 PNG，来源、散列与矢量编号登记在 LegacyAssetMap。场景使用 AtlasTexture 去掉遮罩外透明边，删除重复笔刷/姓名组件及返回按钮；UI 绑定与资源常量仍通过既有生成器更新，没有手改生成文件。
- 可点击控件使用 Godot 手形光标；动态禁用的存档格和保存按钮同步恢复箭头。花果山只扩大 LocationGlow 的场景 Scale，不改变普通图命中区域。
- 用户明确要求删除 ProfileSaveData 并扁平化：GameData 直接持有 ActiveHeroId、Heroes、Gold，配套 HeroSaveData 同文件；格式为 v3，不兼容 v2。存档方法明确写入/快照/读取/回滚含义，阅读顺序见 Save/README.md。本轮没有新增 Framework、addons 或旧工程修改。
- 前端回归用 Viewport.PushInput 的局部坐标分发真实 GUI 事件，在布局等待后提交鼠标事件，按下/松开在同一轮分发；验证四栏切换、关闭/复用、不可选角色拒绝导航与手形光标，不增加生产观测 API，也不以重试跳过失败。

- 本次格式平铺后，按用户此前清理旧开发存档的授权，核对并删除本机两个 v2（经验/金币均为 0）及对应目录、备份，共六文件；GameData 目录剩余文件数为 0，未清理 Validation 或应用设置。

### 本轮模块入口与表现整理（2026-10-12）

- 用户要求统一模块入口、辅助类型命名和按功能分类，明确拒绝整个模块按 Nodes/Rules/Resources 聚集；据此修订第 00/10/40/60/70 章。LevelRun 与 LevelController 合并为同一个 LevelModule（Progress 为同对象 partial），SaveModule 拥有目录状态和读取；辅助脚本按 Camera、Spawning、Stages、Markers、Exit 归组，迁移携带原 UID，场景与验证引用同步更新。不改变用户已暂存的地形、Git 索引或已有工作。
- 仅在源表 BattleConfig.xlsx 把已废弃的 ClearRestartDelay 替换为 MonsterHealthBarFadeDuration=2；配置 C# 与二进制经既有 Luban 脚本生成。BattleHud.cs 经既有 UI 绑定工具生成，资源常量经原 Collection Res 生成器更新。没有手改生成代码，没有新增 Framework、addons、Tools/Luban 或旧工程修改。
- 守关条删除入场弹窗与生命数字，改为左侧白字黑边姓名与右侧原笔刷条。华康海报体 W12 仅从旧 BossBlood 所用字体复制，路径、SHA256 和可重复迁入入口登记在 LegacyAssetMap。小怪两秒淡出与重受伤复位依据造梦西游一官方 BaseMonster.showHpSlip 和 TweenLite 默认缓动；不把旧项目字体宣称为官方提取字体。
- 通关返回地图的延迟来自旧 ClearRestartDelay=3 秒；删除通关等待，下一流程帧等待检查点即切地图，死亡等待仍由 DeathRestartDelay 决定。真实 OpenGL 关卡测得 W 到地图可见 49ms（含写盘），磁盘重读经验 204；完整验证与已知框架退出限制见 [ModuleRefactor](../ModuleRefactor.md)。
