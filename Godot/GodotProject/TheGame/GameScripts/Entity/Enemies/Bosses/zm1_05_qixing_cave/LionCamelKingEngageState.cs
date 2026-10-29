using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 狮驼王按原点距离释放毒雾、旋风或棍击，使用首领公共行为节拍。
    /// </summary>
    internal sealed class LionCamelKingEngageState : AbstractBossAiState
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
            float distance = Math.Abs(agent.TargetOffset.X);
            if (distance > 150f)
            {
                if (agent.AiData.Cooldowns[2] <= 0f)
                {
                    agent.RequestAttack(2);
                }
                else
                {
                    agent.Move(Math.Sign(agent.TargetOffset.X));
                }

                return;
            }

            // 近身只在整秒选招；等待期间保留原来的追击意图，不额外刹停或转身。
            if (decision)
            {
                agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
            }
        }
    }
}
