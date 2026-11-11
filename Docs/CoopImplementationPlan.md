# 双人扩展实施计划

状态：**阶段A～C已完成并经架构审查；D0正式转存完成（0307f36），D1/D2实现与Panel职责调整已完成（ae2a1ba），D3实现及授权出口等待修复已完成（c63cd1d）。E1-1/2/4已实现，规则339/197/19、扫描/引用、完整前端与双人专项通过，最终完整单人UI亦退出0、完整PASS。D0～E1正式键盘验收及E1-3逐关实战未执行；用户明确允许各批暂留推进，并回复“允许待补，继续E1-4收尾”。待验项保持，不把自动回归判作全关实战通过。** 当前v14按阵容保存一至两名英雄，个人数据及本局状态按HeroId，席位仅用于输入/显示与请求来源；双英雄/双HUD、存活者取景/全灭、归属经验/掉落、队友治疗、赠送与技能页签均已接入。最终命令/数字、文件/接口、授权例外及待验表集中见Validation顶部总交付。旧计划的PlayerSlot→个人档案形状及隔离工作树02A草稿仍属废弃方案。

官方依据见 [造一双人源码依据](OfficialReference.md#双人扩展的源码依据2026-11-07) 与 [造三本地双人规则](OfficialReference.md#造梦三本地双人规则2026-11-07)。项目规范以 [ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md) 为准，验证记录见 [Validation](Validation.md)。

---

# 第一部分　执行者须遵守的规则

本计划面向具体实现者。**架构与方向已在本文件中确定，实现者不得自行更改方案。**

## 1. 任务粒度与停止点

- **大任务**（A1、A2、…）是一次交付单元。阶段 A～C：完成一个大任务后**必须停下**，输出交付说明并等待用户审查。**阶段 D～E 例外**：用户要求一次交付，按第七部分第 0 节连续执行，只在 D0-3 正式转存前与触发本部分第 2 节上报条件时停下。
- **子任务**（A1-1、A1-2、…）是大任务内部的执行步骤，按编号顺序做。子任务之间不需要停下等审查。**中间编译门槛：** 删除或改签名的子任务会留下迁移编译错误，这是预期的；每个子任务结束时记录当前编译错误数，**大任务的最后一个子任务必须清零**。不要为了维持中间编译而引入兼容转发、临时重载或默认参数（A1、A2 已按此执行）。
- **"临时取 P1"标记：** 凡本计划要求"本批先用 `HeroOf(PlayerSlot.P1)`"或"先用 `_player.HeroId`"的位置，都是**有意留给后续大任务替换的已知占位**，必须保持单行、直白、可被搜索；不要为它加辅助方法或抽象。第九部分列出了全部占位及其负责批次。
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

基线（2026-11-10，阶段 C 完成后，由审查者实测）：构建 0 错误；ProfileTests 323/323；BattleTests 197/197；LevelTests 19/19。既有框架弃用警告、`BattleTests` 的 CS8632/CS0649 与测试工程的 NU1900 网络警告保留。

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
| 本局数值            | `HeroModel` 持有 `heroId → HeroState` 普通子数据，各 `HeroState.StatusEffects` 仍为普通子数据。**不拆第二个顶层 Model**                                            | 符合 [00 架构](ProjectGuidelines/00-Architecture.md) 的子数据规则                         |
| System 粒度         | 保留 `ProfileSystem`/`EquipmentSystem`/`SkillSystem`/`HeroSystem`/`HeroStatusSystem`/`LevelSystem` 原实例，个人业务方法加 `heroId` 参数                           | 不新增系统                                                                               |
| 迁移顺序            | 阶段 A～C 只把接口参数化，参战人数仍是 1；阶段 D 才生成第二人                                                                                                                   | 每批可编译、可跑单人回归，不需要隔离工作树或兼容层                                       |
| 暂停仲裁            | 沿用现状：`AbstractHeroEntity.TryActivateMusou` 在 `GF.Base.IsGamePaused` 时拒绝；`MusouActivationNode.ShowActivation` 持有暂停；窗口由 `ProcedureLevel.HasWindow` 互斥 | 同帧后到者自然被拒绝且不扣无双，无需新建仲裁                                             |
| 旧档                | 结构变更即递增版本，旧开发档作废重建；不写运行时兼容，不排期迁移脚本                                                                                                            | 规范"仅接受当前格式"                                                                     |

## 3. 现状关键事实（实现时据此改，不要重新调研）

**单人假设的集中点：**

| 位置                                                                                            | 现状                                                   | 谁负责改  |
| ----------------------------------------------------------------------------------------------- | ------------------------------------------------------ | --------- |
| `ProfileModel.HeroIds` / `GetProgress(heroId)` / `GetSlots(heroId)`                       | A1 已移除单一出战身份与便捷视图，按有序阵容查询        | A1 已完成 |
| `GameData.Heroes`                                                                             | A1 已按席位顺序保存阵容，A2 当前格式为 v14             | A1 已完成 |
| `ProfileModel.GetSoul(heroId)` / `GetBag(heroId)` / `HeroSaveData`                        | 个人钱包与背包，实例池全档唯一                         | A2 已实现 |
| `HeroModel` / `HeroState`                                                                   | B1 已将个人数值搬入按 HeroId 索引的普通状态            | B1 已交付 |
| `HeroSystem.OnDeinit`                                                                         | B1 已逐人保存三个闭包并对称退订、清空状态              | B1 已交付 |
| `HeroSystem.Damage` 死亡分支与 `Reset`                                                      | B1 已改用 ClearAll(heroId)，仅清本人效果               | B1 已交付 |
| `SkillSystem._skillDefinitions`                                                               | B2 已改为每位 HeroId 独立登记表                        | B2 已交付 |
| `AbstractHeroEntity.OnHide`                                                                   | B2 已使用 ClearSkillDefinitions(HeroId)，只清本人      | B2 已交付 |
| `AbstractHeroEntity.OnShow`                                                                   | B1 已先检查容器中存在本人的状态，再读取 Attributes     | B1 已交付 |
| `AbstractEnemyEntity.OnHurt` 首先调用 RecordHeroAttacker，鹏魔蛋态单独调用；显示复位归属      | 致死一击记录不受 Dead 早退或 AI 目标清除影响           | C2 已实现 |
| `EnemyDiedEventArgs.KillerEntityId` 与 `LevelSystem.RecordDefeat(heroId, experienceReward)` | 按有效归属发经验；无归属只计击败、不出球，地面掉落保持 | C2 已实现 |
| `LootOrbNode.Tick(delta, target)` 单目标；`LevelViewController.Loot` 只认 `_player`       | 掉落无归属                                             | C3        |
| `InputSystem.ActionOf` 与英雄、出口、战斗/背包窗口按席位读键                                  | P2 映射已接入；SkillForm 默认 P1 留新 D2-3             | C4 已实现 |
| `LevelViewController._player` 单引用（相机、连击、出口、触发区、掉落、死亡、换装都读它）      | 单人                                                   | D2        |
| `LevelCameraNode._target` 单 `Node2D`                                                       | 单人取景                                               | D4        |
| `LevelViewController._Process` 中 `HeroDeathPresentation.Completed` 直接判失败              | 一人死即失败                                           | D5        |
| `TangsengEntity.ApplyHeavenlyDew` 只治疗自己                                                  | 无队友治疗                                             | D6        |
| `DragonKingEntity.OnShow` 读 P1 的成长等级                                                    | 单人等级分档                                           | D7        |
| `equipment_actions_menu.tscn` 的 `Panel/Gift` 按钮永久禁用（脚本无字段）                    | 无赠送                                                 | E1        |
| `SkillForm.SelectPage` 拒绝非出战英雄页签                                                     | 只能看自己                                             | 新 D2-3   |

**其他必须知道的事实：**

- `Tests/ProfileTests` 与 `Tests/BattleTests` 通过 `<Compile Include>` **直接链接生产源文件**（见两个 `.csproj`），所以能用 `internal` 成员，也因此**新增源文件必须同步加编译项**。`Tests/LevelTests` 不引用这些类型。
- 测试夹具：`Tests/ProfileTests/TestGameScope.cs`（含 `RestartHero()`、`EnterMapContext()`）、`Tests/BattleTests/HeroTestScope.cs`、`Tests/BattleTests/BattleArchitecture.cs`、`SkillReuseTests.cs` 内的 `SkillScope`。改构造签名要同步这四处。
- `MainPack/Scripts/Debug/SmokeInspection.cs:259` 用**反射**读 `AbstractHeroEntity` 的私有属性名 `"Attributes"`。改这个属性名会在运行时而非编译期出错。
- `MainPack/Scripts/Debug/` 下 `UiSmokeScenario.*`、`FrontendSmokeScenario`、`LevelSmokeScenario`、`EnemyAiSmokeScenario`、`TestArenaController`、`ProfileTestData` 有大量调用点，参数化接口时必须一起改。
- `GF.Base.PauseGame()/ResumeGame()` 只是全局速度开关，无引用计数。现有五处窗口/演出各自用 `_ownsPause`/`_resumeGame` 标志对称持有，**保持这个模式**。
- 存档读写链：`SaveSystem → GameArchiveUtility → GF.Archive`；DTO 转换集中在 `SaveDataUtility`；恢复在 `ProfileSystem.RestoreCheckpoint`。
- 英雄实体显示当前**不传 userData**（`ProcedureLevel.cs` 的 `ShowEntityAsync<AbstractHeroEntity>` 一行），由 `OnShow` 读模型校验，位置随后紧跟一行单独赋 `GlobalPosition`。敌人显示传 `EnemySpawn` record struct（`AbstractEnemyEntity.cs` 文件末尾），这是显示参数的既有先例。
- `AbstractActorEntity.OnShow` 已把 `Dead = false`；实体自己新增的池化字段必须在各自 `OnShow` 里复位。
- `GF.Entity.GetEntity(int entityId)` 可把 `AttackSource.OwnerEntityId` 解析回实体；`AbstractEnemyEntity.OnHurt` 已用这个方式锁定攻击方，C2 沿用同一手段。
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

| 编号 | 大任务                                 | 依赖 | 玩法行为变化             | 状态                                                      |
| ---- | -------------------------------------- | ---- | ------------------------ | --------------------------------------------------------- |
| A1   | 阵容取代单一出战英雄（存档 v13）       | —   | 无                       | 已完成，d2c3d13，审查通过                                 |
| A2   | 灵魂与背包归个人（存档 v14）           | A1   | 无                       | 已完成，c2f74db，审查通过                                 |
| A3   | 档案规则证据与存档格摘要               | A2   | 存档格文本               | 已完成，fe0bdbc，审查通过                                 |
| B1   | `HeroModel` 按英雄承载本局状态       | A1   | 无                       | 已完成，53e9b11，审查通过                                 |
| B2   | 技能登记与施放按英雄                   | B1   | 无                       | 已完成，e3440ca，审查通过                                 |
| B3   | 战斗规则证据与阶段 A～B 回归           | B2   | 只加测试                 | 已完成，511f0c5，审查通过                                 |
| C1   | 英雄实体携带席位                       | B1   | 无                       | 已完成，394b97c，审查通过                                 |
| C2   | 经验归属最后命中者                     | C1   | 无人命中的怪不发经验与球 | 已完成，c489b65，审查通过                                 |
| C3   | 地面物品与球按归属领取                 | C2   | 无                       | 已完成，61de0f2，审查通过                                 |
| C4   | 第二套键位与按席位开窗                 | C1   | 新增 P2 映射             | 已完成，0523aae，审查通过                                 |
| C5   | 阶段 C 单人回归                        | C4   | 只验证                   | 已完成（记录于 Validation），审查通过                     |
| D0   | 正式存档升级到 v14 并恢复可读          | —   | 用户旧档可读             | D0-3 已确认并转存，0307f36；上手验收待补，用户授权先做 D1 |
| D1   | 标题、单双人、依次选角、选格建档       | D0   | 可建双人档               | 已实现；构建/规则/扫描/引用/前端/UI通过；正式上手待补     |
| D2   | 两人同时出现、各自操作、各自 HUD       | D1   | 出现两个人物与两块 HUD   | 实现/自动验证通过，Panel重构复验通过；正式上手待补，后续推进已获用户授权           |
| D3   | 双人相机、死亡、出口、结算与 Boss 规则 | D2   | 完整双人关卡规则         | 已实现/全部自动验证通过；正式键盘1～6待补 |
| E1   | 双人赠送、技能页签与全关收尾           | D3   | 赠送、页签、全关可通     | E1-1/2/4已实现，最终完整UI亦退出0、完整PASS；用户授权E1-3与上手待补继续收尾 |

原 D1～D8、E1～E3 已于 2026-11-10 按用户要求合并为上表的 D0～E1，并按用户要求**一次交付**：D0→D1→D2→D3→E1 顺序连续执行，只在 D0-3 正式转存前与触发上报条件时停下（规则见第七部分第 0 节）。

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

单一出战英雄编号成员，以及 `Progress`、`Level`、`TotalExperience`、`Experience`、`MaxExperience`、`Slots`、`Weapon`、`Armor`、`Accessory`、`Treasure` 和旧的按部位便捷读取方法。

**A1 阶段保留不变：** `GetProgress(heroId)`、`GetSlots(heroId)`、`HasEquipment(equipmentId)`、当时的共享钱包与背包、`HighestClearedLevelId`、`EastSeaBannerAvailable`、`Capacity`、`HeroProgressionState`、`EquippedItems`。共享钱包与背包在 A2 改为按英雄持有。

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
- `CanAfford`、`TryActivate`、`TrySpendActivatedMana`、`TryActivateInput`、`CancelSkill`、`ClearSkillDefinitions`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable`、`GetBodyStateType`：**本批不改**（它们读 `HeroModel`，归 B1/B2）。

### A1-4　改 `EquipmentSystem`

文件：`GameScripts/Systems/EquipmentSystem.cs`

```csharp
// 旧：public bool TryEquip(Guid instanceId)
public bool TryEquip(int heroId, Guid instanceId)

// 旧：public bool TryUnequip(EquipmentSlot slot)
public bool TryUnequip(int heroId, EquipmentSlot slot)
```

- 归属校验改为将 `(int)definition.Owner` 与显式传入的 `heroId` 比较。
- 读写穿戴改用 `profile.GetSlots(heroId)`；原单次穿戴读取改为从 `GetSlots(heroId).Value` 按部位取（可在本文件内加一个 `private static EquipmentData SlotOf(EquippedItems items, EquipmentSlot slot)` 的纯读取局部方法，**不要**在 `ProfileModel` 上重新加公开便捷成员）。
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
| `MainPack/Scripts/Procedure/ProcedureLoadGame.cs`                    | 构造改传有序阵容、经验曲线与容量；A1 当时仍传共享初始余额，A2 改为由快照逐人恢复；日志中的英雄编号与等级改为阵容首位                                                                                                                                                        |
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

- 删除旧的共享灵魂绑定属性与共享背包集合属性。
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
- **删除**根 DTO 的共享灵魂字段。
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
- `Validate` 增加 `Soul >= 0` 的逐人检查，删除对根 DTO 共享余额的检查。

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
| `MainPack/Scripts/Debug/ProfileTestData.cs` 及各烟测                                     | 全部旧共享钱包与背包调用按 heroId 改                                                                                                        |
| `Tests/ProfileTests/*`、`Tests/BattleTests/*`                                          | 同步；`TestGameScope.CaptureSaveData` 不变                                                                                                |

### A2 完成判定

- 构建与三套测试全绿，数量不减。
- 实机前端与 UI 烟测通过（覆盖拾取、出售、批量出售、学技、唯一奖励、检查点回滚）。
- 全仓源码与文档不再引用 `ProfileModel` 已删除的共享钱包与背包成员；精确搜索及零命中结果记入 Validation，不在记录中重新引入旧成员名。
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
- `SaveForm.Logic.RefreshDirectory` 按席位拼接。2026-11-08 用户最终裁决：单/双人统一只显示角色名，均不显示等级；当前分别为 `"悟空"` 与 `"悟空 · 唐僧"`（双人 22 号字宽 132px）。不新增单双人显示分支、简称映射或动态字体/布局机制。
- **不改 `save_form.tscn` 的节点结构、位置与美术**；只改 Label 文本内容。若现有 Label 宽度放不下两人，停下报告，不要自行改布局。

### A3 完成判定

- 构建与三套测试全绿，ProfileTests 数量明显增加。
- 前端烟测通过（含目录读取、建档、删除确认）。
- 存档页单人、双人统一只显示角色名；日期、字号和布局保持原样（2026-11-08 用户裁决）。

---

# 第五部分　阶段 B：本局状态多人化

目标：`HeroModel` 从"一个英雄的数值"变成"按英雄索引的数值集合"。**阶段 B 结束时仍只生成 1 个英雄实体，单人行为与数值必须完全不变。**

阶段 B 是全计划触及面最广的一批（`HeroModel` 的调用点约 90 个文件、Debug 烟测里数百处）。因此 B1 允许中间编译错误，但必须**按子任务顺序**推进，每步记录错误数，B1-6 清零。

## 大任务 B1　`HeroModel` 按英雄承载本局状态

### B1-1　新建 `HeroState`

新文件：`Godot/GodotProject/TheGame/GameScripts/Models/HeroState.cs`，命名空间沿用 `GameLogic.Battle`。

把现有 `HeroModel.cs` 的**全部状态成员整体搬入** `HeroState`（普通类，**不继承 `AbstractModel`**、不实现 `IModel`）：

- 属性值：`Level`、`Hp`、`Mp`、`MaxHp`、`MaxMp`、`Power`、`Def`、`Mdef`、`Crit`、`Miss`、`Lucky`、`Toughness`、`Htarget`、`CritReduce`、`Ar`、`Sp`、`Vampirism`、`HpRegen`、`MpRegen`
- 无双：`Ws`、`MaxWs`、`WsActive`、`IsWsFull`、`WsDrainProgress`、`WsDrainPerSecond`、`WsDecayProgress`、`WsDecayPerSecond`
- 状态：`IsDepleted`、`ControlsEnabled`、`ActiveSkillId`
- 计时：`HurtHitCount`、`HurtChainSeconds`、`HurtInvulnerabilitySeconds`、`RecoveryProgress`、`SkillInvulnerableFrom`、`SkillInvulnerableUntil`
- 来源：`BaseAttributes`、`Sources`
- 效果：`_statusEffects` 字段与 `StatusEffects`、`GetStatusEffect`、`SetStatusEffect`、`RemoveStatusEffect`、`ClearStatusEffects`
- `CaptureCombatAttributes(int? actorId = null)`
- 嵌套类型 `StatusEffectKind`、`StatusEffectState` 一起搬，**保持嵌套在 `HeroState` 内**

关键约定：

- `HeroId` 改为构造参数并只读：`public HeroState(int heroId)`，`public int HeroId { get; }`。删掉原来的 `internal set`。
- 其余成员的访问级别、初值、`BindableProperty` 实例化方式**逐字保持**（`Level = new(1)`、`SkillInvulnerableFrom` 初值 `+∞` 等）。这些初值影响行为，不要"顺手规范化"。
- `StatusEffectKind` / `StatusEffectState` 从 `HeroModel` 移到 `HeroState` 后，全限定名变化（`HeroModel.StatusEffectKind` → `HeroState.StatusEffectKind`）。使用 `using static GameLogic.Battle.HeroModel;` 的文件（已知：`UiSmokeScenario.Dragon.cs:27`、`UiSmokeScenario.StatusEffects.cs:11`）要改成 `HeroState`。

同步加测试编译项：

- `Tests/ProfileTests/ProfileTests.csproj`：照 `HeroModel.cs` 那一行的写法新增一行 `HeroState.cs`。
- `Tests/BattleTests/BattleTests.csproj`：已用 `Models/**/*.cs` 通配，确认无需改动。

### B1-2　`HeroModel` 改成按英雄的容器

文件：`GameScripts/Models/HeroModel.cs`。继续是 `sealed class HeroModel : AbstractModel`，但只保留容器职责：

```csharp
private readonly Dictionary<int, HeroState> _heroes = new(); // 本局参战英雄的数值与状态。

/// <summary>本局全部参战英雄的只读视图。</summary>
public IReadOnlyDictionary<int, HeroState> Heroes => _heroes;

/// <summary>读取指定参战英雄的本局状态。</summary>
public HeroState GetState(int heroId)

/// <summary>为参战英雄建立本局状态；同一英雄不得重复建立。</summary>
internal HeroState CreateState(int heroId)

/// <summary>清空本局全部英雄状态。</summary>
internal void ClearStates()
```

- `GetState` 用 `_heroes[heroId]`，未建立时按字典默认抛 `KeyNotFoundException`，**不要包装成自定义异常或返回 null**。
- `CreateState` 用 `Add` 使重复建立直接抛出。
- **不加 `Remove`**：当前没有"关卡中途移除某个英雄"的需求（D5 的死亡只是实体退场，状态仍要供结算与回滚读取）。等真有需求再加。
- **不要**保留任何转发到"某个默认英雄"的成员。

### B1-3　`HeroStatusSystem` 参数化

文件：`GameScripts/Systems/Hero/HeroStatusSystem.cs`。先改这个较小的系统，便于验证模式。

- 删除 `private HeroModel Hero => this.GetModel<HeroModel>();`。
- `IsFrozen`、`IsPoisoned` 由属性改为方法：`public bool IsFrozen(int heroId)`、`public bool IsPoisoned(int heroId)`。
- `ApplyFreeze`、`AdvanceFreeze`、`ApplyPoison`、`AdvancePoison` 各加 `int heroId` 作为第一个参数。
- `ClearAll()` 保留无参，语义改为"清空全部参战英雄的效果"：遍历 `GetModel<HeroModel>().Heroes.Values` 逐个 `ClearStatusEffects()`。
- 新增 `public void ClearAll(int heroId)` 清单个英雄的效果，供 `HeroSystem.Damage`（死亡）与 `Reset` 使用。**注意：现在 `HeroSystem` 的死亡分支调用的是无参 `ClearAll()`，多人下会误清另一个人的中毒/冰冻，必须在 B1-4 改成按人清。**

### B1-4　`HeroSystem` 参数化

文件：`GameScripts/Systems/Hero/HeroSystem.cs`。

- 删除 `private HeroModel Hero => this.GetModel<HeroModel>();`。全部私有辅助改为显式接收 `HeroState state` 或 `int heroId`：`ClearHurtProtection`、`ReplacePassiveSource`、`ReplaceEquipmentSources`、`Recalculate`、`ApplyFinalAttributes`、`SetWsState`。
- 四个倍率由属性改为方法：`public float GetMoveSpeedMultiplier(int heroId)`、`GetJumpSpeedMultiplier`、`GetGravityMultiplier`、`GetAttackPowerMultiplier`。
- 下列成员加 `int heroId` 作为第一个参数：`Damage`、`Heal`、`RecordHurt`、`AdvanceHurtProtection`、`AdvanceRecovery`、`TrySpendMp`、`RestoreMp`、`SetSource`、`RemoveSource`、`RecordHit`、`AddWsFromPickup`、`TryActivateWs`、`SetControlsEnabled`、`AdvanceWs`、`ClearWs`，以及 `internal` 的 `Reset`、`SetGrowth`、`ResetWs`、`AddWs`。
- **`Damage` 的死亡分支**改为 `ClearWs(heroId)`、`GetSystem<HeroStatusSystem>().ClearAll(heroId)`、`ClearHurtProtection(该英雄)`，不再影响其他英雄。`Reset` 同理按人清。
- **三个订阅回调**（`OnProgressChanged`、`OnEquipmentChanged`、`OnPassiveLevelsChanged`）当前通过 `Hero.HeroId` 推断自己属于谁。多人下必须改为**每位英雄各自的闭包**，捕获自己的 `heroId`。
- 替换 A1 留下的 `_subscribedHeroId` 单一字段：

```csharp
private readonly Dictionary<int, (Action<HeroProgressionState> Progress, Action<EquippedItems> Slots,
    Action<IReadOnlyDictionary<PassiveId, int>> Passives)> _subscriptions = new(); // 每位参战英雄实际注册的回调，用于对称退订。
```

- **`OnInit`：** 遍历 `profile.HeroIds`，逐人 `CreateState(heroId)`、`Reset(heroId, …)`、`ResetWs(heroId, …)`，再建立三个闭包、存入 `_subscriptions`、以 `priority: 100` 注册。
- **`OnDeinit`：** 遍历 `_subscriptions` 对称退订，清空该字典，然后逐人 `ClearWs`、`ClearHurtProtection`，再 `HeroStatusSystem.ClearAll()`、`GetModel<HeroModel>().ClearStates()`。
- `AttributeSourceId` 的五个静态来源键（`s_weaponSource` 等）当前第二个参数固定为 `0`，而 `Sources` 字典现在归属于各自的 `HeroState`，**所以不需要把 heroId 编进来源键**。不要改这五个常量。

### B1-5　改英雄实体、身体状态与其他消费者

| 文件                                                                                                                                                    | 改法                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| ------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Entity/Heroes/AbstractHeroEntity.cs`                                                                                                                 | `private HeroState Attributes => this.GetModel<HeroModel>().GetState(HeroId);`。**属性名必须保持 `Attributes`**：`SmokeInspection.cs:259` 用反射按名字读它。`Invulnerable`、`Hp`、`MaxHp`、`Level`、三个倍率、`_PhysicsProcess` 全链、`ApplyDamage`、`RestoreHealth`、`TryActivateMusou`、`EndMusou`、`CaptureCombatAttributes`、`OnHitLanded`、`OnHurt`、`RefreshInvulnerabilityGlow`、`PrepareAttack` 全部传本实体 `HeroId` |
| `AbstractHeroEntity.OnShow`                                                                                                                           | 现在校验 `Attributes.HeroId != HeroId`。改为"本局已为本英雄建立状态"：`!this.GetModel<HeroModel>().Heroes.ContainsKey(HeroId)` 时抛同样语义的异常。**注意顺序**：`Attributes` 现在是 `GetState(HeroId)`，未建立时会先抛 `KeyNotFoundException`，所以校验必须先用 `Heroes.ContainsKey` 判断，不要先读 `Attributes`                                                                                                                                   |
| `AbstractHeroEntity.Skills.cs` / `.Capture.cs`                                                                                                      | 按本实体 `HeroId` 取                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `Entity/Heroes/Wukong/WukongEntity.cs`                                                                                                                | `CanReceiveStatusEffect`、`SuperArmor` 读本人 `WsActive`；`skills.CanAfford` 补 heroId                                                                                                                                                                                                                                                                                                                                                                          |
| `Entity/Heroes/Tangseng/TangsengEntity.cs` / `.AnimationEvents.cs`                                                                                  | 全部 `HeroModel` 读取改为本人状态                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `Entity/Heroes/Body/Core/IHeroBody.cs`                                                                                                                | **新增 `int HeroId { get; }`**，由 `AbstractHeroEntity.HeroId` 满足。身体与技能状态只有 `IHeroBody`，必须靠它知道是谁                                                                                                                                                                                                                                                                                                                                       |
| `Entity/Heroes/Body/Core/AbstractHeroBodyState.cs`、`AbstractHeroSkillState.cs`                                                                     | 通过 `body.HeroId` 取状态与调用系统                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `Wukong/Skills/WukongFlameThrustState.cs`、`WukongFireDemonState.cs`、`WukongBlazingFlashState.cs`、`Tangseng/Skills/TangsengIceDragonState.cs` | 按 `body.HeroId` 取                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `Entity/Effects/SkillEffectNode.cs:326`                                                                                                               | 现在无条件 `GetSystem<HeroSystem>().RecordHit(result)`。改为：仅当 `_attacker.Side == CombatSide.Hero` 且 `GF.Entity.GetEntity(Source.OwnerEntityId)` 是 `AbstractHeroEntity hero` 时，调 `RecordHit(hero.HeroId, result)`。**唐僧分身的 `OwnerEntityId` 已是本体 id**，所以分身命中正确记到本体                                                                                                                                                      |
| `Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs:190`                                                                           | `ApplyFreeze` 作用于**实际命中的** `target`（`OnHitLanded` 已把 `target` 模式匹配成 `AbstractHeroEntity`，直接取其 `HeroId`），不要读 AI 目标                                                                                                                                                                                                                                                                                                         |
| `Entity/Enemies/Bosses/zm1_05_qixing_cave/LionCamelPoisonCloudNode.cs`                                                                                | `ApplyPoison` 同上，按 `target` 的 `HeroId`                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `Entity/Level/LevelViewController.cs:485`、`.Loot.cs:64-66`                                                                                         | 按 `_player.HeroId` 取（C3 再改成按归属英雄）                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `Systems/LevelSystem.cs:140,175,342`                                                                                                                  | `IsDepleted` 按英雄取。`TryCollectPickup`、`CollectLoot` 已有 `heroId` 参数，直接用；`TransferToDragonPalace` 本批用 `HeroOf(PlayerSlot.P1)`，D5 改为"任一存活者"                                                                                                                                                                                                                                                                                           |
| `Systems/LevelSystem.cs:151-192`                                                                                                                      | `HeroSystem` 的 `Heal`、`RestoreMp`、`AddWsFromPickup` 补传方法已有的 `heroId`                                                                                                                                                                                                                                                                                                                                                                                |
| `Systems/SkillSystem.cs`                                                                                                                              | 读 `HeroModel` 的八处（`GetBodyStateType`、`CanAfford`、`TryActivate`、`TrySpendActivatedMana`、`CancelSkill`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable`）加 `int heroId`。`TryActivateInput` 同样加。登记表拆分留给 B2；本批 `SelectSkills` 不变，`TryActivate` 里 A1 留下的两处 `HeroOf(PlayerSlot.P1)` 改为用传入的 heroId                                                                                                    |
| `UI/BattleForm/BattleForm.Logic.cs`                                                                                                                   | 七处 `HeroModel` 绑定改为 `GetState(_formData.Hero.HeroId)` 上的属性；`Unbind` 用同一 heroId 对称退订                                                                                                                                                                                                                                                                                                                                                             |
| `UI/InventoryForm/InventoryForm.Logic.cs`、`Components/InventoryCharacterPanel.cs`                                                                  | 同上，按 `_inventoryData.Hero.HeroId`                                                                                                                                                                                                                                                                                                                                                                                                                                 |

### B1-6　改测试与 Debug 夹具（本大任务的清零步）

- `Tests/BattleTests/HeroTestScope.cs`：`HeroModel` 容器 + 一个 `HeroState`；保留便于断言的入口（如 `public HeroState State => Hero.GetState(1);`），`System.Reset(1, …)` 带 heroId。
- `Tests/BattleTests/SkillReuseTests.cs` 的 `SkillScope`、`Tests/ProfileTests/TestGameScope.cs`（含 `RestartHero()` 重建本局模型的那段）同步。
- `IHeroBody` 新增成员后，三处测试假实现必须补 `HeroId`：`HeroBodyTests.cs:819`、`SkillReuseTests.cs:427`（`SkillHero`）、`SkillStateTests.cs:319`（`Motion`）。
- 测试文件按新签名改：`Tests/BattleTests/{HeroModelTests,MusouTests,SkillReuseTests,HeroBodyTests}.cs`、`Tests/ProfileTests/{FreezeTests,PoisonTests,HurtProtectionTests,RecoveryTests,LevelRewardTests,MusouTests,BindablePropertyTests,ProfileTests,PassiveSkillTests,SkillSystemTests,SkillStateTests,SkillLearningTests,EquipmentTests,BossEquipmentTests,ModelTransactionTests}.cs`。
- `MainPack/Scripts/Debug/` 下全部 `GetModel<HeroModel>()`、`GetSystem<HeroSystem>()`、`GetSystem<HeroStatusSystem>()` 调用按 heroId 改。涉及约 25 个 `UiSmokeScenario.*` partial，以及 `FrontendSmokeScenario`、`LevelSmokeScenario`、`EnemyAiSmokeScenario`、`TestArenaController`。
- 本步结束时编译错误必须为 0。

### B1 完成判定

- 构建 0 错误。
- 三套规则测试全绿且数量不减（基线 304 / 193 / 19）。
- `--smoketest=frontend`、`--smoketest=ui` 实机通过；Godot 扫描与 `validate_game_references.py` 通过。
- 单人数值、无双、状态效果、恢复节拍、受击保护与 A3 之前完全一致。
- 全仓搜索：`GetModel<HeroModel>()` 之后只接 `.GetState(`、`.Heroes`、`.CreateState(`、`.ClearStates(`，没有任何标量属性直读。
- 交付说明写明两处缺陷修正：①`HeroSystem` 的三个订阅回调改为逐人闭包并对称退订（替换 A1 的单一 `_subscribedHeroId`）；②`Damage` 死亡分支与 `Reset` 的状态效果清理改为按人，不再清空全部英雄。

---

## 大任务 B2　技能登记与施放按英雄

文件：`GameScripts/Systems/SkillSystem.cs` 及其调用方。

- `_skillDefinitions` 从 `Dictionary<SkillId, SkillDefinition>` 改为 `Dictionary<int, Dictionary<SkillId, SkillDefinition>>`（按 heroId）。
- `SelectSkills(int heroId, params SkillDefinition[])`：只替换该英雄的登记，不影响另一人。保留现有全部校验（`HeroIds.Contains(heroId)`、定义非空、状态类型合法、技能编号唯一、输入动作唯一、配置归属该英雄）。
- `ClearSkillDefinitions(int heroId)` 清单人；保留无参 `ClearSkillDefinitions()` 清全部，供 `OnDeinit` 使用。
- `GetBodyStateType`、`TryActivate`、`TrySpendActivatedMana`、`TryActivateInput`、`CancelSkill`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable`、`CanAfford`、`IsAvailable` 全部按 heroId 查自己的登记表。
- `AbstractHeroEntity.OnHide` 现在调无参 `ClearSkillDefinitions()`，改为只清本人（`ClearSkillDefinitions(HeroId)`）。**这是真实缺陷**：多人下一个英雄回池会清掉另一人的技能登记。
- 同步 `SkillStateTests`、`SkillReuseTests`、`SkillSystemTests`、`SkillLearningTests`、`TestGameScope`（它在构造末尾调 `SelectSkills`）。

**完成判定：** 构建与三套测试全绿、数量不减；单人行为不变；交付说明写明 `OnHide` 只清本人这一缺陷修正。

---

## 大任务 B3　战斗规则证据与阶段 A～B 回归

### B3-1　补战斗规则测试

在 `Tests/BattleTests` 与 `Tests/ProfileTests` 补（夹具需支持两名英雄，优先扩展 `TestGameScope` 而不是新建第三个 scope）：

1. 两人各自的属性来源（装备、被动、`SetSource` Buff）互不影响；`RemoveSource` 只影响本人。
2. 两人的冻结、中毒、受击保护、恢复节拍互不影响；一人死亡时只清自己的状态效果与无双（对应 B1-4 的修正）。
3. 两人的无双蓄积、开启、衰减、消耗互不影响。
4. 两人同时施放技能，各自 `ActiveSkillId` 不被覆盖，各扣自己的蓝。
5. 一人回池清登记后，另一人的技能仍可施放（对应 B2 的 `OnHide` 修正）。
6. 本局重建后旧 `HeroState` 不再被任何订阅更新（证明逐人对称退订）。
7. `SelectSkills` 对不在阵容中的英雄抛异常。

### B3-2　阶段 A～B 完整回归

执行第一部分第 5 节的全部命令，加 `--smoketest=frontend`、`--smoketest=ui`、`validate_game_references.py` 和 Godot 扫描。确认单人行为、数值、存档往返与阶段 A 完全一致。

---

# 第六部分　阶段 C：席位进入运行时

目标：席位进入实体与输入、击杀与掉落有真实归属、第二套键位就位。**阶段 C 结束时仍只生成 1 个英雄实体；单人玩法除 C2 声明的一项外完全不变。**

阶段 C 的批次共同约束：

- 本阶段**不改** `LevelViewController._player` 单引用的结构（它在 D2 才变成集合）。凡需要"参战英雄集合"的地方，本阶段仍用 `_player`，但写法必须是"按某个英雄做判断"，让 D2 只需把单个替换成遍历。
- 不改 `ProcedureLevel` 的单英雄装配（D2）。
- 不改任何 `.tscn` 场景。C4 只改 `project.godot` 的 `[input]` 段。

## 大任务 C1　英雄实体携带席位

### C1-1　新增显示参数 `HeroSpawn`

新文件 `Godot/GodotProject/TheGame/GameScripts/Entity/Heroes/HeroSpawn.cs`，命名空间 `GameLogic.Heroes`（与 `AbstractHeroEntity` 相同）：

```csharp
/// <summary>
/// 一次英雄显示的玩家席位与出生位置；由流程创建，随 GF.Entity.ShowEntityAsync 传入。
/// </summary>
/// <param name="Slot">本次控制该英雄的玩家席位。</param>
/// <param name="Position">出生世界坐标，单位像素。</param>
public readonly record struct HeroSpawn(PlayerSlot Slot, Vector2 Position);
```

- 先例是 `AbstractEnemyEntity.cs` 文件末尾的 `EnemySpawn`；`HeroSpawn` 单独成文件，因为 `AbstractHeroEntity` 已拆为多个 partial。
- 同步编译项：两个测试工程都只显式链接 `Entity/Heroes/` 下的 `Wukong/Skills/*.cs`、`Skills/ISkillActor.cs` 与 `Body/**`，**不链接** `AbstractHeroEntity` 所在目录，所以 `HeroSpawn.cs` **不需要**加编译项。若构建测试时报找不到 `HeroSpawn`，说明有测试直接引用了它，停下报告。

### C1-2　`AbstractHeroEntity` 消费 `HeroSpawn`

文件：`Entity/Heroes/AbstractHeroEntity.cs`。

- 新增 `public PlayerSlot Slot { get; private set; }`（XML 注释说明"本次显示由哪个席位控制；每次 OnShow 覆盖"）。
- `OnShow(object userData)` 的现有开头是"`Heroes.ContainsKey(HeroId)` 不成立就抛 `InvalidOperationException`"。在它**之后**、`base.OnShow(userData)` **之前**插入：
  1. `if (userData is not HeroSpawn spawn)` 抛 `ArgumentException("英雄显示参数必须包含玩家席位与出生位置。", nameof(userData))`，写法照 `AbstractEnemyEntity.OnShow`。
  2. `if (this.GetModel<ProfileModel>().HeroOf(spawn.Slot) != HeroId)` 抛 `InvalidOperationException`，消息说明席位与英雄不一致。`HeroOf` 越界自会抛 `ArgumentOutOfRangeException`，不要再包一层。
  3. `Slot = spawn.Slot;`
- 在 `OnShow` 中现有"池复用复位"那段（`PendingHurt = null;` 一带）加入 `GlobalPosition = spawn.Position;`，与 `Velocity = Vector2.Zero;` 相邻。
- 更新 `OnShow` 的 XML：`<param name="userData">` 改为"必须是 "；`<exception>` 补 `ArgumentException`。
- **不在 `OnHide` 里清 `Slot`**：值类型、下次 `OnShow` 必然覆盖，清理只是无意义防御。

### C1-3　`IHeroBody` 暴露席位

- `Entity/Heroes/Body/Core/IHeroBody.cs` 在 `HeroId` 之后新增 `PlayerSlot Slot { get; }`，XML 注释"本身体当前所属的玩家席位，用于按席位读取输入"。
- `AbstractHeroEntity.Slot` 已是公开属性，自动满足接口。
- 三处测试假实现补 `public PlayerSlot Slot => PlayerSlot.P1;`：`Tests/BattleTests/HeroBodyTests.cs`（`IHeroBody` 实现类）、`Tests/BattleTests/SkillReuseTests.cs`（`SkillHero`）、`Tests/ProfileTests/SkillStateTests.cs`（`Motion`）。以 B1 新增 `HeroId` 成员的位置为准，放在它旁边。

### C1-4　显示英雄的调用方传 `HeroSpawn`

全仓共有三处显示英雄的入口（含测试场地的转发调用）：

| 文件                                                                                                                                   | 改法                                                                                                                                                                                                                                            |
| -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MainPack/Scripts/Procedure/ProcedureLevel.cs` 的 `ShowEntityAsync<AbstractHeroEntity>(heroConfig.EntityId)`                       | 改为 `ShowEntityAsync<AbstractHeroEntity>(heroConfig.EntityId, new HeroSpawn(PlayerSlot.P1, _levelViewController.PlayerSpawnPosition))`；**删除**其后单独的 `_hero.GlobalPosition = _levelViewController.PlayerSpawnPosition;` 那一行 |
| `MainPack/Scripts/Debug/UiSmokeScenario.cs` 的 `ShowHeroAsync` 中 `GF.Entity.ShowEntityAsync(definition.EntityId)`               | 改为传 `new HeroSpawn(PlayerSlot.P1, Vector2.Zero)`。调用方随后自己设置位置的写法保留不动                                                                                                                                                     |
| `MainPack/Scripts/Debug/TestArenaController.cs` 的 `SpawnHeroAsync` 中 `ShowOwnedEntityAsync(entities, _heroEntityId, null, ct)` | 改为传 `new HeroSpawn(PlayerSlot.P1, _playerStart.GlobalPosition)`，并删除其后单独赋 `hero.GlobalPosition` 的那一行                                                                                                                         |

`TestArenaController.ShowOwnedEntityAsync` 只改写 `EnemySpawn`，`HeroSpawn` 原样透传，无需改它。

`SmokeTestDriver` 只在场景树里查找英雄、从不显示英雄，**不改**。

**不要**给 `BattleFormData`、`InventoryFormData` 加席位字段：两者已持有英雄实体，`Hero.Slot` 就是席位。

### C1 完成判定

- 构建 0 错误；三套测试全绿且数量不减（基线 320 / 195 / 19）。
- Godot 扫描、`validate_game_references.py`、`--smoketest=frontend`、`--smoketest=ui` 通过。
- 全仓搜索：英雄实体的 `ShowEntityAsync` 调用全部带 `HeroSpawn`；`ProcedureLevel` 与 `TestArenaController` 中不再有显示后单独赋 `GlobalPosition` 的行。
- 单人出生位置、朝向、动画、外观与之前一致（烟测会覆盖）。

## 大任务 C2　经验与球的归属：最后一次有效命中者

### 官方规则（逐行核对，`BaseMonster.as`）

- `curAttackTarget` 同时是 AI 追击目标与奖励归属者。
- **英雄每次命中**怪物时（`beAttack` 第 503～514 行）**先**把 `curAttackTarget` 改写为命中者（唐僧分身 `Role2Shadow` 改写为其本体 `source`），**再**扣血；扣到死亡时立即 `BaseHero(curAttackTarget).roleProperies.setExper(exp)`（第 536～540 行）。所以**致死那一击的命中者**拿经验。
- 怪物在无人攻击时也会通过 `selectTarget`（第 417～440 行）自行选中一名英雄作为 `curAttackTarget`；**因此官方在"从未被英雄打过、但已锁定某人"的情况下也会把经验给那个人**（例如陷阱击杀已锁定目标的怪）。这是官方把 AI 目标与奖励归属合用一个字段的副作用。
- `dropAura`（第 792～835 行）先 `addMedicine()`、`fallEquip()`（地面物品，与归属无关），**然后**若 `!curAttackTarget` 直接 `return`，否则所有红/绿/黄/蓝球都 `new auraXxx(this, curAttackTarget)`，追踪同一名英雄。

### 项目取舍（已定，不得更改）

- **归属 = 最后一次有效命中本怪的英雄**，独立字段维护，**不复用** AI 目标 `_target`。理由：`_target` 会在目标死亡/离开时被 `UpdateTarget` 替换或在 `BeginDeath` 被 `SetTarget(null)` 清空；而且官方"无人打过也按锁定目标发经验"是共用字段的副作用，不是设计意图，复刻它会让双人时一人纯靠站位白拿经验。
- **从未被任何英雄有效命中的怪物**：计入击败数，**不发经验、不生成球**（地面物品照常掉落）。单人下这只影响"陷阱或环境杀死的怪"，这是本阶段唯一的玩法行为变化，交付说明必须写明并引用上面的官方 `dropAura` 早退依据。

### C2-1　敌人记录最后命中英雄

文件：`Entity/Enemies/AbstractEnemyEntity.cs`。

- 新增私有字段：

```csharp
private int _lastHeroAttackerId; // 最近一次有效命中本怪的英雄实体编号；决定经验与飞行球归属，0 表示没有英雄命中过。
```

- **写入点：`AbstractEnemyEntity.OnHurt` 方法体的第一条语句**，在创建命中闪光之前、在 `if (Dead) return;` 之前：

```csharp
// 先记账再处理表现：致死的那一击也必须记入归属，官方 beAttack 同样先改写目标再扣血。
if (GF.Entity.GetEntity(attackerEntityId) is AbstractHeroEntity)
{
    _lastHeroAttackerId = attackerEntityId;
}
```

  理由：`AbstractActorEntity.ReceiveHit` 先 `ApplyDamage` 置 `Dead`，再调 `OnHurt`；而现有 `OnHurt` 在 `if (Dead) return;` 之后才锁定攻击方。写在 `return` 之后会漏掉致死一击。

- **鹏魔蛋态**：`PengDemonKingEntity.OnHurt` 在 `IsEgg` 时**不调** `base.OnHurt`，而破蛋致死的第五击正发生在蛋态。必须修改它：

```csharp
protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
{
    if (!IsEgg)
    {
        base.OnHurt(data, result, knockback, attackerEntityId);
        return;
    }

    RecordHeroAttacker(attackerEntityId);
}
```

  为此在 `AbstractEnemyEntity` 把上面那段记账抽成 `protected void RecordHeroAttacker(int attackerEntityId)`（带中文 XML），基类 `OnHurt` 第一行调用它。**这是唯一允许为本批新增的受保护方法**，因为已经有两个调用点。

- 其他三个 `OnHurt` 重写都调 `base.OnHurt`，自动覆盖，**不改**：`AbstractMonsterEntity`、`HuaguoshanMonkeyEntity`、`JiaoDemonKingEntity`（同向命中分支也调 `base.OnHurt`）。
- `ReceivePeriodicDamage`（中毒、擒抱吸血）不经 `OnHurt`，不改写归属，保持。
- **复位**：`AbstractEnemyEntity.OnShow` 中现有 `SetTarget(null);` 旁加 `_lastHeroAttackerId = 0;`。
- **不要**在 `BeginDeath` 里清它：`BeginDeath` 要先读取它填事件，且怪物死后不会再进入 `OnHurt`。

### C2-2　死亡事件携带归属

- `Event/Combat/EnemyDiedEventArgs.cs`：新增 `public int KillerEntityId { get; private set; }`（XML："最后一次有效命中本怪的英雄实体编号；0 表示无英雄归属，不发经验与飞行球"）。
- `Create` 增加参数 `int killerEntityId`，**放在 `experienceReward` 之后**（与经验语义相邻）；`Clear` 复位为 0。
- 两个调用点同步：
  - `AbstractEnemyEntity` 的 `IEnemyBody.BeginDeath`：传 `_lastHeroAttackerId`。
  - `MainPack/Scripts/Debug/LevelSmokeScenario.cs` 第 838 行手动 `Fire` 的那处：传 `_hero.Id`（同一方法第 824 行已用 `_hero.Id` 作为攻击来源）。
- `LevelSpawnerNode.EnemyDefeated` 的签名已带 `EnemyDiedEventArgs`，**不改**。

### C2-3　经验发给归属者

- `Systems/LevelSystem.cs` 的 `RecordDefeat` 改为：

```csharp
/// <summary>
/// 记录击败；有归属英雄时把配方经验发给该英雄。
/// </summary>
/// <param name="heroId">归属英雄编号；0 表示无英雄命中过，只计数不发经验。</param>
/// <param name="experienceReward">本次配方经验。</param>
/// <returns>归属英雄是否至少升了一级。</returns>
public bool RecordDefeat(int heroId, int experienceReward)
```

- `DefeatedEnemyCount++` 始终执行。
- `AwardedExperience` **只在 `heroId != 0` 时**累加（它是结算界面"获得经验"，未发出的经验不能计入）。
- `heroId == 0` 返回 false，不调 `AddExperience`。删掉原来的 `HeroOf(PlayerSlot.P1)` 占位。
- `Entity/Level/LevelViewController.Loot.cs` 的 `OnEnemyDefeated`：
  - 在现有开头的守卫之后，解析归属：`var killer = GF.Entity.GetEntity(data.KillerEntityId) as AbstractHeroEntity;`。只接受 `ReferenceEquals(killer, _player)` 的情况为有效归属（D2 改为"属于本会话参战集合"）；否则视为无归属。
  - `RecordDefeat(killer?.HeroId ?? 0, data.ExperienceReward)` 返回 true 时调 `killer.PlayLevelUp()`（**不是** `_player.PlayLevelUp()`）。
  - 现有开头 `!_player.IsAlive` 的整体 return **本批保留**。注意它的含义：玩家死后怪物死亡（如死前打出的延迟效果致死）既不计数也不掉落。单人下玩家死亡即失败，影响可忽略；D5 改为"只要会话进行中就计数，奖励按归属者是否存活决定"。
- 测试同步：`Tests/ProfileTests` 中所有 `RecordDefeat(` 调用补 heroId；**新增**用例覆盖 `heroId == 0` 时击败数增加、经验与 `AwardedExperience` 均不变。

### C2-4　牛心唯一奖励发给归属者

文件：`Entity/Enemies/Bosses/zm1_10_bull_demon_valley/BullDemonHeartDeathState.cs`。

- 官方 `Monster24.toDead` **直接推入 `gc.player1.zblist`**（第 393～397 行），即永远给 P1，不看谁打的。项目**不采用**这一点：双人下应由致死者获得，与其余奖励一致；在交付说明中写明这一项目差异。
- 改为发给致死者。`BullDemonHeartDeathState` 是 `internal sealed`、不派生自敌人实体，读不到 protected 成员；它已有 `var owner = (BullDemonHeartEntity)body;`。因此：
  - 在 `AbstractEnemyEntity` 增加 `internal int LastHeroAttackerId => _lastHeroAttackerId;`（XML："最后一次有效命中本怪的英雄实体编号；供本怪死亡状态发放专属奖励"）。用 `internal` 而非 `public`，因为只有同程序集的死亡状态需要它。
  - 状态中：`int heroId = GF.Entity.GetEntity(owner.LastHeroAttackerId) is AbstractHeroEntity hero ? hero.HeroId : 0;`。
- `heroId == 0` 时**不调用** `GrantUniqueEquipment`，也不 `Fire` `EquipmentRewardEventArgs`；不冒认 P1。
- 删除原 `HeroOf(PlayerSlot.P1)` 占位。
- 注意 `BullDemonHeartDeathState.cs` 被 `Tests/BattleTests/BattleTests.csproj` 第 63 行**显式排除**，测试工程不编译它，改动只影响游戏工程构建。

### C2-5　飞行球只在有归属时生成

- `LevelViewController.Loot.cs` 的 `OnEnemyDefeated` 中，地面物品（`RollGround` 与两次 `ShowGroundItem`）**保持原位、与归属无关**。
- 飞行球那段（`LootSystem.Roll` 与 `orb.Show`）改为仅在 C2-3 解析出有效归属英雄时执行；`hero.MaxHp/MaxMp` 改为读**归属英雄**的 `HeroState`（不再是 `_player`）。
- 球的追踪目标在 C3 改；本批 `orb.Show` 签名不动。

### C2 完成判定

- 构建、三套测试全绿且数量增加（至少新增 C2-3 的无归属用例）。
- `--smoketest=frontend`、`--smoketest=ui` 通过；单人正常击杀的经验、升级动画、球数量与之前一致。
- 全仓搜索 `RecordDefeat(`：每处都带 heroId；`LevelSystem.cs`、`BullDemonHeartDeathState.cs` 中 `HeroOf(PlayerSlot.P1)` 清零。
- 交付说明写明：①唯一行为变化（无英雄命中过的怪只计数、不发经验与球，引用官方 `dropAura` 早退）；②与官方的差异（官方无人打过也会按 AI 锁定目标发经验，牛心奖励官方恒给 P1，项目都改为最后命中者）。

## 大任务 C3　飞行球追踪归属者、地面物品按接触者领取

### C3-1　飞行球记录并追踪归属者

- `Entity/Drops/LootOrbNode.cs`：`Show` 增加 `int ownerEntityId` 参数（放在 `reward` 之后），存为私有字段并提供 `public int OwnerEntityId { get; private set; }`；`OnGet`/`OnRelease` 复位为 0。**`Tick(float delta, Vector2 target)` 签名不变**，节点不持有实体引用。
- `LevelViewController.Loot.cs`：
  - `OnEnemyDefeated` 中 `orb.Show(data.Position, reward, killer.Id, OnLootFinished)`。
  - `TickLoot` 中推进球时，按 `orb.OwnerEntityId` 解析英雄：解析成功且 `IsAlive` 时以其 `HurtBox.GetGlobalBounds().GetCenter()` 为目标；否则**回收该球、不发放**（官方球追踪单一 `sourceHero`，目标消失即作废）。
  - `OnLootFinished(LootOrbNode orb, LootReward reward)` 已拿到 `orb`：按 `orb.OwnerEntityId` 解析英雄后 `CollectLoot(hero.HeroId, reward)`，删除 `_player.HeroId` 占位。注意现有方法先 `NodePool.Instance.Release(orb)` 再发放，**必须在 Release 之前读取 `OwnerEntityId`**，否则读到的是复位后的 0。
  - `TickLoot` 开头的 `!IsInstanceValid(_player) || !_player.IsAlive` 整体 return 会让玩家死亡时所有球停在空中。本批保留（单人下玩家死亡即进入失败流程，`OnHeroDied` 会 `ClearLoot()`）；D5 改为按归属者逐球处理。
- 球的恢复量仍在生成时按归属英雄上限确定（C2-5 已改为读归属英雄），**不要**改成领取时计算。

### C3-2　地面物品由接触者领取

- `TryCollectGroundItem(AbstractHeroEntity hero, PickupId pickupId, EquipmentData data)`：删除 `ReferenceEquals(hero, _player)` 的拒绝；改为接受"属于本会话的存活英雄"（本阶段即 `_player`，写成 `ReferenceEquals(hero, _player) && hero.IsAlive` 的单人等价判断，D2 改为集合判断）。内部 `TryCollectEquipment`/`TryCollectPickup` 改传 `hero.HeroId`，删除 `_player.HeroId` 占位。
- 恢复品飘字位置改为 `hero.GlobalPosition`（当前写死 `_player`）。
- `GroundItemNode.TryCollect` 的 `_collected` 保证同一物品只成功一次，**不改**。
- 满包时 `TryCollectEquipment` 返回 false，物品留在地面，保持现状。

### C3-3　恢复飘字显示在被治疗者身上

- `LevelViewController.OnHeroHealthRestored(int amount)` 当前用 `_player.GlobalPosition`。`AbstractHeroEntity.HealthRestored` 是 `Action<int>`，不带实体。
- **本批不改事件签名**：单人下 `_player` 就是被治疗者。在 D2 把订阅改成按集合时一并改为闭包捕获实体。把这一项登记到第十部分占位清单，**本批不动代码**。

### C3 完成判定

- 构建、三套测试全绿且数量不减；`--smoketest=frontend`、`--smoketest=ui` 通过。
- `LevelViewController.Loot.cs` 中不再出现 `_player.HeroId`；`TickLoot` 不再把 `_player` 当作所有球的统一目标。
- 单人拾取、满包、球飞行与结算数值与之前一致。

## 大任务 C4　第二套键位与按席位读键、开窗

### C4-1　`project.godot` 新增 P2 动作

在 `[input]` 段末尾按**与现有条目完全相同的格式**追加 13 个动作，命名为现有名加 `_p2`，统一 `"device":-1`、`"keycode":0`、按 `physical_keycode` 绑定、`"deadzone": 0.5`（`move_down_p2` 用 0.2，与 `move_down` 一致）：

| 新动作                  | P2 键        | `physical_keycode` | Godot 枚举         |
| ----------------------- | ------------ | -------------------- | ------------------ |
| `move_left_p2`        | ←           | 4194319              | `Key.Left`       |
| `move_right_p2`       | →           | 4194321              | `Key.Right`      |
| `move_down_p2`        | ↓           | 4194322              | `Key.Down`       |
| `interact_p2`         | ↑           | 4194320              | `Key.Up`         |
| `attack_p2`           | 小键盘 1     | 4194439              | `Key.Kp1`        |
| `jump_p2`             | 小键盘 2     | 4194440              | `Key.Kp2`        |
| `skill_1_p2`          | 小键盘 4     | 4194442              | `Key.Kp4`        |
| `skill_2_p2`          | 小键盘 5     | 4194443              | `Key.Kp5`        |
| `skill_3_p2`          | 小键盘 6     | 4194444              | `Key.Kp6`        |
| `skill_4_p2`          | 小键盘 3     | 4194441              | `Key.Kp3`        |
| `musou_p2`            | 小键盘 0     | 4194438              | `Key.Kp0`        |
| `inventory_toggle_p2` | 小键盘 `/` | 4194434              | `Key.KpDivide`   |
| `skill_toggle_p2`     | 小键盘 `*` | 4194433              | `Key.KpMultiply` |

数值已对 `GodotSharp 4.7.2` 实测（`Kp0 = 4194438` 排在 `Kp1..Kp9` 前，不连续）。实现时复核并写进交付说明。技能槽按官方语义对应，**不要**按小键盘数字递增重排。**不改** P1 现有条目，也不统一它们不一致的 `device` 值。

### C4-2　`InputSystem` 按席位解析

文件：`Systems/Input/InputSystem.cs`。

- 保留 13 个 P1 常量不变。
- 新增：

```csharp
/// <summary>
/// 按席位取 InputMap 中实际的动作名；P1 使用基名，P2 使用带 "_p2" 后缀的映射。
/// </summary>
/// <param name="action">P1 基名常量，不能是界面导航动作。</param>
/// <param name="slot">本次读取输入的玩家席位。</param>
/// <returns>InputMap 中实际存在的动作名。</returns>
public static string ActionOf(string action, PlayerSlot slot) => slot == PlayerSlot.P1 ? action : action + "_p2";
```

  **不加** P2 名称缓存：每次拼接一个短字符串的开销可以忽略，缓存是没有证据的优化。若性能烟测显示问题再议。

- `Sample(IHeroBody hero, float dt, bool controlsEnabled)` 内部把每个常量换成 `ActionOf(常量, hero.Slot)`。
- `IsActionPressed`、`IsActionJustPressed`、`IsActionJustReleased`、`ConsumePress` **统一增加 `PlayerSlot slot` 参数**（放最后），内部先 `ActionOf` 再走原逻辑。**不要**做默认 P1 的重载。
- `HeroActionData.ConsumedActions` 记录的是解析后的动作名，P1/P2 不会互相消费，**不改**。
- `ui_*`、`UiAccept`、`UiCancel` 是界面导航，**不经** `ActionOf`。

### C4-3　改所有直接读键处

| 文件                                                                                                    | 改法                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Entity/Heroes/Tangseng/TangsengEntity.cs`（`ConsumePress(ActionData, InputSystem.Skill2)`）        | 补 `Slot`                                                                                                                                                                            |
| `Entity/Heroes/Tangseng/TangsengEntity.AnimationEvents.cs`（`IsActionPressed(InputSystem.Attack)`） | 补 `Slot`                                                                                                                                                                            |
| `Entity/Heroes/Tangseng/Skills/TangsengIceDragonState.cs`（`IsActionPressed(InputSystem.Attack)`）  | 补 `body.Slot`                                                                                                                                                                       |
| `Entity/Heroes/Tangseng/Skills/TangsengWaterDemonBurstState.cs`（`ConsumePress(…Skill4)`）         | 补 `body.Slot`                                                                                                                                                                       |
| `Systems/SkillSystem.cs` 的 `TryActivateInput(int heroId, ISkillActor actor, HeroActionData data)`  | 增加 `PlayerSlot slot` 参数，`input.ConsumePress(data, action, slot)`。`ISkillActor` **不加**席位成员。调用方 `WukongEntity`、`TangsengEntity` 传 `Slot`             |
| `Entity/Level/Exit/LevelExitNode.cs`                                                                  | 见 C4-4                                                                                                                                                                                |
| `UI/BattleForm/BattleForm.Logic.cs` 的 `_UnhandledInput`                                            | 见 C4-5                                                                                                                                                                                |
| `UI/InventoryForm/InventoryForm.Logic.cs` 的 `_Input`                                               | 关闭键改为 `inputEvent.IsActionPressed(InputSystem.ActionOf(InputSystem.InventoryToggle, _inventoryData.Hero.Slot))`；`UiCancel` 不变                                              |
| `UI/SkillForm/SkillForm.Logic.cs` 的 `_Input`                                                       | 本批 `SkillForm` 没有打开者席位（地图也能打开，无英雄实体）。**本批只认 P1 的 `SkillToggle` 与共用 `UiCancel`**，保持现状；E2 引入请求者席位后再改。登记到第十部分占位清单 |
| `Tests/*` 中 `IInputSource` 假实现与直接调用 `ConsumePress`/`IsAction*` 的测试                  | 补 `PlayerSlot.P1`                                                                                                                                                                   |
| `MainPack/Scripts/Debug/` 烟测中调用 `InputSystem` 上述方法的地方                                   | 补 `PlayerSlot.P1`；用 `Input.ActionPress(InputSystem.X)` 注入按键的地方**不改**（仍是 P1 基名，正确）                                                                       |

### C4-4　出口按席位交互

`Entity/Level/Exit/LevelExitNode.cs` 当前：`_UnhandledInput` 只看 P1 的 `Interact`，`FindEligibleHero` 取第一个重叠英雄，再用 `Godot.Input` 直接查 P1 的组合键。改为：

- 删除 `FindEligibleHero` 的"取第一个"语义，改为在 `_UnhandledInput` 里遍历 `GetOverlappingBodies()` 中 `AbstractHeroEntity { IsAlive: true, OnFloor: true }` 的每个英雄：
  - 只有 `input.IsActionPressed(InputSystem.ActionOf(InputSystem.Interact, hero.Slot))` 且非回显时，才考虑该英雄；
  - 组合键排除改为读**同一席位**的 `Attack`/`Jump`/`MoveDown`：`Godot.Input.IsActionPressed(InputSystem.ActionOf(…, hero.Slot))`；
  - 命中后 `SetInputAsHandled()` 并 `Entered?.Invoke(hero)`，返回。
- `_PhysicsProcess` 的提示显示改为"存在任一重叠的存活落地英雄"，语义不变。
- 不改 `Enable`/`Disable`。`LevelViewController.OnExitContact` 的 `ReferenceEquals(hero, _player)` 本批保留（D2 改集合）。

### C4-5　战斗界面按席位请求开窗

- `UI/BattleForm/BattleForm.Logic.cs`：
  - `InventoryRequested`、`SkillsRequested` 从 `Action<int>` 改为 `Action<int, PlayerSlot>`（序列号, 请求席位）；`SettingsRequested` 不变（Esc 共用）。
  - `_UnhandledInput`：对 `PlayerSlot.P1`、`PlayerSlot.P2` 依次判断 `ActionOf(InventoryToggle, slot)` 与 `ActionOf(SkillToggle, slot)`；命中后以该席位发出事件。**只对阵容中存在的席位响应**：`slot` 超出 `ProfileModel.HeroIds.Count` 时忽略（单人下 P2 键无效）。
  - 现有 `ControlsEnabled` 守卫改为检查**请求者席位对应英雄**的状态；本批 BattleForm 只持有 P1 实体，所以 P2 请求在本批一律因"阵容无此席位"被忽略，守卫照旧读 `_formData.Hero`。
  - HUD 按钮点击（`OnInventoryClicked`/`OnSkillsClicked`）以 `_formData.Hero.Slot` 发出事件。
- `MainPack/Scripts/Procedure/ProcedureLevel.cs`：
  - `OnInventoryRequested(int formSerialId, PlayerSlot slot)`、`OnSkillsRequested(int formSerialId, PlayerSlot slot)`。
  - 由席位找英雄：本批只有 `_hero`，要求 `_hero.Slot == slot`，否则返回。D2 改为按席位查集合。
  - **死者不能开背包**：`OnInventoryRequested` 增加 `!_hero.IsAlive` 时返回（官方 `RoleInfo.showBackPack` 提示"角色已经死亡"；本批不加提示文本，D3 再接 HUD 提示）。`OnSkillsRequested` 已有该判断，保持。
  - `SetFormCallbacks` 中事件订阅的方法签名同步。
- `HasWindow` 三窗互斥**不改**。

### C4-6　焦点导航冲突的确认

- `ui_left/right/up/down` 里同时绑了方向键与 WASD，P2 的方向键会驱动 UI 焦点。**本批不改 `ui_*`**。
- 但需实测确认：游戏进行中（无窗口打开）按方向键不会让 HUD 按钮获得焦点并被 `ui_accept` 误触发。`BattleMenuPanel` 的按钮若 `focus_mode` 允许获取焦点，实测有冲突就**停下报告**，不要自行改场景。

### C4 完成判定

- 构建、三套测试全绿且数量不减；Godot 扫描、引用检查、`--smoketest=frontend`、`--smoketest=ui` 通过。
- 全仓搜索：`InputSystem` 的 `IsActionPressed/IsActionJustPressed/IsActionJustReleased/ConsumePress` 每处调用都带席位；`LevelExitNode` 中不再直接用 P1 常量调用 `Godot.Input`。
- `project.godot` 的 13 个新动作在 Godot 编辑器中可见且键位正确（Godot 扫描后用编辑器或脚本读取 `InputMap.ActionGetEvents` 核对，结果写进交付说明）。
- 单人下 P1 全部操作不变；按 P2 键无任何反应。

## 大任务 C5　阶段 C 回归

执行第一部分第 5 节全部命令，加 `--smoketest=frontend`、`--smoketest=ui`、Godot 扫描、`validate_game_references.py`，并实机跑一次花果山。确认：

- P1 移动、攻击、技能、无双、开窗、出口、拾取、经验、球数量与阶段 B 一致；
- P2 映射存在但无人使用；
- 唯一行为变化（无英雄命中过的怪不发经验与球）符合预期；
- 第十部分占位清单中标为 C 阶段的项全部清零。

---

# 第七部分　阶段 D 与 E：一次交付的完整规格

## 0. 本阶段怎么执行（2026-11-10 用户要求）

用户要求把剩余计划**一次全部写好、一次全部交给执行者做完**。为了在一次交付中仍保持可审查、不偏移，规则如下：

1. **执行顺序固定**：D0 → D1 → D2 → D3 → E1，前一个大任务的「完成判定」全部满足后才开始下一个。不得交错、不得并行、不得跳过。
2. **每个大任务完成后不停下等用户，但必须：**
   - 跑完该任务的「完成判定」全部命令；
   - 按该任务的「上手验证」**用真实键盘实机走一遍**并截图（存到 `Godot/GodotProject/.godot/validation/coop_<任务>_<日期>/`）；
   - 在 `Docs/Validation.md` 顶部追加该任务的一节；
   - 然后才进入下一个大任务。
3. **只有以下两种情况允许停下等用户**（其余一律按本规格自行完成）：
   - **D0-3 正式转存前**（必停，等用户回复"确认"）；
   - 触发第一部分第 2 节七类上报条件之一。停下时写明卡在哪个子任务、现状、可选方案；**已完成的大任务保持完成，不回滚**。
4. **最终交付**：E1 完成后，输出一份总交付说明（格式见本部分第 8 节），并更新第三部分总表与第八部分进度。
5. 本部分写明的签名、文件、节点名、数值是**确定决定**，不是建议。实际代码与本规格冲突时按第一部分第 2 节停下报告，不自行取舍。

### 本阶段的硬性规则（在第一部分规则之外追加）

- **执行者烟测**继续用 `run_godot.py` 的案例专属 APPDATA。**上手验证**用正式 APPDATA（即用户自己的存档目录）。
- 本阶段**不改存档格式**（v14 已足够）。若发现必须改，停下报告。
- 除 D0-3 外，不得删除、覆盖或手动编辑 `%APPDATA%/Godot/app_userdata/GameZMXY/GameData/` 下的任何文件。上手验证中新建/覆盖的存档只能写到**第 5、6 格**（当前空格），不得覆盖用户已有的第 1～4 格。
- 场景改动只能通过 Godot 编辑器语义等价的方式（直接编辑 `.tscn` 文本可以，但必须保持 Godot 能无改动重新保存：节点顺序、属性名、`unique_id` 规则与同文件现有写法一致）。改完用 Godot 扫描验证能加载。
- UI 绑定文件（`XxxForm.cs`）只能用 `python Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace GameLogic.UI` 生成。**做法**：先在 `.tscn` 根节点的 `node_paths` 与 `_field = NodePath("...")` 中加好新绑定、删掉被删节点的绑定，再运行生成器（生成器从根节点绑定读取字段名，已核实不会改动既有字段名；绑定指向不存在的节点时会报 `Missing root binding` 并退出）。不用 `--add-binding`。
- 新增 UI 按钮一律复用同场景中已有按钮的样式（同类型、同主题、同字体、同 `focus_mode = 0`、同 `mouse_default_cursor_shape = 2`），不新导美术；**唯一例外**是 E1-1 的赠送按钮可用态贴图，从官方 SWF 导出。
- 所有新按钮点击都播放 `SoundId.MenuConfirm`（项目只有这一个界面音效，返回/取消也用它，与现有 SaveForm 关闭键一致）。

### 本阶段大任务总览

| 编号 | 大任务                                       | 合并自     | 完成后用户能看到                       |
| ---- | -------------------------------------------- | ---------- | -------------------------------------- |
| D0   | 正式存档升级到 v14                           | 新增       | 4 个"存档不可读"恢复显示角色名并能进入 |
| D1   | 标题、单双人、依次选角、选格建档             | 原 D1      | 能建出双人档；读档入口独立             |
| D2   | 两人同时出现、各自操作、各自 HUD             | 原 D2 + D3 | 双人进关，两角色、两块 HUD，各打各的   |
| D3   | 相机、死亡、出口、结算、唐僧奶队友、龙王分档 | 原 D4～D7  | 完整双人关卡规则                       |
| E1   | 赠送、技能页签、全关核验与收尾               | 原 E1～E3  | 赠送与页签可用，全部关卡双人可通       |

---

## 1. 大任务 D0　正式存档升级到 v14

### 背景（审查者已核实）

用户正式存档 `%APPDATA%/Godot/app_userdata/GameZMXY/GameData/` 有 4 格，**全部 v12**，因而全部显示"存档不可读"：

| 格 | UnitId     | 内容                                                                          |
| -- | ---------- | ----------------------------------------------------------------------------- |
| 1  | 1791793987 | 悟空，最远通关 9，灵魂 2630135，59 件装备，4 件穿戴                           |
| 2  | 1793209553 | 悟空，新档，0 件装备                                                          |
| 3  | 1793449001 | 龙王测试档，规模同格 1                                                        |
| 4  | 1793888918 | 唐僧，最远通关 5，灵魂 225080，27 件装备，3 件穿戴，5 个主动技能，被动全 5 级 |

v12 → v14 差异只有三处，且全部可无损推导：

| v12                     | v14                                                                                                                         |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| 根级 `"ActiveHeroId"` | 删除                                                                                                                        |
| 根级 `"Soul"`         | 删除，值写入唯一英雄的 `"Soul"`                                                                                           |
| （无）                  | 唯一英雄新增 `"BagInstanceIds"` = `EquipmentItems` 中**未被**该英雄四个穿戴字段引用的全部实例编号，保持实例池顺序 |

其余字段原样，`"SaveVersion"` 改为 `14`。4 个档的阵容都只有 1 人，不存在归属歧义。

### D0-1　转存工具

新建 `Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py`，纯 Python 标准库，模块开头英文 docstring 说明用途（与同目录其他脚本一致）。

**参数：**

| 参数            | 默认                                               | 说明                                                                                                                 |
| --------------- | -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `--game-data` | `%APPDATA%/Godot/app_userdata/GameZMXY/GameData` | 存档目录                                                                                                             |
| `--capacity`  | 必填                                               | 每人背包上限。用 `75`（`TbProfileConfig.EquipmentCapacity`，见 `Configs/GameConfig/Datas/ProfileConfig.xlsx`） |
| `--dry-run`   | 否                                                 | 只打印计划，不写文件、不建备份                                                                                       |

**行为：**

1. 列出 `Data/*.sav` 与 `Data/*.sav.bak`（两类都处理：GGF 主文件读失败会回退备份，两者版本必须一致）。`Catalogue.sav` 不改。
2. 读取：`open(path, encoding='utf-8-sig')` + `json.load`。记录原文件是否带 BOM（前三字节 `EF BB BF`）。
3. 按 `SaveVersion` 分流：`12` 转换；`13`、`14` 跳过并打印 `SKIP`；其他值打印 `ERROR` 并让脚本以非零退出码结束（不写任何文件）。
4. 转换前校验，任一失败即 `ERROR`：
   - `Heroes` 恰好 1 项；
   - `ActiveHeroId == Heroes[0]["HeroId"]`；
   - 四个穿戴编号中非 `00000000-0000-0000-0000-000000000000` 的都存在于 `EquipmentItems`；
   - 实例池内 `InstanceId` 不重复；
   - 计算出的背包件数 ≤ `--capacity`。
5. 构造新对象，**键顺序**严格为：`SaveVersion, Heroes, HighestClearedLevelId, EastSeaBannerAvailable, EquipmentItems, UnitId`；英雄内键顺序为：`HeroId, TotalExperience, Soul, BagInstanceIds, LearnedSkills, PassiveLevels, WeaponInstanceId, ArmorInstanceId, AccessoryInstanceId, TreasureInstanceId`（与 `GameData.cs` 声明顺序一致）。用 `dict` 按此顺序插入。
6. 写出：`json.dumps(obj, ensure_ascii=False, indent=2)`，换行统一为 `\r\n`，原文件带 BOM 则写回 BOM。先写 `<file>.tmp` 再 `os.replace`。
7. **任一文件 ERROR 时整个运行不写任何文件**：先对全部文件做完校验与构造，全部通过后再统一写。
8. 非 dry-run 时，写入前把整个 `GameData` 目录用 `shutil.copytree` 复制到同级 `GameData.backup-before-v14-<yyyyMMdd_HHmmss>`（用户目录已有 `GameData.backup-before-v9-…` 等同名先例）。
9. 每个文件打印一行：`<OK|SKIP|ERROR> <文件名> version=<旧> hero=<HeroId> soul=<n> bag=<n> worn=<n> <原因>`；结尾打印汇总与备份路径。

### D0-2　样例与往返测试

- 把正式格 4 的 `Data/1793888918.sav` 复制为 `Tests/ProfileTests/Data/save_v12_tangseng.json`（用户自己的数据，可直接使用）。
- 用 D0-1 脚本对一个只含该样例的临时目录运行，把输出复制为 `Tests/ProfileTests/Data/save_v14_tangseng.json`。两份都加入仓库（不提交，由用户提交）。
- `Tests/ProfileTests/ProfileTests.csproj`：照第 85 行把 bytes 拷到输出目录的写法新增一行：`<None Include="Data\*.json" CopyToOutputDirectory="PreserveNewest" />`（样例文件放在测试工程自己的 `Data` 目录，输出路径与 bytes 相同为 `Data\`）。
- 新建 `Tests/ProfileTests/SaveUpgradeTests.cs`（命名空间与同目录测试一致，中文 XML 注释），一个用例：
  1. 读 v14 样例为 `GameData`（`JsonConvert.DeserializeObject<GameData>`，与 GGF 同一 Newtonsoft）。
  2. `TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, TestTables.Tables.TbProfileConfig.EquipmentCapacity)` 成功。
  3. `CaptureSaveData()` 后断言：英雄编号与经验、灵魂、背包实例集合（集合比较，不比顺序）、四个穿戴编号、已学技能集合、被动字典、`HighestClearedLevelId`、`EastSeaBannerAvailable`、实例池实例集合与样例一致。
  4. 再断言 v12 样例的 `Soul` 等于 v14 样例英雄的 `Soul`，v12 的实例池件数 = v14 背包件数 + 非空穿戴件数（证明转换规则）。

### D0-3　正式转存（必停）

1. 运行 `python Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py --capacity 75 --dry-run`，把完整输出贴进交付说明，**停下等用户回复"确认"**。
2. 用户确认后去掉 `--dry-run` 正式运行，记录输出与备份路径。
3. 做上手验证。

### D0 上手验证

1. 打开游戏 → 开始游戏 → 存档页：格 1/2/3 显示"悟空"，格 4 显示"唐僧"，无"存档不可读"。截图。
2. 点格 4 → 地图 → 打开技能窗口：5 个主动技能已学、被动 5 级、灵魂 225080。截图。
3. 进任一关按 C：装备件数与穿戴与转存前一致（27 件中 3 件穿戴、24 件在背包）。
4. 退出再开，4 格仍可读。

### D0 完成判定

- 构建 0 错误；ProfileTests ≥ 324（新增 1 个以上），BattleTests 197，LevelTests 19。
- 上手验证 1～4 通过并截图。
- 运行时代码零改动（`git diff --stat` 中 `Godot/GodotProject` 下无 `.cs` 改动）。

---

## 2. 大任务 D1　标题、单双人、依次选角、选格建档

### 目标流程（依据造三 `GameMenu`、`SelectRole`、`SaveInter`、`MapMenu`）

```
标题：新开游戏 / 读取存档 / 游戏设置 / 游戏帮助 / 游戏须知
├─ 新开游戏 → 主菜单按钮隐藏，原位出现：单人游戏 / 双人游戏 / 返回
│   ├─ 返回 → 恢复主菜单
│   └─ 单人/双人 → ProcedureCharacter（选角）
│        ├─ 返回 → 回标题
│        └─ 选满 → 本流程内打开存档页（保存模式："第一次进入游戏请选择存档"）
│             ├─ 空格 → 建档 → ProcedureLoadGame → 地图
│             ├─ 已有格 → 确认覆盖 → 删除旧档 → 建档 → 地图
│             └─ 关闭 → 回标题（不写档）
└─ 读取存档 → 存档页（读取模式）→ 只能点已有格 → ProcedureLoadGame → 地图
```

### D1-1　流程数据：阵容随选择传递

`TheGame/MainPack/Scripts/Procedure/FlowData/SaveSelectionVariable.cs`：

```csharp
public sealed class SaveSelectionVariable : Variable<(int SlotNumber, long UnitId, int[] HeroIds)>
{
    /// <param name="heroIds">新档按席位排列的阵容；读已有档时为空数组。</param>
    public static SaveSelectionVariable Create(int slotNumber, long unitId, int[] heroIds)
```

- 删除旧的 `int heroId = 0` 参数，**不留默认值**。读档调用传 `[]`。
- 更新 XML 注释与类说明（"标题和选角向读档流程传递格号、稳定存档编号与新档阵容"）。
- 全部使用点同步：`ProcedureTitle`、`ProcedureCharacter`、`ProcedureLoadGame`。

`ProcedureLoadGame`：

- 解构改为 `(int SlotNumber, long UnitId, int[] HeroIds) selection`，调用 `LoadOrCreateAsync(selection.SlotNumber, selection.UnitId, selection.HeroIds, token)`。
- 读档日志改为：`Log.Info("[ProcedureLoadGame] 档案就绪：来源 {0}，阵容 {1}", loaded.Origin, string.Join(",", profile.HeroIds));`（清掉第九部分占位清单中的一项）。

`SaveSystem.LoadOrCreateAsync` 新签名：

```csharp
public async Task<(GameData Data, SaveOrigin Origin)> LoadOrCreateAsync(int slotNumber, long unitId,
    IReadOnlyList<int> initialHeroIds, CancellationToken cancellationToken = default)
```

- 校验：`initialHeroIds` 非 null；建档时（`unitId == 0`）若为空则用 `[TbProfileConfig.StartHeroId]`（保留原单参数时的默认行为，供 Debug 夹具使用）；每个编号必须在 `TbHeroConfig` 中存在，否则抛 `ArgumentOutOfRangeException`。数量与重复由后续 `mapper.Validate` 拒绝，不重复写。
- 建档：`Heroes = initialHeroIds.Select(id => new HeroSaveData { HeroId = id, Soul = tables.TbProfileConfig.StartSoul }).ToList()`。
- 读档（`unitId != 0`）忽略 `initialHeroIds`。
- 更新 XML 注释。

### D1-2　标题：新开游戏 / 读取存档 / 单双人

**场景 `TheGame/UIs/start_form.tscn`**（按钮 45px 高、62px 间距，分隔线在两按钮之间，起点比上一按钮底部早 2px）：

| 节点           | 类型        | 偏移 L,T,R,B    | 文本     | 说明                        |
| -------------- | ----------- | --------------- | -------- | --------------------------- |
| `m_NewGame`  | Button      | 692,160,933,205 | 新开游戏 | 原 `m_Start` 改名并改文本 |
| Divider0       | TextureRect | 692,203,933,224 |          | 保留                        |
| `m_LoadGame` | Button      | 692,222,933,267 | 读取存档 | 新增                        |
| Divider1       | TextureRect | 692,265,933,286 |          | 保留                        |
| `m_Settings` | Button      | 692,284,933,329 | 游戏设置 | 下移一格                    |
| Divider2       | TextureRect | 692,327,933,348 |          | 保留                        |
| `m_Help`     | Button      | 692,346,933,391 | 游戏帮助 | 下移一格                    |
| Divider3       | TextureRect | 692,389,933,410 |          | 保留                        |
| `m_Notice`   | Button      | 692,408,933,453 | 游戏须知 | 下移一格                    |
| Divider4       | TextureRect | 692,451,933,472 |          | 新增，复制 Divider3         |
| `m_Status`   | Label       | 678,475,932,575 |          | 下移 50px，避免与新按钮重叠 |
| `m_Single`   | Button      | 692,160,933,205 | 单人游戏 | 新增，`visible = false`   |
| `m_Double`   | Button      | 692,222,933,267 | 双人游戏 | 新增，`visible = false`   |
| `m_Back`     | Button      | 692,284,933,329 | 返回     | 新增，`visible = false`   |

- 新按钮属性复制 `m_Settings`：`mouse_default_cursor_shape = 2`、`focus_mode = 0`。
- 三个新选项按钮出现时，主菜单 5 个按钮和 5 条分隔线都隐藏。为便于整组切换，把主菜单 5 个按钮和 5 条分隔线移入一个新 `Control` 节点 `m_MainMenu`（`mouse_filter = 2`，全屏偏移 0,0,940,590，子节点保持原绝对偏移），把三个选项按钮移入 `m_PlayerCountMenu`（同样设置，`visible = false`）。这样逻辑只切换两个容器的 `Visible`。
- 根节点绑定改为：`_newGame, _loadGame, _settings, _help, _notice, _status, _mainMenu, _playerCountMenu, _single, _double, _back`，路径按新层级写（如 `m_MainMenu/m_NewGame`）。删除 `_start`。
- 运行 `regenerate_ui_form.py` 生成 `StartForm.cs`。

**`StartForm.Logic.cs`：**

- 删除 `StartGameRequested`。新增：

```csharp
/// <summary>玩家选定人数开始新游戏；参数为本次窗口编号与参战人数（1 或 2）。</summary>
public event Action<int, int> NewGameRequested;

/// <summary>玩家请求读取已有存档；参数为本次窗口编号。</summary>
public event Action<int> LoadGameRequested;
```

- `OnInit`（`isNewInstance`）绑定：`_newGame → ShowPlayerCountMenu`、`_loadGame → OnLoadGameClicked`、`_single → () => OnPlayerCountChosen(1)`、`_double → () => OnPlayerCountChosen(2)`、`_back → ShowMainMenu`。
- `OnOpen`：`_status.Text = ""; ShowMainMenu(playSound: false); Visible = true;`（每次打开都回到主菜单）。
- `OnClose`：两个新事件置 null（与现有写法一致）。
- `ShowPlayerCountMenu`/`ShowMainMenu`：守卫 `GF.UI.HasUIForm(SerialId)`，播放 `MenuConfirm`（`ShowMainMenu` 在 `OnOpen` 调用时不播），切换两个容器的 `Visible`，清空 `_status.Text`。
- `OnPlayerCountChosen(int count)`：守卫、音效、`NewGameRequested?.Invoke(SerialId, count)`。
- `OnLoadGameClicked`：守卫、音效、`LoadGameRequested?.Invoke(SerialId)`。
- 帮助文本改为三行：
  `"P1：A/D 移动 · J 攻击 · K 跳跃 · 空格 无双\nP2：←/→ 移动 · 小键盘1 攻击 · 2 跳跃 · 0 无双\n左下角设置中返回地图"`
- 须知文本不变。

**`ProcedureTitle.cs`：**

- `SetFormCallbacks` 的 `StartForm` 分支改订阅 `NewGameRequested`、`LoadGameRequested`（删除 `StartGameRequested`）。
- 新增 `OnNewGameRequested(int formSerialId, int playerCount)`：校验序列号与 `playerCount is 1 or 2`；`_owner.SetData(FlowKeys.SaveSelection, SaveSelectionVariable.Create(0, 0, new int[playerCount]))`（用数组长度携带人数，元素尚未选择为 0）；`ChangeState<ProcedureCharacter>(_owner)`。
- 原 `OnStartGameRequested` 改名 `OnLoadGameRequested`，逻辑不变，但打开存档页时传 `new SaveFormData(SaveFormMode.Load)`（见 D1-4）。
- `OnSaveSlotSelected`：只接受 `SaveSlotState.Occupied`（原来还接受 Empty 并转去选角，删除该分支）；写入 `SaveSelectionVariable.Create(slot.SlotNumber, slot.UnitId, [])`，切 `ProcedureLoadGame`。
- `OnSaveSlotDeleteRequested` 不变。

### D1-3　选角：依次 1P/2P、已选锁定、重选、返回

**场景 `TheGame/UIs/character_form.tscn`：**

- 新增 `m_PlayerTwo`：复制 `m_PlayerOne` 全部属性，文本 `"2P"`，`font_color = Color(0.35, 0.75, 1, 1)`（与 1P 的金色区分），`visible = false`。
- 新增 `m_Reselect`（Button，文本"重选"）与 `m_Back`（Button，文本"返回"）：
  - 卡片占满全高，没有空白区，所以放在**八戒/沙僧两张未开放卡片区域的底部**：`m_Reselect` 偏移 `500,520,640,565`，`m_Back` 偏移 `760,520,900,565`。
  - 主题沿用根节点 `legacy_menu_theme.tres`（Button 会自动套用），`focus_mode = 0`，`mouse_default_cursor_shape = 2`，`m_Reselect` 默认 `visible = false`。
- 根绑定追加 `_playerTwo, _reselect, _back`，运行生成器。

**新建 `GameScripts/UI/CharacterForm/CharacterFormData.cs`**（照 `InventoryFormData` 写法）：

```csharp
/// <summary>选角窗口的打开参数：本次需要选择的参战人数。</summary>
public sealed class CharacterFormData
{
    /// <param name="playerCount">参战人数，只能为 1 或 2。</param>
    /// <exception cref="ArgumentOutOfRangeException">人数不是 1 或 2。</exception>
    public CharacterFormData(int playerCount)
    /// <summary>本次需要依次确认的英雄数量。</summary>
    public int PlayerCount { get; }
}
```

**`CharacterForm.Logic.cs`：**

- 字段：`private int _playerCount; // 本次需要确认的人数。`、`private readonly List<int> _chosenHeroIds = new(); // 已按席位确认的英雄。`
- 事件改为：

```csharp
/// <summary>全部席位选定；参数为窗口编号与按席位排列的英雄编号。</summary>
public event Action<int, int[]> CharacterConfirmed;

/// <summary>玩家放弃选角返回标题；参数为窗口编号。</summary>
public event Action<int> CharacterCancelled;
```

- `OnInit`：追加 `_reselect.Pressed += OnReselectClicked; _back.Pressed += OnBackClicked;`。
- `OnOpen(object userData)`：`userData is not CharacterFormData data` 时抛 `ArgumentException`；`_playerCount = data.PlayerCount; _chosenHeroIds.Clear(); _reselect.Visible = _playerCount == 2; RefreshCards(); Visible = true;`
- `OnClose`/`OnRecycle`：两个事件置 null；清空 `_chosenHeroIds`。
- `ConfirmCharacter(int heroId)`（点卡）：
  1. 守卫 `HasUIForm`；`_chosenHeroIds.Contains(heroId)` 时直接返回（官方 `selectOneInDouble` 拒绝已按下卡，不播音效）。
  2. 播 `MenuConfirm`，`_chosenHeroIds.Add(heroId)`，`RefreshCards()`。
  3. `_chosenHeroIds.Count == _playerCount` 时 `CharacterConfirmed?.Invoke(SerialId, _chosenHeroIds.ToArray())`。
- `RefreshCards()`：每张卡 `grayscale = _chosenHeroIds.Contains(卡的英雄) ? 0 : 1`；`_playerOne` 放在第 1 个已选卡上方（沿用 `PreviewRole` 的定位算式），`_playerTwo` 放在第 2 个已选卡上方；未选时隐藏对应标签。
- 悬停预览：`PreviewRole` 只在该卡**未被选中**时生效，标签显示为"下一个待选席位"（第 1 次悬停显示 1P，选了 1P 后悬停显示 2P）；`ResetRoleHover` 改为调用 `RefreshCards()`（不能把已选卡变回灰色）。
- `OnReselectClicked`：守卫、音效、`_chosenHeroIds.Clear()`、`RefreshCards()`。
- `OnBackClicked`：守卫、音效、`CharacterCancelled?.Invoke(SerialId)`。
- 卡片与英雄的对应只有现有两张（悟空 1、唐僧 2），用现有两个常量；**不写**"悟空+唐僧"等组合分支。

**`ProcedureCharacter.cs`：**

- `OnEnter`：从 `FlowKeys.SaveSelection` 读人数（`HeroIds.Length`），`GF.UI.OpenUIForm(UIFormId.CharacterForm, new CharacterFormData(count))`。
- 新增字段 `private CancellationTokenSource _cancellation; // 离开时使存档目录读取与删除失效。`、`private int _saveSerialId; // 本流程打开的保存模式存档页。`；`OnEnter` 创建、`OnLeave` 取消并释放，`OnLeave` 关闭在开或加载中的存档页（照 `ProcedureTitle.OnLeave` 写法）。
- `OnCharacterConfirmed(int formSerialId, int[] heroIds)`：校验序列号；把阵容写入 `SaveSelection`（`selection.Value = (0, 0, heroIds)`）；打开存档页 `GF.UI.OpenUIForm(UIFormId.SaveForm, new SaveFormData(SaveFormMode.Save))`，绑定回调，`await saves.RefreshDirectoryAsync(token)`（方法改为 `async void`，照 `ProcedureTitle.OnStartGameRequested`）。选角窗口保持打开在下层。
- `OnCharacterCancelled`：`ChangeState<ProcedureTitle>(_owner)`（标题 `OnEnter` 会清除 `SaveSelection`）。
- 保存模式选格处理见 D1-4。
- `SetFormCallbacks` 增加 `CharacterCancelled` 与 `SaveForm` 分支（`SaveSlotSelected`、`SaveFormClosed`，见 D1-4）。

### D1-4　存档页的读取/保存两种模式与覆盖确认

**新建 `GameScripts/UI/SaveForm/SaveFormData.cs`：**

```csharp
/// <summary>存档页用途。</summary>
public enum SaveFormMode
{
    /// <summary>读取已有存档；空格不可选，可删除。</summary>
    Load,
    /// <summary>为新阵容选择存档格；已有格需确认覆盖，不提供删除。</summary>
    Save
}

/// <summary>存档页打开参数。</summary>
public sealed class SaveFormData
{
    public SaveFormData(SaveFormMode mode)
    /// <summary>本次打开的用途。</summary>
    public SaveFormMode Mode { get; }
}
```

**`SaveForm.Logic.cs`：**

- 字段 `private SaveFormMode _mode; // 本次打开的用途。`、`private bool _confirmingOverwrite; // 确认层当前用于覆盖而非删除。`
- 新增事件 `public event Action<int> SaveFormClosed;`（窗口编号）。`OnCloseClicked` 在 `CloseUIForm` **之前** `SaveFormClosed?.Invoke(SerialId)`。`OnClose` 置 null。标题流程不订阅它（关闭后标题仍在下层，行为不变）。
- `OnOpen(object userData)`：`userData is not SaveFormData data` 时抛 `ArgumentException`；记录 `_mode`；保存模式下 `_status.Text` 由刷新逻辑设为 `"第一次进入游戏请选择存档"`（目录就绪时）。
- `RefreshDirectory` 按模式：
  - 读取模式：`button.Disabled = slot == null || slot.State != SaveSlotState.Occupied`（空格与不可读都禁用）；删除按钮规则不变。
  - 保存模式：`button.Disabled = slot == null || slot.State == SaveSlotState.Unreadable`；删除按钮全部隐藏禁用；目录就绪时 `_status.Text = "第一次进入游戏请选择存档"`（失败/加载中文案不变）。
- `SelectSlot(int number)`：
  - 读取模式：只接受 `Occupied`，其余同现在。
  - 保存模式：`Empty` 直接 `SaveSlotSelected`；`Occupied` 时复用确认层：`_confirmingOverwrite = true; _deleteSlot = number; _deleteMessage.Text = $"确定覆盖第 {number} 个存档吗？\n覆盖后原存档无法恢复。"; _deleteConfirmation.Show(); _deleteCancel.GrabFocus();`
- `ConfirmDelete`：若 `_confirmingOverwrite`，发 `SaveSlotSelected(SerialId, slot)`；否则保持原删除逻辑。`CancelDelete` 复位 `_confirmingOverwrite = false`。
- 复用 `DeleteConfirmation` 节点与美术（取消/确定按钮图本身无文字，适用于覆盖）。

**`ProcedureCharacter` 处理保存模式选格（`async void OnSaveSlotSelected(int formSerialId, int slotNumber)`）：**

1. 校验序列号、目录就绪、格号范围；格为 `Unreadable` 直接返回。
2. 若为 `Occupied`：`bool deleted = await saves.DeleteSlotAsync(slot.SlotNumber, slot.UnitId, token);` 失败直接返回（`SaveSystem` 已把目录设为失败文案并显示在存档页）。成功后重新从 `SaveModel.Slots[slotNumber - 1]` 读取该格（删除后会刷新目录，该格变为 Empty，`UnitId` 可能为 0 或框架空槽编号）。
3. `selection.Value = (slot.SlotNumber, slot.UnitId, heroIds)`（`UnitId` 取刷新后的值），`ChangeState<ProcedureLoadGame>(_owner)`。`LoadOrCreateAsync` 现有逻辑已处理 `unitId == 0` 新建与"框架空槽认领"两种情况。
4. 取消异常照 `ProcedureTitle.OnSaveSlotDeleteRequested` 写法吞掉。

**`ProcedureCharacter.OnSaveFormClosed`**：`ChangeState<ProcedureTitle>(_owner)`。

### D1-5　Debug 烟测同步

- `MainPack/Scripts/Debug/FrontendSmokeScenario.cs`：
  - `AdvanceToLevel` 的按钮映射：`StartForm` → 先点 `m_MainMenu/m_NewGame`，再点 `m_PlayerCountMenu/m_Single`（双人路径点 `m_Double`）；`CharacterForm` 点卡（双人点两张）；`SaveForm`（保存模式）点 `Slots/m_Slot1`。注意新流程是"选角在选格之前"，按当前打开的窗口类型判断下一步。
  - `RunAsync` 主流程中 `m_Start` → 读取存档路径改为 `m_MainMenu/m_LoadGame`；新档路径改为新开游戏 → 单人 → 选角 → 存档页保存模式选空格。
  - 新增覆盖：①新开游戏 → 返回 → 主菜单恢复；②双人选角：同卡第二次点击无效、重选清空、返回回标题；③保存模式点已有格出现覆盖确认，取消不改目录，确认后该格被新阵容覆盖；④保存模式关闭回标题且目录不变；⑤读取模式空格不可点；⑥建双人档后读取模式格名为"悟空 · 唐僧"。
- 其他引用 `StartGameRequested`、`m_Start`、`CharacterConfirmed(int,int)`、`LoadOrCreateAsync(..., heroId)` 的 Debug 位置按新签名改（以编译错误为准逐一修）。

### D1 上手验证（正式存档；新档只写第 5、6 格）

1. 标题右侧：新开游戏 / 读取存档 / 游戏设置 / 游戏帮助 / 游戏须知。截图。
2. 新开游戏 → 原位出现单人游戏 / 双人游戏 / 返回；返回 → 恢复。
3. 双人游戏 → 点悟空：卡亮起、上方 1P；再点悟空无反应；点重选两卡变灰；点悟空、点唐僧（唐僧上方 2P）→ 存档页出现，状态栏"第一次进入游戏请选择存档"，各格无删除按钮。截图。
4. 点第 1 格（已有）→ 出现"确定覆盖第 1 个存档吗？"→ 点取消（**不要确认**，保护用户存档）。
5. 点第 5 格（空）→ 进地图。
6. 返回标题 → 读取存档：第 5 格显示"悟空 · 唐僧"；第 6 格（空）不可点；点第 5 格进地图。截图。
7. 新开游戏 → 单人 → 唐僧 → 选第 6 格 → 地图。回标题读取第 6 格：显示"唐僧"。
8. 双人档进关**仍只出现 P1**（D2 才出现第二人），这是本批预期。

### D1 完成判定

- 构建 0 错误；三套测试全绿不减；Godot 扫描、`validate_game_references.py`、`--smoketest=frontend` 通过（含 D1-5 新增覆盖）。
- 上手验证 1～8 通过并截图；第 1～4 格未被修改（对比 D0 后的 `Catalogue.sav` 中 1～4 格 `SavedAtUtc` 不变）。

---

## 3. 大任务 D2　两人同时出现、各自操作、各自 HUD

### D2-1　ProcedureLevel 装配全部参战英雄

文件：`MainPack/Scripts/Procedure/ProcedureLevel.cs`。

- 字段 `private AbstractHeroEntity _hero;` 改为 `private readonly List<AbstractHeroEntity> _heroes = new(); // 本流程显示并拥有的参战英雄，按席位排列。`
- `OnEnter`：
  - 资源预载：`HeroConfig[] heroConfigs = profile.HeroIds.Select(tables.TbHeroConfig.Get).ToArray();`，对每个配置 `LoadAssetAsync`，与场景、HUD 一起 `Task.WhenAll`。
  - 显示：按席位顺序依次 `await entities.ShowEntityAsync<AbstractHeroEntity>(config.EntityId, new HeroSpawn(slot, SpawnPositionOf(slot)))`，每次 await 后检查 `entrySerial`；**过期则隐藏本周期已显示的全部英雄（包括刚返回的这个）后 return**。每个英雄同样做 `Config == null || !IsShown` 检查，失败抛异常走现有 catch（`CleanupSession` 会回收 `_heroes` 中已有的）。成功显示的英雄立刻加入 `_heroes`，保证失败路径能回收。
  - 出生点：新增私有方法

    ```csharp
    /// <summary>按席位计算出生点：P1 用关卡出生点，P2 在其右侧 60 像素同一高度。</summary>
    private Vector2 SpawnPositionOf(PlayerSlot slot) =>
        _levelViewController.PlayerSpawnPosition + new Vector2(slot == PlayerSlot.P1 ? 0 : 60, 0);
    ```
  - `new BattleFormData(_heroes, _levelViewController)`；`_levelViewController.StartSession(entities, _heroes, cancellationToken)`。
- `CleanupSession`：`foreach (AbstractHeroEntity hero in _heroes) GF.Entity.HideEntitySafe(hero); _heroes.Clear();`（在移除 HeroSystem 之前，与现顺序一致）。`OnLeave` 中 `_hero = null` 改为 `_heroes.Clear()`。
- 新增私有方法 `private AbstractHeroEntity HeroAt(PlayerSlot slot) => _heroes.FirstOrDefault(hero => hero.Slot == slot);`
- `OnInventoryRequested`/`OnSkillsRequested`：`AbstractHeroEntity hero = HeroAt(slot); if (hero == null || !hero.IsAlive) return;`，背包传 `new InventoryFormData(hero)`；技能窗口本批**仍不传 userData**（`SkillFormData` 在 E1-2 新建并同时接入，避免出现无人消费的参数）。

### D2-2　LevelViewController 持有参战集合

文件：`GameScripts/Entity/Level/LevelViewController.cs`、`.Loot.cs`、`.Entrance.cs`。

- 字段 `private AbstractHeroEntity _player;` 改为：

  ```csharp
  private readonly List<AbstractHeroEntity> _heroes = new(); // 借用流程显示并隐藏的参战英雄，按席位排列。
  private readonly Dictionary<AbstractHeroEntity, (Action<int> Restored, Action<EquippedItems> Equipment)> _heroHandlers = new(); // 每名英雄实际注册的回调，用于对称退订。
  ```
- `StartSession(EntityComponent entities, IReadOnlyList<AbstractHeroEntity> heroes, CancellationToken cancellationToken)`：

  - `_heroes.AddRange(heroes)`；逐个：`hero.Died += OnHeroDied;`；创建闭包 `Action<int> restored = amount => _floatingText.ShowRecovery(hero.GlobalPosition, amount, RecoveryKind.Health);`、`Action<EquippedItems> equipment = _ => hero.RefreshAppearance();`，注册 `hero.HealthRestored += restored`、`GetSlots(hero.HeroId).Register(equipment)`，存入 `_heroHandlers`。
  - 删除 `OnHeroHealthRestored`、`OnEquipmentChanged` 两个方法（由闭包取代）。
  - **相机本批仍跟 P1**：`_camera.Follow(_heroes[0])`（D3-1 改）。
- `UnsubscribeProgressEvents`：遍历 `_heroHandlers` 对称退订（`Died`、`HealthRestored`、`GetSlots(...).UnRegister`），死亡演出订阅见 D3-2（本批保持现逻辑但改为逐英雄：凡在本批 `OnHeroDied` 中订阅过 `DeathPresentationFinished` 的英雄都要退订）；然后 `_heroHandlers.Clear()`。
- `Cleanup`：`_player = null` 改为 `_heroes.Clear()`（在 `UnsubscribeProgressEvents` 之后）。
- 新增私有方法 `private bool IsSessionHero(AbstractHeroEntity hero) => hero != null && _heroes.Contains(hero);`，以下全部用它替换 `ReferenceEquals(..., _player)`：

  - `OwnsActor(int entityId) => _heroes.Exists(hero => hero.Id == entityId) || _spawner.OwnsEntity(entityId);`
  - `OnDamageDealt`：攻击者是任一参战英雄即计入共享连击（造三两人共用 `batterNum`）。
  - `OnTriggerContact`、`OnExitContact`、`OnEntranceBodyEntered`（`body is AbstractHeroEntity hero && IsSessionHero(hero) && hero.IsAlive`）。
  - `TryCollectGroundItem`、`OnEnemyDefeated` 的归属解析（`if (!IsSessionHero(killer)) killer = null;`）。
- `OnEnemyDefeated` 开头 `!_player.IsAlive` 改为只判断 `LevelModel.Outcome.Value != LevelOutcome.Running`（一人死亡不影响另一人结算与掉落；死者自己的球由 C3 的逐球归属逻辑自然作废）。
- `TickLoot`：开头 `!IsInstanceValid(_player) || !_player.IsAlive` 改为 `delta <= 0` 即返回；地面物品对 `_heroes` 中每个存活英雄按席位顺序 `item.TryCollect(hero)`，**一旦某人成功就不再让后面的人尝试该物品**（`TryCollect` 返回 true 时 break 内层循环）。
- `OnExitEntered` 中 `!_player.IsAlive` 改为"存在存活参战者"（`_heroes.Exists(hero => hero.IsAlive)`）。
- `EntranceOpacity` setter：对 `_heroes` 中每个有效英雄设置 `Modulate`。
- `OnEntranceBodyEntered`：任一参战存活英雄进入即触发，`SetControlsEnabled(false)` 对 `_heroes` 中**每个**英雄执行。
- **本批保留单人判负**：`OnHeroDied` 本批改为"任一参战者死亡即进入现有死亡演出流程"（行为与现在单人相同；双人下一人死就失败，D3-2 修正），并按死亡英雄订阅其 `DeathPresentationFinished`。`Complete` 的剩余生命本批取 `_heroes[0]`（D3-2 改平均）。

### D2-3　双方 HUD

**场景 `TheGame/UIs/battle_form.tscn`：**

1. 复制节点 `StatusPanel` 整棵子树为 `StatusPanelP2`，`position = Vector2(824, 44)`（940 − 116），`scale = Vector2(-1, 1)`。
2. 复制节点 `MenuPanel` 整棵子树为 `MenuPanelP2`，`position = Vector2(850, 531)`（940 − 90），`scale = Vector2(-1, 1)`。
3. **翻回需要可读的子节点**（在副本中设置 `scale.x = -1`，抵消父级镜像，使文字与图标正向）：
   - `StatusPanelP2`：`m_HpBar/m_HpText`、`m_MpBar/MpText`、`m_ExpBar/ExpText`、`m_LevelLabel`、`Head`、`StatusLabel`。Label 是 Control，用 `scale = Vector2(-1, 1)` 后其左上角原点会翻到另一侧，所以同时把 `pivot_offset` 设为自身尺寸中心（`size / 2`），使翻转围绕中心进行、位置不变。Sprite2D（`Head`、`StatusLabel`）直接 `scale = Vector2(-1, 1)`（Sprite2D 默认居中）。
   - `MenuPanelP2`：`SkillBar/Skill1..5/Icon`（TextureRect，同 Label 做法）、三个按钮各自的 `Caption`（Sprite2D）。
   - **血条/蓝条/经验条/无双条本身不翻回**：镜像后从右往左填充，与官方 P2 一致。
4. 根绑定追加 `_statusP2 = NodePath("StatusPanelP2")`、`_menuP2 = NodePath("MenuPanelP2")`，运行生成器。
5. **做完这 4 步立即截图**（编辑器或运行时双人档），写入交付说明后再继续 D2-4。若镜像后文字位置明显偏移或重叠，按上面 `pivot_offset` 规则调整；仍无法正确显示则停下报告，不改用其他布局方案。

**`BattleFormData`：**

```csharp
public BattleFormData(IReadOnlyList<AbstractHeroEntity> heroes, LevelViewController viewController)
/// <summary>本局参战英雄，按席位排列。</summary>
public IReadOnlyList<AbstractHeroEntity> Heroes { get; }
```

- 校验非空、1～2 人、无 null。删除旧的 `Hero` 属性。

**2026-11-11 用户批准的绑定职责调整**：原规格由Form持有私有席位绑定记录及逐人闭包；用户要求从架构上比较FormData、事件等方案后，明确授权“嗯可以的，你开始吧，做好记录给架构师看就行”。本段同步改为复用现有两个Panel，各实例绑定一名英雄；这项授权只调整D2 HUD实现，不解除D2正式键盘验收门槛，不推进D3。

**既有 `BattleStatusPanel` / `BattleMenuPanel`（不新增类型、节点或场景）：**

- 两个Panel实现现有 `IController`，只借用主流程Architecture和权威状态；不复制数值，不写入Model。
- `BattleStatusPanel.BindHero(int heroId)` / `UnbindHero()`：直接观察本人的Hp、MaxHp、Mp、MaxMp和成长来源；保存实际状态/成长来源引用，用具名 `RefreshResources` / `RefreshProgression` 对称订退。绑定立即刷新头像、资源、等级与经验。
- `BattleMenuPanel.BindHero(int heroId, PlayerSlot slot)` / `UnbindHero()`：直接观察本人的Ws、MaxWs、WsActive和已学技能来源；具名方法订退并初始化条体/技能。WsActive仅订阅变化，不发送初值，改绑已有开启态不能重播一次性演出。
- 菜单 `SettingsSelected` / `InventorySelected` / `SkillsSelected` 从 `Action` 改为 `Action<PlayerSlot>`；新增局部同步 `Action<int,bool> MusouStateChanged` 携带HeroId与开启态。沿用已有C#局部事件，不使用GF.Event.Fire排队，以保持无双立即暂停与同帧后到者拒绝的时序。
- Panel的Unbind只解除自己的订阅、释放借用来源；菜单同时停止满值动画。绑定失败后的部分订阅由Form既有catch统一清理。
- 删除不再被外部调用的旧被动设置入口，不保留平行绑定路径；无需修改FormData、绑定API、Model/System、生成绑定文件或场景。

**`BattleForm.Logic.cs`：**

- 删除私有席位记录、`_seats`、逐人闭包及其五类刷新方法；快捷键直接遍历 `BattleFormData.Heroes`，按请求者的ControlsEnabled处理。
- `OnOpen`：按英雄Slot选择既有左右状态/菜单Panel并调用BindHero；单人隐藏P2。共享TravelAvailable、ComboCount、FeaturedOpponentChanged只订阅一次。
- `OnInit`：两块菜单通过具名处理方法连接带席位的交互事件，以及带英雄编号的MusouStateChanged；只在新实例绑定一次。
- `OnMusouStateChanged(int heroId,bool active)`：沿用原ShowActivation/Reset处理，共享全屏立绘与暂停仍归Form。
- `Unbind` / `OnClose` / `OnRecycle`及失败清理：在Model移除前显式调用四块Panel的UnbindHero，并解除共享订阅和演出。GGF关窗不会把节点移出树，不能依赖ExitTree解绑。

### D2-4　Debug 与测试同步

- `TestArenaController`、`UiSmokeScenario.ShowHeroAsync` 及所有 `new BattleFormData(hero, …)` 调用改为传列表。
- `UiSmokeScenario` 新增一条**双人花果山**路径（参数 `--smoketest=ui --coop-only`，照现有 `--xxx-only` 选项写法）：用隔离存档建双人档进花果山，断言：两英雄都显示且位置相差 60px；两 HUD 头像分别为两英雄；P2 造成致死一击后 P2 经验增加、P1 不变；P2 背包键打开 P2 背包；关卡退出后 `CountBindingHandlers` 对两英雄的 `GetProgress`/`GetSlots` 都为 0。

### D2 上手验证（使用 D1 建的第 5 格双人档）

1. 进花果山：P1 在出生点，P2 在其右侧，两人都有站立动画。截图。
2. 左上 P1 HUD、右上 P2 HUD（镜像，文字正向可读，头像分别为两人）。截图。
3. P1 用 WASD/J/K，P2 用方向键/小键盘 1/2，**同时**移动、跳跃、攻击。
4. P1 用 U/I/O/L、P2 用小键盘 4/5/6/3 放技能，各扣各的蓝。
5. P2 打死一只猴子：P2 经验条涨、球飞向 P2。地上掉的物品谁碰到归谁。
6. P2 按小键盘 `/` 打开 P2 背包；P1 按 C 打开 P1 背包；点右上 P2 HUD 的背包按钮也打开 P2 背包。
7. P2 蓄满后按小键盘 0：显示 P2 立绘、短暂停顿后 P2 进入无双。
8. 单人档（第 6 格）进关：只有一个英雄、只有左上 HUD，行为与之前一致。
9. 已知本批临时状态：相机只跟 P1；任一人死亡即失败；剩余生命按 P1 计——D3 修正。

### D2 完成判定

- 构建 0 错误；三套测试全绿不减；Godot 扫描、引用检查、`--smoketest=frontend`、`--smoketest=ui`、`--smoketest=ui --coop-only` 通过。
- 全仓 `LevelViewController*.cs` 中无 `_player`；`ProcedureLevel.cs` 中无 `_hero` 字段。
- 上手验证 1～8 通过并截图。

---

## 4. 大任务 D3　相机、死亡、出口、结算、唐僧奶队友、龙王分档

### D3-1　相机跟随存活者中点

文件：`GameScripts/Entity/Level/Camera/LevelCameraNode.cs`。

- `_target` 改为 `private readonly List<Node2D> _targets = new(); // 由关卡会话注入的取景目标。`
- `Follow(Node2D target)` 改为 `Follow(IReadOnlyList<AbstractHeroEntity> targets)`：`_targets.Clear(); _targets.AddRange(targets); SetPhysicsProcess(true);`（相机依赖 `IsAlive` 判断，所以参数类型直接用英雄；相机已在 `GameLogic.Level` 命名空间引用实体层）。字段类型相应改为 `List<AbstractHeroEntity>`。
- `_PhysicsProcess`：取景目标 X = 存活目标 X 的平均值；**没有存活目标时本帧不移动相机**（只更新屏幕墙与抵达检测）。`StepCameraCenter` 签名不变。
- `StopFollowing`：`_targets.Clear()`。
- `LevelViewController.StartSession`：`_camera.Follow(_heroes)`。
- 屏幕墙不改：它对每个英雄身体都生效，两人拉开超过屏宽时墙挡住领先者（官方 `ViewControllor`）。

### D3-2　一死一活、全灭判负、平均剩余生命

文件：`LevelViewController.cs`。

- 删除 `HeroDeathPresentation` 枚举与 `_heroDeathPresentation` 字段，改为：

  ```csharp
  private readonly HashSet<AbstractHeroEntity> _deathPresentations = new(); // 已开始死亡演出、尚未结束的英雄。
  private bool _defeatPending; // 全部参战者死亡且演出结束，等待下一帧提交失败。
  ```
- `OnHeroDied(AbstractActorEntity actor)`：`actor is AbstractHeroEntity hero && IsSessionHero(hero) && _deathPresentations.Add(hero)` 时订阅 `hero.DeathPresentationFinished`（闭包存入新字段 `private readonly Dictionary<AbstractHeroEntity, Action> _deathHandlers = new(); // 每名英雄的死亡演出结束回调，用于对称退订。`）。**不再 `ClearLoot()`**：死者的球由 C3 的逐球归属自然作废；活着的人的球和地面物品保留。
- 死亡演出结束回调：从 `_deathPresentations` 移除并退订；若 `_heroes.TrueForAll(h => h.Dead)` 且 `_deathPresentations.Count == 0`，置 `_defeatPending = true`。
- `_Process`：`_defeatPending && Outcome == Running` 时 `_defeatPending = false; Complete(LevelOutcome.Defeated);`。
- `UnsubscribeProgressEvents`/`Cleanup`：退订 `_deathHandlers` 全部并清空 `_deathPresentations`、`_defeatPending = false`。
- `Complete(LevelOutcome outcome)`：剩余生命百分比 = 所有参战者 `Hp/MaxHp` 的平均（死者 Hp 为 0 自然计 0），四舍五入取整：

  ```csharp
  HeroModel heroes = this.GetModel<HeroModel>();
  double average = _heroes.Average(hero => (double)heroes.GetState(hero.HeroId).Hp.Value / heroes.GetState(hero.HeroId).MaxHp.Value);
  this.GetSystem<LevelSystem>().Complete(outcome, (int)Math.Round(100.0 * average));
  ```

  （造三 `GameWin.txt_state` 取平均。`LevelResult`、`SettlementForm` 不改。）
- 死者 HUD：D2 的绑定会自然显示 0 血；死者开背包已被 C4/D2 拒绝。

### D3-3　出口、龙宫入口、转关

- `OnExitContact`：任一存活参战者（C4 已按其席位判断交互键）。
- `OnExitEntered`：现有"等全部在途球落地再结算"保持。2026-11-11用户授权修复最后死者球作废后出口锁住的边界：出口续行检查从 `OnLootFinished` 统一移到 `_Process` 的 `TickLoot` 之后，正常领取和死者作废共用同一完成条件，删除回调旧检查；不新增状态/超时/事件。
- `LevelSystem.TransferToDragonPalace`：把 `GetState(HeroOf(P1)).IsDepleted` 改为"全部参战者都已耗尽才拒绝"：

  ```csharp
  ProfileModel profile = this.GetModel<ProfileModel>();
  HeroModel heroes = this.GetModel<HeroModel>();
  bool allDepleted = profile.HeroIds.All(id => heroes.GetState(id).IsDepleted);
  ```

### D3-4　唐僧甘露治疗队友

文件：`GameScripts/Entity/Heroes/Tangseng/TangsengEntity.cs`。依据造一 `Role2.__cureRole`、`Role2Shadow.cureRole`。

- 新增私有方法（与 `BullDemonKingEntity.CaptureHeroAnimEvent` 相同的遍历方式，不新增注入接口）：

  ```csharp
  /// <summary>治疗以 center 为中心、横向 halfWidth、纵向 halfHeight 范围内的存活英雄。</summary>
  /// <param name="center">治疗判定中心，世界坐标。</param>
  /// <param name="halfWidth">横向半宽，像素。</param>
  /// <param name="halfHeight">纵向半高，像素。</param>
  /// <param name="amount">本次恢复量。</param>
  private void HealHeroesAround(Vector2 center, float halfWidth, float halfHeight, int amount)
  {
      foreach (IEntity entity in GF.Entity.GetAllLoadedEntities())
      {
          if (entity is AbstractHeroEntity { IsAlive: true } hero &&
              Mathf.Abs(hero.GlobalPosition.X - center.X) <= halfWidth &&
              Mathf.Abs(hero.GlobalPosition.Y - center.Y) <= halfHeight)
          {
              hero.Heal(amount);
          }
      }
  }
  ```
- 把 `ApplyHeavenlyDew` 拆成"计算量"与"施放"：

  ```csharp
  /// <summary>本体或分身一次甘露的恢复量：法力上限/8 + 8，无双时 ×1.5。</summary>
  private int HeavenlyDewAmount() { … 现有计算 … return (int)amount; }

  /// <summary>本体甘露：治疗自己，并治疗距本体横纵都不超过 200 像素的其他存活英雄。</summary>
  public void ApplyHeavenlyDew() => HealHeroesAround(GlobalPosition, 200f, 200f, HeavenlyDewAmount());
  ```

  （自己距离为 0，自然包含在内；`Heal` 对死者无效。）
- 分身回调改为以分身位置为中心、横 200 纵 160：

  ```csharp
  _waterClone.PlayHeavenlyDew(() => HealHeroesAround(_waterClone.GlobalPosition, 200f, 160f, HeavenlyDewAmount()));
  ```

  （原代码"分身离本体横≤200、纵≤160 才治疗本体"的语义被包含：本体也在被遍历的英雄中。）

### D3-5　龙王分档、地图技能提示

- `DragonKingEntity.OnShow`：

  ```csharp
  _highLevel = profile.HeroIds.Min(id => profile.GetProgress(id).Value.Level) >= HighLevelMinimum;
  ```

  （造一 `Monster26`：取两人中较低等级；不采用造三平均等级。）
- `MapForm.ResetSkillsHint`：`profile.HeroIds.Any(id => profile.GetSoul(id).Value >= 160)`（造一 `SelectPLace.as:76`）。

### D3-6　测试与烟测

- `Tests/ProfileTests/DragonPalaceTests.cs`（`TransferToDragonPalace` 现有测试所在文件）新增：
  - `TransferToDragonPalace`：一人耗尽一人存活 → 允许；两人都耗尽 → 拒绝。
  - 剩余生命平均：通过 `LevelSystem.Complete` 的传入值无法单测，**改为**在 `--coop-only` 烟测中断言结算 `HealthPercent` 等于两人百分比平均。
- `--coop-only` 烟测补：相机 X 在两人中点附近（考虑死区）；P2 死亡后 P1 仍可行动、关卡不结算；P1 也死亡后出现失败结算；P2 走到出口按 ↑ 能离关；唐僧甘露使范围内悟空回血；龙宫（若烟测已有龙宫入口路径则复用）低等级一方 < 21 时龙王为低级形态。

### D3 上手验证（第 5 格双人档）

1. 花果山两人分开走：相机停在两人中间；一人站着不动，另一人走到屏幕边被挡住。
2. 让 P2 被打死：P2 留墓碑、右上 HUD 血 0、按小键盘 `/` 打不开背包；P1 继续打、P1 的球照常飞向 P1。
3. P1 也死：出现失败结算，剩余生命 0%。截图。
4. 重进，清完最后一波后 P2 走到出口按 ↑：整队离关，结算剩余生命为两人平均。截图。
5. 唐僧放甘露，旁边的悟空也回血（悟空头上出现绿色回血数字）。截图。
6. 地图上：任一人灵魂 ≥ 160 时技能按钮发光。

### D3 完成判定

- 构建 0 错误；三套测试全绿（含新增）；扫描、引用、`frontend`、`ui`、`ui --coop-only` 通过。
- 全仓 `HeroOf(PlayerSlot.P1)`：生产代码中只剩 `SkillForm`（E1-2 处理）与语义就是 P1 的位置，逐一列入交付说明。
- 上手验证 1～6 通过并截图。

---

## 5. 大任务 E1　赠送、技能页签、全关核验与收尾

### E1-1　装备赠送

**美术**：扩充 `Tools/LegacyMigration/export_official_inventory_actions.py` 的 `IMAGES` 表，新增 `'inventory_action_gift.png': ('sprites/DefineSprite_367*', '1.png')`（同一官方元件第 1 帧为可用态；第 2 帧即已有的禁用态）。按该脚本现有参数运行（需要 `--java`、`--ffdec`、`--source` 指向已校验散列的 `Commonv3720.swf`；`Docs/OfficialReference.md` 第 499 行有下载地址与散列，Java 与 FFDec 路径见 `Docs/Validation.md` 已有记录）。在 `Docs/LegacyAssetMap.md` 第 502 行附近补一行：`inventory_action_gift.png | 同包 sprite 367，第 1 帧 | 双人赠送可用`。导出失败（散列不符、工具缺失）则停下报告，**不得**用其他图代替。

**场景 `TheGame/UIs/Inventory/equipment_actions_menu.tscn`**：新增 ext_resource `gift_enabled`；`Panel/Gift` 改为 `texture_normal = gift_enabled`、`texture_disabled = gift`、`mouse_default_cursor_shape = 2`，删除 `disabled = true`；根节点 `node_paths` 追加 `_gift`，`_gift = NodePath("Panel/Gift")`（该组件不是 Form，没有生成器，直接手写 `[Export]` 字段）。

**`EquipmentActionsMenu.cs`**：

- `[Export] private TextureButton _gift; // 双人时把背包装备交给队友，单人禁用。`
- `public event Action<Guid> GiftRequested;`
- `Open(Guid instanceId, Rect2 anchor, bool canEquip, bool canGift)`：同 `_equip` 写法设置 `_gift.Disabled` 与光标。
- `_Ready` 绑定 `_gift.Pressed += OnGiftPressed;`，`OnGiftPressed` 照 `OnSellPressed` 写法。

**`EquipmentSystem.TryGift`**：

```csharp
/// <summary>
/// 把发送者背包中的一件装备原样移交给接收者背包；不重抽属性、不自动穿戴。
/// </summary>
/// <param name="fromHeroId">发送者。</param>
/// <param name="toHeroId">接收者。</param>
/// <param name="instanceId">发送者背包中的实例。</param>
/// <returns>两人都在阵容、实例在发送者背包且接收者背包未满时移交并返回 true。</returns>
public bool TryGift(int fromHeroId, int toHeroId, Guid instanceId)
```

- 校验：`fromHeroId != toHeroId`；两人都在 `profile.HeroIds`；`item = GetBag(fromHeroId)` 中按编号查找，找不到返回 false（穿戴中的实例不在背包，自然被拒绝）；`GetBag(toHeroId).Count < profile.Capacity`，否则返回 false。
- 移交：先 `GetBag(toHeroId).Add(item)`，再从发送者背包按索引 `RemoveAt`。两步都在同一同步调用中完成，期间不 await，不会出现半移交状态被观察到（BindableList 的回调在每步后触发，UI 只会短暂看到两边同时存在一帧内的通知顺序，不影响存档：存档只在检查点或手动保存时捕获）。
- **不写**"接收方能否穿戴"的检查：官方赠送不限制职业专属装备（对方可以出售）。

**`EquipmentCommands.cs`**：新增 `GiftEquipmentCommand(int fromHeroId, int toHeroId, Guid instanceId) : AbstractCommand<bool>`，前置校验与 `SellEquipmentCommand` 相同（关卡运行中）。

**`InventoryForm.Logic.cs`**：

- `OnInit` 绑定 `_actions.GiftRequested += OnGiftRequested;`
- `OnBagClicked`：`canGift = profile.HeroIds.Count == 2`。
- `OnGiftRequested(Guid instanceId)`：接收者 = 阵容中另一个英雄；`SendCommand(new GiftEquipmentCommand(...))` 成功播 `MenuConfirm`；失败（多为对方满包）用 `SaveNoticeForm.ShowMessage("对方背包已满！")`（与"灵魂不足！"同一提示组件）。

**烟测**：`UiSmokeScenario.Inventory.cs` 中断言 Gift 禁用的那处改为：单人档禁用；`--coop-only` 路径中可用且赠送后实例出现在 P2 背包、P1 背包少一件。

**规则测试**（`Tests/ProfileTests`，复用 `TestGameScope([(1,0),(2,0)], …)`）：成功移交且属性不变；穿戴中拒绝；对方满包拒绝；同一实例全档只存在一处；阵容外英雄拒绝；`CaptureSaveData` 往返后归属正确。

### E1-2　技能窗口按请求者打开并切换参战者页签

**新建 `GameScripts/UI/SkillForm/SkillFormData.cs`**：

```csharp
/// <summary>技能窗口打开参数：默认显示的英雄与按其席位关闭窗口的按键。</summary>
public sealed class SkillFormData
{
    /// <param name="heroId">默认显示的参战英雄。</param>
    public SkillFormData(int heroId)
    public int HeroId { get; }
}
```

- `ProcedureLevel.OnSkillsRequested`：`GF.UI.OpenUIForm(UIFormId.SkillForm, new SkillFormData(hero.HeroId))`。
- `ProcedureMap.OnSkillsRequested`：`new SkillFormData(profile.HeroOf(PlayerSlot.P1))`（地图默认 P1，语义就是 P1）。

**`SkillForm.Logic.cs`**：

- `OnOpen`：`userData is not SkillFormData data` 抛 `ArgumentException`；`profile.HeroIds.Contains(data.HeroId)` 不成立抛 `InvalidOperationException`；`_heroId = data.HeroId`；其余绑定不变。删除两处 `HeroOf(PlayerSlot.P1)`。
- `RefreshPage`：`SetHeroButton(_wukong, selected: hero == Wukong, enabled: profile.HeroIds.Contains((int)SkillHero.Wukong))`，唐僧同理。`SetHeroButton` 改为 `(TextureButton button, bool selected, bool enabled)`：`SetPressedNoSignal(selected); Disabled = !enabled; 光标按 enabled`。
- `SelectPage(SkillHero hero, bool passive)`：限制改为"该英雄在阵容中"；切到不同英雄时：先对旧 `_heroId` 退订三项（灵魂、已学技能、被动等级），`_heroId = (int)hero`，再对新英雄注册（`RegisterWithInitValue`），然后 `_activePage.Refresh(_heroId); _passivePage.Refresh(_heroId);`。把注册/退订三项抽成私有方法 `BindHero()`/`UnbindHero()`，`OnOpen` 与 `ReleaseBindingsAndPause` 也改用它们（消除重复）。
- `_Input` 关闭键：接受 `UiCancel`、以及**阵容内任一席位**的 `SkillToggle`（`InputSystem.ActionOf(SkillToggle, slot)`）。地图上没有席位概念，两人的技能键都能关窗，与战斗中一致且无歧义。
- 学习/升级扣当前页签英雄（`_heroId`）的灵魂，现有 Command 已按 `_heroId` 传，无需改。

### E1-3　其余关卡双人核验

用第 5 格双人档（必要时用 `ProfileTestData` 现有调试入口提升等级以便推进，**不改正式数值**）依次通关剩余关卡与隐藏龙宫，重点观察并记录：

| 组 | 关卡                   | 观察点                                                                                      |
| -- | ---------------------- | ------------------------------------------------------------------------------------------- |
| 1  | 桃花源、水帘洞、蝙蝠窟 | 平台与坡地上两人站位；机关与隐藏入口；蝙蝠窟尖刺对两人                                      |
| 2  | 七星洞、迦楼罗之穴     | 毒雾只作用于命中者；延迟与范围技能；鹏魔蛋态与第五击归属                                    |
| 3  | 浅岸、东海、牛魔谷     | 牛魔吞噬只抓一人（`CaptureHeroAnimEvent` 已遍历），被抓者释放；水牢；牛魔多形态的经验归属 |
| 4  | 隐藏龙宫               | 冻结只作用于命中者；龙王分档；东海旗幡全档一次                                              |

2026-11-11计划复核发现：原表未列第9关**鲨之涡流**，但表中“水牢”实际属于该关鲨魔王（`SharkDemonKingEntity`），不属于所列浅岸/东海/牛魔谷。这是核验范围遗漏，现登记供用户/架构师裁决；该关已有场景与Boss代码，双人正式实战尚未核验，不能据原九关表宣称全部主线已验。原表内容保留，本次前端任务不调整关卡玩法。

- **本批不修复**发现的问题：每条写成"关卡 / 复现步骤 / 现象 / 期望"，列入交付说明，交给用户决定后续。
- 若问题导致无法继续推进（例如必然卡死），记录后跳到下一关继续。

### E1-4　收尾清理

- `SkillSystem` 中残留的 `this.GetModel<HeroModel>()?.` 空条件访问：逐处判断调用路径。地图上不会调用施放类方法（已核实只调学习类），关卡内 `HeroModel` 必然存在，因此改为非空访问 `this.GetModel<HeroModel>().GetState(heroId)`。若某处确有地图路径调用，保留并在交付说明写明理由。
- `ProfileModel` 构造函数 `soul` 参数：全仓搜索 `new ProfileModel(`，若生产代码无人使用，删除该参数，测试与 Debug 夹具改为构造后通过 `RestoreCheckpoint` 或 `GetSoul(id).Value = …` 设置。
- 更新文档：`Docs/ProjectTour.md`（双人现状一节改为"已实现"）、`Docs/ProjectGuidelines/70-ProfileAndSave.md`（D0 转存工具说明、新档按阵容每人初始灵魂）、`Docs/WorkingNotes.md`、本文件第三部分总表与第八部分。
- 全仓搜索并列出 `HeroOf(PlayerSlot.P1)` 剩余位置，确认每处语义就是 P1。

### E1 上手验证（第 5 格双人档 + 第 6 格单人档）

1. 双人档进关，P1 按 C 打开背包，点一件背包装备 → 菜单"赠送"可用 → 点它，装备出现在 P2 背包（P2 按小键盘 `/` 查看），P1 少一件。截图。
2. 穿在身上的装备不在背包格里，无法赠送；把 P2 背包塞满（或用已满的档）后赠送提示"对方背包已满！"。
3. 单人档背包里"赠送"为灰色。
4. 战斗中 P2 按小键盘 `*` 打开技能窗口，默认显示 P2 的英雄页；点另一英雄页签可切换，学习技能扣所选英雄的灵魂。截图。
5. 地图上打开技能窗口，默认 P1 页，可切到 P2 页。
6. 双人档逐关推进的结果按 E1-3 表格记录。

### E1 完成判定

- 构建 0 错误；三套测试全绿（含赠送规则用例）；扫描、引用、全部烟测通过。
- 上手验证 1～6 完成，E1-3 核验表完整。
- 文档更新完成。

---

## 6. 子任务完成时都要检查的清单

每个子任务结束时执行者自查（不通过就改，不进入下一步）：

- 新增/修改的类型、方法、属性都有中文 XML 注释；私有字段有行尾说明。
- 每个 `+=`/`Register` 都有对称的 `-=`/`UnRegister`，并在同一类的关闭/清理路径中。
- 没有新增"默认 P1"的重载、参数默认值或兼容分支。
- 没有为测试新增生产入口。
- `git diff` 中没有与本子任务无关的文件。

## 7. 预期会遇到、已给出处理方式的情况

| 情况                              | 处理                                                                          |
| --------------------------------- | ----------------------------------------------------------------------------- |
| 生成器报 `Missing root binding` | 说明 `.tscn` 根绑定还指向已删或改名的节点，先改场景绑定                     |
| `pivot_offset` 翻回后文字仍偏移 | 检查该 Label 的 `size` 是否与偏移一致；仍不正确则停下报告并附截图           |
| 双人档出生时 P2 在墙里            | 停下报告该关名称与出生点坐标，不改关卡场景                                    |
| 覆盖已有格时删除成功但建档失败    | 现有 `ProcedureLoadGame` 失败路径会回标题；交付说明记录日志，不另写恢复逻辑 |
| 官方赠送按钮导出散列不符          | 停下报告，不替换图片                                                          |
| 某关双人卡死                      | 记录到 E1-3 表格，跳下一关                                                    |

## 8. 最终交付说明格式

E1 完成后输出一份总说明，按大任务分节，每节包含：

1. 改动文件清单（新增、修改、删除）。
2. 关键接口前后对照。
3. 每个子任务结束时的编译错误数。
4. 实际执行的验证命令与结果数字。
5. 上手验证逐条结果与截图路径。
6. 计划未覆盖而实际遇到的情况及处理。
7. 遗留问题。

最后附：E1-3 关卡核验表；全仓 `HeroOf(PlayerSlot.P1)` 剩余位置清单；D0 转存输出与备份路径。

---

# 第八部分　当前进度

阶段 A 已完成：A1（d2c3d13）、A2（c2f74db）、A3（fe0bdbc）。A3 中用户裁决存档页单/双人统一只显示角色名（“悟空”“悟空 · 唐僧”），不显示等级；摘要数据仍保留等级。

阶段 B 已完成：B1（53e9b11，含用户追加的 18 处业务方法命名整理）、B2（e3440ca）、B3（511f0c5）。各批实际命令、结果与文件清单见 [Validation](Validation.md)。

阶段 C 已完成：C1（394b97c）、C2（c489b65）、C3（61de0f2）、C4/C5（0523aae，用户授权合并交付；`project.godot` 仅追加 13 个 P2 动作）。实际结果见 [Validation](Validation.md#双人c4c5席位输入与阶段c回归2026-11-10)。

**当前：D0～D3和E1-1/2/4实现收尾，自动证据与架构师总交付见Validation顶部。E1-4已删除无生产消费者的ProfileModel构造soul参数，技能施放路径七处去掉空条件访问；总清理的地图生命周期判断及IsAvailable的地图查询语义保留。规则339/197/19、扫描/引用2285/393、最终前端和双人专项通过，最终完整单人UI亦退出0、完整PASS。用户已明确允许E1-3逐关核验及E1上手验收待补继续E1-4；D0/D1/D2/D3正式键盘待验也保持。实机连接仍为native pipe不存在，不能据隔离烟测声明正常数值全关通过；E1完成判定中的实机项尚未满足。**

---

# 第九部分　阶段 A 架构审查（2026-11-08）

审查方式：逐个 diff 阅读 `8e324af..fe0bdbc` 全部生产代码改动，并由审查者**亲自**重新执行验证，不以执行者报告为准。

## 1. 实测结果

| 项目                                                                         | 结果                                                                  |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `dotnet build Godot/GodotProject --no-restore -v quiet`                    | 0 错误                                                                |
| ProfileTests / BattleTests / LevelTests                                      | 304 / 193 / 19，全部通过（基线 279 / 193 / 19，ProfileTests 净增 25） |
| 全仓手写源码搜索退役的单一出战字段、共享背包字段、穿戴便捷读取和标量摘要引用 | 零命中                                                                |

## 2. 结论：通过，无需返工

数据形状完全按设计落地：

- `ProfileModel` 构造按阵容顺序建表，拒绝空、超过两人、重复与非正编号；`HeroOf`/`SlotOf` 越界抛 `ArgumentOutOfRangeException`。
- `SaveDataUtility.ReadItems` 在提交任何模型前完成全部归属校验：实例存在、全档只被一处引用、无孤立实例、每人不超容量。`RestoreCheckpoint` 用 `SequenceEqual` 拒绝换席位的快照。这满足"先校验、后原地恢复"的事务语义。
- 背包格子顺序靠实例池顺序保留，`BagInstanceIds` 只表达归属并稳定排序，没有新增存档字段。这是一个比计划更简洁的做法，接受。
- `InventoryBagPanel` 改为由所属窗口注入英雄、容量、背包视图与余额，不再自行读 `ProfileModel`。这消除了面板对"当前英雄"的隐式依赖，方向正确。
- 没有出现兼容转发、默认 P1 回退、新 Manager 或测试专用生产入口。

## 3. 小问题（不返工，记录在案）

1. **`ProcedureLoadGame.cs:65` 构造 `ProfileModel` 时不传 `soul`**，靠紧随其后的 `RestoreCheckpoint` 写入各人余额。功能正确，但 `soul` 参数的语义（"每位英雄初始余额"）在生产路径上从未被使用，只有测试与 Debug 夹具使用。暂不改；若 E3 收尾时仍只有测试使用，可考虑删除该参数，测试改走 `RestoreCheckpoint`。
2. **`ProcedureLoadGame.cs:74` 的日志仍写"出战英雄"**，单人下无误，D2 接入双人时一并改为输出阵容。
3. **`UiSmokeScenario.*` 中大量出现 `flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1))` 这种冗长写法。** 这是 Debug 代码，不影响生产，但 B1 还会在同一批文件里再加一层 `HeroModel.GetState(...)`。B1-6 修改这些文件时，**允许**在单个测试方法开头取局部变量 `int heroId = profile.HeroOf(PlayerSlot.P1);` 后复用，以降低噪音；不允许为此新增共享辅助方法。

## 4. 原单人占位的最终核对（2026-11-11）

以下是阶段迁移期间的原占位及当前实现。A～E代码项现已全部接入，原清单中的“留D2/D3/E1”状态已经过期；本次按用户要求核对计划时同步实际状态，不改历史批次规格。生产显式P1仅保留地图打开技能窗的默认页，语义就是首席。

| 位置 | 当前实现 | 负责批次/状态 |
| --- | --- | --- |
| HeroSystem.OnInit | 遍历阵容，按英雄保存具名来源/实际闭包并对称退订 | B1已完成 |
| SkillSystem.TryActivate/技能登记 | 参数heroId读取本人的状态与登记 | B1/B2已完成 |
| LevelSystem.RecordDefeat | 显式heroId；0仅计击败、不发经验 | C2已完成 |
| BullDemonHeartDeathState唯一奖励 | 按最后有效英雄命中来源解析，无归属不发 | C2已完成 |
| LevelViewController.Loot的升级/球上限/球领取 | 使用真实归属英雄；死者球由逐球Tick回收，领取前读取归属 | C2/C3/D2/D3已完成 |
| 地面物品领取 | 遍历阵容席位，首个存活且有容量的接触者领取 | C3/D2已完成 |
| 治疗飘字/换装订阅 | 每名英雄分别注册，在实际被治疗者位置显示，结束对称退订 | D2已完成 |
| Complete剩余生命 | 全部参战者生命百分比平均，死者计0 | D3已完成 |
| EntranceOpacity/龙宫入口控制权 | 全员淡出/关闭控制，任一存活者可交互转关 | D2/D3已完成 |
| TransferToDragonPalace耗尽判断 | 仅全员耗尽时拒绝 | D3已完成 |
| DragonKingEntity分档 | 阵容最低等级 | D3已完成 |
| MapForm技能发光 | 任一参战者灵魂达到160 | D3已完成 |
| SkillForm打开/页签/关闭键 | SkillFormData指定英雄，阵容内切页，先退旧三项再绑新三项；实际席位键关闭 | E1已完成 |
| BattleForm无双立绘 | 已有菜单Panel同步事件携带heroId，Form选择本人立绘；旧席位绑定记录已删除 | D2及用户授权Panel调整已完成 |
| ProcedureLevel生成/开窗 | 有序集合生成；每次await后核对进入周期；开窗按请求席位解析本人 | D2/E1已完成 |
| ProcedureLoadGame读档日志 | 按完整阵容记录 | D1已完成 |
| ProcedureMap打开技能窗默认页 | 显式首席，唯一保留的生产HeroOf(PlayerSlot.P1) | 有意保留，无待迁移项 |

当前未完成的是D0～E1的正式键盘上手与E1-3逐关实战，用户已授权暂留；自动验证不能代替这些项。逐条核验状态、命令与截图限制见Validation顶部总交付及本轮计划复核记录。

## 5. 对原计划的自我审查与修正

阶段 A 的执行暴露出原计划在后续批次的若干漏洞，已在本次修订中逐条修正：

| #  | 原计划问题                                                                                                                                                           | 后果                                                            | 修正位置                                                                     |
| -- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1  | **C2 只说"在 `OnHurt` 里写入归属"**。但 `AbstractEnemyEntity.OnHurt` 开头的 `if (Dead) return;` 位于锁定攻击方之前，**致死那一下根本走不到写入点** | 经验会发给倒数第二次命中者；单人下看不出来，双人下必错          | C2 写明写入点必须在 `if (Dead) return;` 之前，并单列鹏魔蛋态的第五击       |
| 2  | C2 没有说明为什么不能复用 AI 目标 `_target`                                                                                                                        | 执行者很可能直接拿 `_target` 当归属（它会自行选人、会被置空） | C2 增加官方 `curAttackTarget` 逐行依据与"不能复用 `_target`"的明确约束   |
| 3  | C2 留了"`heroId` 为 0 或改成 `int?`，二者选一"                                                                                                                   | 把设计决定交给执行者                                            | 定为 0 表示无归属，禁止引入 `int?`                                         |
| 4  | **C4 的小键盘键码整列写错**（例如 `Kp1` 写成 4194433，实为 `KpMultiply`）                                                                                  | P2 键位全部错位                                                 | 已用 `GodotSharp 4.7.2` 的 `Godot.Key` 实测重写，并附枚举名              |
| 5  | B1 没指出 `HeroSystem.Damage` 死亡分支与 `Reset` 调用的是**无参** `HeroStatusSystem.ClearAll()`                                                          | 多人下一人死亡会清掉另一人的冰冻/中毒                           | B1-3 新增 `ClearAll(int heroId)`，B1-4 要求死亡与重置按人清，B3 补对应测试 |
| 6  | B1 没指出三个订阅回调靠 `Hero.HeroId` 推断归属                                                                                                                     | 多人下回调无法知道自己属于谁                                    | B1-4 要求改为每人一份闭包                                                    |
| 7  | B1 的 `AbstractHeroEntity.OnShow` 校验没考虑 `GetState` 未建立时直接抛 `KeyNotFoundException`                                                                  | 异常信息丢失业务语义                                            | B1-5 写明先 `Heroes.ContainsKey`、后读 `Attributes`                      |
| 8  | B1 给 `HeroModel` 设计了 `Remove(int heroId)`                                                                                                                    | 没有任何消费者，属于预建抽象                                    | 删除该成员                                                                   |
| 9  | B2 没指出 `AbstractHeroEntity.OnHide` 调用的是无参 `ClearSkillDefinitions()`                                                                                     | 多人下一人回池会清空另一人的技能登记                            | B2 列为真实缺陷，B3 补测试                                                   |
| 10 | C1 要求给 `BattleFormData`/`InventoryFormData` 加席位字段                                                                                                        | 两者已持有英雄实体，`Hero.Slot` 即席位，加字段是重复状态      | 删除该要求，C4 改为读 `Hero.Slot`                                          |
| 11 | C1 把 `SmokeTestDriver` 列为需改的显示点                                                                                                                           | 它只在场景树里查找英雄，从不显示英雄                            | 删除该项，补上真正的 `UiSmokeScenario.ShowHeroAsync`                       |
| 12 | C4 允许"按席位重载 或 统一加参数"二选一                                                                                                                              | 默认 P1 的重载等于保留隐式单人入口，违背硬性禁止                | 定为统一加 `PlayerSlot slot`，禁止默认值重载                               |
| 13 | 原计划要求"每个子任务结束时必须编译通过"，A1、A2 实际都需要用户临时裁决例外                                                                                          | 规则与现实不符，每批都要重新申请                                | 第一部分改为通用规则：中间子任务允许迁移错误、逐步记录，大任务最后一步清零   |
| 14 | D7 只写了龙王，漏了官方地图"任一玩家余额 ≥160 即技能发光"（`SelectPLace.as:76`）                                                                                  | 双人下技能入口提示不一致                                        | D7 补入该项                                                                  |

这些修正都**只收紧约束、补充事实**，没有改变第二部分已确定的数据形状与设计决定。

---

# 第十部分　阶段 B 架构审查（2026-11-09）

审查方式：逐个 diff 阅读 `fe0bdbc..511f0c5`（B1 53e9b11、B2 e3440ca、B3 511f0c5）全部生产代码改动，并由审查者亲自重跑验证。

## 1. 实测结果

| 项目                                                       | 结果                                                                     |
| ---------------------------------------------------------- | ------------------------------------------------------------------------ |
| `dotnet build Godot/GodotProject --no-restore -v quiet`  | 0 错误                                                                   |
| ProfileTests / BattleTests / LevelTests                    | 320 / 195 / 19 全部通过（阶段 A 后为 304 / 193 / 19，净增 16 / 2）       |
| `HeroModel` 之后是否还有标量直读                         | 无；生产代码只经 `GetState`/`Heroes`/`CreateState`/`ClearStates` |
| 无参 `ClearAll()` / `ClearSkillDefinitions()` 的调用点 | 只剩各自系统的 `OnDeinit` 与 `HeroSystem.OnDeinit`，符合设计         |

## 2. 结论：通过，无需返工

- `HeroState` 逐字保留初值（`Level = new(1)`、`ControlsEnabled = true`、`SkillInvulnerableFrom = +∞`），`HeroId` 改为构造只读。`HeroModel` 只剩容器，没有 `Remove`，没有默认英雄转发。
- `HeroSystem`：三个订阅回调改为每人闭包、存入 `_subscriptions` 并在 `OnDeinit` 对称退订；`Damage` 死亡分支与 `Reset` 改用 `ClearAll(heroId)`。两处计划指出的缺陷都按要求修正。
- `HeroStatusSystem`：按人方法与两个 `ClearAll` 重载职责清楚。
- `SkillSystem`：登记表按 heroId 分表；`OnHide` 只清本人；`SelectSkills` 只取消本人旧施放。`TryActivate` 在 `IsAvailable` 通过后直接 `_skillDefinitions[heroId][id]` 取定义，因为 `IsAvailable` 已保证存在，这一处去掉了冗余的 `TryGetValue`，正确。
- `SkillEffectNode` 按 `Source.OwnerEntityId` 解析英雄记无双；龙王冻结、狮驼毒云按实际命中的 `target`。
- `AbstractHeroEntity.Attributes` 属性名保留，`SmokeInspection` 反射同步改为 `HeroState`。
- B3 新增 9 组双英雄隔离用例，覆盖了计划要求的 7 条规则（属性来源、效果与死亡清理、受击保护、恢复节拍、无双、并发施放、重建退订、阵容外登记拒绝）。

## 3. 计划外改动的裁定

B1 提交同时包含用户追加授权的 **18 处业务方法命名整理**（`Get` → `GetState`/`GetAppearance`/`GetValue`，`Add`/`Evaluate` → `AddExperience`/`EvaluateProgress`，`NodePool.Get` → `RentNode`，四个倍率属性 → `GetXxxMultiplier(heroId)` 等），并修订了 `10-CSharp.md` 的命名规则。这是用户在 B1 期间的明确追加指令（见 WorkingNotes），不属于执行者越界。所有改名都同步了全部调用点、没有保留旧名转发，接受。

**对后续计划的影响：** 本计划第五部分以前写的 `HeroModel.Get`、`BodyStateType`、`MoveSpeedMultiplier` 等名字已过时，执行者以当前代码为准；第六部分（阶段 C）已按新名字重写。

## 4. 小问题（不返工，记录在案）

1. `SkillSystem.GetBodyStateType`、`TryActivate`、`CancelSkill`、`EndSkill`、`GrantInvulnerability`、`IsInvulnerable` 仍保留 `this.GetModel<HeroModel>()?.` 的空条件访问。在关卡内 `HeroModel` 必然存在；地图上（`SkillForm`）只调学习类方法，不会进入这些施放方法。这些 `?.` 是 B1 之前就有的防御，现在仍可能在 `GetState` 时抛 `KeyNotFoundException` 而非返回 null，所以"防御"本身已不完整。**E3 收尾时统一评估是否去掉**，本阶段不动，避免扩大范围。
2. `HeroSystem.OnDeinit` 先逐人 `ClearWs`/`ClearHurtProtection`，再 `ClearStates()`。由于状态随后整体丢弃，前面的逐人清理只在"有外部订阅者观察 `Ws`/`WsActive` 变化"时才有意义（HUD 会收到归零通知）。保留，属于对称清理，不是冗余。

## 5. 对原计划阶段 C～D 的自我审查与修正

阶段 B 落地后对照真实代码重新核查阶段 C，发现以下问题，已在第六部分改正：

| #  | 原计划问题                                                                                                                                                                         | 后果                                                                                                                                                | 修正                                                                                                  |
| -- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| 1  | **C2 对官方规则描述不准**：写成"官方归属是最后一次有效命中者"。实际 `curAttackTarget` 在无人攻击时也会被 `selectTarget` 自行选中，官方会把经验给"已锁定但从未打过"的英雄 | 执行者可能照搬官方"共用 AI 目标"的写法                                                                                                              | C2 改为如实写明官方行为及其副作用，并给出项目取舍理由：归属只认真实命中，不复刻共用字段的副作用       |
| 2  | **C2 只管经验，没管飞行球**。官方 `dropAura` 在无归属时直接 `return`，即无归属的怪**不出球**；原计划只说"不发经验"                                                 | 单人下陷阱杀怪仍会出球，与官方不一致；双人下球无处可飞                                                                                              | 新增 C2-5：飞行球只在有归属时生成，地面物品照常                                                       |
| 3  | **C2 未说明 `AwardedExperience` 的处理**                                                                                                                                   | 结算界面"获得经验"会计入实际没发出的经验                                                                                                            | C2-3 写明无归属时只加击败数，不加 `AwardedExperience`                                               |
| 4  | **C2 的鹏魔蛋态只说"显式写入或提到 ReceiveHit 一侧"**，又是二选一                                                                                                            | 交给执行者决定                                                                                                                                      | 定为在基类抽出 `protected void RecordHeroAttacker(int)`，基类 `OnHurt` 首行与鹏魔蛋态分支各调一次 |
| 5  | **C2 的牛心奖励给了"二选一"的访问方式**，且没核对官方                                                                                                                        | 官方 `Monster24.toDead` 实际**恒推给 player1**；且 `BullDemonHeartDeathState` 是 `internal sealed`、不派生自实体，读不到 protected 成员 | 定为 `internal int LastHeroAttackerId`；明确写出与官方的差异及理由                                  |
| 6  | C2 漏了 `LevelSmokeScenario` 中手动构造 `EnemyDiedEventArgs` 的调用点                                                                                                          | `Create` 增参后 Debug 工程编译失败                                                                                                                | C2-2 列出，并指明用同方法已有的 `_hero.Id`                                                          |
| 7  | **C1 漏了 `TestArenaController`**：它经 `ShowOwnedEntityAsync(…, null, …)` 显示英雄，原计划误以为"传入即可"                                                            | `OnShow` 改为强制 `HeroSpawn` 后，测试场景直接抛异常                                                                                            | C1-4 列出并给出具体写法                                                                               |
| 8  | C1 没说明 `HeroSpawn.cs` 是否需要加测试编译项                                                                                                                                    | 执行者可能乱加编译项                                                                                                                                | 核对两个 csproj：都不链接 `AbstractHeroEntity` 所在目录，**不需要**加                         |
| 9  | **C3 的 `OnLootFinished` 先 `Release(orb)` 再发放**，原计划没指出                                                                                                        | 若按"在回调里读 `orb.OwnerEntityId`"实现，会读到复位后的 0，奖励全部丢失                                                                          | C3-1 写明必须在 Release 前读取                                                                        |
| 10 | C3 原写"`OnHeroHealthRestored` 改为实际被治疗英雄的位置"，但 `HealthRestored` 是 `Action<int>`，拿不到实体                                                                   | 执行者要么改事件签名（扩大范围），要么做不到                                                                                                        | 改为登记 D2 处理（订阅改集合时用闭包），C3 不动                                                       |
| 11 | **C4 的 `SkillForm` 关闭键"识别打开者席位"无法实现**：`SkillForm` 也在地图打开，没有英雄实体可取席位                                                                     | 执行者会自行引入 FormData 字段                                                                                                                      | 本批 `SkillForm` 保持只认 P1，登记 E2 处理                                                          |
| 12 | C4 没处理 BattleForm 在单人下收到 P2 键的情况                                                                                                                                      | 单人按小键盘 `/` 会以 P2 名义请求开窗，可能越界取 `HeroOf(P2)` 抛异常                                                                           | C4-5 写明席位超出阵容时忽略                                                                           |
| 13 | C4 的出口改造只写了一句"对每个重叠英雄判断"                                                                                                                                        | `FindEligibleHero` 的"取第一个"语义和 `Godot.Input` 直读 P1 组合键都需要改，容易只改一半                                                        | C4-4 拆成具体步骤                                                                                     |
| 14 | C4 没提方向键会让 HUD 按钮获得焦点的风险                                                                                                                                           | P2 移动可能触发 HUD 按钮                                                                                                                            | C4-6 要求实测，出现冲突停下报告                                                                       |
| 15 | C 阶段多处写死行号（如 `ProcedureLevel.cs:176`）                                                                                                                                 | B1 改动后行号已偏移，执行者会改错位置                                                                                                               | 第六部分改为用代码片段定位，不再写行号                                                                |
| 16 | 第九部分占位清单的行号与条目已过时，漏了 `Entrance.cs`、`Complete` 剩余生命、`SkillForm._Input`                                                                              | 后续批次可能遗漏                                                                                                                                    | 按实际代码重新生成清单，逐项标注负责子任务                                                            |

这些修正都只收紧约束、补充事实，没有改变第二部分已确定的数据形状与设计决定。唯一新增的公开/受保护成员（`RecordHeroAttacker`、`LastHeroAttackerId`、`LootOrbNode.OwnerEntityId`、`AbstractHeroEntity.Slot`、`IHeroBody.Slot`、`InputSystem.ActionOf`）都有至少一个真实调用点。

## 6. C3 核对后的后续占位

| 位置                                                     | 本批保留与后续处理                                                                                                                                                            | 负责批次         |
| -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------- |
| `LevelViewController.OnHeroHealthRestored(int amount)` | `HealthRestored` 仍为 `Action<int>`，单人下 `_player.GlobalPosition` 就是被治疗者位置；C3-3 不改代码，D2 按集合订阅时用闭包捕获实际英雄。此项也保留在第九部分总占位清单 | D2               |
| `SkillForm._Input`                                     | 地图上也能打开技能窗，本阶段保留 P1 的 SkillToggle 与共用 UiCancel；新 D2-3 按当前页签英雄的席位关闭                                                                          | 新 D2-3（原 E2） |

---

# 第十一部分　阶段 C 架构审查（2026-11-10）

审查方式：逐个 diff 阅读 `511f0c5..0523aae`（C1 394b97c、C2 c489b65、C3 61de0f2、C4/C5 0523aae）全部生产代码改动，审查者亲自重跑验证，并检查用户正式存档目录。

## 1. 实测结果

| 项目                                    | 结果                                                                     |
| --------------------------------------- | ------------------------------------------------------------------------ |
| 构建                                    | 0 错误                                                                   |
| ProfileTests / BattleTests / LevelTests | 323 / 197 / 19 全部通过（阶段 B 后为 320 / 195 / 19）                    |
| `project.godot` 13 个 P2 动作         | 键码与计划表逐一相同，全部 `device=-1`                                 |
| 方向键 HUD 焦点                         | Validation 记录 5 个菜单按钮均 `focus_mode=0`，方向键与 Enter 不误开窗 |

## 2. 结论：通过，无需返工

- **C1**：`HeroSpawn` 单独成文件；`OnShow` 校验顺序正确（先模型存在、再参数类型、再席位一致）；三处显示点全部传参，`ProcedureLevel` 与 `TestArenaController` 删除了显示后单独赋位置的行。
- **C2**：`RecordHeroAttacker` 是基类 `OnHurt` 第一条语句，在 `if (Dead) return;` 之前；鹏魔蛋态分支调用它；`OnShow` 复位；`EnemyDiedEventArgs.KillerEntityId` 在 `Create`/`Clear` 对称；`RecordDefeat(0, …)` 只计数、不计 `AwardedExperience`；牛心无归属时不发不报；无归属不出球、地面物品照常。
- **C3**：`OnLootFinished` 在 `Release` **之前**读归属；`TickLoot` 逐球按归属者追踪，归属者失效即回收不发放；地面物品按接触者入账，飘字在接触者身上。
- **C4**：`ActionOf` 无缓存、无默认参数重载；`ConsumePress` 记录解析后的动作名，两人不会互相消费；出口按每个重叠英雄的席位判断交互与组合键；BattleForm 只遍历阵容内席位，单人按 P2 键无响应；死者不能开背包。

## 3. 小问题（不返工）

1. `BattleForm._UnhandledInput` 在快捷键分支直接 `Invoke` 事件，而 HUD 按钮点击走 `OnInventoryClicked`/`OnSkillsClicked`（内含 `ControlsEnabled` 守卫）。快捷键分支的 `ControlsEnabled` 守卫在方法开头按 `_formData.Hero` 检查，D2 变为两人时需改成检查请求席位的英雄。已写入 D2-2。
2. `LevelExitNode._PhysicsProcess` 每帧 `GetOverlappingBodies().Any(...)` 分配迭代器，出口只在清场后启用，开销可忽略。不改。

## 4. 计划漏洞：用户正式存档不可读（已在 D0 修正）

用户反映"游玩时存档都无法读取"。审查者检查正式目录 `%APPDATA%/Godot/app_userdata/GameZMXY/GameData/` 与最新日志：4 个格子全部是 **v12**，日志为"存档格 N 不可读"。原因是 A1、A2 把格式升到 v14 时，计划写的是"开发档作废重建，正式档转存须单独授权"，**但之后没有任何批次安排这次转存**。执行者遵守了"不覆盖正式存档"，于是用户的 4 个档一直不可读。

这是计划本身的漏洞。修正：

- 新增 D0：一次性转存工具（不进运行时代码），用正式校验器与样例测试证明无损，**正式执行前停下等用户确认**。
- 第七部分新增硬性规则：D、E 阶段若再改存档格式，必须同批提供并执行转存。

## 5. 对阶段 D～E 的重新规划（用户要求）

用户要求：每批都能上手游玩看到变化；任务量适中，代码很少的任务应合并。对照原计划：

| 原编号                | 实际改动量                                                      | 处理                                                            |
| --------------------- | --------------------------------------------------------------- | --------------------------------------------------------------- |
| D1 标题/选角/选格     | 大（3 个窗口、3 个流程）                                        | 保留为 D1，拆三个子任务                                         |
| D2 装配两人           | 中，但**单独做完看不到效果**（没有 HUD，P2 状态无从观察） | 与 D3 合并为新 D2                                               |
| D3 双方 HUD           | 中                                                              | 并入新 D2                                                       |
| D4 相机               | 小（`Follow` 改为列表 + 取中点）                              | 并入新 D3                                                       |
| D5 死亡/出口/结算     | 中                                                              | 并入新 D3                                                       |
| D6 唐僧治疗队友       | 很小（两处范围判断）                                            | 并入新 D3                                                       |
| D7 龙王分档、地图发光 | 很小（两行）                                                    | 并入新 D3                                                       |
| D8 花果山闭环验收     | 只验证                                                          | 取消，由 D2、D3 的上手验证承担                                  |
| E1 赠送               | 中                                                              | 与 E3 合并为新 E1                                               |
| E2 技能页签           | 小                                                              | 并入新 D2（D2-3），否则 D2 上手时 P2 打开技能窗看到的是 P1 的页 |
| E3 其余关卡核验与收尾 | 只验证 + 少量清理                                               | 并入新 E1                                                       |

另外对照真实代码修正了原 D 阶段的几处错误假设：

| #  | 原计划                                                                                          | 问题                                                                                                                                                                            | 修正                                                                                       |
| -- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| 1  | D1 写"选角确认后回到 ProcedureTitle 打开保存模式"                                               | `ProcedureTitle.OnEnter` 第一件事就是 `RemoveData(FlowKeys.SaveSelection)`，回去会清掉刚选的阵容                                                                            | 保存模式由 `ProcedureCharacter` 自己打开存档页，照 `ProcedureTitle` 已有写法订阅与取消 |
| 2  | D1 写"已有格复用确认层提示覆盖"                                                                 | `LoadOrCreateAsync` 对 `unitId != 0` 走读档，没有覆盖写入路径                                                                                                               | 覆盖 = 先调现有 `DeleteSlotAsync` 再按空格建档，不新增写入路径                           |
| 3  | D1 未提 `SaveSelectionVariable` 只有单个 `HeroId`                                           | 双人无法携带阵容                                                                                                                                                                | 改为 `int[] HeroIds`                                                                     |
| 4  | D1 未提保存模式下的删除按钮                                                                     | 选格时可能误删其他档                                                                                                                                                            | 保存模式隐藏删除按钮                                                                       |
| 5  | D2 只写"出生点按席位横向错开"                                                                   | 没说错开多少、要不要改场景                                                                                                                                                      | 定为 P2 在 P1 右侧 100px（造一 `MainGame` 的 200/300），不改关卡场景                     |
| 6  | D3 写"P2 右上镜像、文字翻回"但没给做法                                                          | 执行者可能新建组件类                                                                                                                                                            | 复制现有面板节点、镜像后翻回可读子节点，Logic 内循环绑定，不新建类                         |
| 7  | D6 治疗队友只写了范围                                                                           | 唐僧拿不到队友列表                                                                                                                                                              | 遍历 `GF.Entity` 中的英雄，与牛魔抓取的既有写法一致，不新增注入                          |
| 8  | 原计划敌人 AI 无改动说明                                                                        | 执行者可能以为要改 AI 选目标                                                                                                                                                    | 已核实 `CanSelectTarget` 接受任意存活异阵营角色，双人无需改 AI                           |
| 9  | D1 只说"改 FrontendSmokeScenario 的路径"                                                        | `AdvanceToLevel` 是 `SmokeTestDriver`、`UiSmokeScenario.Inventory/Tangseng` 共用的进关入口，`MapProgressSmokeScenario` 另有一份按钮表；只改一处会让其余烟测全部卡在标题 | 新增 D1-4 逐项列出                                                                         |
| 10 | D2 漏了 `LevelSystem.StartSession` 的校验形同虚设                                             | 调用方传的是 `ProfileModel.HeroIds` 本身，"实体与阵容一致"永远成立                                                                                                            | D2-1 改为传实际显示英雄的编号                                                              |
| 11 | D2 漏了 `EntranceOpacity` 只淡出 `_player`                                                  | 进龙宫时 P2 不淡出                                                                                                                                                              | D2-1 补入                                                                                  |
| 12 | D2 写"`MusouActivationNode` 不改"，没说 `BattleForm` 调它时写死了 `_formData.Hero.HeroId` | P2 开无双显示 P1 立绘                                                                                                                                                           | D2-2 写明按回调捕获的 heroId 传入                                                          |
| 13 | E2 技能页签排在 E1                                                                              | D2 上手时 P2 按 `*` 看到 P1 的技能页，无法验证 P2 学技                                                                                                                        | 移到 D2-3                                                                                  |
| 14 | 草稿 D0-2 写"用脚本同等逻辑生成期望文件"，含义不清                                              | 执行者可能在 C# 里重写一遍转存逻辑                                                                                                                                              | 明确期望文件由脚本实际生成并提交，测试只用正式校验器读取                                   |
| 15 | 草稿 D0 没提格 3 的 `.bak` 与主文件内容不同                                                   | 转存后若 `.bak` 校验失败，执行者可能误判整格失败                                                                                                                              | D0-1 要求每个文件独立转换、独立报告                                                        |
| 16 | 鹏魔破蛋次数写死 5，注释写"单人模式"                                                            | 双人规则可能不同，但尚未查到官方依据                                                                                                                                            | 登记到 E1-2 核验项，先查源码再报告，不预先改                                               |

这些修正只收紧约束、补充事实，没有改变第二部分已确定的数据形状与设计决定。
