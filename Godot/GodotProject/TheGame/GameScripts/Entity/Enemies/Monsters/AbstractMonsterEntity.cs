using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 小怪保留致死一击的抛飞运动；活体受击机制共享Enemy，具体物种选择自己的AI与运动方式。
    /// </summary>
    public abstract partial class AbstractMonsterEntity : AbstractEnemyEntity
    {
        /// <inheritdoc />
        protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
        {
            if (Dead)
            {
                // 死亡事实先于本回调置位，仍须把致死一击交给死亡状态，不能在这里丢掉击退。
                if (knockback != Vector2.Zero)
                {
                    m_PendingHurt = knockback;
                }

                return;
            }
            base.OnHurt(data, result, knockback, attackerEntityId);
        }
    }
}
