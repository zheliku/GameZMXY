using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 牛妖以110px角色原点距离重斩，不能用宽武器图的边缘替代官方近身选招条件。
    /// </summary>
    public partial class BullGuardEntity : AbstractGroundMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = EnemyAiPresets.Patrol; // Monster25 的原点距离与80%出手欲望。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

    }
}
