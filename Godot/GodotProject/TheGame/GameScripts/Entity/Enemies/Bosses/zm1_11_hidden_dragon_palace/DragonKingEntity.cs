using System;
using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Loot;
using GameFramework.Entity;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Entity;
using GameLogic.Heroes;
using GameLogic.Profile;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龙王拥有水波、六段冰环、冰冻和定点水柱；入场时消费首次旗幡资格。
    /// </summary>
    public partial class DragonKingEntity : AbstractBossEntity, IEnemyAiAgent
    {
        private static readonly EnemyBodyPreset m_BodyPreset = new(typeof(EnemyWalkState),
            typeof(DragonKingDeathState),
            () => new EnemyWalkState(typeof(DragonKingAttackState)), () => new DragonKingAttackState(),
            () => new EnemyHurtState(), () => new EnemyRecoveryState(),
            () => new DragonKingDeathState()); // 本首领的状态组合；运行实例由每次入场独立创建。
        private const int HighLevelMinimum = 21; // 原版仅龙王在入场角色达到21级时切换难度。
        private const int HighLevelHealth = 40000; // 龙王高难度生命，保留官方固定规则。
        private const int HighLevelDefense = 150; // 龙王高难度物防。
        private const int HighLevelExperience = 300; // 龙王高难度击败经验。
        private const int HighLevelSoulPerOrb = 100; // 龙王高难度每球灵魂。
        [Export] private CollisionShape2D m_WaterColumnRange; // 近身水柱水平距离，仅参与AI原点距离判断。

        private EnemyAiPreset m_Preset; // 本首领的状态工厂，创建状态时传入本次入场的出手欲望。
        [Export] private PackedScene m_WaveScene; // 普攻第11源帧创建的定点水波。
        [Export] private PackedScene m_TorrentScene; // 起手在目标左侧65px创建的独立水柱。
        private float m_InvulnerabilityLeft; // 原生起手键覆盖整段冰环和部分冰冻/水柱动作。
        private bool m_DropsBanner; // 本次实例是否已取得唯一保证掉落资格。
        private bool m_HighLevel; // 本次入场的龙王难度，不随战斗内升级改变。
        private IReadOnlyList<AttackConfig> m_HighLevelAttacks; // 本首领的四条高难度攻击数值，不进入公共配置索引。
        private EnemyBodyParams m_HighLevelBodyParams; // 本首领共享动画对应的高难度冷却与攻击参数。

        /// <summary>
        /// 近身水柱水平距离，像素；唯一来源是本角色场景的原生形状。
        /// </summary>
        internal float WaterColumnDistance
        {
            get
            {
                return m_WaterColumnRange.Shape.GetRect().End.X;
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
        protected override IReadOnlyList<AttackConfig> OwnAttacks
        {
            get
            {
                return m_HighLevel ? m_HighLevelAttacks : base.OwnAttacks;
            }
        }

        /// <inheritdoc />
        protected override EnemyBodyParams BodyParams
        {
            get
            {
                return m_HighLevel ? m_HighLevelBodyParams : base.BodyParams;
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
        protected override bool Invulnerable
        {
            get
            {
                return m_InvulnerabilityLeft > 0;
            }
        }

        /// <inheritdoc />
        protected override EquipmentDropPoolId? EquipmentDropPoolIdOverride
        {
            get
            {
                return m_DropsBanner ? EquipmentDropPoolId.EastSeaBanner :
                    m_HighLevel ? EquipmentDropPoolId.DragonKingHigh : null;
            }
        }

        /// <inheritdoc />
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup,
            bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (isNewInstance && (m_WaterColumnRange?.Shape is not SegmentShape2D || !m_WaterColumnRange.Disabled || m_WaveScene == null || m_TorrentScene == null))
            {
                throw new InvalidOperationException("龙王必须绑定独立水波和水柱场景。");
            }

            if (!isNewInstance)
            {
                return;
            }

            // 状态只持有已选择的数值，不向公共AI接口增加龙王专属的难度查询。
            m_Preset = new EnemyAiPreset(typeof(DragonKingEngageState), typeof(DragonKingEngageState),
                () => new DragonKingEngageState(m_HighLevel ? 20 : Config.AttackDesire, WaterColumnDistance));

            // 攻击伤害与冷却仍使用原有攻击表，龙王自行选择，不要求所有敌人支持等级档。
            var attacks = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig;
            var ids = this.GetUtility<ConfigIdUtility>();
            m_HighLevelAttacks = Array.AsReadOnly(new[]
            {
            attacks.Get(ids.GetAttackId(GameConfig.Entity.EntityId.DragonKing, 1, 2)),
            attacks.Get(ids.GetAttackId(GameConfig.Entity.EntityId.DragonKing, 2, 2)),
            attacks.Get(ids.GetAttackId(GameConfig.Entity.EntityId.DragonKing, 3, 2)),
            attacks.Get(ids.GetAttackId(GameConfig.Entity.EntityId.DragonKing, 4, 2)),
        });
            // 两档共用动画与运动，只为本首领替换攻击冷却和收招数值。
            var recovery = new float[m_HighLevelAttacks.Count];
            for (int index = 0; index < m_HighLevelAttacks.Count; index++)
            {
                AttackConfig data = m_HighLevelAttacks[index];
                recovery[index] = Math.Max(0, data.AiRecovery);
            }
            m_HighLevelBodyParams = base.BodyParams with
            {
                AttackRecovery = Array.AsReadOnly(recovery),
            };
        }

        /// <inheritdoc />
        public override void OnShow(object userData)
        {
            m_InvulnerabilityLeft = 0;
            m_DropsBanner = false;
            if (userData is not EnemySpawn spawn)
            {
                throw new ArgumentException("龙王显示参数必须包含出生位置与本次会话回调。", nameof(userData));
            }

            // 只在本首领入场时选择难度，由自身重置规则应用数值，不改写共享怪物配置。
            m_HighLevel = this.GetModel<ProfileModel>().Level >= HighLevelMinimum;
            base.OnShow(spawn);
            // 原版在实例构造时消费资格，即使随后没捡到法宝也不会再次保证掉落。
            m_DropsBanner = this.GetSystem<ProfileSystem>().TryClaimEastSeaBanner();
            if (m_DropsBanner)
            {
                EquipmentDropChanceOverride = 1;
            }

            RestartAnim("RESET");
            RestartAnim(EnemyAnims.Idle);
        }

        /// <summary>
        /// 官方技能沿用起手朝向，只有普通水波由attackTarget明确转向目标。
        /// </summary>
        /// <param name="index">本首领的招式下标。</param>
        protected override void FaceAttackTarget(int index)
        {
            if (index == 0)
            {
                base.FaceAttackTarget(index);
            }
        }

        /// <inheritdoc />
        protected override void ApplyConfigOverrides()
        {
            base.ApplyConfigOverrides();
            if (m_HighLevel)
            {
                m_MaxHp = HighLevelHealth;
                m_Defense = HighLevelDefense;
                m_ExperienceReward = HighLevelExperience;
                SoulPerOrbOverride = HighLevelSoulPerOrb;
                EquipmentDropChanceOverride = .1f;
            }
        }

        /// <inheritdoc />
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            m_InvulnerabilityLeft = Math.Max(0, m_InvulnerabilityLeft - elapseSeconds);
        }

        /// <inheritdoc />
        public override void OnHide(bool isShutdown, object userData)
        {
            m_DropsBanner = false;
            m_InvulnerabilityLeft = 0;
            base.OnHide(isShutdown, userData);
            m_HighLevel = false;
        }

        /// <inheritdoc />
        protected override void OnHitLanded(AbstractActorEntity target, AttackData data, DamageResult result)
        {
            if (AttackSegment == 2 && target is AbstractHeroEntity { CanReceiveStatusEffect: true })
            {
                this.GetSystem<HeroStatusSystem>().ApplyFreeze(m_HighLevel ? 5 : 2);
            }
        }

        /// <summary>
        /// 原生起手键设置官方90、85或24源帧的无敌时间，暂停不推进。
        /// </summary>
        /// <param name="seconds">按24fps换算的游戏秒数。</param>
        private void BeginInvulnerabilityAnimEvent(float seconds)
        {
            m_InvulnerabilityLeft = seconds;
        }

        /// <summary>
        /// 普攻第11源帧在龙王原点创建不移动的水波。
        /// </summary>
        private void SpawnWaveAnimEvent()
        {
            if (AttackSegment == 0)
            {
                SpawnEffect(m_WaveScene, OwnAttacks[0], GlobalPosition, Facing);
            }
        }

        /// <summary>
        /// 水柱在目标原点左侧65px、龙王上方120px创建，按原版保持原始朝向。
        /// </summary>
        private void SpawnTorrentAnimEvent()
        {
            if (AttackSegment != 3)
            {
                return;
            }

            float targetX = GlobalPosition.X + ((IEnemyAiAgent)this).TargetOffset.X;
            SpawnEffect(m_TorrentScene, OwnAttacks[3], new Vector2(targetX - 65, GlobalPosition.Y - 120), -1);
        }

        /// <summary>
        /// 离身效果借用本次等级档的数值和出招快照，由本首领统一回收。
        /// </summary>
        /// <param name="scene">已登记到节点池的效果。</param>
        /// <param name="data">当前等级档的攻击数值。</param>
        /// <param name="position">官方世界原点。</param>
        /// <param name="direction">效果朝向，水柱固定不镜像。</param>
        private void SpawnEffect(PackedScene scene, AttackConfig data, Vector2 position, int direction)
        {
            var effect = RentSkillEffect(scene);
            int power = (int)MathF.Round(Mathf.Lerp(data.FlatPower.X, data.FlatPower.Y, GD.Randf()),
                MidpointRounding.AwayFromZero);
            effect.Show(data, position, direction, CaptureCombatAttributes(), power, false,
                ReleaseSkillEffect, hitSoundId: HitSoundId);
        }
    }
}
