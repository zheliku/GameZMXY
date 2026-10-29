using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲛魔王远距优先十秒冷却旋转，近距按一秒节拍优先六秒冷却龙波，再使用普攻。
    /// </summary>
    internal sealed class JiaoDemonKingEngageState : AbstractBossAiState
    {
        /// <inheritdoc />
        protected override float HeightRange
        {
            get
            {
                return 500f;
            }
        }

        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
        {
            int direction = Math.Sign(agent.TargetOffset.X);
            if (MathF.Abs(agent.TargetOffset.X) > 250f)
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
