# 官方样式核对与战斗完善（2026-10-11）

本说明记录本次实现与证据，不替代 `ProjectGuidelines/`。用户确认保留混合风格：花果山编排按一代《七魔王篇》核对；小怪血条与现有 HUD 保留三代风格；角色无双残影按本地旧项目效果实现。

## 官方依据

来源为 [4399 造梦西游官方游戏入口](https://www.4399.com/flash/zmhj.htm?g=1) 指向的公开游戏资源，直接核对实际程序参数：

- 一代：[游戏加载页](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/v25928.htm)、[游戏 SWF](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/v25928.swf)。核对内嵌游戏的 `GameSence` 前四个 `stopPointIdx`、`MonsterAppearPoint.__step` 与 `MainGame.maxMonsterPerScreen`。
- 三代：[游戏加载页](https://sda.4399.com/4399swf/upload_swf/ftp7/hanbao/20120107/6/v260714.htm)、[游戏 SWF](https://sda.4399.com/4399swf/upload_swf/ftp7/hanbao/20120107/6/v260714.swf)。核对 `BaseMonster.drawMonsterHp`：红色矩形宽 50、高 5，黑色描边 `lineStyle(1.2, 0)`。本项目此前小条是 58×4，现改为 50×5；黑边按当前渲染器对齐为四周各 1 个屏幕像素，避免分数坐标导致不对称。

资源下载、只读分析输出和检查图在忽略目录 `.godot/validation/`，不作为游戏依赖提交。尺寸为设计视口 940×590 下的像素；窗口缩放沿用现有 `canvas_items + keep`。

## Level_1 配置

`LevelConfig.xlsx` 保持嵌套 `Stages/Recipes` 格式、现有四段相机抵达激活与门 ID；只调整怪物编排、计时和并发上限，不搬入原版坐标或旧刷怪脚本。

| 阶段 | 花果山猴子 | 妖猴 | 配方首次生成时刻（阶段启动后） |
| --- | ---: | ---: | --- |
| 入口战 | 3 | 0 | 猴子 3s，间隔 1s |
| 林间战 | 5 | 2 | 猴子 3s，间隔 1s；两只妖猴同在 6s |
| 山道战 | 0 | 6 | 三只妖猴同在 4s；另三只从 6s 起，间隔 2s |
| 洞口战 | 5 | 2 | 猴子 3s，间隔 1s；妖猴分别在 4s、6s |

总计 23 只（猴子 13、妖猴 10），单人并发上限 6。容量不足时继续使用现有 `LevelSpawnSchedule` 的预约与补位规则。

原版生成点先等待 `delay`，再等待首个 `interval`；本项目首只等待 `SpawnDelay + Delay`，所以将原版 `delay + interval` 写入本项目配方 `Delay`，阶段统一延迟设为 0。原版多个同时出怪的生成点合并为同怪物、同首发时刻、间隔 0 的配方，仍引用现有场景生成点。原版第五个停点包含 Boss 与伴随小怪，按用户要求整波暂不接入；当前通关仍发生在第四波之后。

## 无双的运行期与表现

- `MusouGauge` 保持纯 C# 状态源，新增激活、剩余秒数、物理步长推进及清空；激活与结束仍通过聚合 `Changed` 刷新 HUD。开启期间不续充、不重开，满值提示停止，数值随剩余时间耗尽。
- `HeroEntity` 拥有开启入口，空格绑定 `musou`。持续时间 12.5 秒、攻击独立倍率 1.5、横向走跑倍率 1.5 来自旧项目，新增为 `BattleConfig.xlsx` 的三个可调字段；由既有 Luban 入口生成代码与二进制，并在 `ConfigValidator` 校验。
- 攻击加成使用独立 `StatSource`，不修改成长或装备基础值。霸体由身体接口提供能力，清掉待受击事实并解除已有硬直；生命伤害仍正常结算。跳跃与动画速度保持原有配置。已经装填的攻击仍遵守项目的出招快照规则。
- 残影由英雄自己的 `MusouAfterimage` 节点负责；悟空场景通过导出数组显式绑定身体和武器层。默认四组固定缓存（八个精灵），每 0.1 秒采样，0.27 秒暖色淡出，保留采样时的位置、朝向与当前帧，不复制碰撞、动画或玩法节点。
- 耗尽/死亡移除本次无双的属性来源并停止采样；隐藏/池复用清零状态并清空残影。状态不进入档案或存档；没有新增管理器、全局计时器或每帧实例化。
- 生命条仍为即时更新。`ResourceBar` 的可选文字适用于不同场景；角色拖影属于英雄表现，不重新加入血条延迟字段。

## 字体与 UI 目录

迁入旧 HUD 的粗圆数字字体、完整中文粗圆回退与等级海报体，映射见 [LegacyAssetMap.md](LegacyAssetMap.md)。共享主题经 `game_font.tres` 使用字体组合；BattleHud 的生命、魔法、经验标签显式绑定该字体，字号 14px；等级标签另设海报体 16px。根主题不能跨过 `StatusPanel` 的 `Sprite2D`，因此不能依赖其继承到这些数字标签。

已核对框架 `Godot/docs/UISystem.md` 第 6 节、`ScriptGenerateInspector.ResolvePaths` 与 `ScriptGenerateRes`：框架默认 Logic 输出 `GameScripts/UI/`、Ge 输出 `GameScripts/GameProto/UIGe/`。本项目第 00/40 章明确按界面分目录，BattleHud 的三个文件保留在 `GameScripts/UI/BattleHud/`；第 40 章另明确其 Ge 样板同目录并由既有工具重生成。启动/共享 `LoadingForm`、`QuestionTips` 保留在 `MainPack/Scripts/UI/`。当前业务 Logic 目录符合项目约定，无需迁移。

## 规范依据与验证

遵循第 00 章的分层与状态通知、第 20 章源表和 Luban 流程、第 30 章实体生命周期/属性来源/攻击快照、第 40 章导出绑定与资产登记、第 50 章既有生成器及验证边界。第 00/40 章仍有旧血条残影/三参数控件描述，与用户此前要求删除该功能后的实现不同；本轮保持规范原文不变，差异见 [MonsterHealthBars.md](MonsterHealthBars.md)。没有改动框架、插件、Luban 工具或旧项目。

验证结果：

- `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`（`AI_MODE=1`）：成功；两张源表已检查渲染，源表与生成物同步。
- `dotnet build --no-restore`（Godot 项目目录）：通过，0 错误，5 项既有框架过时 API 警告。
- `dotnet test Tests/BattleTests --no-restore`：84/84；`Tests/ProfileTests`：24/24；`Tests/LevelTests`：16/16。
- Godot `--headless --path . --quit-after 12000 -- --smoketest=ui`：`SMOKE PASS`，含真实空格输入、未满/重复开启拒绝、攻击与移速加成、霸体仍扣血、残影定位与淡出、计时还原、死亡/隐藏/池复用，以及两种小怪即时血条。
- Godot `--headless --path . --quit-after 12000 -- --smoketest=level`：`SMOKE PASS`，四段移动、相机、物理门、延迟、23 只怪物与通关结算通过，经验 63 与配置一致。
- Godot `--headless --editor --path . --script res://EditorScripts/regenerate_collection.gd`：105 项资源生成完成，新字体和字体组合已登记。编辑器扫描未再出现新字体加载失败。
- 真实 OpenGL 渲染检查 HUD 数字、中文回退、小怪 50×5 血条与角色淡出残影；字形检查通过。预览在 `.godot/validation/optimization/battle_polish_preview.png`。
- `python Tools/ProjectMaintenance/validate_game_references.py`：`REFERENCE PASS`（100 条路径、166 个 UID）；`git diff HEAD --check`：通过。

UI 烟测包含故意破坏 HUD 导出绑定的失败清理分支，因此日志会有对应的预期报错。两个烟测退出仍出现第 50 章已登记的 `DefaultWebRequestAgentHelper.Reset` 已释放节点异常；Collection 编辑器退出仍有插件 RefCounted/free 异常及既有入口 UID 警告，断言与退出异常分别记录。Boss 波按计划留待后续实现。

## 字体与对称性复查（同日后续）

用户要求仔细确认后，复查发现初次资源/预览检查遗漏了三处实际问题，已修正：

1. HUD 根 Theme 被中间的 `Sprite2D` 隔断，三个数字标签实际解析为 Open Sans SemiBold。现分别显式绑定 `game_font.tres`，Godot 运行时读取均为 `FZCuYuan-M03S`、14px；等级读取为 `DFHaiBaoW12-GB`、16px。主题传播规则见 [Godot Control 官方文档](https://docs.godotengine.org/en/stable/classes/class_control.html#class-control-property-theme)。三个迁入字体与旧项目原文件的 SHA-256 比较全部相同；三代公开 SWF 的 DefineFont3 第 129 号记录也使用 `FZCuYuan-M03S`。
2. 两个怪物实例仍覆盖为 58 像素宽，而可见填充纹理仅 50 像素宽，造成可见条相对代码计算的矩形中心左偏 4 像素。已同步实例为 50×5，并校准编辑器初始位置；运行时仍由既有头顶锚点定位。回归改为核对纹理的可见中心与头顶中心，并覆盖左、右朝向。
3. 0.6 像素边距在真实 OpenGL 渲染下得到左/上 1 像素、右/下 0 像素。改为四周整像素黑边后，满血红条像素包围盒 50×5、含边框包围盒 52×7，左、右、上、下均为 1 像素。

此次复查遵循第 40 章场景导出与资源绑定约定，没有改变控件、实体或框架职责。验证：`dotnet build --no-restore` 通过（0 错误，5 项既有警告）；新增字体解析、实例尺寸与左右朝向断言后 `-- --smoketest=ui` 输出 `SMOKE PASS`；`validate_game_references.py` 输出 `REFERENCE PASS`（101 条路径、166 UID）；`git diff HEAD --check` 通过。实测日志在 `.godot/validation/optimization/font_bar_audit.log`、`bar_pixels.log` 和 `font_bar_ui_smoke.log`，940×590 花果山预览为 `battle_polish_preview.png`。退出时仍有上述已登记框架异常，未改动只读依赖。
