# GameZMXY 架构审计报告与分阶段重构计划

> 审计性质：全面只读架构审计（未修改任何代码）。按用户规范 Phase 1–5 执行完毕，本文件即交付报告与整改计划，**等待确认后才开始 Phase 6 实施**。
> 审计方法：5 路并行代码级审计（Arch/GGF/QF 集成、英雄战斗、敌人 Boss、关卡/UI/存档、横切代码质量），全部结论附 `file:line` 证据；P1 级问题经人工二次复核原文确认。

---

## 1. 整体架构评价

**项目事实**：Godot 4.7.2 .NET / C# net10.0；GGF 位于 `Framework/`（只读依赖，UnityGameFramework 的 Godot 移植）；QF（QFramework）思想落地为自研契约层 `TheGame/GameScripts/Arch/`（10 文件，非引库）。手写业务代码约 276 文件 / 3.5–4 万行（GameProto 为 Luban 生成，另计）。测试 51 文件、约 150–200 项，全部断言真实行为值。

**总体结论：架构水平高于同规模项目，不存在需要推翻性重构的领域。** 经此前多轮整改（见 ArchitectureReview.md / WorkingNotes.md），职责边界、生命周期事务性、防御纪律已接近规范文本的执行上限：

- 单根 Architecture（主流程 FSM 的数据区是唯一存储，`FsmArchitecture` 只是视图），注册事务三保险（Init 失败对称清理、Deinit 失败不阻移除、半初始化对象不复用）。
- 事件总线唯一（GF.Event 仅 3 处 Fire，全部是跨模块一次性事实）；池化三轨分工（GF.Entity / NodePool / ReferencePool）互不重叠；场景切换 100% 收敛于 GGF；生产代码零运行时节点名字符串查找。
- 伤害公式单点（CombatSystem.Calculate + DamageResult readonly struct）；死亡单一事实源（Dead 仅 ReceiveHit 归零时置位）；出招/受击双向属性快照（CombatantAttributes 值类型）。
- 手写代码零 TODO/FIXME/HACK 标记；存档四层身份核对结构性排除写错槽。

**核心问题（本次审计新增发现）**：无 P0。1 项 P1（实体层跨系统耦合 + 潜在崩溃点）、约 10 项 P2（集中在敌人/Boss 重复实现家族、战斗随机公式 12 处复制、双命中管道不对称、地图硬编码、3 处 async void/清理契约风险）、其余为 P3 维护性项。

**适合长期维护与扩展**：是。当前骨架（双 FSM 敌人架构、纯 C# 身体状态可单测、UI 生命周期模板）对即将到来的唐僧角色、双人模式是正确的扩展面；主要风险是敌人侧重复实现会使"每个新 Boss 重抄一遍"的模式延续，应在扩容前收敛。

---

## 2. Godot + GGF + QF 集成审查

### 2.1 结论：三方分工正确，无需引入或替换任何框架

| 关注点 | 结论 | 关键证据 |
| --- | --- | --- |
| GGF 是否被正确使用 | 是。GF 门面使用全部落在组件边界；`InitializeArchitecture` 生产代码仅 ProcedurePrepare 一处；Model 层零节点引用、System 层零 Node 类型 | ProcedurePrepare.cs:65；各 Model 全文核对 |
| QF 是否与 GGF 重复 | 否。Arch 层做了正确减法：命令不能链式发命令（ICommand 不继承 ICanSendCommand）、Query 无 Utility 能力、无父子 Architecture 回退查找、无 EasyEvent | ArchitectureContracts.cs:104-126 |
| Command/Query 滥用/欠用 | 无。SendCommand 6 处全部是玩家业务修改；SendQuery 仅整包出售汇总（Query 可选定位的正确落地）；战斗推进直接 Entity→System（规范明文允许） | InventoryForm.Logic.cs:394-456 |
| 事件总线重复 | 无。Bindable（持续状态）与 GF.Event（一次性事实）双通道即规范本身 | GF.Event.Fire 全仓仅 3 处 |
| 生命周期多轨 | 无。流程 FSM 数据区是唯一存储，键前缀 `"Architecture:{AssemblyQualifiedName}"` 与流程裸键、GGF VarInt32 键零冲突 | FsmArchitectureExtensions.cs:301 |
| 与 Godot 原生边界 | 清晰。动画/判定几何/声音时点归原生 AnimationLibrary；无该用原生而封装的地方（Godot 4.x 无内建节点池，NodePool 有存在必要） | — |

