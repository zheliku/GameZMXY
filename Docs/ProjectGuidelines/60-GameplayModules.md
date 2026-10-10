# 60 玩法模块

> 状态：登记当前实现、长期系统的落位与数据形状，以及旧项目参考。落位表是新玩法的入口约定：新系统按表放目录、选所有者、定数据形状，不新建平行架构。
> 证据：`Configs/GameConfig/Datas/`、`Godot/GodotProject/TheGame/GameScripts/`、`TheGame/MainPack/Scripts/Procedure/`、`TheGame/Scenes/Level_1.tscn`、`Docs/LegacyAssetMap.md` 及所列旧项目脚本。

## 当前实现

- **启动流程**：`Launch → Update → Preload（资源组、本地化、节点池、配置校验）→ LoadProfile（读档/迁移/建档，创建 GameContext）→ Level`。调试直达关卡由烟测驱动使用隔离存档目录。
- **关卡运行**：`ProcedureLevel` 只负责装配与拆除：加载关卡场景 → 按档案构建出战装配并显示英雄 → 关闭加载界面 → 开始关卡会话 → 创建 `LevelRun` → 打开 HUD；离开时按创建逆序清理。结局后按 `BattleConfig.DeathRestartDelay` / `ClearRestartDelay` 重开本关（以后由结算界面与地图流程接管）。
- **收益与关卡事务**：`LevelSpawner` 只报告本关怪物被击败（`MonsterDefeat` 值）；`LevelRun` 查 `MonsterConfig.AddExp` 写入档案，升级时重建装配注入英雄并补满。通关与死亡提交检查点，中途离开回滚；规则见 [70-ProfileAndSave.md](70-ProfileAndSave.md)。
- **角色控制**：悟空实体、输入缓冲、地面/空中/普攻/受击/死亡身体状态，配置驱动移动参数和攻击招式。
- **怪物行为**：花果山猴子与妖猴（旧 Monster_2）实体；巡逻、追击、攻击、目标丢失、受控和死亡 AI；身体层执行移动、攻击、收招、受击与死亡。妖猴的旧 `RunHit`（追击途中也能出手）由"优先招 + 普攻"两条 AttackConfig 表达，不新增 AI 状态。
- **属性与成长**：英雄属性 = 成长表当前等级行（`TbHeroGrowthConfig`）+ 持久修正（本轮为空）；怪物属性 = `MonsterConfig.Stats`。数值与重构前公式逐级一致（单测锁定）。
- **战斗表现**：命中结算、伤害事件和池化伤害飘字（`DamagePopPresenter`，由关卡流程/测试场地持有）。`TestArena.tscn` 测试场地供角色与新怪物战斗测试。
- **战斗 HUD**：`UIs/BattleHud.tscn`（`UIFormId.BattleHud`），打开参数 `BattleHudData`（英雄、档案成长、关卡）；订阅 `Vitals/Musou/HeroProgression.Changed` 与 `TravelAvailableChanged`，变化时拉取一致快照刷新被动 `ResourceBar`；关闭时对称退订。技能槽、增益和护体待对应玩法接入。
- **阶段编排**：唯一当前阶段使用 `Ready → Travelling → Fighting → Travelling/Completed`，停止后为 `Stopped`。普通关卡每段都由实际相机抵达启动；清波不会直接生成下一波。特殊阶段可选 `Trigger`，只启用当前触发区并过滤本会话玩家。
- **空间与相机**：门集合预计算 `[前门, 当前右门]` 区域。行进时前门开放、右门关闭；相机到达右界后封前门、锁定战斗。清波后开右门，相机以连续移动进入下一段。清波后到下一场开战之间，关卡置 `TravelAvailable`，HUD 显示前进提示（Go）。取景细节见 [40-ScenesAndAssets.md](40-ScenesAndAssets.md)。
- **配方与实体**：`LevelConfig` 以 `LevelId` 为主键，`*Stages / *Recipes` 是嵌套多行列表；同段各配方并行。`LevelSpawnSchedule` 按游戏时间调度、轮转分配并存名额；`LevelSpawner` 按死亡事件归还名额，配方全部发完且存活/在途清空才报告清波。
- **所有权与退出**：本关所有运行实体 ID（含死亡动画中的实体）由刷怪服务持有；停止时取消会话、退订、按 ID 隐藏。异步显示返回后使用捕获的令牌和服务清理旧结果；失败回到流程统一结束（运行回滚）。
- **场景首版**：`Level_1.tscn` 保留六个生成点、三个阶段门和出口；`TestArena.tscn` 不进入游戏流程，F6 运行（详见 [40-ScenesAndAssets.md](40-ScenesAndAssets.md)）。
- **编辑器与验证**：`validate_level_scene.gd` 检查编辑器绑定；`--smoketest=level` 驱动真实移动与四段清波并核对档案经验与通关检查点；`--smoketest=ui` 覆盖 HUD、升级注入、关卡事务与存档。

