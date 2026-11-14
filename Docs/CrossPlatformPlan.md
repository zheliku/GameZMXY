# Windows / Android 双端发布实施计划

状态：**A2英雄/背包这一批52张图已原路径替换为2倍同源重导，待用户审图；每批完成后停止。A1b位图恢复方法不采用，平台仍有来源限制，A1c未开始。用户明确以1920×1080验收，接受4K显示器铺满时放大。默认窗口已设1920×1080，940×590逻辑核心与keep视野保持。T1已通过，T2已有修改保留但未完成，T3～T5未开始。** 全库高清与扩图继续分批推进；本批不代表UI、独立特效、敌人或地形已高清。Android横屏16:9～21:9目标保留，正式宽屏仍须与T3关卡数据一起启用。

本计划取代 [CoopImplementationPlan](CoopImplementationPlan.md) 之后的“发布”方向。双人计划 A～E1 已经完成，本计划不改动它的数据形状，也不回滚任何双人成果。

**可勾选进度入口：[ArtProgress.md](ArtProgress.md)。** 按用户2026-11-15要求，逐项列出当前661个图像源文件和182份配套美术资源，每批完成后更新勾选、分类完成量和限制；目前图像52项已实施待审、3项来源/保真限制、606项待处理。该清单只展示状态，实施方案和停止点仍以本计划2.12为准。

---

# 第一部分　执行者须遵守的规则

## 1. 任务粒度与停止点

- 计划分为**大任务**（T1、T2……）和**子任务**（T1-1、T1-2……）。
- 一次只做一个大任务。按顺序完成其中所有子任务，跑完该大任务的“完成判定”，写交付说明，然后**停止**。不得顺手开始下一个大任务。
- 通常子任务之间不需要停下；美术前置阶段按2.12的A0～A5分批审查，A1内部的三类小样也分别停止，不连续自动生成。每批都有交付物、验收方式和停止点。
- 已核实且审查通过的数值、节点和路径按计划实施；若与当前源码或运行证据冲突，说明原因并提交修订，不盲目照抄，也不先做临时补丁。

## 2. 遇到计划未覆盖的情况

按下列顺序处理，不得跳级：

1. 再读一遍本计划的第二部分“设计依据与当前方案”和对应子任务。答案大多已经写在那里。
2. 读 [ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md) 和对应章节。
3. 如果仍然无法决定，**停止当前子任务**，在交付说明里写清：卡在哪个文件哪一行、有哪几种做法、各自的影响。不要猜，也不要先做一个“临时方案”。

下列情况必须停止并上报，不得自行处理：

- 需要修改 `Framework/`、`addons/`、`Tools/Luban/`、生成源或模板；**手工修改**生成代码也禁止。计划明确指定的 `regenerate_ui_form.py` 重新生成绑定代码不在此列。
- 需要新增 System、Model、Manager、Controller、Helper、Utility、接口或抽象基类。
- 需要修改存档格式（`GameData` 版本号）。
- 自动验证失败，而失败原因不在本子任务改动的范围内。
- 计划给出的坐标或数值在实际运行中明显错误（例如按钮跑出屏幕、怪物出生在墙里）。附截图，不要自己换一套数值。

## 3. 硬性禁止

- 不得新增：`TouchInputSystem`、`PlatformService`、`MobileManager`、`ScreenAdapter`、`InputRouter`、`DeviceHelper` 之类的类型或单例。本计划所有功能都由 Godot 原生节点、项目设置和现有类完成。
- 不得为触屏另写一条输入通路。触屏按钮只能通过 Godot 的 `action` 属性驱动 InputMap 动作，游戏逻辑继续只读 `InputSystem`。
- 不得在玩法代码里写 `OS.HasFeature("mobile")`、`OS.GetName() == "Android"` 之类的平台判断。唯一允许的平台分支已在第二部分列出（触屏层显示、开始页双人入口），而且只能用 `DisplayServer.IsTouchscreenAvailable()`。
- 不得加 `?.`、`== null` 检查去保护场景里一定存在的节点。场景结构保证的依赖用 `[Export]` 绑定，缺失就让它在 `_Ready` 报错。
- 不得改动现有键盘绑定（`project.godot` 的 `[input]` 段里已有的事件）。只能新增，不能删改。
- 不得修改 `Docs/ProjectGuidelines/` 下的规范文件，除非子任务明确写了“修改第 XX 章”，并且给出了要写入的原文。
- 不得为了“看起来更好”调整美术资源、HUD 布局或关卡构图。计划没写的位置一律不动。

## 4. 代码风格

- 遵守 [10-CSharp.md](ProjectGuidelines/10-CSharp.md)：微软命名，字段 `_camelCase`，XML 文档注释用中文，写“为什么”和契约，不写流水账。
- 生成绑定代码只通过 `python Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace GameLogic.UI` 更新，不手改。
- 编辑 `.tscn` 时保留原有 `unique_id`、`uid`、`node_paths`。新增节点不需要手写 `unique_id`，交给编辑器或保持缺省。

## 5. 每个大任务的验证（最低要求）

从仓库根目录执行，结果写进交付说明：

```text
dotnet build Godot/GodotProject --no-restore -v quiet
dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet
dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet
dotnet test Tests/LevelTests/LevelTests.csproj --no-restore -v quiet
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py <案例名>_scan --headless --editor --build-solutions --quit --no-window
python Tools/ProjectMaintenance/validate_game_references.py
```

上述最低要求用于恢复后的T2～T5及实际代码/场景接入。A0纯调查与文档修订只核对证据、文档一致性和Git差异；A1～A5按2.12验证离线素材与隔离预览，不把未运行的构建、真机或通关写为通过。

既有代码检查点：构建 0 错误 / 5 个既有警告；规则测试 Profile 339、Battle 197、Level 19 全部通过。恢复实施时数字下降须查清，旧结果不代替修改后的验证。

各大任务另有专项验证，写在各自的“完成判定”里。烟测命令统一形式：

```text
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py <案例名> --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after <帧数> -- --smoketest=<模式>
```

`<案例名>` 自取，格式 `xplat_<任务号>_<内容>_<日期>`，例如 `xplat_t2_ui_20261114`。

## 6. 交付说明必须包含

1. 改了哪些文件（每个文件一句话说明改了什么）。
2. 对照本计划的子任务逐条说明完成情况；没做完的写“未完成”和原因。
3. 验证命令和结果数字（不是“通过”两个字，要有数字或 PASS 行）。
4. 截图路径（涉及画面的任务必须有）。
5. 发现但没处理的问题。

不要另建交付记录文件；结果写进 [Validation.md](Validation.md) 顶部，按该文件既有格式。

## 7. 必读文件

开始任何大任务前读完：

- [ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md) 及其中引用的 00、10、40 章。
- 本计划第二部分全部内容。
- 该大任务列出的“涉及文件”。

---

# 第二部分　设计依据与当前方案

## 1. 用户决策

| 编号 | 问题 | 决定 |
| --- | --- | --- |
| D-1 | 发布平台 | Windows（键盘，1920×1080/16:9验收）+ Android（横屏触屏，16:9～21:9；重点19.5:9、20:9）。平板另行适配。 |
| D-2 | 宽屏 | 保留940×590逻辑核心，高度590、有效世界视野上限改为1377以覆盖21:9。当前适配范围内不接受由旧画布不足造成的空白/黑条；素材向外扩展，原画保留。此前1312/20:9决定已被用户新要求取代，正式启用仍在T3。 |
| D-3 | Android 双人 | Android 只提供单人。双人仍然只在 Windows 键盘上提供。 |
| D-4 | 左侧方向控件 | 四个方向箭头（`TouchScreenButton`），不使用 `VirtualJoystick`。 |
| D-5 | 资源子包 | 首发版本不拆子包，所有资源进主包。 |
| D-6 | 技能按键样式 | 触屏技能键使用图标，不使用文字。 |
| D-7 | Android HUD | 左下菜单面板在 Android 上不出现；背包、技能、设置改为右上角图标按钮；无双改为专用圆形按钮，用半透明「涨水」填充显示进度。左上状态面板保留。 |
| D-8 | 两套 UI 的管理方式 | 同一套组件脚本 + 两套布局场景，开窗时按设备二选一。见 2.7。 |
| D-9 | 美术高清与扩展 | 原始文件不变；高清版本保持内容与逻辑定位，允许增加像素；扩展版本只在原内容外围新增环境。全部661项评估清晰度，其中600项无需扩画布。优先原矢量/高精度来源，其次位图增强；静态背景可做2倍母版，运行纹理逐类实测，不统一翻倍。规格和验收见2.12。 |

## 2. 设计理由与核验依据

### 2.1 统一输入：InputMap 是唯一的汇合点

项目已经满足 Godot 的统一输入方案：

- 所有玩法输入都经过 `InputSystem`（`TheGame/GameScripts/Systems/Input/InputSystem.cs`），它通过 `GodotInputSource` 读取 `Godot.Input.IsActionPressed / IsActionJustPressed / IsActionJustReleased`。
- 窗口快捷键（背包、技能）通过 `InputEvent.IsActionPressed(...)` 读取（`BattleForm.Logic.cs:59-60`、`InventoryForm.Logic.cs:206`、`SkillForm.Logic.cs:155/162`）。

Godot 4.7 的 `TouchScreenButton`（源码 `scene/2d/physics/touch_screen_button.cpp:327-349`）在按下时**同时**做两件事：

1. `Input.action_press(action)` —— `InputSystem` 的轮询读取立刻生效；
2. 向视口推送一个 `InputEventAction` —— `_UnhandledInput` 里的 `IsActionPressed` 也能收到。

所以触屏按钮只需要设置 `action` 属性为现有动作名，**一行逻辑代码都不用写**，背包、技能、攻击、跳跃、无双全部自动生效。这就是 Godot 的统一方案，也是大型开源项目（例如 Godot 官方 demo、SuperTux 的 Android 移植）的共同做法：输入层只认“动作”，设备层只负责产生动作。

### 2.2 为什么不用 VirtualJoystick

已读 4.7 源码 `scene/gui/virtual_joystick.cpp`：

- 只要摇杆位移超过圆形死区，就按下对应方向的动作，没有扇区角度设置。
- 本项目上方向 = `interact`（升龙斩 W+J、出口交互），下方向 = `move_down`（下跳 S+K）。`InputSystem.ConsumeAttack` 和 `ConsumeJump` 都要求 `!UpHeld`（`InputSystem.cs:230, 240`）。
- 结果：向右跑时手指略微偏上，就会误触发升龙斩，或者普攻、跳跃被拦截。

四个独立箭头没有这个问题，而且和截图中造梦西游手游的布局一致。`passby_press = true` 让手指在箭头之间滑动时自动切换。

### 2.3 宽屏目标：940×590逻辑核心，覆盖到21:9

Windows按1920×1080（16:9）配置和验收；它是物理窗口/全屏输出尺寸，不将原逻辑坐标改成1920×1080或1280×720。Android覆盖16:9～21:9横屏，平板另行适配。采用canvas_items；正式keep_height与1377上限在T3就绪后启用。

| 显示尺寸/比例 | 高度590时的逻辑宽度 | 每侧较原940宽增加 |
| --- | ---: | ---: |
| Windows 1920×1080 / 16:9 | 1048.89，验收按1049 | 54.44 |
| 手机 2160×1080 / 18:9 | 1180 | 120 |
| 手机 2340×1080 / 19.5:9 | 1278.33 | 169.17 |
| 手机 2400×1080 / 20:9 | 1311.11 | 185.56 |
| 手机 2520×1080 / 21:9 | 1376.67，视野上限取1377 | 218.33 |

静态背景导出画布建议1408×590，原940×590置于(234,0)，留出约15.67px/侧的21:9绘制余量；画布宽度不是玩法视野宽度。941×591源图按其真实尺寸保护，不裁为940×590。

`keep_height`不会消除4:3平板上下留边，1377上限也不承诺任意超长窗口无留边；这些不在本轮支持范围。原图内刻意绘制的黑色、水墨和半透明暗幕必须保留，不能把它们当空白删除。Godot原生窗口留边无法靠default_clear_color或扩图覆盖，须让支持比例的视口实际展开，T3负责闭环。

`LevelCameraNode.ViewSize`已读取实际视口并计入Zoom；相机、阶段门、刷怪和锁定范围仍须在T3按1377整体复核，不能仅据这个getter断言相机无需调整。

### 2.4 宽屏与关卡数据、美术的依赖

`LevelGatesNode.Initialize`仍要求阶段宽度至少容纳实际世界视野。原940～1150宽阶段无法直接接收1377视野，刷怪点也可能从屏外变成屏内。这属于T3，不在本次美术盘点中修改玩法。

- 原1312/372增量和附录A旧坐标不能继续使用；1377相对940增加437，所有门、刷怪点须重新读取当前场景、配置、碰撞和地面后计算，不能对旧表做统一加减。
- 11关整关宽1222，小于1377。背景扩图不等于新增可走区域；取景、物理边界和装配契约需单独协调，不能用换图代替。原T3-3的放大相机方案不再作为既定实现，见T3-3。
- 五张1440×690天空虽比1377宽，仍会随视差滚动露出右端。本次已核对11关场景、原系数和画面：优先扩图，保留现有视差，不采用把0.13等系数降到约0.02的旧方案。
- 关卡1～9的大部分前景/地形已有约4700～5100宽，不因手机变宽一律重绘。10、11关的四张短前景/地面列为端部复审候选；是否扩以及扩多少，先结合最终镜头与物理墙验收。

### 2.5 UI：设计尺寸不变，锚点适配

