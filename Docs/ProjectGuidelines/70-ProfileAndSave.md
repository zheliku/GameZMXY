# 70 档案与存档

> 适用：玩家持久状态（档案）、存档格式、版本迁移、写盘时机与关卡事务。
> 代码：`TheGame/GameScripts/Profile/`、`Save/`、`Session/`；框架 `Framework/GodotGameFrameworkCore/Archive/ArchiveSystem.cs`。

## 档案（Profile）

- `PlayerProfile` 是一个存档槽内全部持久状态的**唯一运行时所有者**：英雄记录（`HeroRecord`：`HeroId` + `HeroProgression`）、钱包（`Wallet`）；以后的背包、装备栏、技能书、关卡进度、任务都作为它的成员加入。
- 档案由 `GameContext` 持有，从 `ProcedureLoadProfile` 读档开始存在，到回到标题为止；经流程状态机数据传给后续流程，不做成单例。
- 档案对象纯 C#：不引用节点、`GF.UI`/`GF.Entity`；状态只经方法修改（`AddExperience`、`AddGold`、`TrySpendGold`……），一次修改发一次 `Changed`。
- 累计经验是成长的唯一事实：等级、本级经验、上限由共享经验曲线派生，不单独保存。
- 实体不持有持久状态。英雄显示时由 `HeroStatBuilder.Build(HeroRecord)` 构建不可变的 `HeroLoadout`（等级、成长属性、持久修正）作为 `userData`；升级或换装后由关卡运行重新构建并调用 `HeroEntity.ApplyLoadout`。实体不反向写档案。

## 存档格式

- 框架数据类型 `GameData`（全局命名空间）：`SaveVersion`、`Profile`（`ProfileSaveData`）、`Player`（仅读取 v0 旧格式）。
- 存档 DTO 只含 ID、数量与可变状态，不含配置派生值（属性、上限）与运行时引用。运行时档案 ↔ DTO 的映射只在 `ProfileMapper` 一处；新增持久状态时同时改 DTO 与映射，并补映射往返单测。
- `GameData` 字段一律不设初始值：Newtonsoft 先调用默认构造再填 JSON，带初始值会让缺字段的旧档被误判为新版本。
- 列表按稳定键排序写出（如按 `HeroId`），同一状态得到同一份 JSON。

## 版本迁移

- 当前版本由 `SaveMigrator.CurrentVersion` 定义（现为 1）。识别规则：
  - `SaveVersion == 0` 且 `Player != null`：重构前格式，迁移为当前版本（等级不倒退、累计经验不丢失）；
  - `SaveVersion == 0` 且两者皆空：框架首次启动建的空档，按 `TbProfileConfig` 建档；
  - `SaveVersion == CurrentVersion`：原样读取，`Profile` 缺失视为损坏；
  - 更高版本：拒绝读取，避免旧游戏降级覆盖。
- 迁移是纯函数（输入 `GameData` + 曲线 + 建档规则，输出当前版本 DTO）；迁移或新建的档案读档后立即写回。
- 格式变化时：`CurrentVersion` 加一，在 `SaveMigrator` 增加上一版本 → 当前版本的一步迁移，补单测（旧 JSON 字面量 → 期望 DTO）。不修改已发布版本的读取分支。

## 写盘入口与可靠性

- 项目侧唯一入口：`SaveService`（`LoadOrCreateAsync`、`CheckpointAsync`、`Pending`）。业务代码不直接调用 `GF.Archive`、`EasySave` 或文件 API；调试/烟测注入损坏文件除外。
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
| 读档后迁移/新建 | `SaveService.LoadOrCreateAsync` |
| 关卡结局：通关、英雄死亡 | `LevelRun` |
| 返回地图/标题、交易/锻造完成、手动保存 | 对应流程或事务所有者（待实现） |

流程切换前等待 `SaveService.Pending`，保证下一作用域读到最近一次检查点。

## 关卡事务

- `LevelRun` 进关时在内存捕获档案快照（`ProfileMapper.Capture`）；关内收益（经验、金币、掉落）直接写入档案，即时升级、即时显示。
- **提交**：通关或英雄死亡（先到者生效，只提交一次）→ 写检查点。死亡保留本关收益。
- **回滚**：中途放弃（以后的"返回地图"）、运行错误 → `ProfileMapper.RollBack` 恢复进关快照，不写盘。
- **关停**：框架关停时只退订、不写盘；磁盘上仍是上一个检查点，等价于回滚。
- 结局确定后 `LevelRun` 不再接收奖励；本关统计 `RunStats` 只用于展示，不写入存档。

## 设置

- 音量、语言、按键等与存档槽无关的设置用 `GF.Setting`，不进 `GameData`。
