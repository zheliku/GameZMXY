# Windows / Android 双端发布实施计划

状态：**按用户最新决定，跨平台代码实施暂缓，先完成美术方案与分批制作。T1已通过，T2已有修改保留但未完成，T3～T5未开始。** Windows按1920×1080（16:9）验收；横屏手机覆盖16:9～21:9，平板另行适配。这些只作为当前美术覆盖边界，不提前启用手机端配置。先审查2.12的A0方案，再按批次制作、审图；美术交付后由用户决定何时恢复UI与手机适配。正式宽屏仍须与T3关卡数据一起启用。

本计划取代 [CoopImplementationPlan](CoopImplementationPlan.md) 之后的“发布”方向。双人计划 A～E1 已经完成，本计划不改动它的数据形状，也不回滚任何双人成果。

---

# 第一部分　执行者须遵守的规则

## 1. 任务粒度与停止点

- 计划分为**大任务**（T1、T2……）和**子任务**（T1-1、T1-2……）。
- 一次只做一个大任务。按顺序完成其中所有子任务，跑完该大任务的“完成判定”，写交付说明，然后**停止**。不得顺手开始下一个大任务。
- 通常子任务之间不需要停下；当前美术前置阶段按2.12的A0～A5单独分批审查，不连续自动生成。每批说明素材、结果和验证方式。
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

当前基线：构建 0 错误 / 5 个既有警告；规则测试 Profile 339、Battle 197、Level 19 全部通过。数字下降就是回归，必须查清。

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
| D-9 | 美术扩展 | 原始文件及内容保留；只向外扩绘。静态背景优先2倍精度母版，运行时资源按平台实际预算导出；不统一放大角色、图集和逐帧演出。清单、坐标与验收见2.12。 |

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

静态背景导出画布建议1408×590，原940×590置于(234,0)，留出约15.5px/侧的21:9绘制余量；画布宽度不是玩法视野宽度。941×591源图按其真实尺寸保护，不裁为940×590。

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

**范围与状态。** 用户最新要求先搁置跨平台实施，制定美术方案、优化素材后再适配手机。本节成为恢复T2之前的美术前置工作；当前只完成A0盘点、画面证据和计划，交用户审查，不继续生成或接入。已有UI修改保留，不回滚、不继续推进。`Docs/gpt.md`是用户参考意见，具体采用情况见下表，不覆盖集中规范。

#### 2.12.1 对 gpt.md 的项目化裁决

| 建议 | 本项目采用方式 |
| --- | --- |
| 统一逻辑分辨率，按比例扩展视野 | 采用。保留当前940×590及原运动/碰撞坐标；1280×720只是文中例子，不迁移本项目坐标。Windows1920×1080是输出规格。 |
| canvas_items + keep_height | 采用于T3；手机最大按21:9核对，旧1312上限改为1377。 |
| Expand填满平板 | 本轮不采用；平板另行适配，不增加纵向玩法视野。 |
| 全屏TextureRect用Keep Aspect Covered裁切 | 不采用旧图裁切，用户已否定。扩展画布在支持视野中显示，完整保留原画核心；可以裁去扩展画布外围余量，不能裁掉原画。 |
| 分层Parallax2D、无缝重复 | 保留现有分层与Parallax2D；本项目天空含固定构图，未证明可无缝平铺，不强行repeat，也不更换现有滚动观感。 |
| 地形改用TileMapLayer | 不迁移。当前正式关卡已有完整手绘地形与原生碰撞，宽屏美术扩展不需要重建地形体系。 |
| 2倍精度美术母版 | 用于新增背景绘制和离线母版；原图保留，中心不由AI重画。运行时不统一2倍，尤其不放大48帧演出和角色图集。 |
| UI锚点、安全区 | 沿用GGF CanvasLayer、现有Form/组件和锚点。安全区及实际系统栏边界留在T4/T5真机验收，不能宣称扩图已解决刘海和手势遮挡。 |

#### 2.12.2 全量盘点口径及结论

扫描`Godot/GodotProject/`的源文件，排除`.godot`、bin/obj和生成试验输出；共 **661个图像源文件（656 PNG、1 JPG、4 SVG）**。其中657在Sprites、2在主包主题、2为工程图标/插件图标。对全部文件读取尺寸/格式及直接资源引用，扫描1182份场景、资源、脚本等文本；再针对屏幕背景、48帧无双和11关图层进行透明通道、图集、节点尺寸与覆盖检查。不能因文件很宽就判断它是背景，也不把Collection Res自动常量当作玩法消费者。

