# 验证

本页保留当前维护命令、最近覆盖各功能的有效记录及必要失败边界；逐轮尝试、被后续实现替代的结果和旧版“当前限制”已清理，历史可由Git追溯。下列带日期的结果是对应检查点的历史证据，不能自动视为对未来修改已重跑。当前功能与待办见[ProjectTour](ProjectTour.md)。

## 双人C1：英雄实体携带席位（2026-11-09）

用户接受阶段 B 审查并确认开始后，按 C1-1 → C1-4 完成英雄显示参数、实体席位、身体接口和全部三处消费者迁移。当前仍只生成 P1，一套输入与 HUD，存档保持 v14。全部要求验证通过；**本批停在 C1 等待审查，不进入 C2**。

依据为实施计划第一、二部分、第六部分共同约束与 C1、第九部分占位清单及第十部分审查修正，以及规范第 00/10/30 章。复用现有 GF.Entity 显示参数、ProfileModel.HeroOf 和实体池生命周期，不新增系统、兼容入口或默认 P1 回退。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态与基线 | HEAD 为 **511f0c5**；已有计划与 WorkingNotes 两份用户未暂存修订，索引原为空。`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、0 警告（增量）**；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：**320/320、195/195、19/19**，均退出 0。快照与日志在 `.godot/validation/coop_c1_20261109/` |
| C1-1 完成 | 新增 HeroSpawn 后运行 `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架警告**；`c1_1_build.log` |
| C1-2 完成 | 实体新增 Slot、强制参数/席位校验并接管出生位置；同一构建命令：**退出 0，0 错误、5 条既有框架警告**；`c1_2_build.log`。此时旧消费者尚未迁移，未启动游戏；编译通过不等于旧入口可运行 |
| C1-3 完成 | IHeroBody 与三处假实现同步后，同一构建命令：**退出 0，0 错误、5 条既有框架警告**；`c1_3_build.log` |
| C1-4 完成 | 三处显示入口全部传 HeroSpawn 后，同一构建命令：**退出 0，0 错误、5 条既有框架警告**；`c1_4_build.log`。四步编译错误数为 **0 / 0 / 0 / 0** |
| 最终游戏构建 | 格式化后 `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架警告**；`final_build.log` |
| 最终规则测试 | 再次执行上述三条 `dotnet test` 命令：**退出 0，320/320、195/195、19/19**，各自失败 0、跳过 0；`final_profile.log`、`final_battle.log`、`final_level.log`。测试数量及原断言保持，既有 NU1900、CS8632/CS0649 警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**，Godot 正常生成 HeroSpawn.cs.uid；保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、390 个 UID**；比 B3 增加 HeroSpawn 的 1 个 UID，路径数不变；`final_references.log` |
| 测试场地入口 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_arena_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 600 res://TheGame/Scenes/test_arena.tscn`：**退出 0**；日志记录 `Wukong @ (120, 300)`，与 PlayerStart 原标记一致，随后显示两只怪并报告就绪，关停前无参数/席位异常。退出报告 **3 个 Shape2D RID、8 个 ObjectDB 实例、3 个资源占用**，如实保留 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。悟空/唐僧建档、进关、保存重读、地图学技、加载取消与部分注册清理通过。坏文件、重复注册和旧作用域拒绝为预期注入；退出保留 **3 个 ObjectDB 实例**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。技能/无双、拾取/出售、15 件穿戴、朝向/动画/外观、窗口和实体复用、60/120Hz 坡地/平台、死亡/检查点、存档重入、备份恢复、加载取消和失败清理通过。坏文件及 HUD 打开失败为预期注入；退出保留 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、140 个 ObjectDB 实例、11 个资源占用**与 GL 纹理明细。前端/UI 均未设帧数强制退出，使用原 runner 的 900 秒超时与烟测完成后自行退出 |
| 格式 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：GodotProject **6 份**、ProfileTests **1 份**、BattleTests **2 份**，均退出 0；`format_verify_game.log`、`format_verify_profile.log`、`format_verify_battle.log`，仅测试工作区既有加载警告 |
| 差异与边界 | `git diff --check`、`git diff --cached --check` 均退出 0；9 份 C# 为 UTF-8/CRLF，无行尾空白。HEAD 保持 511f0c5；执行中用户暂存的两份原文档修订与开工快照一致，暂存差异 SHA-256 在后续操作中保持；本批增量未暂存，未执行 Git 写操作。Framework/addons/Luban、生成业务代码/资源、源表/bytes、场景、project.godot、csproj 与 FormData、SmokeTestDriver 均无本批变化 |

关键接口前后对照：

| C1 前 | C1 后 |
| --- | --- |
| 无英雄显示参数类型 | 新增 `public readonly record struct HeroSpawn(PlayerSlot Slot, Vector2 Position)`，命名空间 GameLogic.Heroes，位置为世界坐标/像素 |
| AbstractHeroEntity 无席位属性 | 新增 `public PlayerSlot Slot { get; private set; }`，每次 OnShow 覆盖，OnHide 不清空 |
| `OnShow(object userData)` 不消费英雄参数；只检查本局模型已有 HeroId | 签名不变；按顺序检查本局模型已有 HeroId → userData 是 HeroSpawn → `ProfileModel.HeroOf(spawn.Slot) == HeroId` → 写 Slot → base.OnShow。参数类型错误抛 ArgumentException，英雄不匹配抛 InvalidOperationException；非法席位沿用 HeroOf 的 ArgumentOutOfRangeException |
| 流程/测试场地在显示返回后另赋出生位置 | OnShow 的池复位段在 Velocity 旁写 `GlobalPosition = spawn.Position`；删除两处返回后的单独赋值。原朝向、待机、RESET、外观和身体状态机代码保持 |
| IHeroBody 只有 HeroId，无席位 | 在 HeroId 后新增 `PlayerSlot Slot { get; }`；三处既有测试假实现返回 P1 |

全仓搜索执行 `rg -n -C 2 'ShowEntityAsync|ShowOwnedEntityAsync' . --glob '*.cs'`（完整输出见 `hero_show_search.log`），逐项检查显示入口与转发链，仅下列三处创建英雄显示请求，其余为敌人显示、框架实现或注释，无第四处英雄消费者：

| 英雄入口 | 最终参数与位置归属 |
| --- | --- |
| `MainPack/Scripts/Procedure/ProcedureLevel.cs:161` | `ShowEntityAsync<AbstractHeroEntity>(heroConfig.EntityId, new HeroSpawn(PlayerSlot.P1, _levelViewController.PlayerSpawnPosition))` |
| `MainPack/Scripts/Debug/UiSmokeScenario.cs:1659` | `GF.Entity.ShowEntityAsync(definition.EntityId, new HeroSpawn(PlayerSlot.P1, Vector2.Zero))`；具体烟测随后设置测试位置的代码按计划保留 |
| `MainPack/Scripts/Debug/TestArenaController.cs:246` | `ShowOwnedEntityAsync(entities, _heroEntityId, new HeroSpawn(PlayerSlot.P1, _playerStart.GlobalPosition), cancellationToken)`；现有转发方法只改写 EnemySpawn，对 HeroSpawn 原样透传 |

另执行 `rg -n 'new HeroSpawn|GlobalPosition\s*=' Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureLevel.cs Godot/GodotProject/TheGame/MainPack/Scripts/Debug/TestArenaController.cs`：只有两处 HeroSpawn 和测试场地原有相机跟随赋值，英雄出生位置不再由消费者重复赋值。SmokeTestDriver 没有显示入口，未修改。

实际文件清单：**14 份（12 份修改、2 份新增），无删除**。下列 TheGame 路径相对 `Godot/GodotProject/`：

| 目录 | 文件 |
| --- | --- |
| `TheGame/GameScripts/Entity/Heroes/` | 新增 `HeroSpawn.cs`、Godot 生成的 `HeroSpawn.cs.uid`；修改 `AbstractHeroEntity.cs`、`Body/Core/IHeroBody.cs` |
| `TheGame/MainPack/Scripts/Procedure/` | 修改 `ProcedureLevel.cs` |
| `TheGame/MainPack/Scripts/Debug/` | 修改 `UiSmokeScenario.cs`、`TestArenaController.cs` |
| `Tests/BattleTests/` | 修改 `HeroBodyTests.cs`、`SkillReuseTests.cs` |
| `Tests/ProfileTests/` | 修改 `SkillStateTests.cs` |
| `Docs/` | 修改 `CoopImplementationPlan.md`、`WorkingNotes.md`、`ProjectTour.md`、`Validation.md`；保留开工已有的用户文档修订 |

任务目标已明确三处英雄调用，计划 C1-4 首句却仍写“两处”，下段才另列 TestArena；按目标与第十部分修正执行，并将该段统一为三行表格，未改变方案。HeroSpawn 无需测试编译项，两个测试工程均正常编译，未改 csproj。除此之外未遇到需改变方向的计划冲突。

单人出生位置沿用原流程/场地标记，朝向、动画和外观初始化逻辑保持，完整前端/UI 与池复用回归通过；正常数值全关人工实战未执行。本批未改存档结构，v14 档案无需重建。烟测均使用既有 runner 的案例专属 APPDATA，不覆盖正式玩家存档；历史退出诊断保留，未在本批处理。

当前 C1 范围内无未完成项。C2 的前置条件为用户接受 C1 并明确授权；经验/致死归属、掉落归属、P2 输入、第二英雄装配、双 HUD 均未开始。

## 双人B3：战斗规则证据与阶段A～B回归（2026-11-09）

用户接受 B2 后授权 B3，按 B3-1 → B3-2 完成双人战斗规则证据与阶段 A～B 完整回归。**只修改测试和进度/验证文档，生产源码与资源无变化**。新增 17 个规则案例，当前 Profile 320、Battle 195、Level 19。仍只装配一名实战英雄，存档保持 v14，无持久结构变化；本批完成后停在 B3，等待审查，不进入 C1。

依据为计划第一、二、五部分 B3 与第九部分，规范第 00/10/30 章的普通状态所有权、订退生命周期、中文注释/格式和身体状态职责。复用 TestGameScope 的双 HeroState 与既有正式业务系统；Battle 的最小 HeroTestScope 增加两人装配选项，没有新增 scope、生产入口或第三套状态。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | HEAD 为 **e3440ca**，工作区干净。`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误、5 条既有框架警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet` 分别 **305/305、193/193、19/19**，均退出 0。日志位于 `.godot/validation/coop_b3_20261109/baseline_*.log` |
| B3-1 完成 | 同样四条命令全部退出 0，游戏构建 **0 错误、0 警告（增量）**，规则 **320/320、195/195、19/19**。日志 `b3_1_build.log`、`b3_1_profile.log`、`b3_1_battle.log`、`b3_1_level.log`。两个测试工程也完成编译、0 错误；本批没有中间编译失败或失败测试 |
| B3-2 最终构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、0 警告（增量）**，日志 `b3_2_build.log`；开工重编译时的 5 条既有框架弃用警告未处理 |
| B3-2 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，320/320**，失败 0、跳过 0，较 B2 增加 **15** 例；`b3_2_profile.log` |
| B3-2 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，195/195**，失败 0、跳过 0，较 B2 增加 **2** 例；`b3_2_battle.log` |
| B3-2 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0；`b3_2_level.log`。原有测试与断言保留，NU1900、CS8632/CS0649 既有警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b3_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**。保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、389 个 UID**，与 B2 相同；`b3_2_references.log` |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b3_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。两英雄分别建档/选角、进关、学技、保存返回、重读、加载取消和部分注册清理通过；坏文件、重复注册和旧作用域拒绝为预期注入。退出保留 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b3_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。技能/无双、拾取/出售、15 件穿戴、窗口与实体池复用、60/120Hz 坡地/平台、死亡/检查点、存档重入、备份恢复及加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入；退出保留 **20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、138 个 ObjectDB 实例、10 个资源占用**，以及既有 CanvasItem/GL 明细。无帧数强制退出参数，仍使用原 runner 的 900 秒超时，由完整烟测完成后退出 |
| 格式与边界 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：ProfileTests **5 份**、BattleTests **2 份**均退出 0，仅工作区既有加载警告。`git diff --check`、`git diff --cached --check` 均退出 0；7 份 C# 为 UTF-8/CRLF，无行尾空白。HEAD 保持 e3440ca，索引为空，11 份增量均未暂存；生产源码/资源、Framework/addons/Luban、生成物、源表、bytes、场景无差异 |

B3-1 七项验收与实际证据：

| 计划要求 | 案例与断言 |
| --- | --- |
| ① 两人装备、被动、Buff 隔离；RemoveSource 只影响本人 | `TwoHeroes_KeepEquipmentPassivesAndBuffSourcesIndependent` **2 例**，交换操作英雄；通过正式拾取/穿卸和 Upgrade 为双方建立同类装备、不同被动/同名 Buff 来源，检查本人的实际暴击、攻击及生命上限变化，移除 Buff/卸装后对方完整战斗属性、生命魔法与来源保持原样 |
| ② 两人的冻结、中毒、受击保护、恢复节拍隔离；死亡只清本人 | `TwoHeroes_AdvanceAndClearOnlyPersonalEffects` **4 例**：双方各自推进冻结和毒跳相位，死亡/Reset × 两个英雄方向，其他人的冻结剩时、毒跳数/相位、生命、受击保护、无双蓄量保持；死亡者效果/保护/无双清零。`TwoHeroes_KeepHitChainsAndProtectionClocksIndependent` **2 例**核对五次保护、连击断链与两个独立时钟；`TwoHeroes_KeepRecoveryValuesAndPhasesIndependent` **2 例**核对不同恢复数值、25/24 秒节拍、相位和个人 RemoveSource |
| ③ 无双蓄积、开启、衰减、消耗隔离 | Battle 的 `TwoHeroes_KeepGainActivationAndDrainIndependent` **2 例**：交换主要操作英雄，真实 RecordHit 记账、各自自然扣点小数相位、满值开启、未满拒绝、双方同时开启的小数耗点相位、黄球恢复和耗尽；另一人的状态与相位保持 |
| ④ 同时施放不覆盖，各扣本人蓝量 | `TwoHeroes_CastSpendCancelAndClearIndependently` **2 例**，两人真实登记/学习不同技能后同时激活，核对各自 ActiveSkillId、普通/延迟耗蓝、错误技能延迟扣费拒绝、取消/无敌及另一人再次起手的费用 |
| ⑤ 一人回池清登记后另一人仍可施放 | 同上 **2 例**调用与 AbstractHeroEntity.OnHide 相同的正式 `ClearSkillDefinitions(heroId)`，清理者正在施放且拥有无敌区间，核对本人登记/施放/无敌清除，另一人收招后可再次激活并只扣本人蓝量。B2 的原 `RegistrationAndClear_OnlyAffectRequestedHero` 保留。完整 UI 另验证真实单人实体池回收/复用；生产 OnHide 仍直接传 HeroId。本批不生成第二个实战实体，不把纯规则测试称为双实体实战 |
| ⑥ 重建后旧状态不被订阅更新，逐人对称退订 | `TwoHeroes_RestartDetachesAllOldCallbacksAndStates` **1 例**，连续重建两轮，每人核对三个旧委托已从成长/穿戴/被动的真实 Snapshot 中移除，新委托各注册 1 次；旧容器清空、旧状态不变；随后正式经验/换装/被动操作只更新新状态 |
| ⑦ 非阵容英雄登记拒绝 | `Registration_RejectsHeroOutsideRosterWithoutMutation` **2 例**，使用配置合法但阵容外的另一英雄，确认 ArgumentException、原登记/正在施放的编号与蓝量不变、没有创建额外 HeroState |

两个角色均作为被操作方和被保留方，而不是只验证 P1 修改。测试使用真实成长、被动、装备、技能配置与正式业务；Battle 无双仍复用已有零基础值夹具，只扩展到两份状态，不复制规则实现。

实际接口变化：

| 交付前 | 交付后 |
| --- | --- |
| 测试夹具 `HeroTestScope()` | `HeroTestScope(bool twoHeroes = false)`；默认行为保持，选项只在测试中创建两份 HeroState/技能书并共用正式系统 |
| TestGameScope、HeroModel、HeroState、HeroSystem、HeroStatusSystem、IHeroBody、SkillSystem | **本批全部无接口/源码变化**；TestGameScope 已能按阵容创建两份状态，直接复用 |

本批没有发现需要修改生产代码或改变计划的缺陷。B1 的逐人闭包/死亡 Reset 个人效果清理、B2 的个人登记清理均由新增案例检查。针对旧状态不变这一证据的局限，直接在现有测试内反射读取绑定 Snapshot：旧闭包会重新取 Architecture 当前模型，仅比较旧 HeroState 无法发现其泄漏；因此同时检查实际旧委托消失、新委托唯一。没有为观测增加生产成员、事件或测试辅助类型。

实际文件清单：**11 份修改（7 份测试 C#、4 份文档），无新增、无删除正式文件**。

| 所属目录 | 修改文件 |
| --- | --- |
| `Tests/ProfileTests/` | `PassiveSkillTests.cs`、`FreezeTests.cs`、`HurtProtectionTests.cs`、`RecoveryTests.cs`、`SkillSystemTests.cs` |
| `Tests/BattleTests/` | `HeroTestScope.cs`、`MusouTests.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |

生产代码、生成物、框架、源表、场景和存档版本均未变化。阶段 A 的个人档案/钱包/背包、v14 双人往返和原子恢复案例全部重跑；完整前端/UI 验证当前单人玩法、数值、存档往返与 B2 基线一致。双人规则证据不等于双人实战，第二实体、席位输入和双 HUD 仍待后续批次。正常数值全关人工实战本批未执行。烟测继续使用既有 runner 的案例专属 APPDATA/Validation 存档，不覆盖正式玩家存档；未执行 Git 写操作。

C1 须待用户接受 B3 并明确授权后开始，其范围为实体席位/出生参数，不提前实现 C2 的致死归属或 C4 的第二套键位。当前范围内无未完成项。

## 双人B2：技能登记与施放按英雄（2026-11-09）

用户授权在 B1 基础上完成 B2。SkillSystem 的技能登记改为 HeroId → 技能表，SelectSkills 只替换本人，AbstractHeroEntity.OnHide 只撤销本人的登记、施放和无敌区间；系统退出保留无参全局清理。仍只生成 P1，输入/HUD、技能行为、学技和存档格式 v14 保持原状；**本批完成后停在 B2，不进入 B3**。

依据：实施计划第一、二、五部分 B2 和第九部分；第 00 章所有权/生命周期，第 10 章方法命名、中文注释与格式，第 30 章招式与实体生命周期。沿用原 SkillSystem、HeroState 与 Dictionary，没有新增系统、类型、事件、生产观测入口或兼容重载。规范和计划只同步当前实现事实。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态与基线 | HEAD 为 fe0bdbc；B1 的 **99 份差异全部已暂存、无未暂存修改**。开工运行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误/0 警告（增量）；三套规则 **304/304、193/193、19/19**，均退出 0。日志 `.godot/validation/coop_b2_20261109/baseline_*.log` |
| 逐步编译 | B2 计划没有编号子任务；技能系统/实体清理实现后 `dotnet build Godot/GodotProject --no-restore -v quiet`：**0 错误、5 条既有框架警告**（`b2_system_build.log`）；夹具与 B2 回归同步后同命令：**0 错误、5 条既有框架警告**（`b2_consumers_build.log`）。本批没有中间编译错误或失败测试 |
| 最终游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、0 警告（增量）**，日志 `final_build.log`；重编译时的 5 条既有框架弃用警告未处理 |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，305/305**，失败 0、跳过 0（`final_profile.log`）。新增 1 项 B2 登记替换/清理回归；原 304 项及断言保留 |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0（`final_battle.log`）；保留既有 CS8632/CS0649 |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0（`final_level.log`）；测试工程 NU1900 网络警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b2_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**，保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、389 个 UID**（`final_references.log`），与 B1 相同 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b2_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。建档/选角、进关、地图学技、保存返回、重读、加载取消与部分注册清理通过；坏文件与重复注册为预期注入。退出保留 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b2_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。技能/无双、拾取出售、15 件穿戴、窗口与实体池复用、60/120Hz 坡地/平台、死亡/检查点、备份回退、存档重入及加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入；退出保留 **20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**以及既有 CanvasItem/GL 明细。使用原 runner 的 900 秒超时，由完整烟测完成后退出 |
| 格式与最终差异 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：GodotProject **2 份**、ProfileTests **3 份**均退出 0（仅测试工作区既有加载警告）。`git diff --check`、`git diff --cached --check` 均退出 0；5 份 C# 为 UTF-8/CRLF、无行尾空白。10 份增量均未暂存；执行中外部将 B1 提交为 53e9b11，经二进制差异 SHA-256 比对，与开工时暂存的 B1 完全一致，当前索引为空。未执行 Git 写操作；Framework/addons/Luban、生成物、源表、bytes、场景无本批修改 |

关键接口及行为对照：

| B1 交付状态 | B2 交付状态 |
| --- | --- |
| `Dictionary<SkillId, SkillDefinition> _skillDefinitions` | `Dictionary<int, Dictionary<SkillId, SkillDefinition>> _skillDefinitions`，按 HeroId 分表 |
| `void SelectSkills(int heroId, params SkillDefinition[] definitions)` 清空共享登记 | 签名不变；先完整校验，再只取消该 heroId 的施放、替换其登记；同组重登仍不打断 |
| `void ClearSkillDefinitions()` 清全部；实体回池也调用此方法 | 新增 `void ClearSkillDefinitions(int heroId)` 取消本人并移除本人表；无参版本保留，生产调用仅为 OnDeinit |
| `Type GetBodyStateType(int heroId)`、`bool IsAvailable(int heroId, SkillId id)` 读共享表 | 签名不变，读取本人表；没有登记时按既有查询结果返回 null/false |
| `bool CanAfford(int heroId, SkillId id)` 只比较蓝量 | 签名不变，先确认本人表登记，再比较本人蓝量；不能因他人登记或蓝量足够而误报可用 |
| `bool TryActivate(int heroId, SkillId id, ISkillActor actor)` / `bool TrySpendActivatedMana(int heroId, SkillId id)` / `bool TryActivateInput(int heroId, ISkillActor actor, HeroActionData data)` | 签名不变，起手条件、延迟耗蓝和输入枚举均使用本人表；输入仍按登记顺序，失败不排队、不补发 |
| `void CancelSkill(int heroId)` / `void EndSkill(int heroId, SkillId id)` / `void GrantInvulnerability(int heroId, double from, double duration)` / `bool IsInvulnerable(int heroId, double now)` | 签名及按本人 HeroState 处理的行为不变；清除后仍可取消/退出，不人为用登记存在性阻挡清理或独立无敌区间 |
| `AbstractHeroEntity.OnHide` 调 `ClearSkillDefinitions()` | 调 `ClearSkillDefinitions(HeroId)`，真实回池不会清掉另一人的登记或施放 |
| `HeroModel`、`HeroState`、`HeroSystem`、`HeroStatusSystem`、`IHeroBody` | **本批均无接口/源码变化**，沿用 B1 的 GetState(heroId)、普通状态和显式编号契约 |

B1 的两处缺陷已在上一批修正，本批没有重复实现或改动：①HeroSystem 为每位英雄保存三个实际闭包、以同一编号/委托对称退订；②Damage 死亡与 Reset 使用 ClearAll(heroId)，只清本人状态效果。其证据见下方 B1 记录；本批复跑已有规则和单人流程。B2 新修复的是**实体回池清全表**，清理边界已与逐人状态对应。

实际遇到的调用与处理：

- 所有原校验保留：参战阵容包含该编号、非空数组、每个定义非 null、具体 AbstractHeroSkillState 类型、配置归属、技能编号唯一、输入动作非空白且本人组内唯一。先完整校验再修改；不加默认英雄、不增加第二份学习状态。
- 个人表由调用方数组内的不可变 SkillDefinition 复制建立，SelectSkills 仅替换该 heroId 的字典；相同定义序列保持当前施放，两个英雄允许相同输入动作名。TryActivate 在 IsAvailable 确认本人登记后直接读取其定义。
- TestGameScope.RestartHero 在结束数值模型前按 Profile.HeroIds 逐人清理，模拟实体逐人退场。SkillLearningTests 的地图清理也改用个人入口，保留地图无 HeroModel 时的既有行为。SkillStateTests、SkillReuseTests 已在 B1 显式传 HeroId，经核对本批无需额外修改；三套测试全部复跑。
- 新增 `RegistrationAndClear_OnlyAffectRequestedHero`，在现有 TestGameScope 和 SkillSystemTests 内使用正式系统，覆盖两人同名动作、第二人登记/替换不改第一人施放与蓝量、个人清理同时清登记和无敌、第一人之后仍能通过输入继续施放、系统 Deinit 全局清理。未增加测试辅助类型或生产入口；B3 的全面双人属性/效果/无双及实际双实体回池验收仍待授权。

本批文件清单：**10 份修改（2 份生产 C#、3 份测试 C#、5 份文档），无新增、无删除正式文件**。只列相对开工时已暂存 B1 的增量：

| 所属目录 | 修改文件 |
| --- | --- |
| `Godot/GodotProject/TheGame/GameScripts/` | `Systems/SkillSystem.cs`、`Entity/Heroes/AbstractHeroEntity.cs` |
| `Tests/ProfileTests/` | `SkillSystemTests.cs`、`SkillLearningTests.cs`、`TestGameScope.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |

