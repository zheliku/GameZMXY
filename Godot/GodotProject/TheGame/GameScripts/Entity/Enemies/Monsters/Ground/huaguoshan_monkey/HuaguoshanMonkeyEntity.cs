using GameLogic.Enemies.AI;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 花果山猴子：未被攻击时只巡逻，受击激怒后才主动索敌；动作仍复用地面小怪身体。
    /// </summary>
    public partial class HuaguoshanMonkeyEntity : AbstractGroundMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = EnemyAiPresets.Patrol; // Monster1 的原点出招距离。
        private bool m_Provoked; // 本次显示是否已被攻击，对应原版 Monster1 的 isattack。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <summary>
        /// 原版只限制首次索敌；失去攻击者后，已被激怒的猴子可以重新发现英雄。
        /// </summary>
        protected override bool CanAcquireTarget
        {
            get
            {
                return m_Provoked;
            }
        }

        /// <summary>
        /// 实体复用时恢复未被激怒的巡逻行为。
        /// </summary>
        /// <param name="userData">本次怪物出生参数。</param>
        public override void OnShow(object userData)
        {
            m_Provoked = false;
            base.OnShow(userData);
        }

        /// <summary>
        /// 成功受击后记住激怒事实；攻击者锁定、受击打断及死亡仍由 Enemy 处理。
        /// </summary>
        /// <param name="data">本次攻击快照，不保留池化引用。</param>
        /// <param name="result">本次结算结果。</param>
        /// <param name="knockback">本次击退速度，像素每秒。</param>
        /// <param name="attackerEntityId">攻击者运行编号。</param>
        protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
        {
            m_Provoked = true;
            base.OnHurt(data, result, knockback, attackerEntityId);
        }
    }
}
