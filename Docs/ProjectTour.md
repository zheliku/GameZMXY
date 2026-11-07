# 项目导览

唯一规范是 [ProjectGuidelines/AGENTS.md](ProjectGuidelines/AGENTS.md)。本页维护当前实现与后续路线；官方规则和项目差异见 [OfficialReference](OfficialReference.md)，素材来源见 [LegacyAssetMap](LegacyAssetMap.md)，架构取舍见 [ArchitectureReview](ArchitectureReview.md)，可执行命令和实际结果见 [Validation](Validation.md)。

## 当前内容与完成边界

2026-11-07按当前源码、源表和官方造一v25928重新核对：**悟空、唐僧均可单人游玩，十个主线关卡及隐藏龙宫已接入；尚未完整复刻官方全部单人内容。** 唐僧“只有配置”、背包播放战斗待机、存档v8/v10等旧说明已清理。

| 范围 | 当前实现 | 证据入口与边界 |
| --- | --- | --- |
| 两位英雄 | 悟空五段普攻、唐僧弹体普攻/蓄力，各五主动、五共享被动；换装、受击、无双、墓碑、重试 | HeroConfig两行、SkillConfig十招、两位具体Entity；Validation有实际购买/施放、15件各角色装备穿卸及磁盘重读 |
| 主线 | 花果山、水帘洞、桃花源、蝙蝠窟、七星洞、迦楼罗之穴、浅岸、东海、鲨之涡流、牛魔谷 | LevelConfig 1..10及正式场景；逐关解锁、刷怪/门/出口、牛魔三形态、结算和存档 |
| 隐藏关 | 水帘洞下水进入龙宫，龙王入场等级分支、旗幡首次资格、退出 | LevelConfig 11、DragonPalaceRules、LevelViewController.Entrance；不推进主线 |
| 成长与装备 | 55级成长、灵魂/经验、38种装备、四槽穿戴、随机属性、自然恢复、出售及批量出售 | ProfileModel、EquipmentSystem、HeroSystem、Items；官方44种独立定义中另六种的情况见下文 |
| 法宝 | 东海旗幡和金钢琢的获取、排重、穿戴、属性、存档 | 本版源码的zbfb作为装备加入curarray；未发现该版独立主动施放链，不再列虚构的“法宝主动技能缺项” |
| 菜单与存档 | 两英雄选角、六格本地档案、删除确认、设置/音量、地图购技、地点图鉴、保存提示 | Procedure及各Form；地图和关内返回主菜单均提交当前档案，成功才导航 |
| 战斗表现 | HUD、连击、无双立绘、状态效果、伤害/治疗飘字、独立墓碑、各角色专用背包展示 | BattleForm、InventoryForm、角色资源；技能范围用基础形状近似，不是Flash逐像素碰撞 |
| 掉落 | 红色回血、蓝色回蓝、黄色无双、紫色灵魂球，桃子/蓝瓶与装备接触领取 | 复用LootSystem、LootOrbNode、LevelSystem；恢复量在生成时按英雄上限确定，接触才入账 |

关卡流程验证包含提高生命、直接攻击包清场的夹具；角色技能命中有独立专项。现有十关证据不能证明正常数值下用两角色技能逐个打败所有Boss，不能替代自然游玩的难度验收。测试覆盖和退出诊断按Validation解释。

项目保留了用户指定的差异：55级上限、扩展属性/伤害规则、中央±40px相机窗口、部分地形及点位、造二/造三风格的选角/背包与结算界面、玄冰阵删弧光和无双脚底锚定等。全量官方复刻与当前设计之间的差异不自动视为Bug，也不在清理中擅自还原。

## 造一待实现清单与后续顺序