本批没有持久结构变化，存档继续 v14，不需要因 B2 重建现有 v14 档案。仍只装配一个实战英雄，一套输入/HUD；B3 须待用户接受 B2 并授权后补齐全面隔离证据和阶段 A～B 回归，本批不提前开始。所有引擎验证使用原 runner 的案例专属 APPDATA 与 Validation 存档，未覆盖正式玩家存档。正常数值全关人工实战、双人实战均未执行，退出时既有引擎资源诊断单独记录。

## 双人B1：本局状态按英雄承载（2026-11-09）

按已授权目标与实施计划 B1-1～B1-6 顺序完成本局状态迁移，并响应用户追加的全项目业务方法命名要求。HeroModel 只保存 HeroId → HeroState，个人系统、实体、身体/技能状态和 UI 按本人 HeroId 读写；仍只生成 P1，保留一套输入/HUD。**当前停在 B1 等待审查，不进入 B2。** 存档保持 v14，本批没有持久结构变化，不要求重建现有 v14 开发档；v13 及更早档的既有重建规则不变。

依据：计划第一、二、五、九部分，规范第 00 章的普通子数据/单根 Architecture/所有权，第 10 章的命名与中文注释，第 30 章的实体与战斗职责、第 70 章的档案边界。用户明确授权同步规范和计划；本批同步当前归属与 GetState 等签名，没有改变后续批次方向。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误；三套规则测试 **304/304、193/193、19/19**。日志位于 `.godot/validation/coop_b1_20261108/baseline_*.log` |
| 子任务编译 | 每步执行 `dotnet build Godot/GodotProject --no-restore -v quiet`；B1-1～B1-6 错误依次为 **0、597、588、589、444、0**，各步最终日志为同目录 `b1_1_build.log`～`b1_6_build.log`。中间迁移失败按授权保留记录，不加兼容转发；B1-6 清零 |
| 最终游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架弃用警告**。已包含最终方法改名，日志 `renamed_build.log` |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，304/304**，失败 0、跳过 0，日志 `renamed_profile.log` |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0，日志 `renamed_battle.log` |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0，日志 `renamed_level.log`。测试数量与基线一致，保留 NU1900、CS8632/CS0649，未删减断言 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b1_final_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**。保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、389 个 UID**，日志 `renamed_references.log`。比 A3 增加 HeroState 的脚本 UID |
| 最终前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b1_final_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。覆盖选角、进关、装备/被动/成长观察、保存返回、重读、加载取消与部分注册清理；三个实际回调均验证进入时 1 次、退出后 0 次。旧作用域拒绝、坏文件、重复注册为预期注入；退出有 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 最终 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b1_final_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。包含全部 18 处命名整理后的版本；验证技能/无双、拾取/出售、15 件穿戴、窗口与实体复用、60/120Hz 坡地及平台、死亡/检查点、存档重入与备份回退、加载取消和失败清理。预期坏文件与 HUD 打开失败注入保留；退出保留 **20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**诊断，以及既有 CanvasItem/GL 纹理明细。最终使用原 runner 的 900 秒超时，由烟测完成后退出，不以帧数上限截断 |
| 格式 | 使用 `$verifyFiles = @(git diff HEAD --name-only -- '*.cs' \| Where-Object { $_.StartsWith('<工程目录>/') } \| ForEach-Object { (Resolve-Path -LiteralPath $_).Path })` 取得各工程的明确文件清单，再运行 `dotnet format whitespace <csproj> --no-restore --include $verifyFiles --verify-no-changes`；GodotProject **70 份**、ProfileTests **13 份**、BattleTests **5 份** C# 均退出 0，仅测试工作区既有加载警告。88 份 C# 均为 UTF-8/CRLF，无行尾空白 |
| 差异与边界 | `git diff --check`、`git diff --cached --check` 均退出 0；完整源文件对比证明 HeroState 除类名/说明、只读 HeroId 与构造外和迁移前 HeroModel 逐字一致。只读目录、生成代码/资源、源表、bytes、场景无差异，新增脚本的 .cs.uid 由编辑器正常生成。开工仅两份文档已暂存，执行中外部又暂存 B1 的 74 份差异；命名整理前后索引指纹一致，未执行任何 Git 写操作 |

关键接口前后对照（表内省略访问级别，未变化的参数与默认值仍列出）：

| 所属类型 / 修改前 | 修改后 |
| --- | --- |
| HeroModel 直接持有单个英雄全部数值、效果、计时与施放字段 | 普通 `HeroState(int heroId)` 持有原字段与嵌套 StatusEffectKind/StatusEffectState；HeroId 为构造时确定的只读属性，其他初值/计算/方法体保持原样 |
| 无本局状态索引入口 | `IReadOnlyDictionary<int, HeroState> Heroes`、`HeroState GetState(int heroId)`、`internal HeroState CreateState(int heroId)`、`internal void ClearStates()`；无 Remove、默认英雄或标量转发 |
| IHeroBody 无英雄编号 | 新增 `int HeroId { get; }`，由实体和三处假实现提供 |
| HeroSystem：`float MoveSpeedMultiplier` | `float GetMoveSpeedMultiplier(int heroId)` |
| HeroSystem：`float JumpSpeedMultiplier` | `float GetJumpSpeedMultiplier(int heroId)` |
| HeroSystem：`float GravityMultiplier` | `float GetGravityMultiplier(int heroId)` |
| HeroSystem：`float AttackPowerMultiplier` | `float GetAttackPowerMultiplier(int heroId)` |
| HeroSystem：`bool Damage(int amount)` | `bool Damage(int heroId, int amount)` |
| HeroSystem：`void Heal(int amount)` | `void Heal(int heroId, int amount)` |
| HeroSystem：`void RecordHurt()` | `void RecordHurt(int heroId)` |
| HeroSystem：`void AdvanceHurtProtection(float delta)` | `void AdvanceHurtProtection(int heroId, float delta)` |
| HeroSystem：`int AdvanceRecovery(float delta)` | `int AdvanceRecovery(int heroId, float delta)` |
| HeroSystem：`bool TrySpendMp(int amount)` | `bool TrySpendMp(int heroId, int amount)` |
| HeroSystem：`void RestoreMp(int amount)` | `void RestoreMp(int heroId, int amount)` |
| HeroSystem：`void Reset(int heroId, int level, ActorBaseAttributes baseAttributes, EquipmentData weaponData = null, EquipmentData armorData = null, EquipmentData accessoryData = null, EquipmentData treasureData = null)` | `void Reset(int heroId, int level, ActorBaseAttributes baseAttributes, EquipmentData weaponData = null, EquipmentData armorData = null, EquipmentData accessoryData = null, EquipmentData treasureData = null)` |
| HeroSystem：`void SetGrowth(ActorBaseAttributes baseAttributes, int heroId, int level, EquipmentData weaponData, EquipmentData armorData, bool refill, EquipmentData accessoryData = null, EquipmentData treasureData = null)` | `void SetGrowth(int heroId, ActorBaseAttributes baseAttributes, int level, EquipmentData weaponData, EquipmentData armorData, bool refill, EquipmentData accessoryData = null, EquipmentData treasureData = null)` |
| HeroSystem：`void SetSource(AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)` | `void SetSource(int heroId, AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)` |
| HeroSystem：`bool RemoveSource(AttributeSourceId source)` | `bool RemoveSource(int heroId, AttributeSourceId source)` |
| HeroSystem：`void RecordHit(DamageResult result)` | `void RecordHit(int heroId, DamageResult result)` |
| HeroSystem：`void ResetWs(int max, float decayPerSecond, float drainPerSecond)` | `void ResetWs(int heroId, int max, float decayPerSecond, float drainPerSecond)` |
| HeroSystem：`void AddWs(int amount)` | `void AddWs(int heroId, int amount)` |
| HeroSystem：`void AddWsFromPickup(int amount)` | `void AddWsFromPickup(int heroId, int amount)` |
| HeroSystem：`bool TryActivateWs()` | `bool TryActivateWs(int heroId)` |
| HeroSystem：`void SetControlsEnabled(bool enabled)` | `void SetControlsEnabled(int heroId, bool enabled)` |
| HeroSystem：`void AdvanceWs(float delta)` | `void AdvanceWs(int heroId, float delta)` |
| HeroSystem：`void ClearWs()` | `void ClearWs(int heroId)` |
| HeroStatusSystem：`bool IsFrozen` | `bool IsFrozen(int heroId)` |
| HeroStatusSystem：`bool IsPoisoned` | `bool IsPoisoned(int heroId)` |
| HeroStatusSystem：`void ApplyFreeze(float seconds)` | `void ApplyFreeze(int heroId, float seconds)` |
| HeroStatusSystem：`void AdvanceFreeze(float delta)` | `void AdvanceFreeze(int heroId, float delta)` |
| HeroStatusSystem：`void ApplyPoison(AttackConfig attack)` | `void ApplyPoison(int heroId, AttackConfig attack)` |
| HeroStatusSystem：`int AdvancePoison(float delta)` | `int AdvancePoison(int heroId, float delta)` |
| SkillSystem：`Type BodyStateType` | `Type GetBodyStateType(int heroId)` |
| SkillSystem：`bool CanAfford(SkillId id)` | `bool CanAfford(int heroId, SkillId id)` |
| SkillSystem：`bool TryActivate(SkillId id, ISkillActor actor)` | `bool TryActivate(int heroId, SkillId id, ISkillActor actor)` |
| SkillSystem：`bool TrySpendActivatedMana(SkillId id)` | `bool TrySpendActivatedMana(int heroId, SkillId id)` |
| SkillSystem：`bool TryActivateInput(ISkillActor actor, HeroActionData data)` | `bool TryActivateInput(int heroId, ISkillActor actor, HeroActionData data)` |
| SkillSystem：`void CancelSkill()` | `void CancelSkill(int heroId)` |
| SkillSystem：`void EndSkill(SkillId id)` | `void EndSkill(int heroId, SkillId id)` |
| SkillSystem：`void GrantInvulnerability(double from, double duration)` | `void GrantInvulnerability(int heroId, double from, double duration)` |
| SkillSystem：`bool IsInvulnerable(double now)` | `bool IsInvulnerable(int heroId, double now)` |
| HeroStatusSystem：`void ClearAll()` 原为单人 | 新增 `void ClearAll(int heroId)` 清本人；无参方法遍历全部状态，仅用于系统退出 |
| AbstractHeroEntity：`private HeroModel Attributes` | `private HeroState Attributes => this.GetModel<HeroModel>().GetState(HeroId)`；反射契约名 Attributes 保留，OnShow 先检查 Heroes.ContainsKey |

HeroSystem 私有 ClearHurtProtection、ReplacePassiveSource、ReplaceEquipmentSources、ApplyFinalAttributes、SetWsState 接收 HeroState，Recalculate 与三个来源回调显式传 heroId。Reset 原本已有 heroId，签名保持；SetGrowth 将原第二位 heroId 移到首位。五个 AttributeSourceId 常量仍用第二项 0，各英雄自己的 Sources 字典提供隔离。

用户追加命名整理（全项目自有 C#、测试与工具入口检索，按编译符号替换，18 处方法声明；不改行为、不留旧名转发）：

| 原名 | 现名与用途 |
| --- | --- |
| HeroModel.Get / Create / Clear（B1 原计划临时签名） | GetState / CreateState / ClearStates，明确状态对象 |
| ItemAppearancesResource.Get（EquipmentId / PickupId 两重载）、私有 Find | GetAppearance / FindAppearance，明确外观资源 |
| EquipmentAttributes.Get、ActorBaseAttributes 的 AttributeExtensions.Get | GetValue，与既有 WithValue 成对，读取指定属性数值；不修改生成类型 |
| ExperienceCurveUtility.Add / Evaluate | AddExperience / EvaluateProgress，明确经验累计与等级进度计算 |
| NodePool.Get<T> / Get、私有 GetInternal | RentNode<T> / RentNode / RentNodeInternal，明确取池并挂接；避免与 Godot.Node.GetNode 混淆 |
| B1 新参数化方法 MoveSpeedMultiplier / JumpSpeedMultiplier / GravityMultiplier / AttackPowerMultiplier / BodyStateType | 各补 Get 前缀，仍为带 heroId 的原查询；IHeroBody 上的只读属性名保留 |

第 10 章新增方法语义规则，计划 B1 与第九部分同步。集合 Add/Remove/Clear、控件自身 Refresh/Clear、静态类型工厂 Create、池回调 OnGet/OnRelease、接口/重写及生成 API 保留既有契约；没有把简单动词机械扩成冗长名称。生产与测试中已无自有裸 Get 声明；Luban 表和 Godot/GGF 查询仍调用原 API。检索 `NodePool.Instance.Get`、`GetInternal`、字面量方法名和旧 Evaluate 调用无残留。

本批修复的两处多人缺陷：

- 三个来源订阅按每个 heroId 建立独立闭包，按 priority 100 注册并把实际委托存入 _subscriptions；退出逐个按同一编号/委托退订，清空订阅与状态容器。复用了既有 Bindable 注册机制。
- Damage 死亡分支与 Reset 使用 ClearAll(heroId)，只清本人的冰冻/中毒；无双和受击保护也按该英雄清理。全局 ClearAll 仅保留在退出路径。

计划未单列的调用与实际处理：

- SkillSystem 的 SelectSkills 保持原签名，内部 CancelSkill 改传其已有 heroId；全局 ClearSkillDefinitions 暂遍历已存在状态取消施放，仍清共享登记表。共享表及 AbstractHeroEntity.OnHide 的全局清理留给 B2，没有提前按英雄拆登记。
- 原 SmokeInspection 按 handler.Target 等于 HeroSystem 统计订阅，闭包迁移后这一观测失效。复用原方法的 object 参数接受实际 Delegate，既有对象匹配保留；前端取得 _subscriptions 内的三个委托，断言各自只订一次且退订为零，没有生产测试入口。
- 窗口关闭已把 _formData/_inventoryData 置空，退订必须使用关闭前捕获的局部 data.Hero.HeroId；刷新仍用本周期字段。早期前端因此失败，修正后最终完整前端通过。
- UI 实体复用原断言把实体的 Attributes 与 HeroModel 直接比较；迁移后分别验证容器仍为原容器、Attributes 仍为容器 GetState(heroId) 的同一对象，同时保留实体、无双和资源断言。早期 UI 在此失败，修正后改名前完整 UI 以及最终版本均重新运行。
- 新建 HeroState.cs 已加入 ProfileTests 显式编译项；BattleTests 的 Models 通配无需改。三处 IHeroBody 假实现补实际夹具编号；HeroTestScope、SkillScope、TestGameScope 的公开 Hero 为 P1 HeroState，私有容器负责注册和重建，没有新增测试辅助类型。Debug 仅方法内局部复用 heroId，无共享取 P1 辅助方法。
- 机械消费者还包括身体/技能、唐僧动画入口、独立技能命中、实际受击目标的冻结/中毒、关卡拾取、库存面板、Debug 反射与烟测；独立技能按 Source.OwnerEntityId 解析本体英雄记账，未改命中规则。只有 B1 负责的两个 P1 占位被清除；龙宫转场仍按计划显式 P1，其他后续批次占位保持。
- 中间构建、Profile 夹具漏传参数、前端闭包观察和 UI 身份比较的失败均有日志，已全部解决；一次旧二进制前端运行发生在构建失败后，未作为通过证据。最终证据为 named 系列及 renamed 日志。
- 改名后首次完整 UI 案例 `coop_b1_named_ui_20261109` 使用 `--quit-after 30000`，退出 0 但停在大猩猩截图阶段，没有最终 SMOKE PASS；强制结束时出现 BattleForm.Unbind 的架构已结束诊断。因此该次不计完整通过，保留日志并另以不带帧数退出参数的案例重跑，仍使用原 runner 的 900 秒超时，不改任何断言。

实际文件清单：**99 份相对 HEAD 的差异（88 份 C#、9 份文档、1 份 csproj、1 份 UID）**，其中 2 份新增（HeroState.cs 及其 Godot 扫描生成的 .cs.uid），无删除。包含用户原有计划/工作笔记修订；其余为本批实现、命名调用方与记录。

| 所属目录 | 文件（新增用“新增”标记） |
| --- | --- |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/00-Architecture.md`、`ProjectGuidelines/10-CSharp.md`、`ProjectGuidelines/30-EntitiesAndCombat.md`、`ProjectGuidelines/60-GameplayModules.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |
| `Godot/GodotProject/TheGame/GameScripts/` | `Entity/Drops/GroundItemNode.cs`、`Entity/Effects/SkillEffectNode.cs`、`Entity/Enemies/AbstractEnemyEntity.Attacks.cs`、`Entity/Enemies/AbstractEnemyEntity.cs`、`Entity/Enemies/Bosses/zm1_05_qixing_cave/LionCamelPoisonCloudNode.cs`、`Entity/Enemies/Bosses/zm1_10_bull_demon_valley/BullDemonHeartEntity.cs`、`Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`、`Entity/FloatingText/FloatingTextPresenter.cs`、`Entity/Heroes/AbstractHeroEntity.Capture.cs`、`Entity/Heroes/AbstractHeroEntity.Skills.cs`、`Entity/Heroes/AbstractHeroEntity.cs`、`Entity/Heroes/Body/Core/AbstractHeroBodyState.cs`、`Entity/Heroes/Body/Core/AbstractHeroSkillState.cs`、`Entity/Heroes/Body/Core/IHeroBody.cs`、`Entity/Heroes/Tangseng/Skills/TangsengIceDragonState.cs`、`Entity/Heroes/Tangseng/TangsengEntity.AnimationEvents.cs`、`Entity/Heroes/Tangseng/TangsengEntity.cs`、`Entity/Heroes/Wukong/Skills/WukongBlazingFlashState.cs`、`Entity/Heroes/Wukong/Skills/WukongFireDemonState.cs`、`Entity/Heroes/Wukong/Skills/WukongFlameThrustState.cs`、`Entity/Heroes/Wukong/WukongEntity.cs`、`Entity/Level/LevelViewController.Entrance.cs`、`Entity/Level/LevelViewController.Loot.cs`、`Entity/Level/LevelViewController.cs`、`Models/HeroModel.cs`、`Models/HeroState.cs`（新增）、`Models/HeroState.cs.uid`（新增）、`Models/Profile/EquipmentAttributes.cs`、`Models/Profile/ItemAppearancesResource.cs`、`Systems/Combat/AttributeExtensions.cs`、`Systems/ConfigurationSystem.cs`、`Systems/Hero/HeroStatusSystem.cs`、`Systems/Hero/HeroSystem.cs`、`Systems/LevelSystem.cs`、`Systems/ProfileSystem.cs`、`Systems/SaveSystem.cs`、`Systems/SkillSystem.cs`、`UI/BattleForm/BattleForm.Logic.cs`、`UI/InventoryForm/Components/InventoryBagPanel.cs`、`UI/InventoryForm/Components/InventoryCharacterPanel.cs`、`UI/InventoryForm/InventoryForm.Logic.cs`、`UI/MapForm/Components/LevelGuideTooltip.cs`、`Utilities/Utility/ExperienceCurveUtility.cs`、`Utilities/Utility/SaveDataUtility.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `EnemyAiSmokeScenario.cs`、`FrontendSmokeScenario.cs`、`LevelSmokeScenario.cs`、`SmokeInspection.cs`、`UiSmokeScenario.BatCave.cs`、`UiSmokeScenario.Bosses.cs`、`UiSmokeScenario.Bull.cs`、`UiSmokeScenario.CombatFeedback.cs`、`UiSmokeScenario.Dragon.cs`、`UiSmokeScenario.DragonRules.cs`、`UiSmokeScenario.EastSea.cs`、`UiSmokeScenario.EnemyFidelity.cs`、`UiSmokeScenario.Equipment.cs`、`UiSmokeScenario.FireDemon.cs`、`UiSmokeScenario.Garuda.cs`、`UiSmokeScenario.HeroPresentation.cs`、`UiSmokeScenario.Inventory.cs`、`UiSmokeScenario.Passives.cs`、`UiSmokeScenario.Qixing.cs`、`UiSmokeScenario.Recovery.cs`、`UiSmokeScenario.Sea.cs`、`UiSmokeScenario.Shark.cs`、`UiSmokeScenario.Skills.cs`、`UiSmokeScenario.StatusEffects.cs`、`UiSmokeScenario.Tangseng.cs`、`UiSmokeScenario.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/` | `ObjectPool/NodePool.cs` |
| `Tests/BattleTests/` | `HeroBodyTests.cs`、`HeroModelTests.cs`、`HeroTestScope.cs`、`MusouTests.cs`、`SkillReuseTests.cs` |
| `Tests/ProfileTests/` | `EquipmentTests.cs`、`ExperienceCurveTests.cs`、`FreezeTests.cs`、`HurtProtectionTests.cs`、`LevelRewardTests.cs`、`MusouTests.cs`、`PassiveSkillTests.cs`、`PoisonTests.cs`、`ProfileTests.cs`、`ProfileTests.csproj`、`RecoveryTests.cs`、`SkillStateTests.cs`、`SkillSystemTests.cs`、`TestGameScope.cs` |


当前范围内无未完成项。B2 必须等待用户接受 B1 并授权，处理共享技能登记和一人回池清全表；双人隔离专项按既定 B3 补充，本批未运行双人实战或正常数值全关人工验收。引擎退出诊断保持既有边界，不处理只读依赖；烟测使用 run_godot.py 的案例专属 APPDATA 与 Validation 存档，不覆盖正式玩家存档。未执行 Git 写操作。

## 双人A3：档案规则证据与存档格摘要（2026-11-08）

按用户授权完成 A3-1～A3-2：补齐双人个人数据隔离、完整 v14 往返及恢复前校验的规则证据；六格目录发布按席位排列的只读摘要，catalogue 名以单空格连接全员配置名称。**当前停在 A3 等待用户审查，B1 未开始。** 依据为实施计划 A3、规范第 00 章的状态所有权、第 10 章的只读值/中文注释和格式、第 70 章的目录与检查点契约。

标题宽度曾触发计划的停止条件，现已按用户裁决解决：单/双人统一只显示角色名，不显示等级。项目实际 NameCn 为“悟空”“唐僧”，不新增简称映射；当前双人标题“悟空 · 唐僧”宽 **132px**，加入两人的最低等级仍需 **220px**。沿用 **22 号字、187×30px 标题区**，正文终点早于删除按钮；单人显示“悟空”，双人显示“悟空 · 唐僧”，均不显示等级，日期样式不变。没有修改字体、场景、节点或布局。造三存档格以角色名作为标题的既有官方依据见 OfficialReference；双人摘要始终保留双方等级，只在该窄标题中省略。

开工时 A2 的 48 份文件已由用户暂存，工作区无未暂存差异；执行期间其提交为 `c2f74db`，已按 SHA-256 核对提交差异与开工暂存内容完全一致。本轮 A3 改动均保留在未暂存工作区，未执行 Git 写操作。**存档格式仍为 v14，本批不要求重建现有 v14 档。** v13 及更早开发档仍按现有契约拒绝，无迁移分支。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误、增量 0 警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`：**292/292**。日志在 `.godot/validation/coop_a3_20261108/baseline_*.log` |
| 子任务与最终构建 | A3-1、A3-2 与最后一次源码修改后均执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：均**退出 0、0 错误、5 条既有框架弃用警告**。日志 `a3_1_build.log`、`a3_2_build.log`、`final_build.log`；统一纯名称后的最终日志为 `final_names_only_build.log` |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，304/304**，失败 0、跳过 0。A3-1 为 303/303，A3-2 增加摘要只读发布证据；相比开工共增加 **12 例**，原有断言保留。最终日志 `a3_2_profile.log` |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0；既有 CS8632/CS0649 警告保留，日志 `final_battle.log` |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0；各测试工程既有 NU1900 警告保留，日志 `final_level.log` |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_names_only_scan_20261108 --headless --editor --build-solutions --quit --no-window`：**退出 0**，扫描及构建完成；既有 ComponentInsoector 的 RefCounted/free 退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、388 个 UID** 通过 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_names_only_frontend_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄分别建档、目录读取、保存返回、磁盘重读、旧作用域拒绝、加载取消与部分注册清理通过。坏文件与重复注册为预期注入；退出保留 **4 个 ObjectDB 实例、1 个资源占用**诊断 |
| 双人摘要与删除专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_names_only_slots_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --save-delete-only`：**退出 0**，出现 `SMOKE-SAVE-ROSTER PASS`、`SMOKE-SAVE-DELETE PASS` 及最终 `SMOKE PASS`。实际写盘并重读两种席位顺序，核对全员名称/等级、catalogue 名、单/双人统一纯名称标题与删除按钮安全间距；确认取消/确认、主备文件删除、其他格不变、同格重建和过期确认拒绝。退出保留 **4 个 ObjectDB 实例、2 个资源占用**诊断 |
| 原生宽度与画面 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_summary_final_measure_20261108 --headless --script res://.godot/validation/coop_a3_20261108/measure_save_summary.gd --no-window`：**退出 0**，使用原场景实际字体测得名称 132px、最低等级双人 220px、双 55 级 254px；标题到删除按钮的可用距离 164px。已查看 `.godot/validation/frontend/save_slots_coop_1.png` 与 `save_slots_coop_2.png`：分别显示“悟空 · 唐僧”“唐僧 · 悟空”，同屏单人统一显示“唐僧”、无截断或删除按钮遮挡 |
| 格式 | 实际执行 `$gameFiles = @(git diff --name-only -- '*.cs' \| Where-Object { $_.StartsWith('Godot/GodotProject/') } \| ForEach-Object { (Resolve-Path -LiteralPath $_).Path }); dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include $gameFiles --verify-no-changes`；测试用 `$profileFiles = @(git diff --name-only -- 'Tests/ProfileTests/*.cs' \| ForEach-Object { (Resolve-Path -LiteralPath $_).Path }); dotnet format whitespace Tests/ProfileTests/ProfileTests.csproj --no-restore --include $profileFiles --verify-no-changes`：**4 份生产/Debug、3 份测试均退出 0**，仅测试工作区既有加载警告 |
| 最终差异 | `git diff --check` 退出 0；**7 份 C# 均为 UTF-8/CRLF**；`rg -n 'HeroName|slot\.Level' Godot/GodotProject/TheGame Tests --glob '*.cs'` 无旧摘要标量消费者；只读目录、生成物、源表、bytes、场景无差异，12 份改动均未暂存 |

