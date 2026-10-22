# 30 实体与战斗

> 状态：已实现部分按当前代码约束；旧项目只提供玩法参考；未实现内容不预设方案。  
> 证据：`Godot/GodotProject/TheGame/GameScripts/Entity/`、`GameScripts/Battle/`（含 `Attributes/`）、`TheGame/Entitys/`、`project.godot`、旧项目 `ZMXY_BHYH/Script/Base/BaseHero.gd` 与 `BaseMonster.gd`。

## 已实现

- **实体层级**：AbstractActorEntity 提供人怪共用的物理、动画与受击能力。敌方按 `AbstractEnemyEntity → AbstractMonsterEntity / AbstractBossEntity → 具体敌人` 组织；Enemy 管共同生命、感知、攻击与 FSM 生命周期，Monster 提供小怪预设，Boss 管独立攻击效果的归还，具体敌人保留专属选招和无敌等行为。
- **基类与具体类**：共用机制放抽象实体，角色与怪物的特殊行为放具体实体和状态。具体英雄声明 CreateBodyStates 与输入匹配；具体怪物选择 AiPreset，并按需要覆写 BuildBodyParams、BuildAttackParams 和 TryRequestAttack。
- **生命周期**：显示和隐藏经 GF.Entity；OnInit 装配固定节点，OnShow 重置本实例局部状态并建立状态机，OnHide 停止状态、回收攻击与特效。英雄显示前由流程注册并初始化 HeroModel/HeroSystem；怪物接收 EnemySpawn，以 BaseAttributes 和配方倍率重建生命、防御、攻击与经验。
- **数据所有权**：英雄属性、生命魔法、无双与施放状态归 HeroModel；档案成长、装备和灵魂归 ProfileModel，学习记录归 SkillModel。实体拥有节点、运动、输入消费和身体/AI FSM；怪物的普通生命、防御、冷却等局部状态归本实例。
- **属性汇总**：HeroSystem 按 `(基础 + Σ固定值) × (1 + Σ加算百分比) × Π(1 + 独立百分比)` 计算最终值，下限 0，整数四舍五入。成长来自 BaseAttributes，穿戴和局内修正按 AttributeSourceId 登记；SetSource/RemoveSource 更新同一来源的完整修正。
- **结算快照**：出招与受击时读取 CombatantAttributes；AttackData 保存出招快照、威力与命中去重，已发出的攻击保持当次数据。
- **生命与死亡**：ReceiveHit 计算 DamageResult，HeroSystem 或怪物本实例执行扣血；首次归零置 Dead 并触发一次 Died。英雄恢复经 HeroSystem，死亡动作归身体状态，关卡结局归 LevelSystem。
- **身体状态机**：使用 `GF.Fsm`，由实体 `_PhysicsProcess` 调用 `BodyFsmExtensions` 的 C# 14 扩展成员 `fsm.Tick(dt)`；每帧顺序为输入采样、状态推进、`MoveAndSlide`。状态类表达动作与打断，不以多个布尔量拼状态。状态查询直接使用 `IFsm.CurrentState`，测试观测放在验证代码内。
- **敌人 AI**：AI 状态机按框架帧运行，只写意图；身体状态机按物理帧执行移动和动作。AbstractEnemyAiState 管死亡与受控优先级；AbstractMonsterAiState 分支负责巡逻、追击、概率出招，AbstractBossAiState 分支负责两位首领的共同行为时钟，各自专属交战状态处理招式选择。共同身体状态留在 Enemies/Body，不为小怪和首领复制同一套状态。AI 通过接口访问目标和身体事实，不持有节点引用。
- **落地**：IActorBody.OnFloor 是人怪共用事实，AbstractActorEntity 使用本次显示首次 MoveBody 后的真实物理落地；怪物 AI 提交与身体消费攻击都检查落地，空中请求作废。
- **小怪血条**：受伤后按实体场景设置的秒数淡出；治疗更新比例，连续受伤重新计时，暂停停止计时，死亡与复用重置。
- **出手节奏**：`StandAndStrikeState` 进入攻击范围后先按 `MonsterConfig.AttackFirstDelay`（x=最短、y=最长秒）随机停一拍，之后每 `AttackInterval` 按 `AttackDesire`（0-100 出手概率）掷一次。持续交战的期望出手间隔 ≈ `AttackInterval ÷ (AttackDesire/100)`：欲望越高间隔越短，所以间隔不随欲望二次缩放——两个旋钮正交，便于单独调参；首次延迟只是反应时间，与概率无关。
- **动画与判定**：原生 `AnimationLibrary` 的值轨道编辑帧、特效及判定盒几何/启闭；方法轨道以 `AnimEvent` 入口表达出手、声音及阶段时点。挥击、技能和死亡声音调用 `PlaySoundAnimEvent(SoundId)`；成功受击的反馈由 ReceiveHit 按接收实体的 m_DamageSoundId 经 GGF 播放，人怪同源，霸体及零击退也生效，hurt 动画不重复发声。AttackData 与独立效果不保存第二份命中声音。普通状态读取长度按物理秒数计时；技能状态每物理帧手动推进一次原生播放器，不再自建时间线、事件枚举或第二个游标。
- **招式职责**：输入匹配归具体英雄，激活/扣费归 `SkillSystem`，运动、判定威力、阶段分支及打断归对应 GGF 状态；动画节点入口只连接当前状态。方法回调只登记动画切换/结束请求，在播放器推进返回后执行，不在轨道遍历中换动画、销毁状态或回池。死亡收益通知及碰撞清理归状态生命周期，不依赖视觉方法键。
- **退出与复用**：状态退出释放攻击包，应用原生 `RESET` 轨道并恢复普通播放；资源库负责特效默认值，不在角色中硬编码节点路径和空动画名。`HeroModel.ActiveSkillId` 只记录激活编号；独立效果不持有施放包装，延迟回调由所属状态核对原 FSM、当前状态与 `EnterSerial`，取消和复用均不可影响下一次施放。
- **判定几何**：英雄、怪物、BOSS、独立技能效果和普通地形优先用矩形、圆、胶囊等基础形状，以少量形状覆盖主要有效区域。攻击通常一个形状，确有不同区域时可用两个；不按图片 alpha 提取逐像素多边形，不为闪电间隙或美术毛边拆大量碰撞体。排除乌云等纯装饰和外侧大块无攻击区域；按招式阶段调整范围即可，不逐帧追随细小视觉变化。同一用途不重复建立相同判定，身体、受击、感知区按各自物理层分工。
- **地形简化**：地面、平台、墙使用原生矩形等基础形状。平顶带双侧斜坡的土坡保留一个六点简单整体凸坡体，减少形状数量并避免内部侧面和接缝；不按草叶或图片轮廓细分。调整时保留已有空间覆盖、单向平台行为与美术归属，检查走跑、坡面和跳跃。
- **攻击范围**：`AbstractEnemyEntity.Attacks.cs` 集中怪物攻击装配、范围推导、开始与结束。默认 `BuildAttackParams` 从动画有效判定窗口合并少量直接子节点 `CollisionShape2D` 的基础形状边界，包含位置和旋转；该矩形并集只用于 AI 估距，实际命中由原生物理形状判断。特殊招式在具体怪物重写，共享 AbstractActorEntity 不依赖怪物 AI 几何类型。不保留战斗多边形读取和轮廓拟合分支。
- **战斗结算**：`Battle/` 保存属性快照、攻击包、伤害结果和结算公式；不读取场景树或引擎全局状态。随机值由调用方提供。攻击包用 `ReferencePool` 管理，一招对同一目标只结算一次；物理、魔法、真实伤害及击退系数取现有战斗配置。
- **事件边界**：跨模块结果通过 `GF.Event` 发布池化事件参数。事件只携带值，不持有攻击包或实体；订阅者在分发期间读取，不保存事件对象。转发时创建新参数实例。
- **物理层**：代码使用层名，不写位掩码数字：`World`、`PlayerBody`、`EnemyBody`、`Platform`、`PlayerHitBox`、`EnemyHurtBox`、`EnemyHitBox`、`PlayerHurtBox`、`MagicWeapon`、`Trap`、`Item`、`Exit`、`Detector`、`PlayerBarrier`。层序由 `project.godot` 定义。

## 旧项目参考

- `ZMXY_BHYH/Script/Base/BaseHero.gd`、`BaseMonster.gd` 可查角色、怪物的旧玩法和数值表现；旧实现含集中式行为和状态标志，不沿用其代码结构。
- 旧项目判定、掉落和音效数据只作核对依据；新逻辑按当前实体、战斗、配置和事件边界重写。

## 待设计

- 其他 Boss、飞行怪、远程怪及其新增身体/AI 行为；不为尚无差异的身体状态预建空分支。
- 子弹、法宝攻击等新攻击来源的实体与生命周期约定（落位见 [60-GameplayModules.md](60-GameplayModules.md)）。法宝、宠物等跟随物优先做独立实体并记录拥有者实体 ID：GGF `AttachEntity` 的子实体隐藏时不回组容器。现有掉落由 LevelViewController 拥有，通过 NodePool 回收。
- 新角色的具名技能状态与独立效果；复杂技能按实际需要使用原生标记或多个 GGF 阶段，不预建空执行框架。
