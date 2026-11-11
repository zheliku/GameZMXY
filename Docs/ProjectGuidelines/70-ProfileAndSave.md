# 70 档案与存档

## 数据归属

- ProfileModel 保存有序阵容、各英雄累计经验、灵魂、背包与穿戴，以及共享的最远通关关卡和旗幡资格；SkillModel 按 HeroId 保存技能学习记录。当前按阵容装配一至两名英雄与各自HUD，个人档案和全员目录摘要已实现；HeroModel 按 HeroId 持有各自 HeroState，个人本局属性、生命魔法、无双与施放状态从 GetState(heroId) 读取。
- SaveModel 保存当前文件编号、六格目录摘要和手动保存状态；SaveSystem 执行读写。初始化与取用规则见 [00](00-Architecture.md)。

## 文件内容

| 类型 | 持久字段 |
| --- | --- |
| GameData | UnitId、SaveVersion、Heroes、EquipmentItems、HighestClearedLevelId、EastSeaBannerAvailable |
| HeroSaveData（随 GameData） | HeroId、TotalExperience、Soul、BagInstanceIds、LearnedSkills、PassiveLevels、武器/防具/饰品/法宝 InstanceId |
| EquipmentSaveData（随 GameData） | InstanceId、EquipmentId、生成时已确定的 Attributes |
| GameCatalogue | UnitId、SlotNumber、Name、SavedAtUtc |

等级、本级经验与最终属性由经验曲线和配置重新计算。随机装备保存已抽出的实际属性；英雄记录保持阵容顺序，技能和被动记录沿用既有的稳定键排序。`BagInstanceIds` 按实例编号稳定排序，仅表示归属；`EquipmentItems` 是全档实例池，保留各背包的原始格子顺序，恢复时按归属筛选该顺序。同一实例必须且只能被一个背包或穿戴部位引用，拒绝缺失、重复和孤立实例，并逐人校验余额非负及背包容量。完整字段契约见 `TheGame/GameScripts/Models/Save/GameData.cs`。

当前格式 v14：`GameData.Heroes` 与 `ProfileModel.HeroIds` 的顺序即玩家席位，下标 0 为 P1、下标 1 为 P2；阵容包含一至两名英雄，编号必须为正数且不重复，不另存人数或单一出战身份。

`HighestClearedLevelId` 为必需字段，0 表示尚未通关；关卡编号按连续顺序解锁，最新可挑战关卡取最远通关编号加一并限制在已配置范围。通关先推进进度再保存，死亡、放弃与重玩旧关不推进。装备暴击、闪避、命中和抗暴使用 0..1 的直接概率，保存饰品与法宝穿戴引用。通用饰品和法宝可由不同英雄使用，同一实例仍只能属于一个背包或穿戴部位；卸装和其他部位交换不得清除饰品。运行时不接受v13及更早格式；v12正式档通过下述D0离线工具转存，其余旧开发档需重建，不增加运行时兼容分支。

2026-11-11按实施计划E1-4同步D0～E1现状：个人资产、技能与本局数据均按HeroId，阵容顺序表示席位，双人实体及个人HUD已装配。赠送只把同一装备实例从本人背包移到队友背包，不重抽属性、不自动穿戴；满包或已穿戴不能赠送。存档格式保持v14。

## 读写路径

- 读取：SaveSystem → 注入的GF.Archive → GameData → SaveDataUtility.Validate → ProcedureLoadGame 注册档案模型和系统。
- 保存：SaveDataUtility.Capture(profile, skills) → GameData → SaveSystem → 注入的GF.Archive。
- `HeroOf(PlayerSlot)` 与 `SlotOf(int heroId)` 在阵容和席位之间换算；个人成长、灵魂、背包、穿戴与学习业务显式接收 HeroId，不提供全局当前玩家兼容入口。
- 格式以 GameData.CurrentVersion 为准，仅接受当前格式及完整字段。结构变化时递增版本；不增加运行时迁移分支；D0一次性离线工具是用户明确批准的正式v12例外。
- 框架首次建立、尚未认领的当前格式空槽按建档表创建角色并立即写回。已有格按指定 UnitId 读取；读取失败保留原文件并报告。
- SaveSystem构造时借用流程传入的GGF存档服务，删除纯转发ArchiveUtility；生产与独立场地均显式传同一服务，不另建注册或兼容层。
- GGF 负责原子写、备份回退和读写串行；写入调用时捕获序列化内容。调用方检查 Task<bool>，切换作用域前等待 PendingWrite。

