# 60 玩法模块

> 状态：登记当前实现、长期系统的落位与数据形状，以及旧项目参考。落位表是新玩法的入口约定：新系统按表放目录、选所有者、定数据形状，不新建平行架构。
> 证据：`Configs/GameConfig/Datas/`、`Godot/GodotProject/TheGame/GameScripts/`、`TheGame/MainPack/Scripts/Procedure/`、`TheGame/Scenes/level_1.tscn`、`Docs/LegacyAssetMap.md` 及所列旧项目脚本。

## 当前实现

功能现状、初始化位置和代码入口统一见 [项目导览](../ProjectTour.md)。GGF + QF 分工见 [00](00-Architecture.md)，存档与关卡事务见 [70](70-ProfileAndSave.md)。

## 新玩法落位

| 功能 | 持久状态 | 本局状态 / 表现 | 规则入口 |
| --- | --- | --- | --- |
| 技能 | SkillModel 的学习记录 | HeroModel 的登记与施放状态；具体 GGF 技能状态、独立效果节点 | SkillSystem |
| 装备与背包 | ProfileModel 的装备实例、穿戴与灵魂 | Entity 外观、背包窗口 | EquipmentSystem、ProfileSystem |
| 被动、丹药、阵法 | 所属档案模型的学习 / 使用记录 | HeroModel 的属性来源 | 对应 System，经 HeroSystem 登记属性修正 |
| Buff | 按玩法决定是否持久 | 所属角色的持续时间、层数和控制状态 | 对应 System；具体状态执行动作 |
| 法宝、宠物、子弹 | 所属档案模型的实例与出战记录 | 独立实体，记录拥有者实体 ID | 对应 System 与具体实体状态 |
| 商店、锻造、炼丹 | ProfileModel 的资产与结果 | 所属窗口局部选择 | 对应 System 完成交易后提交检查点 |
| 掉落 | 领取后写 ProfileModel | LevelViewController 拥有地面物品与飞行球 | LootSystem 抽取，LevelSystem 发放 |
| 任务、成就、关卡进度 | 所属档案模型的进度与领取记录 | LevelModel 统计、冻结的 LevelResult | 对应 System 处理业务事实 |
| 多英雄 | ProfileModel 的英雄成长与穿戴、SkillModel 的技能书 | 当前出战 HeroModel，各角色自己的输入与状态机 | ProfileSystem、HeroSystem、SkillSystem |

- 数值和平衡参数进配置，动作编排进原生场景/动画，招式规则进具体状态。
- 属性修正使用 AttributeType、AttributeModifierKind 和 AttributeSourceId，由 HeroSystem 按来源登记与移除。
- 新持久字段同时更新 Model、GameData、SaveDataUtility 和映射测试，按存档契约更新版本。
- 新流程加入 GGF 的 Procedure.Procedures 清单前，按第 50 章登记授权范围。

## 旧项目玩法参考

- **英雄与属性**：5 名英雄，存档绑定 1 名；16 项属性；最终值 = 成长 + 装备 + 被动 + 丹药 + 阵法（全加法），Buff 只在结算时作为倍率/标志。等级上限 55，升级补满。见 `ZMXY_BHYH/Script/Base/BaseRoleProperies.gd`、`BaseHero.gd`。
- **装备与法宝**：7 个槽位（武器/防具/饰品/时装/头衔/翅膀/法宝），10 级品质，强化、宝石镶嵌、幻化；法宝独立实战槽、耗蓝冷却、生成子弹。见 `BaseRoleProperies.gd`、`Script/Base/BaseMagicWeapon.gd`、`Script/MagicWeapon/`。
- **技能与 Buff**：每英雄 9 主动 + 1 专属被动、5 个键位；6 个共享被动；Buff 26 种（控制/持续伤害/属性/保护）。见 `Script/Skill/`、`Script/Buff/`、`Script/Base/BaseBuff.gd`。
- **背包与经济**：3 类 × 10 页 × 35 格；商店、神秘商店；炼丹炉（强化/合成/镶嵌/分解/打造）；丹药 6 类 × 5 级。见 `Script/BackPack/`、`Script/Shop/`、`Script/LDL/`、`Script/Pellet/`。
- **关卡**：55 关、5 张地图、线性解锁与境界门槛、Boss、出口、评价（时间 + 剩余血量）、失败界面（回地图/重试）。见 `Script/Base/BaseThroughLevel.gd`、`BaseStage.gd`、`Scene/Level/`。
- **存档**：多槽，每次操作即写盘，只有版本号无迁移。见 `Script/MemoryClass/PlayerData.gd`。旧数据格式和脚本不是新项目实现约定。

## 相机与特殊触发器依据

- 设计参考：[Itay Keren《Scroll Back》](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers) 的相机窗口分类、[Cinemachine Position Composer](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html) 的中央 Dead Zone、[Godot Camera2D](https://docs.godotengine.org/en/stable/classes/class_camera2d.html) 的中心/限位语义。借鉴空间约束与窗口模型，不引入第三方插件。
- 本地旧项目 `Script/Level/camera.gd` 每帧更新 `limit_*`，`BaseThroughLevel.gd` 以角色位置阈值和清怪数量推进；它不以实际相机抵达启动，不能称为该新需求的实现证据。新方案以用户当前相机抵达要求为准。
- 保留区域触发能力有具体依据：旧 `Level_17.gd` 的 `_on_hddy_body_entered` 是带道具条件的隐藏入口，`Level_23.gd` 的 `_on_tp_body_entered/_exited` 是停留计时传送，另有陷阱和机关。当前不移植这些玩法，只保留可选监听接口；Level_1 的普通波次不需要 Area2D。
- 已实际读取 4399 官方第三代游戏入口及公开 SWF，只读核对 940×590 画布和 `ViewControllor` 的普通滚动阈值（前进约 626.67px、后退 188px）。本项目采用原版的卷屏/边界分离行为，中央 ±40px 则依用户后续构图要求选择；参考版本的窗口阈值不作为本项目中央死区的数值来源。
