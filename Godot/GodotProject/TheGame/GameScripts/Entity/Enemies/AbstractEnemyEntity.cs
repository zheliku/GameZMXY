using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Enemies.Body;
using GameLogic.Enemies.AI;
using GameLogic.Entity;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Battle.Attacks;
using GameLogic.Event.Combat;
using GameLogic.Effects;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Enemy;
using GameConfig.Loot;
using GameConfig.Attributes;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Body;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework.Entity;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 敌方单位公共层：生命、感知、身体与 AI 生命周期；小怪和首领分别提供特殊行为。
    /// </summary>
    public abstract partial class AbstractEnemyEntity : AbstractActorEntity, IEnemyAiAgent, IEnemyBody
    {
        // ---- 字段 ----

        private static readonly EnemyBodyPreset m_DefaultBodyPreset = EnemyBodyPresets.Walk(); // 共享只读配方；状态实例不共享。

        [Export] private EnemyPerceptionNode m_Perception; // 本场景唯一的发现、交战与巡逻空间配置。

        [Export] private EntityId m_EnemyEntityId; // 场景声明实体种类，与基础怪物配置一一对应。

        /// <summary>
        /// 本次显示的击败经验；具体敌人在初始化时应用自身守关规则。
        /// </summary>
        protected int m_ExperienceReward;

        /// <summary>
        /// 小怪必需的头顶生命条；首领可由关卡统一生命条展示。
        /// </summary>
        [Export] protected ResourceBar m_HealthBar;

        [Export(PropertyHint.Range, "0.01,30,0.01,or_greater")] private float m_HealthBarFadeDuration = 2f; // 官方showHpSlip的两秒淡出，暂停冻结。

        private float m_HealthBarTimeLeft; // 最近一次受伤后的剩余淡出秒数，按游戏帧推进。
        private int m_LastHealthBarHp; // 上次生命值，用于区分受伤与治疗，治疗不延长显示。

        /// <summary>
        /// 按框架帧驱动的怪物 AI 状态机（行为类别按自己的状态类型查询）。
        /// </summary>
        protected IFsm<IEnemyAiAgent> m_AiFsm;

        /// <summary>
        /// 按物理帧推进的怪物身体状态机（行为类别按自己的状态类型查询受击/收招硬直）。
        /// </summary>
        protected IFsm<IEnemyBody> m_BodyFsm;

        private AbstractActorEntity m_Target; // 当前索敌目标；只在失效或本次显示结束后清除。

        private Vector2 m_SpawnPosition; // 未绑定关卡视野时使用的巡逻中心。

        private Node2D m_PatrolAnchor; // 关卡注入的取景中心；没有关卡时以出生点巡逻。

        private bool m_RecycleRequested; // 死亡动画结束后等待物理帧回收的标记。

        private int m_Level; // 本次显示的配置等级。
        private int m_Hp; // 本怪物的普通生命状态，不注册 Model 或绑定观察者。
        /// <summary>
        /// 本次显示的生命上限，由具体敌人的初始化规则确定。
        /// </summary>
        protected int m_MaxHp;

        /// <summary>
        /// 本次显示的物理防御，由具体敌人的初始化规则确定。
        /// </summary>
        protected int m_Defense;

        private readonly HashSet<SkillEffectNode> m_SkillEffects = new(); // 本敌人拥有的离身攻击；小怪飞刀与首领技能使用同一生命周期。

        // ---- 属性 ----

        /// <summary>
        /// 本次显示的每球灵魂覆盖值；普通敌人为空，直接使用基础配置。
        /// </summary>
        protected int? SoulPerOrbOverride { get; set; }

        /// <summary>
        /// 本次显示的装备掉落概率覆盖值；普通敌人为空，池复用时清除。
        /// </summary>
        protected float? EquipmentDropChanceOverride { get; set; }

        /// <summary>
        /// 特殊敌人按自身奖励规则选择的装备池；为空时使用基础配置。
        /// </summary>
        protected virtual EquipmentDropPoolId? EquipmentDropPoolIdOverride
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// 默认借用同类敌人的共享身体参数；具体敌人可提供自身专属参数。
        /// </summary>
        protected virtual EnemyBodyParams BodyParams
        {
            get
            {
                return this.GetModel<ConfigurationModel>().GetEnemyBodyParams(m_EnemyEntityId);
            }
        }

        /// <inheritdoc />
        public override int Hp
        {
            get
            {
                return m_Hp;
            }
        }

        /// <inheritdoc />
        public override int MaxHp
        {
            get
            {
                return m_MaxHp;
            }
        }

        /// <inheritdoc />
        public override int Level
        {
            get
            {
                return m_Level;
            }
        }

        /// <inheritdoc />
        protected override EntityId EntityType
        {
            get
            {
                return m_EnemyEntityId;
            }
        }

        /// <summary>
        /// 怪物配置
        /// </summary>
        public EnemyConfig Config
        {
            get
            {
                return this.GetModel<ConfigurationModel>().GetEnemy(m_EnemyEntityId);
            }
        }

        /// <inheritdoc />
        public override CombatSide Side
        {
            get
            {
                return CombatSide.Enemy;
            }
        }

        /// <summary>
        /// 霸体：受击照常扣血飘字，但不硬直、不击退、不打断出招（EnemyConfig.SuperArmor）。
        /// 子类可覆写做条件霸体（Boss 出招期间、低血量……）。
        /// </summary>
        protected virtual bool IsSuperArmor
        {
            get
            {
                return Config is { SuperArmor: true };
            }
        }

        /// <summary>
        /// 本怪物独立拥有的 AI 数据，不与同类池实例共享。
        /// </summary>
        public EnemyAiData AiData { get; private set; }

        /// <summary>
        /// 具体怪物声明共享AI配方；完全由身体机制驱动的固定首领可以不创建AI。
        /// </summary>
        protected abstract EnemyAiPreset AiPreset { get; }

        /// <summary>
        /// 是否允许无目标时主动感知；已有目标的维护与受击锁定不受影响。
        /// </summary>
        protected virtual bool CanAcquireTarget
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc />
        EnemyAiPreset IEnemyAiAgent.AiPreset
        {
            get
            {
                return AiPreset;
            }
        }

        /// <summary>
        /// 共同收招状态中；特殊敌人可改用自己的收招状态。
        /// </summary>
        protected virtual bool InRecovery
        {
            get
            {
                return m_BodyFsm?.CurrentState is EnemyRecoveryState;
            }
        }

        /// <summary>
        /// 受击硬直中（由行为类别提供；AI 的受控判定同源）
        /// </summary>
        protected virtual bool InHurt
        {
            get
            {
                return m_BodyFsm?.CurrentState is EnemyHurtState;
            }
        }

        /// <summary>
        /// 已请求攻击、出招或处于收招硬直。
        /// </summary>
        private bool IsBusy
        {
            get
            {
                return AttackSegment >= 0 || AiData.AttackRequest >= 0 || InRecovery;
            }
        }

        /// <summary>
        /// 当前是否允许 AI 自主行动。
        /// </summary>
        private bool CanAct
        {
            get
            {
                return !Dead && !InHurt && !HasPendingHurt;
            }
        }

        /// <summary>
        /// 死亡动作期间是否保留受击体；普通怪物保留原版鞭尸碰撞，首领由父类关闭。
        /// </summary>
        protected virtual bool KeepHurtBoxDuringDeath
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 本形态死亡是否结束配方；只有实际需要后继形态的具体敌人覆写。
        /// </summary>
        protected virtual bool EncounterComplete
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 默认行走配方；具体敌人按实际动作组合替换，不由Monster类别决定身体能力。
        /// </summary>
        protected virtual EnemyBodyPreset BodyPreset
        {
            get
            {
                return m_DefaultBodyPreset;
            }
        }

        // ---- 接口实现 ----

        /// <summary>
        /// 向身体状态机提供怪物共享身体参数。
        /// </summary>
        EnemyBodyParams IEnemyBody.Params
        {
            get
            {
                return BodyParams;
            }
        }

        /// <summary>
        /// 受击和收招后使用与首次显示相同的行动入口。
        /// </summary>
        Type IEnemyBody.MovementStateType
        {
            get
            {
                return BodyPreset.MovementStateType;
            }
        }

        /// <inheritdoc />
        Type IEnemyBody.DeathStateType
        {
            get
            {
                return BodyPreset.DeathStateType;
            }
        }

        /// <summary>
        /// 转发 AI 写入的移动意图读写。
        /// </summary>
        int IEnemyBody.MoveIntent
        {
            get
            {
                return AiData.MoveIntent;
            }

            set
            {
                AiData.MoveIntent = value;
            }
        }

        /// <summary>
        /// 提供怪物身体状态使用的重力参数。
        /// </summary>
        float IActorBody.Gravity
        {
            get
            {
                return BodyParams.Gravity;
            }
        }

        /// <summary>
        /// 向 AI 提供实体死亡事实。
        /// </summary>
        bool IEnemyAiAgent.IsDead
        {
            get
            {
                return Dead;
            }
        }

        /// <summary>
        /// 向 AI 提供当前受击受控事实。
        /// </summary>
        bool IEnemyAiAgent.IsCcLocked
        {
            get
            {
                return InHurt;
            }
        }

        /// <summary>
        /// 向 AI 提供攻击或收招忙碌事实。
        /// </summary>
        bool IEnemyAiAgent.IsAttacking
        {
            get
            {
                return IsBusy;
            }
        }

        /// <summary>
        /// 目标是否仍存活；受击盒尺寸不参与AI原点距离判断。
        /// </summary>
        bool IEnemyAiAgent.HasTarget
        {
            get
            {
                return m_Target is { IsAlive: true };
            }
        }

        /// <inheritdoc />
        Vector2 IEnemyAiAgent.PatrolBounds
        {
            get
            {
                return m_Perception.PatrolBounds;
            }
        }

        /// <inheritdoc />
        Rect2 IEnemyAiAgent.EngageBounds
        {
            get
            {
                return m_Perception.EngageBounds;
            }
        }

        /// <inheritdoc />
        bool IEnemyAiAgent.IsInEngageRange
        {
            get
            {
                return m_Target is { IsAlive: true } &&
                    m_Perception.ContainsEngage(((IEnemyAiAgent)this).TargetOffset);
            }
        }

        /// <summary>
        /// 提供怪物相对出生点的水平偏移。
        /// </summary>
        float IEnemyAiAgent.PatrolOffsetX
        {
            get
            {
                return GlobalPosition.X -
            (IsInstanceValid(m_PatrolAnchor) ? m_PatrolAnchor.GlobalPosition.X : m_SpawnPosition.X);
            }
        }

        /// <summary>
        /// 向 AI 提供怪物共享定义。
        /// </summary>
        EnemyConfig IEnemyAiAgent.Config
        {
            get
            {
                return Config;
            }
        }

        /// <summary>
        /// 目标原点位移，不受受击盒美术偏移影响。
        /// </summary>
        Vector2 IEnemyAiAgent.TargetOffset
        {
            get
            {
                return m_Target is { IsAlive: true }
            ? m_Target.GlobalPosition - GlobalPosition : Vector2.Zero;
            }
        }

        // ---- 生命周期 ----

        /// <summary>
        /// 读取基础实体信息与怪物配置，并初始化攻击和身体参数。
        /// </summary>
        /// <param name="entityId">本次运行实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroup">所属实体组。</param>
        /// <param name="isNewInstance">是否首次创建池实例。</param>
        /// <param name="userData">本次初始化参数。</param>
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance,
            object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

            if (!isNewInstance)
            {
                return;
            }

            this.GetSystem<ConfigurationSystem>().EnsureEnemyBodyParams(m_EnemyEntityId, BuildBodyParams);
            AiData = new EnemyAiData(OwnAttacks.Count);
            m_Perception.Validate();
        }

        /// <summary>
        /// 显示时复位池化状态并重建身体与 AI 状态机。
        /// </summary>
        /// <param name="userData">本次显示参数，由具体实体解释。</param>
        public override void OnShow(object userData)
        {
            base.OnShow(userData);
            if (userData is not EnemySpawn spawn)
            {
                throw new ArgumentException("怪物显示参数必须包含出生位置与本次会话回调。", nameof(userData));
            }

            // 从基础配置重置，再由具体敌人补充自身规则；不改写共享表或继承上次显示的值。
            ApplyConfigOverrides();
            m_Level = Config.Level;
            m_Hp = MaxHp;
            AttackSegment = -1;
            m_PendingHurt = null;
            m_RecycleRequested = false;
            SetTarget(null);
            Velocity = Vector2.Zero;
            this.GetSystem<EnemyAiSystem>().Reset(AiData, OwnAttacks, GD.Randf);

            GlobalPosition = spawn.Position;

            m_SpawnPosition = GlobalPosition;
            m_PatrolAnchor = null;
            SetHurtBoxEnabled(true);
            SetFacing(-1);

            CreateBodyStateMachine();
            CreateAiStateMachine();

            // 战斗状态全部复位后刷新被动血条，避免池复用时展示装配的中间值。
            if (m_HealthBar != null)
            {
                m_HealthBarTimeLeft = 0f;
                m_LastHealthBarHp = Hp;
                m_HealthBar.Modulate = Colors.White;
                RefreshHealthBar();
            }
        }

        /// <summary>
        /// 每次入场应用基础配置及实例覆盖；子类可覆写本实例数值，不改写共享配置表。
        /// </summary>
        protected virtual void ApplyConfigOverrides()
        {
            m_MaxHp = Config.BaseAttributes.MaxHp;
            m_Defense = Config.BaseAttributes.Def;
            m_ExperienceReward = Config.AddExp;
            SoulPerOrbOverride = null;
            EquipmentDropChanceOverride = null;
        }

        /// <summary>
        /// 隐藏时销毁状态机、清空目标并执行基类清理。
        /// </summary>
        /// <param name="isShutdown">是否框架关停；此时节点可能已释放。</param>
        /// <param name="userData">本次隐藏参数。</param>
        public override void OnHide(bool isShutdown, object userData)
        {
            m_PatrolAnchor = null;
            // 身体和独立攻击均由同一个敌人拥有；隐藏后不遗留飞刀、风刃或毒雾。
            if (!isShutdown)
            {
                foreach (var effect in m_SkillEffects.ToArray())
                {
                    ReleaseSkillEffect(effect);
                }
            }

            m_SkillEffects.Clear();
            // 关停时子节点可能已释放，不再访问表现对象。
            if (!isShutdown && IsInstanceValid(m_HealthBar))
            {
                m_HealthBar.Visible = false;
            }

            DestroyAiStateMachine(isShutdown);
            DestroyBodyStateMachine(isShutdown);
            SetTarget(null);
            base.OnHide(isShutdown, userData);
        }

        /// <summary>
        /// AI 计时与感知跟随框架帧；身体状态机和物理按固定物理帧推进。
        /// </summary>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        /// <param name="realElapseSeconds">本帧实际秒数。</param>
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            if (!IsShown || Config == null)
            {
                return;
            }

            if (!Dead)
            {
                this.GetSystem<EnemyAiSystem>().AdvanceCooldowns(AiData, elapseSeconds);
                UpdateTarget();
            }
            UpdateHealthBarFade(elapseSeconds);
        }

        /// <summary>
        /// 物理步长：身体状态机 → 物理 →（死亡时长走完）回收；本帧消费 AI 意图。
        /// </summary>
        /// <param name="delta">本次物理帧秒数。</param>
        public override void _PhysicsProcess(double delta)
        {
            if (!IsShown || Config == null || delta <= 0)
            {
                return;
            }

            float dt = (float)delta;
            m_BodyFsm.Tick(dt);
            MoveBody();
            PollAttackOverlaps();

            if (m_RecycleRequested)
            {
                m_RecycleRequested = false;
                GF.Entity.HideEntitySafe(this);
            }
        }

        /// <inheritdoc />
        protected override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || m_Hp <= 0)
            {
                return false;
            }

            m_Hp = Math.Max(0, m_Hp - amount);
            RefreshHealthBar();
            return m_Hp == 0;
        }

        /// <inheritdoc />
        protected override void RestoreHealth(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            m_Hp = (int)Math.Min(MaxHp, (long)m_Hp + amount);
            RefreshHealthBar();
        }

        /// <inheritdoc />
        protected override CombatantAttributes CaptureCombatAttributes()
        {
            var attributes = Config.BaseAttributes;
            return new CombatantAttributes(Side, Level, attributes.Power, m_Defense, attributes.Mdef,
                attributes.Crit, attributes.Miss, attributes.Lucky, attributes.Toughness, attributes.Htarget,
                attributes.CritReduce, attributes.Ar, attributes.Sp, Id);
        }

        // ---- 内部方法与扩展点 ----

        /// <summary>
        /// 生命变化时重新读取当前值与上限，刷新被动血条。
        /// </summary>
        private void RefreshHealthBar()
        {
            if (!IsShown || m_HealthBar == null)
            {
                return;
            }

            if (Hp < m_LastHealthBarHp)
            {
                m_HealthBarTimeLeft = m_HealthBarFadeDuration;
            }

            m_LastHealthBarHp = Hp;
            // 扣血先刷新血条，再由受击入口确定死亡事实。
            m_HealthBar.SetValue(Hp, MaxHp);
            m_HealthBar.Visible = m_HealthBarTimeLeft > 0f && Hp > 0 && Hp < MaxHp;
            UpdateHealthBarFade(0f);
        }

        /// <summary>
        /// 按官方两秒淡出行为隐藏小血条；继续受伤会从不透明重新计时。
        /// </summary>
        /// <param name="elapseSeconds">本帧游戏秒数；暂停时为零。</param>
        private void UpdateHealthBarFade(float elapseSeconds)
        {
            if (m_HealthBar is not { Visible: true })
            {
                return;
            }

            m_HealthBarTimeLeft = Mathf.Max(0f, m_HealthBarTimeLeft - elapseSeconds);
            float remaining = m_HealthBarTimeLeft / m_HealthBarFadeDuration;
            // TweenMax 默认 Quad.easeOut 的 alpha 从 1 到 0，等价于剩余比例的平方。
            m_HealthBar.Modulate = new Color(1f, 1f, 1f, remaining * remaining);
            if (m_HealthBarTimeLeft <= 0f)
            {
                m_HealthBar.Visible = false;
            }
        }

        /// <summary>
        /// 受击：记击杀者、锁定攻击方；非霸体、招式有击退且动画库有 hurt 动画（硬直时长无从谈起就不登记）时
        /// 登记受击，身体状态机下一物理帧打断出招进入硬直（旧 BaseMonster state_hurt：击退 [0,0] 的招式不硬直）。
        /// 死亡由 ReceiveHit 置位 Dead，身体状态机进入死亡。
        /// </summary>
        /// <param name="data">本次攻击快照，不得保留池化引用。</param>
        /// <param name="result">已完成的伤害结算结果。</param>
        /// <param name="knockback">击退速度，单位像素每秒。</param>
        /// <param name="attackerEntityId">攻击方运行编号；无来源时为零。</param>
        protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
        {
            if (Dead)
            {
                return;
            }

            // 被打即锁定攻击方（仅当它是有效的角色实体）
            if (attackerEntityId != Id &&
                GF.Entity.GetEntity(attackerEntityId) is AbstractActorEntity { IsAlive: true } attacker)
            {
                SetTarget(attacker);
            }

            if (IsSuperArmor || knockback == Vector2.Zero || BodyParams.HurtTime <= 0f)
            {
                return;
            }

            // 受击事实立即撤销待出招与结算包，避免同一物理步里的碰撞回调产生反击；状态迁移仍由身体 Tick 负责。
            AiData.AttackRequest = -1;
            ReleaseAttack();
            m_PendingHurt = knockback;
        }

        /// <summary>
        /// 在实体显示和死亡状态边界切换受击盒可监测性。
        /// </summary>
        /// <param name="enabled">是否允许受击盒被检测。</param>
        protected void SetHurtBoxEnabled(bool enabled)
        {
            if (HurtBox != null)
            {
                HurtBox.Monitorable = enabled;
            }
        }

        /// <summary>
        /// 构建身体状态机所需的配置与动画时长定义（扩展点：飞行/远程怪在子类替换参数）。
        /// </summary>
        /// <returns>身体状态机使用的共享定义。</returns>
        protected virtual EnemyBodyParams BuildBodyParams()
        {
            // 读取每段攻击动画和收招硬直，供状态机按物理时间推进。
            string[] anims = new string[OwnAttacks.Count];
            float[] times = new float[OwnAttacks.Count];
            float[] recovery = new float[OwnAttacks.Count];
            for (int i = 0; i < OwnAttacks.Count; i++)
            {
                anims[i] = AttackAnimations[i];
                times[i] = GetActionDuration(anims[i]);
                recovery[i] = Mathf.Max(0f, OwnAttacks[i].AiRecovery);
            }

            return new EnemyBodyParams
            {
                MoveSpeed = Config.MoveSpeed,
                Gravity = Config.Gravity,
                // 兽形、固定心脏等源角色没有受击动作，不把合法缺省当资源丢失告警。
                HurtTime = HasAnim(EnemyAnims.Hurt) ? GetActionDuration(EnemyAnims.Hurt) : 0f,
                KnockbackRetentionPerSecond = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data.EnemyKnockbackRetentionPerSecond,
                DeathTime = GetActionDuration(EnemyAnims.Death),
                DeathAirLoopTime = AnimPlayer.GetAnimation(EnemyAnims.Death).HasMarker(EnemyAnims.DeathAirLoopMarker)
                    ? (float)AnimPlayer.GetAnimation(EnemyAnims.Death).GetMarkerTime(EnemyAnims.DeathAirLoopMarker)
                    : null,
                AttackAnims = Array.AsReadOnly(anims),
                AttackTimes = Array.AsReadOnly(times),
                AttackRecovery = Array.AsReadOnly(recovery),
            };
        }

        /// <summary>
        /// 让巡逻以当前取景为中心，确保场外出生的怪物会走进战斗视野。
        /// </summary>
        /// <param name="anchor">关卡会话注入的相机中心。</param>
        /// <exception cref="ArgumentNullException">中心节点为空。</exception>
        public void SetPatrolAnchor(Node2D anchor)
        {
            m_PatrolAnchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
        }

        /// <summary>
        /// 保留存活目标；失效后才从场景索敌区选择新目标，不存在另一份视野时限。
        /// </summary>
        private void UpdateTarget()
        {
            if (m_Target is { IsAlive: true })
            {
                return;
            }

            SetTarget(null);
            if (Dead || !CanAcquireTarget)
            {
                return;
            }

            foreach (Node2D body in m_Perception.GetOverlappingBodies())
            {
                if (body is AbstractActorEntity actor && CanSelectTarget(actor))
                {
                    SetTarget(actor);
                    Log.Debug("[Enemy] {0} 发现目标 {1}", Id, actor.Id);
                    return;
                }
            }
        }

        /// <summary>
        /// 按场景首次索敌范围筛选存活敌方角色，横界严格、纵界包含。
        /// </summary>
        /// <param name="actor">索敌区报告的候选角色。</param>
        /// <returns>是否允许将该角色设为新目标。</returns>
        private bool CanSelectTarget(AbstractActorEntity actor)
        {
            if (actor == this || !actor.IsAlive || actor.Side == Side)
            {
                return false;
            }

            Vector2 offset = m_Perception.ToLocal(actor.GlobalPosition);
            Rect2 bounds = m_Perception.DetectionBounds;
            return offset.X > bounds.Position.X && offset.X < bounds.End.X &&
                offset.Y >= bounds.Position.Y && offset.Y <= bounds.End.Y;
        }

        /// <summary>
        /// 设置当前目标并清零视野外计时。
        /// </summary>
        /// <param name="target">本次目标；空值清除索敌。</param>
        private void SetTarget(AbstractActorEntity target)
        {
            m_Target = target;
        }

        /// <summary>
        /// 创建并启动怪物身体状态机；已有状态机时保持复用。
        /// </summary>
        private void CreateBodyStateMachine()
        {
            if (Config == null || m_BodyFsm != null)
            {
                return;
            }

            m_BodyFsm = GF.Fsm.CreateFsm<IEnemyBody>($"EnemyBody_{Id}", this, BodyPreset.CreateStates());
            m_BodyFsm.Start(BodyPreset.MovementStateType);
        }

        /// <summary>
        /// 销毁身体状态机，关停阶段仅清除本地引用。
        /// </summary>
        /// <param name="isShutdown">是否框架关停，关停时不访问状态机服务。</param>
        private void DestroyBodyStateMachine(bool isShutdown)
        {
            if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
            {
                GF.Fsm.DestroyFsm(m_BodyFsm);
            }

            m_BodyFsm = null;
        }

        /// <summary>
        /// 创建并启动怪物 AI 状态机。
        /// </summary>
        private void CreateAiStateMachine()
        {
            if (m_AiFsm != null || Config == null || !IsShown || AiPreset == null)
            {
                return;
            }

            m_AiFsm = GF.Fsm.CreateFsm($"EnemyAI_{Id}", this, AiPreset.CreateStates());
            m_AiFsm.Start(AiPreset.IdleStateType);
        }

        /// <summary>
        /// 销毁 AI 状态机，关停阶段仅清除本地引用。
        /// </summary>
        /// <param name="isShutdown">是否框架关停，关停时不访问状态机服务。</param>
        private void DestroyAiStateMachine(bool isShutdown)
        {
            if (m_AiFsm != null && !isShutdown && !m_AiFsm.IsDestroyed)
            {
                GF.Fsm.DestroyFsm(m_AiFsm);
            }

            m_AiFsm = null;
        }

        /// <summary>
        /// 取出并清除待提交的攻击请求。
        /// </summary>
        /// <returns>待提交下标；没有请求时为 -1。</returns>
        int IEnemyBody.TakeAttackRequest()
        {
            int request = AiData.AttackRequest;
            AiData.AttackRequest = -1;
            return request;
        }

        /// <summary>
        /// 提交怪物招式并装填攻击包。
        /// </summary>
        /// <param name="index">要提交的怪物攻击下标。</param>
        void IActorBody.BeginAttack(int index)
        {
            BeginConfiguredAttack(index);
        }

        /// <summary>
        /// 结束怪物招式并归还攻击包。
        /// </summary>
        void IActorBody.EndAttack()
        {
            EndCurrentAttack();
        }

        /// <summary>
        /// 死亡状态进入时按敌人类别处理受击盒并广播一次击败事件；声音由动画负责。
        /// </summary>
        void IEnemyBody.BeginDeath()
        {
            // AI生命周期结束；身体死亡状态继续负责尸体、奖励与回收。
            AiData.AttackRequest = -1;
            AiData.MoveIntent = 0;
            SetTarget(null);
            DestroyAiStateMachine(false);
            SetHurtBoxEnabled(KeepHurtBoxDuringDeath);
            GF.Event.Fire(this,
                EnemyDiedEventArgs.Create(Id, m_ExperienceReward, Config, HurtBox.GetGlobalBounds().GetCenter(),
                    SoulPerOrbOverride, EquipmentDropChanceOverride, EncounterComplete, EquipmentDropPoolIdOverride));
        }

        /// <summary>
        /// 请求在当前物理帧完成后安全回收实体。
        /// </summary>
        void IEnemyBody.RequestRecycle()
        {
            m_RecycleRequested = true;
        }

        /// <summary>
        /// 由 Godot 随机源提供 AI 随机值。
        /// </summary>
        /// <returns>零到一之间的随机值。</returns>
        float IEnemyAiAgent.NextRandom()
        {
            return GD.Randf();
        }

        /// <summary>
        /// 设置并限制怪物的水平移动意图。
        /// </summary>
        /// <param name="dir">水平意图：负值左，正值右，零停。</param>
        void IEnemyAiAgent.Move(int dir)
        {
            AiData.MoveIntent = Mathf.Clamp(dir, -1, 1);
        }

        /// <summary>
        /// 仅在可行动且未忙碌时按目标方向调整朝向。
        /// </summary>
        /// <param name="dir">期望朝向：正值右，负值左。</param>
        void IEnemyAiAgent.Face(int dir)
        {
            if (CanAct && !IsBusy)
            {
                SetFacing(dir);
            }
        }

        /// <summary>
        /// 校验攻击请求并登记供身体状态机下一物理帧消费。
        /// </summary>
        /// <param name="index">希望提交的攻击配置下标。</param>
        /// <returns>下标和当前状态是否允许该请求。</returns>
        bool IEnemyAiAgent.RequestAttack(int index)
        {
            return TryRequestAttack(index);
        }

        /// <summary>
        /// 校验并登记攻击请求；Boss 可覆写特殊出招条件，身体状态机统一提交。
        /// </summary>
        /// <param name="index">攻击配置下标。</param>
        /// <returns>当前状态是否接受请求。</returns>
        protected virtual bool TryRequestAttack(int index)
        {
            if (index < 0 || index >= OwnAttacks.Count || IsBusy || !CanAct)
            {
                return false;
            }

            AiData.AttackRequest = index;
            AiData.MoveIntent = 0;
            this.GetSystem<EnemyAiSystem>().MarkUsed(AiData, OwnAttacks, index, GD.Randf);
            return true;
        }
    }

    /// <summary>
    /// 一次怪物显示的位置与会话回调；特殊属性和奖励由具体敌人初始化，不由生成配方解释。
    /// </summary>
    /// <param name="Position">世界出生位置。</param>
    /// <param name="SpawnFailed">子实体异步显示失败时通知本次会话；独立场地允许为空。</param>
    /// <param name="ContinueEncounterAsync">交接到后继形态，沿用本次配方名额及会话所有权；普通怪物不使用。</param>
    public readonly record struct EnemySpawn(Vector2 Position,
        Action<Exception> SpawnFailed = null,
        Func<int, EntityId, EnemySpawn, Task> ContinueEncounterAsync = null);
}