A3-1 必需规则与实际证据：

| 计划要求 | 实际覆盖 |
| --- | --- |
| 六类个人状态完全隔离 | 新增 `PersonalData_ChangesOnlyTheRequestedHero` 两个方向，正式业务分别修改成长、钱包、背包、穿戴、主动/被动；另一人的完整英雄 DTO 保持原样 |
| 全档实例唯一 | 复用 A2 `PersonalAssets_RejectInvalidInstanceOwnership` 跨背包重复案例，新增 case 6 覆盖一人背包与另一人穿戴占用同一实例；已有运行时拾取/穿戴排重保留 |
| 非法阵容构造拒绝 | 复用 `Profile_RejectsInvalidHeroSets`，已覆盖空、超过两人、重复、零和负编号及非法席位 |
| 完整 v14 双人往返 | 新增 `TwoHeroCheckpoint_RoundTripsAllPersonalData` 两种席位顺序，实际 Capture → JSON → Validate → RestoreCheckpoint；包括不同余额/经验、各人背包、四槽穿戴/随机属性、不同主动和被动等级、共享进度/旗幡，并比较完整捕获结果 |
| 第二人非法时第一人不变 | 新增 `RestoreCheckpoint_RejectsInvalidSecondHeroWithoutMutation` 六例：余额、经验、引用不存在、跨人重复归属、错误英雄主动技能、非法被动等级；全部状态与调用前一致、第一人绑定零通知、容器身份不变 |
| 孤立实例拒绝 | 复用 `PersonalAssets_RejectInvalidInstanceOwnership` case 4，直接调用 ReadItems 拒绝池中无归属实例 |

关键接口变化：

| 修改前 | 修改后 |
| --- | --- |
| `SaveSlotInfo(int slotNumber, long unitId, SaveSlotState state, string heroName = "", int level = 0, DateTimeOffset? savedAt = null)` | `SaveSlotInfo(int slotNumber, long unitId, SaveSlotState state, IEnumerable<SaveHeroSummary> heroes = null, DateTimeOffset? savedAt = null)` |
| 存档格的单个 `HeroName` / `Level` 标量 | `IReadOnlyList<SaveHeroSummary> Heroes`，构造时复制，只读发布并保留席位顺序 |
| 无参战者摘要值类型 | 按计划在同文件新增 `readonly record struct SaveHeroSummary(string Name, int Level)`，不写入存档 DTO |
| 读取/写入/窗口生命周期公共方法 | 签名均不变；目录读取由首位改为全员摘要，写入 catalogue 名由首位改为全员 NameCn 单空格连接 |

本批文件清单：**12 份修改（7 份 C#、5 份文档），无新增/删除正式文件**。唯一新增类型为计划要求的同文件 SaveHeroSummary，没有新的系统、接口、配置或测试辅助类型。

| 所属目录 | 修改文件 |
| --- | --- |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |
| `Godot/GodotProject/TheGame/GameScripts/` | `Models/Save/SaveSlotInfo.cs`、`Systems/SaveSystem.cs`、`UI/SaveForm/SaveForm.Logic.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `FrontendSmokeScenario.cs` |
| `Tests/ProfileTests/` | `EquipmentTests.cs`、`ModelTransactionTests.cs`、`ProfileTests.cs` |

实际遇到的情况及处理：

- 标题宽度与计划原示例冲突时先报告并停止；用户明确授权按官方风格缩减文字，并最终要求单/双人统一；全部标题只显示角色名，不显示等级。实际使用 NameCn，删除单双人显示分支，未硬编码角色简称或组合，没有运行时字体缩放/宽度回退机制；完整名称已能在现有位置显示。
- A3-1 首次两例往返测试因新增夹具余额不足以购买二级被动而失败；仅增加该往返夹具初始余额，保留扣费规则及所有断言，随后 303/303、最终 304/304 通过。
- 计划未单列的机械消费者为 `FrontendSmokeScenario` 的唐僧摘要断言，改为读取唯一的 Heroes 摘要。双人建档入口尚未开发，因此在既有删除专项内经反射调用既有私有 WriteSnapshotAsync 写入双人快照，随后走正式目录重读与原生窗口显示；没有生产测试入口，也不装配第二个战斗英雄。普通前端不执行删除分支，本批另跑 `--save-delete-only`，没有用通用尾句替代专项证据。
- A3-2 增加 `SaveSlotSummary_PreservesOrderAndOwnsReadonlySnapshot`，验证来源集合修改和消费者写入均不能改变已发布摘要，空格和不可读格为空摘要。

用户最终要求单/双人统一后，已删除专门的显示分支，两个模式均只以同一条 string.Join 按席位拼接名称；最终构建、编辑器扫描、完整前端与存档/删除专项均重新通过，截图已核对。

本批要求已完成，无范围内遗留。B1 须在用户接受 A3 并授权后开始；仍为单个 HeroModel、一套输入/HUD，当前建档/关卡装配只有一人。引擎退出诊断沿用既有边界保留。所有烟测使用 runner 的案例专属 APPDATA，未覆盖正式玩家存档。完整战斗 UI 烟测和双人实战本批未执行；本批按 A3 计划验证档案、存档页及前端流程，双人战斗留给后续阶段。

## 双人A2：灵魂与背包归个人（2026-11-08）

A1 已由用户接受并提交为 `d2c3d13`；本轮按实施计划顺序完成 A2-1～A2-4，钱包、背包、拾取、穿卸、出售和学技均显式指定 HeroId，唯一奖励与实例排重覆盖全档。本局仍只装配 P1，单个 HeroModel、输入和 HUD 保持原状。构建、规则测试、扫描、引用、完整前端/UI 烟测与牛心唯一奖励专项均通过；当前停在 A2 等待用户审查，不进入 A3。

依据为实施计划 A2、规范第 00 章的状态所有权与订退生命周期、第 10 章的中文注释/格式、第 70 章的存档与检查点规则。用户已授权同步规范和计划原文；本批更新其当前事实，不提前实现后续阶段。

存档 v13 → v14：删除根级共享余额；每个 `HeroSaveData` 必需保存 `Soul` 与 `BagInstanceIds`，`EquipmentItems` 改为包含全部背包和穿戴的全档实例池。同一实例必须且只能被一处引用，拒绝缺失引用、重复归属、孤立实例、个人超容量及负余额。仍只接受当前格式，**旧 v13 及更早开发档需重建**，没有兼容或迁移分支。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 条既有框架弃用警告；三套规则测试分别为 **282/282、193/193、19/19**，均退出 0 |
| 子任务编译 | 每步执行 `dotnet build Godot/GodotProject --no-restore -v quiet`。用户明确允许 A2-1～A2-3 暂留本批迁移错误；错误数依次为 **122、126、130**，A2-4 全部消除：**0 错误、5 警告、退出 0**。步骤日志为 `.godot/validation/coop_a2_20261108/a2_1_build.log` 至 `a2_4_build.log`；中间失败不作为最终通过证据 |
| 最终游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架弃用警告** |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，292/292**，失败 0、跳过 0。比 A2 开工增加 10 例：个人容量/出售收益隔离、排序归属与原格子顺序往返两项 Fact，6 例非法实例归属，2 例必需英雄字段缺失；原有断言保留 |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0 |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0。测试工程既有 NU1900，Battle 的 CS8632/CS0649 警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_scan_final_20261108 --headless --editor --build-solutions --quit --no-window`：**退出 0**，最终代码扫描与构建完成；既有 ComponentInsoector 的 RefCounted/free 退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、388 个 UID** 通过 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_frontend_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄建档、选角、地图主动/被动购技、钱包刷新、保存返回、磁盘重读、加载取消和部分注册清理通过。旧作用域拒绝、坏文件与重复注册为预期注入；退出有 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_ui_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**；拾取/满包/过期与池复用、单件/批量出售、钱包观察、学习扣费、15 件装备穿卸、60/120Hz 坡地与平台、死亡/检查点回滚、备份恢复、加载取消/失败清理、存档重入通过。坏文件与 HUD 打开失败为预期注入；退出保留 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**诊断，并有 20 条 GL 纹理泄漏明细 |
| 牛心唯一奖励专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_bull_reward_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --bull-only`：**退出 0**，`SMOKE-BULL TREASURE PASS` 与最终 `SMOKE PASS` 均出现；真实致死攻击、首次直接入包、重复击败排重、满包拒绝、提示缺席仍完成死亡、提示池复用及订退通过。另有 60/120Hz 原生招式与牛心演出 PASS；退出保留 **10 个 ObjectDB 实例、1 个资源占用**诊断。唯一奖励由此既有专项执行，普通完整 UI 不覆盖此分支 |
| 退役接口搜索 | 从仓库根执行 `$oldBag = 'Equipment' + 'Datas'; rg -n -- $oldBag .`，以及 `$oldWallet = ('Profile' + 'Model') + '.' + ('So' + 'ul'); rg -n -F -- $oldWallet .`、`$oldRootWallet = ('Game' + 'Data') + '.' + ('So' + 'ul'); rg -n -F -- $oldRootWallet .`：均退出 1、**0 命中**，含源码、规范、计划和历史说明；同时逐项核对剩余 `.Soul`，只属于英雄 DTO、掉落枚举或显示行，不再访问旧共享钱包。遵循仓库忽略规则，Git 对象、构建缓存和验证日志不属于源码文档验收 |
| 格式与边界 | `dotnet format whitespace <csproj> --no-restore --include <本批文件绝对路径清单> --verify-no-changes`：Godot 工程 **31 份**、ProfileTests **12 份** C# 文件均退出 0；仅既有工作区加载警告。`git diff --check` 退出 0；43 份修改 C# 均验证为 UTF-8/CRLF。无新增/删除类型或正式文件，Framework、addons、Tools/Luban、生成代码/资源、源表、bytes、场景无本批修改；未执行 Git 写操作 |

关键接口变化（返回类型未变化的行省略返回类型）：

| 修改前 | 修改后 |
| --- | --- |
| ProfileModel 共享钱包与共享背包属性 | 删除；改为 `BindableProperty<int> GetSoul(int heroId)`、`BindableList<EquipmentData> GetBag(int heroId)`；构造的 `soul` 表示每人初始余额，`Capacity` 表示每人容量 |
| `AddSoul(int amount)` / `TrySpendSoul(int amount)` | `AddSoul(int heroId, int amount)` / `TrySpendSoul(int heroId, int amount)` |
| `TryCollectEquipment(EquipmentData data)`（ProfileSystem/LevelSystem） | `TryCollectEquipment(int heroId, EquipmentData data)` |
| `GrantUniqueEquipment(EquipmentId equipmentId, Func<float> random)` | `GrantUniqueEquipment(int heroId, EquipmentId equipmentId, Func<float> random)` |
| `TrySell(Guid instanceId)` / `SellByQuality(EquipmentQuality quality)` | `TrySell(int heroId, Guid instanceId)` / `SellByQuality(int heroId, EquipmentQuality quality)` |
| `CollectLoot(LootReward reward)` / `TryCollectPickup(PickupId id)` | `CollectLoot(int heroId, LootReward reward)` / `TryCollectPickup(int heroId, PickupId id)` |
| `EquipmentSaleSummaryQuery(EquipmentQuality quality)` | `EquipmentSaleSummaryQuery(int heroId, EquipmentQuality quality)` |
| `SellEquipmentCommand(Guid instanceId)` / `SellEquipmentByQualityCommand(EquipmentQuality quality)` | 两个构造函数均在首位增加 `int heroId` |
| `InventoryBagPanel.Refresh(bool resetPage = false)` | `Refresh(int heroId, int capacity, IReadOnlyList<EquipmentData> items, int soul, bool resetPage = false)`；Form 注入同一英雄的只读显示数据，关闭时清除借用引用 |
| `ReadItems(GameData data, int capacity)` 返回共享数组与逐人穿戴 | 返回 `(Dictionary<int, EquipmentData[]> Bags, Dictionary<int, EquippedItems> Slots)`；先完整校验，再逐人恢复 |
| Debug 的 `RestoreSoul(int soul)` / `RestoreItems(IEnumerable<EquipmentData> datas)` | 两个入口均在首位增加 `int heroId`，只修改目标英雄 |

本批文件清单：**48 份修改（43 份 C#、5 份文档），无新增、无删除**。下表逐项列出各目录内的相对文件名。

| 所属目录 | 修改文件 |
| --- | --- |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |
| `Godot/GodotProject/TheGame/GameScripts/` | `Commands/EquipmentCommands.cs`、`Entity/Enemies/Bosses/zm1_10_bull_demon_valley/BullDemonHeartDeathState.cs`、`Entity/Level/LevelViewController.Loot.cs`、`Models/Profile/ProfileModel.cs`、`Models/Save/GameData.cs`、`Queries/EquipmentSaleSummaryQuery.cs`、`Systems/EquipmentSystem.cs`、`Systems/LevelSystem.cs`、`Systems/ProfileSystem.cs`、`Systems/SaveSystem.cs`、`Systems/SkillSystem.cs`、`UI/InventoryForm/Components/InventoryBagPanel.cs`、`UI/InventoryForm/InventoryForm.Logic.cs`、`UI/MapForm/MapForm.Logic.cs`、`UI/SkillForm/SkillForm.Logic.cs`、`Utilities/Utility/SaveDataUtility.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `FrontendSmokeScenario.cs`、`LevelSmokeScenario.cs`、`ProfileTestData.cs`、`UiSmokeScenario.Bull.cs`、`UiSmokeScenario.CheckpointFailure.cs`、`UiSmokeScenario.Dragon.cs`、`UiSmokeScenario.Equipment.cs`、`UiSmokeScenario.FireDemon.cs`、`UiSmokeScenario.Garuda.cs`、`UiSmokeScenario.Inventory.cs`、`UiSmokeScenario.Passives.cs`、`UiSmokeScenario.Skills.cs`、`UiSmokeScenario.Tangseng.cs`、`UiSmokeScenario.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/` | `ProcedureLoadGame.cs` |
| `Tests/ProfileTests/` | `BossEquipmentTests.cs`、`CommandTests.cs`、`DragonPalaceTests.cs`、`EquipmentTests.cs`、`LevelModelTests.cs`、`LevelRewardTests.cs`、`ModelTransactionTests.cs`、`PassiveSkillTests.cs`、`ProfileTests.cs`、`QueryTests.cs`、`SkillLearningTests.cs`、`TestGameScope.cs` |

计划之外的实际情况与处理：

- 中间构建门槛与删除旧接口的顺序冲突，已上报并取得用户裁决；保留原顺序且不增兼容视图，A2-4 编译错误清零。
- 背包显示按集合下标取格子；仅依照排序后的归属编号恢复会改变原单人背包顺序。本批沿用实例池的原始顺序来恢复各人的背包，归属编号仍按计划排序；没有增加持久字段或排序状态，反向 GUID 插入的 JSON 往返测试已验证。
- 计划未单列的机械消费者：`SaveSystem` 新档把初始余额写入首个英雄记录，空槽检查和快照回写删除根级钱包访问；`ProcedureLoadGame` 与 `TestGameScope.FromSaveData` 构造时使用默认零余额，再由现有 RestoreCheckpoint 恢复逐人钱包；烟测的根字段比较删除共享余额，完整 Heroes 比较已覆盖个人余额。
- `BossEquipmentTests.Save_RejectsAlteredProbabilitiesAndInvalidAccessoryOwnership` 原夹具直接把包内饰品移到穿戴位置，v14 必须同时移除原背包引用；首次规则测试因此失败，修正夹具后全套 292/292 通过，原有断言未弱化。A2-4 早期构建失败也已清零，失败日志保留在忽略的验证目录。
- `Tests/BattleTests` 经核对无需改源码或断言；本批未新增类型、正式文件、事件、配置或生产测试入口。

遗留与下一批前置条件：A3 须在用户接受 A2 并授权后开始，补齐完整双人档案规则证据和全员存档格摘要；当前仍只显示阵容首位摘要。单份本局模型、技能登记、输入/HUD、P1 开窗和牛心奖励本阶段显式归 P1，分别留给既定 B/C/D/E 批次。引擎退出诊断保留，不在本批修改只读依赖或处理无关生命周期。实机烟测使用既有 runner 的案例专属 APPDATA 和 Validation 存档，正式玩家存档未覆盖；正常数值下全关人工实战未执行，烟测不替代该验收。

## 双人A1：阵容取代单一出战英雄（2026-11-07）

按用户目标文件及 CoopImplementationPlan 第四部分顺序实施 A1-1～A1-8。当前仍只建立一名战斗英雄，单人输入与界面布局不变；钱包和背包归属留给 A2，本局模型留给 B1。依据项目规范第 00/10/30/50/70 章执行分层、中文注释、对称订退和隔离存档验证，2026-11-08 用户进一步要求文档也清零并授权同步规范和计划，已补齐文档当前状态。

