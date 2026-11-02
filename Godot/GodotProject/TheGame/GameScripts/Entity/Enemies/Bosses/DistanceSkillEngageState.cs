using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 远距优先第三招，近距按首领节拍优先第二招、否则普攻；鲛魔王和鲨魔王共用这一选招规则。
    /// </summary>
    internal sealed class DistanceSkillEngageState : AbstractBossAiState
    {
        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
        {
            int direction = Math.Sign(agent.TargetOffset.X);
            if (MathF.Abs(agent.TargetOffset.X) > agent.EngageBounds.End.X)
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