- 所有窗口根节点已经是全屏锚点（`anchors_preset = 15`），这一点不用改。
- 但它们的子节点大多是固定坐标（`anchors_preset = 0`，位置按 940×590 摆放）。keep_height 下视口变宽，固定坐标的内容会贴在左边。
- 处理原则：**整块居中，不拆布局**。每个窗口把现有内容放进一个已有的或新增的 940×590 居中容器（`anchors_preset = 8` 居中，`offset` 为 ±470/±295），背景图另行铺满。SkillForm 已经这样做了（`Content` 节点 `anchors_preset = 8`、`offset_left = -470`），这是现成的参照。
- 战斗 HUD 不居中，而是按屏幕角落锚定：P1 状态区和菜单区贴左，P2 镜像区、Go 提示贴右，Boss 血条和连击数保持居中或贴右。详见 T2。

### 2.6 Android HUD 与触屏布局（D-7）

Windows 保留现有造三 HUD。Android 不显示左下菜单面板，改成下列布局：

| 区域 | 内容 | 输入或数据来源 |
| --- | --- | --- |
| 左上 | 现有状态面板（HP/MP/EXP/头像/等级） | `BattleStatusPanel`，不变 |
| 左下 | ←、→、↑、↓ 四个箭头 | `move_left`、`move_right`、`interact`、`move_down` |
| 右下大键 | 攻击、跳跃 | `attack`、`jump` |
| 右下围绕 | 技能 1～4，只显示图标 | `skill_1` ～ `skill_4` |
| 右下按键簇上方 | 无双圆形按钮，圆形内半透明水量从下往上升；满值沿用现有闪光 | `musou`；现有 `HeroState.Ws/MaxWs` |
| 右上 | 背包、技能、设置三个图标按钮，不带文字 | 复用 `BattleMenuPanel` 已有的三个事件 |

武器、宠物、文字 Caption 和左下技能栏不进入 Android HUD。`m_Go`、Boss 血条、连击数和无双立绘仍由 BattleForm 统一管理。

### 2.7 两套 UI 的管理方式（D-8）

采用成熟 UI 方案的共同原则：**视图可以有多套，数据绑定和交互事件只有一份**。不是做两个 BattleForm、两个 UIFormId，也不是复制一套 TouchBattleMenuPanel。

- `keyboard_menu_panel.tscn` 与 `touch_menu_panel.tscn` 是两个布局场景，根节点都使用现有 `BattleMenuPanel` 脚本。
- `battle_form.tscn` 同时实例化 `MenuPanel`（键盘）和 `TouchMenuPanel`（触屏）。`OnOpen` 只用一次 `DisplayServer.IsTouchscreenAvailable()` 选择显示和绑定哪一个，另一套不解绑时不订阅任何 HeroState。
- 状态面板、P2 面板、Go、Boss 血条、连击和无双立绘继续由原节点负责。Android 没有双人布局（D-3）。
- `BattleMenuPanel.BindHero/UnbindHero`、三个菜单事件和无双状态通知由两套布局共用。图标仍来自 `battle_skill_icon_frames.tres`；未学习灰色，角色没有的直接技能键隐藏。
- 不新增 TouchControls 脚本、TouchMenuPanel 类、UIFormId 或平台 Manager。只有 BattleForm 和 StartForm 允许读取设备类型。

### 2.8 触屏按钮与窗口生命周期

- 所有方向/攻击/技能/无双键使用 `TouchScreenButton`，`action` 直接填现有 InputMap 动作，`visibility_mode = TouchscreenOnly`。引擎同时更新动作轮询和 `InputEventAction`，InputSystem 与 `_UnhandledInput` 都无需新增通路。
- 已核对 4.7 源码：按钮在 `_input` 阶段接收触摸，**早于 Control GUI**。背包、设置的全屏遮罩本身不能阻止下层触屏键，不能靠遮罩兜底。
- 因此 BattleForm 的 `OnCover/OnPause` 必须隐藏 TouchMenuPanel（隐藏会释放已按下的手指），`OnReveal/OnResume` 再按当前设备恢复。具体状态字段和顺序见 T4-4。键盘布局不变。
- Windows 下 `DisplayServer.IsTouchscreenAvailable()` 只在开启“鼠标模拟触摸”时为真；Android 恒为真。开发时可在本地编辑器开启该项调试双布局，**不得**把 `emulate_touch_from_mouse` 提交为项目默认值。

### 2.9 Android 双人入口

- 开始页“双人”按钮在触屏设备上隐藏：`StartForm.Logic.cs` 在打开时设 `_double.Visible = !DisplayServer.IsTouchscreenAvailable()`。这是除 BattleForm 布局选择外唯一一处设备判断。
- 已有的双人存档在 Android 上读取：不阻止，正常进入；只绑定 P1 的 HUD 和触屏输入，P2 没有键盘时站在原地。这是有意接受的边界，首发不处理，交付说明注明。

### 2.10 资源子包

已核实：

- `addons/asset_bundle/export_plugin.gd:44, 107, 123` —— `export_enabled = true` 的 bundle 会被排除出主包，单独写成 `subpackages/*.pck`。
- 运行时 `ProcedureUpdate.cs:102-106` 从 `OS.GetExecutablePath()` 所在目录的 `subpackages` 读取。Android 上可执行文件在 APK 内部，这个目录不存在。
- 6 个 bundle（`TheGame/{Audios,DataTables,Entitys,Scenes,Sprites,UIs}/*Bundle.tres`）全部默认 `export_enabled = true`。

决定：6 个 `*Bundle.tres` 都写 `export_enabled = false`。此时：

- 导出插件跳过拆包（`export_plugin.gd:110` `if (!has_export_enabled_bundle): return;`），资源全部进主包。
- `ProcedureUpdate.TryLoadLocalSubpackagesAsync`（`ProcedureUpdate.cs:928-937`）找不到清单时只记一条 Info 日志并继续，不需要改代码。
- Luban 配置 `ConfigSystem.cs:52` 和本地化都用 `FileAccess.Open("res://...")` 读取，进了主包就能读。

`.bytes` 和 `.txt` 不是 Godot 导入资源。导出时须在 Android 和 Windows 预设的 `include_filter` 中写 `*.bytes, *.txt`，否则不会被打包（T5-2、T5-3 执行）。

### 2.11 调研依据与边界