### 2.2 集成层遗留问题（P2/P3）

- **清理异常契约缺口（P2）**：`RemoveMember` 的 Deinit 异常在 finally 移除后继续向上传播（FsmArchitectureExtensions.cs:40-48），`ProcedureTitle.OnEnter` 等调用方无兜底——一个 OnDeinit 抛错可中断标题装配。现有 OnDeinit 实现均简单，风险低但契约无保护。
- **注册 catch 内死分支（P3）**：`RegisterModel/RegisterSystem` 中 `if (Initialized) Deinit()` 不可达（基类 Init 失败已内部清理），注释与实际清理层次不符（FsmArchitectureExtensions.cs:129-133、182-186）。
- **死 API（P3）**：`BindableProperty.SetValueWithoutEvent` 全仓零调用者。
- **规范张力（P3，报告不擅改）**：`SoundExtensions` 位于"纯扩展"目录却读 `GF.Fsm` 全局状态（SoundExtensions.cs:36），与 00:91 条款冲突——属规范未覆盖"GGF 组件适配扩展"形态。

---

## 3. 2D 横版动作游戏架构审查（按实际存在系统）

### 3.1 玩家控制与状态机 — 优，无正确性缺陷

- 打断优先级单点（`AbstractHeroBodyState.Interrupt`，死亡>捕获>受击>本状态），`MaxHopsPerFrame=4` 防同帧互跳死循环；`IHeroBody` 纯 C# 零节点引用，配 1020 行真实 FSM 单测（24/60/120Hz 官方跳跃轨迹验证）。
- `AbstractHeroEntity._PhysicsProcess`（AbstractHeroEntity.cs:382-480，约 99 行 10 阶段）编排膨胀：冻结/中毒的视觉表现内联在物理循环——每加一种状态效果就再加一段（P2，提取表现方法即可，不动架构）。
- 跳跃数 `JumpCount` 四处写者、"占用二段跳"片段在升龙/风暴两处复制（P3，加 `ConsumeAirJump()` 宿主方法）。
- InputSystem（481 行）承载官方 Flash 输入语义（动作闩锁、双击奔跑、组合键、连段冷却），复杂度合理非过度设计；仅 2 处边沿检测冗余（P3）。

### 3.2 战斗与伤害系统 — 结算责任归属清晰

- 完整链路：`InputSystem.Sample → MatchActionInput → SkillSystem.TryActivate(原子校验+登记) → 身体状态 ChangeState → PrepareAttack(出招快照+威力冻结+池化 AttackData) → 动画 HitBox → TryRegisterHit(一招一目标去重) → ReceiveHit → CombatSystem.Calculate(单点公式) → ApplyDamage(英雄走 HeroModel/怪物走实例 m_Hp) → DamageDealtEventArgs(值复制)`。
- 伤害计算无重复、扣血无重复、攻击包归还时序安全（战斗链路零 await，唯一跨帧回调带三重身份核对）。
- **双命中管道不对称（P2）**：效果管道（SkillEffectNode）有 Side 检查 + `Intersects` 形状级复核（防池化复用/瞬移后的过期 overlap 缓存，正是 2026-11-01 修复的火魔 700px 误伤根因）；身体 HitBox 管道（AbstractActorEntity.cs:788-816）两者皆无。实际误命中未成事实（火魔走效果管道），但规则不一致是未来新攻击来源的陷阱。
- **攻击力随机公式 12 处复制（P2）**：`Lerp(FlatPower.X, FlatPower.Y, GD.Randf())` 基准在 AbstractActorEntity.cs:749-750，Boss 特殊路径无法复用 PrepareAttack 各自复制，且舍入语义 4 种并存（(int) 截断 / RoundToInt / 银行家 / AwayFromZero）——同一规则不同伤害分布。
- 玩家/敌人完全复用同一结算（差异仅生命所有权）；无敌帧（技能区间+连击保护）统一于 `Invulnerable` 读数；霸体≠无敌明确。

### 3.3 敌人与 Boss 系统 — 骨架优秀，重复实现是最大技术债