| 领域 | 文件数 | 本轮结论 |
| --- | ---: | --- |
| UI | 225 | 4张静态全屏背景需扩；48帧无双需扩展屏幕覆盖；其余173张保持原样，使用锚点/遮罩适配。 |
| 角色、敌人、Boss及配套图集 | 116 | 保持，不扩画布、不改变注册点/格尺寸/碰撞。牛魔王`bull_background.png`是881×440实体局部效果，不是全屏背景。 |
| 世界特效、球、飘升、升级/死亡、受击 | 189 | 保持透明边与动画原点，不用AI填满透明区域。 |
| 关卡贴图 | 37 | 5张共享天空扩展；4张短前景/地面复审候选；其余28张保持原样。 |
| 装备、物品、消耗品图标 | 79 | 保持原图；触屏技能键复用既有图标，不为宽屏重新生成。 |
| 数字 | 10 | 保持图集排列及透明间距。 |
| bhpic、主题SVG、工程/插件图标 | 5 | 保持；只读插件素材不修改。 |
| **合计** | **661** | **9张确定静态扩图，48帧演出覆盖适配，4张条件候选，600张保持；另新增1套选角页侧景。** |

逐文件清单与引用明细见[661项CSV](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/classified_inventory.csv)及同目录`classified_inventory.json`；制作计划与决定只维护本文件。[屏幕美术接触表](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/screen_art_contact.png)用于对照原画、透明面板与演出。11个仅见自动常量、未查到其他静态消费者的旧图/数字也计入保持范围，不混入本轮删资源。已有图集的空格、padding、透明裁切均不是待补黑边。上述验证材料在本地忽略目录，不作为正式游戏资源。

#### 2.12.3 确定扩展的四张静态UI背景

下列路径均相对`TheGame/Sprites/`。每张只生成新外围内容，使用独立`_wide.png`，原文件与UID不覆盖。原图完整RGBA区域粘回最终1倍导出，再检查像素差为0；JPG以其原始解码RGB/RGBA为准，不对它反复有损保存。

| 编号 | 源图 / 当前尺寸 | 消费者 | 1倍扩展规格与重点 |
| --- | --- | --- | --- |
| A-UI-01 | `UI/frontend/title_backdrop.png`，941×591 | `start_form/Frame/Background` | 1409×591，旧图置(234,0)。延续环境、画框/墨迹和菜单侧景；中央人物、标题及原黑色菜单设计全部保留，不再Cover放大。显示时保留旧图原有映射比例，不能把941源图硬裁成940。 |
| A-UI-02 | `UI/frontend/journey_map.png`，940×590 | `map_form/Frame/Background` | 1408×590，旧图置(234,0)。只延续海岸、远山、海水；不新增岛屿、路线、入口、文字和地点光效。接入时背景相对原Frame左移234，抵消新画布保护区偏移；所有地点、悬停轮廓、图鉴锚点保持原Frame内坐标。 |
| A-UI-03 | `UI/skills/skills_background.jpg`，940×590 | `skill_form/Content/Backdrop` | 1408×590，旧图置(234,0)。延续棕色水墨和上下带状构图；表格、说明、图标、页签与余额保持原布局。原背景刻意的黑带不涂改。输出新PNG。 |
| A-UI-04 | `UI/settlement/settlement_backdrop.png`，940×590 | `settlement_form/Content/Backdrop/Background` | 1408×590，旧图置(234,0)。延续蓝色水墨；背景上已有“4399”等笔画也保留，不复制到新边缘；胜败图、成绩条和按钮不重绘。 |

**选角页 A-UI-05 为新增侧景，不扩四张人物卡。** `character_form/Background`目前只是近黑ColorRect，无完整背景位图。制作1408×590（母版2816×1180）的低对比水墨侧景，原940×590中央保持现有底色/透明保护，不改四张卡的235×590显示槽、人物、姓名、1P/2P和底部按钮。可将背景层作为既有ColorRect之上的被动TextureRect，不增加脚本或Manager；先审图确认风格。

**明确不扩：** `save_slots_backdrop.png`虽为941×591，但实际是透明画布中的存档窗框；`inventory_panels.png`同尺寸但有效alpha范围为(110,53)～(865,551)，属于背包面板。二者保留居中，外围显示其下层地图/关卡或全屏遮罩。设置卷轴、删除框、提示、LoadingForm和QuestionTips同理；全屏遮罩用原生Control铺满，不用重画按钮/窗框。

