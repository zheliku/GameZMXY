using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 人形按80/100/200px原点距离选择吞噬、践踏、冲刺和魔焰，四招各有十五秒冷却。
    /// </summary>
    internal sealed class BullDemonKingEngageState : AbstractBossAiState
    {
        /// <inheritdoc />
        protected override float HeightRange
        {
            get
            {
                return float.PositiveInfinity;
            }
        }

        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
        {
            float distance = MathF.Abs(agent.TargetOffset.X);
            if (distance <= 80f)
            {
                agent.Move(0);
                if (agent.AiData.Cooldowns[4] <= 0f)
                {
                    agent.RequestAttack(4);
                }

                return;
            }
            if (distance <= 100f && agent.AiData.Cooldowns[3] <= 0f)
            {
                agent.RequestAttack(3);
                return;
            }
            if (distance <= 200f)
            {
                if (agent.AiData.Cooldowns[1] <= 0f || agent.AiData.Cooldowns[2] <= 0f)
                {
                    int index = agent.AiData.Cooldowns[1] > 0f ? 2 : agent.AiData.Cooldowns[2] > 0f ? 1
                        : agent.NextRandom() < .5f ? 1 : 2;
                    agent.RequestAttack(index);
                    return;
                }
                if (decision && agent.NextRandom() < .8f)
                {
                    agent.RequestAttack(0);
                }
                else
                {
                    agent.Move(Math.Sign(agent.TargetOffset.X));
                }

                return;
            }
            agent.Move(Math.Sign(agent.TargetOffset.X));
        }
    }
}