- 双 FSM（AI 意图→身体消费，`TakeAttackRequest` 取走即清、FSM SetData 传参取走即删）是教科书级设计；继承链最深 5 层无空中间类；死亡→AI 销毁→身体回收→池归链路对称。
- **重复实现家族（P2，本次审计最主要发现）**：
  | 重复模式 | 份数 | 证据 |
  | --- | --- | --- |
  | 走动 Boss MoveState（≡ EnemyWalkState，仅 ChangeState 目标不同） | 4 | EnemyWalkState.cs:14-36 ≡ DragonKingMoveState / BullDemonKingMoveState / SharkDemonKingMoveState（当前打开文件即其一） |
  | "等外部效果完成再回收"死亡状态 | 3+3 变体 | Jiao/Shark/PengDemonKingDeathState.cs:24-52 逐字同构；BullHeart/DragonKing/Transform 为变体 |
  | Engage 状态逐字/近似复制 | 3 对 | Jiao≡Shark（32 行除类名相同）；Knife≡Tide（连注释相同）；Bat≈FlyingDemon（90 行仅悬停高度 90/60 差 2 行） |
  | 无敌倒计时字段+属性+清零+递减+AnimEvent 入口 | 6（grep 40 处） | DragonKing/BullDemonKing/Shark/Peng/JiaoDemonKing/SharkSpawn |
  | SpawnBossDeathAnimEvent | 4 | Jiao/Shark/Peng/BullDemonKing Entity |
  | Boss Engage 前奏（IsAttacking/HasTarget） | 8 | AbstractBossAiState.cs:28-42 已示范可上提 |
  | 攻击力随机公式 | 12 | 见 3.2 |
- **AI 契约张力（P2）**：`IEnemyAiAgent` 声明"AI 状态纯 C# 可单测"（IEnemyAiAgent.cs:5-11），但 8 个物种的 Engage 状态下转型具体实体读物种事实（如 BatEngageState.cs:29）——反例证明可行：DragonKingEngageState 构造注入 attackDesire、KnifeDemonEngageState 只覆写 Attack()。
- **P1（唯一）**：`BullDemonHeartEntity.BeginDeathPresentation`（BullDemonHeartEntity.cs:153-177，已亲自复核）——死亡表现方法内直接：Roll 装备 → ProfileSystem 入包 → `GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single().ShowMessage("获得 金钢琢")`。三层问题：① 实体横跨 Profile/Equipment/UI 三个子系统；② `Single()` 在表单未加载/多实例时抛 InvalidOperationException 中断死亡流程；③ 文案硬编码绕过 UIStringKeys。金钢琢"直接入包"是官方特例，但实现位置错误。

### 3.4 技能与属性 — 优

技能定义（SkillDefinition 登记）/学习记录（SkillModel）/本局激活（HeroModel.ActiveSkillId+SkillSystem 登记）/招式执行（具体状态）四段分离干净；`(基础+Σflat)×(1+Σ加算%)×Π(1+独立%)` 单点汇总（stackalloc 实现）；Resource 共享陷阱不存在（配置只读、运行态归 Model/实体）。

### 3.5 关卡/场景/UI/存档 — 优，少量风险点

- ProcedureLevel（815 行）三线异步防护（entrySerial + CancellationToken + 场景所有权记账）与 CleanupSession 幂等逆序拆除超出一般项目水准；关卡 VC 9 项订阅完全对称退订；GetNode 违规扫描：生产代码零违规。
- UI 十窗口生命周期模板（OnInit 单次绑定→OnOpen 订阅+回滚→OnClose 退订→OnRecycle 清理）零例外执行。
- 存档链路（SaveSystem→ArchiveUtility→GameData→SaveDataUtility 拒绝式校验）与关卡事务（进关快照/通关提交/放弃回滚）逐条落实规范。
- **风险点**：① `NavigateAfterCheckpointAsync` async void 裸 `await write`（ProcedureLevel.cs:676-705，已亲自复核）——GGF 写盘任务若以异常终止将进程级崩溃，发生在"结算返回地图"关键路径（其余 7 处 async void 均有兜底）；② MapForm 关卡 1-10 硬编码于 5 处平行维护点，`_ => throw`（MapForm.Logic.cs:517-535）——新增第 11 关需改 5 处；③ SaveNoticeForm `Single()` 直查 3 处生产调用（SkillForm.Logic.cs:253/275 + BullDemonHeart）。