## D0正式旧档转存与新档初始值

`Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py`仅用于已批准的v12正式档离线转存，不接入游戏运行时。先以`--capacity 75 --dry-run`核对主/备文件，用户确认后才执行写入；工具先完整备份GameData目录，按各主/备文件自身内容转换，Catalogue保持原字节，v13/v14跳过，未知版本或非法归属拒绝。转存把原单人钱包与背包归入该档原英雄，保留编号、属性、实例顺序与BOM，不重新抽取装备。

2026-11-10正式执行已获用户“确认”，8个主/备文件成功，备份位于`C:\Users\zheliku\AppData\Roaming\Godot\app_userdata\GameZMXY\GameData.backup-before-v14-20261110_191354`。完整dry-run/正式输出及核对见[Validation](../Validation.md#双人d0正式转存完成上手验证受阻2026-11-10)。该授权不允许后续脚本手改正式存档；上手只能使用第5、6格，不能覆盖第1～4格。

新建档案由SaveSystem按有序阵容为每人写入ProfileConfig.StartSoul，双方各得配置初值；ProfileModel构造仅建立零余额，生产读档随后由RestoreCheckpoint恢复DTO余额，不再保留构造soul参数。初始值唯一来自建档表，不在表现层或构造函数另设默认初始钱包。

## 检查点与关卡事务

| 时点 | 行为 |
| --- | --- |
| 新建档案 | 立即保存 |
| 进关 | LevelSystem 捕获完整档案快照 |
| 关内成长、拾取、出售、学习 | 即时修改运行时档案 |
| 通关或死亡 | 首个结局提交收益并写检查点；仅通关推进关卡进度 |
| 放弃、运行失败 | ProfileSystem.RestoreCheckpoint 原地恢复进关快照 |
| 地图手动保存 | 捕获当前档案，写盘后更新本次窗口的保存状态 |
| 有活跃档案时返回主菜单 | 地图与关内设置通过SaveSystem提交当前档案，写盘成功后才导航；失败保留档案和当前页面供重试，不能只等待上次PendingWrite |

SaveDataUtility 负责校验与转换；ProfileSystem 负责恢复 Model。回滚前必须确认快照的英雄编号有序序列与当前阵容完全一致，不能交换席位；全部英雄的余额、背包、穿戴和技能数据完整校验后才提交恢复。窗口、实体和 DTO 分别保留各自生命周期。交易/锻造等后续玩法按自身检查点接入 SaveSystem。

## 六格目录与异步请求

- SaveSlotInfo 是只读摘要，SlotNumber 为 1～6，UnitId 为框架文件编号；Heroes 是按席位排列的 SaveHeroSummary 只读集合，每项保存 Name 与 Level，构造时复制且不写入档案 DTO。标题读取目录后，流程将所选格号与编号作为 SaveSelectionVariable 传给读档流程。
- 已有格具备有效格号和检查点 UTC 时间；角色名称来自配置；目录按阵容顺序保存全员摘要，catalogue 名以单空格连接全员 NameCn。用户裁决存档页单/双人统一只显示角色名，如“悟空”或“悟空 · 唐僧”；摘要数据仍保留双方等级，标题不显示等级，日期样式、字号和场景布局不变。不可读槽禁用并保留文件；重复格号、超过六档或目录损坏时报告错误。
- 提交检查点前核对档案身份、当前目录和数据编号。目录读取、读档与手动保存使用所属流程的取消令牌；迟到结果只作用于原周期。
- 音量、语言、键位等应用偏好使用 GF.Setting。
