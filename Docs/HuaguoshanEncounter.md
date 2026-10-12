# 花果山守关、战斗节奏与代码整理

> 本文保留前一轮的实现与验证记录。后续已合并 LevelRun/LevelController 为 LevelModule、统一 SaveModule/Helper/Node 命名、移除进场弹窗和三秒通关等待；当前阅读路径见 [GameplayArchitecture](GameplayArchitecture.md)，最新验证见 [ModuleRefactor](ModuleRefactor.md)。

2026-10-12。本文记录本轮实现、来源和验证；项目规则仍以 `ProjectGuidelines/` 为准。架构入口见 [GameplayArchitecture](GameplayArchitecture.md)。

## 完成的行为

| 要求 | 实现 |
| --- | --- |
| 怪物不能在空中攻击 | `IActorBody.OnFloor` 由 `ActorEntity` 共用实现。AI 提交和身体消费都检查落地，空中旧请求作废。英雄不再单独声明落地属性。 |
| 避免贴脸瞬击和受击反打 | 首次出手等待 0.5～1 秒，后续每 1.5 秒以 50% 概率判断。追击优先招也经过反应门槛；妖猴删除旧项目特有的 RunHit。普通攻击至少有约 0.3 秒前摇。非霸体受击立即清掉待出招和攻击包，下个物理帧由身体状态机进入受击。 |
| 缩短命名空间 | 身体、英雄、怪物与 AI 分别用 `GameLogic.Body`、`GameLogic.Heroes.Body`、`GameLogic.Monsters.Body`、`GameLogic.Monsters.AI` 等模块名。目录仍保留 Core/States 的浏览分组。 |
| 核对官方成长 | 悟空成长、经验曲线和三类怪物的基础生命、攻防、经验按官方资源校正；最高等级仍采用项目的 55 级。 |
| 第一关大猩猩守关 | 第五波包含大猩猩 1 只和妖猴 9 只；普通 `GorillaEntity : MonsterEntity`，复用近战身体/AI 状态。入场题签和顶部血条只由关卡配置指定，不存在 Boss 实体继承层。 |
| 清场后使用出口 | 最后一波清场进入 `AwaitingExit`；显示循环光标。英雄存活、落地、接近并按 W 后才完成关卡、提交检查点。流程等待写入完成后返回地图。 |
| 整理两个 snapshot | 改名为 `HeroLoadout` 和 `CombatantStats`。前者是进场/升级的成长输入，后者是出招/受击时的最终属性；不混合基础值与 Buff 后的值。后者包含可选来源 `ActorId`，攻击包与防守快照均传入实际实体编号，不持有节点。 |
| 整理存档职责 | `GameContext` 位于脚本根目录，关联本槽档案和保存入口；`Save/GameSave` 负责读写；`Level/LevelRun` 负责本关奖励、结局、提交和放弃回滚。移除 Session 目录及旧命名，移动脚本保留 UID。 |

`HeroLoadout` 只在进场或升级时创建；`CombatantStats` 在每次攻击或受击时按值冻结。当前没有陷阱实体，不为其预设空层；结算快照不依赖 `ActorEntity`，非实体来源可自行提供侧别、等级和属性。

## 官方依据与数值边界

