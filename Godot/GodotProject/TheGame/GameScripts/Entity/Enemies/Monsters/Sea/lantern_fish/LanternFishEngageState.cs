using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 灯笼鱼按官方200px距离追击，近身每1.5秒以50%欲望扑击，高处目标时继续游荡。
    /// </summary>
    internal sealed class LanternFishEngageState : AbstractSeaMonsterEngageState
    {
        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, int direction, bool decide)
        {
            if (MathF.Abs(agent.TargetOffset.Y) > 400f)
            {
                PatrolBelowTarget(agent);
                return;
            }
            if (MathF.Abs(agent.TargetOffset.X) > 200f)
            {
                agent.Move(direction);
                return;
            }
            if (decide)
            {
                if (Chance(agent, agent.Config.AttackDesire))
                {
                    agent.RequestAttack(0);
                }
                else
                {
                    agent.Move(0);
                }
            }
        }
    }
}
