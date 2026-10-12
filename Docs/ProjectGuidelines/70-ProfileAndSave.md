# 70 档案与存档

> 适用：玩家持久状态（档案）、当前存档格式、写盘时机与关卡事务。
> 代码：`TheGame/GameScripts/Profile/`、`Save/`、`Level/`、`GameContext.cs`；框架 `Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs`。

## 档案（Profile）

- `PlayerProfile` 是一个存档槽内全部持久状态的**唯一运行时所有者**：英雄成长（按 HeroId 索引的 `HeroProgression`）、钱包（`Wallet`）；以后的背包、装备栏、技能书、关卡进度、任务都作为它的成员加入。
- 档案由 `GameContext` 持有，从 `ProcedureLoadProfile` 读档开始存在，到回到标题为止；经流程状态机数据传给后续流程，不做成单例。
- `ProcedureLoadProfile` 根据实际 `SaveOrigin` 分流：Created 进入选角，Loaded 直接进入地图。选角当前只确认已实现的悟空，为静态窗口，不持有档案打开参数；档案保留在流程状态机中。
- 档案对象纯 C#：不引用节点、`GF.UI`/`GF.Entity`；状态只经方法修改（`AddExperience`、`AddGold`、`TrySpendGold`……），一次修改发一次 `Changed`。
- 累计经验是成长的唯一事实：等级、本级经验、上限由共享经验曲线派生，不单独保存。
- 实体不持有持久状态。英雄显示时由 `HeroLoadout.Create(heroId, level, growth)` 构建不可变的 `HeroLoadout`（等级、成长属性、持久修正）作为 `userData`；升级或换装后由关卡运行重新构建并调用 `HeroEntity.ApplyLoadout`。实体不反向写档案。

## 存档格式

- 框架数据类型 `GameData`（全局命名空间）：`SaveVersion`、`ActiveHeroId`、`Heroes`、`Gold`，另继承框架 `UnitId`。按用户要求删除 `ProfileSaveData`，进度直接平铺；配套 `HeroSaveData` 与它集中在 `Save/Data/GameData.cs`。转换仍由 `Save/Data/ProfileSaveHelper.cs` 负责。
- 当前实际进度为出战英雄 ID、拥有英雄的 ID 与累计经验、金币；等级和战斗属性读档时重算，生命/魔法/无双、位置与波次不保存。背包、装备、技能、关卡解锁尚未接入存档。字段与三个读写入口见 [存档阅读说明](../../Godot/GodotProject/TheGame/GameScripts/Save/README.md)。
- 存档 DTO 只含 ID、数量与可变状态，不含配置派生值（属性、上限）与运行时引用。运行时档案 ↔ DTO 的映射只在 `ProfileSaveHelper` 一处；新增持久状态时同时改 DTO 与映射，并补映射往返单测。
- `GameData.SaveVersion` 新建时取当前版本；版本与三个进度字段均用 Newtonsoft `Required.Always` 要求显式存在，Heroes 不允许 null。缺字段不能被初始化值掩盖。
- 列表按稳定键排序写出（如按 `HeroId`），同一状态得到同一份 JSON。

## 只支持当前格式

- 用户明确要求不保留存档兼容。当前格式为 `GameData.CurrentVersion = 3`；格式改变时递增版本并清理开发旧档，不新增迁移器、旧 DTO、历史字段或按旧等级补经验的规则。
- 已有档案由 `ProfileSaveHelper.RestoreProfile(GameData, curve)` 校验版本与内容；版本不一致、缺档案或无有效格号/检查点时间时拒绝读取，不自动转换或覆盖。
- GGF 首次初始化的空槽仍是当前格式。只有编号一致、v3、ActiveHeroId/Gold 为 0、Heroes 为空、SlotNumber/SavedAtUtc 为 0 且 Name 为空时，才视作尚未认领的新空槽，固定显示在第一格；这不是历史目录映射。
- 选择空格时按 `TbProfileConfig` 新建并立即写回；已有档案只读取，直到业务检查点才写盘。

## 写盘入口与可靠性

