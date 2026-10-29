using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 虾兵保持200～350px距离，在源一秒节拍按40%欲望发射魔法弹。
    /// </summary>
    internal sealed class ShrimpDemonEngageState : AbstractSeaMonsterEngageState
    {
        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, int direction, bool decide)
        {
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

                return;
            }

            float distance = MathF.Abs(agent.TargetOffset.X);
            if (distance <= 200f)
            {
                agent.Move(-direction);
            }
            else if (distance >= 350f)
            {
                agent.Move(direction);
            }
        }
    }
}