### 3.6 动画/特效/音效 — 优

方法轨道回调只登记请求、播放器推进后才执行的帧安全纪律全链一致；SkillEffectNode 统一离身攻击生命周期（含 Intersects 几何复核与命中预算）；表现层无越权。

---

## 4. 代码质量审查

| 维度 | 结论 |
| --- | --- |
| 重复代码 | 敌人/Boss 重复家族（见 3.3）与攻击公式 12 处是仅有的实质性重复；Utilities 四工具、Event 三件套零职责重叠 |
| 补丁式实现 | 无。全仓零 TODO/FIXME/hack/临时标记；无"为修 bug 加的 flag"；catch 均 Log+补偿动作，零吞异常 |
| 复杂逻辑 | >800 行仅 4 文件（AbstractEnemyEntity 1019 / AbstractActorEntity 873 / ProcedureLevel 815 / AbstractHeroEntity 792），职责内聚不建议按行数拆分；>60 行方法仅 2 个 |
| 不必要抽象 | 无上帝类、无空转发层（HeroLoadout 等历史包装已在前轮清理） |
| 防御性编程 | 密度健康：检查集中在四类合法边界（资源加载/await 后/池化复活/外部数据）；冗余判空合计约 14 处恒真级（如 SkillSystem 对 HeroModel 7 处判空 vs 其他系统零判空的风格分裂、OnHide 的 IsInstanceValid 不对称、冻结三处检查两处不可达） |
| 生命周期/资源 | 池化归还、订阅退订、异步守卫经逐行核对成立；遗留项仅为引擎/插件退出诊断（Validation.md 已单列，只读边界不处理） |
| 性能 | 仅一处实质热点：`PollAttackOverlaps` 每物理帧无条件 `GetChildren()`+`GetOverlappingAreas()` 分配（AbstractActorEntity.cs:770-774，数十敌人时 GC 压力）；其余逐帧路径干净（MainPack 业务侧零逐帧回调） |

---

## 5. 代码规范与编辑器体验

- 风格一致性：m_ 前缀 100%、XML 文档齐全、类名=文件名、partial 命名规范；CSharpStyle 工具自动护栏 + regenerate_ui_form.py 生成器 + validate_game_references.py + audit_config_* 等工具链完整且互不重叠。
- 命名空间（业务域）与目录（技术层）双轴组织是既定选择，仅 `Systems/Hero → GameLogic.Battle` 映射建议在 00 章补全（规范维护需用户授权）。
- EditorScripts 与运行时代码零交叉引用；测试断言真实行为值，无生产观测 API。
- 编辑器侧改进机会（低优先）：MapForm 地点表数据化后 Inspector 可维护性提升；相机编辑器绘制按需开关。

---

## 6. 推荐目标架构

**维持现有 GGF + QF + Godot 三方分工，不引入新框架、不建新容器层。** 本轮全部优化是"收敛重复 + 消除风险点"，目标结构增量只有三处：

```mermaid
flowchart LR
    subgraph 敌人共享层（新增收敛点）
        A[EnemyBodyPresets.Walk&lt;TAttackState&gt; 泛型预设] --> B[4 份 MoveState 删除]
        C[共享死亡状态基类<br/>完成源参数化: 动画时长 / 外部效果回调] --> D[6 份死亡状态收敛]
        E[AbstractEnemyEntity 无敌倒计时<br/>受保护字段 + 统一 AnimEvent] --> F[6 处复制删除]
        G[AbstractEnemyAiState 上提<br/>IsAttacking/HasTarget 前奏] --> H[8 处样板删除]
    end
    subgraph 战斗
        I[AttackRoll 静态规则<br/>入 Systems/Combat] --> J[12 处随机公式收敛]
    end
    subgraph 提示链（P1 修复后）
        K[Boss 死亡掉落] -->|EnemyDied/专用事件 或 LevelSystem 收益链| L[ProfileSystem]
        L -->|GF.Event 一次性事实| M[SaveNoticeForm 订阅显示]
    end
```

关键接口设计约束：物种专属 AI 状态的数值事实一律构造注入（沿用 DragonKingEngageState 注入 attackDesire 的既有范式）；实体不得直接调用 UI 窗口方法；`async void` 必须自带兜底 catch（对齐 SaveAndNavigateAsync 既有结构）。

