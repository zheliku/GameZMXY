using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Monsters.Body;
using GameLogic.Monsters.AI;
using GameLogic.Entity;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Battle.Attacks;
using GameLogic.Event.Combat;
using System.Linq;
using System;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Monster;
using GameConfig.Attributes;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Body;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework.Entity;
using GodotGameFramework;

namespace GameLogic.Monsters
{
    /// <summary>持有怪物配置、感知及身体和 AI 状态机，具体行为由直接派生的怪物声明。</summary>
    public abstract partial class AbstractMonsterEntity : AbstractActorEntity, IMonsterAiAgent, IMonsterBody
    {
        // ---- 字段 ----

        [Export] private Area2D m_Detector; // 索敌区；为空时只会在受击后反击。

        [Export] private EntityId m_MonsterEntityId; // 场景声明实体种类，与基础怪物配置一一对应。

        private float m_AttackMultiplier = 1f; // 本次显示的招式威力倍率，每次从刷怪参数重置。

        private int m_ExperienceReward; // 本次显示的击败经验；配方可覆盖基础配置奖励。

        [Export] private ResourceBar m_HealthBar; // 可选头顶小血条，作为实体子场景随 GGF 实体池复用。

        [Export(PropertyHint.Range, "0.01,30,0.01,or_greater")] private float m_HealthBarFadeDuration = 4f; // 受伤血条淡出的游戏秒数。

        private float m_HealthBarTimeLeft; // 最近一次受伤后的剩余淡出秒数，按游戏帧推进。
        private int m_LastHealthBarHp; // 上次生命值，用于区分受伤与治疗，治疗不延长显示。

        [Export] private Vector2 m_HealthBarOffset = new(0f, -8f); // 血条底边中心相对受击盒顶边中心的像素偏移。

        /// <summary>按框架帧驱动的怪物 AI 状态机（行为类别按自己的状态类型查询）。</summary>
        protected IFsm<IMonsterAiAgent> m_AiFsm;

        /// <summary>按物理帧推进的怪物身体状态机（行为类别按自己的状态类型查询受击/收招硬直）。</summary>
        protected IFsm<IMonsterBody> m_BodyFsm;

        private AbstractActorEntity m_Target; // 当前索敌目标；失效或超时后清除。

        private Vector2 m_SpawnPosition; // 未绑定关卡视野时使用的巡逻中心。

        private Node2D m_PatrolAnchor; // 关卡注入的取景中心；没有关卡时以出生点巡逻。

        private bool m_RecycleRequested; // 死亡动画结束后等待物理帧回收的标记。

        private float m_OutOfSightTime; // 目标持续在视野外的秒数。

        private int m_Level; // 本次显示的配置等级。
        private int m_Hp; // 本怪物的普通生命状态，不注册 Model 或绑定观察者。
        private int m_MaxHp; // 配方生命倍率应用后的上限。
        private int m_Defense; // 配方防御倍率应用后的物防。

        /// <summary>借用模型中同类怪物共享的身体参数。</summary>
        private MonsterBodyParams BodyParams => this.GetModel<ConfigurationModel>().GetMonsterBodyParams(m_MonsterEntityId);

        // ---- 属性 ----

        /// <inheritdoc />
        public override int Hp => m_Hp;
        /// <inheritdoc />
        public override int MaxHp => m_MaxHp;
        /// <inheritdoc />
        public override int Level => m_Level;

        /// <inheritdoc />
        protected override EntityId EntityType => m_MonsterEntityId;

        /// <summary>怪物配置</summary>
        public MonsterConfig Config => this.GetModel<ConfigurationModel>().GetMonster(m_MonsterEntityId);

        /// <inheritdoc />
        public override CombatSide Side => CombatSide.Monster;

        /// <summary>
        /// 霸体：受击照常扣血飘字，但不硬直、不击退、不打断出招（MonsterConfig.SuperArmor）。
        /// 子类可覆写做条件霸体（Boss 出招期间、低血量……）。
        /// </summary>
        protected virtual bool IsSuperArmor => Config is { SuperArmor: true };