- 项目侧唯一入口：`SaveModule`（`RefreshDirectoryAsync`、`LoadOrCreateAsync(slotNumber, unitId)`、`SaveCheckpointAsync`、`PendingWrite`）。业务代码不直接调用 `GF.Archive`、`EasySave` 或文件 API；调试/烟测注入损坏文件除外。
- 框架侧 `ArchiveSystem` 保证（2026-10 加固，登记见 [50-FrameworkAndTools.md](50-FrameworkAndTools.md)）：
  - 原子写：临时文件写完整后替换目标，旧内容保留为 `.bak`；写失败目标保持原样。
  - 串行：所有读写按调用顺序执行；写入在调用时刻序列化，之后修改 `CurrentData` 不影响已排队的写入。
  - 读回退：主文件不存在或损坏时读 `.bak`；两者都不可用才失败，且不新建空档覆盖。
  - 覆盖已有槽位时同步重写目录 `Catalogue.sav`。
  - 所有操作返回 `bool`；调用方检查结果并记录，失败不改变内存档案。
- 读档失败（文件存在但不可读、版本无法识别）停在加载界面并报告，绝不新建空档覆盖玩家数据。

## 写盘时机：检查点

只在检查点写盘，不随每次数值变化写盘：

| 检查点 | 触发者 |
| --- | --- |
| 新建档案 | `SaveModule.LoadOrCreateAsync` |
| 关卡结局：通关、英雄死亡 | `LevelModule` |
| 返回地图/标题、交易/锻造完成、手动保存 | 地图保存按钮已实现；返回标题等待在途检查点，交易/锻造待实现 |

流程切换前等待 `SaveModule.PendingWrite`，保证下一作用域读到最近一次检查点。

## 关卡事务

- `LevelModule` 进关时在内存捕获档案快照（`ProfileSaveHelper.CaptureSaveData`）；关内收益（经验、金币、掉落）直接写入档案，即时升级、即时显示。
- **提交**：通关或英雄死亡（先到者生效，只提交一次）→ 写检查点。死亡保留本关收益。
- **回滚**：中途放弃（左下角设置 →“返回地图”或“返回主菜单”）、运行错误 → `ProfileSaveHelper.RestoreCheckpoint` 恢复进关快照，不写盘。
- **关停**：框架关停时只退订、不写盘；磁盘上仍是上一个检查点，等价于回滚。
- 结局确定后 `LevelModule` 不再接收奖励；本关击败数与经验统计只用于日志，保持私有，不写入存档。

## 设置

- 音量、语言、按键等与存档槽无关的设置用 `GF.Setting`，不进 `GameData`。

## 六格选择与目录摘要

- 标题流程只持有 `SaveModule`，调用 `RefreshDirectoryAsync`；窗口通过 `SaveFormData` 订阅模块拥有的 `SaveSlotDirectory` 完整状态。目录对象不读盘、不成为第二个入口。选择之前只读目录摘要，不建立 `GameContext` 或英雄；框架首次初始化的空文件仍显示为空格。
- `SaveSlotInfo` 是不可变显示信息；`SlotNumber` 固定为 1～6，`UnitId` 是 GGF 文件编号。流程只接受当前已打开存档窗口中的可读格，把二者作为一个池化 `SaveSelectionVariable` 交给读档流程。读档明确按编号进行，不以“最近存档”替代玩家选择。
- `GameCatalogue.SlotNumber` 与 `SavedAtUtc` 分别记录固定格号和最近检查点 UTC Unix 秒数，`Name` 记录角色名。已有档案必须具备有效元数据；不按创建时间补格号或保存时间。GGF 的文件布局、加密和备份契约保持原实现。
- 目录只通过 `SaveModule` 与 `GF.Archive` 获取，窗口不接触文件。不可读槽保留并禁用；无效或重复格号、超过六档、目录主备份均失败时报告错误，不删除或覆盖数据。
- `SaveModule` 记住所属 UnitId，检查点提交前核对 GGF 当前目录和数据编号，拒绝把旧档案写入其他槽。地图手动保存捕获当前档案快照，通过具名事件反馈结果；离开菜单后的旧结果不会改写复用窗口。
