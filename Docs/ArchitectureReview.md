# GGF + QF 架构取舍

本页合并原ArchitectureReview、ArchitectureResearch与ArchitectureReviewForGPT的有效结论和外部来源；当前代码/路线只维护在[ProjectTour](ProjectTour.md)，规则只维护在[集中规范](ProjectGuidelines/AGENTS.md)。2026-11-02审计条目及GLM裁决保留在[实施记录](ArchitectureAudit-20261102.md)，不把历史报告作为当前待办。

## 采用的分工

GGF拥有流程、实体、UI、资源、声音、事件、池与归档生命周期；本地Arch是借鉴QF的Model/System/Command/Query/Bindable契约及GGF FSM适配，并未引用完整QFramework包；Godot场景、节点、物理和原生动画拥有表现事实。无需再叠加MVC/MVVM容器、第二个Architecture、事件总线或技能调度器。

Model按数据所有权与生命周期聚合，System按真实业务规则划分，两者不要求一一对应。ProfileSystem和EquipmentSystem共享ProfileModel，HeroSystem与HeroStatusSystem共享HeroModel；StatusEffects等领域子数据是普通数据，不另外注册Model。全局仅同一根Architecture，应用/档案/本局是其中成员的不同生命周期；F6独立世界可有自己的FSM。

本项目选择Controller读/订阅，玩家业务修改经Command→负责不变量的System；实体运动/动画、局部窗口交互仍直接执行。简单读取不强制Query，组合筛选/汇总才使用Query。Query返回本次结果，不承担绑定或持续状态。工具通过IUtility注册后GetUtility取用；ConfigIdUtility、ExperienceCurveUtility不在System中重新构造，也没有静态ValidateTables旁路。

## 为什么保留这些选择

| 外部依据 | 可借鉴点 | 本项目的边界 |
| --- | --- | --- |
| [QFramework README及源码](https://github.com/liangxiegame/QFramework/blob/master/QFramework.cs) | 分层访问能力、Command/Query和Bindable | 官方Command可直接写Model并发Event，本项目收窄写入路径属于项目选择，不冒称QF强制 |
| [QF Godot版](https://github.com/liangxiegame/QFramework/tree/master/QFramework.Godot4.5%2B)的架构文档与counter_app | 作者明确规则是理想态，可按项目调整；示例Controller也直接读Model | 采用节点/窗口生命周期对称退订；GGF池复用按OnOpen/OnClose处理，不照搬仅等节点销毁的方案 |
| [CrazyCar固定版本](https://github.com/TastSong/CrazyCar/tree/513932d9f5641ea97082fae0ed206af67129bcfd) | README_Dev、BuyEquipCommand与UserInfoQuery展示规则/实际调用的差距 | 目录名与Query包装不保证职责隔离，不移植它的网络/热更或异步命令链 |
| [UnityRpgGame固定版本](https://github.com/llyy12333/UnityRpgGame/tree/eea9dbbaff8c0e9ef66f3327cbcc1fd3df431a56) | 可参考RPG模块归属和流程 | 其静态单例/BinaryFormatter不是本项目推荐实现，公开Demo不能证明大型商业工程的全部做法 |
| [UGF StarForce](https://github.com/EllanJiang/StarForce)与[GGF](https://github.com/NuoYan/GGF) | Procedure拥有导航/服务请求，框架提供生命周期 | 使用本仓库的实际API和已授权框架修正，不从示例猜测版本或重复建设管理器 |
| [Godot原生动画](https://docs.godotengine.org/en/stable/classes/class_animationplayer.html) | 帧、方法键、分段、完成信号可直接作者化 | 具体GGF状态负责招式分支；不再建立平行时间轴解释器 |

这些是已有调研结论，不声称本轮重新遍历或排名全部开源项目。GGF以源码快照随仓库保存，没有可证明的独立上游Tag；精确历史快照见审计记录。

## 当前重构判断

- 先查数据所有者、真实消费者、生命周期及失败路径，再合并或删除。无调用的SpatialExtensions属于已失去用途的旧估距API，本轮删除；正在使用的SpatialUtility及Bindable契约保留。
- 原生场景绑定、配置校验、文件IO、异步取消和池复用需要的保护保留；不为减少判断吞掉失败，不为测试添加生产观测API。
- 玩家/敌人/技能/特效都有实际差异，不把所有行为塞进一个CombatManager，也不逐招建立新System。共享部分已有Hero/Enemy基类、身体/AI状态和SkillEffectNode。
- 双人有明确未来需求，但当前清理不预造双玩家容器。实施时先修改同一状态所有者的玩家索引与调用契约，再逐步接入力、HUD、相机、奖励和存档，方案及验收见ProjectTour。
- 测试与官方作者工具是维护能力，不因未被游戏运行时调用就删除；生成资源登记只能证明被扫描，不能单独证明资产有真实消费者。

新功能按第00/10/30/40/50/70章执行。本页不另设与规范竞争的“给GPT硬规则”、阶段门或审批流程。