        /// <summary>本怪物独立拥有的 AI 数据，不与同类池实例共享。</summary>
        public MonsterAiData AiData { get; private set; }

        /// <summary>具体怪物声明共享状态配方；基类不预设具体行为。</summary>
        protected abstract MonsterAiPreset AiPreset { get; }

        /// <inheritdoc />
        MonsterAiPreset IMonsterAiAgent.AiPreset => AiPreset;

        /// <summary>收招硬直中，由具体怪物提供，AI 据此拒绝新攻击请求。</summary>
        protected abstract bool InRecovery { get; }

        /// <summary>受击硬直中（由行为类别提供；AI 的受控判定同源）</summary>
        protected abstract bool InHurt { get; }

        /// <summary>已请求攻击、出招或处于收招硬直。</summary>
        private bool IsBusy => AttackSegment >= 0 || AiData.AttackRequest >= 0 || InRecovery;

        /// <summary>当前是否允许 AI 自主行动。</summary>
        private bool CanAct => !Dead && !InHurt && !HasPendingHurt;

        /// <summary>目标受击盒相对本实体原点的范围。</summary>
        private Rect2 TargetBox
        {
            get
            {
                if (m_Target is not { IsAlive: true } target)
                {
                    return default;
                }

                Vector2 o = GlobalPosition;
                if (target.HurtBox == null)
                {
                    return new Rect2(target.GlobalPosition - o - Vector2.One * 0.5f, Vector2.One);
                }

                Rect2 r = target.HurtBox.GetGlobalBounds();
                return new Rect2(r.Position - o, r.Size);
            }
        }

        /// <summary>身体初始状态类型（**行为类别必须给出**）。</summary>
        protected abstract Type InitialBodyStateType { get; }

        /// <summary>AI 初始状态；默认由预设选择，Boss 可覆写。</summary>
        protected virtual Type InitialAiStateType => AiPreset.IdleStateType;

        // ---- 接口实现 ----

        /// <summary>向身体状态机提供怪物共享身体参数。</summary>
        MonsterBodyParams IMonsterBody.Params => BodyParams;

        /// <summary>向 AI 提供同一份共享身体及攻击参数。</summary>
        MonsterBodyParams IMonsterAiAgent.Params => BodyParams;

        /// <summary>转发 AI 写入的移动意图读写。</summary>
        int IMonsterBody.MoveIntent
        {
            get => AiData.MoveIntent;
            set => AiData.MoveIntent = value;
        }

        /// <summary>提供怪物身体状态使用的重力参数。</summary>
        float IActorBody.Gravity => BodyParams.Gravity;

        /// <summary>向 AI 提供实体死亡事实。</summary>
        bool IMonsterAiAgent.IsDead => Dead;

        /// <summary>向 AI 提供当前受击受控事实。</summary>
        bool IMonsterAiAgent.IsCcLocked => InHurt;

        /// <summary>向 AI 提供攻击或收招忙碌事实。</summary>
        bool IMonsterAiAgent.IsAttacking => IsBusy;

        /// <summary>提供目标受击盒相对怪物原点的范围。</summary>
        Rect2 IMonsterAiAgent.TargetBox => TargetBox;

        /// <summary>提供怪物相对出生点的水平偏移。</summary>
        float IMonsterAiAgent.PatrolOffsetX => GlobalPosition.X -
            (IsInstanceValid(m_PatrolAnchor) ? m_PatrolAnchor.GlobalPosition.X : m_SpawnPosition.X);

        /// <summary>向 AI 提供怪物共享定义。</summary>
        MonsterConfig IMonsterAiAgent.Config => Config;

        // ---- 生命周期 ----

        /// <summary>读取基础实体信息与怪物配置，并初始化攻击和身体参数。</summary>
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
                return;
            this.GetSystem<ConfigurationSystem>().EnsureMonsterBodyParams(m_MonsterEntityId, BuildBodyParams);
            AiData = new MonsterAiData(BodyParams.Attacks.Count);
            ConfigureDetector();
        }