---

## 7. 问题清单（合并去重后 22 项）

| # | 问题 | 代码证据 | 根本原因 | 影响 | 优化方案 | 优先级 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 牛心死亡表现内嵌装备发放+UI 直调+Single() | BullDemonHeartEntity.cs:153-177 | 官方特例直写实体 | 潜在崩溃+跨系统耦合 | 掉落走 ProfileSystem/关卡收益链，提示改事件订阅或安全取窗 | **P1** |
| 2 | async void 裸 await 检查点写盘 | ProcedureLevel.cs:676-705 | 该方法未套用项目既有兜底模板 | 写盘任务异常→进程崩溃 | 补 try/catch（OCE 放行+Log.Error），复用 SaveAndNavigateAsync 结构 | **P2** |
| 3 | RemoveMember 的 Deinit 异常向上传播无兜底 | FsmArchitectureExtensions.cs:40-48；ProcedureTitle.cs:42-48 | 清理契约未定义异常处置 | OnDeinit 抛错中断下一流程装配 | 移除段包 try/catch 记 Fatal，或 RemoveMember 吞 Deinit 异常转日志 | **P2** |
| 4 | SaveNoticeForm Single() 直查 3 处 | SkillForm.Logic.cs:253/275；BullDemonHeartEntity.cs:174 | 提示复用走窗口实例直查 | 未加载/多实例时 InvalidOperationException | 统一安全入口（FirstOrDefault+守卫）或事件化 | **P2** |
| 5 | 走动 Boss MoveState 4 份复制 | EnemyWalkState.cs:14-36 ≡ DragonKing/BullDemonKing/SharkDemonKingMoveState | EnemyWalkState sealed 且硬编码目标状态；Walk 预设缺泛型参数 | 新 Boss 必然第 5 份复制 | 仿 `Swim<TAttackState>` 提供 `Walk<TAttackState>` | **P2** |
| 6 | "等效果完成再回收"死亡状态 3+3 份 | Jiao/Shark/PengDemonKingDeathState.cs:24-52 等 | 缺参数化完成源抽象 | 新 Boss 重抄清场序列 | 共享死亡状态基类（完成源+BeginDeath 时点参数化） | **P2** |
| 7 | Engage 状态 3 对逐字复制 | Jiao≡Shark；Knife≡Tide；Bat≈FlyingDemon(悬停高度 90/60) | 物种事实未走注入 | 改一处退避 bug 要同步多文件 | 合并+构造注入差异参数 | **P2** |
| 8 | 无敌倒计时 6 处复制 | DragonKing/BullDemon/Shark/Peng/Jiao/SharkSpawn Entity（grep 40 处） | 通用机制散落各 Boss | 时序调整改 6 处 | 下沉 AbstractEnemyEntity 受保护成员+统一 AnimEvent | **P2** |
| 9 | SpawnBossDeathAnimEvent 4 连抄 | Jiao:182-191 ≈ Shark:213-222 ≈ Peng:325-335 ≈ Bull:216-226 | 共享行为未上提 | 样板噪音 | 上提 AbstractEnemyEntity | **P2** |
| 10 | 攻击力随机公式 12 处复制+4 种舍入 | 基准 AbstractActorEntity.cs:749-750；KnifeDemon:39 等 11 处 | Boss 专属路径无法复用 PrepareAttack | 同规则不同伤害分布，调平衡困难 | 提取 AttackRoll 统一入口（舍入作参数保持行为；是否统一语义待确认） | **P2** |
| 11 | 双命中管道不对称 | SkillEffectNode.cs:252-303（Side+Intersects 复核）vs AbstractActorEntity.cs:788-816（皆无） | 两管道分头演化 | 身体管道理论存在同款过期 overlap 误伤 | 身体管道补相交复核（行为相关需专项验证），或注释声明信任边界 | **P2** |
| 12 | PollAttackOverlaps 每帧无条件分配 | AbstractActorEntity.cs:770-774 | 缺无攻击早退 | 数十敌人时 GC 压力 | m_ActiveAttackData==null 早退 | **P2** |
| 13 | MapForm 关卡 1-10 硬编码 5 处+throw 默认分支 | MapForm.Logic.cs:66-78/309-384/517-535/543-547/593-627 | 地图 UI 与关卡配置双轨 | 新增关卡改 5 处；越界开图即抛 | 地点→按钮→动画键集中单一常量表；default 安全回退+日志（行为变化需确认） | **P2** |
| 14 | 英雄 _PhysicsProcess 表现内联 | AbstractHeroEntity.cs:382-480 | 编排逐步累积 | 可读性/改动扩散 | 提取 AdvanceStatusPresentation 私有方法/partial | **P2** |
| 15 | AI 状态下转型具体实体（8 物种） | BatEngageState.cs:29 等 vs IEnemyAiAgent.cs:5-11 契约 | IEnemyAiAgent 无物种事实扩展点 | AI 状态不可单测 | 存量逐步迁移构造注入；新代码强制遵守 | **P2** |
| 16 | Boss 前奏 8 处重复 | grep `if (agent.IsAttacking)` 8 文件 vs AbstractBossAiState.cs:28-42 | 基类只上提了死亡/受控 | 样板噪音 | 前奏上提 AbstractEnemyAiState | P3 |
| 17 | 死代码：SetValueWithoutEvent、GapX/GapY、注册 catch 死分支 | BindableProperty.cs:82-85；SpatialExtensions.cs:29-42；FsmArchitectureExtensions.cs:129-133 | 预留 API 未出现用例 | 维护面 | 删除（死分支同时修注释） | P3 |
| 18 | SkillSystem 对 HeroModel 7 处判空 vs 全仓零判空风格分裂 | SkillSystem.cs:29/126/150/176/227/267/279 | 注册边界已保证非空 | 理解成本 | 统一信任注册边界 | P3 |
| 19 | 订阅清单 Register/UnRegister 字面量双写 | InventoryForm.Logic.cs:137-146 vs 252-266；BattleForm.Logic.cs:145-155 vs 301-311 | 无共享清单 | 漏改一侧→泄漏/漏刷新 | 提取 static readonly 属性数组共用 | P3 |
| 20 | 英雄层小重复：HoldLaunchAxis 4 处、ConsumeAirJump 2 处、RecordHit 双实现 | HeroAttackState.cs:52-67；WukongRisingDragon:27-57；SkillEffectNode.cs:274-277 | 片段复制而非宿主 API | 维护性 | 提取基类帮助方法 | P3 |
| 21 | InputSystem/OnHide/初始化失败策略等 8 处微小不一致 | InputSystem.cs:148-158；AbstractHeroEntity.cs:337-349；AbstractActorEntity.cs:317-345 | 演化期差异 | 可读性 | 按规范统一（见阶段三清单） | P3 |
| 22 | ProcedureUpdate 递归重试+加载窗双清理；SettingsForm 缺回滚 catch；Title/Map 裸 await 隐式契约 | ProcedureUpdate.cs:228-331/377-381；SettingsForm.Logic.cs:70-88；ProcedureTitle.cs:106-123；ProcedureMap.cs:132-140 | 模板未统一执行 | 窄风险窗口 | 递归改循环；补 catch 模板 | P3 |

