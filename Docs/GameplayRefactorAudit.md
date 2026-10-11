# 关卡边界与玩法职责整理

2026-10-11。本轮目标覆盖整个手写游戏代码，框架、插件、Luban 及生成配置保持只读；生成 UI 绑定仍走原生成器。暂存区不改动。本文记录证据、决定与尚未验证项，不以编译通过代替完整验收。

## 验收清单

| 用户要求 | 当前证据 | 需要完成的工作 | 状态 |
| --- | --- | --- | --- |
| 阶段墙与官方表现一致，解释首波场外刷怪 | 官方 BaseObject / BaseHero 限制身体在屏幕 x=20～920；出生点确实在停止点之外 | 补随屏幕移动的玩家墙；怪物以取景中心巡逻；首波保留真实 AI 验证入场，四波真实移动与清场 | 已验证 |
| 战斗结构与命名清晰 | 攻击实例、公式输入快照、结算输出职责不同 | AttackInstance / CombatStatsSnapshot / DamageResult；按 Attacks、Damage、Attributes、Resources 分类 | 已验证 |
| MonsterDefeat 等载荷清晰且必要 | 击败事实只有怪物配置 ID 有实际消费者 | 删除 MonsterDefeat；死亡事件只保留实体与配置 ID；伤害事件直接携带 DamageResult；LoadedProfile 改为 ProfileLoadResult | 已验证 |
| Profile / Session 去除绕行和冗余 | 空装配链、成长包装、测试曲线 getter、上下文重复引用 | HeroId 并入 HeroProgression；HeroBattleSnapshot 替代 HeroLoadout 与 HeroStatBuilder；PlayerSession 只关联本槽档案和保存服务；LevelRewardTransaction 直接依赖所需对象；流程删除上下文字段 | 已验证 |
| 全项目同类问题与阅读路径 | 43 个手写类型目录和命名空间不一致；部分事件只有测试订阅 | 对齐 Level、身体/AI Core 与 States、HurtBox 命名空间；删除属性和钱包的无消费者事件、测试用击杀者状态、无用计时器字段；提供架构图和阅读顺序 | 已验证 |

## 初步事实与处理原则

- 官方第一代原程序缓存 `gamezmxy-official-v25928.swf` 的 SHA-256 为 `a5e9650ca4c3b779d62fb7987a0b7e7d5af5983b3197fa2f2e846a91b3f9ccce`，与此前下载登记一致。`ViewControllor` 的停止点触发窗口是屏幕 x=900～980；`MonsterAppearPoint` 按场景标记创建。后续定位到 BaseObject 的屏幕内侧限制和 BaseMonster 的视野中心巡逻，说明出生点正确，但原来的固定墙/出生点巡逻不能完整表达这两个行为。
- 攻击实例拥有目标去重集合并跨帧存在；属性快照按值冻结出招/受击时属性；伤害结果携带最终数字与闪避/暴击。这三者职责不同，重构会通过命名和目录表达，而不是为了减少类型混合它们。
- 合并没有独立职责的包装，保留实际保证一致性、对象池生命周期、事件解耦和事务回滚的边界。未实现功能不能成为继续堆叠空装配和共享上下文的理由。
- 前面的角色无交互、保存提示、地图放大命中、无双满值闪烁、存档 v3 与调试按钮隐藏均已回归。

## 审查范围与保留的边界

- 检查手写 GameScripts 与 MainPack 业务入口中的类型职责、目录命名空间、调用链、事件实际消费者、状态来源、异步等待与清理。身体/AI 纯规则同时由 84 项战斗单测覆盖，档案由 23 项、关卡调度由 17 项覆盖。
- `Profile` 现在只有档案、英雄成长、钱包、经验曲线四个主要类型；`Session` 只有本槽会话和本关收益事务。没有把每个数据载荷都拆成独立层，也没有为未来装备维护空修正链。
- 保留 PlayerSession 是为了把本槽档案与绑定本槽编号的存档服务一起传给流程，避免跨槽配错；它不再汇集曲线、配置总表或构建器。关卡收益事务不持有它。
- 保留 HeroBattleSnapshot 是为了让实体接收冻结的战斗输入，不引用可变档案；保留 MarkerEntry 是编辑器装配契约；保留 UI Data、GGF 池化 Variable/EventArgs 是实际框架与生命周期边界。
- 不改 Framework、addons、Luban、源表与生成配置；移动手写文件保留原 UID，删除已无类型的 UID。用户暂存区未改动，现有场景地形与地图缩放参数保留。

## 规范依据与差异报告

执行集中规范第 00 章的分层/显式依赖/作用域所有权、第 10 章的清晰职责/实际消费者/测试不侵入、第 30 章的 AI 决策与身体执行分离、第 40 章的场景装配边界、第 50 章的工具只读与真实验证。

集中规范第 00/10/30/70 章仍列出旧名称 GameContext、LevelRun、HeroRecord、HeroLoadout/Builder、Battle/Stats；第 30 章的巡逻说明仍以出生点为中心。当前用户明确要求简化这些设计并核对官方行为，本轮按该请求修改实现。遵照入口“规范与实现冲突时报告，不自行更改规范”，没有改写集中规范；实际实现、映射和限制集中记录在本文与 [GameplayArchitecture](GameplayArchitecture.md)。UI 沿用既有生成器的 GameLogic.UI 命名空间，未手改生成绑定。

官方第五波 Boss、非对称滚屏及逐帧 AI 复刻仍不在既有四波原型范围；巡逻半径/索敌和速度继续来自当前正式表，不声称全关已与官方完全一致。

## 最终验证

| 命令 / 模式 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet -clp:ErrorsOnly` | 0 错误；5 个原有框架过时 API 警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 84/84 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 23/23 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 17/17 |
| `python Tools/ProjectMaintenance/validate_game_references.py` | 174 场景路径、195 UID 通过 |
| `git diff HEAD --check` | 通过 |
| `--smoketest=level` | 退出 0；首波场外入场、20px 屏幕留白、左右阻挡、四波清场、63 经验与检查点通过 |
| `--smoketest=ui` | 退出 0；无双满值循环、圆形效果、升级、HUD/实体复用、死亡保存与重开、放弃回滚、备份恢复通过 |
| `--smoketest=ai` | 退出 0；巡逻、追击、平台守候、受控、死亡与回收通过 |
| `--smoketest=level-cancel` | 退出 0；1 个在途显示取消，晚到结果回收通过 |
| `--smoketest=frontend` | 最终代码退出 0、SMOKE PASS；含放大轮廓保持与点击、关闭复用、锁定角色、保存提示、音量与调试按钮 |

引擎命令通过 `S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject --quit-after N -- --smoketest=<模式>` 执行；level/ai/cancel 使用 headless，UI/frontend 用 `gl_compatibility` 保存真实画面。日志在临时目录 `gamezmxy-refactor-*.log/.err`；截图在忽略的 `.godot/validation/frontend/`。测试档案仅写入每次随机的 `user://Validation/<模式>/<随机 ID>`。

测试限制单独登记：NuGet 漏洞索引在受限网络下给出 NU1900；Godot 编辑器重扫发生已登记的 ComponentInsoector 退出异常，退出 -1073741819；运行模式断言通过且退出 0，但关闭阶段仍有原有 DefaultWebRequestAgentHelper 的已释放节点异常。没有把这些异常记为“全部无警告”。