存档 v12 → v13：删除单一出战英雄字段，以 `GameData.Heroes` 和 `ProfileModel.HeroIds` 的顺序表示席位（下标 0=P1、1=P2）；阵容人数限 1～2，英雄编号唯一。捕获不重新排序，回滚必须匹配同一有序阵容。其余持久字段和数值规则不变。只接受当前格式，**旧 v12 开发档需重建**，没有迁移或兼容分支。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，0 错误、5 条既有框架弃用警告；三套 `dotnet test`：279/279、193/193、19/19 |
| 子任务编译 | 每步均执行 `dotnet build Godot/GodotProject --no-restore -v quiet`。用户已明确允许 A1-1～A1-7 暂留本批接口迁移错误；各步错误数依次为 194、208、218、208、208、189、135，A1-8 全部消除，退出 0、0 错误、5 条既有警告。日志在 `.godot/validation/coop_a1_20261107/a1_*_build.log` |
| 最终游戏构建 | 八步结束后重执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，0 错误；增量构建显示 0 警告，A1-8 的完整编译保留原 5 条弃用警告，无新增警告 |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：退出 0，**282/282**，失败 0、跳过 0。比基线新增 3 例：反序阵容席位/JSON 往返、非法阵容信封、交换席位快照拒绝且数据不变；原有测试全部保留并迁移参数 |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：退出 0，**193/193**，失败 0、跳过 0；既有 CS8632/CS0649 警告保留 |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：退出 0，**19/19**，失败 0、跳过 0；三个测试工程的既有 NU1900 网络警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a1_scan_20261107 --headless --editor --build-solutions --quit --no-window`：退出 0，扫描和构建完成；既有 ComponentInsoector 的 free/退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：退出 0，**2283 条序列化路径、388 个 UID** 通过 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a1_frontend_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出 0，完整 `SMOKE PASS`，覆盖两英雄建档/选角、地图购技、保存返回、目录重读、加载取消及失败清理；预期旧作用域拒绝、坏文件与重复注册注入单列，退出有 3 个 ObjectDB 诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a1_ui_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：退出 0，完整 `SMOKE PASS`；HUD/背包/技能、15 件装备穿卸、实体/窗口解绑复用、60/120Hz 坡地/平台、拾取/死亡、检查点重入、备份回退、加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入；退出保留 38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例及 11 个资源占用诊断 |
| 旧接口搜索 | 2026-11-08 从仓库根执行 `$retiredField = 'Active' + 'HeroId'; rg -n -- $retiredField .`：退出 1，**0 命中**，包含源码、规范、计划与历史说明；遵循仓库忽略规则，Git 对象、构建缓存和验证日志不属于源码文档验收。命令中分段构造搜索词，以免记录本身重新引入旧名称 |
| 目标复核 | 2026-11-08 新目标文件仍指定 A1。对照当前 A1-1～A1-8 实现、接口、数据流、测试、8 份步骤构建日志及交付记录核验，未重做已完成实现。重新执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误、增量 0 警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`：282/282；`dotnet test Tests/BattleTests --no-restore -v quiet`：193/193；`dotnet test Tests/LevelTests --no-restore -v quiet`：19/19；`python Tools/ProjectMaintenance/validate_game_references.py`：2283 路径/388 UID 通过，均退出 0。非 Docs 目录工作区差异 SHA-256 仍为 `6629b8ff93d0ebb2bf7cb60bb8e76356261979b5f3ed8f7e759b714f6f65b3d6`，与完整扫描/前端/UI 验证后的代码一致；已检查现存扫描完成与两项最终 PASS 日志，本轮未重复执行引擎烟测。旧名称搜索与 `git diff --check` 再次通过，继续停在 A1 待审 |
| 文档补齐 | 2026-11-08 仅更新上述 6 份文档；`git diff --check` 退出 0。前后对 Git 索引及非 Docs 目录工作区差异执行 SHA-256 比较，两项均相同；本轮构建、规则与引擎烟测未重跑，沿用上一轮未变代码的验证结果 |
| 格式与边界 | `dotnet format whitespace <csproj> --no-restore --include <本批文件绝对路径清单> --verify-no-changes`：Godot 工程 38 文件、ProfileTests 22 文件、BattleTests 1 文件均退出 0；仅工作区加载既有警告。`git diff --check` 通过；61 份修改的 C# 文件均为 UTF-8/CRLF，文档保留原换行格式。Framework、addons、Tools/Luban、生成代码/资源、源表、bytes、场景无本批差异；未执行 Git 写操作，既有四份暂存文档保留 |

关键接口变化（类型、返回类型不变的行省略返回类型）：

| 修改前 | 修改后 |
| --- | --- |
| `ProfileModel(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes, ExperienceCurveUtility curve, int equipmentCapacity, int soul = 0)` | `ProfileModel(IEnumerable<(int HeroId, int TotalExperience)> roster, ExperienceCurveUtility curve, int equipmentCapacity, int soul = 0)` |
| 单一出战英雄及其成长/穿戴便捷成员 | 删除；使用 `GetProgress(int heroId)`、`GetSlots(int heroId)`，新增 `int HeroOf(PlayerSlot slot)`、`PlayerSlot SlotOf(int heroId)` |
| `AddExperience(int amount)` | `AddExperience(int heroId, int amount)` |
| `Learn(SkillId skillId)` / `Upgrade(PassiveId id)` | `Learn(int heroId, SkillId skillId)` / `Upgrade(int heroId, PassiveId id)` |
| `IsLearned(SkillId skillId)` / `IsLearnable(SkillId id)` / `IsAvailable(SkillId id)` | 对应方法均在首位增加 `int heroId` |
| `TryEquip(Guid instanceId)` / `TryUnequip(EquipmentSlot slot)` | `TryEquip(int heroId, Guid instanceId)` / `TryUnequip(int heroId, EquipmentSlot slot)` |
| `EquipEquipmentCommand(Guid instanceId)` / `UnequipEquipmentCommand(EquipmentSlot slot)` / `LearnSkillCommand(SkillId skillId)` / `UpgradePassiveCommand(PassiveId passiveId)` | 四个构造函数均在首位增加 `int heroId` |
| `StartSession(int heroId)`（LevelSystem） | `StartSession(IReadOnlyList<int> heroIds)` |
| `InventoryCharacterPanel.Refresh(AbstractHeroEntity hero)` | `Refresh(AbstractHeroEntity hero, HeroProgressionState progress, EquippedItems items)` |
| `ActiveSkillsPage.Refresh()` / `PassiveSkillsPage.Refresh()` | `Refresh(int heroId)`；由窗口传入本周期显示的英雄 |
| `TestGameScope(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes, int soul, int capacity, bool initializeHero = true)` | 删除首个身份参数，阵容首位决定本阶段的战斗英雄 |

本批文件清单：67 份修改（61 份 C#、6 份文档），无新增、无删除。以下目录内逐项列出相对文件名；此外修改 `Docs/Validation.md`、`Docs/WorkingNotes.md`、`Docs/CoopImplementationPlan.md`、`Docs/ProjectTour.md`、`Docs/ProjectGuidelines/10-CSharp.md`、`Docs/ProjectGuidelines/70-ProfileAndSave.md`。2026-11-08 文档补齐只涉及这六份文档；用户原已暂存的 OfficialReference 保持不动，Git 索引未改。

| 所属目录 | 修改文件 |
| --- | --- |
| `Godot/GodotProject/TheGame/GameScripts/` | `Commands/EquipmentCommands.cs`、`Commands/LearnSkillCommand.cs`、`Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`、`Entity/Heroes/AbstractHeroEntity.cs`、`Entity/Heroes/Wukong/WukongEntity.cs`、`Entity/Level/LevelViewController.cs`、`Models/Profile/ProfileModel.cs`、`Models/Save/GameData.cs`、`Systems/EquipmentSystem.cs`、`Systems/Hero/HeroSystem.cs`、`Systems/LevelSystem.cs`、`Systems/ProfileSystem.cs`、`Systems/SaveSystem.cs`、`Systems/SkillSystem.cs`、`UI/BattleForm/BattleForm.Logic.cs`、`UI/InventoryForm/Components/InventoryCharacterPanel.cs`、`UI/InventoryForm/InventoryForm.Logic.cs`、`UI/SkillForm/Components/ActiveSkillsPage.cs`、`UI/SkillForm/Components/PassiveSkillsPage.cs`、`UI/SkillForm/SkillForm.Logic.cs`、`Utilities/Utility/SaveDataUtility.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `FrontendSmokeScenario.cs`、`LevelSmokeScenario.cs`、`MapProgressSmokeScenario.cs`、`TestArenaController.cs`、`UiSmokeScenario.CombatFeedback.cs`、`UiSmokeScenario.Dragon.cs`、`UiSmokeScenario.DragonRules.cs`、`UiSmokeScenario.Equipment.cs`、`UiSmokeScenario.FireDemon.cs`、`UiSmokeScenario.Inventory.cs`、`UiSmokeScenario.Passives.cs`、`UiSmokeScenario.Skills.cs`、`UiSmokeScenario.Tangseng.cs`、`UiSmokeScenario.Terrain.cs`、`UiSmokeScenario.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/` | `ProcedureLevel.cs`、`ProcedureLoadGame.cs` |
| `Tests/ProfileTests/` | `BindablePropertyTests.cs`、`BossEquipmentTests.cs`、`CommandTests.cs`、`DragonPalaceTests.cs`、`EquipmentTests.cs`、`FreezeTests.cs`、`HurtProtectionTests.cs`、`LevelModelTests.cs`、`LevelProgressTests.cs`、`LevelRewardTests.cs`、`ModelTransactionTests.cs`、`MusouTests.cs`、`PassiveSkillTests.cs`、`PoisonTests.cs`、`ProfileTests.cs`、`QixingConfigurationTests.cs`、`QueryTests.cs`、`RecoveryTests.cs`、`SkillLearningTests.cs`、`SkillStateTests.cs`、`SkillSystemTests.cs`、`TestGameScope.cs` |
| `Tests/BattleTests/` | `SkillReuseTests.cs` |

计划之外的实际情况与处理：

- 中间步骤编译门槛与删除接口的先后顺序冲突，开工前已上报并取得用户裁决；保留原子任务顺序，逐步记录错误，A1-8 清零。
- 机械消费者补齐：`AbstractHeroEntity.RefreshAppearance` 按实体 HeroId 读取穿戴；`WukongEntity` 的升龙斩学习判断补 HeroId；`SkillSystem.TryActivate` 内部调用新增签名时显式传本阶段的 P1；`PassiveSkillsPage` 由窗口传 HeroId；`SaveSystem.IsUnclaimedSlot` 与快照回写移除已删除字段；Debug 页面刷新、反射字段比较及所有相关规则调用一并更新。
- `LevelSystem.StartSession` 的实际调用位置是 `LevelViewController.StartSession`，本批在实际调用处传 `profile.HeroIds`，未移动流程职责。`MapForm`、`ProfileTestData`、`EnemyAiSmokeScenario`、`HeroTestScope`、`BattleArchitecture` 经核对无需改动；SmokeInspection 的 `Attributes` 反射契约保留。
- 2026-11-08 用户明确要求文档也清零，并授权同步规范和计划。第 70 章、ProjectTour 与计划现状已更新为 v13、A1 已实现待审；历史记录保留原版本含义，以描述旧字段职责代替旧名称。此前的搜索范围待裁决项已解决。

当前仍保留共享灵魂/背包、单份 HeroModel/技能登记和 P1 开窗；这些是后续计划边界。A2 前置条件为用户审查接受本批 A1；本轮不开始 A2。引擎烟测使用既有 runner 的案例专属 APPDATA 和 Validation 存档；未覆盖正式玩家存档。正常数值下全关人工实战：未执行；本次烟测不替代该验收。

## 微软C#命名与格式迁移（2026-11-07）

按用户要求从规范配置推进到实际项目代码，并排除GGF/第三方。40个枚举去E（21手写、19Luban），1014个字段按可见性与静态归属命名；12份UI绑定、项目Luban加载器/总表模板从各自生成入口更新。脚本UID、枚举成员/值、配置bytes、存档v12与玩法保持。主工作区不应用双人02A草稿。

Framework/addons/Tools/Luban共937份跟踪文件逐字节核对HEAD无变化，Git索引保持原样。GGF扩展的旧EEntityId/EUIFormId引用仅通过GodotProject.csproj的两个编译别名绑定正式新类型，不新增兼容枚举或复制框架API。`.editorconfig`的C#规则也限定在项目自有源文件路径。

以下从仓库根执行可复用格式检查；目录路径必须保留末尾`/`，SDK 10.0.303在不带斜杠时可能没有选中任何文件仍返回0。已用故意不规范样例验证两种参数的差别。真正应用时移除`--verify-no-changes`，保留输入/排除范围，不传`--include-generated`。

```powershell
Push-Location Godot/GodotProject
dotnet format whitespace GodotProject.csproj --no-restore --include TheGame/ EditorScripts/ --exclude TheGame/GameScripts/GameProto/ --verify-no-changes
dotnet format style GodotProject.csproj --no-restore --include TheGame/ EditorScripts/ --exclude TheGame/GameScripts/GameProto/ --severity info --diagnostics IDE0007 IDE0008 IDE0011 IDE0021 IDE0022 IDE0023 IDE0025 IDE0026 IDE0027 IDE0049 IDE0065 IDE0161 --verify-no-changes
Pop-Location
```

小批次可把目录换成具体文件。测试和工具在各自csproj按同一方式指定本批文件；需要特殊生成契约的ExternalTypeUtil手写入口可明确指定单文件。命名是独立重构，不能以格式化代替场景导出/动画属性/反射/源表迁移。旧CSharpStyle的固定成员重排和块命名空间不再用于写入。

| 验证 | 命令与实际结果 |
| --- | --- |
| 配置源表与生成 | 9张表56格经Artifact Tool编辑、重算、逐值核验及前后渲染后，只转入原生XML文本。9份最终xlsx与858f22a加E前版本逐字节一致，无关ZIP部件、共享字符串与样式保留。`$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`最终schema/data/validation/code完成；Luban工具本体未改；全部bytes对HEAD无差异 |
| UI生成 | `python Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace <原命名空间>`逐个重生成10份业务Form与LoadingForm/QuestionTips；生成器只在项目侧转换模板字段，不修改Framework模板。代码/资源引用同步 |
| 类型与资源保持 | 40个枚举成员序列/值逐项一致；19份配置枚举与PlayerSlot的原UID保留。96份场景/资源与操作前快照独立比较，仅序列化属性名替换，节点名/路径、几何、动画轨道和数值原样；正式存档未写入 |
| 编译与命名检查 | `dotnet build Godot/GodotProject --no-restore -v quiet -p:EnforceCodeStyleInBuild=true`最终0错误、0条IDE1006；保留5条既有框架弃用警告。只在本命令临时启用风格分析，没有修改全仓构建门槛 |
| 规则与工具 | `dotnet test Tests/ProfileTests --no-restore -v quiet`279/279，BattleTests同命令193/193，LevelTests19/19；`python -m unittest discover -s Tests/ToolTests -v`26/26。既有NU1900及Battle的CS8632/CS0649警告保留；12份变更Python均py_compile通过 |
| 格式 | 按显式手写文件清单应用SDK style/whitespace；上述有效目录命令的whitespace与全部列出的style诊断`--verify-no-changes`最终均退出0，四组测试/工具显式清单复核也退出0。简单自动属性保留单行，using/命名空间/表达式体/var按EditorConfig，初始化次序不重排 |
| 实际Godot扫描 | 既有runner案例`microsoft_style_import_20261107 --headless --editor --build-solutions --quit --no-window`退出0、完成扫描/构建；退出仍有既有ComponentInsoector/free诊断，未修改插件 |
| 实际导出绑定 | runner案例`microsoft_style_bindings_verified_20261107 --headless --script res://.godot/validation/microsoft_style/validate_bindings.gd --no-window`退出0：91场景、1110个序列化属性实际实例化取值通过。NodePath解析到真实节点；Resource的local_to_scene复制按存储内容比较，不误判为丢绑定 |
| 正式前端 | runner案例`microsoft_style_frontend_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`退出0、完整SMOKE PASS；包括建档/选角、目录、地图、关内保存返回、备份/失败及重新加载。预期失败注入与退出5个ObjectDB/1条资源诊断分别保留 |
| 完整UI | runner案例`microsoft_style_ui_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`退出0、完整SMOKE PASS；HUD/背包/技能、实体/窗口复用、60/120Hz地形/平台、拾取、死亡、备份回退、加载取消与失败清理通过。预期坏文件/HUD失败注入单列；退出仍有20个Texture RID、8个ShapedText、2个Font RID、140个ObjectDB/11条资源诊断 |
| 引用与边界 | `python Tools/ProjectMaintenance/validate_game_references.py`2283路径/388UID通过；`audit_equipment_appearances.py`38装备/38外观通过。`git diff --check`通过，索引未改，隔离02A草稿不动 |

临时符号迁移/核验脚本和报告均在忽略目录`.godot/validation/microsoft_style/`，未引入正式迁移工具或生产测试API。首次UI尝试发现烟测元组内保存的旧反射字段名，已修复后重新完整执行；早期失败不作为最终通过证据。以下E前缀章节只保留历史实施证据，其规范已被本节及第10章替代。

## 全项目枚举E前缀（2026-11-07）

按用户本次要求统一项目自有枚举类型：21个手写枚举（含私有/调试状态）与19个Luban配置枚举全部加E前缀。通过C#符号区分类型和同名字段，公开接口、状态机、UI、测试及工具引用一起迁移；字段/属性、枚举项及持久键不加前缀。独立枚举文件同名迁移并携带原UID；EPlayerSlot.P1/P2保持1/2。按第10章更新命名规范，第20章同步配置类型说明；框架两份项目适配扩展仅6处配置枚举参数同步，例外登记第50章。双人02A未开始。

| 验证 | 命令及结果 |
| --- | --- |
| 源表/生成 | Artifact Tool编辑并逐值检查9张表56个类型单元格，查看__enums__/__beans__前后预览；只转入这些单元格的原生XML文本，所有其他单元格、样式、关系、共享字符串、工作簿部件及ZIP条目顺序保持。`$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：最终schema/data/validation/code完成；原19份声明清理、新文件保留原UID。未手改生成C#或bytes，未声称操作了用户XLSX Diff插件 |
| 名称与数值 | TheGame内40/40枚举均为E前缀；对照隔离的改名前快照，40个成员序列和赋值逐项一致；全部配置bytes无diff。439份C#还原类型前缀后与改名前内容一致，后续仅另同步一处注释类型名；框架差异验证仅6个类型引用，无逻辑变更 |
| 构建/规则 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`：279/279；BattleTests同命令193/193；LevelTests同命令最终19/19（补齐3个显式源码文件名引用）。`python -m unittest discover -s Tests/ToolTests -v`：最终26/26；未弱化原断言。规则项目NU1900及Battle既有CS8632/CS0649警告保留 |
| Godot扫描 | 既有runner案例`enum_prefix_import_20261107 --headless --editor --build-solutions --quit --no-window`：退出0，扫描和构建完成；既有ComponentInsoector/free退出诊断保留 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py enum_prefix_frontend_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出0，完整SMOKE PASS，含建档/选角、目录、地图学技、战斗、设置保存返回、备份/失败及重新加载；预期失败注入保留，退出5个ObjectDB/1条资源诊断 |
| 完整UI回归 | 同runner案例`enum_prefix_ui_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：退出0，完整SMOKE PASS；技能/无双、HUD/背包绑定、实体/窗口复用、60/120Hz地形/平台、拾取、死亡结算、备份回退、加载取消及失败清理通过。预期坏文件/HUD失败注入与退出诊断分开：20个Texture RID、8个ShapedText、2个Font RID、138个ObjectDB/10条资源诊断保留 |
| 引用/格式/索引 | `python Tools/ProjectMaintenance/validate_game_references.py`：2283序列化路径/388UID通过。CSharpStyle审计362个自有文件，XML齐全；165个原有格式建议不扩为本次全项目格式化。`git diff --check`通过；既有Git暂存内容散列保持，未暂存/提交 |

## 双人01：玩家身份枚举（2026-11-07）

按CoopImplementationPlan的01执行，仅新增`TheGame/GameScripts/Models/PlayerSlot.cs`及Godot扫描的UID；`Tests/ProfileTests/ProfileTests.csproj`增加一行源码引用。枚举在GameLogic命名空间，P1=1、P2=2；BattleTests已有Models通配引用。依据第10章的类型命名/XML与第50章新增C#扫描要求。未改变模型、业务API、输入、场景或v12存档；未增加只验证枚举字面值的测试。本批完成后暂停等待审查，02A未开始。

| 检查 | 命令与结果 |
| --- | --- |
| 游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0，0错误，5条既有框架弃用警告 |
| 独立源码引用 | `dotnet build Tests/ProfileTests --no-restore -v quiet`：退出0，0错误；NuGet漏洞服务不可达的既有NU1900警告保留；本次没有运行规则测试 |
| Godot扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_stage01_player_slot_scan_20261107 --headless --editor --build-solutions --quit --no-window`：退出0，dotnet_build_project及update_scripts_classes完成，生成`PlayerSlot.cs.uid`。既有ComponentInsoector退出时Can't free a RefCounted/nonexistent free诊断保留，不宣称编辑器无错误 |
| 范围 | `git diff --check`通过；本批实现差异3个文件（含UID），另更新计划/导览/沟通/验证状态；既有暂存文档内容散列保持，未自动暂存/提交 |

## 双人实现撤回与重新规划（2026-11-07）

用户要求恢复逐步审查，已撤回本次双人实现：169个受影响的已跟踪文件（含格式触及）恢复到858f22a原字节；移除10个本次新增实现/测试/UID文件。源码、测试、规范、场景与当前v12格式均恢复，Git索引内容保持，未暂存或提交。只保留规划文档和官方只读依据，并新增CoopImplementationPlan作为详细计划。

| 检查 | 结果 |
| --- | --- |
| 回退核验 | 逐文件读取HEAD原字节与恢复文件比较，全部相同；`git diff --stat`只包含文档，原新增实现文件已不存在 |
| 回退后构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误，5条既有框架弃用警告 |
| 边界 | `PlayerSlot`枚举尚未实现，v13未保留，正式玩家存档未修改；此前实现的规则/引擎结果不作为回退后功能证据 |

后续按[详细计划](CoopImplementationPlan.md)逐批执行；不因历史验证通过而跳过审查，也不在本次规划后自动开始01。

## 双人规划的现状验证（2026-11-07）

本次是先审查、制定分阶段计划，只改ProjectTour/OfficialReference/WorkingNotes及本记录；未实现双人、修改源表/场景/框架/存档格式，未写入玩家存档。入口时工作区干净，审阅基线提交858f22a。

| 检查 | 本轮命令及结果 |
| --- | --- |
| 实际架构/调用链 | 读取project.godot/csproj、Arch/FsmArchitectureExtensions、ProcedurePrepare/LoadGame/Level/Character、Profile/Hero/Skill/Level/SaveModel及系统、GameData/SaveDataUtility、InputSystem、攻击来源/伤害/死亡事件、敌人索敌、Loot/GroundItem、相机/入口、HUD/Inventory/SkillForm/Musou、甘露/分身与龙王。证据及待改契约在CoopImplementationPlan，不以额外英雄烟测冒充双人独立数据 |
| 官方只读核对 | 读取同一主程序SelectRole、MemoryClass、KeyBoardControl、BaseMonster/BaseAura、RoleInfo、PackThings/FallEquipObj、MainGame/GMain、ViewControllor、Role2/Role2Shadow、Monster26；核实不可重复选角、同槽双方、奖励归属/赠送/治疗范围、全灭和最低等级分档 |
| 构建基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0，0错误/5条既有框架弃用警告 |
| 档案/技能/掉落基线 | `dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`：279/279通过 |
| 战斗基线 | `dotnet run --project Tests/BattleTests --no-restore -- -parallel none`：193/193通过；测试项目既有CS8632与Godot导出字段CS0649警告保留 |
| 关卡基线 | `dotnet run --project Tests/LevelTests --no-restore -- -parallel none`：19/19通过 |

三个规则项目均有NU1900（NuGet漏洞数据服务不可达），不影响本轮断言。此次未执行双人引擎烟测，也没有以单人基线测试证明双人方案已经实现；实际玩法、输入、美术及生命周期验收按ProjectTour各阶段执行。

## 红色回血球与官方掉落数值（2026-11-07）

遵循第20/30/40/50章，扩展既有LootSystem/LootOrbNode/LevelSystem。Monster_v1 sprite3原始渲染确认为红色，auraGreen只是类名，先前“绿色回血球”已纠正。红/蓝恢复量在生成时固定为上限5%向下取整、最低10；黄色每颗5无双。三类各自独立12%，1/2/3颗各4%。

