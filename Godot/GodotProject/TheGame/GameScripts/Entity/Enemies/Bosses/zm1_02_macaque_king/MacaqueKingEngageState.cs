using System;
using GameFramework.Fsm;
using Godot;

namespace GameLogic.Enemies.AI;

/// <summary>
/// 猕猴王：远处先发风刃，近处按一秒节拍优先旋风，否则棍击；不采用小怪的随机选招。
/// </summary>
public sealed class MacaqueKingEngageState : AbstractBossAiState
{
    /// <summary>
    /// 独立决定风刃、旋风、棍击或追击；冷却在请求被接受时计入。
    /// </summary>
    /// <param name="agent">只提供感知、冷却和请求的宿主。</param>
    /// <param name="decision">是否跨过一秒近身选招时点。</param>
    /// <param name="delta">本次逻辑推进的秒数。</param>
    protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
    {
        Vector2 offset = agent.TargetOffset;
        if (Math.Abs(offset.X) > agent.EngageBounds.End.X)
        {
            if (agent.AiData.Cooldowns[2] <= 0f && agent.RequestAttack(2))
            {
                return;
            }

            agent.Move(Math.Sign(offset.X));
            return;
        }
        agent.Move(0);
        agent.Face(Math.Sign(offset.X));
        if (decision)
        {
            agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
        }
    }
}
