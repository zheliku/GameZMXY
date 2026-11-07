# 双人扩展实施计划

状态：**双人02A隔离草稿暂停；主工作区先完成微软C#命名/格式迁移。** 主工作区仍为单人v12。02A仅在`P:/Godot-Project/.codex-worktrees/GameZMXY-coop-02a`形成ProfileModel、PlayerProfile、SkillModel三文件草稿，尚未核验完成或应用；02B及以后未开始。当前项目代码使用PlayerSlot及微软字段命名，后续继续02A时须基于新命名重放并核验草稿，不能直接覆盖当前源文件。本文件是双人实施范围、步骤和审查边界的唯一计划；实际规则来源见[OfficialReference](OfficialReference.md#双人扩展的源码依据2026-11-07)，验证记录见[Validation](Validation.md)。项目规范仍以[ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md)为准。

## 已确定的需求

- 本地同屏、同键盘合作。一格存档保存双方，单人和双人共用一种结构。
- 每人独立拥有经验、灵魂、背包、穿戴、主动和被动技能；关卡进度和档案级一次性奖励共享。
- 两名参战者不能选择相同英雄；可选角色来自已实装目录，不能写死悟空加唐僧，未来新增英雄适用同一规则。
- 玩家身份使用`PlayerSlot.P1`、`PlayerSlot.P2`，与`HeroId`、GGF实体编号、六格存档的`SlotNumber`分开。
- 复用一个根Architecture、现有System、英雄状态机、技能效果、伤害公式和GGF生命周期。
- 继续遵循[Stop That Shit 0.2.5](https://github.com/lennney/stop-that-shit/blob/0.2.5/skills/stop-that-shit/SKILL.md)：完成当前批准的步骤及必要调用方，不自行扩大到下一步。

## 每次如何推进和审查

1. **一次只执行一个明确编号的审查批次。** 先列该批实际文件及接口，完成后给出diff、相关验证和剩余问题；用户审阅后再进入下一批，不因上一批通过就自动继续。
2. 下文“迁移单元”表示必须最终一起闭合的调用链，**不等于一次修改许可**。其中A/B/C是分别提交审查的批次，禁止再次把整个单元或多个单元一口气落进工作区。
3. 根Model字段移动会影响多个消费者。此类单元允许在隔离工作树中按A/B/C审查，完成全部批次后统一构建、验收，再应用回主工作区。中间批次若不能独立编译，交付时明确说明；不为了表面上每批可编译，增加默认P1兼容视图、重复状态或临时全局玩家。
4. 新发现的必要调用方先补入当前批文件清单并说明原因；若跨到另一个领域或明显增加审查量，先更新计划，不能直接执行。
5. 一次规则变化与单纯参数迁移分开说明。只运行受影响的检查；完整回归放在迁移单元完成时，不在每个机械修改后重复跑全项目。
6. Framework、addons、Luban、旧项目及生成物保持既有只读边界。源表、UI绑定或资源确需调整时先登记范围，只经项目规定入口生成。不得覆盖正式玩家存档，不自动暂存或提交。

## 目标数据与接口

| 位置 | 拟采用的形状与职责 |
| --- | --- |
| 玩家身份 | 普通枚举`PlayerSlot { P1 = 1, P2 = 2 }`，建议放`GameScripts/Models/PlayerSlot.cs`，共享命名空间`GameLogic`；不另建身份管理器 |
| ProfileModel | 一个根Model持有`PlayerSlot → PlayerProfile`普通子数据，以及共享关卡进度、旗幡资格；`ActivePlayerSlots`表示本次参战名单 |
| PlayerProfile | 原单人档案中的英雄成长、钱包、背包及穿戴字段；不实现IModel、不独立注册 |
| SkillModel | 以`(PlayerSlot, HeroId)`区分主动与被动学习记录 |
| HeroModel | 一个根Model持有`PlayerSlot → HeroState`普通子数据；HP/MP/WS、属性来源、状态效果与时钟各自独立 |
| System | 保留现有实例；个人业务接明确玩家，例如`Damage(PlayerSlot player, int amount)`、`TryEquip(PlayerSlot player, Guid item)` |
| 技能与实体 | `ISkillActor`、`IHeroBody`及真实英雄提供所属玩家；技能激活从actor取得身份；GGF显示参数携带明确玩家 |
| 个人窗口/请求 | FormData、Command、Query传入所属玩家；窗口订阅并退订同一个人的状态，不修改全局“当前玩家” |
| 存档 | 拟升v13：`Players`保存所有已建立的个人档案，`ActivePlayerSlots`保存参战者，共享字段只一份；枚举按明确数字1/2序列化，不增加一套存档字符串规则 |
| 攻击来源 | 沿用AttackSource的OwnerEntityId/Caster/Emitter，补充玩家归属值；敌人或环境明确没有玩家归属，不能默认为P1 |

`PlayerSlot`只表达两名本地玩家；未来第三种、第四种英雄仍用HeroId扩展，与玩家枚举无关。参战人数由名单派生，不同时维护一个可能矛盾的Coop布尔和PlayerCount。

## 顺序与依赖

| 编号 | 交付 | 依赖 | 当前状态 |
| --- | --- | --- | --- |
| 01 | 玩家身份枚举 | 无 | 已实施，待审查 |
| 02A～02C | 个人档案与技能所有权 | 01 | 02A隔离草稿暂停；02B/C未开始 |
| 03A～03C | 整档存储、转换和目录 | 02 | 未开始 |
| 04A～04C | 局内属性与状态效果 | 02、03 | 未开始 |
| 05A～05B | 施放登记、英雄和效果归属 | 04 | 未开始 |
| 06A～06B | 个人窗口、Command和Query | 02～05 | 未开始 |
| 07 | 正式单人回归，关闭数据迁移单元 | 02～06 | 未开始 |
| 08A～08B | 单/双人选择、非重复选角与装配 | 07 | 未开始 |
| 09 | 两套键盘输入与提示 | 08 | 未开始 |
| 10 | 双方HUD | 08、09 | 未开始 |
| 11 | 单相机、屏幕墙及队伍距离 | 08、09 | 未开始 |
| 12 | 模态窗口和无双演出协调 | 09、10 | 未开始 |
| 13A～13B | 击杀、飞行球与地面拾取归属 | 05、08 | 未开始 |
| 14 | 唐僧队友治疗 | 05、08 | 未开始 |
| 15A～15B | 一死一活、全灭、出口与结果 | 11～14 | 未开始 |
| 16 | 花果山完整合作闭环验收 | 08～15 | 未开始 |
| 17A～17B | 双方菜单与装备赠送 | 06、12、16 | 未开始 |
| 18 | 其余关卡、隐藏龙宫和生命周期收口 | 16、17 | 未开始 |

## 01：玩家身份枚举

**已实施，等待用户审查。** 本批仅新增`GameScripts/Models/PlayerSlot.cs`及Godot扫描生成的UID；需要独立链接源文件的规则测试工程增加编译项。

明确`P1=1`、`P2=2`的稳定含义及XML注释。此批不改ProfileModel、HeroModel、存档版本、技能或场景，不全项目替换整数，也不为两个枚举值新增冗余测试。构建及Godot扫描通过后交付审查。

实际代码差异只有`Models/PlayerSlot.cs`、扫描生成的`.cs.uid`及`Tests/ProfileTests/ProfileTests.csproj`的一行编译引用。BattleTests已有Models通配引用，无需修改。构建和扫描结果见[Validation](Validation.md#双人01玩家身份枚举2026-11-07)；本批完成后暂停，未经审查不进入02A。

玩家枚举曾随E前缀迁移更名，现按用户最终选择恢复微软PascalCase，继续携带原UID和1/2数值。命名/格式整理不代替双人功能迁移；执行边界仍是每批审阅后再继续。

## 02：个人档案与技能所有权迁移

原个人字段和消费者强耦合，A/B/C在同一迁移单元中分批审查；模型批次本身不宣称已经可运行双人。

| 批次 | 主要文件与变化 | 本批审查要点 |
| --- | --- | --- |
| 02A 数据 | `Models/Profile/ProfileModel.cs`、新增同目录`PlayerProfile.cs`、`Models/SkillModel.cs` | 原个人字段移动到普通子数据；根Model保留共享进度；按枚举取玩家；活动名单包含P1、席位存在、双人英雄不重复 |
| 02B 个人规则 | `Systems/ProfileSystem.cs`、`Systems/EquipmentSystem.cs` | 加经验、收支、拾取、穿卸、出售明确归属；装备实例只属于一人；唯一奖励排重覆盖双方；继续复用原公式 |
| 02C 规则证据 | `Tests/ProfileTests`中档案、装备、技能所有权用例及`TestGameScope.cs` | P1/P2交换英雄后仍隔离；无效名单、重复角色和跨玩家重复装备拒绝；第三种测试HeroId不受二选一限制 |

此单元不开放选角/双键盘、不实现赠送、不改变装备随机属性或持久InstanceId。

## 03：一格保存双方

| 批次 | 主要文件与变化 | 验收 |
| --- | --- | --- |
| 03A 格式与快照 | `Models/Save/GameData.cs`、`Utilities/Utility/SaveDataUtility.cs`、`ProfileSystem.RestoreCheckpoint` | v13 DTO、全部玩家捕获/校验/原地回滚；未知枚举、重复席位/角色/装备拒绝；第二人坏字段不能先改掉第一人 |
| 03B 实际读写 | `Systems/SaveSystem.cs`、`MainPack/Scripts/Procedure/ProcedureLoadGame.cs`、`MainPack/Scripts/Debug/ProfileTestData.cs`及对应存档测试 | 继续同一个Archive事务；新单人档仅建立P1；已有P2未参战仍保存；双方资产/技能/装备实际写盘并重读 |
| 03C 目录与旧档 | `Models/Save/SaveSlotInfo.cs`、`UI/SaveForm/SaveForm.Logic.cs`；拟新增一次性`Tools/ProjectMaintenance/migrate_save_v12_to_v13.py`及工具测试 | 六格展示双方摘要且保持原美术；v12先保留原件，只生成新路径副本及备份；按当前配置验证转换副本，不自动安装到正式目录 |

测试隔离目录使用项目既有方案。运行期只处理当前格式，不长期累积旧版兼容分支。旧档转换应用到真实玩家文件须单独明确范围。

## 04：局内属性和状态效果

| 批次 | 主要文件与变化 | 验收 |
| --- | --- | --- |
| 04A 本局数据 | `Models/HeroModel.cs`、拟新增`Models/HeroState.cs`及规则测试源文件链接 | 普通子数据承接原字段；只给参战者创建状态；不留下原单人状态副本 |
| 04B 属性规则 | `Systems/Hero/HeroSystem.cs`、`Tests/BattleTests/HeroModelTests.cs`、`MusouTests.cs`及相关ProfileTests | 伤害/恢复/消耗/无双/属性来源按玩家处理；逐人监听成长、装备、被动；保存真实委托对称退订，重建后旧状态不再被更新 |
| 04C 状态效果 | `Systems/Hero/HeroStatusSystem.cs`、`Tests/ProfileTests/FreezeTests.cs`、`PoisonTests.cs`、`HurtProtectionTests.cs`、`RecoveryTests.cs` | 冻结、中毒、受击保护、恢复节拍及死亡清理互不影响；继续现有秒制规则 |

不新增第二套HeroSystem/状态系统，也不改移动手感、伤害公式或单人无双数值。

## 05：英雄与技能归属

| 批次 | 主要文件与变化 | 验收 |
| --- | --- | --- |
| 05A 施放规则 | `Systems/SkillSystem.cs`、`Entity/Heroes/Skills/ISkillActor.cs`、`Body/Core/IHeroBody.cs`及技能规则测试 | 同一系统按玩家登记、扣费、占用、取消和无敌；两人同时施放不覆盖；学习归各自钱包 |
| 05B 实体调用 | `AbstractHeroEntity.cs`及Skills/Capture分部、公共身体/技能状态、悟空/唐僧具体状态；`Systems/Combat/AttackSource.cs`、`Entity/Effects/SkillEffectNode.cs`；`ProcedureLevel.cs`、`TestArenaController.cs`的显示参数 | GGF userData指定身份，池复用重置实体局部数据；隐藏一人只撤销本人登记；离身/分身攻击冻结玩家归属，命中无双收益不串人 |

05B的确切角色文件清单在执行前根据调用图列出；按公共宿主、悟空、唐僧/效果分次审查，不直接把整个英雄目录批量格式化。正式关卡仍只生成P1，双人显示在08进行。

## 06：个人请求与窗口绑定

| 批次 | 主要文件与变化 | 验收 |
| --- | --- | --- |
| 06A 请求 | `Commands/EquipmentCommands.cs`、`LearnSkillCommand.cs`、`Queries/EquipmentSaleSummaryQuery.cs`和对应Command/Query测试 | 请求必须携带PlayerSlot；装备归属、费用和会话状态按提交时重新校验 |
| 06B 显示 | `UI/InventoryForm`的Data/Logic/CharacterPanel/BagPanel，`UI/SkillForm`的Logic/页面与必要FormData，`BattleFormData.cs`、`BattleForm.Logic.cs`及流程打开参数 | P2背包、技能、状态只绑定P2；关闭/失败/复用完整退订；沿用现有预览、字体、按钮和美术 |

06B按背包、技能、HUD单人绑定分别审查。这里仅完成参数和数据消费迁移，双份HUD布局及两人开窗协调在10/12处理。

## 07：数据迁移单元收口

将02～06批准过的批次合在一起核验全部原单人调用方，重点是`ProcedureLoadGame/ProcedureLevel`、地图/战斗开窗、TestArena及现有烟测夹具。先列还需迁移的调用点，再修改；不给生产代码增加测试专用入口。

完成条件：项目构建；Profile/Battle/Level规则通过；完整前端与受影响UI流程通过；正式保存/返回/放弃/死亡/重入行为保持；双人数据专项覆盖双方存档往返和独立局内状态。检查旧API、默认P1视图、重复字段和无关格式改动均已清除。完成后才能把“数据基础已完成”写进项目现状。

## 08：正式单/双人入口和非重复选角

**08A选角。** 涉及`ProcedureTitle.cs`、`ProcedureCharacter.cs`、既有`FlowData`选择参数、`CharacterForm.Logic.cs`及`character_form.tscn`。先复用现有卡片和可玩角色配置，依次确认P1/P2；已选英雄禁用，取消第二步不写半份档案。单人按钮和双人入口的具体摆放先给实际场景预览审查，不自行重画美术。新增角色只增加对应目录/卡片绑定，不增加角色组合分支。

**08B装配。** 涉及`SaveSystem.cs`、`ProcedureLoadGame.cs`、`ProcedureMap.cs`、`ProcedureLevel.cs`和`LevelViewController.cs`。一个流程拥有参战实体集合，按已选身份显示两人；原取消令牌和进入周期覆盖全部请求，第二人失败/迟到时回收本周期已取得对象。保存名单只一份，切回单人保留P2资产；一人一套Architecture不在方案内。

此步验收正确建档、重读、显示、取消和清理，不把“出现两个人物”记为可玩合作完成。

## 09：独立输入

修改`project.godot`的InputMap、`Systems/Input/InputSystem.cs`、`HeroActionData.cs`、英雄采样入口及按键提示消费者；复用原组合、边沿、双击与消费规则。

| 动作 | P1 | P2 |
| --- | --- | --- |
| 移动/上/下 | A、D、W、S | 左、右、上、下 |
| 普攻/跳跃 | J、K | 小键盘1、2 |
| U/I/O/L语义技能槽 | U、I、O、L | 小键盘4、5、6、3 |
| 无双 | 空格 | 小键盘0 |
| 背包/技能窗口（项目建议） | 保留现有C/V | 小键盘7/8 |
| 设置 | Esc | 共享Esc |

官方语义顺序依据KeyBoardControl，不能直接把小键盘3/4/5/6按递增顺序配四个槽。ui_*窗口导航与战斗动作分开。验收真实同时输入、角色交换、组合技、暂停后无补发；真实键盘的多键冲突另作人工核验。

## 10：双方HUD

修改`BattleForm.Logic.cs`、`BattleFormData.cs`及`battle_form.tscn`；复用已有`BattleStatusPanel`、`BattleMenuPanel`、无双表现。一个BattleForm持有双方个人区域，P2沿官方右侧风格；Boss条、阶段和队伍连击共用。

先审布局截图，再接绑定。检查两人的HP/MP/经验/技能/无双各自刷新，单人布局保留；任何改动涉及生成的绑定文件，只通过项目现有生成器更新。

## 11：相机和屏幕边界

修改`Entity/Level/Camera/LevelCameraNode.cs`、`LevelViewController.cs`及已存在的屏幕墙/空间工具调用处。继续单个Camera2D，取全部存活者位置计算取景；距离过大时使用既有边界限制前进，保持同屏，不瞬移落后者。

验收同向、反向、远距离、跳跃和一人死亡；阶段触发仍依实际相机，单人保留原死区与参数。需要改墙节点或场景参数时列出具体场景，不能全关无差别改资源。

## 12：窗口与无双演出协调

修改现有`ProcedureLevel.cs`的窗口/无双请求协调、BattleForm请求参数，以及Inventory/Skill/Settings窗口暂停归属；核对`MusouActivationNode`的真实调用链。

GGF暂停/恢复只是全局速度开关。由现有流程接受一个当前模态操作；同帧按稳定玩家顺序接受一个，拒绝方不扣无双、不排队；窗口/演出结束仅由接受者恢复。两人同时开窗、开窗与无双竞争、离场/取消均不得提前Resume。具体由哪个现有入口统一持有暂停，执行前列调用链确认；不新增PauseManager或通用仲裁框架。

## 13：收益和掉落

**13A致死归属。** `AbstractEnemyEntity`伤害/死亡链、`EnemyDiedEventArgs.cs`、`LevelSystem.RecordDefeat`、`LevelViewController.Loot.cs`。在实际有效致死命中时冻结攻击所属玩家，不读取怪物当前AI目标；分身和离身效果仍归本体。经验只发一份，环境无玩家来源不冒认P1；牛魔变身交接另在18覆盖。

**13B领取。** `LootOrbNode.cs`、`GroundItemNode.cs`、`LevelViewController.Loot.cs`、`LevelSystem`个人领取入口和现有LootSystem消费处。飞行球跟随其归属玩家，生成时确定恢复量；个人死亡后取消其未领球是计划建议。地面装备/恢复品由实际接触的存活玩家领取，满包留地，两人同帧按稳定席位顺序只成功一次。测试双方交替击杀、延迟击杀、不同上限、满包和同帧接触；保持官方概率与数值。

## 14：唐僧治疗队友

修改`TangsengEntity.cs`的现有甘露入口、`TangsengWaterCloneNode.cs`及关卡已有恢复飘字接收处。恢复量仍读施法者自身属性，时点仍由正式动画触发：本体自疗；队友相对施法点横/纵各≤200；分身横≤200、纵≤160。只治疗存活者。

验收范围内外、本体/分身、双方不同等级/无双状态及死者不复活。不得复制一套合作治疗公式或通过全局HeroModel恢复所有人。

## 15：死亡、全队出口与结果

**15A死亡。** `LevelViewController.cs`、英雄死亡/墓碑通知、`LevelSystem.cs`、`Models/Level/LevelResult.cs`和`SettlementFormData/Logic`。一死一活继续，死者只结束本人控制/技能/效果/在途球；全灭且相应演出结束后结算一次。共享统计和个人结果各归其数据；不添加碰墓碑复活玩法。

**15B离关。** `LevelExitNode.cs`、`LevelViewController.Entrance.cs`、隐藏入口节点、`ProcedureLevel.cs`。建议任一存活者用自己的交互键确认，等待本关有效球结算后整队退出；隐藏入口整队只淡出/导航一次。死者不阻塞；返回标题保存完整双方，放弃/失败按完整进关快照回滚。

验收一死一活、同帧全灭、Boss死亡与玩家死亡同帧、重复出口、隐藏入口、重试和加载取消；“建议”项在执行该步时和界面流程一起确认，不提前写成已采用规则。

## 16：花果山合作闭环验收

从标题选择单/双人、依次选不同英雄，到真实进入花果山、同时移动/攻击/技能、各自成长/拾取/无双、治疗、一死一活、全灭/通关、保存退出再读入。交换P1/P2英雄完整复验；单人跑同一路径。

用现有FrontendSmokeScenario/UiSmokeScenario及纯规则测试承载证据；每次记录场景、输入和断言。提高生命、直接攻击包清场只能证明流程/边界，不能替代正常数值下合作实战。此步通过才称“首关双人可玩”。

## 17：双方菜单与装备赠送

**17A菜单。** `InventoryFormData/Logic`、`SkillForm`及其页面、`ProcedureMap/Level`的打开请求。正式开放各自快捷键、地图切换所观察玩家；学习/升级/穿卸/出售均用各自资产，保存按钮始终保存整档。复用06的数据绑定，不再新建Player2窗口。

**17B赠送。** `EquipmentCommands.cs`、`EquipmentSystem.cs`和既有背包装备操作菜单组件。增加明确的发送者/接收者/实例请求。仅赠送背包物，穿戴物先卸下；验证参战资格、源归属和目标容量后同步移交同一实例，不await、不自动装备、不重抽属性，不新建交易系统。

验收满包、已穿戴、过期选择、快速出售与赠送、双方界面刷新；同一InstanceId只存在一处；保存重读及放弃回滚包含赠送。目标英雄暂不能穿戴不阻止其正常接收装备。

## 18：其余关卡与最终收口

按现有关卡顺序分组审查，避免“全项目统一处理”的大批次：

1. 桃花源、水帘洞、蝙蝠窟：平台/坡地、机关、隐藏入口与屏幕边界。
2. 七星洞、迦楼罗之穴：毒雾、延迟/范围效果、飞行目标和双人命中。
3. 浅岸、东海、牛魔谷：抓取、水牢、解救、牛魔多形态与致死归属传递。
4. 隐藏龙宫：冻结、龙王按参战者最低等级分档，共享旗幡资格和双方装备排重。

逐组先列确切Boss/机关/效果文件，仅修改有单人假设的调用点，复用已有AI多目标和CombatSide友伤过滤。最后核验60/120Hz、加载第二人取消、退出、重复进入、单/双人切换、窗口/实体退订、源表/场景引用和文档。完成后再把目标行为写入规范现状，清理迁移中的旧入口和重复实现。

## 本轮交付与下一步

用户已要求按本计划逐阶段实施，并在每阶段后暂停审查。当前仅01完成；存档仍v12，档案、战斗、输入和界面尚未迁移。

当前暂停等待01审查。用户确认后再进入02A，不自动接着迁移整个档案与战斗层。