| 验证 | 命令与结果 |
| --- | --- |
| 原帧/源表范围 | `python Godot/GodotProject/.godot/validation/health_orbs_20261107/verify_sources.py`：退出0；19张health PNG与独立FFDec导出的sprite3逐字节相同，57张旧球PNG与用户索引相同。Combat仅AC:AF第1/3/5/6行共16格变化；其余单元格和ZIP部件（含sheet2、共享字符串、样式、关系）原字节保持。Artifact Tool已编辑并检查前后预览，未声称已操作用户XLSX Diff插件。 |
| 生成 | `$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：最终schema/data/validation/code均完成，只更新对应BattleConfig/TbBattleConfig及bytes。新增共用生命球概率、数量、最低量和上限比例，蓝/黄球数值沿用既有Enemy/Combat配置。 |
| 构建/规则 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`：279/279，覆盖4%分布、恢复最低量/取整、跨升级保持生成收益、满血、失效会话拒收；`dotnet run --project Tests/LevelTests --no-restore -- -parallel none`：19/19。规则项目有NuGet漏洞源不可达的NU1900警告。 |
| 实际掉落/拾取 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py health_orbs_capture_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --loot-only`：退出0，60/120Hz真实击杀红球掉落、19帧动画、追踪、暂停、接触恢复、满血回收、红蓝共池复用、清理不发放均通过；死亡在途红球回收且不复活。种子仅测试侧选择，生产概率不改。 |
| 菜单既有修正 | 同runner案例`health_orbs_map_buttons_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --map-buttons-only`：退出0、SMOKE-MAP-BUTTONS PASS；技能单次展开保持、保存悬停换图、设置轮廓对齐/字号字体、保存/失败保留/重读/复用通过。预期保存失败注入另记，退出2个ObjectDB/1条资源诊断保留。 |
| 导入/引用 | 同runner案例`health_orb_import_20261107 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0、997资源；官方红帧最终重导入案例`health_orb_final_import_20261107 --headless --editor --import --quit --no-window`退出0。编辑器既有ComponentInsoector/free与RID/ObjectDB退出诊断保留。`python Tools/ProjectMaintenance/validate_game_references.py`：2283路径、387UID通过。 |
| 格式/范围 | `dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/health_orbs_20261107/style_final.json`：361自有文件通过；`python -m py_compile Tools/LegacyMigration/export_official_combat_effects.py`、`git diff --check`通过。保留用户索引，本次未暂存/提交；存档版本和只读框架不变。 |

已查看官方四种球色板与实机`health_orbs_120hz.png`中的红球；60Hz首只怪物在视口外，不将该截图当红球可见证据。19帧/24fps、透明边缘与原点沿用官方原帧，无调色shader。专项退出0仍有12个ObjectDB/3条资源退出诊断，功能PASS不代表无关停诊断；此次不代表自然数值下全关实战验收完成。

## 项目清理、官方完整性核对与后续路线（2026-11-07）

依据第00/10/20/30/40/50/70章，按用户本轮“清理+重构+规划”完成。删除30个已跟踪遗留文件：临时xlsx三份、构建/会话日志与旧审计产物、五份已被现规范替代的计划、重复研究/局部README、无消费者的空debug_arena和迦楼罗冗余天空、SpatialExtensions及四份被通用入口替代的地图脚本（含UID）。保留正式test_arena、已注册数据/动态资源、原应用图标、有效规则/引擎测试及官方导出链，不改源表、Framework/addons/Luban、正式存档或Git索引。资源常量只经既有生成器更新。

ProjectTour重写为当前功能矩阵、单人待办和双人分阶段方案；ArchitectureReview合并两份重复研究；唐僧现状/源规则并入OfficialReference；WorkingNotes从逐轮账本收敛为当前意图和有效取舍；Validation清理被后续记录替代的尝试。Docs正文由约980KB减至约400KB，历史仍可由Git查阅。第20/30/40/60/70章只对齐用户已确定的配置实例取用、源表类型行、敌人/两英雄现状、独立背包和存档字段，不引入新玩法规则。

| 验证 | 精确入口与结果 |
| --- | --- |
| 官方版本/功能链 | 重新下载OfficialReference列出的v25928/Role_v7/OtherMat_v9，SHA及恢复后SHA与基线一致；只读摘要`.godot/validation/project_cleanup_20261106/official_downloads.json`及`official_content_audit.json`。核对13条实际调用链：绿球生成/恢复、法宝装备、背包离线兑换、牛心战功及结尾、片头/当前活动、双人输入/死亡/存档/竞技/治疗。AllEquipment去重44种，Items实际38种，六种差额逐件列出；不把定义或旧包嵌入脚本当可达玩法证据。 |
| 全关卡来源 | `python Tools/LegacyMigration/audit_official_level_dependencies.py --repo . --work Godot/GodotProject/.godot/validation/remaining_levels`：退出0，重新验证5包SHA、11关、24种递归关联敌人、各生成流/阶段/音乐；更新的是忽略的调查清单，未重写正式关卡。 |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：最终状态退出0、0错误/5条既有框架弃用警告。 |
| C#规则 | `dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`：274/274；`dotnet run --project Tests/BattleTests --no-restore -- -parallel none`：193/193；`dotnet run --project Tests/LevelTests --no-restore -- -parallel none`：19/19。删除空间扩展后同步移除BattleTests项目链接及失效using；首次遗留using构建失败已修正，不计作通过。 |
| Python工具 | `python -m unittest discover -s Tests/ToolTests -v`：最终26/26。新增两个真实边界回归：应用图标仅通过UID引用，删除图片但残留.import仍应报错；Autoload UID不可由别处的ext_resource或旧.godot缓存冒充定义。`python -m py_compile Tools/ProjectMaintenance/validate_game_references.py Tests/ToolTests/test_game_references.py`通过。 |
| 地图作者入口合并 | 在忽略目录的完整副本执行`Godot4CSharp_console.exe --headless --editor --path <副本> --script res://EditorScripts/register_official_map_location.gd -- <key> <旧节点> <字段> <题签> 19`，依次bianfuku/Location4/m_Bianfuku、qixingdong/Location5/m_Qixingdong、jialuoluo/Location6/m_Jialuoluo：均退出0。随后原生比较脚本`verify_map_authoring.gd`退出0，三地点位置/尺寸、hover留白、缩放、业务ID/子节点数量、轨道路径及19帧保持；map与3个库UID相同。修复了通用入口对自身模板重建及重算缩放覆盖人工几何的问题，正式map/动画无变化。最终比较脚本/日志保留于`project_cleanup_20261106`，验证副本已清除。 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py cleanup_frontend_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出0，完整前端PASS；两英雄建档/技能、设置/音量、地图/战斗、三次返回后直接读盘一致、失败清理/复用通过；没有缺失UID或图片载入错误。退出5个ObjectDB及1条资源诊断保留。 |
| 菜单专项及F6 | 同runner案例`cleanup_map_buttons_20261107`，同OpenGL参数并加`--smoketest=frontend --map-buttons-only`：单次展开/保存hover/设置字体/保存失败保留/重试及读盘通过；该次同时暴露图标误删，修正后的最终完整前端见上行，不能称此旧运行无资源错误。案例`cleanup_test_arena_20261107 --headless --scene res://TheGame/Scenes/test_arena.tscn --audio-driver Dummy --quit-after 360`退出0，悟空/两怪实际就绪；8个ObjectDB/3资源及形状RID退出诊断。 |
| 资源与引用 | 同runner案例`cleanup_collection_final_20261107 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`退出0、978项正式资源；既有编辑器ComponentInsoector/free、RID/ObjectDB诊断保留。`python Tools/ProjectMaintenance/validate_game_references.py`：最终2245序列化路径/387UID，并检查project.godot的UID-only设置。`python Tools/LegacyMigration/audit_equipment_appearances.py --output Godot/GodotProject/.godot/validation/project_cleanup_20261106/equipment.json`：38种外观/图标通过。 |
| 格式与范围 | `dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/project_cleanup_20261106/style.json`：361自有文件、0格式差异/缺XML；18份入口/Docs Markdown本地链接无缺失；`git diff --check`通过，受保护依赖/源表无改动、Git索引仍为空。 |

本轮中间发现bhpic.png是project.godot通过UID配置的应用图标，已按HEAD原字节与原.import恢复，最终保留；引用检查由此增加真实边界覆盖。图标曾误删的早期frontend/map-buttons运行只支持其实际断言，不替代修正后的完整运行。作者比较脚本一次NodePath类型错误已在夹具中修正，最终退出0的独立比较才是证据。编辑器/运行时退出诊断与预期旧档案、坏文件、注册/保存失败注入分别保留。

清理阶段完成的是清理/重构/规划；该阶段尚缺的回血球现已按上节后续实装。片头/结尾、自然数值下完整通关、战功兑换和双人仍以ProjectTour待办为准。当前单人主线已可玩，不代表官方全部内容或正常难度已最终验收。

## 当前限制与验收边界

- 当前v12本地存档；本次双人实现已撤回，悟空/唐僧单人及红色回血球均已接入。片头/结尾、战功兑换和双人未实现，详细范围见ProjectTour，不能用旧“唐僧未实装”描述现状。
- 正常数值下两角色完整自然战斗通关尚缺统一验收；主线进度夹具提高生命并用攻击包清场，技能命中专项另验。
- 构建保留框架弃用警告。长流程可出现ObjectDB/资源/RID退出诊断，编辑器另有ComponentInsoector/free诊断；按具体记录解释，不以业务PASS宣布无泄漏。
- 跨槽/旧档案/损坏文件、注册或HUD装配失败属于显式失败注入；须确认预期拒绝/清理后测试继续通过。测试只操作隔离Validation存档，正式玩家进度和用户Git索引不动。
- 既有DefaultWebRequestAgentHelper.Reset关停异常已按2026-10-31授权修正，不能继续把更早异常列为当前未解决事实；若再次出现按新日志定位。
- 官方SWF/XML与截图、原始日志位于忽略目录；可重跑作者工具和规则/引擎测试保留。版本源与SHA见OfficialReference。


## 返回主菜单提交当前档案与选关可见间距（2026-11-06）

地图设置原先只等待`PendingWrite`，没有提交地图上新产生的修改。现在地图与关内设置都调用既有`SaveSystem.SaveManuallyAsync`，成功后才导航，失败保留当前档案与页面。全部正式`ProcedureTitle`入口已检查：`ProcedurePreload`是无档案的首次启动；`ProcedureMap`提交当前档案；`ProcedureLevel`原本已提交后再`EndSession(false)`。未增加导航或存档抽象，未把写盘放进标题销毁档案的阶段。

本节布局结果覆盖下节的初次位置：原生透明渲染按alpha>128测量完整按钮图案，普通态技能/保存/设置的横向中心分别为75.5/76/75px；两处垂直可见边界间隔从91/37px改为64/64px。保存整体上移27px，设置图案和题签分别校正；技能展开时保留官方扩展轮廓，不按动态包围盒移动按钮。设置20号海报字、官方技能单次展开和保存悬停换图均保留。

| 验证 | 命令与结果 |
| --- | --- |
| 选关专项与失败重试 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py map_return_save_and_spacing_20261106 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --map-buttons-only`：退出0、`SMOKE-MAP-BUTTONS PASS`。技能普通态静止、悬停展开15帧后保持末帧且持续0.8秒不重播、保存真实鼠标悬停切图、设置字体/固定尺寸/提亮/复用均通过。购买后再增加未手动保存的17灵魂/3经验；隔离档案编号故意不匹配时拒绝返回并保留状态，恢复编号后实际点击返回、重读磁盘与重新进图通过。 |
| 全部返回路径 | 同runner案例`all_title_returns_save_20261106`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出0、完整前端PASS；悟空地图、唐僧地图、关内设置三次`SMOKE-RETURN-SAVE PASS`。返回前独立捕获快照，标题出现后直接通过`EasySave.LoadFromUserAsync<GameData>`读取对应文件，逐项比较版本、出战角色、灵魂、所有英雄的经验/学习/穿戴、装备实例/属性、关卡进度及东海旗标，不以内存检查点代替写盘证据。原有标题、六格档案、手动保存、暂停/回滚与失败清理回归通过。 |
| 可见边界 | 同runner案例`map_visible_bounds_aligned_20261106`，参数`--rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/map_correction_20261106/render_map_bounds.gd`：退出0、`MAP VISIBLE BOUNDS RENDER PASS`。三张单按钮透明图在alpha>128下的包围盒为技能(41,59)..(110,103)、保存(11,167)..(141,259)、设置(21,323)..(129,436)（右/下边界不含），两处边界间隔均64px。 |
| 实际画面 | 同runner案例`map_equal_visible_gaps_preview_20261106`，参数`--rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/map_correction_20261106/render_map.gd`：退出0、`MAP NATIVE RENDER PASS`。实际查看`.godot/validation/map_correction_20261106/map_native_idle.png`与`map_native_hover.png`；使用正式地图场景，确认普通态排列及展开末帧。独立渲染夹具设根尺寸时有一条anchors警告。 |
| 构建/格式/引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误/5条既有框架弃用警告；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/title_return_spacing_style_20261106.json`：362文件、0格式差异/缺XML；`python Tools/ProjectMaintenance/validate_game_references.py`：2248路径/388UID；`git diff --check`通过。 |

选关专项中的档案身份错误和拒绝返回日志是上述故意失败注入；退出仍有2个ObjectDB及1条资源诊断。完整前端保留既有旧档案、坏文件和注册失败注入，退出3个ObjectDB诊断。均未修改Framework/addons或用户Git索引。下方只等待`PendingWrite`以及按控件中心等距的旧记录不代表当前实现。

## 选关停帧、保存悬停与设置标题纠正（2026-11-06）

用户明确技能不应循环。此前把它当成故障描述，又把hover声明帧数当循环依据，两处结论都不成立：实际488/487帧脚本的frame15均stop。现恢复普通态1帧静止、悬停15帧单次展开后保持，移出回普通态；491的19帧可购黄光是源文件独立规则。保存502的BUTTONRECORD明确up/down=499、over=501，新增同注册画布的官方悬停PNG。设置标题15号改20号海报字、金色黑边/白外边，按源字图中心约75.5对齐并向右修正3.5px；使用单个原生Label的outline/shadow_outline，不额外加文字节点。

| 验证 | 命令与结果 |
| --- | --- |
| 官方导出 | `python Tools/LegacyMigration/export_official_map_combo_ui.py --source Godot/GodotProject/.godot/validation/skills-source/official_othermat.swf --java <已安装Java> --ffdec <已安装ffdec.jar> --work Godot/GodotProject/.godot/validation/map_correction_20261106/export --map-only`：源SHA校验通过，只更新地图资源；manifest记录488/487停帧、502/499/501和画布原点。 |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误/5条既有框架弃用警告。 |
| 实际交互 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py map_official_controls_final_20261106 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --map-buttons-only`：退出0，普通态静止、展开到14号帧停止并继续悬停0.8秒仍不重播、移出复位、可购发光/购买、保存真实鼠标悬停切图、标题对齐/字号/描边、设置导航/磁盘重读/复用通过；2个ObjectDB及1条资源退出诊断。 |
| 完整前端 | 同runner，案例`map_official_frontend_final_20261106`，去掉`--map-buttons-only`：退出0，标题/两英雄建档重读、地图技能购买、手动保存、地点命中、设置导航/暂停/回滚/复用和失败清理全部通过。故意旧档案/坏档/注册失败注入日志保留，退出3个ObjectDB诊断。 |
| 实际画面 | 已查看`frontend/map_buttons_aligned.png`、`map_save_official_hover.png`、`map_skill_entry_hover.png`及原生字体并排比较。保存图案没有裁切，标题同列，设置字号/金色与黑白轮廓已调整。 |
| 资源登记 | runner案例`map_official_resources_20261106 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0，980资源生成；PNG导入完成，既有ComponentInsoector/free、RID/ObjectDB退出诊断保留。 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`：2248路径/388UID；CSharpStyle报告`map_official_controls_style_20261106.json`：362文件、0格式差异/缺XML；作者工具py_compile、`git diff --check`通过。 |

## 身体、攻击来源、技能与官方背包展示收尾（2026-11-07）

本节覆盖当前12项目标及随后“两英雄都改用官方背包展示”的要求，覆盖下方11月6日关于战斗待机预览、SecondJumpTime和玄冰原点不动的历史结论。依据第00章单一状态所有权、第30章原生动画/基础形状、第40章原点/遮挡及第50章生成边界完成；第40章共用战斗/背包图像条款按用户当前要求作独立官方图例外，未改规范正文。

runner为 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py`；下表案例命令的 `...` 表示 `--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 --`。日志位于 `.godot/validation/movement_presets/<案例>/{stdout,stderr}.log`，APPDATA独立；烟测里的通用UI PASS只代表实际执行分支。

| 目标 | 当前实现与实际证据 |
| --- | --- |
| 1 幻影外观大小 | 以用户修改的本体为准，同用88.4高胶囊；按源幻影比本体高且偏低的差异，Sprite设(0,-33)/0.9。查看`hero_alignment_20261107/clone_body_aligned.png`及真实`tangseng_clone_platform_120_1.png`，未用角色根位移抵消姿态。 |
| 2 身体脚底高度 | 本体/幻影胶囊中心均(0.5,-6.2)，局部底边38；正式平台召唤后两者Y差小于0.1px，双向60/120Hz检查通过。 |
| 3 二跳时钟 | 删除SecondJumpTime和重复截止时刻，HeroAirState读取原生播放器完成情况；沿用GetActionDuration的complete标记/长度。两英雄实际60/120Hz翻身、切下落、落地通过；BattleTests保留两个有效频率用例，不重复四个无效参数组合。 |
| 4 攻击来源 | AttackSource记录OwnerEntityId、CasterType/CasterInstanceId及EmitterInstanceId；ReceiveHit只消费AttackData，事件复制同一值快照。实机区分本体/幻影共同归属和不同施放、判定节点；攻击包池化测试核对来源复制/清空。 |
| 5 无双归属 | 删除DamageBeforeDefense公开字段；CombatSystem在伤害计算中就地选定身体/离身的阶段伤害并算AttackerWsGain，HeroSystem只按当前死亡/激活状态接收。规则回归覆盖官方特殊招式分母、减伤与舍入；实机普通/未满每次+2，冰龙多次逐次增长。 |
| 6 甘露居中 | 源亮核多阶段位于中心附近，去掉-4.5px横向偏移，保留-24.5纵向、34帧加法和治疗时间。已查看`tangseng_cure_full.png`，深浅底加法既有检查继续有效。 |
| 7 玄冰范围/接地 | 幼柱42×56、成柱70×100、双柱148×100/154×100，原生轴在左右出生/长成/淡出时切换；无双按真实脚底上移30.4px。9个阶段实际物理点查询通过柱心有效/已去除外缘无效，真实目标技能命中通过，形状叠图和无双截图已查看。保留少量矩形近似，不声称Flash像素碰撞完全一致。 |
| 8 进关等待 | 沿用GF.Resource异步接口，把英雄预加载与场景/HUD并行，无永久缓存或新管理器。时长记录见下；取消/迟到实体回收专项通过。 |
| 9 重复普通音 | 未满冰龙释放沿用起手已播的普通音；满蓄保留专用音。真实点击/未满/满蓄/贫魔、分身创建换位、玄冰单声、双甘露、二次L、命中及受击全部通过。官方会重播未满普通音，这里按用户要求主动调整。 |
| 10 平台召唤 | 平台夹具复制正式单向平台到当前波次，不传送过门触发下一波。本体实际落地后从正式CreateWaterClone生成；两频率双向均停留平台。完整8秒寿命、墙/落地、暂停、辅助动作、取消复用继续通过。 |
| 11 EXP粗于框 | 原生Frame贴图独立画框，填充内缩为233×16；上下九宫格边距改2。两英雄正式背包0/50/100%截图已查看，满条仍在框内。 |
| 12 全Boss死亡 | 猕猴王、禺狨王、狮驼王及石像补原生闪烁/第25帧旋涡；七位普通死亡目标复用AbstractBoss入口和已有状态/效果，60/120Hz共14例通过回收/可见复用。源脚本逐个检查后保留大猩猩尸体、牛魔形态链及龙王25帧无旋涡特例，详见OfficialReference。 |
| 追加 两英雄官方背包 | backpack_v2悟空252为62帧短循环，没有立棍；唐僧283为1帧静态姿势，嵌套帧是装备selector。26张专用紧凑图、两份预览外观复用既有两层/字典/AnimationPlayer。两英雄自然播放和各15件正式GUI穿卸通过；已查看裸装、唐僧全装、经验条截图。战斗100/91帧wait继续只用于战斗。 |

| 验证 | 实际命令/结果 |
| --- | --- |
| 构建/扫描 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误/5条既有框架弃用警告；`run_godot.py hero_alignment_scan_final_20261107 --headless --editor --build-solutions --quit --no-window`退出0，完成脚本扫描/构建；退出仍有既有ComponentInsoector/free诊断。 |
| 档案与配置 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：274/274，退出0。 |
| 战斗 | `dotnet test Tests/BattleTests --no-restore -v quiet`：193/193，退出0，含来源快照清空；二跳删除两个重复参数组合，非减少行为覆盖。 |
| 工具 | `python -m unittest discover -s Tests/ToolTests -q`：24/24，退出0。 |
| 唐僧完整技能/幻影 | `run_godot.py tangseng_alignment_full_final_20261107 ... --smoketest=ui --tangseng-only`：退出0；正式选角/建档/入关、两种频率完整幻影生命周期、平台召唤、普攻/冰龙、无双、来源、五技能及八音频通过。stderr仅5个ObjectDB退出诊断。 |
| 官方背包/二跳/水魔 | `run_godot.py inventory_native_final_wukong_20261107 ... --smoketest=ui --hero-presentation-only`及 `inventory_native_final_tangseng_20261107 ... --smoketest=ui --tangseng-only --hero-presentation-only`：均退出0，分别SMOKE-PREVIEW-NATIVE 62/1帧；双向普攻根位置、60/120Hz二跳、EXP截图及唐僧细柱内外真实目标通过。退出分别5/6个ObjectDB，悟空1条资源占用。 |
| 两英雄正式GUI穿卸 | `inventory_native_wukong_wardrobe_final_20261107 ... --smoketest=ui --equipment-only`、`inventory_native_tangseng_wardrobe_20261107 ... --smoketest=ui --tangseng-only --equipment-only`：均退出0，各15件源表可穿装备、分页/战斗与专用预览换装/卸装复位/存档通过。悟空完整UI回归同时通过；退出ObjectDB/资源/RID诊断保留，见各stderr。 |
| 普通首领死亡 | `run_godot.py boss_death_statue_final_20261107 ... --smoketest=ui --boss-death-only`：退出0、14例PASS，实际旋涡播放到可见帧后截图，检查闪烁、唯一效果、完成回收及复用恢复。查看`boss_death_TamarinKing_120.png`及石像截图；stderr仅5个ObjectDB退出诊断。 |
| 牛魔形态回归 | `run_godot.py boss_death_bull_regression_20261107 ... --smoketest=ui --bull-only`：退出0，冲撞/卫兵、五招双向60/120Hz、心脏淡入淡出/手掌火焰、致死赠宝/重复/满包/死亡完成/订退通过；9个ObjectDB退出诊断。 |
| 龙王攻击回归 | `run_godot.py boss_death_dragon_regression_20261107 ... --smoketest=ui --dragon-only`：退出0，两难度/双向/两频率全部攻击、控制与效果取消通过；6个ObjectDB及1条资源退出诊断。 |
| 龙王死亡与传送 | `run_godot.py boss_death_dragon_flow_final_20261107 ... --smoketest=ui --dragon-flow-only`：退出0，真实下水/暂停淡入淡出/GGF传送、收益保持、首次旗幡拾取、原生死亡、W传送门、磁盘检查点与返回地图通过；11个ObjectDB及3条资源退出诊断。 |
| 玄冰原生范围 | `run_godot.py dark_ice_shapes_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/hero_alignment_20261107/render_dark_ice_shapes.gd`：退出0、stderr空；源1/5/6/11/16/30/35/40/45帧物理查询/形状叠图通过，已查看`dark_ice_shapes_montage.png`。 |
| 加载取消 | `run_godot.py hero_loading_cancel_final_20261107 ... --smoketest=level-cancel`：退出0，中途显示取消、迟到实例回收、不推进关卡通过；8个ObjectDB及1条资源退出诊断。 |
| 资源登记 | `run_godot.py hero_alignment_collection_final_20261107 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0、979项资源由既有生成器更新；编辑器退出仍有ComponentInsoector/free、RID/ObjectDB诊断。 |
| 引用/格式/作者工具 | `python Tools/ProjectMaintenance/validate_game_references.py`：2246路径/388UID；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/hero_alignment_style_applied_20261107.json --write`：362自有文件、0缺XML，token保持整理9文件。四个变更Python作者工具py_compile及`git diff --check`通过。 |

