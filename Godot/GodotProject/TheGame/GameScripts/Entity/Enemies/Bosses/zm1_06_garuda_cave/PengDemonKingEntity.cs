using System;
using GameLogic.Architecture;
using GameFramework.Entity;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Body;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 彭魔王：拥有地面/飞行切换、化卵命中次数及离身火焰，最终死亡才提交击败收益。
    /// </summary>
    public partial class PengDemonKingEntity : AbstractBossEntity, IActorBody
    {
        private static readonly EnemyBodyPreset m_BodyPreset = new(typeof(PengDemonKingMoveState),
            typeof(PengDemonKingDeathState),
            () => new PengDemonKingMoveState(), () => new PengDemonKingAttackState(),
            () => new PengDemonKingHurtState(), () => new PengDemonKingEggState(),
            () => new PengDemonKingRebirthState(), () => new PengDemonKingDeathState()); // 本首领的状态组合；运行实例由每次入场独立创建。
        private static readonly EnemyAiPreset m_Preset = new(typeof(PengDemonKingEngageState), typeof(PengDemonKingEngageState),
            () => new PengDemonKingEngageState()); // 每只首领独立选招。

        [Export] private CollisionShape2D m_TrackingRange; // 本首领允许追踪的高度，独立于圆形选招半径。
        [Export] private CollisionShape2D m_MeleeRange; // 近身刺击半径，仅参与AI原点距离判断。

        [Export] private PackedScene m_FireScene; // 攻击和化卵期间使用的同一池化火焰场景。
        [Export] private PackedScene m_DeathScene; // 官方首领死亡旋涡，由死亡动画末帧创建。
        private float m_GroundLeft; // 地面阶段剩余秒数，化卵/复生时冻结而不重置。
        private float m_FlightLeft; // 飞行阶段剩余秒数。
        private float m_InvulnerableLeft; // 起飞和冲刺共用的本实例无敌时钟。
        private int m_EggHitsLeft; // 凤凰蛋还需成功命中的次数，不是生命值。

        /// <summary>
        /// 原版追踪高度，像素；不随圆形选招半径改变。
        /// </summary>
        internal float TrackingHeight
        {
            get
            {
                return m_TrackingRange.Shape.GetRect().End.Y;
            }
        }

        /// <summary>
        /// 近身刺击半径，像素；唯一来源是本角色场景的原生形状。
        /// </summary>
        internal float MeleeDistance
        {
            get
            {
                return m_MeleeRange.Shape.GetRect().End.X;
            }
        }

        /// <inheritdoc />
        protected override EnemyBodyPreset BodyPreset
        {
            get
            {
                return m_BodyPreset;
            }
        }

        /// <summary>
        /// 当前使用无重力的空中运动；出招和受击期间也保留此运动事实。
        /// </summary>
        internal bool Flying { get; private set; }

        /// <summary>
        /// 首次生命耗尽而尚未破蛋，不等同于敌人死亡。
        /// </summary>
        internal bool IsEgg
        {
            get
            {
                return Hp <= 0 && !Dead;
            }
        }

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <inheritdoc />
        protected override bool InHurt
        {
            get
            {
                return m_BodyFsm?.CurrentState is PengDemonKingHurtState;
            }
        }

        /// <inheritdoc />
        protected override bool InRecovery
        {
            get
            {
                return m_BodyFsm?.CurrentState is PengDemonKingEggState or PengDemonKingRebirthState;
            }
        }

        /// <inheritdoc />
        protected override bool Invulnerable
        {
            get
            {
                return m_InvulnerableLeft > 0f;
            }
        }

        /// <summary>
        /// 同一身体契约按当前飞行事实提供重力，不改共享配置。
        /// </summary>
        float IActorBody.Gravity
        {
            get
            {
                return Flying ? 0f : Config.Gravity;
            }
        }

        /// <summary>
        /// 装配时校验首领必需的原生效果，不在出招时静默省略火焰或死亡演出。
        /// </summary>
        /// <param name="entityId">本次运行编号。</param>
        /// <param name="entityAssetName">实体场景路径。</param>
        /// <param name="entityGroup">所属实体组。</param>
        /// <param name="isNewInstance">是否首次创建池实例。</param>
        /// <param name="userData">本次显示参数。</param>
        /// <exception cref="InvalidOperationException">缺少首领专属效果绑定。</exception>
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup,
            bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (isNewInstance && (m_MeleeRange?.Shape is not CircleShape2D || !m_MeleeRange.Disabled || m_TrackingRange?.Shape is not SegmentShape2D || !m_TrackingRange.Disabled || m_FireScene == null || m_DeathScene == null))
            {
                throw new InvalidOperationException("彭魔王必须绑定火焰与死亡效果场景。");
            }
        }

        /// <inheritdoc />
        public override void OnShow(object userData)
        {
            Flying = false;
            m_GroundLeft = 10f;
            m_FlightLeft = 30f;
            m_InvulnerableLeft = 0f;
            m_EggHitsLeft = 0;
            base.OnShow(userData);
            // RESET restores the death-only visibility track before this pooled body moves again.
            RestartAnim("RESET");
            RestartAnim(EnemyAnims.Idle);
        }

        /// <summary>
        /// 原版形态计时在出招和受击中仍推进，在化卵与复生中冻结。
        /// </summary>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        /// <param name="realElapseSeconds">本帧实际秒数。</param>
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            if (!IsShown || Dead || elapseSeconds <= 0f)
            {
                return;
            }

            m_InvulnerableLeft = Math.Max(0f, m_InvulnerableLeft - elapseSeconds);
            if (IsEgg || m_BodyFsm.CurrentState is PengDemonKingRebirthState)
            {
                return;
            }

            if (Flying)
            {
                m_FlightLeft -= elapseSeconds;
                if (m_FlightLeft <= 0f)
                {
                    Flying = false;
                    GlobalPosition = new Vector2(GlobalPosition.X, 150f);
                    m_GroundLeft = GD.RandRange(10, 19);
                }
            }
            else
            {
                m_GroundLeft -= elapseSeconds;
                if (m_GroundLeft <= 0f)
                {
                    Flying = true;
                    Velocity = new Vector2(Velocity.X, -240f);
                    m_FlightLeft = GD.RandRange(10, 19);
                    m_InvulnerableLeft = 3f;
                }
            }
        }

        /// <summary>
        /// 破蛋按命中次数处理，保留共同命中反馈但不产生伤害数字。
        /// </summary>
        /// <param name="data">已经由攻击来源完成去重的攻击包。</param>
        /// <returns>凤凰蛋为零伤害成功命中，其余使用正式战斗公式。</returns>
        protected override DamageResult CalculateIncomingHit(AttackData data)
        {
            return IsEgg ? new DamageResult(0, false, false, data.Kind) : base.CalculateIncomingHit(data);
        }

        /// <summary>
        /// 首次归零改为化卵；单人模式第五次破蛋命中才返回真正死亡。
        /// </summary>
        /// <param name="amount">普通阶段的实际生命伤害。</param>
        /// <returns>是否已破蛋并进入最终死亡。</returns>
        protected override bool ApplyDamage(int amount)
        {
            if (IsEgg)
            {
                return --m_EggHitsLeft <= 0;
            }

            if (base.ApplyDamage(amount))
            {
                m_EggHitsLeft = 5;
                m_InvulnerableLeft = 0f;
            }
            return false;
        }

        /// <inheritdoc />
        protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
        {
            if (!IsEgg)
            {
                base.OnHurt(data, result, knockback, attackerEntityId);
            }
        }

        /// <summary>
        /// 空中允许本首领专属招式，地面仍沿用首领落地限制。
        /// </summary>
        /// <param name="index">本首领招式下标。</param>
        /// <returns>是否接受了新的出招请求。</returns>
        protected override bool TryRequestAttack(int index)
        {
            if (!Flying)
            {
                return base.TryRequestAttack(index);
            }

            var agent = (IEnemyAiAgent)this;
            if (index < 0 || index >= OwnAttacks.Count || agent.IsAttacking || agent.IsCcLocked || Dead ||
                HasPendingHurt || m_BodyFsm.CurrentState is not PengDemonKingMoveState)
            {
                return false;
            }

            AiData.AttackRequest = index;
            AiData.MoveIntent = 0;
            this.GetSystem<EnemyAiSystem>().MarkUsed(AiData, OwnAttacks, index, GD.Randf);
            return true;
        }

        /// <summary>
        /// 化卵时立即停止原来的攻击与抛飞，随后由蛋状态施加正常重力。
        /// </summary>
        internal void BeginEgg()
        {
            Flying = false;
            Velocity = Vector2.Zero;
            ReleaseAttack();
            TakePendingHurt();
            AiData.MoveIntent = 0;
            AiData.AttackRequest = -1;
        }

        /// <summary>
        /// 七秒内未破蛋则补满本次配方生命，复生动画本身不额外无敌。
        /// </summary>
        internal void BeginRebirth()
        {
            RestoreHealth(MaxHp);
            m_EggHitsLeft = 0;
            Velocity = Vector2.Zero;
        }

        /// <summary>
        /// 普攻动画从原文件fire子节点传入局部Y，具体位置由本首领决定。
        /// </summary>
        /// <param name="sourceY">源动作fire子节点的局部纵坐标。</param>
        private void SpawnFireAnimEvent(float sourceY)
        {
            SpawnFire(sourceY);
        }

        /// <summary>
        /// 冲刺第17源帧只登记本招的运动变化，不在播放器回调中切换状态。
        /// </summary>
        private void BeginDashAnimEvent()
        {
            if (m_BodyFsm.CurrentState is PengDemonKingAttackState state)
            {
                NewAttackAnimEvent();
                state.BeginDash(this);
            }
        }

        /// <summary>
        /// 原生起飞和冲刺动画按源时长开启无敌，暂停时不推进。
        /// </summary>
        /// <param name="seconds">本次无敌的游戏秒数。</param>
        private void BeginInvulnerabilityAnimEvent(float seconds)
        {
            m_InvulnerableLeft = seconds;
        }

        /// <summary>
        /// 死亡动画末帧创建首领死亡旋涡；动画只负责时点，效果自身负责回池。
        /// </summary>
        private void SpawnBossDeathAnimEvent()
        {
            if (m_BodyFsm.CurrentState is not PengDemonKingDeathState state)
            {
                return;
            }

            var effect = CreateSkillEffect(m_DeathScene);
            effect.Show(null, GlobalPosition, Facing, CaptureCombatAttributes(), 0, false, ReleaseSkillEffect,
                state.CompleteEffect);
        }

        /// <summary>
        /// 创建独立火焰；蛋形态没有fire锚点，按原版在身体上方散布。
        /// </summary>
        /// <param name="sourceY">可选源动作fire锚点Y。</param>
        internal void SpawnFire(float? sourceY = null)
        {
            Vector2 offset = sourceY is float y ? new Vector2(Facing * (float)GD.RandRange(30f, 230f), y) :
                new Vector2((float)GD.RandRange(-100f, 100f), -(float)GD.RandRange(100f, 250f));
            var effect = CreateSkillEffect(m_FireScene);
            var attack = OwnAttacks[0];
            int power = (int)MathF.Round(Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf()));
            effect.Show(attack, GlobalPosition + offset, Facing, CaptureCombatAttributes(), power, false,
                ReleaseSkillEffect, hitSoundId: HitSoundId);
        }
    }
}