## 长期系统落位

新系统按下表落位。"档案"列是持久状态（写入存档），"运行时"列是局内状态（随实体或关卡运行销毁）。

| 系统 | 配置 | 档案（`Profile/`） | 运行时 | 规则与表现 |
| --- | --- | --- | --- | --- |
| 技能 | `SkillConfig`：耗蓝、冷却、学习条件、招式 ID、效果 | `SkillBook`：已学技能与等级、键位（5 槽） | `SkillSet`（实体持有）：冷却、可用判定 | 释放驱动身体状态机的技能状态；子弹为 `BulletEntity` |
| 被动 | `PassiveConfig`：`StatModifier` 列表 | 已学被动 | — | `HeroStatBuilder` 换算为持久修正 |
| Buff | `BuffConfig`：时长、跳频、叠加规则、修正、控制标签、跳伤 | — | `BuffSet`（实体持有）：实例、层数、剩余时间；写 `StatSheet` 局内来源 | 来源：攻击包、技能、装备；HUD 在 `OnUpdate` 读剩余时间 |
| 背包 | `ItemConfig`：类别、叠加上限、价格、品质 | `Inventory`：可叠加物 `itemId + count`；唯一实例 `instanceId + 强化/宝石/五行` | — | 拾取、使用、丢弃为 `Inventory` 方法 |
| 装备 | `EquipmentConfig`：槽位、`StatModifier`、外观资源路径 | `Equipment`：槽位 → 实例 ID | — | `HeroStatBuilder` 按实例换算持久修正（一件一个 `StatSource`）；换装后重建装配 |
| 法宝 | `MagicWeaponConfig`：耗蓝、冷却、子弹 | 法宝实例与等级（背包实例）、出战槽 | 法宝实体（独立实体，记录拥有者 ID） | 属性加成经持久修正；攻击为子弹 |
| 丹药/阵法 | 对应表：`StatModifier` 列表 | 已服用/已激活记录 | — | `HeroStatBuilder` 换算持久修正 |
| 商店/锻造/炼丹 | 商品、配方、成功率表 | 对 `Inventory + Wallet` 的原子事务 | — | 成功后写检查点 |
| 掉落 | `DropConfig`：掉落组、权重、数量 | 拾取后进 `Inventory`/`Wallet` | 掉落实体（`LevelRun` 生成并拥有） | `LootRoller` 纯函数（随机值由调用方传入）；收益随关卡事务提交 |
| 任务/成就 | `QuestConfig`：目标类型枚举、参数、奖励 | `QuestLog`：进度、领取状态 | — | 订阅领域事件推进，不轮询 |
| 关卡进度 | `LevelConfig` 增解锁条件、评价规则 | `WorldProgress`：解锁、最佳评价 | `RunStats`：用时、剩余血量 | 结算界面读 `RunStats`；通关时更新 `WorldProgress` |
| 多英雄 | `HeroConfig` + 成长表行 | `Heroes` 字典已支持；`ActiveHeroId` | — | 选人界面修改出战英雄后写检查点 |

约定：

- 所有属性加成都表达为 `StatModifier`（`StatType` + `StatModifierKind` + 数值），按来源整体登记和移除；不在结算公式里写特例。
- 持久加成（装备、法宝、被动、丹药、阵法）只经 `HeroStatBuilder` 进入实体；局内加成（Buff）只经实体运行时进入。
- 档案新增成员时同步：`PlayerProfile` 成员、`ProfileSaveData` 字段、`ProfileMapper` 映射与回滚、存档版本号与迁移、单测。
- 新流程（菜单、选档、地图）按需加入 `Framework/GameFramework.tscn` 的流程清单，每次单独授权并登记。

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
- 已实际读取 4399 官方第三代游戏入口及公开 SWF，只读核对 940×590 画布和 `ViewControllor` 的普通滚动阈值（前进约 626.67px、后退 188px）。本项目采用原版的卷屏/边界分离行为，中央 ±40px 则依用户后续构图要求选择。版本哈希和证据见 [原版相机核查](../Reviews/zmxy3_camera_reference_2026-10-06.md)。