- [Godot 4.7 Multiple resolutions](https://docs.godotengine.org/en/4.7/tutorials/rendering/multiple_resolutions.html)：横版 2D 推荐 keep_height；Keep、KeepHeight、Expand 的黑边行为以该文档为准。
- [Godot 4.7 TouchScreenButton](https://docs.godotengine.org/en/4.7/classes/class_touchscreenbutton.html) 与 4.7-stable 源码：原生多点触控、InputMap action、TouchscreenOnly；本计划的 `_input` 遮挡处理来自源码，不凭经验猜测。
- Godot 4.7-stable `scene/gui/virtual_joystick.cpp`：确认内置 VirtualJoystick 存在，但其四向动作没有角度扇区，故 D-4 不采用。
- [SuperTux](https://github.com/SuperTux/supertux)：大型开源横版游戏同时发布桌面和 Android，说明设备差异应留在输入/表现层，不为手机复制一套玩法。它不是 Godot 项目，不照搬其实现。
- [godot-mobile-example](https://github.com/scottpetrovic/godot-mobile-example)：Godot 键鼠/触摸并存示例，采用同一游戏逻辑和触摸控件；它是小型示例，不作为大型架构依据。
- 未找到可核实、与本项目操作一致的“造梦西游手游”开源仓库；不引用来源不明的反编译工程。Android 按键布局以用户提供的截图、D-7 决策和本项目现有官方素材为准。

2026-11-14 实施前复核补充：

- 本机实际引擎为 `4.7.2.stable.mono.official.ed1daf0bf`，SDK 为 `Godot.NET.Sdk/4.7.2`、`net10.0`。GGF 以源码随仓库保存，README 指向 `NuoYan/GGF`；程序集 `0.0.0.0` 不能证明上游版本，没有可核实的上游 Tag/Commit。QF 为本地 `GameScripts/Arch/` 职责契约，未引用 QFramework 包。
- [Godot 4.7.2 VirtualJoystick 源码](https://github.com/godotengine/godot/blob/4.7.2-stable/scene/gui/virtual_joystick.cpp#L142) 再次确认按分量正负触发方向，没有方向扇区过滤；当前上方向参与组合技，保留 D-4。
- [Godot 4.7.2 GLES3 源码](https://github.com/godotengine/godot/blob/4.7.2-stable/drivers/gles3/rasterizer_gles3.cpp#L427) 将视口外区域直接清为黑色，`default_clear_color` 只影响视口内背景，不能改变窗口留边。最初D-2/T2-3据此接受20:9外黑边；现由用户16:9～21:9支持范围取代，支持范围内须展开视口并补足美术。
- [SuperTux Android 构建](https://github.com/SuperTux/supertux/blob/master/.github/workflows/android.yml) 和 [输入入口](https://github.com/SuperTux/supertux/blob/master/src/control/input_manager.cpp) 证实共用游戏工程、按玩家读取动作控制器；不将其 SDL 实现描述为 Godot InputMap。
- [Mindustry MobileInput](https://github.com/Anuken/Mindustry/blob/master/core/src/mindustry/input/MobileInput.java) 与 [DesktopInput](https://github.com/Anuken/Mindustry/blob/master/core/src/mindustry/input/DesktopInput.java) 共用 InputHandler，分别组织触摸图标、菜单位置和键盘提示。可借鉴输入/视图差异停留在边界、玩法状态共享的原则；本项目无需照搬其专用输入类。
- 本轮检索“造梦西游 Android”未找到可核实的对应开源实现，泛词检索主要为 Flash、工具及 Pygame 仿制；没有据此宣称验证了手游布局。本次消息未附截图，后续布局先按 D-4/D-7 做出可审查画面。

**2026-11-14 用户裁决：** T1 通过；T2 先适配 UI，T3 完成门、刷怪点和相机边界后再启用宽屏及上限，以保持每次交付可游玩。原 T2-1/T2-2 已移至 T3-0；T2 的宽屏布局用独立 UI 预览验证，不改变正式关卡视野。


### 2.12 美术前置阶段：完整清单、扩展规格与分批审查

**当前边界。** 用户认可A1a后授权继续全库1080p适配，并再次要求每批先交审。正式图按最新指令**原路径替换，不设hd目录或后缀，保留UID；旧版留Git和离线备份**，取代A0/A1a的正式图不覆盖口径。本批只接入已验证的两英雄外观和专用背包52张图；位图恢复未通过的方法停止推广。来源、进度和验证记入既有文档，不清理无关UI/测试代码。总体目标仍包含全部美术。**提高分辨率不能改变已有图像内容，只有原画外围扩图允许新增环境。** 不新增资源Manager/System、第二套加载机制或两套平台业务场景。

#### 2.12.1 旧方案问题与 gpt.md 的项目化裁决

旧方案已正确区分输出与逻辑坐标、保留原文件并识别扩图对象，但仍有四项缺口：把600项“不扩画布”写成“保持原样”；统一要求最终图合回旧像素，阻止高清中心升级；未核实SWF内部矢量/位图差异及200px装配契约；批次从地图扩图直接推广，缺少高清重导、位图增强与运行纹理验证。以下替代旧口径，不沿用旧A1直接生成。

| 参考建议 | 本项目裁决 |
| --- | --- |
| 统一逻辑分辨率、按比例扩展 | 保留940×590逻辑核心。1280×720只是参考例子；1920×1080是物理输出，均不迁移原运动、碰撞和UI坐标。 |
| canvas_items + keep_height | T3与门/刷怪/相机一起启用；21:9按1377逻辑宽校准，1312只够20:9。当前仍是keep。 |
| Expand填满平板、Cover裁切旧图 | 不采用；平板另行适配，原图主体、地图内容、文字和地形不能裁切。只能裁新增外围余量。 |
| 分层Parallax2D、循环背景、TileMapLayer | 复用已有Parallax2D及原生碰撞。天空未证实可无缝重复，保持原视差；不为美术重建关卡。 |
| 2倍美术母版 | 静态背景可采用；角色/特效/图标逐类评估。大图集不代表单帧高清，普通插值不代表增加细节。 |
| UI锚点与安全区 | 复用GGF CanvasLayer、原Form和Godot控件；锚点已有改动保留，系统栏/刘海与触控留T4/T5实测。 |

#### 2.12.2 全量盘点：高清化与扩画布分别判断

复核现有清单并重新扫描：**661个图像源文件，656 PNG、1 JPG、4 SVG**，排除`.godot`、bin/obj。既有1182份引用文本及44张关卡覆盖图作为历史证据复用；本轮没有重跑它们。覆盖分类合计为9张静态扩图、48帧演出适配、4张条件候选、600张不扩画布，另新增1套选角侧景。**661项都进入清晰度评估；600不是高清豁免数。** 四张SVG含只读插件/工程图标，纳入盘点不意味着授权修改。

| 领域 / 文件数 | 高清处理与检查 | 宽屏覆盖处理 |
| --- | --- | --- |
| UI / 225 | 背景、面板、头像、选角卡、技能与物品入口分别查来源；地图地点查普通/悬停全帧及alpha命中，字图保留笔画。原生Label/字体和StyleBox直接检查1080p显示，不栅格化为新图。 | 4张全屏背景扩；48帧无双处理覆盖；其余173张不扩画布，仍评估清晰度。选角另做侧景。 |
| 角色、敌人、Boss、装备外观 / 116 | 以动作单帧而非图集总尺寸评估；优先既有SWF/SVG导出，保留姿态、装备身份、逐帧遮挡及原点。 | 不扩画布，不改变物理/攻击范围。牛魔局部效果不是屏幕背景。 |
| 动画、战斗特效、球与飘升 / 189 | 优先原时间轴和原生滤镜重导；嵌入位图另判。逐帧检查透明度、模糊/发光、首尾与连续性，禁止逐帧AI重画。 | 保留透明边、padding、注册点；局部效果不因屏幕变宽就补满。 |
| 背景、地形、前景 / 37 | 分别查天空位图与地形/前景shape、sprite依赖；地形重导保持绘制位置及已修接缝，不动碰撞。 | 5张共享天空扩；4张短关候选待镜头裁决；其余28张不扩画布。 |
| 装备、物品、消耗品图标 / 79 | 官方透明位图不等于矢量；保留轮廓、配色、品质、边距和地面贴合。先找更高精度原图，再试保真增强。 | 不扩画布；触屏按钮复用已审技能图标。 |
| 数字与文字贴图 / 10 | 现为旧PNG重打包的数字；评估单字有效像素、描边和基线。保留十格顺序、比例字宽与脉冲显示；字图不交给AI重写。 | 不扩画布，保留透明间距。 |
| bhpic、主题SVG、工程/插件图标 / 5 | 主题SVG沿用原生渲染；其余按实际消费者判断。插件只读、无消费者候选仅登记，不借本轮删除。 | 不扩画布。 |

逐项路径/尺寸/引用继续复用[661项CSV](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/classified_inventory.csv)和同目录JSON。其历史“保持原样”标签仅表示无需扩图；高清来源尚未逐项确认，不把它当高清结论。A1开始后在这份既有盘点的后续记录中补源文件/散列/元件、矢量或位图、单帧像素、逻辑显示尺寸、处理与验收结果，不另建平行计划。当前尚不能给出661项中“可矢量重导”的准确总数。

#### 2.12.3 四张静态UI背景及选角侧景

下列路径相对`TheGame/Sprites/`。尺寸先表达**原密度的覆盖画布**，不是全部正式运行图规格。高清后可以使用k倍像素及相同逻辑映射。候选可在隔离目录区别处理类型；正式图按最新指令直接替换原路径，UID保持，不留hd并行版本。原始基线冻结在Git/离线备份，不能把源基线与当前运行PNG混为一份。

| 编号 / 源图 | 原尺寸 / 消费者 | 覆盖画布与内容边界 |
| --- | --- | --- |
| A-UI-01 `UI/frontend/title_backdrop.png` | 941×591；`start_form/Frame/Background` | 候选1409×591，原内容置(234,0)。保留人物、标题、墨迹、菜单黑色构图及原映射比例，不硬裁成940。2倍母版2818×1182，原内容范围1882×1182。 |
| A-UI-02 `UI/frontend/journey_map.png` | 940×590；`map_form/Frame/Background` | 1408×590，原内容置(234,0)；2倍2816×1180，原内容1880×1180置(468,0)。只外延海岸/远山/海水，不新增岛屿、路线、入口、文字或地点光效。背景逻辑左移234，地点/图鉴仍使用原Frame坐标。 |
| A-UI-03 `UI/skills/skills_background.jpg` | 940×590；`skill_form/Content/Backdrop` | 同上1408×590/2816×1180；保留棕色水墨、上下黑带、表格和按钮位置。扩展图输出PNG，不反复有损保存JPG。 |
| A-UI-04 `UI/settlement/settlement_backdrop.png` | 940×590；`settlement_form/Content/Backdrop/Background` | 同上；延续外围蓝色水墨，原“4399”等笔画保留但不复制到新边缘，胜败图、成绩条和按钮不重绘。 |

1408是制作余量：21:9需要1376.67逻辑宽，剩余约15.67/侧。标题的941×591并非940×590，需结合现有TextureRect映射确认1409×591实际显示覆盖；不得仅因母版尺寸足够就判验收通过。高清与扩图的两套保护口径见2.12.8。

**A-UI-05选角侧景：** 当前是近黑ColorRect加四张人物卡，无整幅背景。新增1408×590（可选母版2816×1180）的低对比水墨侧景，中央940×590保留原底色/透明关系，不横向拉伸卡片，不改235×590显示槽、人物姓名、1P/2P与底部按钮。接入可用被动TextureRect，`mouse_filter = Ignore`，不新增脚本。

存档941×591透明窗框、背包图`inventory_panels.png`的有效alpha范围(110,53)～(865,551)、设置卷轴、删除框和图集padding均不需要填满屏幕；原画刻意黑色必须保留。它们需要时评估高清，仍通过居中、底层画面和全屏原生遮罩适配。

#### 2.12.4 五张共享天空：覆盖范围与高清密度分开

源均1440×690。复用`sky_coverage.json`及11关几何：原点为0且世界宽不少于视野时，右端需求为`V + scroll_scale.x × (scene_width − V)`；V取1377作保守校准。须计入原点、缩放、实际可达区间，短关不机械套公式。

| 编号 / 源图 | 复用关卡 | 原系数下需求（向上取整） | 覆盖规格 |
| --- | --- | ---: | --- |
| A-SKY-01 `Levels/zm1_01_huaguoshan/level_1_bg_end.png` | 1、2、3 | 最大1819，1关1809 | 候选2048×690，向右扩608；原内容仍在(0,0)。 |
| A-SKY-02 `Levels/zm1_04_bat_cave/bat_cave_sky.png` | 4 | 1819 | 同上，保留洞穴光照。 |
| A-SKY-03 `Levels/zm1_05_qixing_cave/qixing_cave_sky.png` | 5、6 | 1819 | 同上，保留洞穴轮廓。 |
| A-SKY-04 `Levels/zm1_07_shore/sea_shore_sky.png` | 7、8、9、11 | 7～9为1819；11单独复核 | 同上，不复制原龙形焦点。 |
| A-SKY-05 `Levels/zm1_10_bull_demon_valley/bull_demon_valley_sky.png` | 10 | 1661 | 可统一2048×690，保留约0.49456滚动关系。 |

2048是覆盖余量，并非高清结论或2次幂硬约束。可选2倍母版4096×1380，原内容范围2880×1380；若运行图用2倍，只对绘制密度补偿，不将其世界覆盖误当4096，也不改变Sprite注册点、视差速度及原alpha。原尺寸扩图可合回旧1440×690；高清扩图则使用已审高清基线，不能再合回低清中心。

[11关端部对照](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/level_end_contact_final.png)来自11关×940/1377宽×起点/总右界=44张固定SubViewport图：1377下1～10关总右端露底，11关底层天空仍覆盖；940下均无清屏色。它证明图层覆盖缺口，不证明阶段推进或通关已验，实际末阶段可能更早停镜。后续仍须按T3真实可达范围验收。

#### 2.12.5 短关四张条件候选

| 源图（`Levels/`下） | 原尺寸 / 世界覆盖 | 处理条件 |
| --- | --- | --- |
| `zm1_10_bull_demon_valley/bull_demon_valley_front.png` | 1880×590；x=-30～1850，总右界1951 | 总右端有101px装饰缺口；最终可达镜头确实看到时才补边。 |
| `zm1_10_bull_demon_valley/bull_demon_valley_ground.png` | 1896×95；x≈-16.05～1879.95 | 总右界少约71.05px，但末阶段右门1814.95仍在图内，不能认定当前一定缺地。 |
| `zm1_11_hidden_dragon_palace/dragon_palace_front.png` | 944×504；x=9～953，世界宽1222 | 无清屏露底不代表前景连续；镜头范围定后再续边，不复制中心宝剑。 |
| `zm1_11_hidden_dragon_palace/dragon_palace_ground.png` | 1300×97；x≈-43.4～1256.6 | 原关内足够；若镜头越墙，需不可玩封闭侧景。 |

四图都评估高清，但扩展长度待T3-3取景裁决，不先盲画。龙宫1222小于1377，放大相机塞入旧世界会改变人物尺度/可见高度，不作为默认方案。绘出地面不等于扩大玩法范围，禁止据此移碰撞、墙、平台或刷怪点。

#### 2.12.6 无双：高清与48帧覆盖复用同一原时间轴

悟空、唐僧各24张940×590，含半透明暗幕、光束、人物和“无双开启”，不能独立AI重绘48帧。`export_official_musou_activation.py`已核对：Role_v7的WsEffect59，选中角色55/58并隐藏另一人，补齐24帧，固定舞台940×590、注册(470,295)，原生24fps；背景52来源有既有登记，尚未验证可独立拆层后重合。

先查59→52/55/58的依赖和滤镜，试原时间轴公共背景分层，并同时评估高密度原生栅格化。关键帧1/8/12/24先验，再整段24帧。若分层不能保持现有alpha、遮挡和内容，则用同一时间轴离线合成宽帧；选定一条运行路径，不让原全幅暗幕与新全幅暗幕重复叠加，不增第二个暂停时钟。仅背景外侧可续暗幕/光束，人物与字形使用原源，不AI生成。

48帧RGBA8总像素成本：940×590约101.6MiB；1408×590约152.1MiB；再全2倍约608.4MiB，尚未计mipmap与运行压缩。不能假定只加载当前英雄：现有battle_form同时引用两套，应查实际依赖驻留后实测；不以PNG压缩大小代替显存。此项先试公共层节省重复像素，再决定运行密度。

#### 2.12.7 来源与工具核实：事实、候选和未知

**SWF是容器，sprite/shape也可能引用位图或滤镜；不能把“来自SWF”写成“全部可矢量高清”。** 以下为本轮读代码与既有来源登记的结论，不是本轮已完成2倍重导。

| 类别 | 已核实的来源/入口 | 当前结论与待查项 |
| --- | --- | --- |
| 两英雄战斗、装备外观、背包预览 | `export_official_wukong.py`、`export_official_tangseng.py`已有`sprite:svg`分层；悟空导入用Godot `Image.load_svg_from_string`；`export_official_inventory_preview.py`复用分层。Role_v7/backpack_v2版本散列见OfficialReference、LegacyAssetMap。 | **已存在矢量导出链，可优先试重栅格化。** 仍需检查SVG的image依赖、原生滤镜和当前清理/遮挡步骤；不承诺每个子层均为纯矢量。现有viewBox、父矩阵和帧映射须保留。 |
| 地形/前景 | `export_official_level_art.py`包装主程序shape56/60等；`export_official_qixing_level_art.py`处理115/116/117；`export_official_remaining_level_layers.py`保留源矩阵。 | **源shape/显示列表重导候选。** 查bitmap fill与滤镜后决定精度；保留已修地面单列接缝、透明边及当前人工动画。 |
| 敌人、Boss、独立技能特效 | `export_official_ground_monsters.py`、`export_official_bosses.py`、`export_official_final_actors.py`及`tangseng_effects`等原生FFDec/装箱入口。 | **原源和原生导出链有登记；纯矢量比例未确认。** 按动作追子元件；含模糊/发光的效果优先原生渲染，不能直接改SVG尺寸代替滤镜验证。 |
| 原生位图：技能底图、徽章、升级字、装备图标、部分天空 | `export_official_skill_ui.py`直接提取bitmap375/586及569的ARGB数据；`export_official_equipment.py`用`-export image`保留透明前景；蝙蝠窟导出器明确提取天空bitmap18，后续关卡工具同样另导天空image。 | **这些直接提取项是位图，重新提取不会产生新细节。** 其余天空逐文件映射尚待核实；先找更高精度同内容原件，再试保真增强，不能整批称矢量重导。 |
| 标题/地图/结算、数字及部分HUD | `import_frontend_assets.py`逐字节复制旧`Bg1.png`、`183.png`；`import_battle_ui_assets.py`复制结算623.png；`gen_damage_numbers.gd`拼接旧单字PNG。 | **已确认当前直接来源是旧位图；更上游矢量/高精度原件未确认。** 标题、地图、结算旧文件本轮可见；不因源文件存在就声称找到高清源。 |
| 选角卡、地图地点、技能小图 | `export_official_character_cards.py`渲染完整511/515/518/521；`export_official_map_hints.py`、`export_official_coastal_map.py`、`export_official_skill_ui.py`保留显示列表、原矩阵/全帧。 | **可追溯原组合，不等于全部纯矢量。** 查字体、image、mask、filter依赖；人物、字、地图地点不准AI重画。 |
| 4张现存SVG与实时文字 | 两张设置抓手SVG、工程和插件图标；运行字体沿用现有Font/Theme。 | SVG优先原生栅格化；抓手可评估导入精度。实时文字检查字体大小/渲染，不能把它们混入低清PNG批处理。 |

本轮确认Godot为`4.7.2.stable.mono.official.ed1daf0bf`，既有FFDec jar路径存在；未执行任何美术导出。当前`.godot/validation/`未找到SWF或大于100KB的源XML，历史命令所用临时源缓存不能当作现在可重跑的证明。A1前先定位或按OfficialReference取得同版本源、核对散列与依赖；找不到则标“来源待确认”，停止该来源的重导，不改用AI重画中心。

工具也不是统一的只写临时目录入口：选角卡、背包预览等会直接写正式目录；无双虽有`--output`，生成的资源路径仍按`TheGame`相对路径计算。试制前读清输出链，优先复用既有提取/栅格化步骤，将输出重定向到隔离候选目录；确需小改既有作者工具时留在后续授权批次，不重跑会覆盖正式资源的默认命令，不另造通用资源系统。

#### 2.12.8 原始、高清、扩展的验收口径

| 产物 | 允许变化 | 验收方式 |
| --- | --- | --- |
| 原始基线 | 冻结在Git/离线备份，不修改基线字节 | 基线SHA256保持；记录原解码数据、尺寸、源帧/注册点和场景变换。最新授权允许已审高清PNG替换正式原路径，UID保持；不得因此覆盖离线基线。 |
| 高清版本 | 可增加像素、改善采样；图像内容不变 | 优先原矢量/高精度源按原父矩阵重导。和现有图在相同逻辑尺寸/固定参考点叠加，比较人物姿态、五官、衣装、武器、配色、笔画、地图/地形边界、遮挡和alpha。允许栅格抗锯齿差异，不要求跨分辨率像素差0，也不以“语义差不多”接受改画。 |
| 原尺寸扩展版本 | 仅原画外新增环境 | 原解码区域按既定偏移合回，保护区RGBA像素差0；只在新增区修缝。不得为了接缝修改旧内容。 |
| 高清后再扩展 | 已审高清基线之外新增环境 | 先单独审定高清中心，再冻结其像素/散列；扩展后对此高清保护区像素差0。不能把旧低清图粘回后称整个成品高清。 |

位图路径先找同内容高精度来源；没有时只试保真超分/去压缩噪声等恢复候选。普通插值是尺寸转换基准，不是新增细节。**禁止在核心区生成原图没有的纹理、人物细节、字形或地形，禁止风格化重画。** 对照原图、普通插值和候选的1080p实际显示及100%/200%裁片，出现轮廓、颜色、笔画或结构改变即退回；无法保真或看不出有效改善时保留原图并登记限制，不承诺所有位图都能恢复真实高清。不得为了提高锐度制造白边、颗粒、透明黑边或动画闪烁。

扩图沿已审基线外围续画，禁裁主体、模糊补边、镜像重复和机械拉伸；不新增交互内容。接缝在100%/200%及实际输出下无竖缝、色带和光照突变，背景节点不拦输入。ECPA此前已出草稿，但保护区与接缝未过审，没有合格成品；调用成功不等于流程已验证。本轮不调用生成接口，后续若用图像技能则按当时可用入口执行。

#### 2.12.9 高清替换必须保持的资源契约

依据第40章及实际消费者。A0只调查是历史边界；当前英雄密度接入见2.12.15，仍保留200逻辑格、六列和原定位，不以放宽检查代替适配。

| 契约 / 实际证据 | 后续试制和接入要求 |
| --- | --- |
| `HeroAppearanceResource.ApplyAtlas`强制宽1200、200px格、6列，按高/200设置Vframes；战斗与专用背包共同调用 | 2400宽图会直接被拒，不能覆盖双倍PNG。1倍重导可先保持网格，但只算当前密度改善。真正2倍运行图须先审查原类的最小密度/网格调整方案及第40章约束，验证后再推广；不预加管理器或第二套装备绑定。 |
| 英雄SVG viewBox与源父矩阵、身体/武器独立层、遮挡裁切、帧索引 | 像素增密保持相同逻辑viewBox和帧序，不按每帧可见框居中，不丢透明帧。只对视觉节点补偿像素密度，实体根、碰撞、速度/跳高不缩放；原遮挡及已做的特效清理仍有效，不能因重导恢复被项目有意删掉的内容。 |
| `AtlasTexture.region/margin/filter_clip`、Sprite `offset/centered/scale`、H/V帧数 | 源像素字段须按k核算，逻辑位置保持；显示尺寸应为像素尺寸/k，再叠现有表现变换。使用Sprite补偿时通常offset随k、视觉scale除k，不能只改纹理。固定Rect的TextureRect保留逻辑尺寸和比例，检查expand/minimum size；不能直接按图片自然尺寸推大控件。升级动画现有region裁蓝边、margin补透明行和filter_clip须同步换算，不能恢复脏边。 |
| `MusouAfterimageNode.Capture`复制纹理、帧、offset、region和GlobalTransform；`HeroPreviewNode`使用独立PreviewAppearance/Animations | 战斗身体/武器、左右朝向、残影和背包必须一起验证。专用背包不能偷用战斗姿态；密度补偿不得被动画/RESET覆盖，残影不能放大或错帧。 |
| `MapLocationButton`按源alpha缓存命中；普通图可能KeepCentered，悬停图逆变换后按KeepAspect取样；shader与C#共用源像素`line_thickness` | 普通图不能因自然尺寸翻倍而变大；两态统一逻辑定位。描边宽度若增密要同步像素换算和动画参数，保持逻辑显示厚度及可见轮廓命中；现有显式TextureClickMask也须与新图对齐。验证缩放/悬停边缘、锁定地点和图鉴，不另加第二套命中机制。 |
| 数字十格与`FloatingTextNode`按alpha读取字宽、动画脉冲；逐帧动画使用SpriteFrames/AnimationLibrary | 增密不能改变比例字宽、基线、间距或暴击峰值；不通过缩放动画根偷改字距。帧数、帧号、duration、fps、循环/暂停/首尾保持，滤镜半径随密度核算；在深浅底和运动中检查alpha、裁边及图集串色。 |

需要突破200px公共契约时先给出原类、作者工具、战斗/背包/残影、审计消费者的具体最小改动清单，提交用户裁决；本轮不开始实现，不改Framework/addons。后续验收复用现有装备身份/动作/攻击审计、`--equipment-only`及相关引擎烟测，不只看导入成功或站立截图。

#### 2.12.10 Windows / Android运行纹理策略

在keep_height、高590时，1080像素高对应约1.831物理像素/逻辑单位；2倍源可接近1:1采样，但仍须检查单帧实际绘制尺度。当前keep在1920×1080中受590高度约束，原940核心约1721像素宽；换高清只改善内部清晰度，不消除视口外留边。

| 类别 | 母版与首轮运行候选 | 平台选择依据 |
| --- | --- | --- |
| 静态UI背景 | 普通2816×1180、标题2818×1182；对照1倍覆盖图 | Windows优先评估2倍的实际收益；Android先测相同已审候选及1倍导出，按驻留、清晰度和目标机预算决定，不先写平台分支。 |
| 共享天空 | 4096×1380可作母版；2048×690/2倍运行候选 | 检查共享天空与当前关卡同时驻留、实际最大纹理尺寸及透明度；4096宽并不保证所有目标设备适用。 |
| 角色、敌人、装备与特效 | 先分帧高清母版；运行仍遵循已审装配方案 | 当前1200×5800英雄RGBA8约26.55MiB，双倍2400×11600约106.2MiB且可能超设备单边上限。现有Boss效果已达6520×3200约79.59MiB，不能统一翻倍。确有尺寸/驻留缺口再局部调整原装箱流程，不提前整库拆图集。 |
| 无双、图标、字图 | 无双先公共层；小图按逻辑尺寸与源质量决定密度 | 核实两英雄演出真实加载依赖、场景切换释放及图标压缩后笔画/alpha；不能将所有帧与小图机械2倍。 |

RGBA8估算：1408×590约3.17MiB、2816×1180约12.68MiB；2048×690约5.39MiB、4096×1380约21.56MiB，双倍宽高约四倍像素成本。以上是无mipmap、未压缩的理论纹理字节，不是峰值实测；完整mipmap通常另增约1/3，CPU图像/暂存/上传还会增加峰值。

本轮只抽查`journey_map.png.import`：`compress/mode=0`、无mipmap、`size_limit=0`、fix_alpha_border开启、未预乘alpha。无损磁盘压缩不等于运行显存压缩，不能据一张图推广全库。后续优先用Godot原生导入/导出设置和原GGF资源生命周期；仅实测证明确有平台差异，才评估原生导入或导出差异，不复制业务场景或在运行时自建纹理缓存。Android压缩格式与Compatibility导出结果须在目标GPU确认，不预设ETC2/ASTC必然可用或看不出损失。

每批记录源/导入尺寸与格式、是否mipmap、最大纹理单边、当前/峰值驻留、加载耗时及相同场景帧时间/帧率。Android代表机、内存/帧率目标尚待用户指定，当前不能声称性能通过；A5确定运行候选，T5以实际导出包和真机收尾。

#### 2.12.11 输出和比例预览的小步骤

四个概念分别记录：**物理输出**是窗口/设备实际像素；**逻辑设计**仍为940×590；**素材密度**是每逻辑单位的源像素；**相机世界视野**是实际逻辑视口除Zoom。图片翻倍不等于视野翻倍，窗口1920×1080也不等于设计坐标变为1920×1080。

| 物理输出 | 比例 | keep_height预期逻辑宽（高590） |
| --- | --- | ---: |
| 1920×1080 | 16:9 | 1048.89（预览取1049，正式记录实际值） |
| 2340×1080 | 19.5:9 | 1278.33（预览取1279） |
| 2400×1080 | 20:9 | 1311.11（预览取1312） |
| 2520×1080 | 21:9 | 1376.67（预览取1377） |

1. **A1输出基线：** 使用已安装引擎的`--windowed --resolution WxH`逐尺寸启动隔离烟测；参数已经本轮`--help`确认，具体命令只维护Validation。先记录当前keep的实际窗口、viewport和留边，不改正式配置。尺寸被桌面/窗口管理器限制时记录实际值，不冒充目标分辨率。
2. **A1～A5素材/比例预览：** 复用原UI/视觉层，临时SubViewport或独立预览仅在自身窗口切换keep_height，记录1049/1279/1312/1377及精确输出目标。固定SubViewport像素不受桌面尺寸影响；标明模拟画面，不能当手机真机或完整关卡通过。原有940×590也保留对照。测试不反复编辑`project.godot`，不增加生产预览切换器。
3. **Windows默认1920×1080已在本批按用户补充要求设置：** 只写窗口输出override，逻辑仍940×590、keep视野不变；T3-0.1保留此项，另与门/刷怪/相机一起启用keep_height。窗口与全屏分别实测：当前默认窗口1920×1080，4K显示器全屏为3840×2160；用户接受后者放大1080p密度素材，不记原生4K高清。Android按设备实际尺寸，不强锁1920。
4. **T3/T5最终验收：** 四种目标输出逐一记录视口、Zoom/世界视野、UI与天空起终点；11关真实推进/通关另验，T4/T5补触屏、安全区和实际导出性能。平板另案，不提前交付宽屏已开却进不了关的版本。

#### 2.12.12 分批顺序、交付物与停止点

| 批次 | 素材与责任 | 交付与验收 / 停止点 |
| --- | --- | --- |
| A0（历史检查点） | 全量两维分类、来源/消费者调查、修订唯一计划 | 调查与计划已交付；用户本轮授权进入A1a。 |
| A1a 高清重导小样（英雄路线已认可） | 悟空身体/武器的待机、走路、攻击代表姿态；一块`huaguoshan_platform.png`；核实原源后导出 | 用户认可英雄清晰度，授权继续；31姿态和最小方案见2.12.13。平台2倍只作位图采样基准，未通过高清；这项限制保留。 |
| A1b 位图保真小样（方法不采用） | 已确认位图的`bat_cave_sky.png`，加`Items/equipment/ground_rough_pilgrim_staff.png`透明小图 | 原图/插值/FSRCNN候选及原生1080p预览已交付，见2.12.14。天空无明显收益，木棍有颜色偏移，保留正式原图并停止此方法推广。 |
| A1c 扩图小样 | 只做`journey_map.png`一张；高清中心若尚未过审，明确只验证原密度扩图 | 母版、运行候选、保护区像素差0、接缝及四种目标输出预览，地点坐标不动。完成即停审；ECPA无合格样图前不推广。A1同时补全输出/比例基线。 |
| A2 高清分类推广（英雄/背包待审） | 用户认可的英雄矢量路线先行；本批52张已接入，随后UI/头像/图标/地图地点/字图及其他类别仍逐批处理 | 本批结果见2.12.15；完整动作/成套衣装、源映射、1080p实图与成本均记录。位图来源不足项单列。当前停止，用户审图后再做下一类。 |
| A3 静态宽屏制作 | 其余3张UI背景+选角侧景；五张共享天空按共享关系制作 | 已审高清基线或明确标注原密度基线、扩图保护和四比例/原视差端部预览。按UI/天空分批停审，不改正式引用。 |
| A4 演出与短关 | 无双公共层/关键帧→两英雄完整24帧；四张短关只读取景复核 | alpha叠加、无闪烁、人物字形/24fps/暂停保持；短关交精确镜头条件和候选范围。镜头未裁决不盲画，依赖T3的项目留待T3，不冒称完成。停审。 |
| A5 美术交付审查 | 汇总全库处理结论、已审母版及运行候选、未决项 | 原图全部保留，清楚区分高清/仅扩图/暂保留及原因；给出纹理预算和Android待实测项。用户审查后恢复T2 UI接入→T3关卡/显示联动→T4触屏→T5导出/真机。 |

A1a/b/c是三项独立验证，互不以一个成功代替另一个；某类未通过就停止该类推广，仍可完成已授权且不依赖它的调查。A2各领域、A3各组都按小批审查，不一次性生产整库。后续实际制作的来源、导出参数及扩图提示词登记LegacyAssetMap，执行命令和验收证据写Validation。正式接入通过既有作者工具和原生资源登记，避免新旧运行实现并存；原始文件仍作离线基线保留。

T2的9个UI场景、10个编辑器验证脚本和11个烟测脚本的合理修改按既有审查保留；不因暂缓适配回退。临时移动诊断可后续单独清理，原测试断言不能删；本轮不清理。此前双人移动首轮失败后补诊断通过，尚未找到根因，不写成已经修复。A0开工Git为干净工作区/空暂存；A1a开工已有CrossPlatformPlan、WorkingNotes、Validation三份暂存修改，全部保留索引，不重置或覆盖。

**交用户裁决的实施选择（不阻碍本轮完成计划）：**

- 真正2倍英雄运行图必然涉及200px既有契约。建议A1先交固定逻辑尺寸的高清样本与原类最小修改方案，再决定是否调整该契约及对应第40章；未获该阶段授权前不改规范/装配。
- 请在A1b审同尺度对照后决定是否采用保真位图增强；“内容不变”已确定，不提供接受改画内容的选项。无法恢复的位图应保留原图或等待更高精度同内容原件。
- Android代表机/最低GPU、内存和帧率目标需指定，才能决定2倍运行图与压缩格式；不先用臆测预算淘汰高清候选。短关镜头与不可玩侧景范围仍在T3-3裁决，不能为画满屏改变玩法。

#### 2.12.13 A1a历史实物与最小适配提案（2026-11-14，英雄路线已认可）

本批只写忽略目录`.godot/validation/art_a1a_20261114/`及四份既有记录文档。原源版本、导出参数见LegacyAssetMap；命令、散列及验证见Validation。已交付[同逻辑对照](../Godot/GodotProject/.godot/validation/art_a1a_20261114/same_logical_comparison.png)、[31姿态对照](../Godot/GodotProject/.godot/validation/art_a1a_20261114/all_pose_pairs.png)、[1080p裁片](../Godot/GodotProject/.godot/validation/art_a1a_20261114/1080p_detail_comparison.png)及原图/2倍完整预览。

- **英雄结果：** 裸身/默认木棍；待机529的1/7/13/19/25/31/85/90、走路440的1/5/9/13、攻击211全部8帧及272全部11帧，共31姿态。62张身体/武器1倍图与正式对应格RGBA完全一致；2倍使用同一SVG和逐片遮挡，仅改变Godot栅格密度。样本逻辑边界误差最大0.5像素，属于抗锯齿采样变化；没有重画或新增内容。攻击一/三的76次原生关键帧检查保留正式长度、帧号和双朝向。尚未验证其余动作、装备、专用背包或独立特效高清。
- **样本格与正式格：** 只取代表姿态的六列稀疏图为1200×1800/2400×3600，格为200/400；不经过正式Appearance装配。走路正式格120/124/128/132在样本中使用原基础姿态槽9/10/11/12，显式映射记在manifest的`native_cell/cell`，预览原图按正式格取样；没有回写正式帧号或网格。两层RGBA8理论成本合计16.48/65.92MiB，无mipmap；不作为移动端预算或峰值实测。
- **平台结果：** 主程序shape56实际填充bitmap55，内嵌263×75 JPEG+alpha；当前正式图与1倍原生重导相同。526×150的2倍原生重导已提供，但没有更高精度细节，**不计高清通过**。本次登记源及旧第一关素材中未找到同内容高精度版本；保留原图，等待高精度来源或后续明确授权的保真方法，不在A1a改做AI重画/超分。

**拟交审的最小正式适配，仅提案、未实施：**

下表为A1a当时的提案；后续实际实现与原路径替换以2.12.15和第40章为准。

| 现有落点 | 必要变化与保持项 |
| --- | --- |
| `HeroAppearanceResource` | 增加资源级整数素材密度（默认1），同一资源的身体/武器/全部装备使用一致密度；像素格=`200×密度`，仍6列，行数从像素格计算。逻辑格仍200，缺图/不完整网格继续由现有装配拒绝。不得直接放宽宽度检查而省略定位补偿。 |
| 战斗Sprite与`HeroPreviewNode` | 只补偿各自视觉层：原视觉scale÷密度、offset×密度，重复换装按基准赋值，不能反复除导致累计缩小。当前悟空层scale=1、offset=0，可赋为1/k及0；位置(0,-28)、实体根、背包根0.95及朝向保持。正式动作/RESET不新增密度轨道；专用背包仍用backpack_v2，不能复用战斗图替代。 |
| `MusouAfterimageNode.Capture` | 现已复制Texture、H/V、Frame、Offset、Region和GlobalTransform，先复用原实现；接入时专项验证密度补偿随快照保留，不预加第二套残影逻辑。 |
| 既有作者与审计工具 | 在`import_official_wukong*.gd`及背包导出链给原栅格/遮挡/装箱步骤传入密度，保持源矩阵和正式帧映射；`validate_wukong_wardrobe_motion.gd`等按像素密度核对并换算回逻辑坐标。正式碰撞及判定值不重算、不放大；不得为了贴图增密改玩法几何。 |
| 大图集与接入门槛 | 完整早期图直接2倍为2400×11600；本次只上传了2400×3600样本，不能证明完整图集或Android支持。先保留分帧母版，正式运行装箱须在A2按既有紧凑装箱器及目标纹理上限另审；若需改帧映射，连同原动画库一次处理。本提案不承诺当前样本可直接替换。第40章200px正式契约需该阶段获授权后再同步。 |

**后续授权：用户已认可A1a英雄清晰度并要求继续全部素材适配1080p。英雄按该路线推进完整外观；平台高清未通过的结论保持，不把继续授权解释为可以改变位图原内容。正式宽屏仍留T3。**

#### 2.12.14 A1b实测裁决与完整英雄路线（2026-11-14）

位图小样放`.godot/validation/art_a1b_20261114/`，[1080p候选对照](../Godot/GodotProject/.godot/validation/art_a1b_20261114/1080p_candidates.png)包含原图、普通插值和FSRCNN×2。天空bitmap18为原生1440×690 JPEG，木棍bitmap16为原生52×54透明位图，当前正式PNG与各自原始载荷解码一致。未找到同内容更高精度来源。

FSRCNN使用非生成式亮度回归、原色度采样与独立alpha重采样；没有扩图或生成新物体。天空回采样后RGB平均差0.526/255，1080p下没有明确收益；木棍不透明区平均差4.785/255、最大29/255，配色偏移不接受。**当前方法不采用，两个正式原图保持，不批量增强其他位图。** 数值只是候选诊断，不能代替保真审图。Godot三种固定1920×1080预览装配通过，不代表候选画质通过。

完整英雄先解决实际11600px单边问题：悟空13张当前外观的174槽位按全套共同签名只有86种像素姿态。复用`compact_atlases`增加密度与共享层映射选项；仍六列，2倍像素格400，身体/武器按同一等价映射装箱。最终数量以同源2倍重导后签名为准；所有正式动画只改对应帧索引，关键时刻、方法、位移、碰撞和源动作顺序保持。该工作不依赖外围扩图，先行验证，A1c另行推进。

#### 2.12.15 A2英雄/背包批次（2026-11-14，已接入，待审）

正式原路径替换52张PNG：悟空/唐僧战斗外观各13张、backpack_v2专用预览共26张。UID和原资源路径保留，四份Appearance只增加密度2；原版归档在本批`original_pngs/`和Git。没有正式hd目录/后缀。来源、命令和完整结果分别维护LegacyAssetMap、Validation；[661项进度](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/hd_progress.csv)中52项已替换待审、3项位图限制、606项待处理，未把全部素材判为完成。

悟空六列从29行174槽压为15行90槽（86个共享槽含透明格、4个补齐空格），每格400px，图2400×6000；按全部13图联合签名去重。**没有删减播放帧：13图×174原槽=2262次逐槽回查全部保持原源像素或透明阶段，6份动画库的时刻、事件、位移、判定及播放键数量保持；只改悟空身体/武器的等价格索引。** 唐僧保留原100/37格映射，身体2400×6800、武器2400×2800；背包均2400×400。当前尺寸已足够，无需按动作拆图；若后续确有尺寸问题，按用户授权可用专用动作图集，不能因此遗漏动作。

运行时复用HeroAppearanceResource和原Sprite/残影/背包链，视觉层scale=0.5，逻辑格仍200、原position/offset不变；着色器从顶点推导实际像素格并维持原逻辑光晕半径。没有新Manager或平台加载分支。角色/预览UI、全装备源格与实际换装、双朝向播放、攻击覆盖、残影及受击光晕验证通过。构建0错误/5个既有警告；编辑器关停诊断、烟测故障注入和资源释放提示单独记Validation。

验收图：[1080p原生裁片对照](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/hero_review_contact.png)、[当前完整预览](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/heroes_1920x1080_current.png)、[实际唐僧背包](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/tangseng_inventory_wearing.png)。背景和独立特效未在本批高清。默认窗口已实测1920×1080；4K全屏实际3840×2160，用户接受继续放大，不要求4倍源。所有52张同时常驻的RGBA8无mipmap理论合计1362.30MiB，隔离对照夹具GPU纹理账本约2.0～2.4GiB（含双英雄效果及对照副本）；不是游戏峰值或Android实测。Android预算、加载/FPS比较仍未验，不以单边低于8192代替移动端通过。

**本批停止，等待用户审图。下一类UI/头像/图标、敌人、独立特效、字图及地形尚未实施；外围扩图和正式宽屏仍留后续阶段。**

---

# 第三部分　执行顺序总表

**当前停在A2英雄/背包批次审查，见2.12.15。A1b方法不采用，A1c外围扩图未做；其余A2类别及A3～A5逐批推进。下表T2～T5暂缓，正式宽屏不与高清混做。**

| 编号 | 大任务 | 依赖 | 玩法行为变化 | 状态 |
| --- | --- | --- | --- | --- |
| T1 | 输入映射整理与 Windows 体验基线 | — | 无（只验证） | 已审查通过（2026-11-14） |
| T2 | UI宽屏锚点与已审美术接入 | T1 + 美术前置阶段审查 | 正式视野暂不变 | 已有UI修改保留；后续实施暂缓 |
| T3 | 宽屏关卡数据与取景 | T2 | 宽屏下能看到更多世界 | 未开始 |
| T4 | 触屏 HUD 双布局与操作层 | T3 | Android 使用独立 HUD 布局并可操作 | 未开始 |
| T5 | 导出与真机验收 | T4 | 产出 exe 和 apk | 未开始 |

顺序原因：T2、T3 先把“宽屏下画面对不对”这个问题在 Windows 上就解决掉（Windows 拉宽窗口即可复现），T4 才在一个已经正确的画面上摆触屏键，避免摆完键再返工。

---

# 第四部分　大任务 T1：输入映射整理与 Windows 体验基线

**目标**：确认现有输入全部经 InputMap；补齐 Android 需要的两个设置；建立宽屏截图基线，供 T2、T3 对比。**不改任何玩法代码。**

涉及文件：`Godot/GodotProject/project.godot`、`TheGame/GameScripts/Systems/Input/InputSystem.cs`（只读）。

## T1-1　核对输入清单（只读，不改文件）

列出 `project.godot` `[input]` 段所有动作和绑定，与下表核对。不一致就停下上报，不要改。

| 动作 | P1 键 | 触屏是否需要 |
| --- | --- | --- |
| `move_left` / `move_right` / `move_down` | A / D / S | 是（箭头） |
| `interact` | W | 是（↑箭头） |
| `jump` / `attack` | K / J | 是（大键） |
| `skill_1` ～ `skill_4` | U / I / O / L | 是（图标键） |
| `musou` | 空格 | 是 |
| `inventory_toggle` / `skill_toggle` | C / V | 否（HUD 已有按钮） |
| `ui_*` | 默认 | 否 |
| 以上各动作的 `_p2` 版本 | 第二套键位 | 否（D-3） |

交付说明附上这张表的实际核对结果。

## T1-2　project.godot 新增三项设置

在 `[input_devices]` 段（没有就新建）写入：

```ini
[input_devices]

pointing/emulate_mouse_from_touch=true
pointing/android/enable_long_press_as_right_click=false
```

第一项是 Godot 默认值，显式写出是为了表明菜单依赖“触摸 = 左键点击”，以后不要关。第二项也是默认值：唐僧长按攻击、持续方向和持续移动不能被长按右键手势打断；显式关闭，避免以后误开。

在 `[application]` 段写入：

```ini
config/quit_on_go_back=false
```

原因：Android 返回键默认直接退出游戏，会丢失未保存进度。改为 false 后返回键不做任何事（首发不处理返回键，见第六部分遗留项）。

不要改 `[display]` 段，T2 再改。

## T1-3　建立宽屏截图基线

不改代码，只运行。用 runner 的 `--resolution` 参数分别以三种窗口尺寸运行现有 UI 烟测，保存截图：

```text
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py xplat_t1_base_940 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --resolution 940x590 --quit-after 30000 -- --smoketest=ui
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py xplat_t1_base_1920 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --resolution 1920x1080 --quit-after 30000 -- --smoketest=ui
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py xplat_t1_base_2400 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --resolution 2400x1080 --quit-after 30000 -- --smoketest=ui
```

烟测本身会把截图写到 `res://.godot/validation/` 下。三种尺寸必须顺序运行，退出后先把新产生的 png 复制到 `.godot/validation/xplat_baseline/<尺寸>/`，再运行下一种尺寸，避免同名截图覆盖。此时 aspect 仍是 `keep`，设计构图不变。注意 `GetViewport().GetTexture().GetImage()` 只截图渲染视口，不含窗口外的黑边：1920×1080 与 2400×1080 的原始图均约为 1720×1080；夹具内部缩放专项的图片约为 1175×737。交付时标明这种区别，不把视口截图冒充包含留边的原生窗口截屏。

## T1 完成判定

- 第一部分第 5 节的最低验证全部通过，数字与基线一致。
- 三组截图存在，交付说明列出路径。
- `git diff` 只包含 `project.godot` 的三处新增。

**停止，等待用户审查。**

---

# 第五部分　大任务 T2：窗口与 HUD 的宽屏锚点适配

**目标**：完成窗口在940～1377逻辑宽度下的锚点、美术扩展与UI接入。先审查2.12的两维方案及A1三类小样，再逐批审图；正式项目继续keep，世界宽屏在T3启用。不改玩法/碰撞，不先制作替代关卡。天空新图可在本任务试制，正式关卡资源切换与取景协同T3。

涉及文件：`TheGame/UIs/*.tscn` 及无双演出子场景；按既有工具再生的窗口绑定；Debug/EditorScripts 中受节点迁移影响的路径；`TheGame/MainPack/UI/*.tscn` 和 `LevelGuideTooltip.cs`（只读核对）。素材清单、母版、运行图及被动背景节点按2.12执行；不修改 `project.godot`、ProcedureLaunch 或集中规范的拉伸规则。

## T2-1 / T2-2　宽屏启用已移至 T3-0

按用户明确裁决，T2 不切换 keep_height、不增加宽度钳制、不修改规范中的当前拉伸模式。对应原文和实施内容统一移至 T3-0，避免新旧安排并存。

## T2-3　窗口留边边界

不修改 `environment/defaults/default_clear_color`，不新增装饰节点。当前 GL Compatibility 渲染器将视口外区域清为黑色，背景色设置无法改变该区域；原计划对此的判断有误，已通过 4.7.2 源码核实。

按 2026-11-14 用户确认：原1312上限已由当前用户16:9～21:9要求取代。T3采用1377上限并展开支持比例的视口；T2通过扩图填补绘制不足。4:3平板和21:9外比例另行适配，现有原画内部的黑色构图保持。

## T2-4　菜单类窗口整体居中

逐个处理下列窗口。原则：根节点保持全屏；现有子节点除“全屏遮罩/背景”外全部移入一个 940×590 居中容器。

| 场景 | 全屏铺满的节点（保持或改为 `anchors_preset = 15`） | 放进居中容器的节点 |
| --- | --- | --- |
| `start_form.tscn` | 背景扩图后覆盖全宽 | 原画与其他内容均保留原构图居中；不采用裁切标题的Cover旧图方案 |
| `character_form.tscn` | `Background` | 其余全部 |
| `map_form.tscn` | 无（地图图本身就是内容） | 全部（含 `Background`） |
| `save_form.tscn` | `Shade`、`DeleteConfirmation` | `Backdrop`、`Slots`、`m_Close`、`m_Status`；确认层留根层，内部Panel居中（用户确认） |
| `settings_form.tscn` | `Shade`（已是 15） | 复用`Scroll`直接居中，偏移为(-163,-219,163,219)，保留原326×438尺寸，不再增加Frame |
| `inventory_form.tscn` | `Dim`、`m_Actions`、`m_SaleConfirmation` | 复用`Window`直接居中，保留940×590与内部路径 |
| `settlement_form.tscn` | 无 | 复用`Content`直接居中，保留940×590与内部路径 |
| `skill_form.tscn` | — | 已经居中，不动 |
| `save_notice_form.tscn` | — | 已居中，不动；原顶部方案实测裁字，用户已确认保留原提示和动画 |
| `MainPack/UI/LoadingForm.tscn` | `Overlay`（已是 15） | 其余已锚定，不动 |
| `MainPack/UI/QuestionTips.tscn` | `BG`（已是 15） | `Content` 已居中，不动 |

居中容器的写法统一为：

```text
[node name="Frame" type="Control" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -470.0
offset_top = -295.0
offset_right = 470.0
offset_bottom = 295.0
grow_horizontal = 2
grow_vertical = 2
mouse_filter = 2
```

多块内容需要新容器时，节点名统一叫 `Frame`，内容坐标不变。已有单一完整内容容器（Window、Content、Scroll）则直接设置居中锚点，不增加中转父节点。这遵循第二部分2.5的“已有或新增容器”原则，避免不必要节点和内部路径迁移。

用户已否定旧图裁切和普通黑边方案，选择在原画外扩展重绘：开始页与地图保留原始素材及中央构图，新增宽屏背景版本填补两侧，不拉伸主体、不改交互坐标。地图源图940×590，左右各扩234至1408×590；开始页941×591扩到1409×591。素材生产以2.12为准，高清中心与原尺寸扩图分别按2.12.8验收；2倍图按相同逻辑尺寸映射，不把低清中心合回后称已高清。ECPA此前可调用但样稿保护区/接缝未过，不把接口成功或未完成样稿当成成品。全屏ColorRect遮罩仍使用全屏锚点。

**节点路径变化处理**：移动节点后，`node_paths` 指向的路径会从 `m_Xxx` 变成 `Frame/m_Xxx`。必须做两件事：

1. 用 `python Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace GameLogic.UI` 重新生成该窗口的绑定代码。
2. 在 `TheGame/MainPack/Scripts/Debug/` 里搜索该窗口的节点路径字符串（例如 `GetNode<...>("m_Close")`），同步改成新路径。仅同步节点路径与迁移后的坐标/父节点取用，保留原断言意图、检查数量和数值。旧全局940边界改用Frame内坐标，地图直属子节点枚举改从Frame读取，不删减断言。

如果某个窗口的 `.Logic.cs` 里用字符串路径找节点（不是 `[Export]`），先上报；本轮已由用户确认 SaveForm 的 DeleteConfirmation 保留根层，业务脚本不改。删除确认Panel使用居中锚点，偏移(-176,-100.5,176,100.5)，维持原352×201画面。

## T2-5　战斗 HUD 按角落锚定

`battle_form.tscn` 的子节点目前是 `Sprite2D`（不是 `Control`），无法用锚点。处理方式：在根下新增两个锚点容器，把 Sprite 挂进去，Sprite 的本地坐标不变。

新增：

```text
[node name="LeftAnchor" type="Control" parent="."]
layout_mode = 1
anchors_preset = 9
anchor_bottom = 1.0
offset_right = 470.0
grow_vertical = 2
mouse_filter = 2

[node name="RightAnchor" type="Control" parent="."]
layout_mode = 1
anchors_preset = 11
anchor_left = 1.0
anchor_right = 1.0
anchor_bottom = 1.0
offset_left = -470.0
grow_horizontal = 0
grow_vertical = 2
mouse_filter = 2
```

挂载与坐标：

| 节点 | 新父节点 | 新 position |
| --- | --- | --- |
| `StatusPanel` | `LeftAnchor` | 不变 `(116, 44)` |
| `MenuPanel` | `LeftAnchor` | 不变 `(90, 531)` |
| `StatusPanelP2` | `RightAnchor` | `(824 − 470, 44)` = `(354, 44)`，scale 保持 `(-1, 1)` |
| `MenuPanelP2` | `RightAnchor` | `(850 − 470, 531)` = `(380, 531)`，scale 保持 `(-1, 1)` |
| `m_Go` | `RightAnchor` | `(870 − 470, 259)` = `(400, 259)` |
| `m_Combo` | `RightAnchor` | `offset_left` / `offset_right` 各减 470 |
| `m_EncounterHealthBar` | 不动（已是顶部居中 `anchors_preset = 5`） | — |
| `m_MusouActivation` | 不动（已全屏） | 见下一条 |

`m_MusouActivation` 内部的 `Animation`（`TheGame/UIs/Battle/musou_activation.tscn`，`position = (470, 295)`）是固定坐标。在该场景根下的 `Animation` 外包一个居中 `Control`（同 T2-4 的 `Frame` 写法，节点名 `Frame`），`Animation` 的 position 保持 `(470, 295)`。其导出路径同步为 `Frame/Animation`（包含BattleForm实例覆盖）；保留AnimationPlayer在根层，将其root_node指向`../Frame`，继续复用原动画库和时序。

完成后重新生成 BattleForm 绑定，更新 Debug 烟测里的路径（已知：`UiSmokeScenario.cs:815-818, 873, 961-962, 992, 1792`，`UiSmokeScenario.HeroDeath.cs:53`，`FrontendSmokeScenario.cs:589, 714`，`UiSmokeScenario.cs:2185`，`SmokeTestDriver.cs:890` 用 FindChildren 不受影响）。路径从 `"StatusPanelP2/..."` 改为 `"RightAnchor/StatusPanelP2/..."`，依此类推。

## T2-6　LevelGuideTooltip 核对（只读）

`LevelGuideTooltip.cs:129` 用 `GetViewportRect().Size` 夹紧位置，视口变宽后仍然正确。不改。在1377宽度截图里确认图鉴浮层不出界即可；此前1312图鉴专项为历史有效检查点，不充作最终21:9验收。

## T2 完成判定

- 美术前置阶段A0～A5已经审查，确定需要的UI高清/背景/演出边缘已接入，并按2.12.8分别通过内容保持或保护区、接缝、内存和多比例验收；天空随T3接入，未决短关候选明确留T3，不冒称已扩。
- 最低验证全部通过，生产显示设置保持 T1 状态。
- 完整烟测三项都在 `--resolution 2400x1080` 下运行并 PASS：`--smoketest=frontend`（`--quit-after 18000`）、`--smoketest=ui`（30000）、`--smoketest=ui --coop-only`（30000）。正式视野仍为 keep，不接受由宽屏引起的关卡初始化失败。
- 在940×590、1920×1080、2340×1080、2400×1080、2520×1080保存画面；这些正式窗口仍等比保留旧构图，已有2560×1080截图仅作额外尺寸证据。另用忽略目录中的一次性UI预览，在940、1049、1279、1312、1377逻辑宽度下验证菜单、存档、设置、背包、技能、结算和单/双HUD的实际锚点。预览只实例化原场景，不载入关卡、不修改生产项目设置。
- 逐项检查全屏遮罩、背景覆盖、菜单居中、HUD贴边、无双动画轨道及图鉴浮层边界，交付说明列出截图、验证方式和范围；不能把静态UI预览记为宽屏玩法验收。

**停止，等待用户审查。**

---

# 第六部分　大任务 T3：宽屏关卡数据与取景

**目标**：1377宽视野下，11关都正常推进；保留屏外出生关系、原视差观感和角色尺度，美术覆盖全宽。

涉及文件：`TheGame/Scenes/Levels/**/level_*.tscn`；`TheGame/GameScripts/Entity/Level/Camera/LevelCameraNode.cs`（仅 T3-3）；T3-0 的 `project.godot`、`ProcedureLaunch.cs` 和明确授权的第40章原句。

## T3-0　与关卡数据一起启用宽屏

原 T2-1/T2-2 按用户裁决移到本任务。完成 T3-1～T3-3 的门、刷怪点、天空和取景适配后，在本任务交付前实施以下设置；不交付宽屏已开启但关卡不兼容的中间状态。

### T3-0.1　切换拉伸模式

`project.godot`的`[display]`段保留940×590逻辑尺寸，新增宽屏策略及Windows默认窗口1920×1080输出覆盖值：

```ini
window/stretch/aspect="keep_height"
window/size/window_width_override=1920
window/size/window_height_override=1080
```

本步骤是美术结束后恢复显示配置的明确交付：Windows默认窗口1920×1080，另验显示器实际模式下的全屏清晰度；Android全屏按设备实际像素尺寸输出，不把手机固定为1920×1080。这里的窗口输出覆盖值不改变逻辑坐标和美术注册点，T2不提前写入。

同时修改第 40 章（`Docs/ProjectGuidelines/40-ScenesAndAssets.md`）。把这一句：

> 窗口使用 `canvas_items + keep` 保持 940×590 构图；`expand` 会在 1920×1080 下扩到 1048px，改变既定视野和原版波次构图。

替换为：

> 窗口使用 `canvas_items + keep_height`：宽屏下高度固定590，宽度随屏幕比例在940～1377之间变化，超出1377的部分保留原生黑边；较窄比例等比留边。普通关卡阶段与刷怪点按1377校准（见CrossPlatformPlan附录A）；整关小于1377的短关需先审查可见区域与物理边界契约，不能为满足画面宽度直接加长可玩地形。

这是本计划授权的第一处规范修改，只改这一句。

### T3-0.2　视野宽度上限 1377

keep_height 本身没有宽度上限。在现有唯一启动入口 `ProcedureLaunch` 中，根据窗口尺寸动态切换根 Window 的 `ContentScaleAspect`：未超上限用 KeepHeight，超上限用 Keep + 1377×590。不要新增启动器或屏幕适配类。

在 `ProcedureLaunch.OnEnter`（`TheGame/MainPack/Scripts/Procedure/ProcedureLaunch.cs`）验证通过、`ChangeState<ProcedureUpdate>` 之前加入：

```csharp
// keep_height 下视野宽度随屏幕变宽；超过关卡校准宽度时改为两侧留边。
Window root = GF.Base.GetTree().Root;
root.SizeChanged += () => ClampViewWidth(root);
ClampViewWidth(root);
```

以及同文件内的私有静态方法：

```csharp
/// <summary>
/// 视野宽度超过关卡校准宽度时，改用 keep 拉伸以留边；否则保持 keep_height。
/// </summary>
/// <param name="root">根窗口。</param>
private static void ClampViewWidth(Window root)
{
    const float MaxViewWidth = 1377f; // 关卡阶段与刷怪点按此宽度校准，见 CrossPlatformPlan 附录 A。
    Vector2I size = root.Size;
    float viewWidth = size.Y > 0 ? 590f * size.X / size.Y : 940f;
    root.ContentScaleAspect = viewWidth > MaxViewWidth ? Window.ContentScaleAspectEnum.Keep : Window.ContentScaleAspectEnum.KeepHeight;
    root.ContentScaleSize = viewWidth > MaxViewWidth ? new Vector2I(1377, 590) : new Vector2I(940, 590);
}
```

说明（执行者需理解，不用写进代码）：宽于上限时切到 `keep` 并把基准尺寸设为 1377×590，于是视口正好是 1377 宽，两侧保留引擎原生黑边。`ProcedureLaunch` 已经是唯一的启动入口，这里不新增任何类型。

如果 `GF.Base.GetTree()` 不可用，改用 `((SceneTree)Engine.GetMainLoop()).Root`。不要做其他替代。

## T3-1 按1377重新校准门和刷怪点

读取本阶段实际配置、当前场景门、碰撞与生成点，重新计算各区域至少容纳1377世界宽度后的边界。原1312版附录A已废止，不照抄旧值，不把每个点统一平移。原屏外生成点保留其屏外关系，复核脚下地面/平台、墙和关卡范围。11关短关按T3-3处理，不机械要求扩展物理世界。

在编辑器打开11关，检查原标记/业务ID/资源引用；逐关在940、1049、1279、1312和1377宽度验证阶段推进、相机抵达和屏外出生。生成点y、平台几何和数值源不因绘图随意改变。

## T3-2 接入已审天空，保持现有视差

按2.12.4的五图共享关系替换为已审扩展资源，保持原世界注册点、scale和scroll_scale。不再执行旧1312版的视差降速表，也不修改集中规范中的原视差规律来适应不足的图片。用实际相机起终点和阶段边界截图确认；某图未达到覆盖要求就回到对应美术任务补图，不用改变视差/加底色掩盖。

## T3-3 龙宫与牛魔谷短关取景

先核对物理世界宽度（龙宫1222、牛魔谷1951）、最终阶段区域和2.12.5四张候选图的覆盖；新增画面只作为装饰，不增加可行走区域。旧方案把龙宫相机Zoom放大以塞进1222，会改变角色显示尺度和可见高度，不能再当作默认实施。

优先保持角色/相机原尺度，用已有LevelCameraNode与LevelGatesNode协调短关可见范围和真实物理区域；若需要改变其公共装配契约，先说明具体方案并提交用户审查，不新增相机/平台Manager。以短关实际21:9画面、屏幕墙、守关目标及出口完成证据裁决，不能只凭静态扩图宣称取景完成。

## T3-4　技能索敌范围核对（只读）

`AbstractHeroEntity.Skills.cs:153-177` 的 `FindSkillTarget` 用视口宽度判断“怪物是否在屏幕内”。视野变宽后，远处的怪物也会被选中。这与“看得见就能被锁定”的直觉一致，**不改**。在交付说明里注明。

## T3 完成判定

- 最低验证全部通过。
- 关卡烟测`--smoketest=level`、`level2`～`level10`（每个`--quit-after 30000`）在`--resolution 1920x1080`和`2520x1080`下全部PASS；另保留940×590逻辑基线验证。21:9最大视野不能用旧20:9结果替代。
- `--smoketest=bosses` 在2400×1080和2520×1080下PASS；不得用20:9通过替代21:9最大视野验收。
- 每关在1920×1080与2520×1080下保存开战瞬间及实际末阶段画面，使用现有截图点核对屏外出生；2400×1080作为中间比例补充检查。
- 11关均在2520×1080下验证实际相机端部，确认无未绘制空白、不显示误导性的可走区域；短关按T3-3的已审方案验收。

**停止，等待用户审查。**

---

# 第七部分　大任务 T4：触屏 HUD 双布局与操作层

**目标**：Windows 保留现有 HUD；Android 使用 D-7 的布局。两套布局共用 BattleMenuPanel 和 BattleForm，不建立第二套数据通路。开始页在触屏设备隐藏双人入口。

涉及文件：`TheGame/UIs/battle_form.tscn`；新建 `TheGame/UIs/Battle/keyboard_menu_panel.tscn`、`touch_menu_panel.tscn`；`BattleForm.cs/.Logic.cs`；`BattleMenuPanel.cs`；`SkillSystem.cs`；`StartForm.Logic.cs`；新建 `TheGame/Sprites/UI/touch/*.png`。

## T4-1　抽取键盘菜单场景并准备触屏素材

**先抽取，后做触屏，避免两个布局逐渐漂移。**

1. 把 T2 后 `LeftAnchor/MenuPanel` 整支保存为 `TheGame/UIs/Battle/keyboard_menu_panel.tscn`，根节点名 `MenuPanel`，保留原 `BattleMenuPanel` 脚本、全部子节点、导出绑定和坐标。
2. `battle_form.tscn` 中该节点改为上述场景的实例，实例名仍为 `MenuPanel`，父节点和 position 不变。`MenuPanelP2` 也改成同一场景的第二个实例，保留 T2 的父节点、position 和根 `scale = (-1, 1)`。
3. P2 原分支中用于抵消镜像的子节点 `scale = (-1, 1)` 必须逐项保留为实例覆盖：`WsGrey`、`m_WsMax`、五个 `SkillBar/SkillN/Icon`、五个菜单按钮的 `Caption`。其余外观与 P1 一致。根导出绑定 `_menu`、`_menuP2` 的路径不变。
4. 运行一次 UI 烟测确认抽取没有改变键盘 HUD，再继续触屏。

在 `TheGame/Sprites/UI/touch/` 准备以下 PNG（透明底）：

| 文件 | 尺寸 | 内容 |
| --- | --- | --- |
| `touch_arrow.png` / `touch_arrow_pressed.png` | 96×96 | 向右箭头，金色描边、半透明暗底；pressed 提亮 |
| `touch_attack.png` / `touch_attack_pressed.png` | 128×128 | 圆形，棍棒/剑图标 |
| `touch_jump.png` / `touch_jump_pressed.png` | 112×112 | 圆形，向上箭头/云图标 |
| `touch_skill_ring.png` / `touch_skill_ring_pressed.png` | 80×80 | 圆形空环，技能图标外框 |
| `touch_musou.png` / `touch_musou_pressed.png` | 88×88 | 无双圆钮的透明圆环和边框 |
| `touch_musou_under.png` | 88×88 | 圆钮内的暗色底 |
| `touch_musou_fill.png` | 88×88 | 同尺寸半透明蓝色水面，供从下往上填充 |

素材优先从 `Sprites/UI/hud/`、`Sprites/UI/skills/` 裁切、缩放、着色；不得从网络下载。无法得到满意效果时生成简单几何占位图，在交付说明注明“占位素材，待美术替换”，不要因此改布局或卡住任务。导入设置与 `Sprites/UI/hud/` 一致；在 [LegacyAssetMap.md](LegacyAssetMap.md) 追加来源。三个方向不单独做图，用旋转复用箭头。

## T4-2　touch_menu_panel.tscn：Android 菜单和按键布局

新建场景，根节点：

```text
[node name="TouchMenuPanel" type="Sprite2D"]
script = ExtResource("battle_menu_panel")  # 与键盘菜单相同，不写新脚本
```

根 Sprite2D 不设置 texture，**不画左下面板底图**。根下放三个 Control 锚点容器。它们的直接父节点不是 Control，锚点由最近的 Control 祖先（全屏 BattleForm）计算，因此等同按视口布局；容器内坐标全部用下列本地值。

| 容器 | anchors_preset | offset_left/top/right/bottom | 用途 |
| --- | --- | --- | --- |
| `Left` | 2（左下） | `16, -590, 486, 0` | 四向箭头 |
| `Right` | 3（右下） | `-486, -590, -16, 0` | 攻击、跳跃、技能、无双 |
| `TopRight` | 1（右上） | `-260, 16, -16, 96` | 背包、技能、设置 |

三个容器 `mouse_filter = 2`，尺寸固定为 470×590（TopRight 为 244×80）。

所有方向/攻击/技能按钮都同时设置 `texture_normal` 和同名 `_pressed` 的 `texture_pressed`；无双按钮同理。所有 `TouchScreenButton` 都设置 `shape_visible = false`，不在玩家画面上绘制命中圆。

### 四向箭头

全部为 `TouchScreenButton`，纹理 `touch_arrow`，`visibility_mode = 1`（TouchscreenOnly）、`passby_press = true`、`modulate = Color(1,1,1,0.65)`。命中形状为 `CircleShape2D`，半径 48，`shape_centered = true`。

纹理原点在左上，旋转也绕该点；以下 position 已按 `desired_center - R(rotation) × (48,48)` 补偿，直接照填：

| 节点名 | position | rotation_degrees | action | 箭头中心 |
| --- | --- | --- | --- | --- |
| `MoveRight` | (144, 406) | 0 | `move_right` | (192, 454) |
| `MoveLeft` | (96, 502) | 180 | `move_left` | (48, 454) |
| `InteractUp` | (72, 430) | -90 | `interact` | (120, 382) |
| `MoveDown` | (168, 478) | 90 | `move_down` | (120, 526) |

左右滑动切换方向不会额外触发攻击。上下箭头也参与滑动切换，与原手游方向键一致。

### 攻击、跳跃和直接技能

这些按钮 `visibility_mode = 1`、`passby_press = false`、`modulate = Color(1,1,1,0.65)`。攻击/跳跃必须允许另一根手指同时按方向或技能，不能用滑入切换。

| 节点名 | 纹理 | position（纹理左上角） | action | 命中圆半径 |
| --- | --- | --- | --- | --- |
| `Attack` | `touch_attack` | (318, 438) | `attack` | 64 |
| `Jump` | `touch_jump` | (196, 462) | `jump` | 56 |
| `Skill1` | `touch_skill_ring` | (206, 372) | `skill_1` | 40 |
| `Skill2` | `touch_skill_ring` | (282, 340) | `skill_2` | 40 |
| `Skill3` | `touch_skill_ring` | (366, 340) | `skill_3` | 40 |
| `Skill4` | `touch_skill_ring` | (130, 404) | `skill_4` | 40 |

每个 `SkillN` 下放 `TextureRect` 子节点 `Icon`：`position = (8,8)`、`size = (64,64)`、`expand_mode = 1`（Ignore Size）、`stretch_mode = 5`（Keep Aspect Centered）、`mouse_filter = 2`。图标由 T4-3 赋值，不在场景里写死角色。

### 无双圆钮与涨水进度

`Right/Musou` 是 `TouchScreenButton`：position `(330,150)`、`action = "musou"`、`passby_press = false`、半径 44、纹理 `touch_musou`。它不与右侧 Go 提示重叠。

其子节点：

- `WsBar`：现有 `ResourceBar` 脚本（TextureProgressBar），`position = (0,0)`、`size = (88,88)`、`mouse_filter = 2`，`texture_under = touch_musou_under`、`texture_progress = touch_musou_fill`、`fill_mode = 3`（Bottom to Top）、`tint_progress = Color(1,1,1,0.55)`。`_text` 留空。现有 `ResourceBar.SetValue` 已把 Ws/MaxWs 转为 0～1，**不需要 Tween，也不新增进度状态**。
- `WsMax`：现有 `battle_menu_ws_glow_frames.tres` 的 AnimatedSprite2D，圆钮中心 `(44,44)`，缩放到适合 88px 圆钮；满值播放由同一个 `BattleMenuPanel.RefreshMusou/ResetMusou` 管理。

### 右上菜单图标

`TopRight` 下放 `m_InventoryButton`、`m_SkillsButton`、`m_SettingsButton` 三个 TextureButton，分别复用 `Sprites/UI/hud/menu_{inventory,skills,settings}_{normal,hover}.png`，`scale = (1.5,1.5)`，点击区域约 70×56。position 分别为 `(0,12)`、`(82,12)`、`(164,12)`。**不添加 Caption 子节点，不显示文字。** 按钮事件仍由 BattleMenuPanel._Ready 连接。

场景根导出绑定：

```text
_wsBar = NodePath("Right/Musou/WsBar")
_wsMax = NodePath("Right/Musou/WsMax")
_settingsButton = NodePath("TopRight/m_SettingsButton")
_inventoryButton = NodePath("TopRight/m_InventoryButton")
_skillsButton = NodePath("TopRight/m_SkillsButton")
_icons = []
_directIcons = [NodePath("Right/Skill1/Icon"), NodePath("Right/Skill2/Icon"), NodePath("Right/Skill3/Icon"), NodePath("Right/Skill4/Icon")]
_skillIcons = ExtResource("official_skills")
```

## T4-3　技能图标按 InputAction 复用现有登记

已核实 `SkillConfig` 只有 `Id/LegacyId/Hero/Order/NameCn/Description/LearnSoul/ManaCost`，**没有按键字段**。`ConfigurationModel.GetActive` 是官方展示顺序，悟空前两项是组合技，唐僧第一项是组合技，不能用同一个偏移量。

权威对应关系已经存在于英雄登记的 `SkillDefinition.InputAction`：悟空 `skill_1～3 = BlazingFlash/FlameStorm/FireDemonSlash`；唐僧 `skill_1～4 = DarkIceFormation/WaterClone/HeavenlyDew/WaterDemonBurst`。UI 通过 System 的只读方法查询，不复制技能 ID 列表，不改 Luban 表。

1. 在现有 `SkillSystem` 增加唯一一个只读方法：

   ```csharp
   /// <summary>
   /// 按本局英雄已登记的直接输入动作查询技能；未登记时返回 null。
   /// </summary>
   /// <param name="heroId">本局英雄编号。</param>
   /// <param name="action">InputMap 动作名，例如 skill_1。</param>
   public SkillId? GetSkillByInputAction(int heroId, string action)
   {
       if (_skillDefinitions.TryGetValue(heroId, out Dictionary<SkillId, SkillDefinition> definitions))
       {
           foreach (SkillDefinition definition in definitions.Values)
           {
               if (string.Equals(definition.InputAction, action, StringComparison.Ordinal))
               {
                   return definition.Id;
               }
           }
       }
       return null;
   }
   ```

2. `BattleMenuPanel` 新增：

   ```csharp
   [Export] private TextureRect[] _directIcons = []; // 触屏布局的 skill_1～4 图标；键盘布局为空。
   ```

3. `_Ready` 校验改为：`_icons` 与 `_directIcons` 必须且只能有一组非空；`_icons` 仍要求直属 Control 父节点，`_directIcons` 要求直属 TouchScreenButton 父节点。`_skillCells` 仍只从 `_icons` 收集。
4. `RefreshSkills` 保留现有 `_skillCells` 刷新；只有 `_skillCells.Length > 0` 时才检查“格子数不得少于配置技能数”。随后刷新 `_directIcons`：
   - 第 i 个图标用 `GetSkillByInputAction(_state.HeroId, $"skill_{i + 1}")` 查询；
   - 有技能时纹理仍为 `_skillIcons.GetFrameTexture(id.ToString(), learned.Contains(id) ? 1 : 0)`，父按钮显示；
   - 返回 null 时 `Texture = null`，父按钮隐藏。悟空第 4 键因此隐藏，不留无效按钮。
5. 组合技不新增按钮：悟空升龙仍是 ↑+攻击，奔跑突刺仍是奔跑+攻击，唐僧冰龙波仍是长按攻击。

不得把上述对应表硬编码进 UI，不得新增配置字段，不得让 UI 读取英雄实体的私有技能数组。

## T4-4　BattleForm 同时持有两套布局，但只绑定一套

1. `battle_form.tscn` 根下实例化 `touch_menu_panel.tscn`，实例名 `TouchMenuPanel`，`position = (0,0)`、`visible = false`。它不与键盘菜单共节点，也不包含 P2。
2. 根 `node_paths` 新增 `_touchMenu = NodePath("TouchMenuPanel")`；用生成器更新 `BattleForm.cs`，不手改生成物。
3. `BattleForm.Logic.cs` 新增字段：

   ```csharp
   private bool _touchLayout; // 本次开窗选择的布局。
   private bool _touchCovered; // GGF 覆盖状态，避免下层触屏键先于 GUI 收到输入。
   private bool _touchPaused; // GGF 暂停状态，与覆盖分别恢复。
   ```

4. `OnInit(isNewInstance)` 中给 `_touchMenu` 连接与 `_menu` 相同的四个事件：SettingsSelected、InventorySelected、SkillsSelected、MusouStateChanged。只连接一次。
5. `OnOpen` 在校验通过后：
   - `_touchLayout = DisplayServer.IsTouchscreenAvailable()`；`_touchCovered = _touchPaused = false`；
   - `_menu.Visible = !_touchLayout`；
   - `_statusP2.Visible = _menuP2.Visible = !_touchLayout && data.Heroes.Count == 2`；
   - 绑定循环里，触屏布局跳过非 P1 英雄；P1 使用 `_touchMenu`，键盘布局按原席位使用 `_menu/_menuP2`；
   - `_touchMenu` 也执行 `BindHero`，状态面板仍按原逻辑绑定；根 `Visible = true` 后调用下述 `RefreshTouchMenuVisibility()`。
6. 新增私有方法：

   ```csharp
   /// <summary>只在战斗窗口可见、未被覆盖且未暂停时接收触屏操作。</summary>
   private void RefreshTouchMenuVisibility()
   {
       _touchMenu.Visible = _touchLayout && _formData != null && Visible && !_touchCovered && !_touchPaused;
   }
   ```

7. `OnCover`：`_touchCovered = true` 后刷新；`OnReveal`：false 后刷新。`OnPause`：`_touchPaused = true` 后刷新；`OnResume`：false 后刷新。隐藏会由 TouchScreenButton 自动释放按住的动作，恢复后不会粘键。
8. `Unbind` 增加 `_touchMenu.UnbindHero()`（沿用现有 IsInstanceValid 写法），`_formData` 清空后刷新可见性。装配校验把 `_touchMenu` 纳入必需节点。
9. 不要按 `GF.Base.IsGamePaused` 隐藏布局：无双立绘也使用游戏暂停，不能把正常的无双演出当成关窗。只使用上述 GGF 窗口生命周期。

## T4-5　开始页隐藏双人入口

`StartForm.Logic.cs` 在 `OnOpen` 显示主菜单之前加入：

```csharp
// 触屏设备只有一套操作，双人仅在键盘端提供。
_double.Visible = !DisplayServer.IsTouchscreenAvailable();
```

`_double` 是已有导出字段。若隐藏后固定坐标布局留下明显空位，截图上报，不自行重排主菜单。

## T4-6　出口提示文字

`level_exit.tscn` 的 `Prompt` 文本“按 W 返回地图”改为“按 ↑ 返回地图”。↑ 箭头就是 `interact`，键盘 W 仍表示上方向，双端含义一致，不写运行时分支。

## T4 完成判定

- 最低验证全部通过；Battle/Profile/Level 数字不低于基线。
- 完整烟测 `--smoketest=ui`、`--smoketest=ui --coop-only`、`--smoketest=frontend` 在 940×590 下 PASS，证明键盘布局和双人窗口行为未回归。
- 本地编辑器开启“鼠标模拟触摸”后，940×590 和 2400×1080 各截：悟空战斗、唐僧战斗、背包打开、设置打开。确认：左下面板消失、左上状态保留、右上三图标可点、技能图标/灰色状态正确、悟空第 4 键隐藏、无双水量随 Ws 上升、满值闪光、Go 不被圆钮遮挡。
- 鼠标模拟只能验证单指。分别验证方向、攻击、跳跃、各技能、无双和三个菜单按钮；↑+攻击、↓+跳跃等多指组合标为“待 T5 真机验证”，不得用单指鼠标结果冒充多指通过。
- 打开背包/设置时 TouchMenuPanel 隐藏且按住的方向被释放；关闭后恢复，无粘键。键盘端截图确认完全不显示触屏布局。
- 完成后关闭本地“鼠标模拟触摸”，确认 `git diff project.godot` 不包含该项。

**停止，等待用户审查。**

---

# 第八部分　大任务 T5：导出与真机验收

**目标**：产出可安装的 Windows 包和 Android APK，在真机上完成一关。

## T5-1　资源子包关闭

6 个 bundle 文件各加一行（在 `[resource]` 段下）：

```text
export_enabled = false
```

文件：`TheGame/Audios/AudioBundle.tres`、`TheGame/DataTables/DataTableBundle.tres`、`TheGame/Entitys/EntityBundle.tres`、`TheGame/Scenes/ScenesBundle.tres`、`TheGame/Sprites/SpritesBundle.tres`、`TheGame/UIs/UIsBundle.tres`。

修改第 40 章：在“资源 bundle 根目录保持稳定”这句后追加：

> 首发版本（Windows、Android）全部 bundle 设 `export_enabled = false`，资源进主包；Android 的可执行文件位于 APK 内，`ProcedureUpdate` 读取的安装目录 `subpackages/` 不存在。恢复子包前须先让子包随 APK 分发。

## T5-2　Windows 导出预设

`export_presets.cfg` 已有 `preset.0`（Windows Desktop）。只改：

```text
include_filter="*.bytes, *.txt"
```

其余不动。

## T5-3　Android 导出预设

在编辑器里新建 Android 预设（`Project → Export → Add → Android`），然后核对 `export_presets.cfg` 中新增的 `preset.1`：

| 选项 | 值 |
| --- | --- |
| `name` | `"Android"` |
| `export_path` | `"../ExportApp/Android/GameZMXY.apk"` |
| `include_filter` | `"*.bytes, *.txt"` |
| `gradle_build/use_gradle_build` | `false` |
| `architectures/arm64-v8a` | `true` |
| `architectures/armeabi-v7a` | `false` |
| `architectures/x86_64` | `false` |
| `package/unique_name` | `"com.zheliku.gamezmxy"` |
| `package/name` | `"造梦西游"` |
| `screen/immersive_mode` | `true` |
| `screen/edge_to_edge` | `false` |
| `screen/support_small` | `false` |

在 `project.godot` 增加以下 Android 专用设置（不放在导出预设里）：

```ini
[display]

window/handheld/orientation=0

[rendering]

renderer/rendering_method.mobile="gl_compatibility"
```

`orientation=0` 为 landscape；不要把它写成不存在的 `screen/orientation` 预设项。Android 使用 Compatibility 渲染器覆盖更多 2D 设备；Windows 的渲染设置不改。

`edge_to_edge = false`：不让画面延伸到刘海和导航栏下面，系统会自动避开刘海，不需要处理安全区。`immersive_mode = true`：隐藏状态栏和导航栏。

签名：Debug 签名用 Godot 自动生成的 debug keystore 即可。Release 签名需要用户提供 keystore，**不要**自己生成并提交 keystore 或密码。

Android SDK / JDK 路径在编辑器设置里配置（`Editor Settings → Export → Android`），这是本机设置，不进仓库。本机已有：JDK 17 位于 `S:\Unity Editor\6000.3.24f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK`，SDK 位于同目录的 `SDK`。导出模板已安装：`%APPDATA%\Godot\export_templates\4.7.2.stable.mono\android_*.apk`。

.NET：项目 `GodotProject.csproj` 为 `net10.0`，沿用现有目标框架，不为导出降级。若本机缺少 Godot .NET Android 所需 SDK 或工作负载，记录完整导出错误并停止上报，不自行安装到工作区外。

## T5-4　导出与冒烟

1. 导出 Windows：编辑器 `Export → Windows Desktop → Export Project`（Release）。运行导出的 exe，完成：新建单人档 → 选悟空 → 进 1-1 → 打完 → 回地图 → 保存 → 退出 → 重开读档。
2. 确认导出目录下**没有** `subpackages/` 文件夹。
3. 导出 Android Debug APK，`adb install` 到真机（`adb` 在 SDK 的 `platform-tools`）。在真机上完成与第 1 步相同的流程。
4. 真机上额外检查：
   - 支持的16:9～21:9横屏内无素材覆盖不足或比例策略造成的黑条；至少记录一台手机实际尺寸，其他目标比例用独立预览/模拟器补验，系统刘海/导航保留区另记；
   - 四向箭头、攻击、跳跃、技能、无双全部可用；
   - 升龙（↑+攻击）、下跳（↓+跳跃）可用；
   - 菜单点击可用：地图选关、背包装备、技能学习、设置；
   - 开始页没有“双人”按钮；
   - 退到后台再回来，游戏状态保持；
   - 按系统返回键，游戏不退出。

没有真机时，用 Android Studio 模拟器（2400×1080，API 34）完成画面、安装和单指冒烟，交付说明注明；多指组合仍记“待真机”，此时不得把 T5 判为全部完成。

## T5-5　记录

在 [Validation.md](Validation.md) 顶部按既有格式写入 T5 的验收结果。在 [ProjectTour.md](ProjectTour.md) 的“启动”部分追加一小节“导出”，写明两个预设的位置和 `include_filter` 的原因（3～5 行）。

## T5 完成判定

- Windows exe 和 Android apk 都已产出，路径写在交付说明里。
- T5-4 的每一项都有结果（通过 / 失败 + 截图）。

**停止，等待用户审查。**

---

# 第九部分　首发不做的事（遗留项）

以下内容已经评估，明确不在本计划内。执行者不得顺手实现。

| 项目 | 原因 / 现状 |
| --- | --- |
| 21:9之外的窗口装饰留边 | 超过1377视野上限时接受原生黑边；4:3平板另行适配。支持范围内的原画外扩展按2.12实施，不属于本项遗留。 |
| Android 返回键打开设置菜单 | 需要在 ProcedureLevel 处理 `NotificationWMGoBackRequest`，涉及流程，单独立项。 |
| 触屏键位自定义、透明度设置 | 需要设置页新选项和存储，首发固定。 |
| 鼠标悬停才有的内容 | 地图地点悬停动画和图鉴浮层（`MapLocationButton.cs:43-60`）、选角悬停变彩色（`CharacterForm.Logic.cs:53-57`）、存档删除键悬停（`SaveForm.Logic.cs:90-91`）、装备详情 Tooltip（`EquipmentCell.cs:65-104`）。触屏上点击会先触发一次悬停再点击，功能可用，只是看不到悬停效果、装备详情浮层不出现。首发接受，装备详情改为“点击查看”单独立项。 |
| Android 双人（手柄） | D-3。 |
| 热更新 / 子包 | D-5。 |
| 平板与任意超宽比例 | 当前范围为Windows16:9及手机16:9～21:9；不以牺牲角色尺度补齐任意比例。11关短关取景仍须按T3-3完成，不列为免验项。 |
| 悟空第 4 个直接技能键 | 悟空只有 3 个直接技能，触屏第 4 键由 `GetSkillByInputAction` 返回 null 后隐藏，不保留无效按钮。 |

---

# 附录 A 关卡数据校准表（待T3按1377重新生成）

旧表按1312/372计算，已被当前21:9目标废止；其历史内容保留在Git/用户原暂存版本，不再作为可执行坐标。本轮只审美术，不代替T3重新移动关卡节点。

T3须从当前场景生成新表，至少列出关卡、节点路径、原/新世界坐标、阶段区域宽、屏外关系、地面/墙证据和验收截图；11关短关契约单独裁决。没有完成复核前，不宣称1377门与刷怪数据已就绪。
