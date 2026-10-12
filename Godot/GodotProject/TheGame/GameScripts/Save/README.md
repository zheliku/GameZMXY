# 存档代码怎么读

想确认“游戏到底保存什么”，先看 [Data/GameData.cs](Data/GameData.cs)，全部写盘字段和配套英雄记录都在这一个文件里。当前实际进度只有下列字段：

| 字段 | 保存内容 |
| --- | --- |
| `ActiveHeroId` | 当前出战英雄的配置 ID |
| `Heroes[].HeroId` | 已拥有英雄的配置 ID |
| `Heroes[].TotalExperience` | 每个英雄的累计经验 |
| `Soul` | 钱包灵魂 |

等级、本级经验和战斗属性从累计经验与配置重新计算。当前生命、魔法、无双、位置、怪物与波次不写入档案；背包、装备、技能和关卡解锁尚未实现存档。

## 三个业务入口

唯一入口是 [SaveModule.cs](SaveModule.cs)，目录刷新部分在同一类型的 [SaveModule.Directory.cs](SaveModule.Directory.cs)，顺着一次操作阅读即可：

1. 打开六格窗口：`RefreshDirectoryAsync` → 私有读取与目录状态提交 → GGF 读取文件 → `ProfileSaveHelper.RestoreProfile` → `SaveSlotInfo` 显示摘要。
2. 选择一个格：`LoadOrCreateAsync` → 读取已有文件或按配置创建 → `SaveLoadResult` 返回运行时档案及 `Loaded / Created` 来源。
3. 保存游戏：`SaveCheckpointAsync` → `ProfileSaveHelper.CaptureSaveData` 捕获快照 → 私有 `WriteSnapshotAsync` → `GF.Archive.OverWriteAsync` 写入。

`PendingWrite` 是最近一次写入任务，离开流程或再次读档前等待它。`m_ActiveSlotUnitId` 防止旧档案误写入后来选择的另一个格。

## 文件与显示对象的区别

| 代码 | 职责 | 是否写盘 |
| --- | --- | --- |
| `Data/GameData.cs` | 每格文件根对象：`UnitId`、`SaveVersion`、`ActiveHeroId`、`Heroes`、`Soul` | 是 |
| `Data/GameData.cs` 中的 `HeroSaveData` | 英雄列表的一条记录：配置 ID 与累计经验 | 是 |
| `Data/GameCatalogue.cs` | 目录条目：`UnitId`、`SlotNumber`、`Name`、`SavedAtUtc` | 是 |
| `Data/ProfileSaveHelper.cs` | 运行时档案 ↔ 保存快照；也用于关卡放弃时恢复检查点 | 否 |
| `SaveLoadResult.cs` | 一次读档操作的返回结果及加载来源 | 否 |
| `Slots/SaveSlotInfo.cs` | 六格窗口所需的名称、等级、时间与可读状态 | 否 |
| `Slots/SaveSlotDirectory.cs` | 六格窗口的加载阶段、错误和变更通知 | 否 |

`GameData`、`GameCatalogue` 的名字和全局命名空间沿用现有 GGF 绑定。目录存于 `user://GameData/Catalogue.sav`，每格存于 `user://GameData/Data/{UnitId}.sav`；`.bak` 是备份，由框架负责原子写和恢复。只读取当前 v4 格式，不做历史兼容。

运行时 `PlayerProfile.Heroes` 直接按 ID 持有 `HeroProgression`（英雄 ID + 累计经验与派生进度），没有额外的 `HeroRecord` 层。`GameContext` 把它与本槽 `SaveModule` 一起传给流程；`Level/LevelModule` 只负责本关奖励、提交与回滚；通关由清场后靠近出口按 W 触发，写盘完成后返回地图。完整阅读路径见 [玩法架构](../../../../../Docs/GameplayArchitecture.md)。

## 新增一个持久字段

先给运行时 `PlayerProfile` 或其所属状态添加真实玩法数据；再修改`GameData` 或配套英雄记录，并在 `ProfileSaveHelper.CaptureSaveData / RestoreProfile` 两侧补齐转换。关卡内会改变的数据还要处理 `RestoreCheckpoint`，并验证保存、读取与放弃回滚。格式有变化时递增 `GameData.CurrentVersion`。

音乐与音量属于应用设置，通过 GGF Sound/Setting 保存，不属于上述角色存档。集中规范见 [第 70 章](../../../../../Docs/ProjectGuidelines/70-ProfileAndSave.md)。

所有检查点实际写盘成功后，由 SaveModule 发出 SaveSucceededEventArgs，应用级 SaveNoticeForm 统一显示提示。关内怪物奖励通过池化飞行球接触英雄后入账；W 退出若仍有球在飞行，先完成实际拾取再保存。