#### 2.12.4 五张共享天空：扩图保留原视差

现有原图均1440×690，视差系数并不相同。以当前整个场景镜头区间、最大视野1377核对，若天空原点为0，最右端所需宽度为：`1377 + scroll_scale.x × (scene_width − 1377)`。计算包含原点、共享关系与实际节点尺寸；短关卡另算，不把公式机械套到世界宽度小于视野的情况。

| 编号 / 源图 | 复用关卡 | 保留原系数时的宽度需求（向上取整） | 制作规格 |
| --- | --- | ---: | --- |
| A-SKY-01 `Levels/zm1_01_huaguoshan/level_1_bg_end.png` | 1、2、3 | 最大1819（1关约1809） | 2048×690；原图仍在(0,0)，仅向右扩608，不平移已有世界内容。 |
| A-SKY-02 `Levels/zm1_04_bat_cave/bat_cave_sky.png` | 4 | 1819 | 同上，保持洞穴远景与光照。 |
| A-SKY-03 `Levels/zm1_05_qixing_cave/qixing_cave_sky.png` | 5、6 | 1819 | 同上，保留原洞穴轮廓。 |
| A-SKY-04 `Levels/zm1_07_shore/sea_shore_sky.png` | 7、8、9、11 | 7～9为1819；11另按短关镜头核对 | 同上，延续海底氛围，不复制原龙形焦点。 |
| A-SKY-05 `Levels/zm1_10_bull_demon_valley/bull_demon_valley_sky.png` | 10 | 1661 | 统一2048×690，保留本关约0.49456滚动关系及原火山焦点。 |

这里不把原图居中进2048后忽略Sprite位置：原图在世界中已注册，向右续画即可，旧原点、缩放和视差系数不变。2倍母版为4096×1380，原图对应区域2880×1380；最终运行版仍须合回原始1440×690并检查像素一致，保留原有alpha，不统一改成不透明图。

**证据范围：** 11个正式场景已以原生SubViewport按940/1377宽、起点/最右界分别渲染，共44张；临时清屏色为洋红，便于识别未覆盖区域。1377下关卡1～10的全场景最右界出现露底，940下均未出现；这证明素材覆盖缺口，不代表真实通关或所有阶段都已验证。当前阶段门可能比场景总右界更早停止，相机最终可达区间仍在T3验收。

审查用[11关端部对照图](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/level_end_contact_final.png)，洋红表示图层未覆盖；它是临时清屏色，不是新绘素材或正式游戏画面。

#### 2.12.5 短关卡端部：四张条件候选，不一律加长地形

| 编号 / 源图 | 原尺寸 / 当前世界覆盖 | 处理边界 |
| --- | --- | --- |
| A-END-01 `Levels/zm1_10_bull_demon_valley/bull_demon_valley_front.png` | 1880×590，x=-30～1850；场景总右界1951 | 全场景端部有101px装饰覆盖余量缺口；先与最终可达右界核对。需要时只补右侧岩壁/熔岩装饰，旧内容与位置不动。 |
| A-END-02 `Levels/zm1_10_bull_demon_valley/bull_demon_valley_ground.png` | 1896×95，x≈-16.05～1879.95 | 相对场景总右界少约71.05px；现末阶段右门1814.95仍在图内，不能直接称正式关卡当前必然缺地面。若最终取景露出端部，补不可玩侧景，不擅自延长碰撞。 |
| A-END-03 `Levels/zm1_11_hidden_dragon_palace/dragon_palace_front.png` | 944×504，x=9～953；场景总宽1222 | 没有洋红露底（底层天空仍覆盖），但21:9镜头会放大装饰不连续的问题。先确定短关镜头，再只续画边缘海底/礁石，不复制中心宝剑。 |
| A-END-04 `Levels/zm1_11_hidden_dragon_palace/dragon_palace_ground.png` | 1300×97，x≈-43.4～1256.6 | 原关内覆盖足够；宽视野若看到两侧之外，需要画与物理墙协调的封闭边景，不能伪造可行走区域。 |

本轮保留这些原图，不先导出一个随意长度。边缘位置与长度须在T3短关镜头裁决后落数；最初四张母版可先绘制外部装饰草图，禁止据此移动墙、刷怪或平台。

#### 2.12.6 两套无双演出：48帧覆盖问题，禁止48次独立AI重绘

