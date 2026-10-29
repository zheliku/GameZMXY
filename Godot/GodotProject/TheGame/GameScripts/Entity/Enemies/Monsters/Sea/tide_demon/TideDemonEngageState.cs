using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 潮汐妖在150px内优先使用十秒冷却的第二招，其余节拍使用普攻。
    /// </summary>
    internal sealed class TideDemonEngageState : AbstractSeaMonsterEngageState
    {
        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, int direction, bool decide)
        {
            if (MathF.Abs(agent.TargetOffset.Y) > 300f)
            {
                PatrolBelowTarget(agent);
                return;
            }
            if (MathF.Abs(agent.TargetOffset.X) > 150f)
            {
                agent.Move(direction);
                return;
            }
            if (decide)
            {
                agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
            }
        }
    }
}
