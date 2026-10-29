using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 牛妖以110px角色原点距离重斩，不能用宽武器图的边缘替代官方近身选招条件。
    /// </summary>
    public partial class BullGuardEntity : AbstractGroundMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = EnemyAiPresets.Patrol; // 普通巡逻、追击与80%出手欲望由共享小怪状态负责。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <inheritdoc />
        protected override bool TryRequestAttack(int index)
        {
            return MathF.Abs(((IEnemyAiAgent)this).TargetOffset.X) <= 110f && base.TryRequestAttack(index);
        }
    }
}
