using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI;

/// <summary>猕猴王：远处先发风刃，近处按一秒节拍优先旋风，否则棍击；不采用小怪的随机选招。</summary>
public sealed class MacaqueKingEngageState : AbstractBossAiState
{
    private const float MeleeDistance = 150f; // Monster4.attackRange，以角色原点计算。
    /// <summary>原版 attackRange × 2 的纵向追击范围。</summary>
    protected override float HeightRange => 300f;

    /// <summary>独立决定风刃、旋风、棍击或追击；冷却在身体提交出招时计入。</summary>
    /// <param name="agent">只提供感知、冷却和请求的宿主。</param>
    /// <param name="decision">是否跨过一秒近身选招时点。</param>
    protected override void Engage(IEnemyAiAgent agent, bool decision)
    {
        var offset = agent.TargetOffset;
        if (Math.Abs(offset.X) > MeleeDistance)
        {
            if (agent.AiData.Cooldowns[2] <= 0f && agent.RequestAttack(2))
                return;
            agent.Move(Math.Sign(offset.X));
            return;
        }
        agent.Move(0);
        agent.Face(Math.Sign(offset.X));
        if (decision)
            agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
    }
}
