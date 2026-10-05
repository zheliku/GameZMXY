# 60 玩法模块

> 状态：登记当前可证实实现、首版关卡契约和待设计模块。首版关卡已实现入口加载、空间校验、阶段触发/清除/生成；出口交互和结算仍按本契约扩展。
> 证据：`Configs/GameConfig/Datas/`、`Godot/GodotProject/TheGame/GameScripts/`、`TheGame/MainPack/Scripts/Procedure/ProcedureGame.cs`、`TheGame/Scenes/Level_1.tscn`、`Docs/M5ArchitectureAudit.md`、`Docs/LegacyAssetMap.md` 及所列旧项目脚本。

## 设计参考

- 场景组合、`PackedScene` 复用、`Marker2D` 空间锚点和 `Area2D` 触发器采用 Godot 官方 2D Platformer 示例及官方场景/节点文档中的常见模式；本项目只借鉴边界，不复制第三方代码。
- 旧项目 `ZMXY_BHYH` 只用于核对 Level_1 的四段布局和刷怪节奏；旧脚本的全局状态、并行数组和继承树不属于新实现约定。

## 当前实现

- **角色控制**：悟空实体、输入缓冲、地面/空中/普攻/受击/死亡身体状态，配置驱动移动参数和攻击招式。
- **怪物行为**：花果山猴子实体；巡逻、追击、攻击、目标丢失、受控和死亡 AI；身体层执行移动、攻击、收招、受击与死亡。
- **战斗表现**：命中结算、伤害事件和池化伤害飘字。当前验证场地供角色与猴子战斗测试使用。
- **数据**：现有 Luban 表覆盖实体、UI 表单、英雄与等级配置、怪物、攻击、战斗和音效。英雄等级表已生成，但没有运行时消费者。
- **运行入口**：`ProcedureGame` 加载 `Scenes/Level_1.tscn`，从 `LevelController` 读取玩家出生点，并按关卡表首阶段配方生成实体；实体由 `GF.Entity` 管理。
- **关卡首版**：`Level_1.tscn` 固定包含 `LevelController`、`World/Geometry`、`World/SpawnPoints`、`World/StageTriggers`、`World/Exit` 和 `RuntimeActors` 插槽。`LevelSpawnPoint` 保存场景唯一 `SpawnPointId`，`LevelStageTrigger` 保存唯一 `TriggerId`。
- **场景与数据边界**：场景是地形、碰撞、相机、出生点和触发位置的唯一来源。`LevelConfig` 采用 Luban `list` 模式，一个关卡按阶段占多行，首阶段行保存关卡级元数据，其余行保存阶段元数据；`LevelStageConfig` 每行保存一条怪物配方，使用可读的 `MonsterEntityId` 枚举名，通过 `LevelId + StageOrder` 和 `Sequence` 支持同阶段多种怪物和多批次生成。坐标只在场景，表通过 `SpawnPointId` 外键关联。
- **阶段生命周期**：首阶段在进入关卡后启动；后续阶段由 `TriggerId` 触发，并在前一阶段完成生成且注册实体清零后启动。活跃怪物以 `IEntity.Id` 注册，`MonsterDiedEventArgs` 只按值移除计数；`MaxActive` 限制并存数量。清除判定不读取实体组或场景树子节点数量。
- **存档**：`GameCatalogue` 与 `GameData` 只有占位字段；当前流程没有存档消费者。

## 旧项目玩法参考

- **成长经验**：旧角色经验与升级见 `ZMXY_BHYH/Script/Base/BaseRoleProperies.gd`、`BaseMonster.gd`。
- **装备与法宝**：旧装备属性和外观见 `BaseRoleProperies.gd`、`BaseHero.gd`；法宝行为见 `Script/Base/BaseMagicWeapon.gd` 与 `Script/MagicWeapon/`。
- **技能与 Buff**：旧技能见 `Script/Skill/`、`BaseHero.gd`；Buff 见 `Script/Buff/` 与 `Script/Base/BaseBuff.gd`。
- **关卡与阶段**：旧关卡、刷怪与出口行为见 `Script/Base/BaseThroughLevel.gd`、`BaseStage.gd` 和 `Scene/Level/`。
- **背包与商店**：旧物品、交易及锻造玩法见 `Script/BackPack/`、`Script/Shop/`、`Script/LDL/`。
- **存档数据**：旧玩家数据见 `Script/MemoryClass/PlayerData.gd`。旧数据格式和脚本不是新项目实现约定。

## 待设计

- 角色经验、等级成长与升级结算。
- 装备、法宝、技能和 Buff。
- 关卡门/阻挡、出口交互、奖励与通关结算；它们必须复用上述 `TriggerId`、阶段清除和实体注册契约。
- 背包、物品掉落、商店、锻造与分解。
- 存档字段、版本迁移及其接入流程。
- 主菜单、选人、战斗 HUD 和其他玩法界面。