**明确不值得改**（审计确认后排除）：空壳预设类（场景脚本锚点，每只 21 行）、MathF/Mathf 混用、表单 GetArchitecture 样板、Command 会话守卫 6 处重复（显式性价值）、ClearWs 幂等二次调用、恒真 IsInstanceValid 两处、命名空间双轴整体重组、按行数拆分四大基类。

---

## 8. 分阶段重构计划

**通用约束（每阶段生效）**：只改 `TheGame/GameScripts/` 与 `TheGame/MainPack/` 手写业务代码；`Framework/`、`addons/`、`GameProto/`、`Tools/Luban/`、正式存档只读；保持游戏行为不变（标注 ☑ 处除外）；每完成一项删除旧实现不留并存；每阶段结束更新 WorkingNotes/Validation 并跑全量验证。

**每阶段验证命令**：
```
dotnet build Godot/GodotProject --no-restore -v quiet          # 0 错误（5 条既有弃用警告允许）
dotnet test Tests/ProfileTests --no-restore -v quiet            # 228/228 基线
dotnet test Tests/BattleTests --no-restore -v quiet             # 178/178 基线
dotnet test Tests/LevelTests  --no-restore -v quiet             # 19/19 基线
python Tools/ProjectMaintenance/validate_game_references.py     # 路径/UID 通过
dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <根> <输出.json>
git diff --check                                                # 无空白错误
```
针对性烟测（隔离 APPDATA，命令形如 `S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject --rendering-method gl_compatibility --audio-driver Dummy -- --smoketest=ui --<专项>`）按涉及系统选择既有专项模式。

