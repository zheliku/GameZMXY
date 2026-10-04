# 60 玩法模块

> 状态：只登记当前可证实的实现、旧项目玩法参考和待设计模块；待设计项不定义数据结构或流程。  
> 证据：`Configs/GameConfig/Datas/`、`Godot/GodotProject/TheGame/GameScripts/`、`TheGame/MainPack/Scripts/Procedure/ProcedureGame.cs`、`TheGame/Scenes/DebugArena.tscn`、`Docs/M5ArchitectureAudit.md`、`Docs/LegacyAssetMap.md` 及所列旧项目脚本。

## 当前实现

- **角色控制**：悟空实体、输入缓冲、地面/空中/普攻/受击/死亡身体状态，配置驱动移动参数和攻击招式。
- **怪物行为**：花果山猴子实体；巡逻、追击、攻击、目标丢失、受控和死亡 AI；身体层执行移动、攻击、收招、受击与死亡。
- **战斗表现**：命中结算、伤害事件和池化伤害飘字。当前验证场地供角色与猴子战斗测试使用。
- **数据**：现有 Luban 表覆盖实体、UI 表单、英雄与等级配置、怪物、攻击、战斗和音效。英雄等级表已生成，但没有运行时消费者。
- **运行入口**：`ProcedureGame` 加载 `Scenes/DebugArena.tscn` 并生成悟空和猴子；不是选人、关卡推进或通关流程。
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
- 关卡场景、阶段/波次、生成点、出口与通关结算。
- 背包、物品掉落、商店、锻造与分解。
- 存档字段、版本迁移及其接入流程。
- 主菜单、选人、战斗 HUD 和其他玩法界面。