来源是 [4399 官方入口](https://www.4399.com/flash/zmhj.htm) 的一代公开资源。主程序 [v25928.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/v25928.swf) 中核对 `export.hero.Role1.upGrade`、`base.BaseRoleProperies.getPower`、`base.BaseMonster` 和第五波场景标记；怪物包 [Monster_v1.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Monster_v1.swf) 中核对 `Monster1/Monster2/Monster3` 构造参数。怪物包 SHA-256：`a6552637ac61553c3e021c79b725587cba66f6303e3245180ababd1f7db97026`。分析输出只放临时目录，未复制官方程序。

| 怪物配置 | HP | 防御 | 普攻威力 | 经验 |
| --- | ---: | ---: | ---: | ---: |
| 1 猴子 | 50 | 0 | 5 | 5 |
| 2 妖猴 | 100 | 1 | 12 | 6 |
| 3 大猩猩基础值 | 300 | 5 | 20 | 7 |

按用户后续要求，大猩猩只保留配置 3，删除守关专用配置 4。花果山 `stage_5_a` 配方使用 `Gorilla`，生命倍率 3、物防倍率 1、攻击倍率 1、经验覆盖 25，因此仍为 900 生命、5 物防、20 普攻威力、25 经验。第二关场景尚未搭建；普通大猩猩使用同一基础配置和全 1 倍率，经验覆盖填 -1。

## 刷怪倍率如何配置

在 `Configs/GameConfig/Datas/LevelConfig.xlsx` 每条 `Recipes` 的末尾填写：

| 字段 | 语义 | 基础值 |
| --- | --- | --- |
| `MonsterEntityId` | 可读怪物枚举，与基础怪物配置一一对应 | 如 `Gorilla` |
| `HealthMultiplier` | 生命上限倍率，必须为有限正数 | 1 |
| `DefenseMultiplier` | 物理防御倍率，有限且非负 | 1 |
| `AttackMultiplier` | 全部招式威力倍率，包含固定威力，有限且非负 | 1 |
| `ExperienceOverride` | 击败经验覆盖；-1 用基础奖励，0 不给经验 | -1 |

倍率不影响其他配方；没有暴击、魔防等高级属性倍率。基础怪物表保持只读，生命/物防通过现有属性来源叠加，招式装填时应用攻击倍率。每次 `OnShow` 都从基础值重新装配，池复用不会累乘或残留；`OnInit` 仅在首次实例化时读取该实体唯一的基础配置和动画参数。

经验在生成时根据配方确定，通过死亡事件携带实际奖励；`LevelRun` 不再回查怪物配置，也不再依赖怪物配置表。启动校验拒绝重复基础配置、非法倍率与小于 -1 的经验覆盖。

## 成长与伤害边界

悟空 HP = `80 + 50×(等级−1)`，MP = `50 + 20×(等级−1)`；基础攻击下限 `10 + 5×(等级−1)`，普攻额外随机整数 0～4。防御在 15 级前为 `2 + 2×(等级−1)`，之后为 `28 + 5×(等级−14)`。一级升级需要 135 经验。五波共 33 只怪物，经验合计 204，新档通关后为 2 级、69/145。

本轮校正的是以上基础数值与经验，不声称伤害公式逐项复刻：`DamageCalculator` 仍采用项目原有的比例减伤、等级压制、暴击和闪避规则；官方一代物理伤害为直接扣防。反应时间与动画前摇是本项目为可读出招做的调整，也不是逐帧移植官方 AI。

## 场景与资源

第五波战斗区域为 x=2752.85～4202.85，三个生成点及时间按 [官方花果山记录](OfficialHuaguoshan.md) 接入。保留任务开始时前四波已调整的坐标和地形，没有用文档里的旧坐标覆盖它们。

大猩猩五张图集、出口光标及血条笔刷来自只读旧项目，路径映射见 [LegacyAssetMap](LegacyAssetMap.md)。`Tools/LegacyMigration/import_gorilla_assets.py` 生成外置 SpriteFrames/AnimationLibrary；已有产物默认跳过，指定 `--force 文件名` 才重建。守关题签动画独立放在 `UIs/Animations/encounter_entrance.tres`，HUD 只实例化 `encounter_banner.tscn`。

出口使用 `Scenes/Widgets/level_exit.tscn`，动画帧引用 `level_exit_sheet.png`，避免 Collection Res 的全树 basename 冲突。实际渲染的 `gorilla_attack.png` 和 `level_exit.png` 已检查，位于忽略目录 `.godot/validation/encounter/`。

## 规范依据与验证

遵循第 00/30 章的所有权、AI 写意图/身体执行、纯战斗结算与池复用；第 20 章的源表和 Luban 生成；第 40/50 章的外置资源、场景绑定、原生成器与只读依赖；第 70 章的检查点和回滚语义。

第 10 章仍要求命名空间逐层对应目录，本轮按用户要求采用较短模块名，未自行改写规范。规范中的 StatSheet、HeroRecord、HeroStatBuilder、SaveService 及四波原型等旧描述也应按本次实际实现阅读；本文登记差异，不代替规范修订。

| 验证 | 结果 |
| --- | --- |
| `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，`AI_MODE=1` | Luban 成功；配置源表与代码/二进制同步 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet -clp:ErrorsOnly` | 0 错误，5 个既有框架过时 API 警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 86/86，含空中旧请求作废和来源快照冻结 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 33/33，含官方数值、经验门槛、非法倍率/经验覆盖及重复基础配置拒绝 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18，含五波编排、配方倍率隔离和出口完成阶段 |
| `--smoketest=level` | 实际五波清场、大猩猩入场血条、远处 W 拒绝、接近 W、204 经验、返回地图后重新读盘通过 |
| `--smoketest=ui`（OpenGL） | 三类怪物真实下落禁招、前摇后命中、先手打断；同一大猩猩 300→900→300 生命、5→10→5 物防、20→60→20 招式威力、7→25→7 经验的池复用复位通过 |
| `--smoketest=ai`、`--smoketest=frontend` | AI 行为、菜单、锁定角色、地图放大命中、保存提示和 GGF 声音通过 |
| `--smoketest`、`--smoketest=level-cancel` | 英雄 32 次状态切换、在途显示取消与晚到实体回收通过 |
| `validate_anim_libraries.gd`、`validate_level_scene.gd` | 四类角色动画契约与五波场景检查通过 |
| `regenerate_collection.gd` | 原生成器登记 186 项资源；生成完成后仍触发既有编辑插件退出异常 |
| `validate_game_references.py --compare-staged-geometry`、`git diff --check` | 195 条序列化路径、198 个 UID、重复节点路径、23 个地形节点与差异格式检查通过 |

引擎命令使用 `S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject -- --smoketest=<模式>`；所有存档测试均使用独立 `user://Validation/` 目录。日志为临时目录 `gamezmxy-encounter-*.log/.err`。UI 回归会故意损坏测试档案和 HUD 绑定，相关报错属于验证路径。受限网络下测试存在 NU1900；运行关闭时仍有已登记的 WebRequest 节点释放异常；编辑器有 ComponentInsoector 退出异常。没有改动框架、插件或旧项目。

配方倍率整理遵循第 20 章的可读怪物枚举、基础配置一一对应与原 Luban 生成流程，以及第 30 章的属性来源和池复用边界；集中规范未修改。本次补充回归日志为 `gamezmxy-scale-ui.log/.err` 和 `gamezmxy-scale-level-final.log/.err`，最终场景完整五波、204 经验、W 出口、返回地图重新读盘通过。

同轮修复出口场景重名：删除早期 `World/Exit` 空占位节点，将编辑器自动改名的 `Exit2` 子场景恢复为 `Exit`，保留编辑器保存的节点顺序、标识与地形。资源校验增加同一场景内重复节点路径检查，并确认能检出上版 `World/Exit` 重名；关卡场景校验增加出口类型与控制器绑定检查。普通运行与 `--editor` 模式均输出 `LEVEL SCENE PASS`；编辑器退出仍触发既有 GGF 检查器插件释放异常（退出码 -1073741819），不声称编辑器进程正常退出。日志为 `gamezmxy-scale-scene.log/.err` 和 `gamezmxy-scale-editor.log/.err`。对应第 40 章的出口语义插槽和导出 NodePath 绑定要求。
