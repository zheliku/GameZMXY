using GameLogic.Enemies.AI;
using GameFramework.Entity;
using GodotGameFramework;
using System;
using GameLogic.Entity;
using Godot;
using GameLogic.Architecture;
using GameLogic.Config;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨魔王低生命阶段召唤的飞行鲨鱼，拥有接触斩击和隐藏后地面旋风。
    /// </summary>
    public partial class SharkSpawnEntity : AbstractSeaMonsterEntity, IEnemyFreeMoveBody
    {
        private static readonly EnemyAiPreset m_Preset = new(typeof(SharkSpawnEngageState), typeof(SharkSpawnEngageState),
            () => new SharkSpawnEngageState()); // 追踪与近身决策保持同一时钟。
        private const int SlashAttackId = 2033; // 接触后独立斩击，不复用身体攻击包。
        private static readonly EnemyBodyPreset m_BodyPreset = EnemyBodyPresets.Swim<SharkSpawnAttackState>(
            288f, 96f, 72f); // 游动保持原加速度/退避速度，仅替换为鲨鱼专属攻击。

        [Export] private CollisionShape2D m_TrackingRange; // 本鲨鱼追踪高度，与250×50px近身条件分开。
        [Export] private PackedScene m_SlashScene; // 接触目标后原地创建的连续斩击。
        [Export] private PackedScene m_WarningScene; // 隐藏阶段目标地面的纯视觉警示。
        [Export] private PackedScene m_TornadoScene; // 起手与警示结束时分别创建的旋风。
        private int m_EffectGeneration; // 显示周期编号，阻止旧警示完成回调污染池复用。
        private float m_InvulnerabilityLeft; // 旋风起手的剩余无敌秒数。

        /// <summary>
        /// 官方外层追踪高度半幅，像素。
        /// </summary>
        internal float TrackingHeight
        {
            get
            {
                return m_TrackingRange.Shape.GetRect().End.Y;
            }
        }

        /// <summary>
        /// 本鲨鱼 AI 请求的垂直游动方向；0 为悬停。
        /// </summary>
        public int VerticalIntent { get; internal set; }

        /// <summary>
        /// 退避时按原规则直接上游，其余阶段使用本鲨鱼的垂直加速度。
        /// </summary>
        public bool DirectVerticalMovement { get; internal set; }

        /// <summary>
        /// 当前世界高度，用于保持原版退避时的垂直边界。
        /// </summary>
        internal float Altitude
        {
            get
            {
                return GlobalPosition.Y;
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
        protected override EnemyBodyPreset BodyPreset
        {
            get
            {
                return m_BodyPreset;
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
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (isNewInstance && (m_TrackingRange?.Shape is not SegmentShape2D || !m_TrackingRange.Disabled || m_SlashScene == null || m_WarningScene == null || m_TornadoScene == null))
            {
                throw new InvalidOperationException("鲨鱼必须绑定斩击、警示和旋风效果。");
            }
        }

        /// <inheritdoc />
        public override void OnShow(object userData)
        {
            m_EffectGeneration++;
            m_InvulnerabilityLeft = 0f;
            VerticalIntent = 0;
            DirectVerticalMovement = false;
            base.OnShow(userData);
            Body.Visible = true;
        }

        /// <summary>
        /// 仅在自主游动状态接受接触斩击或旋风请求，不要求落地。
        /// </summary>
        /// <param name="index">本鲨鱼的招式下标。</param>
        /// <returns>本次攻击请求是否已经登记。</returns>
        protected override bool TryRequestAttack(int index)
        {
            if (m_BodyFsm?.CurrentState is not EnemySwimState || !base.TryRequestAttack(index))
            {
                return false;
            }

            VerticalIntent = 0;
            return true;
        }

        /// <inheritdoc />
        public override void OnHide(bool isShutdown, object userData)
        {
            m_EffectGeneration++;
            base.OnHide(isShutdown, userData);
        }

        /// <inheritdoc />
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            m_InvulnerabilityLeft = Math.Max(0f, m_InvulnerabilityLeft - elapseSeconds);
        }

        /// <inheritdoc />
        protected override void OnHitLanded(AbstractActorEntity target, AttackData data, DamageResult result)
        {
            base.OnHitLanded(target, data, result);
            if (AttackSegment != 0 || m_BodyFsm.CurrentState is not SharkSpawnAttackState state)
            {
                return;
            }

            state.CompleteContact(GlobalPosition + ((IEnemyAiAgent)this).TargetOffset - new Vector2(100f, 100f));
        }

        /// <summary>
        /// 在正常身体帧创建接触斩击，避免AreaEntered查询期间改变物理形状。
        /// </summary>
        /// <param name="position">接触时已捕获的世界原点。</param>
        internal void SpawnContactSlash(Vector2 position)
        {
            var slash = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(SlashAttackId);
            var effect = CreateSkillEffect(m_SlashScene);
            effect.Show(slash, position, Facing, CaptureCombatAttributes(),
                Mathf.RoundToInt(slash.FlatPower.X), false, ReleaseSkillEffect, hitSoundId: HitSoundId);
        }

        /// <summary>
        /// hit1第34/37/40/43/46源帧沿施放朝向突进，帧间不重新创建攻击包。
        /// </summary>
        private void DashAnimEvent()
        {
            if (AttackSegment == 0)
            {
                Velocity = new Vector2(Facing * 480f, Velocity.Y);
            }
        }

        /// <summary>
        /// 旋风施放起手进入官方30/24秒无敌，同时留下初始位置的旋风攻击。
        /// </summary>
        private void BeginTornadoAnimEvent()
        {
            if (AttackSegment != 1)
            {
                return;
            }

            m_InvulnerabilityLeft = 30f / 24f;
            SpawnTornado(GlobalPosition);
        }

        /// <summary>
        /// 隐藏身体并在目标地面留下警示；本周期仍有效时才创建旋风和恢复身体。
        /// </summary>
        private void SpawnTornadoWarningAnimEvent()
        {
            if (AttackSegment != 1 || m_BodyFsm.CurrentState is not SharkSpawnAttackState state)
            {
                return;
            }

            int generation = m_EffectGeneration;
            Vector2 position = new(GlobalPosition.X + ((IEnemyAiAgent)this).TargetOffset.X, 510f);
            Body.Visible = false;
            SetHurtBoxEnabled(false);
            Velocity = Vector2.Zero;
            state.WaitForReturn();
            var warning = CreateSkillEffect(m_WarningScene);
            warning.Show(null, position, Facing, CaptureCombatAttributes(), 0, false, ReleaseSkillEffect,
                () =>
                {
                    if (generation == m_EffectGeneration && IsAlive && m_BodyFsm.CurrentState == state)
                    {
                        SpawnTornado(position);
                        RestoreSharkBody();
                        state.CompleteReturn();
                    }
                });
        }

        /// <summary>
        /// 警示结束或攻击中断时恢复身体和受击盒，不改变实体世界原点。
        /// </summary>
        internal void RestoreSharkBody()
        {
            Body.Visible = true;
            if (IsAlive)
            {
                SetHurtBoxEnabled(true);
            }
        }

        /// <summary>
        /// 创建鲨鱼旋风并使用本招独立命中预算。
        /// </summary>
        /// <param name="position">本次旋风的世界原点。</param>
        private void SpawnTornado(Vector2 position)
        {
            var effect = CreateSkillEffect(m_TornadoScene);
            effect.Show(OwnAttacks[1], position, Facing, CaptureCombatAttributes(),
                Mathf.RoundToInt(OwnAttacks[1].FlatPower.X), false, ReleaseSkillEffect,
                hitSoundId: HitSoundId);
        }
    }
}
