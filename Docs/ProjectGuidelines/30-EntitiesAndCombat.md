# 30 实体与战斗

> 状态：已实现部分按当前代码约束；旧项目只提供玩法参考；未实现内容不预设方案。  
> 证据：`Godot/GodotProject/TheGame/GameScripts/Entity/`、`GameScripts/Systems/Combat/`、`TheGame/Entitys/`、`project.godot`、旧项目 `ZMXY_BHYH/Script/Base/BaseHero.gd` 与 `BaseMonster.gd`。

## 已实现

- **实体层级**：AbstractActorEntity 提供人怪共用的物理、动画与受击能力。敌方按 `AbstractEnemyEntity → AbstractMonsterEntity / AbstractBossEntity → 具体敌人` 组织；Enemy 管共同生命、感知、攻击与 FSM 生命周期，Monster 提供小怪预设，Enemy统一登记/归还离身攻击，Boss保留首领共同行为，具体敌人保留专属选招和无敌等行为。
- **基类与具体类**：共用机制放抽象实体，角色与怪物的特殊行为放具体实体和状态。具体英雄声明 CreateBodyStates 与输入匹配；具体怪物选择 AiPreset，并按需要覆写 BuildBodyParams、BuildAttackParams 和 TryRequestAttack。
- **生命周期**：显示和隐藏经 GF.Entity；OnInit 装配固定节点，OnShow 重置本实例局部状态并建立状态机，OnHide 停止状态、回收攻击与特效。英雄显示前由流程注册并初始化 HeroModel/HeroSystem；怪物接收 EnemySpawn，以唯一基础BaseAttributes重建生命、防御与经验；守关/等级变体由具体物种的专属Resource及规则处理。
- **数据所有权**：英雄属性、生命魔法、无双与施放状态归 HeroModel；档案成长、装备和灵魂归 ProfileModel，学习记录归 SkillModel。实体拥有节点、运动、输入消费和身体/AI FSM；怪物的普通生命、防御、冷却等局部状态归本实例。
- **属性汇总**：HeroSystem 按 `(基础 + Σ固定值) × (1 + Σ加算百分比) × Π(1 + 独立百分比)` 计算最终值，下限 0，整数四舍五入。成长来自 BaseAttributes，穿戴和局内修正按 AttributeSourceId 登记；SetSource/RemoveSource 更新同一来源的完整修正。
- **结算快照**：出招与受击时读取 CombatantAttributes；AttackData 保存出招快照、威力与命中去重，已发出的攻击保持当次数据。
- **生命与死亡**：ReceiveHit 计算 DamageResult，HeroSystem 或怪物本实例执行扣血；首次归零置 Dead 并触发一次 Died。英雄恢复经 HeroSystem，死亡动作归身体状态，关卡结局归 LevelSystem。
- **身体状态机**：使用 `GF.Fsm`，由实体 `_PhysicsProcess` 调用 `BodyFsmExtensions` 的 C# 14 扩展成员 `fsm.Tick(dt)`；每帧顺序为输入采样、状态推进、`MoveAndSlide`。状态类表达动作与打断，不以多个布尔量拼状态。状态查询直接使用 `IFsm.CurrentState`，测试观测放在验证代码内。
- **敌人 AI**：AI按框架帧写意图，身体按物理帧消费。AbstractEnemyAiState及公共Patrol/Engage/FlyingPursuit/DistanceSkillEngage状态复用共同决策；首领必要时继承AbstractBossAiState取得共同时钟，具体选招留物种。EnemyAiData由每个实体独占；系统不拥有节点。
- **落地**：IActorBody.OnFloor 是人怪共用事实，AbstractActorEntity 使用本次显示首次 MoveBody 后的真实物理落地；怪物 AI 提交与身体消费攻击都检查落地，空中请求作废。
- **小怪血条**：受伤后按实体场景设置的秒数淡出；治疗更新比例，连续受伤重新计时，暂停停止计时，死亡与复用重置。
- **出手节奏**：EnemyAiSystem推进共同游戏秒时钟，按已配置攻击间隔、欲望和逐招冷却决策。物种特殊选招保留其源规则，不再使用已删除的StandAndStrikeState/AttackFirstDelay预设。
- **动画与判定**：原生 `AnimationLibrary` 的值轨道编辑帧、特效及判定盒几何/启闭；方法轨道以 `AnimEvent` 入口表达出手、声音及阶段时点。挥击、技能和死亡声音由对应动画方法轨道触发 `PlaySoundAnimEvent(SoundId)`；普通业务代码直接调用无后缀实现或 GGF 音频服务。成功受击的反馈由 ReceiveHit 按接收实体的 m_HurtSoundId 经 GGF 播放，人怪同源，霸体及零击退也生效，hurt 动画不重复发声。AttackData 与独立效果不保存第二份命中声音。普通状态读取长度按物理秒数计时；技能状态每物理帧手动推进一次原生播放器，不再自建时间线、事件枚举或第二个游标。
- **招式职责**：输入匹配归具体英雄，激活/扣费归 `SkillSystem`，运动、判定威力、阶段分支及打断归对应 GGF 状态；动画节点入口只连接当前状态。方法回调只登记动画切换/结束请求，在播放器推进返回后执行，不在轨道遍历中换动画、销毁状态或回池。死亡收益通知及碰撞清理归状态生命周期，不依赖视觉方法键。
- **退出与复用**：状态退出释放攻击包，应用原生 `RESET` 轨道并恢复普通播放；资源库负责特效默认值，不在角色中硬编码节点路径和空动画名。`HeroModel.ActiveSkillId` 只记录激活编号；独立效果不持有施放包装，延迟回调由所属状态核对原 FSM、当前状态与 `EnterSerial`，取消和复用均不可影响下一次施放。
- **判定几何**：英雄、怪物、BOSS、独立技能效果和普通地形优先用矩形、圆、胶囊等基础形状，以少量形状覆盖主要有效区域。攻击通常一个形状，确有不同区域时可用两个；不按图片 alpha 提取逐像素多边形，不为闪电间隙或美术毛边拆大量碰撞体。排除乌云等纯装饰和外侧大块无攻击区域；按招式阶段调整范围即可，不逐帧追随细小视觉变化。同一用途不重复建立相同判定，身体、受击、感知区按各自物理层分工。
- **地形简化**：地面、平台、墙使用原生矩形等基础形状。平顶带双侧斜坡的土坡保留一个六点简单整体凸坡体，减少形状数量并避免内部侧面和接缝；不按草叶或图片轮廓细分。调整时保留已有空间覆盖、单向平台行为与美术归属，检查走跑、坡面和跳跃。
- **攻击范围**：实际伤害由原生HitBox基础形状与动画启闭决定，当前世界形状经HurtBoxNode.Intersects复核后才消费AttackData去重。AI发现/交战/巡逻读取Perception原生矩形、圆或线段，禁止从动画伤害形状并集反推AI距离。AttackSource保存归属实体、实际施放者和判定来源的值快照，不持有节点。
- **战斗结算**：`Systems/Combat/` 保存属性快照、攻击包、伤害结果和结算公式；不读取场景树或引擎全局状态。随机值由调用方提供。攻击包用 `ReferencePool` 管理，一招对同一目标只结算一次；物理、魔法、真实伤害及击退系数取现有战斗配置。
- **事件边界**：跨模块结果通过 `GF.Event` 发布池化事件参数。事件只携带值，不持有攻击包或实体；订阅者在分发期间读取，不保存事件对象。转发时创建新参数实例。
- **物理层**：代码使用层名，不写位掩码数字：`World`、`PlayerBody`、`EnemyBody`、`Platform`、`PlayerHitBox`、`EnemyHurtBox`、`EnemyHitBox`、`PlayerHurtBox`、`MagicWeapon`、`Trap`、`Item`、`Exit`、`Detector`、`PlayerBarrier`。层序由 `project.godot` 定义。