        /// <summary>显示时复位池化状态并重建身体与 AI 状态机。</summary>
        /// <param name="userData">本次显示参数，由具体实体解释。</param>
        public override void OnShow(object userData)
        {
            base.OnShow(userData);
            if (userData is not MonsterSpawn spawn)
                throw new ArgumentException("怪物显示参数必须包含出生位置与基础属性倍率。", nameof(userData));

            // 每次从基础配置装配倍率，随后补满生命；不修改共享表，也不沿用上次显示的修正。
            // 怪物不建立通用属性容器，出生时应用本配方需要的基础倍率。
            m_MaxHp = Math.Max(1, (int)MathF.Round(Config.BaseAttributes.MaxHp * spawn.HealthMultiplier, MidpointRounding.AwayFromZero));
            m_Defense = Math.Max(0, (int)MathF.Round(Config.BaseAttributes.Def * spawn.DefenseMultiplier, MidpointRounding.AwayFromZero));
            m_AttackMultiplier = spawn.AttackMultiplier;
            m_ExperienceReward = spawn.ExperienceOverride < 0 ? Config.AddExp : spawn.ExperienceOverride;
            m_Level = Config.Level;
            m_Hp = MaxHp;
            AttackSegment = -1;
            m_PendingHurt = null;
            m_RecycleRequested = false;
            SetTarget(null);
            Velocity = Vector2.Zero;
            this.GetSystem<MonsterAiSystem>().Reset(AiData, BodyParams, GD.Randf);

            GlobalPosition = spawn.Position;

            m_SpawnPosition = GlobalPosition;
            m_PatrolAnchor = null;
            SetHurtBoxEnabled(true);
            SetFacing(-1);

            CreateBodyStateMachine();
            CreateAiStateMachine();

            // 战斗状态全部复位后再订阅，避免池复用时展示装配的中间值。
            if (m_HealthBar != null)
            {
                m_HealthBarTimeLeft = 0f;
                m_LastHealthBarHp = Hp;
                m_HealthBar.Modulate = Colors.White;


                RefreshHealthBar();
            }
        }

        /// <summary>隐藏时销毁状态机、清空目标并执行基类清理。</summary>
        /// <param name="isShutdown">是否框架关停；此时节点可能已释放。</param>
        /// <param name="userData">本次隐藏参数。</param>
        public override void OnHide(bool isShutdown, object userData)
        {
            m_PatrolAnchor = null;
            // 先解除纯 C# 订阅；关停时子节点可能已释放，不再访问表现对象。


            if (!isShutdown && IsInstanceValid(m_HealthBar))
            {
                m_HealthBar.Visible = false;
            }

            DestroyAiStateMachine(isShutdown);
            DestroyBodyStateMachine(isShutdown);
            SetTarget(null);
            base.OnHide(isShutdown, userData);
        }

        /// <summary>AI 计时与感知跟随框架帧；身体状态机和物理按固定物理帧推进。</summary>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        /// <param name="realElapseSeconds">本帧实际秒数。</param>
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            if (!IsShown || Config == null)
            {
                return;
            }

