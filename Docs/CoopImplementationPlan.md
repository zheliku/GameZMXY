# 双人扩展实施计划

状态：**A1-1～A1-8 已实现并完成验证，停在 A1 等待用户审查；A2 未开始。** 当前档案采用 `ProfileModel.HeroIds` / `GameData.Heroes` 有序阵容，个人成长、穿戴与学习接口显式传 HeroId，存档 v13；本局仍只有一个 `HeroModel`、一套输入与 HUD。`PlayerSlot` 用于席位换算，当前只装配 P1。2026-11-08 按用户授权同步规范、计划与文档引用；旧计划的 `PlayerSlot → PlayerProfile/HeroState` 形状及隔离工作树 02A 草稿仍属废弃方案。

官方依据见 [造一双人源码依据](OfficialReference.md#双人扩展的源码依据2026-11-07) 与 [造三本地双人规则](OfficialReference.md#造梦三本地双人规则2026-11-07)。项目规范以 [ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md) 为准，验证记录见 [Validation](Validation.md)。

---

# 第一部分　执行者须遵守的规则

本计划面向具体实现者。**架构与方向已在本文件中确定，实现者不得自行更改方案。**

## 1. 任务粒度与停止点

- **大任务**（A1、A2、…）是一次交付单元。完成一个大任务后**必须停下**，输出交付说明并等待用户审查，不得自动进入下一个大任务。
- **子任务**（A1-1、A1-2、…）是大任务内部的执行步骤，按编号顺序做。子任务之间不需要停下等审查，但每个子任务都要求本步骤结束时项目仍能 `dotnet build` 通过。**A1 的用户裁决例外：允许 A1-1～A1-7 保留本批接口迁移引起的编译错误，每步记录，A1-8 必须全部消除；其余子任务仍要求逐步构建通过。**
- 禁止合并大任务、禁止"顺手"实现下一个大任务的内容。

## 2. 遇到计划未覆盖的情况

出现下列任一情况时，**停下并报告**，不要自行决定：

1. 本计划给出的签名或字段与实际代码冲突。
2. 发现计划未列出的调用点，且改法会影响玩法行为（不只是机械传参）。
3. 需要新增本计划未列出的类型、文件、接口、Manager、事件或配置字段。
4. 需要修改 `Framework/`、`addons/`、`Tools/Luban/`、生成代码、生成资源或旧项目（只读边界）。
5. 需要修改源表 `Configs/GameConfig/Datas/*.xlsx` 或重新生成 bytes。
6. 需要改动场景 `.tscn` 的节点结构、位置或美术资源。
7. 验证命令失败且原因不在本批改动范围内。

**只是机械补传参数的新调用点**：直接补，并在交付说明里列出来。

## 3. 硬性禁止

- 不新增 Manager / Controller / Helper / Facade / 中间转发层。
- 不新增第二个 `Architecture`，不新增事件总线、暂停管理器、仲裁框架、交易系统。
- 不为"将来可能有第三个玩家"做任何扩展；本项目上限是 2 人。
- 不添加表示"当前玩家"的全局状态，不加默认回退到 P1 的兼容视图。
- 不写存档旧版本兼容分支。存档结构改变就递增 `GameData.CurrentVersion`，旧开发档作废重建。
- 不为通过编译而注释掉、删除或弱化已有测试断言。测试断言需要跟着接口改，但**覆盖的规则不能减少**。
- 不给生产代码添加测试专用入口、getter、事件或状态字符串。
- 不机械添加 null 检查和 try-catch。本项目的场景装配、注册顺序和模块契约已经保证的前提，不要重复校验。
- 不改动与本批无关的文件，不顺手格式化其他文件。
- 不提交 git、不暂存、不改 git 配置、不覆盖 `%APPDATA%` 下的正式玩家存档。

## 4. 代码风格（硬性）

完整规范见 [10-CSharp.md](ProjectGuidelines/10-CSharp.md)，必须遵守的要点：

- 类型 / 方法 / 属性 / 公开字段 / 常量：`PascalCase`。私有实例字段：`_camelCase`。私有静态字段：`s_camelCase`。参数与局部：`camelCase`。
- 枚举不加 `E` 前缀，成员 `PascalCase`。
- **所有手写类型、方法（含私有方法）、构造函数、重写方法、公开/受保护成员都要写中文 XML 文档注释**；方法补齐影响调用方式的 `<param>` `<returns>` `<exception>`，单位写进说明。
- 私有字段和私有常量在声明行尾写 `//` 中文说明。
- 方法内部按逻辑块写 `//` 中文注释（校验、索引建立、异步等待、状态迁移、清理、非直观循环）。简单转发不强制。
- UTF-8、4 空格缩进、CRLF、Allman 大括号、文件末尾换行、无行尾空格。
- 文件范围命名空间；`using` 放命名空间外，`System` 在前。

## 5. 每个大任务的验证（最低要求）

从仓库根执行：

```powershell
dotnet build Godot/GodotProject --no-restore -v quiet
dotnet test Tests/ProfileTests --no-restore -v quiet
dotnet test Tests/BattleTests --no-restore -v quiet
dotnet test Tests/LevelTests --no-restore -v quiet
```

基线（2026-11-07）：构建 0 错误、5 条既有框架弃用警告；ProfileTests 279/279；BattleTests 193/193；LevelTests 19/19。`BattleTests` 另有既有 CS8632/CS0649 警告，测试工程另有 NU1900 网络警告，这些保留。

**测试数量允许增加，不允许减少。** 若某个大任务后测试数变少，必须在交付说明中逐条解释。

涉及场景、资源、输入映射或 UI 绑定的批次，额外执行（命令见 [Validation](Validation.md) "常用命令"）：

```powershell
# Godot 脚本扫描与解决方案构建；<案例名> 自取，例如 coop_a1_scan_20261108
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py <案例名> --headless --editor --build-solutions --quit --no-window
# 资源与 UID 引用
python Tools/ProjectMaintenance/validate_game_references.py
```

涉及实机流程的批次，额外执行对应烟测，例如：

```powershell
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py <案例名> --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend
python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py <案例名> --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui
```

退出时既有的 `ComponentInsoector` / `ObjectDB` / 资源占用诊断属于历史问题，保留并说明，不要试图在本计划范围内修掉。

## 6. 交付说明必须包含

1. 实际改动的文件清单（含新增、删除）。
2. 关键接口变化（前后签名对照）。
3. 实际执行的验证命令与结果数字（不得声称未运行的验证通过）。
4. 计划未覆盖而实际遇到的情况及处理方式。
5. 遗留问题和下一个大任务的前置条件。

## 7. 必读文件

开工前读：

- [ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md) 及其中 [00-Architecture](ProjectGuidelines/00-Architecture.md)、[10-CSharp](ProjectGuidelines/10-CSharp.md)、[70-ProfileAndSave](ProjectGuidelines/70-ProfileAndSave.md)
- [WorkingNotes.md](WorkingNotes.md)（用户意图与方向）
- 本文件第二、三部分

---

# 第二部分　已确定的设计（不得更改）

## 1. 需求（来自官方）

| 事项     | 规则                                                                                                                                     | 官方依据                                                                                                   |
| -------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| 模式     | 本地同屏、同键盘合作；联机、邀请好友、云存档、点券不做                                                                                   | 造三 `isSingleGame()` 即本地分支                                                                         |
| 人数入口 | 标题右侧分**新开游戏**与**读取存档**两个按钮；新开游戏在原位展开"单人 / 双人 / 返回"，返回恢复主菜单；读取存档才打开存档面板 | 造三 `GameMenu.newGameClick→showSelectNum`、`backClick→showMenu`、`continueClick→SaveInter(read)` |
| 新档选格 | 选人数→依次选角→打开存档面板保存模式（提示"第一次进入游戏请选择存档"）；空格直接建档，已有格先确认覆盖；关闭回标题且不写档             | 造三 `MapMenu`(isnew)→`SaveInter(save)`、`IsCover`、关闭 `initData`                               |
| 人数随档 | 人数与两人英雄在建档时确定并随档保存，读档不能切换单/双人                                                                                | 造三 `MemoryClass` 读写 `playerNum`                                                                    |
| 存档格   | 沿用项目六格；格内显示参战英雄与时间                                                                                                     | 造三六格、标题为两名角色名                                                                                 |
| 个人数据 | 经验/等级、灵魂、背包、穿戴、主动/被动技能各自一份                                                                                       | 造三 `player1_obj/player2_obj`                                                                           |
| 共享数据 | 最远通关、东海旗幡资格                                                                                                                   | 造三根级 `curBigStage` 等                                                                                |
| 选角     | 1P、2P 依次点选；已选卡保持选中并标 1P/2P 且不可再选；双人可"重选"清空两人；返回取消                                                     | 造三 `SelectRole`；造一移除已选监听                                                                      |
| 键位     | P1 保持现有；P2 见 C4 表                                                                                                                 | 造一/造三 `KeyBoardControl`                                                                              |
| 开窗     | 快捷键或本人 HUD 按钮打开**本人**背包；技能窗可切换参战者页签；同一时刻只开一个窗口；死者不能开背包                                | 造三 `RoleInfo.showBackPack/studySkill`、`BuySkill`                                                    |
| 暂停     | 窗口和无双开场演出都暂停双方                                                                                                             | 造三 `stopGame`；造一 `turnToGXP→stopGame(1000)`                                                      |
| HUD      | P1 左上；P2 右上水平镜像，文字和按钮翻回可读；各自无双槽；Boss 条居中共用                                                                | 造一/造三 `GameInfo`、`RoleInfo.setPos`                                                                |
| 经验与球 | 怪物经验全给最后一次有效命中它的英雄；该怪掉落的球都飞向并只结算给该英雄；从未被英雄命中的不发经验                                       | 造一/造三 `BaseMonster.curAttackTarget`、`dropAura`、`addWarriors`                                   |
| 地面物品 | 存活且背包有空位的首个接触者领取；满包者不消耗                                                                                           | `FallEquipObj.colwho`                                                                                    |
| 死亡     | 死者留墓碑并退出本关，无复活；另一人继续；全部死亡才失败                                                                                 | 造一 `MainGame.gameOver`、造三 `checkGameOver`                                                         |
| 结算     | 共享一份；剩余生命取全部参战者平均（死者计 0）                                                                                           | 造三 `GameWin` 无个人区、`txt_state` 取平均                                                            |
| 赠送     | 背包中物品赠给另一人，单人禁用；穿戴物需先卸下；项目额外要求接收方有空位                                                                 | 造一/造三 `PackThings.gyClick`                                                                           |
| 相机     | 单相机按存活者取景，屏幕墙限制每人不出屏                                                                                                 | `ViewControllor`、造三 `BaseHero` 20..920                                                              |
| 唐僧甘露 | 本体自疗；队友距施法点横纵 ≤200；分身横 ≤200、纵 ≤160；只治疗存活者                                                                   | 造一 `Role2.__cureRole`、`Role2Shadow.cureRole`                                                        |
| 龙王分档 | 取参战者最低等级（**不采用**造三的平均等级）                                                                                       | 造一 `Monster26`                                                                                         |

## 2. 数据形状（核心决定）

| 事项                | 采用                                                                                                                                                                            | 理由                                                                                     |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| 席位表达            | **`GameData.Heroes` 与 `ProfileModel.HeroIds` 的顺序就是席位**：下标 0 = P1，下标 1 = P2                                                                              | 阵容建档后固定、英雄不重复，席位与 HeroId 一一对应。不需要第二套主键，也不需要单独存人数 |
| 运行时个人数据主键  | **HeroId**。`ProfileModel`、`SkillModel`、`HeroModel` 全部以 HeroId 取个人数据                                                                                      | 现有 `GetProgress/GetSlots/GetLearnedSkills/GetPassiveLevels` 已是这个形状，直接复用   |
| `PlayerSlot` 职责 | 只表示座位：键位组、HUD 左右、模态请求来源、实体显示参数。**不作为任何数据或存档字段的键**                                                                                | 席位是输入与表现事实                                                                     |
| 人数                | 由 `HeroIds.Count` 派生。**不存 PlayerCount，不存 IsCoop 布尔**                                                                                                         | 不维护可能矛盾的第二份事实                                                               |
| 本局数值            | `HeroModel` 持有 `heroId → HeroState` 普通子数据，与现有 `HeroModel.StatusEffects` 同一模式。**不拆第二个顶层 Model**                                              | 符合 [00 架构](ProjectGuidelines/00-Architecture.md) 的子数据规则                         |
| System 粒度         | 保留 `ProfileSystem`/`EquipmentSystem`/`SkillSystem`/`HeroSystem`/`HeroStatusSystem`/`LevelSystem` 原实例，个人业务方法加 `heroId` 参数                           | 不新增系统                                                                               |
| 迁移顺序            | 阶段 A～C 只把接口参数化，参战人数仍是 1；阶段 D 才生成第二人                                                                                                                   | 每批可编译、可跑单人回归，不需要隔离工作树或兼容层                                       |
| 暂停仲裁            | 沿用现状：`AbstractHeroEntity.TryActivateMusou` 在 `GF.Base.IsGamePaused` 时拒绝；`MusouActivationNode.ShowActivation` 持有暂停；窗口由 `ProcedureLevel.HasWindow` 互斥 | 同帧后到者自然被拒绝且不扣无双，无需新建仲裁                                             |
| 旧档                | 结构变更即递增版本，旧开发档作废重建；不写运行时兼容，不排期迁移脚本                                                                                                            | 规范"仅接受当前格式"                                                                     |

## 3. 现状关键事实（实现时据此改，不要重新调研）

**单人假设的集中点：**

| 位置                                                                                                                             | 现状                                             | 谁负责改  |
| -------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------ | --------- |
| `ProfileModel.HeroIds` / `GetProgress(heroId)` / `GetSlots(heroId)`                                                        | A1 已移除单一出战身份与便捷视图，按有序阵容查询  | A1 已完成 |
| `GameData.Heroes`                                                                                                              | A1 已移除单一身份字段，按席位顺序保存阵容（v13） | A1 已完成 |
| `ProfileModel.Soul` / `EquipmentDatas` / `GameData.Soul`                                                                   | 全档共享钱包与背包                               | A2        |
| `HeroModel` 全部字段                                                                                                           | 单个英雄的本局数值                               | B1        |
| `HeroSystem.OnDeinit`                                                                                                          | A1 已按实际订阅编号退订；B1 再逐人保存委托       | A1 / B1   |
| `SkillSystem._skillDefinitions`                                                                                                | 单份技能登记表                                   | B2        |
| `AbstractHeroEntity.OnShow` 校验 `Attributes.HeroId != HeroId`                                                               | 依赖全局单一身份                                 | B1        |
| `EnemyDiedEventArgs` 无致死者字段；`ProfileSystem.AddExperience` 给出战英雄                                                  | 经验无归属                                       | C2        |
| `LootOrbNode.Tick(delta, target)` 单目标；`LevelViewController.Loot` 只认 `_player`                                        | 掉落无归属                                       | C3        |
| `InputSystem` 动作名是 P1 常量；`LevelExitNode`/`BattleForm`/`InventoryForm`/`SkillForm`/`TangsengEntity` 等直接读键 | 单套键位                                         | C4        |
| `LevelViewController._player` 单引用（相机、连击、出口、触发区、掉落、死亡、换装都读它）                                       | 单人                                             | D2        |
| `LevelCameraNode._target` 单 `Node2D`                                                                                        | 单人取景                                         | D4        |
| `LevelViewController._Process` 中 `HeroDeathPresentation.Completed` 直接判失败                                               | 一人死即失败                                     | D5        |
| `TangsengEntity.ApplyHeavenlyDew` 只治疗自己                                                                                   | 无队友治疗                                       | D6        |
| `DragonKingEntity.OnShow` 读 P1 的成长等级                                                                                     | 单人等级分档                                     | D7        |
| `equipment_actions_menu.tscn` 的 `Panel/Gift` 按钮永久禁用（脚本无字段）                                                     | 无赠送                                           | E1        |
| `SkillForm.SelectPage` 拒绝非出战英雄页签                                                                                      | 只能看自己                                       | E2        |

**其他必须知道的事实：**

- `Tests/ProfileTests` 与 `Tests/BattleTests` 通过 `<Compile Include>` **直接链接生产源文件**（见两个 `.csproj`），所以能用 `internal` 成员，也因此**新增源文件必须同步加编译项**。`Tests/LevelTests` 不引用这些类型。
- 测试夹具：`Tests/ProfileTests/TestGameScope.cs`（含 `RestartHero()`、`EnterMapContext()`）、`Tests/BattleTests/HeroTestScope.cs`、`Tests/BattleTests/BattleArchitecture.cs`、`SkillReuseTests.cs` 内的 `SkillScope`。改构造签名要同步这四处。
- `MainPack/Scripts/Debug/SmokeInspection.cs:259` 用**反射**读 `AbstractHeroEntity` 的私有属性名 `"Attributes"`。改这个属性名会在运行时而非编译期出错。
- `MainPack/Scripts/Debug/` 下 `UiSmokeScenario.*`、`FrontendSmokeScenario`、`LevelSmokeScenario`、`EnemyAiSmokeScenario`、`TestArenaController`、`ProfileTestData` 有大量调用点，参数化接口时必须一起改。
- `GF.Base.PauseGame()/ResumeGame()` 只是全局速度开关，无引用计数。现有五处窗口/演出各自用 `_ownsPause`/`_resumeGame` 标志对称持有，**保持这个模式**。
- 存档读写链：`SaveSystem → GameArchiveUtility → GF.Archive`；DTO 转换集中在 `SaveDataUtility`；恢复在 `ProfileSystem.RestoreCheckpoint`。
- 英雄实体显示当前**不传 userData**（`ProcedureLevel.cs:161`），由 `OnShow` 读模型校验。敌人显示传 `EnemySpawn` record struct（`AbstractEnemyEntity.cs:836`），这是显示参数的既有先例。
- 所有敌人死亡路径（普通、牛魔形态转换、龙王延迟、牛心延迟、鹏魔蛋态）最终都走 `IEnemyBody.BeginDeath()` → `EnemyDiedEventArgs`，所以在那里加致死者字段可以覆盖全部。
- `AbstractEnemyEntity.OnHurt(data, result, knockback, attackerEntityId)` 有 5 个重写：`AbstractMonsterEntity`、`HuaguoshanMonkeyEntity`、`JiaoDemonKingEntity`、`PengDemonKingEntity`，加上基类。
- `project.godot` 的 `[input]` 段里动作按**物理键**绑定（`"keycode":0` + `"physical_keycode"`）；`device` 字段现有 `-1`/`0`/`16` 三种值不一致，新增 P2 动作统一用 `"device":-1`。
- `ui_left/right/up/down` 里同时绑了方向键和 WASD，所以 P2 的方向键会同时驱动 UI 焦点导航。
- 单元测试通过 `IInputSource`（`TestGameScope.TestInputSource`、`HeroBodyTests.FrameInput`、`SkillReuseTests`）注入按键，不读 `project.godot`。
- UI 的 `XxxForm.cs` 是生成文件，生成器是 `addons/ComponentInsoector/ScriptGenerateInspector.cs`，项目侧入口 `python Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace <原命名空间>`。**现有 Ge 文件的字段名与 `m_` 前缀规则不完全一致，重新生成可能改字段名**，所以改 UI 绑定前先确认是否必须重新生成。
- `inventory_action_gift_disabled.png` 已存在，**可用的赠送按钮图尚未导出**（`export_official_inventory_actions.py` 只取了 disabled 态）。E1 需要先导出再接线。
- `UiSmokeScenario.Inventory.cs:156-158` 断言 Gift 按钮必须 `Disabled`。E1 要改这条断言。

---

# 第三部分　执行顺序总表

| 编号 | 大任务                           | 依赖   | 玩法行为变化       | 状态                     |
| ---- | -------------------------------- | ------ | ------------------ | ------------------------ |
| A1   | 阵容取代单一出战英雄（存档 v13） | —     | 无                 | 已实现并验证，待用户审查 |
| A2   | 灵魂与背包归个人（存档 v14）     | A1     | 无                 | 未开始                   |
| A3   | 档案规则证据与存档格摘要         | A2     | 存档格文本         | 未开始                   |
| B1   | `HeroModel` 按英雄承载本局状态 | A1     | 无                 | 未开始                   |
| B2   | 技能登记与施放按英雄             | B1     | 无                 | 未开始                   |
| B3   | 战斗规则证据与阶段 A～B 回归     | B2     | 只加测试           | 未开始                   |
| C1   | 英雄实体携带席位                 | B1     | 无                 | 未开始                   |
| C2   | 经验归属最后命中者               | C1     | 环境击杀不再发经验 | 未开始                   |
| C3   | 球与地面物品归属                 | C2     | 无                 | 未开始                   |
| C4   | 第二套键位与按席位开窗           | C1     | 新增 P2 映射       | 未开始                   |
| C5   | 阶段 C 单人回归                  | C4     | 只验证             | 未开始                   |
| D1   | 标题人数入口、依次选角、选格建档 | A1、C4 | 可建双人档         | 未开始                   |
| D2   | 关卡装配全部参战者               | D1、B2 | 出现两个人物       | 未开始                   |
| D3   | 双方 HUD                         | D2     | 界面               | 未开始                   |
| D4   | 相机与屏幕墙                     | D2     | 取景               | 未开始                   |
| D5   | 死亡、出口与结算                 | D2～D4 | 结局规则           | 未开始                   |
| D6   | 唐僧队友治疗                     | D2     | 技能规则           | 未开始                   |
| D7   | 龙王等级取最低                   | D2     | Boss 分档          | 未开始                   |
| D8   | 花果山双人闭环验收               | D1～D7 | 只验证             | 未开始                   |
| E1   | 装备赠送                         | A2、D3 | 新功能             | 未开始                   |
| E2   | 技能窗口双人页签                 | D3     | 界面               | 未开始                   |
| E3   | 其余关卡分组核验与文档收口       | D8、E2 | 收尾               | 未开始                   |

**D 与 E 阶段的子任务拆解在其依赖批次审查通过后补写**，因为那些签名取决于 A～C 的实际落地结果。现在写出的 D/E 范围是方向约束，执行前会细化。

---

# 第四部分　阶段 A：档案个人数据所有权

目标：把"单一出战英雄"换成"有序阵容"，并把灵魂和背包改成按人持有。**阶段 A 结束时参战人数仍是 1，单人玩法行为必须完全不变。**

## 大任务 A1　阵容取代单一出战英雄（存档 v13）

### A1-1　改 `ProfileModel`

文件：`Godot/GodotProject/TheGame/GameScripts/Models/Profile/ProfileModel.cs`

**构造函数改为：**

```csharp
public ProfileModel(IEnumerable<(int HeroId, int TotalExperience)> roster,
    ExperienceCurveUtility curve, int equipmentCapacity, int soul = 0)
```

- 删除单一出战身份参数。
- `roster` 的**枚举顺序即席位顺序**，按此顺序建立 `_heroes`、`_heroSlots` 与 `HeroIds`。
- 校验（构造边界，抛异常）：
  - `roster` 非空；
  - 元素数量在 1..2 之间（`PlayerSlot` 只有两个值，超过即拒绝）；
  - `HeroId` 不重复、且 `> 0`。
- `HeroIds` 原来是 `OrderBy(x => x)`，**改为保持 roster 顺序**，仍返回只读列表。

**新增成员：**

```csharp
/// <summary>按席位取参战英雄编号。</summary>
public int HeroOf(PlayerSlot slot)

/// <summary>按英雄编号取其席位。</summary>
public PlayerSlot SlotOf(int heroId)
```

- `HeroOf`：`HeroIds[(int)slot - 1]`；席位超出阵容时抛 `ArgumentOutOfRangeException`。
- `SlotOf`：按 `HeroIds.IndexOf` 换算；不在阵容中时抛 `ArgumentOutOfRangeException`。

**删除下列成员**（它们隐式指"出战英雄"）：

单一出战英雄编号成员，以及 `Progress`、`Level`、`TotalExperience`、`Experience`、`MaxExperience`、`Slots`、`Weapon`、`Armor`、`Accessory`、`Treasure`、`GetEquippedItem(EquipmentSlot)`。

**保留不变：** `GetProgress(heroId)`、`GetSlots(heroId)`、`HasEquipment(equipmentId)`、`Soul`、`HighestClearedLevelId`、`EastSeaBannerAvailable`、`Capacity`、`EquipmentDatas`、`HeroProgressionState`、`EquippedItems`。

本子任务结束时会有大量编译错误，由 A1-2 ~ A1-8 消除。

### A1-2　改存档 DTO 与转换（v13）

文件：

- `GameScripts/Models/Save/GameData.cs`
- `GameScripts/Utilities/Utility/SaveDataUtility.cs`

**`GameData`：**

- `CurrentVersion` 从 `12` 改为 `13`。
- **删除**单一出战英雄编号字段。
- `Heroes` 的 XML 注释改为说明"**顺序即玩家席位**，下标 0 为 P1"。

**`SaveDataUtility.Capture(ProfileModel, SkillModel)`：**

- 去掉已删除的单一出战身份赋值。
- 遍历 `profile.HeroIds` 时保持顺序写入 `data.Heroes`（现在就是 `foreach (int heroId in profile.HeroIds)`，`HeroIds` 改序后天然有序，确认不要再排序）。
- 其余字段不变。

**`SaveDataUtility.ValidateEnvelope(GameData)`：**

- 现有"`Heroes == null || Count == 0`"检查扩展为：数量必须在 1..2；`HeroId` 互不重复。

**`SaveDataUtility.Validate(...)`：**

- 删除旧的“英雄集合必须包含单一出战编号”条件，其余检查保留。

**`ReadItems`、`ValidateSkills`、`ValidateLevelProgress`、`ValidateEquipment`、`ValidateSlot`：** 逻辑不变。

### A1-3　改 `ProfileSystem` 与 `SkillSystem` 的个人业务签名

文件：

- `GameScripts/Systems/ProfileSystem.cs`
- `GameScripts/Systems/SkillSystem.cs`

**`ProfileSystem`：**

```csharp
// 旧：public int AddExperience(int amount)
public int AddExperience(int heroId, int amount)
```

- 内部改用 `profile.GetProgress(heroId)`，等级差从该英雄的进度前后值求。
- `RestoreCheckpoint(GameData)` 的身份检查改为：`data.Heroes` 的 **HeroId 有序序列**必须与 `profile.HeroIds` 完全相同（用 `SequenceEqual`）；数量、重复、经验非负、灵魂非负的检查保留。删除旧的单一出战编号相等检查。
- `AddSoul`、`TrySpendSoul`、`TryCollectEquipment`、`GrantUniqueEquipment`、`LatestLevelId`、`CanEnterLevel`、`RecordLevelClear`、`TryClaimEastSeaBanner` 本批不改（A2 再改钱包/背包的归属）。

**`SkillSystem`：** 下列方法加 `int heroId` 作为第一个参数，内部使用该参数选择英雄：

```csharp
public SkillLearnResult Upgrade(int heroId, PassiveId id)
public SkillLearnResult Learn(int heroId, SkillId skillId)
public bool IsLearned(int heroId, SkillId skillId)
public bool IsLearnable(int heroId, SkillId id)
public bool IsAvailable(int heroId, SkillId id)
```

- `SelectSkills(int heroId, params SkillDefinition[])`：签名不变，但把旧的单一出战身份比较改为 `!profile.HeroIds.Contains(heroId)`。
- `CanAfford`、`TryActivate`、`TrySpendActivatedMana`、`TryActivateInput`、`CancelSkill`、`ClearSkillDefinitions`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable`、`BodyStateType`：**本批不改**（它们读 `HeroModel`，归 B1/B2）。

### A1-4　改 `EquipmentSystem`

文件：`GameScripts/Systems/EquipmentSystem.cs`

```csharp
// 旧：public bool TryEquip(Guid instanceId)
public bool TryEquip(int heroId, Guid instanceId)

// 旧：public bool TryUnequip(EquipmentSlot slot)
public bool TryUnequip(int heroId, EquipmentSlot slot)
```

- 归属校验改为将 `(int)definition.Owner` 与显式传入的 `heroId` 比较。
- 读写穿戴改用 `profile.GetSlots(heroId)`；原来用 `profile.GetEquippedItem(slot)` 的地方改为从 `GetSlots(heroId).Value` 按部位取（可在本文件内加一个 `private static EquipmentData SlotOf(EquippedItems items, EquipmentSlot slot)` 的纯读取局部方法，**不要**在 `ProfileModel` 上重新加公开便捷成员）。
- `Roll`、`TrySell`、`SellByQuality`、`TryExchange` 本批不改。

### A1-5　改 Command / Query

文件：

- `GameScripts/Commands/EquipmentCommands.cs`
- `GameScripts/Commands/LearnSkillCommand.cs`

```csharp
public sealed class EquipEquipmentCommand(int heroId, Guid instanceId) : ...
public sealed class UnequipEquipmentCommand(int heroId, EquipmentSlot slot) : ...
public sealed class LearnSkillCommand(int heroId, SkillId skillId) : ...
public sealed class UpgradePassiveCommand(int heroId, PassiveId passiveId) : ...
```

- `SellEquipmentCommand`、`SellEquipmentByQualityCommand`、`EquipmentSaleSummaryQuery` 本批不改（A2）。
- 保持现有的"关卡存在时必须处于运行状态"等前置校验不变。

### A1-6　改 `HeroSystem`、`LevelSystem`、`DragonKingEntity`

文件：

- `GameScripts/Systems/Hero/HeroSystem.cs`
- `GameScripts/Systems/LevelSystem.cs`
- `GameScripts/Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`

**`HeroSystem.OnInit`：** 本批只做最小替换，先取 `int heroId = profile.HeroOf(PlayerSlot.P1);`，然后

- `Reset(heroId, profile.GetProgress(heroId).Value.Level, tables.TbHeroGrowthConfig.Get(heroId)..., 从 GetSlots(heroId).Value 取四个部位)`；
- `profile.GetProgress(heroId).Register(...)`、`profile.GetSlots(heroId).Register(...)`、`GetPassiveLevels(heroId).Register(...)`。

**`HeroSystem.OnDeinit`：** A1 迁移前使用 `Hero.HeroId` 退订被动。本批改为用一个私有字段保存 `OnInit` 时实际订阅的 heroId 并据此退订（B1 会改成逐人保存委托，这里先消除对 `HeroModel` 的依赖）。

**`LevelSystem.StartSession`：**

```csharp
// 旧：public void StartSession(int heroId)
public void StartSession(IReadOnlyList<int> heroIds)
```

- 校验 `heroIds` 与 `profile.HeroIds` 序列相同，否则抛 `InvalidOperationException`（消息沿用"关卡实体必须与档案阵容一致"的语义）。
- `RecordDefeat`、`CollectLoot`、`TryCollectPickup`、`TryCollectEquipment` 本批只补 `AddExperience` 的 heroId 实参（用 `profile.HeroOf(PlayerSlot.P1)`），签名改动留给 C2/C3。

**`DragonKingEntity.OnShow`：** `this.GetModel<ProfileModel>().Level` 改为 `profile.GetProgress(profile.HeroOf(PlayerSlot.P1)).Value.Level`。D7 再改成取阵容最低。

### A1-7　改流程与 UI 调用方

文件与改法：

| 文件                                                                   | 改法                                                                                                                                                                                                                                                                        |
| ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MainPack/Scripts/Procedure/ProcedureLoadGame.cs`                    | `new ProfileModel(data.Heroes.Select(x => (x.HeroId, x.TotalExperience)), curve, capacity, data.Soul)`；日志中的英雄编号与等级改为阵容首位                                                                                                                                |
| `MainPack/Scripts/Procedure/ProcedureLevel.cs`                       | `TbHeroConfig.Get(profile.HeroOf(PlayerSlot.P1))`；关卡会话由 `LevelViewController.StartSession` 向 `LevelSystem` 传 `profile.HeroIds`；`new InventoryFormData(_hero)` 暂不改（C1 再带席位）                                                                      |
| `GameScripts/Systems/SaveSystem.cs`                                  | `LoadOrCreateAsync` 新档 `GameData` 去掉单一出战身份赋值，`Heroes = [new HeroSaveData { HeroId = startHeroId }]` 保留；`ReadSlotsAsync` 的摘要英雄编号取 `data.Heroes[0].HeroId`；`WriteSnapshotAsync` 的 catalogue 名同样改为 `Heroes[0]`（A3 改成全员摘要） |
| `GameScripts/Entity/Level/LevelViewController.cs`                    | `ProfileModel.Slots.Register/UnRegister(OnEquipmentChanged)` 改为 `GetSlots(_player.HeroId)`                                                                                                                                                                            |
| `GameScripts/UI/BattleForm/BattleForm.Logic.cs`                      | `_status.SetHero(...)`、`progression.Progress`、`GetLearnedSkills(...)` 的 heroId 改为 `_formData.Hero.HeroId`                                                                                                                                                      |
| `GameScripts/UI/InventoryForm/InventoryForm.Logic.cs`                | 入口的单一出战身份校验改为 `!profile.HeroIds.Contains(data.Hero.HeroId)`；`profile.Progress/Slots` 改为按 `data.Hero.HeroId` 取；穿戴与卸下两个 Command 补 heroId；出售命令留给 A2                                                                                    |
| `GameScripts/UI/InventoryForm/Components/InventoryCharacterPanel.cs` | 现在读 `profile.Level/Experience/MaxExperience/Weapon/...`。改为由 `InventoryForm.Logic` 把该英雄的 `HeroProgressionState` 与 `EquippedItems` 作为完整值传入刷新方法，面板不再自己从 `ProfileModel` 取"当前英雄"                                                  |
| `GameScripts/UI/SkillForm/SkillForm.Logic.cs`                        | 新增私有字段保存本窗口当前显示的英雄（本批取 `profile.HeroOf(PlayerSlot.P1)`）；`Soul`、`GetLearnedSkills`、`GetPassiveLevels`、`RefreshPage`、`SelectPage`、两个 Command 都用它。**本批不开放切页签**（E2 做）                                           |
| `GameScripts/UI/SkillForm/Components/ActiveSkillsPage.cs`            | `IsLearnable`/`IsLearned` 补 heroId：由 Logic 通过 `Refresh(int heroId)` 显式传入；`SetHero(SkillHero)` 继续控制可见目录                                                                                                                                            |
| `GameScripts/UI/SkillForm/Components/PassiveSkillsPage.cs`           | `Refresh(int heroId)` 显式读取该英雄的被动等级，由 Logic 传入本周期身份                                                                                                                                                                                                   |
| `GameScripts/UI/MapForm/MapForm.Logic.cs`                            | `ResetSkillsHint` 的灵魂门槛本批不变（`Soul` 仍是全档）                                                                                                                                                                                                                 |

### A1-8　改测试与 Debug 夹具

| 文件                                                                                                                                                                  | 改法                                                                                                                                                                                                                                                                                   |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Tests/ProfileTests/TestGameScope.cs`                                                                                                                               | 构造签名去掉单一出战身份参数，改为 `TestGameScope(IEnumerable<(int,int)> heroes, int soul, int capacity, bool initializeHero = true)`；`Profile = new(heroes, TestTables.Curve, capacity, soul)`；`SelectSkills` 的条件由旧的出战身份等于 1 判断改为 `Profile.HeroIds[0] == 1` |
| `Tests/ProfileTests/*.cs`                                                                                                                                           | 所有 `new TestGameScope(...)` 调用、`AddExperience`、`TryEquip`、`TryUnequip`、`Learn`、`Upgrade`、`IsLearned`、`IsLearnable`、`IsAvailable`、Command 构造补 heroId；断言覆盖的规则不许减少                                                                          |
| `Tests/BattleTests/SkillReuseTests.cs`（`SkillScope`）                                                                                                            | `new ProfileModel([(1, 0)], curve, 20)`                                                                                                                                                                                                                                              |
| `Tests/BattleTests/HeroTestScope.cs`                                                                                                                                | 若 `HeroSystem.OnInit` 的改动影响该夹具则同步；它当前不 Init `HeroSystem`，预计只需确认                                                                                                                                                                                            |
| `MainPack/Scripts/Debug/ProfileTestData.cs`                                                                                                                         | `RestoreExperience(heroId, xp)` 等签名已带 heroId，确认内部不再依赖已删除的单一出战身份                                                                                                                                                                                              |
| `MainPack/Scripts/Debug/TestArenaController.cs`                                                                                                                     | `new ProfileModel([(heroId, 0)], ...)`                                                                                                                                                                                                                                               |
| `MainPack/Scripts/Debug/FrontendSmokeScenario.cs`、`MapProgressSmokeScenario.cs`、`LevelSmokeScenario.cs`、`UiSmokeScenario*.cs`、`EnemyAiSmokeScenario.cs` | 所有单一出战身份及 `Progress`、`Slots`、`Level`、`TotalExperience`、`Weapon/Armor/Accessory/Treasure` 的读取改为按具体 heroId；`HeroIds.SequenceEqual(new[]{1})` 之类断言保留语义                                                                                          |

### A1 完成判定

- `dotnet build` 0 错误、仍是 5 条既有框架弃用警告。
- 三套规则测试全绿，总数 ≥ 279 / 193 / 19。
- `--smoketest=frontend` 与 `--smoketest=ui` 实机通过。
- 全仓源码与文档不再包含已删除的单一出战英雄字段名；精确搜索命令与零命中结果记入 Validation，不在说明文字中重新引入旧名称。
- 单人实际行为不变：新建档、选角、进关、成长、拾取、穿脱、学技、保存、返回标题、重读。
- 交付说明列出 v12→v13 的变更，并说明旧开发档需重建。

---

## 大任务 A2　灵魂与背包归个人（存档 v14）

### A2-1　改 `ProfileModel` 的钱包与背包

文件：`GameScripts/Models/Profile/ProfileModel.cs`

- 删除 `public BindableProperty<int> Soul` 与 `public BindableList<EquipmentData> EquipmentDatas`。
- 新增按英雄的私有字典与取用方法：

```csharp
/// <summary>读取指定英雄唯一的灵魂余额。</summary>
public BindableProperty<int> GetSoul(int heroId)

/// <summary>读取指定英雄唯一的背包集合。</summary>
public BindableList<EquipmentData> GetBag(int heroId)
```

- 构造函数的 `soul` 参数保留，表示**每位英雄的初始余额**（建档只有一人，行为不变）；在 XML 注释里写清楚。
- `Capacity` 不变，表示每人同一上限。
- `HasEquipment(equipmentId)` 改为遍历全部 `HeroIds` 的背包与穿戴（唯一奖励排重覆盖双方）。

### A2-2　改 `ProfileSystem` 与 `EquipmentSystem`

```csharp
// ProfileSystem
public void AddSoul(int heroId, int amount)
public bool TrySpendSoul(int heroId, int amount)
public bool TryCollectEquipment(int heroId, EquipmentData data)
public EquipmentGrantResult GrantUniqueEquipment(int heroId, EquipmentId equipmentId, Func<float> random)

// EquipmentSystem
public bool TrySell(int heroId, Guid instanceId)
public int SellByQuality(int heroId, EquipmentQuality quality)
```

- `TryCollectEquipment` 的"实例是否已被任何人穿戴"检查仍扫描全部 `HeroIds`（保持唯一归属），容量检查改为该英雄自己的背包。
- `RestoreCheckpoint` 按每位英雄恢复各自灵魂与背包。
- `EquipmentSystem` 的穿卸只在**同一英雄**的背包与穿戴之间交换。

### A2-3　改存档 DTO 与转换（v14）

文件：`GameScripts/Models/Save/GameData.cs`、`GameScripts/Utilities/Utility/SaveDataUtility.cs`

- `CurrentVersion` → `14`。
- **删除** `GameData.Soul`。
- `HeroSaveData` 新增：

```csharp
/// <summary>该玩家的灵魂余额。</summary>
public int Soul { get; set; }

/// <summary>该玩家背包内装备的实例编号；穿戴物不出现在此列表。</summary>
public List<Guid> BagInstanceIds { get; set; } = new();
```

- `GameData.EquipmentItems` 的职责改为**全档实例池**，注释同步。
- `Capture`：按英雄写 `Soul` 与 `BagInstanceIds`（排序稳定：按 `InstanceId`），实例池收集全部背包与穿戴物。
- `ReadItems` 的返回改为 `(Dictionary<int, EquipmentData[]> Bags, Dictionary<int, EquippedItems> Slots)`；校验：
  - 每个实例编号在全档只被一处（某人背包或某人某部位）引用；
  - 池中不存在未被引用的孤立实例（拒绝，不静默丢弃）；
  - 每人背包不超 `capacity`。
- `Validate` 增加 `Soul >= 0` 的逐人检查，删除对 `data.Soul` 的检查。

### A2-4　改消费者

| 文件                                                                                       | 改法                                                                                                                                        |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `GameScripts/Systems/SkillSystem.cs`                                                     | `Learn`/`Upgrade` 扣 `TrySpendSoul(heroId, ...)`                                                                                      |
| `GameScripts/Systems/LevelSystem.cs`                                                     | `CollectLoot`、`TryCollectEquipment`、`TryCollectPickup` 加 `int heroId` 参数并透传（本批调用方仍传唯一参战者；C2/C3 接入真实归属） |
| `GameScripts/Entity/Level/LevelViewController.Loot.cs`                                   | 调用处补 heroId（`_player.HeroId`）                                                                                                       |
| `GameScripts/Entity/Enemies/Bosses/zm1_10_bull_demon_valley/BullDemonHeartDeathState.cs` | `GrantUniqueEquipment` 补 heroId（本批用 `profile.HeroOf(PlayerSlot.P1)`；C2 后改成致死者）                                             |
| `GameScripts/Queries/EquipmentSaleSummaryQuery.cs`                                       | 构造加 `int heroId`，读该英雄背包                                                                                                         |
| `GameScripts/Commands/EquipmentCommands.cs`                                              | `SellEquipmentCommand(int heroId, Guid)`、`SellEquipmentByQualityCommand(int heroId, EquipmentQuality)`                                 |
| `GameScripts/UI/InventoryForm/InventoryForm.Logic.cs`                                    | 绑定 `GetSoul/GetBag(data.Hero.HeroId)`；两个出售 Command 补 heroId                                                                       |
| `GameScripts/UI/InventoryForm/Components/InventoryBagPanel.cs`                           | `Capacity`、背包集合、`Soul` 都改为由 Logic 注入该英雄的值；`EquipmentSaleSummaryQuery` 补 heroId                                     |
| `GameScripts/UI/SkillForm/SkillForm.Logic.cs`                                            | `GetSoul(当前显示英雄)`                                                                                                                   |
| `GameScripts/UI/MapForm/MapForm.Logic.cs`                                                | 技能按钮发光门槛读 `GetSoul(profile.HeroOf(PlayerSlot.P1))`                                                                               |
| `MainPack/Scripts/Debug/ProfileTestData.cs` 及各烟测                                     | 全部 `Soul`、`EquipmentDatas` 调用按 heroId 改                                                                                          |
| `Tests/ProfileTests/*`、`Tests/BattleTests/*`                                          | 同步；`TestGameScope.CaptureSaveData` 不变                                                                                                |

### A2 完成判定

- 构建与三套测试全绿，数量不减。
- 实机前端与 UI 烟测通过（覆盖拾取、出售、批量出售、学技、唯一奖励、检查点回滚）。
- 全仓搜索 `ProfileModel` 上的 `.Soul`、`.EquipmentDatas` **零命中**。
- 单人行为、数值、存档往返不变。

---

## 大任务 A3　档案规则证据与存档格摘要

### A3-1　补档案规则测试

文件：`Tests/ProfileTests/`（在已有测试文件中补，不新建冗余文件；确需新文件时放同目录并由 `*.cs` 通配自动编译）

必须覆盖：

1. 两名英雄的经验、灵魂、背包、穿戴、主动技能、被动等级完全隔离。
2. 同一 `InstanceId` 不能同时出现在两人背包、或一人背包与另一人穿戴。
3. 阵容为空、超过 2 人、HeroId 重复、HeroId ≤ 0 全部被构造函数拒绝。
4. v14 双人往返：`Capture` → `Validate` → `RestoreCheckpoint` 后席位顺序、数值、穿戴、技能完全一致。
5. `RestoreCheckpoint` 在第二人数据非法（如实例引用不存在）时抛异常，且**第一人的数据没有被改动**（原地回滚语义）。
6. 实例池中存在孤立实例时 `ReadItems` 拒绝。

### A3-2　存档格摘要显示参战者

文件：

- `GameScripts/Models/Save/SaveSlotInfo.cs`
- `GameScripts/Systems/SaveSystem.cs`
- `GameScripts/UI/SaveForm/SaveForm.Logic.cs`
- `SaveSlotInfo` 的 `HeroName`、`Level` 两个标量改为一个按席位有序的只读摘要集合，元素含英雄名与等级（用一个随 `SaveSlotInfo` 放在同文件的 `readonly record struct`，命名按 10 章规则）。
- `SaveSystem.ReadSlotsAsync` 按 `data.Heroes` 顺序构建摘要；`WriteSnapshotAsync` 的 catalogue 名改为各英雄 `NameCn` 以单空格连接（与造三 `"孙悟空 唐僧"` 一致）。
- `SaveForm.Logic.RefreshDirectory` 的文本改为逐位拼接（示例：`"孙悟空 Lv.10　唐僧 Lv.8"`），单人只显示一项。
- **不改 `save_form.tscn` 的节点结构、位置与美术**；只改 Label 文本内容。若现有 Label 宽度放不下两人，停下报告，不要自行改布局。

### A3 完成判定

- 构建与三套测试全绿，ProfileTests 数量明显增加。
- 前端烟测通过（含目录读取、建档、删除确认）。
- 存档页在单人档下显示与之前一致。

---

# 第五部分　阶段 B：本局状态多人化

目标：`HeroModel` 从"一个英雄的数值"变成"按英雄索引的数值集合"。**阶段 B 结束时仍只生成 1 个英雄实体，单人行为与数值必须完全不变。**

## 大任务 B1　`HeroModel` 按英雄承载本局状态

### B1-1　新建 `HeroState`

新文件：`GameScripts/Models/HeroState.cs`，命名空间沿用 `GameLogic.Battle`。

把现有 `HeroModel` 的**全部状态成员**整体搬入 `HeroState`（普通类，不实现 `IModel`）：

- `HeroId`、`Level`、`Hp`、`Mp`、`MaxHp`、`MaxMp`、`Power`、`Def`、`Mdef`、`Crit`、`Miss`、`Lucky`、`Toughness`、`Htarget`、`CritReduce`、`Ar`、`Sp`、`Vampirism`、`HpRegen`、`MpRegen`
- `Ws`、`MaxWs`、`WsActive`、`IsWsFull`、`WsDrainProgress`、`WsDrainPerSecond`、`WsDecayProgress`、`WsDecayPerSecond`
- `IsDepleted`、`ControlsEnabled`、`ActiveSkillId`
- `HurtHitCount`、`HurtChainSeconds`、`HurtInvulnerabilitySeconds`、`RecoveryProgress`
- `SkillInvulnerableFrom`、`SkillInvulnerableUntil`
- `BaseAttributes`、`Sources`
- `_statusEffects` 字典与 `StatusEffects`、`GetStatusEffect`、`SetStatusEffect`、`RemoveStatusEffect`、`ClearStatusEffects`
- `CaptureCombatAttributes(int? actorId = null)`
- 嵌套类型 `StatusEffectKind`、`StatusEffectState`

访问级别保持原样（`public` 仍 `public`，`internal` 仍 `internal`）。`HeroId` 改为构造参数并只读。

同步加编译项：

- `Tests/ProfileTests/ProfileTests.csproj`（照 `HeroModel.cs` 那一行的写法新增一行）
- `Tests/BattleTests/BattleTests.csproj` 已用 `Models/**/*.cs` 通配，确认无需改动

### B1-2　`HeroModel` 改成按英雄的容器

文件：`GameScripts/Models/HeroModel.cs`

只保留：

```csharp
private readonly Dictionary<int, HeroState> _heroes = new(); // 本局参战英雄的数值与状态。

/// <summary>本局全部参战英雄的只读视图。</summary>
public IReadOnlyDictionary<int, HeroState> Heroes => _heroes;

/// <summary>读取指定参战英雄的本局状态。</summary>
public HeroState Get(int heroId)

/// <summary>为参战英雄建立本局状态；同一英雄不得重复建立。</summary>
internal HeroState Create(int heroId)

/// <summary>移除指定英雄的本局状态。</summary>
internal bool Remove(int heroId)

/// <summary>清空本局全部英雄状态。</summary>
internal void Clear()
```

`Get` 对未建立的英雄抛 `KeyNotFoundException`（字典默认行为即可，不要额外包装）。

**不要**在 `HeroModel` 上保留任何转发到"某个默认英雄"的成员。

### B1-3　`HeroSystem` / `HeroStatusSystem` 参数化

文件：`GameScripts/Systems/Hero/HeroSystem.cs`、`HeroStatusSystem.cs`

下列成员加 `int heroId` 作为第一个参数：

`HeroSystem`：`MoveSpeedMultiplier`、`JumpSpeedMultiplier`、`GravityMultiplier`、`AttackPowerMultiplier`（四个属性改为同名方法 `float MoveSpeedMultiplier(int heroId)` 等）、`Damage`、`Heal`、`RecordHurt`、`AdvanceHurtProtection`、`AdvanceRecovery`、`TrySpendMp`、`RestoreMp`、`SetSource`、`RemoveSource`、`RecordHit`、`AddWsFromPickup`、`TryActivateWs`、`SetControlsEnabled`、`AdvanceWs`、`ClearWs`，以及 `internal` 的 `Reset`、`SetGrowth`、`ResetWs`、`AddWs`。

`HeroStatusSystem`：`IsFrozen`、`IsPoisoned` 改为 `bool IsFrozen(int heroId)` / `bool IsPoisoned(int heroId)`；`ApplyFreeze`、`AdvanceFreeze`、`ApplyPoison`、`AdvancePoison` 加 heroId；`ClearAll()` 保留并清空全部英雄。

内部所有私有辅助（`ClearHurtProtection`、`OnProgressChanged`、`OnEquipmentChanged`、`OnPassiveLevelsChanged`、`ReplacePassiveSource`、`ReplaceEquipmentSources`、`Recalculate`、`ApplyFinalAttributes`、`SetWsState`）改为接收或携带 heroId；删除 `private HeroModel Hero => ...` 这种隐含单人的捷径。

**`OnInit`：** 遍历 `profile.HeroIds`，逐人 `hero.Create(heroId)`、`Reset(...)`、`ResetWs(...)`，并**逐人保存委托**后订阅：

```csharp
private readonly Dictionary<int, (Action<HeroProgressionState> Progress, Action<EquippedItems> Slots,
    Action<IReadOnlyDictionary<PassiveId, int>> Passives)> _subscriptions = new(); // 每位参战英雄实际注册的回调，用于对称退订。
```

**`OnDeinit`：** 用 `_subscriptions` 对称退订全部英雄，然后 `ClearWs`、`HeroStatusSystem.ClearAll()`、清除受击保护、`hero.Clear()`。A1 已按实际订阅编号修正单人退订；B1 必须在此基础上覆盖全部英雄，不能继续只保存一份编号。

### B1-4　改英雄实体与身体/技能状态

| 文件                                                                                                                                                    | 改法                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| ------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Entity/Heroes/AbstractHeroEntity.cs`                                                                                                                 | `private HeroState Attributes => this.GetModel<HeroModel>().Get(HeroId);`（**属性名保持 `Attributes`**，`SmokeInspection.cs:259` 用反射读它）。`Invulnerable`、`Hp`、`MaxHp`、`Level`、三个倍率、`_PhysicsProcess` 全链、`ApplyDamage`、`RestoreHealth`、`TryActivateMusou`、`EndMusou`、`CaptureCombatAttributes`、`OnHitLanded`、`OnHurt`、`RefreshInvulnerabilityGlow`、`PrepareAttack` 全部改为传本实体 `HeroId` |
| `AbstractHeroEntity.OnShow`                                                                                                                           | 校验改为"本局已为本英雄建立状态"（`this.GetModel<HeroModel>().Heroes.ContainsKey(HeroId)`），不再比较全局 `HeroId`                                                                                                                                                                                                                                                                                                                                         |
| `AbstractHeroEntity.Skills.cs` / `.Capture.cs`                                                                                                      | 同上，按 `HeroId` 取                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `Entity/Heroes/Wukong/WukongEntity.cs`                                                                                                                | `CanReceiveStatusEffect`、`SuperArmor` 读本人 `WsActive`                                                                                                                                                                                                                                                                                                                                                                                                 |
| `Entity/Heroes/Tangseng/TangsengEntity.cs` / `.AnimationEvents.cs`                                                                                  | 全部 `HeroModel` 读取改为本人状态                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `Entity/Heroes/Body/Core/AbstractHeroBodyState.cs`、`AbstractHeroSkillState.cs`                                                                     | 通过 `IHeroBody` 取状态。`IHeroBody` 已继承 `ICanGetModel`，但身体状态需要知道是哪个英雄：**在 `IHeroBody` 上新增 `int HeroId { get; }`**，由 `AbstractHeroEntity.HeroId` 满足。同步改测试假实现 `HeroBodyTests.cs:819`、`SkillReuseTests.cs:427`、`SkillStateTests.cs:319`                                                                                                                                                            |
| `Wukong/Skills/WukongFlameThrustState.cs`、`WukongFireDemonState.cs`、`WukongBlazingFlashState.cs`、`Tangseng/Skills/TangsengIceDragonState.cs` | 按 `body.HeroId` 取                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `Entity/Effects/SkillEffectNode.cs`                                                                                                                   | `RecordHit` 的归属改为 `Source.OwnerEntityId` 解析出的英雄：只在 `_attacker.Side == CombatSide.Hero` 且该实体是 `AbstractHeroEntity` 时调用 `RecordHit(hero.HeroId, result)`。通过 `GF.Entity.GetEntity(Source.OwnerEntityId)` 取                                                                                                                                                                                                                  |
| `Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`                                                                               | `ApplyFreeze` 作用于**实际命中的** `target` 英雄（`OnHitLanded` 已经把 `target` 匹配成 `AbstractHeroEntity`，取其 `HeroId`）                                                                                                                                                                                                                                                                                                                 |
| `Entity/Enemies/Bosses/zm1_05_qixing_cave/LionCamelPoisonCloudNode.cs`                                                                                | `ApplyPoison` 同上                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `Entity/Level/LevelViewController.cs`、`.Loot.cs`、`.Entrance.cs`                                                                                 | 按 `_player.HeroId` 取                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `Systems/LevelSystem.cs`                                                                                                                              | `IsDepleted`、`Heal`、`RestoreMp`、`AddWsFromPickup` 按传入的 heroId                                                                                                                                                                                                                                                                                                                                                                                   |
| `Systems/SkillSystem.cs`                                                                                                                              | 读 `HeroModel` 的地方改为按 heroId（B2 完成登记表拆分，本批先让它能编译：`TryActivate`、`CanAfford`、`TrySpendActivatedMana`、`CancelSkill`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable`、`BodyStateType` 都加 heroId）                                                                                                                                                                                                             |
| `UI/BattleForm/BattleForm.Logic.cs`                                                                                                                   | 绑定 `Get(_formData.Hero.HeroId)` 的属性                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `UI/InventoryForm/InventoryForm.Logic.cs`、`Components/InventoryCharacterPanel.cs`                                                                  | 同上                                                                                                                                                                                                                                                                                                                                                                                                                                                           |

### B1-5　改测试与 Debug 夹具

- `Tests/BattleTests/HeroTestScope.cs`：`Hero` 改为 `HeroModel` 容器 + 一个 `HeroState`；暴露便于断言的 `State` 属性；`System.Reset(1, ...)` 带 heroId。
- `Tests/BattleTests/SkillReuseTests.cs` 的 `SkillScope`、`Tests/ProfileTests/TestGameScope.cs`（含 `RestartHero`）同步。
- `Tests/BattleTests/HeroModelTests.cs`、`MusouTests.cs`、`Tests/ProfileTests/{FreezeTests,PoisonTests,HurtProtectionTests,RecoveryTests,LevelRewardTests,MusouTests,BindablePropertyTests,ProfileTests,PassiveSkillTests,SkillSystemTests,SkillStateTests,SkillLearningTests,EquipmentTests,BossEquipmentTests,ModelTransactionTests}.cs` 按新签名改。
- `MainPack/Scripts/Debug/` 下所有 `GetModel<HeroModel>()` 与 `GetSystem<HeroSystem>()` / `GetSystem<HeroStatusSystem>()` 调用（数量很大，见本文件第二部分 3）按 heroId 改。

### B1 完成判定

- 构建与三套测试全绿，数量不减。
- `--smoketest=ui`、`--smoketest=frontend`、关卡实机通过。
- 单人数值、无双、状态效果、恢复节拍、受击保护与之前一致。
- 全仓搜索：`HeroModel` 上不再有任何标量属性访问（只有 `Get`/`Heroes`）。
- 交付说明说明 `OnDeinit` 退订缺陷的修正。

---

## 大任务 B2　技能登记与施放按英雄

文件：`GameScripts/Systems/SkillSystem.cs` 及其调用方。

- `_skillDefinitions` 改为 `Dictionary<int, Dictionary<SkillId, SkillDefinition>>`（按 heroId）。
- `SelectSkills(int heroId, params SkillDefinition[])`：只替换该英雄的登记；校验保持（具体状态类型、配置归属、编号唯一、输入动作唯一）。
- `ClearSkillDefinitions(int heroId)` 与 `ClearSkillDefinitions()`（后者清全部，供 `OnDeinit` 用）。
- `BodyStateType`、`TryActivate`、`TrySpendActivatedMana`、`TryActivateInput`、`CancelSkill`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable`、`CanAfford` 全部按 heroId 操作。
- `AbstractHeroEntity.OnHide` 只清本人登记。
- 同步 `SkillStateTests`、`SkillReuseTests`、`SkillSystemTests`、`SkillLearningTests` 与 `TestGameScope`。

**完成判定：** 构建与三套测试全绿；两人同时登记不互相覆盖（由 B3 的测试证明）；单人行为不变。

---

## 大任务 B3　战斗规则证据与阶段 A～B 回归

### B3-1　补战斗规则测试

在 `Tests/BattleTests` 与 `Tests/ProfileTests` 补：

1. 两人各自的属性来源（装备、被动、Buff）互不影响。
2. 两人的冻结、中毒、受击保护、恢复节拍互不影响。
3. 两人的无双蓄积、开启、衰减、消耗互不影响。
4. 两人同时施放技能，各自 `ActiveSkillId` 不被覆盖，扣各自蓝。
5. 本局重建（`RestartHero` 语义）后，旧 `HeroState` 不再被任何订阅更新（证明 `HeroSystem.OnDeinit` 对称退订）。
6. `SelectSkills` 对不在阵容中的英雄抛异常。

### B3-2　阶段 A～B 完整回归

执行第一部分第 5 节的全部命令，加 `--smoketest=frontend`、`--smoketest=ui`，以及 `validate_game_references.py` 和 Godot 扫描。确认单人行为、数值、存档往返与阶段 A 之前完全一致。

---

# 第六部分　阶段 C：席位进入运行时

目标：席位、致死归属、掉落归属、第二套键位全部到位。**阶段 C 结束时仍只生成 1 个英雄实体。**

## 大任务 C1　英雄实体携带席位

- 在 `Entity/Heroes/` 下新增显示参数（仿 `EnemySpawn` 的做法，`readonly record struct`，随 `AbstractHeroEntity` 放同一文件或同目录）：

```csharp
/// <summary>一次英雄显示的席位与出生位置。</summary>
/// <param name="Slot">本次控制该英雄的玩家席位。</param>
/// <param name="Position">出生世界坐标。</param>
public readonly record struct HeroSpawn(PlayerSlot Slot, Vector2 Position);
```

- `AbstractHeroEntity.OnShow` 消费它：校验 `profile.HeroOf(spawn.Slot) == HeroId`，设置 `GlobalPosition`，并把席位存进一个私有字段；暴露 `public PlayerSlot Slot { get; private set; }`。池复用时在 `OnShow` 复位。
- `IHeroBody` 增加 `PlayerSlot Slot { get; }`（C4 的按席位读键要用）；同步三处测试假实现。
- `ProcedureLevel`：`ShowEntityAsync<AbstractHeroEntity>(heroConfig.EntityId, new HeroSpawn(PlayerSlot.P1, _levelViewController.PlayerSpawnPosition))`，删掉之后单独赋 `GlobalPosition` 的那行。
- `TestArenaController.SpawnHeroAsync`、`UiSmokeScenario.ShowHeroAsync`、`SmokeTestDriver` 同步。
- `BattleFormData`、`InventoryFormData` 增加席位字段（供 C4/D3 使用）。

## 大任务 C2　经验归属最后命中者

- `AbstractEnemyEntity` 增加私有字段记录**最近一次有效命中**（非 Miss、非零伤害）的 `AttackData.Source.OwnerEntityId`；在 `OnHurt` 里写入。注意：
  - `OnHurt` 有 5 个重写，`PengDemonKingEntity` 在蛋态不调 `base.OnHurt`，`JiaoDemonKingEntity` 同向命中走另一分支——确认这些分支都会记录到归属，否则停下报告。
  - `ReceivePeriodicDamage` 不带来源，不改写归属。
  - 显示/隐藏/池复用时复位该字段。
- `EnemyDiedEventArgs` 增加 `KillerEntityId`（`int`，0 表示无英雄归属），在 `Create` 与 `Clear` 中处理；`IEnemyBody.BeginDeath` 传入。
- `LevelSpawnerNode.EnemyDefeated` 透传（事件签名已带 `EnemyDiedEventArgs`，无需改签名）。
- `LevelViewController.OnEnemyDefeated`：把 `KillerEntityId` 解析成参战英雄（`GF.Entity.GetEntity(...)` as `AbstractHeroEntity`），解析不到就不发经验、不播升级动画，但**仍计入击败数**。
- `LevelSystem.RecordDefeat(int heroId, int experienceReward)`；`heroId` 为 0 时只计数不发经验（或者改成 `int?`，二者选一并保持一致）。
- 牛心唯一奖励 `BullDemonHeartDeathState` 改发给致死者。
- 唐僧分身与离身效果的 `OwnerEntityId` 已是本体 id，确认无需额外处理。
- 陷阱 `BatCaveSpikeNode` 的 `OwnerEntityId` 是 0，确认不发经验。

**行为变化（需在交付说明中明确）：** 从未被英雄命中的怪物（陷阱、环境击杀）不再发经验。

## 大任务 C3　球与地面物品归属

- `LootOrbNode.Show` 增加归属英雄的实体编号参数并保存；`Tick` 改为由关卡传入该英雄的位置（关卡负责解析，节点不持有实体引用）。
- `LevelViewController.Loot`：
  - 球按归属英雄位置推进；归属者死亡时回收其在途球（不发放）。
  - `TryCollectGroundItem` 接受任一**参战且存活**的英雄（不再 `ReferenceEquals(hero, _player)`），领取写入接触者背包；满包留在地面。
  - `TickLoot` 对全部存活参战者逐一尝试地面物品；同帧多人接触按席位顺序，只有第一个成功。
- `LevelSystem.CollectLoot(int heroId, LootReward)`、`TryCollectPickup(int heroId, PickupId)`、`TryCollectEquipment(int heroId, EquipmentData)` 用真实归属。
- 恢复量仍在**生成时**按该英雄上限确定（现状即如此，不要改成领取时计算）。

## 大任务 C4　第二套键位与按席位开窗

### C4-1　`project.godot` 新增 P2 动作

在 `[input]` 段按**与现有条目完全相同的格式**追加，命名为现有名加 `_p2` 后缀，`"device":-1`，按物理键绑定（`"keycode":0` + `physical_keycode`）：

| 新动作                  | P2 键        | `physical_keycode` |
| ----------------------- | ------------ | -------------------- |
| `move_left_p2`        | ←           | 4194319              |
| `move_right_p2`       | →           | 4194321              |
| `move_down_p2`        | ↓           | 4194322              |
| `interact_p2`         | ↑           | 4194320              |
| `attack_p2`           | 小键盘 1     | 4194433              |
| `jump_p2`             | 小键盘 2     | 4194434              |
| `skill_1_p2`          | 小键盘 4     | 4194436              |
| `skill_2_p2`          | 小键盘 5     | 4194437              |
| `skill_3_p2`          | 小键盘 6     | 4194438              |
| `skill_4_p2`          | 小键盘 3     | 4194435              |
| `musou_p2`            | 小键盘 0     | 4194432              |
| `inventory_toggle_p2` | 小键盘 `/` | 4194439              |
| `skill_toggle_p2`     | 小键盘 `*` | 4194440              |

**上表的 `physical_keycode` 数值必须在实际 Godot 4.7 中核对**（`Key.Kp0` 等枚举值），不要照抄不验证；核对后把实际值写进交付说明。技能槽按官方语义对应，**不要**按小键盘数字递增重排。

### C4-2　`InputSystem` 按席位解析动作名

文件：`GameScripts/Systems/Input/InputSystem.cs`

- 保留现有 13 个 P1 常量不变（大量调用点依赖它们）。
- 新增按席位解析的内部方法，把基名加 `_p2`：

```csharp
/// <summary>按席位取实际输入映射名；P1 使用基名，P2 使用带席位后缀的映射。</summary>
public static string ActionOf(string action, PlayerSlot slot)
```

- `Sample(IHeroBody hero, float dt, bool controlsEnabled)` 内部全部用 `ActionOf(常量, hero.Slot)`。
- `IsActionPressed`/`IsActionJustPressed`/`IsActionJustReleased`/`ConsumePress` 增加按席位的重载（或统一加 `PlayerSlot slot` 参数），组合、边沿、双击、消费规则**完全复用现有实现**，不要复制一份。
- `HeroActionData.ConsumedActions` 已是每实体独占，保持。

### C4-3　改直接读键的消费者

| 文件                                                                 | 改法                                                                                                                                                                                                                                                                          |
| -------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Entity/Heroes/Tangseng/TangsengEntity.cs:55`                      | `ConsumePress` 按本实体席位                                                                                                                                                                                                                                                 |
| `Entity/Heroes/Tangseng/TangsengEntity.AnimationEvents.cs:23`      | `IsActionPressed(Attack, Slot)`                                                                                                                                                                                                                                             |
| `Entity/Heroes/Tangseng/Skills/TangsengIceDragonState.cs:60`       | `body.Slot`                                                                                                                                                                                                                                                                 |
| `Entity/Heroes/Tangseng/Skills/TangsengWaterDemonBurstState.cs:32` | `body.Slot`                                                                                                                                                                                                                                                                 |
| `Systems/SkillSystem.cs:268`                                       | `SkillDefinition.InputAction` 保持基名，在 `TryActivateInput` 里用 `ActionOf(action, actor 的席位)`；`ISkillActor` 需要能提供席位——若 `ISkillActor` 尚无席位，由 `TryActivateInput(ISkillActor, HeroActionData, PlayerSlot)` 显式传入，不要给接口加不必要的成员 |
| `Entity/Level/Exit/LevelExitNode.cs:39,50-51`                      | 对每个重叠的存活落地英雄，用**该英雄席位**的 `Interact` 判断按下，并用同席位的 Attack/Jump/MoveDown 排除组合键                                                                                                                                                        |
| `UI/BattleForm/BattleForm.Logic.cs:57-58`                          | 同时识别两套背包/技能键，并把请求者席位随事件传给流程：`InventoryRequested`/`SkillsRequested` 的签名从 `Action<int>` 改为 `Action<int, PlayerSlot>`                                                                                                                   |
| `MainPack/Scripts/Procedure/ProcedureLevel.cs`                     | `OnInventoryRequested(int formSerialId, PlayerSlot slot)`、`OnSkillsRequested(...)`：按席位解析英雄并打开该英雄的窗口；沿用现有 `HasWindow` 三窗互斥；**死者不能开背包**（请求者不存活就直接返回）                                                                |
| `UI/InventoryForm/InventoryForm.Logic.cs:195,203`                  | 关闭键识别**打开者席位**的 `InventoryToggle`（席位由 `InventoryFormData` 带入）                                                                                                                                                                                     |
| `UI/SkillForm/SkillForm.Logic.cs:146-147`                          | 同理识别打开者席位的 `SkillToggle`；`UiCancel` 保持共用                                                                                                                                                                                                                   |
| `MainPack/Scripts/Debug/` 各烟测                                   | 注入按键的地方按席位改；`UiSmokeScenario.Inventory/Skills` 里改 InputMap 的夹具同步                                                                                                                                                                                         |

### C4-4　P2 映射的确认

- `ui_left/right/up/down` 已绑方向键，P2 移动会同时驱动 UI 焦点导航。**本批不改 `ui_*`**；若实测发现焦点干扰影响玩法，停下报告。
- `Esc`（设置）两人共用，不新增 P2 版本。

## 大任务 C5　阶段 C 单人回归

执行全部构建、三套规则测试、`--smoketest=frontend`、`--smoketest=ui`、关卡实机、Godot 扫描、引用检查。确认：

- P1 行为、经验数值、掉落数值与阶段 B 之前一致；
- P2 映射存在但无人使用；
- 环境击杀不发经验这一项变化符合预期。

---

# 第七部分　阶段 D 与 E：范围约束

**子任务拆解在依赖批次审查通过后补写。** 以下是不得偏离的范围。

| 编号 | 范围                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | 验收要点                                                                                                                                                                                                                                                                                                                                                     |
| ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| D1   | **D1a 标题**：`start_form.tscn`/`StartForm` 的"开始游戏"拆为"新开游戏""读取存档"，新开游戏在原位展开"单人/双人/返回"，返回恢复主菜单；`ProcedureTitle` 读取存档打开 `SaveForm` 读模式（空格无响应）。**D1b 选角**：`CharacterForm` 依次确认 1P/2P，已选卡保持选中并显示 1P/2P 标记且不可再选，双人显示"重选"清空两人，返回回到标题；单人可改选。**D1c 选格建档**：选角确认后打开 `SaveForm` 保存模式并显示"第一次进入游戏请选择存档"，空格直接建档，已有格复用现有确认层提示覆盖，关闭回标题且不写档；`SaveSelectionVariable` 携带有序阵容，`SaveSystem` 建档写入阵容 | 按钮与"重选"的美术先从官方导出并给场景预览审查；`StartForm` 现有四个按钮间距 62px、下一个位置约 y=408，布局改动先给预览；返回与关闭不写半份档案；覆盖必须明确确认；卡片按已实装英雄配置绑定，不写角色组合分支                                                                                                                                              |
| D2   | `ProcedureLevel`、`LevelViewController.StartSession` 持有参战实体集合取代 `_player`；出生点按席位横向错开                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | 按阵容显示 1 或 2 人；取消令牌覆盖全部请求，第二人失败或迟到时回收本周期已取得对象；`OwnsActor`、连击、换装、触发区、出口、掉落、死亡全部改为按集合判断                                                                                                                                                                                                    |
| D3   | `BattleForm.Logic`、`BattleFormData`、`battle_form.tscn`，复用 `BattleStatusPanel`/`BattleMenuPanel`/`MusouActivationNode`                                                                                                                                                                                                                                                                                                                                                                                                                                                                  | 一个 BattleForm 持双方区域，P2 右上镜像、文字翻回（参照造一 `RoleInfo.setPos`）；各自无双槽与菜单按钮打开本人窗口；无双演出显示发动者立绘；Boss 条、阶段、连击共用；单人布局不变；**先审布局截图再接绑定**；Ge 绑定文件只经 `regenerate_ui_form.py` 更新，且先确认字段名不会被改坏                                                                 |
| D4   | `LevelCameraNode`（单 `Node2D` → 存活参战者集合）、`LevelViewController`、屏幕墙                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | 仍单个 Camera2D 与中央死区；`SpatialUtility.StepCameraCenter` 不改签名，传两人中点即可；屏幕墙阻止每人出屏，落后者限制前进；阶段抵达仍依实际取景；死者不参与取景                                                                                                                                                                                           |
| D5   | `LevelViewController`、`LevelSystem`、`LevelResult`、`SettlementForm`、`LevelExitNode`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | 一死一活继续，死者只结束本人控制/技能/效果/在途球；**全部死亡且墓碑演出结束后**失败结算一次（改 `_Process` 里 `HeroDeathPresentation` 的单人判定）；任一存活者按本人交互键使用出口，等有效球结算后整队离关；结算共享一份，剩余生命为参战者平均（死者计 0）；放弃/失败按进关快照回滚双方                                                            |
| D6   | `TangsengEntity` 甘露入口、`TangsengWaterCloneNode`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | 本体自疗；队友距施法点横纵 ≤200；分身横 ≤200、纵 ≤160；只治疗存活者；恢复量读施法者属性；不复制一套治疗公式                                                                                                                                                                                                                                               |
| D7   | `DragonKingEntity`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | 入场分档取参战者**最低**等级；旗幡资格仍全档一次                                                                                                                                                                                                                                                                                                       |
| D8   | 花果山闭环                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | 标题新开游戏选双人 → 依次选不同英雄 → 选格建档 → 同时移动/攻击/技能 → 各自成长/拾取/无双 → 治疗 → 一死一活 → 全灭与通关 → 保存退出后经读取存档再进入；交换 1P/2P 英雄复验；单人跑同一路径；两人同帧按无双只有一人开启且另一人不扣无双。用现有 `FrontendSmokeScenario`/`UiSmokeScenario` 与规则测试承载证据，**不以提高生命或清场夹具代替** |
| E1   | `EquipmentCommands`＋`EquipmentSystem` 增加发送者/接收者/实例请求；启用 `equipment_actions_menu.tscn` 的 `Panel/Gift`（单人保持禁用，与造三一致）。**可用态按钮图需先用 `export_official_inventory_actions.py` 扩充导出**                                                                                                                                                                                                                                                                                                                                                               | 只赠送背包物；校验双方属于阵容、源归属、接收方有空位后同步移交同一实例，不 await、不自动装备、不重抽属性；`UiSmokeScenario.Inventory.cs:156-158` 的断言同步改为"单人禁用、双人可用"；验收满包、已穿戴、过期选择、同一 `InstanceId` 只存在一处、保存重读与放弃回滚包含赠送                                                                                |
| E2   | `SkillForm` 已有的悟空/唐僧页签改为启用全部参战者，打开时默认请求者页签（地图上默认 P1），学习/升级扣所选英雄自己的灵魂                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | 复用 A1/A2 的数据绑定，不新建 Player2 窗口；`SelectPage` 的"只能看出战英雄"限制改为"只能看参战英雄"                                                                                                                                                                                                                                                        |
| E3   | 分组核验：①桃花源、水帘洞、蝙蝠窟（平台/坡地、机关、隐藏入口、屏幕边界）②七星洞、迦楼罗之穴（毒雾、延迟/范围效果、飞行目标）③浅岸、东海、牛魔谷（抓取、水牢、解救、牛魔多形态与经验归属传递；`BullDemonKingEntity.CaptureHeroAnimEvent` 已遍历全部英雄，确认其 `TryCapture` 对多人正确）④隐藏龙宫（冻结、龙王分档、旗幡与装备排重）                                                                                                                                                                                                                                                             | 最后核验 60/120Hz、第二人加载取消、退出、重复进入、窗口与实体退订、源表/场景引用；更新 [ProjectTour](ProjectTour.md) 与规范现状，清理迁移中的旧入口与重复实现                                                                                                                                                                                                 |

---

# 第八部分　本轮交付

A1（子任务 A1-1～A1-8）已实现并完成构建、规则测试、Godot 扫描、引用与前端/UI 烟测；实际命令、数字、接口对照和文件清单统一见 [Validation](Validation.md)。2026-11-08 已按用户授权清理文档中的旧字段名称，并同步第 70 章与 ProjectTour 的 v13 现状；本次文档补齐不重复执行代码验证。

当前**停在 A1 等待用户审查**。A2 及后续大任务均未开始，需审查通过后再获授权推进。