## 敌人原生碰撞显示（2026-10-30）

按用户本轮要求，敌人及其效果的原生 CollisionShape2D 统一语义颜色：身体灰蓝 `(0.58,0.64,0.72,0.12)`、受击青绿 `(0.30,0.66,0.62,0.14)`、攻击砖红 `(0.88,0.36,0.30,0.18)`、首次索敌淡蓝 `(0.36,0.58,0.80,0.035)`、巡逻灰绿 `(0.45,0.62,0.48,0.03)`、交战/追踪/专属选招淡金 `(0.85,0.65,0.35,0.045)`。仅身体和攻击区填色，其他范围显示轮廓或单维线段；不为显示颜色启用禁用的物理形状。Godot 会将禁用形状显示为灰白，这是原生编辑器行为。

作者工具共用 `EditorScripts/enemy_collision_authoring.gd`，只写原生 debug_color/debug_fill；运行时不增加绘图器。调整后检查代表物种的实际原生视口，保持大范围低存在感、身体与动作可见。

## 旧项目参考

- `ZMXY_BHYH/Script/Base/BaseHero.gd`、`BaseMonster.gd` 可查角色、怪物的旧玩法和数值表现；旧实现含集中式行为和状态标志，不沿用其代码结构。
- 旧项目判定、掉落和音效数据只作核对依据；新逻辑按当前实体、战斗、配置和事件边界重写。

## 扩展边界

当前悟空、唐僧、全部造一关卡敌人、飞行/海怪和既有子弹已接入，不再列“其他Boss待设计”的过期待办。单人缺口及双人设计只维护[ProjectTour](../ProjectTour.md)。新增实际角色/攻击时沿用现有具名状态与独立效果，复杂阶段按实际需要使用原生标记或GGF状态；不预建通用技能框架。现有NodePool掉落与效果仍由创建者会话拥有；GGF AttachEntity不替代该回收责任。