### 阶段一：风险消除（P1 + 崩溃路径）

1. **牛心掉落解耦（问题 #1/#4）**：金钢琢直接入包的官方特例移出 `BeginDeathPresentation`——掉落决策归 ProfileSystem/LevelSystem 收益链（或经 EnemyDiedEventArgs 扩展字段走既有 LevelViewController.Loot 链），UI 提示改 SaveNoticeForm 安全入口（FirstOrDefault+守卫，或新增 GF.Event 一次性事实由 SaveNoticeForm 订阅，与 SaveSucceeded 对称）。SkillForm 两处 Single() 同步换安全入口。验证：牛魔谷专项烟测（`--smoketest=ui` bull 相关模式）+ ProfileTests。
2. **async void 风险收口（#2/#3）**：NavigateAfterCheckpointAsync 补 try/catch（对齐 SaveAndNavigateAsync:462-467 结构）；ProcedureTitle.OnEnter/ProcedureMap.OnSaveRequested 调用方补统一 catch；流程侧移除段（ProcedureTitle.cs:42-48）包 try/catch 记 Fatal。验证：frontend 烟测 + 存档往返。
3. 交付物：上述行为不变（仅异常路径不再崩溃）、Validation 记录、WorkingNotes 更新。

### 阶段二：敌人/Boss 与战斗重复收敛（P2 主体）

按"先建共享、再逐 Boss 迁移、每迁移一个即删旧文件"执行：

1. `EnemyBodyPresets.Walk<TAttackState>` 泛型预设 → 删除 DragonKing/BullDemonKing/SharkDemonKing 三份 MoveState（问题 #5）。
2. 共享死亡状态基类（完成源：动画时长/外部效果回调参数化；BeginDeath 时点虚方法）→ 收敛 Jiao/Shark/Peng 三份同构 + 评估三份变体（#6）。
3. AbstractEnemyEntity 无敌倒计时受保护成员 + 统一 `BeginInvulnerabilityAnimEvent` 入口（JiaoDemonKing 动画时长变体参数化）→ 删 6 处复制（#8）；同时上提 SpawnBossDeathAnimEvent（#9）与 Engage 前奏（#16）。
4. Engage 合并：Jiao/Shark 共享一份、Knife/Tide 合并一份、Bat/FlyingDemon 合并并构造注入悬停高度（#7）。
5. `AttackRoll` 静态规则入 Systems/Combat：统一公式入口，**舍入语义作参数逐站保持**（#10；☑ 若选择统一为 AwayFromZero 会产生 ≤1 的伤害分布变化，默认不启用，需单独确认）。
6. `PollAttackOverlaps` 无激活攻击早退（#12）。
7. 英雄：_PhysicsProcess 提取 AdvanceStatusPresentation（#14）；☑ 身体命中管道补 Intersects 复核（#11，属行为收紧——默认先以注释声明信任边界，补复核作为可选项单独确认）；HoldLaunchAxis/ConsumeAirJump/RecordHit 统一（#20）。
8. 验证：BattleTests 全量 + 全敌人/AI/BOSS 既有专项烟测（dragon/bull/qixing/fire-knockback 等）+ `--smoketest=ui` 完整回归；迁移中每删一份复制跑对应物种专项。

### 阶段三：代码规范与 API 统一（P3）

1. 死代码清理（#17）：SetValueWithoutEvent、GapX/GapY、注册 catch 死分支（同步修正注释与清理层次描述）。
2. 判空策略统一（#18）：SkillSystem 7 处判空改为信任注册边界；OnHide IsInstanceValid 不对称统一；AbstractActorEntity 初始化失败策略统一为边界 throw（#21 子项）。
3. UI 模板补齐（#19/#22）：InventoryForm/BattleForm 订阅清单单一来源；SettingsForm 补 try/catch 回滚；InputSystem 边沿检测冗余清理。
4. AI 状态可测性迁移（#15）：8 物种 Engage 的下转型改为构造注入数值事实（分 2–3 批，每批一个物种目录）；迁移后 IEnemyAiAgent 契约注释与实现对齐。
5. PengDemonKing TryRequestAttack 基类校验/登记两步拆分；DragonKingDeathState 受击盒操控并入共享死亡状态。
6. 验证：三套测试 + CSharpStyle + frontend/ui 烟测。

