# 70 档案与存档

## 数据归属

- ProfileModel 保存英雄身份、累计经验、灵魂、装备与穿戴、最远通关关卡；SkillModel 保存技能学习记录。HeroModel 保存本局属性、生命魔法、无双与施放状态。
- SaveModel 保存当前文件编号、六格目录摘要和手动保存状态；SaveSystem 执行读写。初始化与取用规则见 [00](00-Architecture.md)。

## 文件内容

| 类型 | 持久字段 |
| --- | --- |
| GameData | UnitId、SaveVersion、ActiveHeroId、Soul、Heroes、EquipmentItems、HighestClearedLevelId |
| HeroSaveData（随 GameData） | HeroId、TotalExperience、LearnedSkills、武器/防具/饰品 InstanceId |
| EquipmentSaveData（随 GameData） | InstanceId、EquipmentId、生成时已确定的 Attributes |
| GameCatalogue | UnitId、SlotNumber、Name、SavedAtUtc |

等级、本级经验与最终属性由经验曲线和配置重新计算。随机装备保存已抽出的实际属性；存档集合按稳定键排序。完整字段契约见 `Save/Data/GameData.cs`。

当前格式 v9：HighestClearedLevelId 为必需字段，0 表示尚未通关；关卡编号按连续顺序解锁，最新可挑战关卡取最远通关编号加一并限制在已配置范围。通关先推进进度再保存，死亡、放弃与重玩旧关不推进。装备暴击、闪避、命中和抗暴使用 0..1 的直接概率，保存饰品穿戴引用。通用饰品可由不同英雄使用，同一实例仍只能属于一个背包或穿戴部位；卸装和其他部位交换不得清除饰品。旧开发档按已授权的一次性转存规则处理，不在正式代码中增加兼容分支。

## 读写路径

- 读取：SaveSystem → ArchiveUtility → GF.Archive → GameData → SaveDataUtility.Validate → ProcedureLoadGame 注册档案模型和系统。
- 保存：SaveDataUtility.Capture(profile, skills) → GameData → SaveSystem → ArchiveUtility → GF.Archive。
- 格式以 GameData.CurrentVersion 为准，仅接受当前格式及完整字段。结构变化时递增版本；旧开发档的一次性转存按用户授权执行。
- 框架首次建立、尚未认领的当前格式空槽按建档表创建角色并立即写回。已有格按指定 UnitId 读取；读取失败保留原文件并报告。
- GGF 负责原子写、备份回退和读写串行；写入调用时捕获序列化内容。调用方检查 Task<bool>，切换作用域前等待 PendingWrite。

## 检查点与关卡事务

| 时点 | 行为 |
| --- | --- |
| 新建档案 | 立即保存 |
| 进关 | LevelSystem 捕获完整档案快照 |
| 关内成长、拾取、出售、学习 | 即时修改运行时档案 |
| 通关或死亡 | 首个结局提交收益并写检查点；仅通关推进关卡进度 |
| 放弃、运行失败 | ProfileSystem.RestoreCheckpoint 原地恢复进关快照 |
| 地图手动保存 | 捕获当前档案，写盘后更新本次窗口的保存状态 |

SaveDataUtility 负责校验与转换；ProfileSystem 负责恢复 Model。窗口、实体和 DTO 分别保留各自生命周期。交易/锻造等后续玩法按自身检查点接入 SaveSystem。

## 六格目录与异步请求

- SaveSlotInfo 是只读摘要，SlotNumber 为 1～6，UnitId 为框架文件编号。标题读取目录后，流程将所选格号与编号作为 SaveSelectionVariable 传给读档流程。
- 已有格具备有效格号和检查点 UTC 时间；角色名称来自配置。不可读槽禁用并保留文件；重复格号、超过六档或目录损坏时报告错误。
- 提交检查点前核对档案身份、当前目录和数据编号。目录读取、读档与手动保存使用所属流程的取消令牌；迟到结果只作用于原周期。
- 音量、语言、键位等应用偏好使用 GF.Setting。