加载实测的场景/HUD资源就绪、英雄显示、HUD就绪分别为：原串行`hero_alignment_baseline_20261107` 1259/2246/2277ms，并行初跑`hero_alignment_pipeline_20261107` 1062/1106/1152ms；后者之后在旧“分身缩放必须为1”的断言失败，不能称整组技能通过，但该加载阶段测量有效。最终完整唐僧场景为393/445/489ms，属于后续缓存条件，不拿它与首次串行数字直接计算性能提升。性能收益来自缩短串行资源等待，不承诺消除所有首次磁盘/纹理导入延迟。

背包导出复现接口为 `export_official_inventory_preview.py --swf <恢复后的backpack.swf> --xml <backpack.xml> --work <忽略目录> --java <java.exe> --ffdec <ffdec.jar> --godot <Godot控制台> --manifests <已审阅装备manifest目录...>`；源SHA和selector见LegacyAssetMap。重用已审核的衣装身份/深度分层与紧凑打包，不手改原图或生成常量。初次将自定义Resource属性写在script前导致null，修正脚本声明顺序后以本节最终两英雄专项为准；旧测试要求战斗与预览纹理同一引用，也已同步改成对应专用资源。

## UI与唐僧表现13项收尾（2026-11-06）

以下逐项对应活动目标。依据第30章原生动画与少量基础判定形状、第40章源注册/实际渲染和第70章存档边界，复用已有状态、池、飘字及GGF窗口；来源和实现结论见OfficialReference、LegacyAssetMap。runner为 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py`，日志在同级案例目录；运行参数中的 `...` 均为 `--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 --`。

| 目标 | 最终结果与证据 |
| --- | --- |
| 1 技能按钮循环 | SelectPLace showBuySkill的普通态1帧静止，hover15帧、available19帧按24fps循环；`map_buttons_token_20261106 ... --smoketest=frontend --map-buttons-only`实际hover/out/可学态通过。 |
| 2 保存按钮动画 | 官方是SimpleButton，没有持续循环；保留其静态/交互态，不虚构技能式循环。与地图同一专项核对真实保存行为。 |
| 3 设置令牌 | 复用原map_return_title令牌，标题“游戏设置”，原生AnimationPlayer悬停缩放1↔1.04，移出复位；地图专项与截图`frontend/map_settings_token_hover.png`通过。 |
| 4 存档删除与面板 | 旧项目棕色确认框/金色按钮/原字体；小叉24px、默认42%透明度，仅可读存档显示。原图、真实鼠标、取消/删除/主备文件/重读均通过，精确命令见下一节。 |
| 5 背包角色位置 | 官方backpack_v2裸装注册比较后只在唐僧预览补 `(2,6)`，共用锚点不动。`tangseng_preview_sampling_final_20261106 ... --smoketest=ui --tangseng-only --hero-presentation-only`、`wukong_preview_natural_final_20261106 ... --smoketest=ui --hero-presentation-only`均退出0；0/0.5/2.7/3/3.6秒真实渲染采样已查看。 |
| 6 命中效果 | Role_v7的MonsterBeHurt1/2八帧闪光，唐僧hue+160、幻影原色；`tangseng_effects_release_final_20261106 ... --smoketest=ui --tangseng-only --effects-only`通过普攻/冰龙实际闪光，已查看`tangseng_normal_hit_feedback_60.png`。`enemy_lethal_flash_final_20261106 ... --smoketest=ui --enemy-death-only`六组60/120Hz致死闪光、抛飞、落地回收及一次死亡收益通过，退出0、6个ObjectDB诊断。 |
| 7 二跳下落 | HeroAirState从原生complete标记取翻身时长；两英雄表现专项60/120Hz实际双跳均完整翻身、及时切下落并落地。源hit1/jump2/jump3同原点逐帧图与实机截图已查看。四项规则测试通过。 |
| 8 冰龙蓄力条 | 原生ProgressBar复用官方ExceedPower尺寸/位置、绿条红框、满蓄闪色和半秒渐变。`tangseng_physics_charge_heal_musou_20261106 ... --smoketest=ui --tangseng-only`两种物理频率实际蓄力/暂停/取消/复用通过；已查看`tangseng_charge_60_full.png`。 |
| 9 全英雄无双 | HeroSystem消费AttackData/DamageResult，按getBasePower的普通/特殊阶段基准、本体/离身减伤规则结算。`dotnet test Tests/ProfileTests --no-restore -v minimal`274/274，`dotnet test Tests/BattleTests --no-restore -v minimal`195/195；唐僧实际普通命中+2、冰龙多次命中逐次增长，60/120Hz专项通过。 |
| 10 普攻前移 | 官方hit1的探身保留；两英雄表现专项60/120Hz、左右四组各逐物理步检查，根横坐标变化均0，未人为补偿源姿态。 |
| 11 幻影重力 | CharacterBody2D共用本体普通重力/身体基线及地形层，施法停住；`tangseng_physics_charge_heal_musou_20261106`两种物理频率的下落/地面/墙、暂停、施法、八秒寿命和回池复用通过，已查看`tangseng_clone_gravity_120.png`。 |
| 12 甘露数字 | 实际Heal恢复量进入既有FloatingTextPresenter，本体及幻影显示绿色20；同一专项和最终effects专项通过，已查看`tangseng_dew_heal_number.png`、`tangseng_clone_dew_heal_number.png`。 |
| 13 全技能判定 | 普通/冰龙基础形状修正，玄冰46帧左右生长/淡出阶段及无双缩放完整复核，甘露无攻击形状。水魔原动画11–20帧的250×420大矩形改为细柱+上环+水花，Tang表现专项四组真实目标验证柱心命中、侧空白不命中、上环/水花命中及池复用；最终effects专项确认实际二次L流程正常。判定是基础形状近似，不宣称Flash像素级一致。 |

追加的预览立棍核对：原生backpack252是62帧短循环，项目按用户要求复用Role_v7完整100帧战斗wait。`wukong_preview_natural_final_20261106`在不Seek的正式窗口自然播到立棍并循环回普通姿态，`SMOKE-PREVIEW-NATURAL PASS`，查看`wukong_preview_standing_pole.png`；唐僧3秒低头姿势同样可见。先前同一回调连续Seek后的ForceDraw截图会读到旧渲染，已经改为等待真实帧；旧静止截图不作为完整循环证据。

追加的玄冰无双位置核对：`dark_ice_registration_native_final_20261106 --rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/hero_presentation_20261106/render_dark_ice_registration.gd`退出0、stderr空。Role2.SpecialAttack的源x/y与角色相同，只在无双绕原点缩放 `(1.26,1.8)`；第21源帧同锚点原生图/正式场景的alpha>64边界普通均为 `(130,92,501,196)`，无双均为 `(81,46,548,233)`。已查看`hero_presentation_20261106/dark_ice_normal_musou_registered_comparison.png`；底边下延属于源缩放，无额外Y偏移。`tangseng_dark_ice_position_final_20261106 ... --smoketest=ui --tangseng-only --effects-only`再次通过实际本体/幻影普通及无双施放、共同根坐标与全部效果回归，退出0、仅6个ObjectDB退出诊断；已查看`tangseng_dark_ice_{normal,musou}_registered.png`两张实际战斗截图。

最终实机专项均退出0；唐僧表现、悟空自然预览分别只有6/2个ObjectDB退出诊断，`tangseng_effects_release_final_20261106`有14个ObjectDB及4条资源占用诊断。未将通用“SMOKE PASS”的文字当成未执行分支的证据。951资源登记由既有Collection Res生成器完成；`dotnet build Godot/GodotProject --no-restore -v quiet`0错误/5个既有框架警告；CSharpStyle `hero_ui_closeout_style_20261106.json`361个自有文件通过、token保持；`python Tools/ProjectMaintenance/validate_game_references.py`2178路径/386UID通过，三份修改的迁移脚本py_compile和`git diff --check`通过。`hero_ui_closeout_scan_20261106 --headless --build-solutions --quit --no-window`退出0、完成脚本扫描与解决方案构建，并生成新partial的UID；退出仍有既有ComponentInsoector/free诊断，未修改只读插件。

## 存档删除与原项目面板（2026-11-06）

按第40章旧资源迁入、第70章SaveSystem/ArchiveUtility边界及用户本轮美术要求完成。旧 `choose_scene.tscn` 的五张PNG与华康宋体W3字体原样迁入，删除入口只显示于可读存档，24×24、默认42%透明度、鼠标移入提亮；删除确认复用原面板和按钮字图，清理先前自绘StyleBox和额外关闭叉号。

| 验证 | 命令/结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0，0错误、5个既有框架弃用警告。 |
| Godot资源导入 | `run_godot.py import_delete_confirm_assets_20261106 --headless --editor --quit --no-window`、`import_delete_confirm_font_20261106`：均退出0，完成六项导入；编辑器退出仍有既有ComponentInsoector/free诊断。 |
| 实机删除与视觉 | `run_godot.py save_delete_original_art_20261106 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --save-delete-only`：退出0，`SMOKE-SAVE-DELETE PASS`；真实鼠标验证空槽隐藏、24px暗色图标、悬停提亮/离开复位、原图载入、取消不删、确认删除主备文件、其他格保留、同槽新建唐僧、标题磁盘目录重读与关闭后的迟到确认。此运行stderr为空。 |
| 截图 | 已查看`.godot/validation/frontend/save_delete_slots.png`、`save_delete_hover.png`、`save_delete_confirmation.png`，确认棕色原面板与原按钮字图、空槽无叉号、正文无遮挡。截图前等待造档成功提示自然结束。 |
| 最终资源与格式 | `run_godot.py save_delete_resources_20261106 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0、916资源生成，编辑器退出仍有已知插件/free及RID诊断；`python Tools/ProjectMaintenance/validate_game_references.py`：2106路径、385UID通过；CSharpStyle `style_final.json`：358文件、0格式差异、0缺失文档；源5PNG+字体逐字节核对与`git diff --check`通过。 |

最初`save_delete_official_ui_20261106`虽有逻辑PASS但新PNG未导入、字体未迁入，发生资源解析错误；此案例不算视觉通过，最终以`save_delete_original_art_20261106`为准。本节只覆盖存档专项，完整13项结果见上方收尾记录。

## 动画方法入口与角色相关表现收尾（2026-11-05）

按第10章动画入口命名、第30章状态与表现分层和Stop Ladder复核全项目 `*AnimEvent` 调用链。`AnimEvent` 仍只作为AnimationPlayer方法轨道键；普通业务使用无后缀实现，轨道方法只转发：唐僧音效、SkillEffect攻击刷新/结束、敌人攻击刷新和虾兵弹体均已收敛。烟测不再直接调用动画事件名，改为真实播放 `attack_1` 的第3源帧。

| 检查 | 命令与结果 |
| --- | --- |
| 编译/格式/引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . .../animation_event_boundaries_style_final_20261105.json`：358文件、0格式变更、0缺失文档；`validate_game_references.py`：2098路径、385 UID。 |
| 唐僧/海怪事件边界 | `animation_event_boundaries_tangseng_20261105`、`animation_event_boundaries_sea_20261105`：退出0；唐僧技能/八段声音/玄冰与水魔效果、虾兵真实弹体命中/消散/暂停/池复用通过。退出期仅有既有ObjectDB/资源诊断。 |
| 悟空/彭魔王事件边界 | `animation_event_boundaries_wukong_20261105`、`animation_event_boundaries_garuda_20261105`：退出0；悟空五技能/独立效果/声音与实际命中，彭魔王四招/冲刺/复生/回池通过。 |
| 共用功能唐僧 | `tangseng_shared_physics_status_20261105`：退出0；官方平台双跳、狮驼王六招/中毒、60/120Hz冰冻控制锁与死亡清理通过。 |
| 吞噬角色选择 | `tangseng_capture_bull_20261105` 与 `wukong_capture_bull_20261105`：退出0；两角色60/120Hz、双朝向、源五次脉冲、释放和selector图集选择通过。唐僧专属图集导入扫描 `tangseng_capture_import_20261105` 退出0。 |

退出期仍报告ObjectDB/RID/资源占用；编辑器扫描另有已知插件free诊断。未据专项通过声称这些诊断已解决，也未修改只读插件。

## 唐僧完整流程、目录与悟空共用功能对照（2026-11-05）

| 检查 | 精确入口与结果 |
| --- | --- |
| 唐僧完整生命周期 | `run_godot.py tangseng_complete_role_validation_20261105 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 24000 -- --smoketest=ui --tangseng-only --tangseng-lifecycle-only`：退出0；正式新档、五主动学习/施放、15件实际拾取与GUI穿卸、60/120Hz无双/受击保护、25次被动购买、平地/坡面死亡重试、地图/标题/同槽磁盘重读、装备GUID/随机属性/槽位/技能与被动保持，再次穿卸通过。退出25个ObjectDB及5条资源占用诊断。 |
| 十关主线 | `tangseng_chapter_gate_filtered_20261105` 用户参数 `--smoketest=progress --tangseng-only`：真实完成1..8，每关经标题重读；外部runner在900秒终止（124），保留原日志。复制这份隔离APPDATA到`tangseng_chapter_resume_disk_20261105`，以同一档案 `Validation/Progress/c55712374bf247d0ba774bc84938c197` 续跑；日志明确 `next=9, hero=2, read from actual disk profile`，实际完成9、10并再次标题重读，退出0。现有可重现续跑参数为 `--smoke-run-id=c55712374bf247d0ba774bc84938c197`，固定在Validation目录，不修改正式存档。最终18个ObjectDB及3条资源占用诊断。 |
| 隐藏龙宫 | `tangseng_dragon_role_regression_20261105`，用户参数 `--smoketest=ui --tangseng-only --dragon-flow-only`：退出0，实际下水/暂停淡出/GGF转场/奖励保留/首次旗幡拾取/龙王死亡/W出口/磁盘/地图通过；退出11个ObjectDB及3条资源占用诊断。 |
| 悟空既有流程 | `wukong_role_final_20261105`，用户参数 `--smoketest=ui --terrain-level5`：退出0；全部背包/技能、HUD退订/实体复用、七星洞坡面、事务/失败/当前格式读写及备份重入通过。退出138个ObjectDB、10条资源占用及RID诊断。 |
| 悟空无双与墓碑 | `wukong_musou_role_regression_20261105 -- --smoketest=ui --musou-hurt-only`、`wukong_tombstone_role_regression_20261105 -- --smoketest=ui --hero-death-only`：均退出0；悟空专属立绘、白光/霸体、8帧受击闪光和60/120Hz平地/坡面墓碑、重试/地图重入通过。分别退出3与5个ObjectDB，后者1条资源占用诊断。 |
| 目录与生成 | `musou_activation/{wukong,tangseng,shared}`、`hero_hurt/{wukong,tangseng}`、唐僧技能素材 `Effects/tangseng/skills`、悟空预览动画 `Animations/Heroes/Wukong`；全部携UID更新引用与作者工具。`hero_resources_collection_closeout_20261105 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0，明确 `Collection complete. 910 resources saved`；退出有既有编辑器插件/free与RID诊断。 |
| 规则与工具 | `dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260；`python -m unittest discover -s Tests/ToolTests -v`：24/24（包含唐僧就绪门）；第40章的可玩角色装备门已将唐僧从跳过名单改为身份/动作/GUI三项必验。 |

所有引擎案例均通过同一个 `Godot/GodotProject/.godot/validation/movement_presets/run_godot.py` 执行，除单独列出的headless导入/生成外使用OpenGL兼容渲染及Dummy音频。角色功能/技能命中由专项覆盖；主线清场夹具使用提高HP和直接攻击包清怪，十关证据证明移动/刷怪/门/结算/解锁/保存流程，不声称全程用唐僧技能击败每个Boss。

失败夹具的修正：悟空技能官方透明身体格91仅允许在烈焰闪/火魔斩的对应阶段；敌人1/2/3旧编号改为实体枚举；阶段门探针只取双方原有PlayerBarrier掩码，避免世界高台混入。原失败日志保留，不修改正式素材或放宽实际游戏规则来迎合测试。

## 唐僧官方音频与输入触发（2026-11-05）

按第20章声音事件编码、第30/50章GGF/原生方法轨道和当前Stop Ladder复用要求执行。已有官方技能音频导出器增加`--hero tangseng`，8段来自登记的Music.swf原MP3载荷。新增声音源表仅Resources与__enums__；不新增死亡音、不改悟空声音或运行期音频架构。

| 检查 | 命令与结果 |
| --- | --- |
| 官方导出 | `python Tools/LegacyMigration/export_official_skill_audio.py --source Godot/GodotProject/.godot/validation/wukong-skills-export/audio/music.swf --work Godot/GodotProject/.godot/validation/tangseng_audio_20261105/source --java "S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe" --ffdec "$env:TEMP/gamezmxy-ffdec/ffdec.jar" --output Godot/GodotProject/TheGame/Audios/SFX/tangseng --hero tangseng`：退出0，既有固定源SHA检查通过，原样复制8段MP3。 |
| 作者化与原包 | `.godot/validation/tangseng_audio_20261105/author.mjs --edit`由Artifact作者化并重算、inspect/零公式错误扫描和前后render；两个视图已查看。preserve.py逐表核对全部值/公式后仅局部添加8行及共享字符串；原ZIP的样式/关系/其他Sheet和元数据部件逐字节保留。 |
| XLSX Diff | 同目录`verify_diff.mjs`使用已安装1.6.4真实worker：Resources普通/公式均56格、__enums__均24格；全部右侧Sheet非空。此结果不代替用户VS Code面板恢复确认。 |
| Luban | `$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：退出0；只更新SoundId.cs和sound_tbsoundconfig.bytes。 |
| 构建/纯测 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260，NU1900网络警告保留。 |
| Godot导入 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py tangseng_audio_import_20261105 --headless --editor --quit --no-window`：退出0，8段导入完成；退出仍有既有ComponentInsoector/free诊断。 |
| 正式输入与声音 | 同run_godot.py入口案例`tangseng_audio_verified_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --tangseng-only --effects-only`：退出0，8段经GGF SFX解码/无循环/停止，60/120Hz短按/未满/满蓄/低蓝复用、分身创建与换位、普通/无双玄冰仅本体发声、甘露分身两次起手、水魔无追加无声音/追加一次、真实命中与受击全部PASS。原生方法键核对普攻0、玄冰14/24、水魔下落13/24秒。 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`：1958路径、384UID；`python -m py_compile Tools/LegacyMigration/export_official_skill_audio.py`与`git diff --check`通过。两份改动C#通过既有CSharpStyle的token/注释保持格式化。 |

初轮普攻在伤害发生的同帧检查GGF派发前的命中音计数而失败；等待实际成功播放事件后修正。后续把异步成功播放时的动画位置当作原生键时间也失败，已改为检查实际动画键并分别观察播放结果，没有修改生产时点以迎合测试。最终专项退出仍有14个ObjectDB及4条资源占用诊断，未声称退出无错误。证据目录`.godot/validation/tangseng_audio_20261105/`及`movement_presets/tangseng_audio_*_20261105/`。全部装备实际穿卸、共用被动/状态、完整通关与磁盘重读仍待，原目标保持。

## Stop That Shit 安装与项目规则（2026-11-05）

用户要求安装插件并作为项目规范。按固定tag的INSTALL_FOR_AGENTS执行Codex原生安装，未克隆仓库、直接编辑宿主配置或绕过Hook信任；工程规则纳入ProjectGuidelines/AGENTS.md，安装/使用边界归第50章。

| 检查 | 命令与结果 |
| --- | --- |
| 宿主 | `codex --version`：0.154.0；`node --version`：v24.14.0。 |
| Marketplace | `codex plugin marketplace add lennney/stop-that-shit --ref 0.2.5 --json`：退出0，注册stop-that-shit marketplace，固定tag。 |
| 安装 | `codex plugin add stop-that-shit@stop-that-shit --json`：退出0，version0.2.5，安装目录为`C:/Users/zheliku/.codex/plugins/cache/stop-that-shit/stop-that-shit/0.2.5`。 |
| 状态 | `codex plugin list --marketplace stop-that-shit --json`：installed=true、enabled=true、Git来源为指定仓库。 |
| 包与文档 | 已阅读安装版SKILL.md及hooks/codex-hooks.json；`node --check <安装目录>/hooks/stop-that-shit.cjs`退出0。`git diff --check`通过；只修改中央规范与当前记录。 |

尚未完成：重启后的宿主发现、CLI `/hooks`用户审阅/信任、显式任务模式后的实际Guard拒绝/允许烟测。上游安装说明要求用户自行审阅并信任，未用直接执行Hook、修改信任文件或伪造用户prompt代替。当前证据只支持插件已安装启用及工程规则已落地，不支持Guard已在本会话拦截。原唐僧目标仍继续，音效表工作稿未写回。

## ConfigurationSystem实例初始化与注册依赖（2026-11-05）

按用户当前要求移除静态ValidateTables、ValidateSkillCombat、ValidateSkillCatalog对外入口和Utility可空参数/兜底构造。ConfigurationSystem.OnInit只借用所属架构中已注册的ConfigurationModel、ExperienceCurveUtility、ConfigIdUtility；全部内部校验方法为私有实例方法。经验表验证仍由工具构造时完成，Procedure注册失败沿原失败出口终止。测试通过现有TestArchitecture按同一依赖顺序装配并执行ICanInit，未新增生产测试入口。第20章旧ValidateTables方法名尚未据此改写规范正文。

