using System;
using GameConfig.Entity;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 牛魔王人形拥有五招、短时无敌和吞噬控制，完整死亡后转入心脏形态。
    /// </summary>
    public partial class BullDemonKingEntity : AbstractBullDemonFormEntity
    {
        private static readonly EnemyBodyPreset m_BodyPreset = new(typeof(BullDemonKingMoveState),
            typeof(BullDemonTransformState),
            () => new BullDemonKingMoveState(), () => new BullDemonKingAttackState(),
            () => new EnemyHurtState(), () => new EnemyRecoveryState(),
            () => new BullDemonTransformState()); // 本首领的状态组合；运行实例由每次入场独立创建。
        private static readonly EnemyAiPreset m_Preset = new(typeof(BullDemonKingEngageState), typeof(BullDemonKingEngageState),
            () => new BullDemonKingEngageState()); // 人形专属距离分支和冷却。
        [Export] private CollisionShape2D m_CaptureRange; // 吞噬动作命中时的水平捕获范围，与80px选招距离分开。
        [Export] private CollisionShape2D m_SwallowRange; // 吞噬水平距离，仅参与AI原点距离判断。
        [Export] private CollisionShape2D m_StompRange; // 践踏水平距离，仅参与AI原点距离判断。

        [Export] private PackedScene m_DeathScene; // 人形闪烁后继续播放官方BossDead。
        private float m_InvulnerabilityLeft; // 原生方法键启动的无敌秒数。
        private AbstractHeroEntity m_Captive; // 当前吞噬目标，只在本次招式中借用。
        private int m_CaptiveId; // 捕获时的运行编号，防止对象回池后误恢复另一具英雄。

        /// <summary>
        /// 吞噬水平距离，像素；唯一来源是本角色场景的原生形状。
        /// </summary>
        internal float SwallowDistance
        {
            get
            {
                return m_SwallowRange.Shape.GetRect().End.X;
            }
        }

        /// <summary>
        /// 践踏水平距离，像素；唯一来源是本角色场景的原生形状。
        /// </summary>
        internal float StompDistance
        {
            get
            {
                return m_StompRange.Shape.GetRect().End.X;
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

        /// <inheritdoc />
        public override void OnInit(int entityId, string entityAssetName, GameFramework.Entity.IEntityGroup entityGroup,
            bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (isNewInstance && (m_CaptureRange?.Shape is not SegmentShape2D || !m_CaptureRange.Disabled || m_SwallowRange?.Shape is not SegmentShape2D || !m_SwallowRange.Disabled || m_StompRange?.Shape is not SegmentShape2D || !m_StompRange.Disabled))
            {
                throw new System.InvalidOperationException("本物种必须绑定禁用物理碰撞的原生选招范围。");
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
        protected override EntityId NextForm
        {
            get
            {
                return EntityId.BullDemonHeart;
            }
        }

        /// <inheritdoc />
        protected override Vector2 NextFormPosition
        {
            get
            {
                return new Vector2(1350, 300);
            }
        }

        /// <inheritdoc />
        protected override bool Invulnerable
        {
            get
            {
                return m_InvulnerabilityLeft > 0f;
            }
        }

        /// <inheritdoc />
        public override void OnShow(object userData)
        {
            m_InvulnerabilityLeft = 0f;
            m_Captive = null;
            m_CaptiveId = 0;
            base.OnShow(userData);
        }

        /// <inheritdoc />
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            m_InvulnerabilityLeft = MathF.Max(0, m_InvulnerabilityLeft - elapseSeconds);
        }

        /// <inheritdoc />
        public override void OnHide(bool isShutdown, object userData)
        {
            if (!isShutdown)
            {
                ReleaseCaptive();
            }

            m_Captive = null;
            m_CaptiveId = 0;
            m_InvulnerabilityLeft = 0f;
            base.OnHide(isShutdown, userData);
        }

        /// <summary>
        /// 收招、硬直、死亡和关卡离开都恢复仍属本次吞噬的目标。
        /// </summary>
        internal void ReleaseCaptive()
        {
            if (IsInstanceValid(m_Captive) && m_Captive.Id == m_CaptiveId)
            {
                m_Captive.ReleaseCapture(Id);
            }

            m_Captive = null;
            m_CaptiveId = 0;
        }

        /// <summary>
        /// 起手轨道记录官方无敌持续秒数。
        /// </summary>
        /// <param name="seconds">按24fps源帧换算的游戏秒数。</param>
        private void BeginInvulnerabilityAnimEvent(float seconds)
        {
            m_InvulnerabilityLeft = seconds;
        }

        /// <summary>
        /// 冲刺第30源帧把实际运动交给当前专属身体状态。
        /// </summary>
        private void BeginDashAnimEvent()
        {
            if (m_BodyFsm.CurrentState is BullDemonKingAttackState state)
            {
                state.BeginDash(Facing);
            }
        }

        /// <summary>
        /// 吞噬第28源帧按200px原点距离捕获非无敌英雄，失败则立即收招。
        /// </summary>
        private void CaptureHeroAnimEvent()
        {
            foreach (var entity in GF.Entity.GetAllLoadedEntities())
            {
                if (entity is not AbstractHeroEntity hero || !hero.IsAlive ||
                    MathF.Abs(hero.GlobalPosition.X - GlobalPosition.X) > m_CaptureRange.Shape.GetRect().End.X || !hero.TryCapture(Id))
                {
                    continue;
                }

                m_Captive = hero;
                m_CaptiveId = hero.Id;
                return;
            }
            if (m_BodyFsm.CurrentState is BullDemonKingAttackState state)
            {
                state.MissCapture();
            }
        }

        /// <summary>
        /// 原生吞噬脉冲直接扣除配置威力，并按该威力十倍恢复首领生命。
        /// </summary>
        private void DrainCaptiveAnimEvent()
        {
            if (!IsInstanceValid(m_Captive) || m_Captive.Id != m_CaptiveId || !m_Captive.IsAlive)
            {
                return;
            }

            var attack = OwnAttacks[4];
            int power = Mathf.RoundToInt(Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf()));
            m_Captive.ReceiveCaptureDamage(Id, power);
            Heal(power * 10);
        }

        /// <summary>
        /// 人形闪烁完成后播放独立死亡旋涡，完成才交接最终形态。
        /// </summary>
        private void SpawnBossDeathAnimEvent()
        {
            if (m_BodyFsm.CurrentState is not BullDemonTransformState state)
            {
                return;
            }

            var effect = CreateSkillEffect(m_DeathScene);
            effect.Show(null, GlobalPosition, Facing, CaptureCombatAttributes(), 0, false, ReleaseSkillEffect,
                state.CompleteAnimation);
        }
    }
}
