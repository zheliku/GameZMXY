# 存档代码怎么读

先看 [Data/GameData.cs](Data/GameData.cs)，全部写盘字段和配套记录集中在这一个文件中。

| 字段 | 保存内容 |
| --- | --- |
| ActiveHeroId | 出战英雄 ID |
| Heroes[].HeroId / TotalExperience | 拥有的英雄与累计经验 |
| Heroes[].LearnedSkills | 已学习主动技能 ID；未学习不记录 |
| Soul | 灵魂余额 |
| EquipmentItems[].InstanceId / EquipmentId / Attributes | 装备唯一编号、定义与实际随机属性，每件只保存一次 |
| Heroes[].WeaponInstanceId / ArmorInstanceId | 穿戴引用，空 Guid 表示空槽 |

当前格式为 v7，没有历史兼容。等级、本级经验、属性上限从配置与累计经验重算；当前 HP/MP/WS、位置、波次、未拾取掉落不保存，关卡解锁尚未接入。

## 三个入口

唯一文件业务入口是 [SaveSystem](SaveSystem.cs)，框架读写经 ArchiveUtility。

1. `RefreshDirectoryAsync`：读目录与 DTO → SaveDataUtility.Validate → 生成六格摘要 → 更新 SaveModel.Directory。目录浏览不创建运行时玩家 Model。
2. `LoadOrCreateAsync`：读已有文件或按配置建档并立即写回 → 返回 `(GameData Data, SaveOrigin Origin)`。ProcedureLoadGame 创建并注册 ProfileModel、SkillModel 和对应 System；Created 进入选角，Loaded 进入地图。
3. `SaveCheckpointAsync`：核对捕获的 ProfileModel 仍是当前注册对象 → SaveDataUtility.Capture 捕获独立快照 → 核对文件编号 → ArchiveUtility.OverWriteAsync。

PendingWrite 是最近一次写入任务。流程离开或重新读档前等待它；框架负责原子写、备份与串行排队，不随每次数值变化写盘。所有检查点写入成功后发 SaveSucceededEventArgs，由 SaveNoticeForm 显示提示；地图调用 SaveManuallyAsync；等待/结果绑定 SaveModel.ManualSave，系统拒绝重复保存并过滤离开窗口后的旧结果。

## 谁拥有数据

| 对象 | 内容与生命周期 |
| --- | --- |
| ProfileModel | 英雄身份、累计经验、穿戴、背包和灵魂；读档 → 回标题 |
| SkillModel | 各英雄已学技能、当前施放与秒制无敌时限；读档 → 回标题，只有学习记录写盘 |
| HeroModel | 本局最终属性、生命、魔法、无双；进关 → 离关，不写盘 |
| SaveModel | 当前文件 ID、目录与手动保存显示状态；应用期 |
| LevelModel | 进关快照、阶段、统计和结局；本局 |
| SaveDataUtility | 无状态校验与捕获；同步调用期间借用 ProfileModel 与 SkillModel，不保存引用，不改模型、不读写文件 |
| ProfileSystem | 奖励、领取与回滚业务；统一修改 ProfileModel |

所有 Model 由流程 FSM 持有，System 与 UI 经 GetModel 借用。Model 保存数据和提供查询；System 使用 Value、标准集合操作和 ReplaceAll 修改，不经同名 Model 业务方法转发。绑定只传当前值，整数优先级默认 0，数值越大越先执行，同优先级按注册顺序。HeroSystem 的成长、穿戴同步使用 100，UI 使用默认值。没有手动补发通知 API，也没有第二套事件系统。

捕获：`SaveDataUtility.Capture(ProfileModel, SkillModel)`。工具只接收本次输入，不反向查询 Architecture。回滚：`ProfileSystem.RestoreCheckpoint(GameData)`。共享经验曲线单独注册为 Utility，ConfigurationModel 只持有配置数据与索引。持久档案与本局 HeroModel 生命周期不同；存档 DTO 保持 GameData 的扁平结构，没有 SaveLoadResult 或兼容层。

## 检查点与回滚

进关时捕获 GameData。关内经验、装备和灵魂经业务 System 即时更新 ProfileModel；通关或死亡提交检查点，死亡保留收益；中途返回地图/标题或运行失败时 RestoreCheckpoint 原地恢复，保留绑定对象与集合身份；学习记录同时回滚。

回滚先校验完整快照，再使用正常属性和集合入口依次恢复穿戴、成长、背包、余额。每个状态源修改时自动通知；优先级保证对应派生系统先于同源 UI 刷新，不承诺多个状态源之间的整体通知。Items 绑定提供实时只读视图；只有显式存档/回滚快照才复制集合。

新增持久状态：修改所属 Model → 修改 GameData 及配套记录 → 补齐 SaveDataUtility 的校验/捕获与 ProfileSystem 的回滚 → 验证往返和放弃事务。格式变化递增 CurrentVersion，不添加旧 DTO 或运行时迁移器。

GameData / GameCatalogue 的类名和全局命名空间由 GGF 绑定。正式目录为 `user://GameData/Catalogue.sav`，每格为 `user://GameData/Data/{UnitId}.sav`，`.bak` 为框架备份。烟测使用隔离 Validation 路径，不改正式档。

装备满包时留在地面直到过期；桃子和蓝瓶不占背包，接触后恢复并显示数字，蓝量灵魂仅回蓝。穿戴、卸下、出售经 LevelSystem → EquipmentSystem，售价来自 EquipmentConfig。音乐音量属于 GF.Setting，另行保存。

所有者与调用链见 [项目导览](../../../../../Docs/ProjectTour.md)。集中规范见 [第 70 章](../../../../../Docs/ProjectGuidelines/70-ProfileAndSave.md)，其中旧模型与版本描述已由用户后续指令替代，规范正文未自行更改。