            this.GetSystem<MonsterAiSystem>().AdvanceCooldowns(AiData, elapseSeconds);
            UpdateTarget(elapseSeconds);
            UpdateHealthBarFade(elapseSeconds);
        }

        /// <summary>物理步长：身体状态机 → 物理 →（死亡时长走完）回收；本帧消费 AI 意图。</summary>
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
            if (m_HealthBar is { Visible: true })
            {
                UpdateHealthBarPosition();
            }

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
                return false;
            m_Hp = Math.Max(0, m_Hp - amount);
            RefreshHealthBar();
            return m_Hp == 0;
        }

        /// <inheritdoc />
        protected override void RestoreHealth(int amount)
        {
            if (amount <= 0)
                return;
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

        /// <summary>生命变化时重新读取当前值与上限，刷新被动血条。</summary>
        private void RefreshHealthBar()
        {
            if (!IsShown || m_HealthBar == null)
                return;
            if (Hp < m_LastHealthBarHp)
                m_HealthBarTimeLeft = m_HealthBarFadeDuration;
            m_LastHealthBarHp = Hp;
            // 扣血先刷新血条，再由受击入口确定死亡事实。
            m_HealthBar.SetValue(Hp, MaxHp);
            m_HealthBar.Visible = m_HealthBarTimeLeft > 0f && Hp > 0 && Hp < MaxHp;
            UpdateHealthBarFade(0f);
            UpdateHealthBarPosition();
        }

        /// <summary>按官方两秒淡出行为隐藏小血条；继续受伤会从不透明重新计时。</summary>
        /// <param name="elapseSeconds">本帧游戏秒数；暂停时为零。</param>
        private void UpdateHealthBarFade(float elapseSeconds)
        {
            if (m_HealthBar is not { Visible: true })
                return;
            m_HealthBarTimeLeft = Mathf.Max(0f, m_HealthBarTimeLeft - elapseSeconds);
            float remaining = m_HealthBarTimeLeft / m_HealthBarFadeDuration;
            // TweenMax 默认 Quad.easeOut 的 alpha 从 1 到 0，等价于剩余比例的平方。
            m_HealthBar.Modulate = new Color(1f, 1f, 1f, remaining * remaining);
            if (m_HealthBarTimeLeft <= 0f)
                m_HealthBar.Visible = false;
        }

        /// <summary>按受击盒头顶定位血条，独立于身体和攻击盒的朝向镜像。</summary>
        private void UpdateHealthBarPosition()
        {
            m_HealthBar.GlobalPosition = HeadPosition + m_HealthBarOffset -
                new Vector2(m_HealthBar.Size.X * 0.5f, m_HealthBar.Size.Y);
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

        /// <summary>延迟切换受击盒可监测性，允许在物理回调中安全调用。</summary>
        /// <param name="enabled">是否允许受击盒被检测。</param>
        private void SetHurtBoxEnabled(bool enabled)
        {
            // deferred：可能在物理回调内调用
            HurtBox?.SetDeferred(Area2D.PropertyName.Monitorable, enabled);
        }

        /// <summary>构建身体状态机所需的配置与动画时长定义（扩展点：飞行/远程怪在子类替换参数）。</summary>
        /// <returns>身体状态机使用的共享定义。</returns>
        protected virtual MonsterBodyParams BuildBodyParams()
        {
            // 读取每段攻击动画和收招硬直，供状态机按物理时间推进。
            string[] anims = new string[OwnAttacks.Count];
            float[] times = new float[OwnAttacks.Count];
            float[] recovery = new float[OwnAttacks.Count];
            var attacks = new MonsterAttackParams[OwnAttacks.Count];
            for (int i = 0; i < OwnAttacks.Count; i++)
            {
                attacks[i] = BuildAttackParams(i, OwnAttacks[i]);
                anims[i] = AttackAnimations[i];
                times[i] = GetAnimLength(anims[i]);
                recovery[i] = Mathf.Max(0f, OwnAttacks[i].AiRecovery);
            }

            return new MonsterBodyParams
            {
                MoveSpeed = Config.MoveSpeed,
                Gravity = Config.Gravity,
                HurtTime = GetAnimLength(MonsterAnims.Hurt),
                KnockbackRetentionPerSecond = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data.MonsterKnockbackRetentionPerSecond,
                DeathTime = GetAnimLength(MonsterAnims.Death),
                AttackAnims = Array.AsReadOnly(anims),
                AttackTimes = Array.AsReadOnly(times),
                AttackRecovery = Array.AsReadOnly(recovery),
                Attacks = Array.AsReadOnly(attacks),
            };
        }

        /// <summary>让巡逻以当前取景为中心，确保场外出生的怪物会走进战斗视野。</summary>
        /// <param name="anchor">关卡会话注入的相机中心。</param>
        /// <exception cref="ArgumentNullException">中心节点为空。</exception>
        public void SetPatrolAnchor(Node2D anchor)
        {
            m_PatrolAnchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
        }

        /// <summary>维护目标有效性、视野超时和索敌区发现。</summary>
        /// <param name="dt">本帧游戏秒数。</param>
        private void UpdateTarget(float dt)
        {
            // 已有目标先检查存活与视野超时，保留仍在范围内的目标。
            if (m_Target is { IsAlive: true })
            {
                bool outOfSight = Mathf.Abs(m_Target.GlobalPosition.X - GlobalPosition.X) > Config.SightRange;
                m_OutOfSightTime = outOfSight ? m_OutOfSightTime + dt : 0f;
                if (Config.LoseTargetTime <= 0f || m_OutOfSightTime < Config.LoseTargetTime)
                {
                    return;
                }

                Log.Debug("[Monster] {0} 丢失目标 {1}", Id, m_Target.Id);
            }

            // 当前目标失效后清除，再从索敌区选择第一个有效角色。
            SetTarget(null);
            if (Dead || m_Detector == null)
            {
                return;
            }

            foreach (Node2D body in m_Detector.GetOverlappingBodies())
            {
                if (body is AbstractActorEntity { IsAlive: true } actor && actor != this)
                {
                    SetTarget(actor);
                    Log.Debug("[Monster] {0} 发现目标 {1}", Id, actor.Id);
                    return;
                }
            }
        }

        /// <summary>设置当前目标并清零视野外计时。</summary>
        /// <param name="target">本次目标；空值清除索敌。</param>
        private void SetTarget(AbstractActorEntity target)
        {
            m_Target = target;
            m_OutOfSightTime = 0f;
        }

        /// <summary>按视野范围复制并调整索敌区矩形，避免共享资源互改。</summary>
        private void ConfigureDetector()
        {
            if (m_Detector == null)
            {
                return;
            }

            // 只调整本实体私有的 Shape 副本，避免改动 PackedScene 共享资源影响其他实例。
            foreach (Node child in m_Detector.GetChildren())
            {
                if (child is CollisionShape2D { Shape: RectangleShape2D rect } shapeNode)
                {
                    RectangleShape2D own = (RectangleShape2D)rect.Duplicate();
                    own.Size = new Vector2(Config.SightRange * 2f, rect.Size.Y);
                    shapeNode.Shape = own;
                    return;
                }
            }

            Log.Warning("[AbstractMonsterEntity] m_Detector 缺少 RectangleShape2D 形状，索敌失效：{0}", Name);
        }

        /// <summary>身体状态集合（**行为类别必须给出**：地面近战是移动/攻击/收招/受击/死亡，飞行、远程各不相同）。</summary>
        /// <returns>本怪物使用的全部身体状态。</returns>
        protected abstract FsmState<IMonsterBody>[] CreateBodyStates();

        /// <summary>创建并启动怪物身体状态机；已有状态机时保持复用。</summary>
        private void CreateBodyStateMachine()
        {
            if (Config == null || m_BodyFsm != null)
            {
                return;
            }

            m_BodyFsm = GF.Fsm.CreateFsm<IMonsterBody>($"MonsterBody_{Id}", this, CreateBodyStates());
            m_BodyFsm.Start(InitialBodyStateType);
        }

        /// <summary>销毁身体状态机，关停阶段仅清除本地引用。</summary>
        /// <param name="isShutdown">是否框架关停，关停时不访问状态机服务。</param>
        private void DestroyBodyStateMachine(bool isShutdown)
        {
            if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
            {
                GF.Fsm.DestroyFsm(m_BodyFsm);
            }

            m_BodyFsm = null;
        }

        /// <summary>创建并启动怪物 AI 状态机。</summary>
        private void CreateAiStateMachine()
        {
            if (m_AiFsm != null || Config == null || !IsShown)
            {
                return;
            }

            m_AiFsm = GF.Fsm.CreateFsm($"MonsterAI_{Id}", this, AiPreset.CreateStates());
            m_AiFsm.Start(InitialAiStateType);
        }

        /// <summary>销毁 AI 状态机，关停阶段仅清除本地引用。</summary>
        /// <param name="isShutdown">是否框架关停，关停时不访问状态机服务。</param>
        private void DestroyAiStateMachine(bool isShutdown)
        {
            if (m_AiFsm != null && !isShutdown && !m_AiFsm.IsDestroyed)
            {
                GF.Fsm.DestroyFsm(m_AiFsm);
            }

            m_AiFsm = null;
        }

        /// <summary>取出并清除待提交的攻击请求。</summary>
        /// <returns>待提交下标；没有请求时为 -1。</returns>
        int IMonsterBody.TakeAttackRequest()
        {
            int request = AiData.AttackRequest;
            AiData.AttackRequest = -1;
            return request;
        }

        /// <summary>提交怪物招式并装填攻击包。</summary>
        /// <param name="index">要提交的怪物攻击下标。</param>
        void IActorBody.BeginAttack(int index) => BeginConfiguredAttack(index);

        /// <summary>结束怪物招式并归还攻击包。</summary>
        void IActorBody.EndAttack() => EndCurrentAttack();

        /// <summary>死亡状态进入时关闭受击盒并广播一次击败事件；声音由动画负责。</summary>
        void IMonsterBody.BeginDeath()
        {
            SetHurtBoxEnabled(false);
            GF.Event.Fire(this,
                MonsterDiedEventArgs.Create(Id, m_ExperienceReward, Config, HurtBox.GetGlobalBounds().GetCenter()));
        }

        /// <summary>请求在当前物理帧完成后安全回收实体。</summary>
        void IMonsterBody.RequestRecycle() => m_RecycleRequested = true;

        /// <summary>由 Godot 随机源提供 AI 随机值。</summary>
        /// <returns>零到一之间的随机值。</returns>
        float IMonsterAiAgent.NextRandom() => GD.Randf();

        /// <summary>设置并限制怪物的水平移动意图。</summary>
        /// <param name="dir">水平意图：负值左，正值右，零停。</param>
        void IMonsterAiAgent.Move(int dir) => AiData.MoveIntent = Mathf.Clamp(dir, -1, 1);

        /// <summary>仅在可行动且未忙碌时按目标方向调整朝向。</summary>
        /// <param name="dir">期望朝向：正值右，负值左。</param>
        void IMonsterAiAgent.Face(int dir)
        {
            if (CanAct && !IsBusy)
            {
                SetFacing(dir);
            }
        }

        /// <summary>校验攻击请求并登记供身体状态机下一物理帧消费。</summary>
        /// <param name="index">希望提交的攻击配置下标。</param>
        /// <returns>下标和当前状态是否允许该请求。</returns>
        bool IMonsterAiAgent.RequestAttack(int index) => TryRequestAttack(index);

        /// <summary>校验并登记攻击请求；Boss 可覆写特殊出招条件，身体状态机统一提交。</summary>
        /// <param name="index">攻击配置下标。</param>
        /// <returns>当前状态是否接受请求。</returns>
        protected virtual bool TryRequestAttack(int index)
        {
            if (index < 0 || index >= OwnAttacks.Count || IsBusy || !CanAct || !OnFloor)
            {
                return false;
            }

            AiData.AttackRequest = index;
            AiData.MoveIntent = 0;
            return true;
        }
    }

    /// <summary>一次怪物显示的出生位置与基础属性倍率；默认倍率 1，池复用时整体重置。</summary>
    /// <param name="Position">世界出生位置。</param>
    /// <param name="HealthMultiplier">生命倍率，必须为正。</param>
    /// <param name="DefenseMultiplier">物理防御倍率，不能为负。</param>
    /// <param name="AttackMultiplier">全部招式威力倍率，不能为负。</param>
    /// <param name="ExperienceOverride">击败经验覆盖；-1 使用基础配置，0 表示不给经验。</param>
    public readonly record struct MonsterSpawn(Vector2 Position, float HealthMultiplier = 1f,
        float DefenseMultiplier = 1f, float AttackMultiplier = 1f, int ExperienceOverride = -1);
}