| 检查 | 命令与结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告。 |
| 同生命周期规则测试 | `dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260；正式表初始化通过，编号/重复基础身份/攻击预算/英雄输入非法配置均由实例OnInit拒绝。测试包保留NU1900联网警告。 |
| 依赖/旧入口检查 | 全项目C#引用检查无外部静态ConfigurationSystem校验调用；ConfigurationSystem内无static、new Utility、可空工具参数或工具缺失兜底。`git diff --check`通过。 |
| 实际启动 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py config_system_registered_init_20261105 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --level-guide-only`：退出0，正式Prepare注册/配置OnInit、十地点图鉴、悬停/两种尺寸及真实点击进关PASS。退出仅剩3个既有ObjectDB诊断，无资源占用错误。 |

本次涉及的配置系统与测试文件通过CSharpStyle审计，0格式差异、0缺失XML。上一节编号专项的历史通过不替代本次初始化链路验证。唐僧声音表仍未编辑，后续继续原目标。

## 配置ID工具统一与注册链路（2026-11-05）

按用户本轮明确要求，ConfigIdUtility位于`TheGame/GameScripts/Utilities/Utility/`，实现现有IUtility，在ProcedurePrepare和独立TestArena注册。编号公式与范围判断只保留在该实例工具中；实体、图鉴、启动校验与烟测通过GetUtility取用，独立规则测试借用同一实现。未为本项改表、重编枚举、修改存档或改写规范正文。第00章的纯静态规则描述不覆盖用户此次指定的注册型工具形式。

| 检查 | 命令与结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误，5条既有框架弃用警告。 |
| 规则/配置/图鉴 | `dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260通过，含固定编号样本、跨实体/环境误归属启动拒绝、实体范围边界、毒云与龙王高等级档。NuGet漏洞数据查询仍有NU1900网络警告。 |
| Godot扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py config_id_utility_instance_scan_20261105 --headless --build-solutions --quit --no-window`：退出0、扫描完成；退出仍有既有ComponentInsoector与ArchiveSettingInspectorPlugin的free诊断。 |
| 鲨魔王/鲨鱼 | 同run_godot.py入口，案例`config_ids_shark_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --shark-only`：退出0；60/120Hz、双朝向、两阶段八个出招选择、水牢警示/取消、鲨鱼斩击和召唤/死亡/复用全部PASS。 |
| 毒云 | 案例`config_ids_qixing_20261105`，同上参数改`--qixing-only`：退出0，六招双朝向、飞刀/毒云命中、持续中毒、暂停/复用PASS。 |
| 龙王 | 案例`config_ids_dragon_20261105`，同上参数改`--dragon-only`：退出0；普通/高等级四招、60/120Hz、双朝向及控制/暂停/弹体取消PASS。 |
| 地图图鉴 | 案例`config_ids_guide_20261105`，同上用户参数改`--smoketest=frontend --level-guide-only`：退出0；十张真实图鉴、首领顺序、两种尺寸、嵌套悬停与点击进关清理PASS，验证Query取用已注册的工具。 |
| 引用/重复规则 | `python Tools/ProjectMaintenance/validate_game_references.py`：1958路径、384UID通过。全项目手写C#/GDScript/Python搜索确认实体攻击/首领池乘法、攻击解析和敌人范围分组不再散落；固定源表枚举、场景引用及独立期望值保留。`git diff --check`通过。 |

上述四项实际引擎案例退出时仍分别有9/9/7/5个ObjectDB实例与各1条资源占用诊断；不以专项PASS宣称退出期无错误。CSharpStyle审计357个手写文件、0缺失XML；新工具与更新的编号测试单独通过token/注释保持格式化，未批量改动其余历史格式差异。证据目录为`.godot/validation/config_id_utility_20261105/`及`movement_presets/config_ids_*_20261105/`。这些结果不替代仍在进行的唐僧声音、完整穿卸/状态/章节通关/磁盘重读验收。

## 唐僧瞄准圈注册、特效层级与技能运动（2026-11-05）

依第30章具体状态负责运动/分支、第40章完整父级矩阵和原生资源定位、第50章实际渲染验证执行。水魔圈补回官方body矩阵，与此前相比下移19.95px、横向补12.45px并随朝向镜像。唐僧离身场景z=2，血条层保留。玄冰本体/分身同步起手、各自朝向、无双缩放/额外刷新区别与最终整数威力对齐；删除错误统一冻结五招的中间基类，恢复冰龙空中运动与瞬发水幻影动量。

| 检查 | 结果 |
| --- | --- |
| 官方完整父级 | `python Godot/GodotProject/.godot/validation/water_ball_position_review_20261105/prepare_reference.py`：退出0，核验SWF/XML散列，生成hit4完整body父级41源帧双朝向参考；第10/20/30/41帧与修正前后Godot截图已查看。 |
| 正式场景注册 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py water_ball_all_registered_frames_20261105 --rendering-method gl_compatibility --audio-driver Dummy --quit-after 300 --script res://.godot/validation/water_ball_position_review_20261105/render.gd`：退出0、0错误；第10–41共32源帧×双朝向实际global_transform与官方注册点全部一致。像素不是全等：原生FFDec与Godot滤镜边缘不同，未据坐标通过宣称整幅像素相同。 |
| 正式选角/学习/技能 | 同run_godot.py入口，案例`tangseng_full_fidelity_verified_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --tangseng-only`：退出0；分身walk/hit3/hit5/暂停/取消复用/192帧寿命，J短按/未满/满蓄/低蓝回退，空中蓄力/松方向/重力/瞬发分身动量/空中水魔拒绝，普通与无双玄冰同步/朝向/缩放/威力/独立刷新/层级，甘露末帧治疗、水魔圈/超时/追加独立命中均PASS，前四组覆盖60/120Hz。四张最新战斗截图已查看。 |
| 清理后复跑 | 案例`tangseng_cleanup_verified_20261105`，同上参数加`--effects-only`：退出0；删除玄冰本体空判定轨道、甘露重复绑定与无用回调场景参数后，J输入、空中运动、普通/无双玄冰、甘露和水魔链再次全部PASS。此次退出剩5个既有ObjectDB诊断，无资源占用错误。 |
| 构建与引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告。`python Tools/ProjectMaintenance/validate_game_references.py`：1958路径、383UID通过。`git diff --check`通过。 |

首轮无双案例在激活演出的暂停中发送输入而超时，测试改为等待演出释放暂停后重跑；空中夹具首轮在MoveAndSlide刷新传送前落地缓存前设置速度，改为进入Air后设定待测速度，未修改生产运动迎合错误夹具。参考渲染初版遗漏scaleY导致全透明，已修正重导；旧Seek采样值轨道的下一键问题改用原生Advance后复核。最终完整专项退出仍有既有8个ObjectDB实例和1个资源占用诊断。证据在`.godot/validation/water_ball_position_review_20261105/`和`movement_presets/tangseng_full_fidelity_verified_20261105/`。完整装备穿卸、全部状态/被动、章节通关及磁盘重读仍待，不据通用SMOKE尾句认定已验收。

## 唐僧特效全帧清理与透明图集（2026-11-05）

依据第40章源注册点与第50章实际渲染验证约束执行。用户明确要求去掉玄冰阵蓝色弧光；源604父轴完整核对为第5–11帧，595/597/599/601/603五张形状，已全部排除。593冰柱及其碎冰、滤镜和淡出保留。甘露仅对源703的ADD图做颜色/alpha等效转换，保留ADD材质，关闭该贴图fix_alpha_border。四组图集复用compact_registered_atlas与AtlasTexture.margin；不重新居中、删除动画帧或修改伤害判定。水魔球素材源注册offset=(-0.85,-11.3)；正式场景叠加hit4父级矩阵后的修正见上节。

| 检查 | 命令与结果 |
| --- | --- |
| 官方重导 | `python Tools/LegacyMigration/export_official_tangseng_effects.py --role Godot/GodotProject/.godot/validation/wukong-skills-export/role.swf --xml Godot/GodotProject/.godot/validation/action_origin_review/fresh_role.xml --java "S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe" --ffdec "$env:TEMP/gamezmxy-ffdec/ffdec.jar" --work Godot/GodotProject/.godot/validation/tangseng_effects_20261105/export --only dark_ice water_demon_fall cure_role water_demon_ball --install-to Godot/GodotProject/TheGame/Sprites/Effects/tangseng`：退出0，Role_v7 SHA校验通过，125源帧。 |
| 帧/原点回读 | `python Godot/GodotProject/.godot/validation/tangseng_effects_20261105/verify.py`：125帧通过；从正式SpriteFrames回读region/margin与源画布逐帧比对，普通混合可见RGBA相同，甘露ADD光量误差≤1/255。普攻/冰龙单图与原始导出逐字节相同。 |
| 存储裁切 | 玄冰阵2862×1536→2048×980；甘露2892×1662→1024×2090；水魔下落1602×2160→1024×1972；水魔球1176×332→4096×91。水魔下落22–25及玄冰阵45–46空帧仍保留原时长，共用1px透明存储；重复帧只共用贴图。 |
| 实际GPU对比 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py tangseng_effect_gpu_verified_20261105 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --script res://.godot/validation/tangseng_effects_20261105/render.gd`：退出0、0错误、250张深浅背景双栏对比。逐图比较左右550×500区域，玄冰/球/下落最大通道差0，甘露最大差1；gpu_report.json已记录。 |
| 正式技能回归 | 同run_godot.py入口，案例`tangseng_effects_verified_runtime_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --tangseng-only --effects-only`：退出0，正式学习控件、34帧甘露末帧治疗、玄冰真实命中、移动roundMc、无追加输入收招、二次释放落点及独立命中/回池PASS；实际截图已查看。退出仍有既有8条ObjectDB泄漏与1条资源占用诊断。 |
| 构建与引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0警告、0错误；`python Tools/ProjectMaintenance/validate_game_references.py`：1957路径、384UID通过；`python -m py_compile Tools/LegacyMigration/export_official_tangseng_effects.py`及`git diff --check`通过。 |

首轮GPU脚本的Vector2/Vector2i比较解析错误已修复。后续对比发现最近邻采样需对齐像素中心，以及甘露fix_alpha_border会在新alpha编码后改写半透明颜色；均修正后按上表最终案例复跑，未以最初截图代替通过结果。编辑器导入退出仍有既有ComponentInsoector/ArchiveSettingInspectorPlugin释放诊断，未修改只读插件。证据根为`.godot/validation/tangseng_effects_20261105/`；本项不代替唐僧全技能、全部状态、正式穿卸、完整通关与磁盘重读验收。

## 配置身份、攻击、掉落池与声音编码（2026-11-04）

依据 `ProjectGuidelines/20-Configs.md`，本轮统一 EntityId/EnemyId、去除实体资源重复主键，按实体/动作/段号编码攻击，按品质或 Boss 身份编码掉落池，并整理声音事件段。HeroId、EquipmentId、SkillId、PassiveId 等持久身份未重编。

| 检查 | 实际命令与结果 |
| --- | --- |
| 正式导表 | `$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：成功，`bye~`；C# 与 bytes 仅经此流程生成。 |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误，5 条既有框架弃用警告。 |
| 配置/档案测试 | `dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet`：246/246；最终复跑通过，另有无法获取 NuGet 漏洞数据的 NU1900 网络警告。 |
| 战斗测试 | `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet`：191/191。 |
| Schema/可读性 | `audit_config_schema.py`：17 个映射 Sheet、0 错误；`audit_config_readability.py`：11 本表、24 个枚举列，英文值/缺失别名/错误均为0。 |
| 引用/包格式 | `validate_game_references.py`：2072 路径、378 UID 通过；`normalize_xlsx_namespaces.py`：0 本待修复；`git diff --check` 通过。 |
| XLSX Diff 解析 | 已安装 1.6.4 worker 对 Resources/Enemy/Combat/Items/__enums__/__tables__ 的值与公式两模式均返回全部非空 Sheet；右侧变化格分别103/26/677/76/162/1（值模式）。表值/公式与审核稿逐项相同，未编辑 ZIP 部件逐字节保留。Artifact 预览检查实体主键、英雄攻击与掉落分组。 |
| Godot 扫描 | `S:/Godot4/Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --build-solutions --quit --no-window`：退出0；退出期仍有既有 ComponentInsoector RefCounted/free 诊断。 |
| 实际 OpenGL 音乐 | 隔离 APPDATA 的 `run_godot.py identity_music_20261104 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --chapter-music-only`：退出0；洞窟/海底/牛魔谷播放、进度推进、暂停停止及洞窟场景绑定通过；退出期3条既有 ObjectDB 泄漏诊断。 |
| 悟空实际回归 | 同入口 `identity_hero_20261104 ... -- --smoketest`：退出0，39次状态迁移、5次真实猴子受击、连击 HUD 显示；退出期8条 ObjectDB 与1条资源诊断。 |

证据位于 `.godot/validation/identity_layout_20261104/` 和 `movement_presets/identity_*_20261104/`。worker 检查不能代替 VS Code 面板显示验证；用户此前确认 Hero 正常、其他表空白，目前尚无实际面板恢复的证据。此前对字符串类型或缓存的解释没有完成界面复现，不能认定为根因。唐僧五技能、完整通关与磁盘重读继续验收，未在此标记完成。

## XLSX Diff 共享字符串回退复核（2026-11-04）

此前将三张表所有文本统一为 `t="s"` 共享字符串后，插件 worker 仍能读到差异，但 VS Code 面板仍显示空白。已确认 Hero 使用原生 `str`/`inlineStr` 混合存储；本次从已逐格核对的原生 ZIP 保留包恢复 `__enums__`、Resources、Combat 的文本存储，再仅规范化 SpreadsheetML 默认命名空间。值和公式未变化。

| 检查 | 实际命令与结果 |
| --- | --- |
| 值/公式核对 | 三表与恢复候选包逐格值/公式相同；`Hero` 也保持相同。 |
| 命名空间 | `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py <四表>`：0 workbook(s) need repair。 |
| XLSX Diff | `node Godot/GodotProject/.godot/validation/tangseng_20261103/config/check_diff.mjs`：退出0，值/公式两模式均通过，Hero 951、`__enums__` 3、Resources 5、Combat 16 格。 |
| 生成与构建 | `cmd /c Configs\\GameConfig\\gen_code_bin_to_project_lazyload.bat`：退出0、`bye~`；`dotnet build Godot/GodotProject --no-restore -v quiet`：0警告、0错误。 |
| 包结构 | `__enums__` 使用 `inlineStr`；Resources/Combat 使用原生 `str`/`inlineStr` 混合存储，不再全量使用 `t="s"`。 |

VS Code 旧对比页可能保留此前空模型，需关闭该页后重新打开文件；本轮没有改变配置内容或设计。

## 更小的怪物列表图鉴与漩涡三态（2026-11-02）

依据第00/10/40/50章，继续使用地图拥有的单一浮层和只读Query；怪物直接由原生ItemList排布，不增加逐怪物脚本。最终208px宽、177～253px高，怪物28px图＋名称两列、守关对手浅金排首位，装备32px透明图；相对上一版面积减少约33%～56%。同名牛魔王形态去重，蝙蝠/鲨鱼召唤物补入名单，掉落规则不变。外置`brown_guide_panel.tres`沿用原棕金样式，仅收窄内边距。地图场景和三态素材保持用户暂存版本。

证据目录为`Godot/GodotProject/.godot/validation/guide_compact_20261102/`。

| 检查 | 命令与结果 |
| --- | --- |
| 官方漩涡 | 直接读取已核验OtherMat_v9源XML及元件37_109.frame1：wrapper478第1帧只有alpha=0的shape453并stop，第2帧474、第3帧477；直接提取JPEG388对比确认底图亦无漩涡。用户明确保留原版透明常态，未添加常驻图片。原图对比为`background_compare.png`，依据补入OfficialReference。 |
| 原生作者/构建 | `Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --script res://EditorScripts/author_level_guide.gd`输出PASS并保留UID。`dotnet build Godot/GodotProject --no-restore -v quiet`为0错误、5条既有警告；`--headless --path Godot/GodotProject --build-solutions --quit --no-window`扫描/构建退出0。新样式初次未登记UID而产生路径回退，正式扫描后最终运行已无此警告。 |
| 十关图鉴与名称 | `python <证据>/run_guide.py`退出0。十卡片尺寸、两列怪物、名称身份和实际文字宽度、21份图像裁切、透明装备图标通过；截图`cards/1..10.png`及`cards_montage.png`逐张查看。BOSS在前，牛魔谷只一项牛魔王；全部38件装备详情及六品质仍通过。 |
| 原生鼠标与漩涡 | 同一专项通过十地点悬停→移入装备→嵌套详情→离开关闭、940×590和1175×738窗口边界、真实点击花果山及HUD就绪清理。另实测漩涡普通图透明、最新关48帧/24fps实际运动、悬停图、离开恢复及通关后透明，注册位置和画布大小保持；`frontend/guide_whirlpool_{normal,current,hover}.png`已查看。 |
| 查询与引用 | `dotnet test Tests/ProfileTests --no-restore -v quiet`为232/232，覆盖名单包含实际配方、顺序/去重及召唤物；仅有既有NuGet漏洞源不可达警告。`python Tools/ProjectMaintenance/validate_game_references.py`为1885路径/379UID通过，`git diff --check`通过。 |

验证不接触正式存档和Git索引。最终图鉴运行退出时仍有5个ObjectDB/1资源存活诊断；原生编辑器保留既有ComponentInsoector释放诊断，未修改只读插件。未复跑无改动的整套战斗和背包流程；共用装备详情已在本次图鉴专项逐件验证。

## 龙王血条、施法规则与攻击范围（2026-11-01）

依据第10/30/40/50章：专属规则留在龙王状态，范围归原生动画，测试侧观测不扩充生产API。源为[Monster3_v3.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Monster3_v3.swf)，SHA256与OfficialReference一致。以下`<证据>`为`Godot/GodotProject/.godot/validation/dragon_review_20261101`，`<源>`为`.godot/validation/remaining_levels`；Godot脚本及参数路径使用`res://`。正式存档、用户Git索引和只读依赖未改。

| 检查 | 命令与实际结果 |
| --- | --- |
| 源范围作者化 | `python Tools/LegacyMigration/prepare_enemy_attack_geometry.py --dragon-only --output <证据>/attack_geometry.json`→`repair_dragon_hitbox_nodes.gd`→编辑器`repair_new_enemy_attack_geometry.gd -- <报告>`。七根源冰柱独立，暴雪保持大范围，水波/水柱排除透明和ADD零光、保留遮罩。重复禁用占位资源合并，身体库517个矩形收敛到14个，波12个、柱40个；不改贴图、声音、攻击编号和时长。 |
| 原生几何 | `Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/validate_new_enemy_attack_geometry.gd -- <报告>`：840帧/区域样本、1224双向包含/排除查询、48柱间空隙查询及收招复位通过。`validate_dragon_resources.gd -- <报告>`另通过300源帧与房间/入口/出口/音乐绑定；旧clipped_geometry单并集报告不再作为此工具输入。 |
| 源时序不变 | `validate_dragon_art.gd -- <源>/dragon_art/actors/manifest.json <源>/dragon_art/source_rules.json`：315身体帧、complete标记、六次新攻击编号、三段无敌、效果创建与冰块绑定通过，退出0。 |
| 真实四招 | `python <证据>/run_ui.py dragon-only`：32组（普通/高级×60/120Hz×双向×四招）、五次受击后的英雄连击保护、无双状态免疫、暂停和活动水波取消全部通过，退出0。初次运行在第24组截图等待超时；测试截图改为ForceDraw当前姿势后完整重跑通过，未改变游戏逻辑来避过失败。 |
| 实际规则与空隙 | `python <证据>/run_ui.py dragon-rules-only`：20/21级×60/120Hz×双向8组通过。真实AI覆盖99.9/100/100.1/450/450.1px、无高度门、单/双技能就绪、冷却、50%选招及冷却中1/1.5秒普攻欲望；四招以±96水平速度起手不滑行，技能保持原朝向。真实冻结约1.983～2.000/4.983～5.000秒、暂停不扣时。普通档四组另将完整悟空受击盒放入柱间，停留不扣血，再移入柱内真实扣血。 |
| 共享名称 | OpenGL `validate_encounter_name_alignment.gd`：11个名称渲染通过，包括蝙蝠洞石像；名称固定右端、长名向左扩展、覆盖条左cap，整体控件顶部居中。已查看龙王/牛魔王/五字名称、冰环/暴雪/水波/水柱叠加图与实际战斗截图。 |
| 隐藏关与共享效果 | `python <证据>/run_ui.py dragon-flow-only`：真实S+K下水、暂停淡出、GGF转场、收益保留、旗幡拾取、死亡后开门、W出口、磁盘检查点与返回地图通过；`effect-shapes-only`：60/120Hz第二形状单独命中、共用去重/预算、暂停、取消、复用通过，均退出0。 |
| 构建/规则/引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有弃用警告；`dotnet test Tests/{ProfileTests,BattleTests,LevelTests} --no-restore -v quiet`：228/228、178/178、19/19；`python -m unittest discover -s Tests/ToolTests`：23/23。`validate_game_references.py`为1852路径/376UID通过，`git diff --check`通过。`Godot4CSharp_console.exe --headless --path Godot/GodotProject --build-solutions --quit --no-window`完成扫描/构建、退出0；保留下述既有插件释放诊断。 |

范围是对官方像素命中的基础形状近似，矩形内部仍有少量留白，不称逐像素等价。`run_ui.py`隔离APPDATA；通用末尾汇总文案不代表运行了完整UI生命周期。dragon-only和rules退出各报告5个ObjectDB；flow为11个ObjectDB/3资源，effect-shapes为5个ObjectDB/1资源。编辑器作者化仍有ComponentInsoector的RefCounted.Free及RID诊断，原始日志`geometry_author_compact.log`保留；未扩大修改框架/插件。

## 牛魔谷与火魔斩六项核对（2026-11-01）

依据第30/40/50章：具体首领规则与生命周期归本角色，帧序、范围与启闭归原生动画；只用少量基础形状，不新增通用表开关、像素多边形或生产观测API。原文件为OtherMat_v9及Monster2_v4（实际SHA256 `42a301a5ae4222aec54dfdc4c14efced48b9ad4f979902471011ff226d8cd09b`）。原始XML/脚本、PNG及新的失败/通过证据位于`Godot/GodotProject/.godot/validation/bull_review_20261101/`及既有remaining_levels目录。Framework、addons、Luban和正式存档未改；之前补齐的第1格及法宝修复保留。

六项结果：牛魔谷恢复1:1最新提示和官方原点；火魔池化复用700px误伤已复现并修复；人形438个正式渲染帧与官方源图相同，普通第二挥的黑斧风与hit2变牛冲锋保留；原版hit4确为变形震地，判定改为六阶段本体/地刺范围；心脏秒数保留官方规则，修复Stop恢复旧hide起点造成的错误提前显现；火柱改用喷火帧的240～286px高度，并按第36帧先命中后回收。

以下`<证据>`为上述bull_review_20261101目录；`<源>`为`.godot/validation/remaining_levels`，路径传给Godot时使用`res://`。