### 阶段四：编辑器体验与长期维护（P3/低收益可选）

1. MapForm 地点常量表集中（#13）：地点→按钮→动画键→提示单一来源，switch default 改安全回退+Log（☑ 行为从抛异常改为不闪烁+日志，需确认）。
2. 相机编辑器绘制 SetProcess 按需开关；英雄 OnShow 缓存系统引用（注意会话重建时机，谨慎评估）。
3. ProcedureUpdate 重试递归改循环（#22）。
4. 规范维护提案（**仅提案，用户授权后改 ProjectGuidelines**）：00 章为 GGF 组件适配扩展（SoundExtensions 形态）补充落位条款；00:92 映射表补 `Systems/Hero → GameLogic.Battle` 条目；30 章补"AI 状态物种事实经构造注入，禁止下转型"与"新增走动 Boss 使用 Walk<TAttackState>/共享死亡状态"条款。
5. Validation/ProjectTour/ArchitectureReview 对应更新，形成闭环。

### 需用户逐项确认的行为影响点（默认全部按"保持行为"处理）

| 决策点 | 选项 A（默认） | 选项 B |
| --- | --- | --- |
| 攻击力舍入统一 | 保持各站语义，仅集中公式 | 统一 AwayFromZero（伤害分布 ±1 内变化） |
| 身体命中管道 | 注释声明信任边界 | 补 Intersects 复核（行为收紧，可能拒绝边缘命中） |
| MapForm 越界默认 | 维持 throw | 安全回退+日志不闪烁 |

---

## 9. 最终工程规范（供后续 AI 编程持续遵循，新增条目提案）

既有 ProjectGuidelines（00–70 章）继续作为唯一规范源；本轮审计建议在其上**增补**以下条目（经用户授权后并入对应章节）：

1. **实体不碰 UI**：实体/状态层不得直接调用窗口方法或遍历 UIForm 集合；跨模块一次性提示走 GF.Event 由窗口订阅。
2. **async void 纪律**：所有 async void 必须自带兜底 catch（OCE 放行、其余 Log.Fatal/Error + 用户可见状态），对齐 SaveAndNavigateAsync 既有模板。
3. **敌人扩展走共享预设**：新增走动 Boss 用 `Walk<TAttackState>`；"等外部效果完成再回收"的死亡演出用共享死亡状态基类；无敌倒计时用基类统一入口；禁止再复制这四类模板。
4. **数值事实注入**：AI 状态的物种数值一律构造注入（DragonKingEngageState 范式），禁止下转型具体实体。
5. **随机公式单点**：威力/属性随机抽取统一经 AttackRoll 类入口，新代码禁止手写 `Lerp(范围, GD.Randf())`。
6. **判空边界**：内部契约（注册顺序保证的 Model/System、[Export] 子节点、同一帧内的实例字段）不判空；判空只出现在资源加载、await 后存活性、池化复活、外部数据四类边界。
7. **订阅单一来源**：成对的 Register/UnRegister 清单必须共享同一常量集合。

---

## 10. 审计范围外事项（明确不做）

- 引擎/插件退出期 ObjectDB/RID 诊断、ComponentInsoceptor 释放错误、MP3 流退出泄漏（Validation.md 已单列为只读边界问题）。
- Framework/addons/Luban 生成链、正式存档格式（v12）与既有数据迁移。
- 造一功能缺项（唐僧、连击 UI、双人模式等 ProjectTour 待办清单）——属功能开发非架构整改。
- 按行数拆分四大基类、命名空间双轴整体重组、Command/Query 体系调整（审计确认现状正确）。

---

## 11. 实施顺序总结

阶段一（3 项风险消除）→ 阶段二（7 项重复收敛，最大工作量在敌人侧 12 个文件左右的删除与共享层新建）→ 阶段三（6 组规范统一）→ 阶段四（4 项长期维护，含规范提案落编）。每阶段独立可验证、可暂停；阶段间无强制依赖，但建议按序执行以先消除崩溃面。