核对对象是相同散列的主程序及实际加载的Role_v7、Monster包、OtherMat_v9。主程序和资源包含重名类时，以实际主程序调用链为准；不能只查较旧的资源包嵌入脚本。原始身份、方法和数值集中在[官方完整性核对](OfficialReference.md#单人与双人内容完整性核对2026-11-07)。

| 优先级 | 工作 | 当前缺口与实施位置 | 完成条件 |
| --- | --- | --- | --- |
| P1 | 正常数值单人验收 | 两角色主线流程和技能专项都有证据，但缺完整自然战斗通关与节奏评估 | 新档正常成长/学习/掉落，覆盖各Boss招式、死亡重试、隐藏关、最终通关/读档；记录难度问题和已接受差异，不以清场夹具代替 |
| P2 | 片头及最终结尾 | GMain首次选角进入Opening；Monster24结束创建export.scene.Ending。项目当前直接进地图/通用结算 | 复用现有Procedure和原生动画/窗口，支持跳过、重入和暂停，不重复发奖励或提前切图 |
| P2 | 战功与离线兑换 | 官方牛心每日首杀+50战功，Config/MemoryClass保存日期与次数；BackPack.btn_dj→sdList→PackThings.dhClick可离线兑换三件活动装备。项目没有战功/兑换 | 先决定是否纳入当前离线版本；若纳入，数据归ProfileModel/DTO，规则归Profile/Equipment系统，明确本地日界与重复领取规则，并更新存档契约 |
| P2 | 六种未登记装备的取舍 | dszk、zljs仅见定义及显示白名单，当前包未找到实际获得入口；hjsl来源依赖活动；tndblg/tndbthl/zcld有战功兑换链 | 区分已定义、可获得、需外部服务；确认获取和美术后再补表，不能为凑44种伪造Boss掉落 |
| P2 | 退出诊断定位 | 长流程仍有ObjectDB/资源/RID诊断，编辑器另有ComponentInsoector/free问题 | 以最小复现区分自有资源与依赖，修自有生命周期；Framework/addons仍受只读边界约束，不通过吞错判通过 |
| P3 | 原平台与周年活动 | 4399账号、云存档、助威、兑换码/2025活动HTTP服务 | 单列可选平台适配，不把本地六槽伪装为官方在线服务；不作为单人主线可玩的前提 |

不列作造一必补内容：八戒/沙僧、宠物、炼丹、强化、宝石和额外主线4-2/4-3。GameMenu.shoping/sroom在该版明确“暂未开放”；装备描述不能证明已存在套装被动。结算评分尚无该版可达评分公式依据，项目目前只展示真实时间、剩余生命、击败数和经验，不增造评级算法或排行榜。

用户当前选择先推进下节双人扩展，正常数值下的单人/双人实战验收在阶段交付中保留。片头/结尾和战功另行排期；清理后的回血球补漏已完成，以上剩余玩法仍为待办。

## 双人扩展方案

双人实现回退到`858f22a`后，现仅按计划完成01：新增PlayerSlot枚举与规则测试工程编译引用。当前仍是单人ProfileModel、HeroModel、一套输入与HUD，存档v12。此前一次性推进的双人实现已按用户要求撤回，不能把其测试结果或数据结构当作当前功能。

已确认一格保存双方、个人资产独立、共享进度、参战英雄不可重复，且继续支持新增英雄；玩家身份已定义`PlayerSlot.P1/P2`枚举，业务调用方尚未迁移。保留唯一根Architecture和现有业务系统。

详细文件清单、接口、依赖、分批审查与验收统一见[双人扩展实施计划](CoopImplementationPlan.md)。01已完成并暂停待审查；后续每次仅执行用户选定的一批，审阅后再推进。官方证据见[OfficialReference](OfficialReference.md#双人扩展的源码依据2026-11-07)，当前验证见[Validation](Validation.md#双人实现撤回与重新规划2026-11-07)。

## 启动与返回

`Launch → Update → Prepare → Preload → Title（开始/六格存档）→ 空槽Character确认英雄 → LoadGame → Map → Level`；已有槽由Title直接LoadGame→Map。选角先确定英雄，再创建首次快照，不能先写悟空再改唐僧。

Prepare注册资源组、共享Utility及应用Model/System；LoadGame校验DTO后注册档案作用域；Level注册本局模型、初始化数值，再显示英雄/场景/HUD。存档入口是SaveSystem→ArchiveUtility→GF.Archive，不在窗口或实体写文件。

返回主菜单从设置请求交给所属Procedure：地图/关内都先SaveManuallyAsync提交当前状态，成功才离开，失败留在原页。通关/死亡保存收益；放弃关卡和运行失败按检查点回滚。所有具体读写与生命周期契约见[第70章](ProjectGuidelines/70-ProfileAndSave.md)。

## 所有者与初始化位置

同一根GGF流程FSM持有六个顶层Model，System借用；Entity不创建第二份业务模型。

| Model | 数据 | 生命周期 |
| --- | --- | --- |
| ConfigurationModel | Tables、已验证配置与共享身体参数 | Prepare→应用结束 |
| SaveModel | 文件身份、目录、手动保存反馈 | Prepare→应用结束 |
| ProfileModel | 出战身份、各英雄成长/穿戴、背包、灵魂、进度/旗幡资格 | LoadGame→标题 |
| SkillModel | 主动学习和被动等级 | LoadGame→标题 |
| HeroModel | 本局属性、HP/MP/WS、当前技能、恢复节拍及StatusEffects普通子数据 | Level→离关 |
| LevelModel | 阶段、时间、统计、结局、连击和完整进关快照 | Level→离关 |

| System | 负责的规则 |
| --- | --- |
| ProfileSystem / EquipmentSystem | 收益、唯一归属、回滚；装备随机、穿脱/交换、出售 |
| HeroSystem / HeroStatusSystem | 成长/装备/被动属性、生命魔法/无双；冻结/中毒的效果生命周期 |
| SkillSystem | 学习/扣费、当前角色的技能状态登记与激活；不自建另一套行为生命周期 |
| LevelSystem | 阶段、奖励、统计、结局与关卡事务 |
| SaveSystem | 目录、读档/建档/删除、检查点和手动保存；过滤旧作用域结果 |
| ConfigurationSystem | OnInit取已注册模型/工具执行配置校验 |
| Combat / Input / EnemyAi / Loot | 共享伤害、输入采样、AI时钟/决策规则和纯掉落选择；运行实例状态仍归实体/模型 |

档案注册顺序：ProfileModel/SkillModel→ProfileSystem→EquipmentSystem/SkillSystem。本局：LevelModel/HeroModel→HeroStatusSystem→HeroSystem→LevelSystem。离关先关窗退订、停场景协作、移除Level系统/模型、隐藏英雄、移除英雄系统/模型，再卸载场景；回标题再释放档案。实际顺序以Procedure代码为准。

独立F6场地`TheGame/Scenes/test_arena.tscn`自带GGF，按相同分工装配内存档案，不读写正式存档；烟测由默认静默的SmokeTestDriver通过命令行启用。

## Model 的属性绑定

BindableProperty/BindableList/BindableDictionary持有唯一状态；UI读同一数据并在OnOpen/OnClose订退。关联字段以完整值提交，System同步优先级100，UI默认0；集合使用标准操作/ReplaceAll，不手动补发通知。没有另一套事务或事件容器。

| 通信 | 用途 |
| --- | --- |
| Model绑定 | 血量、经验、穿戴、技能书、目录、结局等持续状态 |
| 局部具名回调 | 窗口导航、场景协作；窗口编号拒绝池复用的迟到请求 |
| GGF Event | 伤害、敌人死亡、保存成功和唯一装备奖励等跨职责事实 |
| Command / Query | 玩家业务修改交给System；组合读取返回当前结果，不承担持续绑定 |

GF.Event在框架下一轮分发，池化参数只在回调期间有效，跨await先复制必要值。不可取消的迟到实例由原请求服务回收；退出先取消本周期、关窗退订，再移除依赖。

## 战斗、技能与敌人 AI 阅读入口

英雄：`AbstractHeroEntity → InputSystem/HeroActionData → 共享身体状态/具体技能状态`。具体WukongEntity/TangsengEntity匹配输入、登记自己的状态及技能；SkillSystem校验学习与消耗，原生AnimationPlayer负责帧、判定几何和声音时点。方法轨道的AnimEvent只连接当前具体状态；普通业务调用使用无后缀方法。回调登记阶段/结束请求，在Advance返回后执行，不在轨道遍历中回池。

AttackData冻结本次属性、威力和AttackSource来源；CombatSystem计算DamageResult与命中无双收益，不把减伤中间值当作第二份公开状态。身体命中与独立SkillEffectNode复用HurtBoxNode.Intersects及攻击去重。特效由创建者登记、结束或隐藏时归还已有NodePool。

二跳是否结束读取原生播放器的complete标记/动作长度，不另外维护时间副本。独立效果自行推进，英雄状态控制招式分支；冰龙蓄力、水幻影、火魔斩等待落地均保留在具体状态。

### 敌人 AI 阅读入口

`AbstractEnemyEntity`拥有共同生命、感知、攻击和双FSM；`AbstractMonsterEntity`提供小怪共性，再分Ground/Sky/Sea；Boss从Enemy独立派生。共享AI状态为PatrolState、EngageState、FlyingPursuitState、DistanceSkillEngageState；各物种保留有实际差异的选招、捕获、形态、无敌和回收。

AI读IEnemyAiAgent事实并写意图，身体状态消费意图与攻击请求。感知/交战/巡逻由场景Perception的原生矩形、圆或线段提供，伤害盒只管实际命中，不再合并动画形状推导AI距离。公共时钟与冷却归EnemyAiSystem，实体独占EnemyAiData；死亡销毁AI，受控暂停决策。静止石像/牛心不建空AI。

EnemyDeathState共用普通尸体与等待死亡效果的终态；龙王死亡延迟、牛魔形态转换及牛心奖励时点保留专用状态。首领普通闪烁/旋涡由本角色原生动作控制，不把鹏魔蛋态误判为最终死亡。

## 配置、存档和场景数据

| 源工作簿 | 业务Sheet |
| --- | --- |
| Hero.xlsx | HeroConfig、HeroGrowthConfig、HeroLevelConfig |
| Enemy.xlsx | MonsterConfig、BossConfig，合并生成TbEnemyConfig |
| Skills.xlsx / Combat.xlsx | SkillConfig、PassiveConfig / BattleConfig、AttackConfig |
| Items.xlsx | EquipmentConfig、EquipmentDropPoolConfig、PickupConfig |
| Resources.xlsx | EntityConfig、UIFormConfig、SoundConfig |
| LevelConfig.xlsx / ProfileConfig.xlsx | 关卡嵌套阶段/配方 / 建档规则 |
| __tables__ / __beans__ / __enums__ | 输入映射、结构与稳定身份 |

十一工作簿、十七业务Sheet；数据、编号及原ZIP保真约定见[第20章](ProjectGuidelines/20-Configs.md)。EntityId与EnemyId同号：英雄1..9999、小怪10000..99999、Boss100000..199999；LegacyId独立记录官方编号。ConfigIdUtility是已注册IUtility，调用方GetUtility取用，不自行new或散写编码。

存档当前**v12**，唯一字段契约是`Models/Save/GameData.cs`：保存英雄成长、主动/被动学习、四槽实例引用、装备实际属性、灵魂、主线进度、旗幡资格；不保存临时HP/MP/WS、位置、波次和地面掉落。SaveDataUtility捕获/校验，ProfileSystem恢复，SaveSystem执行读写；不保留旧格式运行时兼容。六格本地档案与应用音量设置分开。

## 界面与资源编辑位置

| 内容 | 正式编辑入口 |
| --- | --- |
| 菜单/地图/设置/存档 | UIs下各snake_case场景；GameScripts/UI内Ge生成绑定与.Logic业务分开 |
| 地图技能/保存/设置 | map_form：技能静态普通态/单次展开/可购黄光；保存官方hover换图；设置20号海报字/提亮。按实际可见轮廓对齐，常态上下间隔64px |
| 地点图鉴 | MapLocationButton→LevelGuideQuery→共用LevelGuideTooltip；读取配置/掉落，不创建怪物实体 |
| 角色战斗与换装 | Entitys/Heroes及Entitys/Animations/Heroes/Wukong、Tangseng；身体/武器分层，保留父矩阵及源遮挡 |
| 背包角色展示 | 两英雄独立官方preview资源；悟空62帧，唐僧外层1帧，装备selector不是待机动作 |
| 无双 | hud/musou_activation/{wukong,tangseng,shared}及MusouActivationNode；满值19帧提示另用battle_menu_ws_glow_frames.tres，无裁剪shader |
| 血条和结算 | UIs/Battle/monster_health_bar、encounter_health_bar及settlement_form；位置/表现留原生场景 |
| 地面物品/飘字 | GroundItemNode、LootOrbNode、FloatingTextPresenter；一个固定拾取盒，领取后0.8秒图标上移；实际恢复后显示数字 |
| 关卡空间 | Scenes/Levels/zm1_*；World的Geometry、Platforms、SpawnPoints、StageGates、可选Triggers及Exit；Camera消费实际限位 |

资源作者化和重导边界见LegacyAssetMap及第40章。保留可重跑的官方导出/审计/注册工具；不要批量运行历史repair脚本覆盖人工场景。生成代码、资源常量与bytes只经已有生成器更新。

## 按顺序阅读

1. ProcedurePrepare、ProcedureTitle/Character/LoadGame、ProcedureMap/Level：装配、保存、导航和清理。
2. Arch/ArchitectureContracts、FsmArchitectureExtensions、Bindable与Models：唯一状态与生命周期。
3. Systems/Profile、Equipment、Hero、Skill、Combat及Command/Query：业务与数据流。
4. AbstractHeroEntity、两角色具名状态、AbstractEnemyEntity、AI/Body：动作、输入与命中。
5. LevelViewController及Camera/Spawning/Stages/Exit、各Form：节点协作与可编辑位置。
6. Validation：按受影响范围运行既有规则测试、真实引擎专项和资源检查；未执行的范围不宣称通过。