| 检查 | 命令与实际结果 |
| --- | --- |
| 地图实际画面和输入 | `Godot4CSharp_console.exe --path Godot/GodotProject --rendering-method gl_compatibility --audio-driver Dummy --script res://EditorScripts/validate_bull_map_layout.gd`：退出0，19帧原比例、原点、真实鼠标进入/扩展图内停留/点击/离开通过。已查看`map/current_09.png`与`hover_05.png`，无巨大牛角覆盖地图。 |
| 火魔修复前后 | `python <证据>/run_ui.py fire-overlap-only`：修复前`fire-overlap-before.log`明确失败“60Hz池化九连误伤了700px以外的旧目标”；修复后60/120Hz均通过旧缓存拒绝与本地真实伤害。未降低寻敌130px或禁止近邻范围伤害。 |
| 火魔真实连招 | `python <证据>/run_ui.py fire-knockback-only`：退出0，猴子/大猩猩×左右×60/120Hz共8次真实输入，九连、下冲、落地均实际命中，击退朝向正确。`effect-shapes-only`同样退出0，两频率的第二形状单独命中、共用去重/预算、暂停、取消和复用通过。 |
| 正式贴图与源帧 | `audit_registered_actor_packing.py --manifest <源>/bull_art/actors/manifest.json --output <证据>/source_audit`：644源帧像素/注册点通过。OpenGL `validate_registered_actor_atlas.gd -- <同manifest> bull_demon_king res://TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_frames.tres <证据>`：438正式渲染帧通过，含两段挥斧、冲撞与震地；三份源动作联系图已查看，未发现缺帧、错绑或坐标移位。 |
| hit4六阶段范围 | `prepare_enemy_attack_geometry.py --bull-only --output <证据>/attack_geometry.json` → 编辑器`repair_new_enemy_attack_geometry.gd` → `validate_new_enemy_attack_geometry.gd`：54帧、140项双向包含/排除及旋转复位通过。键位在20/30/33/37/41/53源帧，初段约153×166，地刺完全扩散约451px；叠加图`bull_demon_king_attack_4_review.jpg`已查看。这里是基础矩形近似，包含内部空隙。 |
| 心脏修复前后 | `python <证据>/run_ui.py bull-heart-timing-only`：修复前复用时预期隐藏5秒，但0.008秒已alpha=1，证据`bull-heart-timing-before.log`。修复后60/120Hz均通过：首次隐藏5～9秒、两秒淡入、完整显现2～5秒、两秒淡出、下一隐藏5～11秒；首个完全显现物理步可受伤，半透明时拒绝伤害，受伤不重启周期。 |
| 火柱高度与真实伤害 | `repair_bull_fire_geometry.gd -- <证据>/fire_bounds.json` → `validate_bull_fire_geometry.gd`：36源帧、预警无伤害、上部相交/上界外排除均通过。上述`bull-heart-timing-only`另实测两频率下-350px处不受伤、可见火柱-220px处真实扣血；六阶段叠加图`fire_geometry_review.jpg`已查看。阶段形状不可变，多根火柱不共享可变尺寸。 |
| 全资源与实机首领 | `validate_bull_resources.gd -- <源>/bull_art/actors/manifest.json <源>/bull_art/monsters/manifest.json <源>/all_levels.json <证据>/attack_geometry.json`：1324源帧双向样本通过。最终`python <证据>/run_ui.py bull-only`退出0：兽形/护卫、60/120Hz双向五招、五次吞噬与释放、心脏暂停、手/火池清理、金钢琢首次/重复/满包及复用全部通过。 |
| 构建、纯规则及引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有弃用警告。`dotnet test Tests/{ProfileTests,BattleTests,LevelTests} --no-restore -v quiet`分别228/228、178/178、19/19；仅NuGet审计服务不可达警告。`python Tools/ProjectMaintenance/validate_game_references.py`为1853路径/375UID通过；`git diff --check`通过。 |

`run_ui.py`每个模式使用隔离APPDATA和`--smoketest=ui --<模式>`，测试范围以上表为准，不将通用末尾汇总文案当作完整UI生命周期验收。最终bull-only退出仍有10个ObjectDB和1个资源存活；心脏专项9个ObjectDB/2资源，火魔/多形状专项也有存活资源诊断。原生编辑器作者化退出另复现ComponentInsoector的`RefCounted.Free`/不存在free及RID泄漏，原始输出保存在`geometry_author.log`；没有修改只读插件或用业务PASS声称这些诊断已解决。

## 全装备与法宝穿戴（2026-10-31）

第1格已备份后更新为 v12：保留原有进度，装备集合补齐当前配置 1～38（40 个实例，原有重复实例保留），金钢琢写入悟空独立法宝槽。第2/3格补齐 `TreasureInstanceId` 空字段并升到 v12。

| 检查 | 命令与结果 |
| --- | --- |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误，5条既有 Framework/生成代码弃用警告。 |
| 纯规则测试 | Profile `222/222`、Battle `178/178`、Level `19/19` 通过；法宝测试覆盖穿戴、属性变化、保存、重建与卸下。 |
| 存档读回 | 正式 `SaveDataUtility.Validate` 验证第1/2/3格全部通过；第1格 `equipment=40`、金钢琢 `TreasureInstanceId` 非空。备份目录为 `GameData.backup-before-treasure-slot-20261031_214739`。 |
| UI 烟测 | `--smoketest=ui --equipment-only`：`SMOKE-TREASURE GUI PASS`，跨页选择、独立法宝槽、tooltip、穿戴、属性替换和法宝替换通过；退出业务码0。 |

## 框架关停生命周期（2026-10-31）

根因是 `BaseComponent` 在 `OnPreDestroy` 才调用框架 Shutdown，此时 Godot 已先释放 `HttpRequest`、声音辅助器和部分 UI 节点；任务池随后访问已释放对象，分别产生 WebRequest `ObjectDisposedException`、声音代理异常和窗口模型空引用。修复将既有 `GameEntry.Shutdown(ShutdownType.None)` 移到 `BaseComponent.OnExitTree`，并让标题、选角、地图、关卡流程在关停时先关闭自己拥有的窗口；普通导航继续使用原淡出，关停立即停止音乐。背包确认框只在仍在场景树内时释放焦点。框架授权登记在第50章。

- `validate_framework_shutdown.py`：本地回环 GET/POST、超时后复用、4条在途+2条排队请求、显式重复关闭、同进程重建，`FRAMEWORK SHUTDOWN PASS`；无 `ObjectDisposedException`、无对象/资源泄漏诊断。
- `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误、5个既有生成代码弃用警告。
- `--smoketest=frontend`：退出0，开始/存档/选角/地图流程通过；存档损坏、旧作用域和预期失败注入仍按测试设计输出错误日志。
- `--shutdown-window=inventory|skills|settings`：三种真实暂停窗口均能在退出前完成退订；背包焦点错误不再出现。
- `dotnet test`：Battle 178/178，Profile 211/211；C# 风格346文件通过，引用1852路径/374 UID通过，`git diff --check`通过。

剩余限制：Godot 原生 `AudioStreamPlayer` 在独立、无 GGF 的 MP3 播放脚本中也会报告退出时 `AudioStreamPlaybackMP3`/资源仍在使用，属于引擎退出行为；本轮没有用业务层强行释放或修改只读插件。编辑器 `ComponentInsoector` 的 `Free()` 错误是另一个插件生命周期问题，不属于本次运行时框架修复，仍按既有插件记录保留。

## 全敌人完整性与官方行为（2026-10-30）

本轮使用v25928官方主程序及Monster1/2/3缓存，散列见OfficialReference。只修改项目自有代码、源表及经既有作者工具保存的资源；玩家正式存档和Git索引保留。规范依据为第10/20/30/40/50章；第30章的旧首掷延迟/通常1～2形状与用户本轮官方行为、四冰柱要求有差异，本轮以用户要求为准，规范未改。

在仓库根执行，Godot的脚本参数是资源路径，末尾`--`后为用户参数：

```powershell
$project = 'P:/Godot-Project/GameZMXY/Godot/GodotProject'
$work = "$project/.godot/validation/enemy_fidelity_20261030"
$runner = "$project/.godot/validation/movement_presets/run_godot.py"
$env:AI_MODE = '1'
cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat
dotnet build Godot/GodotProject/GodotProject.csproj --no-restore
dotnet test Tests/BattleTests/BattleTests.csproj --no-restore
dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore
dotnet test Tests/LevelTests/LevelTests.csproj --no-restore
python Tools/ProjectMaintenance/audit_config_schema.py
python Tools/LegacyMigration/audit_official_enemy_fidelity.py --output "$work/source_audit_final.json"
python $runner enemy_final_import --headless --editor --import --build-solutions --quit
python $runner enemy_final_geometry --headless --script res://EditorScripts/validate_enemy_fidelity.gd -- "$work/ice_regions.json"
python $runner enemy_boundaries --headless --script res://EditorScripts/validate_enemy_boundaries_and_knife.gd -- "$work/knife_source_frames.json"
python $runner enemy_final_visual --rendering-method gl_compatibility --audio-driver Dummy --script res://EditorScripts/render_enemy_grounding.gd -- "$work/native_final"
```

Luban成功只覆写`enemy_tbenemyconfig.bytes`；构建0错误，5条既有依赖弃用警告。Battle232、Profile211、Level19项均通过，配置17张映射表0错误。源码报告24种、1066格、71条攻击；可映射的数值无差异，扩展/旧预设列有明确分类，不把它们计为官方已证实值。Enemy.xlsx共39格变化（11丢失时限、飞刀间隔、灯笼鱼视野、24受伤后延迟、2基础魔抗），原ZIP其余部分/样式保留，已安装XLSX Diff 1.6.4 worker在数值和公式模式均读到39格。

原生检查：13个小怪装配完整；四冰柱360个形状样本、102个真实shape间隙排除，左右与idle复位通过。11关86处玩家门允许敌人穿过、22处外墙仍阻挡，飞刀27源帧几何/位移与官方逐帧一致。24种敌人429个原生动作截图含死亡全部生成并人工查看；最终形态牛魔王另外查看`bull_heart_visible.png`的完整实机背景、心脏、双手。最后完整导入后的几何和渲染stderr为空，原先SpriteFrames缓存UID/旧纹理尺寸问题已消除。

真实回归入口如下，均使用隔离APPDATA和Validation存档。可用上面的runner替换Godot启动命令；`--enemy-hierarchy-only`、`--bat-cave-only`、`--dragon-only`需要普通OpenGL窗口观察画面，其余可headless。每次指定唯一case目录，不与玩家档混用。

```powershell
python $runner <case> --headless --audio-driver Dummy --max-fps 60 -- --smoketest=ui <专项参数>
python $runner enemy_ai_official_ready --headless --audio-driver Dummy --max-fps 60 -- --smoketest=ai
python $runner enemy_bosses_final --headless --audio-driver Dummy --max-fps 60 -- --smoketest=bosses
```

| 专项参数 / 实际case | 验证内容与结果 |
| --- | --- |
| `--enemy-fidelity-only` / enemy_fidelity_ready | 24种原点索敌两侧严格横界/含纵界、视野外保留目标；6种近战60/120Hz自主追击/节拍，飞刀实测1→5秒投刀；PASS、退出0 |
| `--enemy-knockback-only` / enemy_knockback_live | 首中末战斗门、两朝向、60/120Hz实际ReceiveHit/身体推进穿越；PASS、退出0 |
| `--effect-shapes-only` / enemy_effect_shapes | 仅第二形状命中、共享去重/预算、暂停、取消、复用、自然回收；PASS、退出0 |
| `--monster-health-only` / enemy_health_final | 全13小怪实际生命上限（含大猩猩守关900）、两秒淡出、治疗、暂停、连续受伤、头顶位置、死亡、隐藏及池复用；PASS、退出0 |
| `--sea-ai-only` / enemy_sea_ai_clock | 虾兵追退/40%出招，蟹150px/十秒重击，龟400px/60%出招，高处/暂停，60/120Hz；PASS、退出0 |
| `--east-sea-ai-only` / enemy_east_ai_clock | 鲛魔王远近分支与灯笼鱼自主行为，两频率；PASS、退出0 |
| `--shark-ai-only` / enemy_shark_ai_clock | 召唤鲨鱼自主追踪、折返节流、速度上限与近身选招，两频率；PASS、退出0 |
| `--garuda-ai-only` / enemy_garuda_ai_ready | 自主出招、共享冷却、起飞/落下、退避及受击连续性，两频率；PASS、退出0 |
| `--qixing-only` / enemy_knife_regression_fix | 六招双向、刀/云实际命中、毒、旋转无敌、暂停与复用；PASS、退出0 |
| `--shark-only` / enemy_shark_shapes | 两档生命、两频率、双向，水牢预警、独立效果、召唤死亡/隐藏取消及复用；PASS、退出0 |
| `--garuda-only` / enemy_garuda_shapes | 两频率双向五招、淡出时持续命中、蛋/复生/死亡与复用；业务PASS、退出0；有两条资源加载GCHandle异常，单列在下方 |
| `--bull-only` / enemy_bull_shapes | 冲撞/休息、牛妖双向、两频率五招、五次吞噬、心脏/双手/火柱、直接奖励、满包与复用；PASS、退出0；实机截图已查看 |
| `--dragon-only` / enemy_dragon_shapes_fix | 两等级、两频率、双向四招、水波/水柱位置、七段冰环及英雄第五次后保护、冰冻/无双免疫、暂停和弹体取消；PASS、退出0 |
| `--smoketest=ai` / enemy_ai_official_ready | 猴子受击激怒、追击、出招锁向、恢复、高处不可达、重新落地、视野外保留、受击/死亡/回池；PASS、退出0 |
| `--smoketest=bosses` / enemy_bosses_final | 前两位首领五招实际命中、无敌、风刃独立时钟、隐藏和复用；PASS、退出0 |

补充最终整合：`enemy_bat_complete`（蝙蝠追踪/空中攻击/受击/收招/死亡、石像与落刺）、`enemy_sea_complete`（浅岸三物种四招双向两频率、弹体击散/淡出/暂停/复用）、`enemy_east_complete`（东海四招双向两频率、背防、死亡/复用，冰柱真实四次命中）均PASS、退出0。层级/声音回归及妖猴逐绘制帧检查在`enemy_hierarchy_verified`全部PASS、退出0。此前声音夹具将池实例的物理推进冻结，现测试侧恢复，左右自主追击与高处目标均无一帧举棒；没有给生产代码添加测试兜底。

复现作者化：

```powershell
python Tools/LegacyMigration/prepare_grounded_enemy_art.py --manifest "$project/.godot/validation/remaining_levels/sea_art/actors_registered/manifest.json" --extra-manifest "$project/.godot/validation/action_origin_review/ground_monsters_export/manifest.json" --output "$work/grounded"
python $runner enemy_ground_author --headless --editor --script res://EditorScripts/apply_grounded_enemy_art.gd -- "$work/grounded/manifest.json"
python Tools/LegacyMigration/prepare_enemy_fidelity_geometry.py --xml "$project/.godot/validation/official_continuation/monster2_full.xml" --manifest "$project/.godot/validation/remaining_levels/sea_art/actors_registered/manifest.json" --output "$work/ice_regions.json"
python $runner enemy_shapes_author --headless --editor --script res://EditorScripts/repair_enemy_fidelity.gd -- "$work/ice_regions.json"
python $runner enemy_bars_author --headless --editor --script res://EditorScripts/repair_monster_health_bars.gd
python $runner enemy_detection_author --headless --editor --script res://EditorScripts/apply_enemy_detection_ranges.gd -- "$work/source_audit_final.json"
# 之后必须完整import，再执行原生几何、渲染与真实命中回归。
```

初次回归的失败与修正：海怪右向第二招因冻结英雄身体连带冻结五连击保护，夹具改为每次独立施放前清零保护；正式受击规则不改。龙王冰环测试此前错误期待冻结英雄连吃7次，现同时检查6次原生刷新与5次真实扣血，最后两段被官方保护免疫。血条夹具最初忽略大猩猩守关900；索敌夹具暴露旧21px偏移，实际场景修正；多项旧测试的Move/SkyMonsterMove旧名和旧站位已改用当前Walk/Fly及明确落地条件。飞刀1→6秒投刀失败促成冷却请求起点与源步末扣减修正，最终1→5秒通过。

依赖诊断独立记录：编辑器退出仍有ComponentInsoector的RefCounted.Free/Invalid call和RID清理；运行退出仍有WebRequest已释放HttpRequest的ObjectDisposed及少量ObjectDB/resource泄漏。enemy_garuda_shapes在GGF的LoadThreadedGet链路出现两次`SwapGCHandleForType: Handle is not initialized`，但各招断言及退出0；这是仍存在的引擎/依赖资源桥接异常，不能称运行日志全净，本轮未修改addons/Framework。专项业务结果与这些诊断分别报告。

## 剩余造一关卡统一交付（2026-10-28）

依据第00/20/30/40/50/60章，按官方v25928资料完成六关当前指纹验收。全部测试使用隔离APPDATA和存档；正式玩家档、Git索引、框架/插件/旧工程源码与Luban本体未改。日志相对`.godot/validation/remaining_levels/current_final/`。

| 命令 / 检查 | 当前结果 |
| --- | --- |
| `python .../current_suite.py --resume`；`record_current_suite.py` | `final_20261028.log`、`imagery_20261028.log`及`commands_levels.json`记录79项当前成功命令；六关每道门引用实际命令、退出码、日志SHA和工程指纹，不刷新历史收据冒充重跑 |
| 官方依赖清单；`audit_source_recipes.py` | 11个官方标签、24种动态敌人；82条出生流身份/数量/首只等待/后续间隔一致。独立核对发现浅岸16条首只等待少2秒，已按`delay+interval`修正LevelConfig的S111:S126，后续间隔不变 |
| Artifact作者化、原ZIP局部转存、XLSX Diff、Luban | `shore_timing/`保留前后工作簿/预览；普通与公式模式均只有16格差异，其他ZIP部件保持。既有入口重建关卡bytes；`build_after_generation.log`0错误 |
| `validate_wukong_wardrobe_motion.gd`；`validate_official_wukong_attacks.gd`；`--equipment-only` | 38件外观、15件悟空装备、2158源格、23904原生播放样本、5208普攻样本及真实跨页穿卸通过。像素审计改为无图形阶段，全部组合播放/截图另在OpenGL执行；覆盖不减 |
| 原生图像与六关全景 | 32组敌人/效果检查通过：31组原生动画绘制对照及虾兵19×13单帧弹体。前景/地形全幅像素对照通过；动态天空、陷阱、出口由场景和实机检查。两张ADD预览首次因目录缺失未写入，创建隔离输出目录后重跑对应两项通过，PNG已查看 |
| 2-3、3-1、3-2、3-3、4-1、9-1场景与实机专项 | 全部BOSS/AI/判定/掉落/暂停/结算/失败重试/地图返回/磁盘重读通过；五主线完整通关。隐藏龙宫覆盖真实下水、1秒淡出、两档龙王四招、首次旗幡、死亡后出口与主线进度隔离 |
| `--chapter-music-only`及各关Music断言 | Cave/Sea/Bull三首章节曲的GGF代理、循环、播放推进、暂停策略和停止通过。官方无专属招式音的敌人不虚构事件，保留真实英雄命中反馈。测试使用Dummy音频驱动，不声明做过扬声器听感验收 |
| Profile/Battle/Level；ToolTests | 208/223/19/20项通过，合计470。工具新增拒绝漏算首次interval、错误怪物/流集合、旧清单缺逐流证据；关卡就绪审计持久集成同一校验 |
| `python Tools/LegacyMigration/audit_remaining_level_readiness.py --inventory Godot/GodotProject/.godot/validation/remaining_levels/dependency_inventory.json --work Godot/GodotProject/.godot/validation/remaining_levels --output Godot/GodotProject/.godot/validation/remaining_levels/readiness_final_20261028.json --strict` | 退出0，**6/6 complete、每关0 open gates**；源SHA和82条配方均无错误 |
| `git diff --check`；`git diff --cached --check` | 通过，Git索引未改 |

当前工程指纹为`4eaccece3f58372dbfa6aab022204b9f8fe3f67c572dc1d640acb781b2045ef5`；官方清单SHA为`f9b841f512fb0a551cc4c484fd0d85389bd4b810118484025c2958732d48048e`。首次GPU纹理读回发生内存分配崩溃的日志、配方对比首次失败日志均保留。最终游戏进程仍有既有WebRequest关停ObjectDisposedException、ObjectDB/RID/资源存活诊断，退出码和玩法断言通过；没有修改只读依赖来消除这些诊断。法宝按用户已定范围仅获取/背包/存档，主动玩法未开放；唐僧等未开放角色不属于本次关卡制作。

## 常用命令

```powershell
# 改源表后先导出；AI_MODE=1 跳过脚本末尾 pause
$env:AI_MODE = '1'
cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat
dotnet build Godot/GodotProject --no-restore -v quiet
dotnet test Tests/BattleTests --no-restore -v quiet
dotnet test Tests/ProfileTests --no-restore -v quiet
dotnet test Tests/LevelTests --no-restore -v quiet
python Tools/ProjectMaintenance/validate_game_references.py

# 真实 UI、物理、流程；写入隔离 Validation 存档
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=ui
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=level
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=frontend
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest
# F6 独立测试场地，不启动正式流程或读写玩家档案
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360
```

按修改范围选用 `--smoketest=level-cancel`、`ai`；规则单测无需打开编辑器。场景装配用 `--headless --script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，编辑器 Tool 再加 `--editor`；触发区用 `validate_level_triggers.gd`，动画库用 `validate_anim_libraries.gd`。有渲染器时，`validate_musou_flash.gd` 检查原版无双 19 帧透明动画；`validate_skill_layout.gd` 检查技能面板坐标、45px 图标、造三五入口及无双黑底板的空/半满/满值装配，`validate_level_up.gd` 检查升级蓝线裁剪、画面和注册尺寸。

XLSX Diff 1.6.4 的解析器无法读取带 `x:` 前缀的 SpreadsheetML 标签，会错误地显示零张表。导表工具仍能读取这类文件。遇到该问题时，用 `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py --write <文件.xlsx>` 将标签改为等价的默认命名空间；脚本逐项验证 XML 内容并原子替换，不重新生成单元格或样式。不带 `--write` 时只检查。已暂存的文件需要重新暂存修复后的版本，工作区修复不改 Git 索引；历史版本若含相同前缀，插件仍无法读取历史侧。

引擎截图写入忽略的 `.godot/validation/`；无双开场及后半段分别为 `musou_activation.png`、`musou_activation_tail.png`，升级为 `level_up.png`、`level_up_tail.png`，原帧/裁剪帧对照为 `level_up_frames.png`，BOSS 条为 `encounter/featured_health_bar.png`。通过 PASS/FAIL、退出码、错误与画面共同判断结果。

战斗背包为 `inventory_form.png`，品质上拉菜单为 `inventory_quality_sale.png` / `inventory_quality_sale_scaled.png`，出售确认框为 `inventory_sale_confirmation.png` / `inventory_sale_confirmation_scaled.png`，多属性详情为 `inventory_tooltip_attributes.png`，失败结算为 `settlement_failure.png`，成功结算为 `encounter/settlement_success.png`。成功页截图在真实检查点完成后立即采集，可能带有短暂的保存成功提示；提示按真实时间淡出，背包和结算暂停不会将它冻结。

装备背包与实际详情为 `inventory_equipment.png`，操作菜单为 `inventory_actions.png`，武器/防具同时穿戴为 `inventory_wearing.png`，绿色/蓝色恢复数字为 `recovery_numbers.png`；无名称的地面恢复品和装备为 `ground_pickups.png`；地图默认边缘闪烁的两端画面为 `frontend/map_current_low.png`、`frontend/map_current.png`。

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
