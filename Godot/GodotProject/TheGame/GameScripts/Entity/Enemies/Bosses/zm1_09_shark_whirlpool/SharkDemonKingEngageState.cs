using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨魔王按150像素距离选择水牢、鲨波和普攻。
    /// </summary>
    internal sealed class SharkDemonKingEngageState : AbstractBossAiState
    {
        /// <inheritdoc />
        protected override float HeightRange
        {
            get
            {
                return 300f;
            }
        }

        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
        {
            int direction = Math.Sign(agent.TargetOffset.X);
            if (MathF.Abs(agent.TargetOffset.X) > 150f)
            {
                if (agent.AiData.Cooldowns[2] <= 0f && agent.RequestAttack(2))
                {
                    return;
                }

                agent.Move(direction);
                return;
            }
            agent.Move(0);
            if (decision)
            {
                agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
            }
        }
    }
}
