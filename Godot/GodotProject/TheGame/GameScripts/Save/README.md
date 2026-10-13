# 存档代码怎么读

想确认“游戏到底保存什么”，先看 [Data/GameData.cs](Data/GameData.cs)，全部写盘字段和配套英雄记录都在这一个文件里。当前实际进度只有下列字段：

| 字段 | 保存内容 |
| --- | --- |
| `ActiveHeroId` | 当前出战英雄的配置 ID |
| `Heroes[].HeroId` | 已拥有英雄的配置 ID |
| `Heroes[].TotalExperience` | 每个英雄的累计经验 |
| `Soul` | 钱包灵魂 |
| `EquipmentItems[].InstanceId / EquipmentId` | 装备实例唯一编号和定义编号，背包与穿戴共用，每件只保存一次 |
| `EquipmentItems[].Attributes` | 16 个具名整数属性，读档不重抽 |
| `Heroes[].WeaponInstanceId / ArmorInstanceId` | 当前穿戴实例编号，空 Guid 表示空槽 |

等级、本级经验和战斗属性从累计经验与配置重新计算。当前生命、魔法、无双、位置、怪物与波次不写入档案；未拾取的地面物品不写盘；技能和关卡解锁尚未接入。

## 三个业务入口

唯一入口是 [SaveModule.cs](SaveModule.cs)，目录刷新部分在同一类型的 [SaveModule.Directory.cs](SaveModule.Directory.cs)，顺着一次操作阅读即可：

1. 打开六格窗口：`RefreshDirectoryAsync` → 私有读取与目录状态提交 → GGF 读取文件 → `ProfileSaveHelper.RestoreProfile` → `SaveSlotInfo` 显示摘要。
2. 选择一个格：`LoadOrCreateAsync` → 读取已有文件或按配置创建 → `SaveLoadResult` 返回运行时档案及 `Loaded / Created` 来源。
3. 保存游戏：`SaveCheckpointAsync` → `ProfileSaveHelper.CaptureSaveData` 捕获快照 → 私有 `WriteSnapshotAsync` → `GF.Archive.OverWriteAsync` 写入。

`PendingWrite` 是最近一次写入任务，离开流程或再次读档前等待它。`m_ActiveSlotUnitId` 防止旧档案误写入后来选择的另一个格。

## 文件与显示对象的区别

| 代码 | 职责 | 是否写盘 |
| --- | --- | --- |
| `Data/GameData.cs` | 每格文件根对象：`UnitId`、`SaveVersion`、`ActiveHeroId`、`Heroes`、`Soul`、`EquipmentItems` | 是 |
| `Data/GameData.cs` 中的 `HeroSaveData` | 英雄列表的一条记录：配置 ID、累计经验与武器/防具实例引用 | 是 |
| 同文件的 `EquipmentSaveData` 与 `Equipment/EquipmentAttributes` | 装备实例及其实际属性，字段显式必需 | 是 |
| `Data/GameCatalogue.cs` | 目录条目：`UnitId`、`SlotNumber`、`Name`、`SavedAtUtc` | 是 |
| `Data/ProfileSaveHelper.cs` | 运行时档案 ↔ 保存快照；也用于关卡放弃时恢复检查点 | 否 |
| `SaveLoadResult.cs` | 一次读档操作的返回结果及加载来源 | 否 |
| `Slots/SaveSlotInfo.cs` | 六格窗口所需的名称、等级、时间与可读状态 | 否 |
| `Slots/SaveSlotDirectory.cs` | 六格窗口的加载阶段、错误和变更通知 | 否 |

`GameData`、`GameCatalogue` 的名字和全局命名空间沿用现有 GGF 绑定。目录存于 `user://GameData/Catalogue.sav`，每格存于 `user://GameData/Data/{UnitId}.sav`；`.bak` 是备份，由框架负责原子写和恢复。只读取当前 v6 格式，不做历史兼容；旧档保留为不可读格，不自动覆盖。

运行时 `PlayerProfile.Heroes` 直接按 ID 持有 `HeroProgression`（英雄 ID + 累计经验与派生进度），没有额外的 `HeroRecord` 层。`GameContext` 把它与本槽 `SaveModule` 一起传给流程；`Level/LevelModule` 只负责本关奖励、提交与回滚；通关由清场后靠近出口按 W 触发，写盘完成后返回地图。完整阅读路径见 [玩法架构](../../../../../Docs/ProjectTour.md)。

## 新增一个持久字段

先给运行时 `PlayerProfile` 或其所属状态添加真实玩法数据；再修改`GameData` 或配套英雄记录，并在 `ProfileSaveHelper.CaptureSaveData / RestoreProfile` 两侧补齐转换。关卡内会改变的数据还要处理 `RestoreCheckpoint`，并验证保存、读取与放弃回滚。格式有变化时递增 `GameData.CurrentVersion`。

音乐与音量属于应用设置，通过 GGF Sound/Setting 保存，不属于上述角色存档。集中规范见 [第 70 章](../../../../../Docs/ProjectGuidelines/70-ProfileAndSave.md)。

所有检查点实际写盘成功后，由 SaveModule 发出 SaveSucceededEventArgs，应用级 SaveNoticeForm 统一显示提示。关内怪物奖励通过池化飞行球接触英雄后入账；W 退出若仍有球在飞行，先完成实际拾取再保存。

`PlayerProfile.Inventory` 只拥有未穿戴的已拾取装备，`HeroEquipment` 拥有每名英雄穿戴的两件装备；`EquipmentModule` 负责按配置生成和校验实例，窗口只显示。满包时装备留在地面，直到原定的 10 秒过期，不重抽属性；桃子、蓝瓶接触后立即恢复，不占背包。通关与死亡保存已拾取装备，中途放弃恢复进关时的背包内容。

点击背包格打开装备／赠送／出售菜单，单人赠送禁用；异角色装备不能穿戴，但可出售。售价只来自 EquipmentConfig.SellPrice，粗糙 10、普通 20 灵魂；重复或过期选择不会重复入账。穿戴、卸下、出售均通过 LevelModule → EquipmentModule 提交，窗口不直接改档案。C 通过 inventory_toggle 输入动作打开/关闭背包。