- `UI/hud/musou_activation/wukong/wukong_musou_activation_01.png`～`24.png`，24张940×590；唐僧同结构另24张。已经逐帧接触表及alpha检查；它们包含半透明暗幕、横向光束、人物与“无双开启”，两边不能简单拉伸复制，也不能当静态菜单背景处理。
- 原始来源已有`Tools/LegacyMigration/export_official_musou_activation.py`：Role_v7的WsEffect59，公共背景52、悟空55、唐僧58；24fps、24帧原生时钟与现有AnimationLibrary保留。先核实源与遮罩后沿用这个作者入口，只扩公共背景的外侧范围，不重画人物/汉字。
- 优先做公共背景边缘随原时间轴扩展，中央继续显示现有原帧；若分层重导不能做到中央内容/透明度一致，再离线合成两套宽帧。两条路径先用1/8/12/24帧验证再定，不能保留两套同时播放的效果或增加第二个暂停时钟。
- 新边缘只允许暗幕、光束和已有光效的连续外延；不得生成额外人物、文字或每帧不同的新纹理。检查alpha与叠加后的画面，不能只检查PNG的RGB。
- 不统一上2倍运行版：48张原940×590 RGBA8约101.6MiB；全改1408×590约152.1MiB，再全2倍约608.4MiB（均未计mipmap/压缩）。这是实际文件规模推算，不能把压缩PNG文件大小当显存。以单套当前英雄实际加载和Android导入结果验收，先复用/分层，再决定是否导出新帧。

#### 2.12.7 提升美术精度时保留原图、母版和运行版

1. **原图基线**：保留原文件、alpha、UID、图集region、注册点和节点变换，记录SHA256与原解码像素；已有美术不被模型直接覆盖。
2. **高精度母版**：静态UI按2倍制作（普通背景2816×1180、标题2818×1182、天空4096×1380）。低清原图2倍放大只作构图/拼接参照，不能声称增加了真实细节；有可靠原矢量源时可按原注册重导更高清版本，并单独审查中心内容。没有原矢量源时不让AI“修复”人物/字体/地形。
3. **可用运行版**：1倍新画布保留原图中央/已注册区域的逐像素内容，只将新绘外围降采样后合入；Windows若选择高清运行背景，须验证映射比例与内存，作为后续独立导出选择，不提前复制整套业务场景。Android优先采用预算内运行版，不统一把全部661项升2倍。
4. 美术2倍不改变Control设计尺寸、人物Scale、移速、跳高、碰撞或GGF/QF架构。1倍中央像素一致和2倍母版内容保持是不同验证口径，不能混称像素无损放大。

#### 2.12.8 生成、合成、接入和验收

**已验证工具路径：** 内置image_gen当前不可用；用户已明确要求尝试本机ECPA，现有本地EasyCLIProxyAPI的`gpt-image-2`编辑接口已成功出图，凭据仅进子进程环境，不进仓库/日志。使用imagegen技能的原CLI，不修改技能脚本或代理配置。此前整图及单边样稿改变了受保护区域，合回原图后仍有接缝，均未通过成品验收，未接入生产。

**固定流程：** 原图及场景引用登记 → 原尺寸保护区/蒙版 → 2倍外围生成 → 原图核心精确合回 → 只在新增区修接缝 → 离线多比例合成 → 忽略目录中的原生场景预览 → 分批交付母版/运行候选图与清单。美术阶段不切换正式场景引用；恢复T2/T3后再接入原Form/Parallax资源并验证完整流程。任何接缝处理都不能跨进保护区悄悄改原像素；模型输出尺寸和蒙版不可信，必须实测。先完成一张地图，证明流程可行再批量。

**验收标准：**

- 旧原图未覆盖，1倍成品保护区尺寸/偏移正确、像素差0（包含alpha）；2倍母版保留内容，不伪称新增了原画细节。
- 新旧边界在100%和200%预览中无竖缝、色带、镜像重复、拉丝或光照突变；不得用模糊背景或裁主体掩盖接缝。
- Windows1920×1080、手机2160×1080/2340×1080/2400×1080/2520×1080；菜单背景覆盖全部支持画面，主体和文字不变形，既有按钮仍与原画对齐。宽图多出部分只裁新增余量。
- 十一关起点、实际末阶段与相机极限、关键转场验证，天空不露底，前景与地面接缝不悬空，原物理墙/平台不因画图移动。
- 两名英雄无双完整24帧、跳帧/首尾/暂停与复用验证，外围无闪烁、文字/人物不重复，原暂停释放与帧时序保持。
- 原生遮罩全屏、鼠标/触屏命中不被背景节点拦截；触屏安全区在T4/T5真机确认。
- 记录Windows与Android选用纹理、实际导入尺寸/格式及显存/帧率证据，不仅记录磁盘PNG大小。来源和生成提示词登记在既有LegacyAssetMap，命令与结果写Validation；不再建第二份交付报告。

