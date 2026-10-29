using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龙王近身优先水柱，中距离在冰环与冰冻中选招，冷却中按原版节拍尝试水波。
    /// </summary>
    internal sealed class DragonKingEngageState : AbstractBossAiState
    {
        private readonly int m_AttackDesire; // 本次入场选定的普通水波出手欲望，战斗内升级不改变。

        /// <summary>
        /// 由龙王实体传入本次入场的出手欲望，状态不引用实体节点。
        /// </summary>
        /// <param name="attackDesire">0..100的普通水波出手概率。</param>
        public DragonKingEngageState(int attackDesire)
        {
            m_AttackDesire = attackDesire;
        }

        /// <inheritdoc />
        protected override float HeightRange
        {
            get
            {
                return 300;
            }
        }

        /// <inheritdoc />
        protected override double GetDecisionPeriod(IEnemyAiAgent agent)
        {
            return Math.Abs(agent.TargetOffset.X) <= 100 ? 1.5 : 1;
        }

        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
        {
            float distance = Math.Abs(agent.TargetOffset.X);
            if (distance <= 100)
            {
                if (agent.AiData.Cooldowns[3] <= 0)
                {
                    agent.RequestAttack(3);
                    return;
                }
            }
            else if (distance <= 450)
            {
                bool ice = agent.AiData.Cooldowns[1] <= 0;
                bool freeze = agent.AiData.Cooldowns[2] <= 0;
                if (ice || freeze)
                {
                    agent.RequestAttack(ice && freeze ? agent.NextRandom() < .5f ? 1 : 2 : ice ? 1 : 2);
                    return;
                }
            }
            else
            {
                agent.Move(Math.Sign(agent.TargetOffset.X));
                return;
            }

            // 保留近身游荡和中距离追击的区别，未出招时不每帧重新掷出手概率。
            if (decision)
            {
                if (agent.NextRandom() * 100 < m_AttackDesire)
                {
                    agent.RequestAttack(0);
                }
                else
                {
                    Patrol(agent, delta);
                }
            }
            else if (distance > 100)
            {
                agent.Move(Math.Sign(agent.TargetOffset.X));
            }
            else
            {
                Patrol(agent, delta);
            }
        }
    }
}
