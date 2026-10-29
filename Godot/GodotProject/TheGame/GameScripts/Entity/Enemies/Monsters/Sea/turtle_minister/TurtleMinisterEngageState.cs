using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龟丞相按400px攻击距离追击，以60%欲望施放长程身体魔法。
    /// </summary>
    internal sealed class TurtleMinisterEngageState : AbstractSeaMonsterEngageState
    {
        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, int direction, bool decide)
        {
            if (MathF.Abs(agent.TargetOffset.Y) > 800f)
            {
                PatrolBelowTarget(agent);
                return;
            }
            if (MathF.Abs(agent.TargetOffset.X) > 400f)
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