#### 2.12.9 美术先行的工作顺序与停止点

| 顺序 | 子任务 | 交付/停止点 |
| --- | --- | --- |
| A0（当前） | 661项清点、gpt.md裁决、覆盖截图、修订本计划 | **本轮只到这里，交用户审查。** 不继续生成或替换素材。 |
| A1 | 地图扩图流程试制 | 1张可审查样稿、保护区像素结果、16:9/20:9/21:9预览、母版/运行版；先审图，再批量。 |
| A2 | 其余3张静态背景 + 新选角侧景 | 逐图审查，字体、人物、地图热区不漂移；不改窗口业务。 |
| A3 | 五张天空，按共享关系一次制作 | 离线层合成与原系数边缘预览；先审美术，正式关卡改引用/启用仍与T3协同。 |
| A4 | 两套无双外围连续性 + 短关侧景复核 | 先公共背景/关键帧，确认再处理完整24帧；短关四候选先做只读取景核对，不能在不确定长度时盲画。仍依赖关卡契约的部分明确留到T3，不冒称已完成。 |
| A5 | 美术成果汇总与交付审查 | 交付已审母版、运行候选图、原图映射、保护区与接缝/连续性结果，以及未决短关清单。正式引用与代码保持；用户确认后再恢复T2的UI接入及T3～T5适配。 |

T2已经实施的锚点/生成绑定/验证路径调整保留；前端与单人完整烟测已有通过，双人一次移动断言失败、补充诊断后通过，原日志保留，不将其解释成已找到根因。美术先行不等于T2完成。下列跨平台任务仅保留为恢复时的计划；当前不推进关卡节点、显示设置和触屏逻辑。

---

# 第三部分　执行顺序总表

**当前先执行美术A0～A5（见2.12）；下表的T2～T5暂缓，恢复前先由用户审查美术成果。**

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

**目标**：完成窗口在940～1377逻辑宽度下的锚点、美术扩展与UI接入。先审查2.12的A0清单，再逐批审图；正式项目继续keep，世界宽屏在T3启用。不改玩法/碰撞，不先制作替代关卡。天空新图可在本任务试制，正式关卡资源切换与取景协同T3。

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

用户已否定旧图裁切和普通黑边方案，选择在原画外扩展重绘：开始页与地图保留原始素材及中央构图，新增宽屏背景版本填补两侧，不拉伸主体、不改交互坐标。地图源图940×590，左右各扩234至1408×590；开始页941×591扩到1409×591。素材生产以2.12为准，ECPA接口已验证可调用；此前样稿接缝未过，不把裁切或未完成样稿当成成品。全屏ColorRect遮罩仍使用全屏锚点。

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

- 美术前置阶段A0～A5已经审查，确定需要的UI背景/演出边缘已接入并通过保护区、接缝、内存和多比例验收；天空随T3接入，未决短关候选明确留T3，不冒称已扩。
- 最低验证全部通过，生产显示设置保持 T1 状态。
- 完整烟测三项都在 `--resolution 2400x1080` 下运行并 PASS：`--smoketest=frontend`（`--quit-after 18000`）、`--smoketest=ui`（30000）、`--smoketest=ui --coop-only`（30000）。正式视野仍为 keep，不接受由宽屏引起的关卡初始化失败。
- 在940×590、1920×1080、2400×1080、2520×1080保存画面；这些正式窗口仍等比保留旧构图，已有2560×1080截图仅作额外尺寸证据。另用忽略目录中的一次性UI预览，在940、1049、1377逻辑宽度下验证菜单、存档、设置、背包、技能、结算和单/双HUD的实际锚点。预览只实例化原场景，不载入关卡、不修改生产项目设置。
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

Windows按16:9验收；Android全屏按设备实际像素尺寸输出，不把手机固定为1920×1080。这里的窗口输出覆盖值不改变逻辑坐标和美术注册点，T2不提前写入。

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

在编辑器打开11关，检查原标记/业务ID/资源引用；逐关在940、1049、1312和1377宽度验证阶段推进、相机抵达和屏外出生。生成点y、平台几何和数值源不因绘图随意改变。

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
