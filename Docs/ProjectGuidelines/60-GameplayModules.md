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
- **运行入口**：`ProcedureGame` 读取关卡配置、加载场景、显式初始化、显示玩家、等待加载界面实际关闭，然后 `StartSession` 注入玩家和取消令牌。初始化期不订阅相机抵达或触发器玩法事件，不生成敌人。
- **阶段编排**：唯一当前阶段使用 `Ready → Travelling → Fighting → Travelling/Completed`，停止后为 `Stopped`。普通关卡每段都由实际相机抵达启动；清波不会直接生成下一波。特殊阶段可选 `Trigger`，只启用当前触发区并过滤本会话玩家。
- **空间与相机**：门集合预计算 `[前门, 当前右门]` 区域。行进时前门开放、右门关闭；相机到达右界后封前门、锁定战斗。清波后开右门，相机以连续移动进入下一段；节点中心与显示中心一致，不在限位之外累计位置。取景细节见 [40-ScenesAndAssets.md](40-ScenesAndAssets.md)。
- **配方与实体**：`LevelConfig` 以 `LevelId` 为主键，`*Stages / *Recipes` 是嵌套多行列表；同段各配方并行。`LevelSpawnSchedule` 按游戏时间调度、轮转分配并存名额；`LevelSpawner` 只执行当前计划，按死亡事件归还名额，配方全部发完且存活/在途清空才报告清波。没有轮询定时器，也不靠场景树数量判定清波。
- **所有权与退出**：本关所有运行实体 ID（含死亡动画中的实体）由刷怪服务持有；停止时取消会话、退订、按 ID 隐藏。异步显示返回后使用捕获的令牌和服务清理旧结果；失败回到流程统一结束，不能只记日志留下锁死关卡。
- **场景首版**：`Level_1.tscn` 保留用户暂存的地面、背景位置与三形状斜坡；保留六个生成点、三个阶段门和出口。移除四个普通刷怪触发区，保留空的可选触发区集合插槽；首段 a/b 出生点移到相机锁屏时的可见区域。
- **编辑器与验证**：统一 Tool Resource 列表及父集合标注；`validate_level_scene.gd` 检查编辑器绑定，可用 `--snapshot` 输出实际圆圈/死区绘制图。`--smoketest=level` 驱动真实移动、跨坡跳跃、右墙停步、第二段左墙和四段清波，验证相机连续性与生成总数。
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
- 关卡门/阻挡、出口交互、奖励与通关结算；它们必须复用当前阶段完成事件与实体所有权契约；区域交互按实际需要添加。
- 背包、物品掉落、商店、锻造与分解。
- 存档字段、版本迁移及其接入流程。
- 主菜单、选人、战斗 HUD 和其他玩法界面。

## 相机与特殊触发器依据

- 设计参考：[Itay Keren《Scroll Back》](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers) 的相机窗口分类、[Cinemachine Position Composer](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html) 的中央 Dead Zone、[Godot Camera2D](https://docs.godotengine.org/en/stable/classes/class_camera2d.html) 的中心/限位语义。借鉴空间约束与窗口模型，不引入第三方插件。
- 本地旧项目 `Script/Level/camera.gd` 每帧更新 `limit_*`，`BaseThroughLevel.gd` 以角色位置阈值和清怪数量推进；它不以实际相机抵达启动，不能称为该新需求的实现证据。新方案以用户当前相机抵达要求为准。
- 保留区域触发能力有具体依据：旧 `Level_17.gd` 的 `_on_hddy_body_entered` 是带道具条件的隐藏入口，`Level_23.gd` 的 `_on_tp_body_entered/_exited` 是停留计时传送，另有陷阱和机关。当前不移植这些玩法，只保留可选监听接口；Level_1 的普通波次不需要 Area2D。
- 已实际读取 4399 官方第三代游戏入口及公开 SWF，只读核对 940×590 画布和 `ViewControllor` 的普通滚动阈值（前进约 626.67px、后退 188px）。本项目采用原版的卷屏/边界分离行为，中央 ±40px 则依用户后续构图要求选择；不把原版非对称窗口、Cinemachine 屏幕 Hard Limits 与阶段物理门混为同一概念。版本哈希和证据见 [原版相机核查](../Reviews/zmxy3_camera_reference_2026-10-06.md)。
