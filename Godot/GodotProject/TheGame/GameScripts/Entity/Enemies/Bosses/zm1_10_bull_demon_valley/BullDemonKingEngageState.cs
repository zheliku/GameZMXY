using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 人形按80/100/200px原点距离选择吞噬、践踏、冲刺和魔焰，四招各有十五秒冷却。
/// </summary>
/// <param name="swallowDistance">吞噬区间的水平上限，像素。</param>
/// <param name="stompDistance">践踏区间的水平上限，像素。</param>
internal sealed class BullDemonKingEngageState(float swallowDistance, float stompDistance) : AbstractEnemyAiState
{
    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float delta)
    {
        if (agent.IsAttacking)
        {
            return;
        }
        if (!agent.HasTarget)
        {
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
            return;
        }
        // Monster23按三个互斥水平区间选招，失败与范围外均执行自身普通游荡。
        float distance = MathF.Abs(agent.TargetOffset.X);
        if (distance <= swallowDistance)
        {
            if (agent.AiData.Cooldowns[4] <= 0f)
            {
                agent.RequestAttack(4);
            }
            return;
        }
        if (distance <= stompDistance)
        {
            if (agent.AiData.Cooldowns[3] <= 0f)
            {
                agent.RequestAttack(3);
                return;
            }
        }
        else if (distance <= agent.EngageBounds.End.X)
        {
            if (agent.AiData.Cooldowns[1] <= 0f || agent.AiData.Cooldowns[2] <= 0f)
            {
                int index;
                if (agent.AiData.Cooldowns[1] > 0f)
                {
                    index = 2;
                }
                else if (agent.AiData.Cooldowns[2] > 0f)
                {
                    index = 1;
                }
                else
                {
                    // 两招都就绪时才抽签，单招就绪时不消费随机源。
                    index = agent.NextRandom() < .5f ? 1 : 2;
                }
                agent.RequestAttack(index);
                return;
            }
        }
        else
        {
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
            return;
        }
        // 原版80%普攻抽签每源24Hz发生一次；以游戏秒换算，避免高帧率改变频率。
        if (agent.NextRandom() < 1 - Math.Pow(.2, delta * 24))
        {
            agent.RequestAttack(0);
        }
        else
        {
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
        }
    }
}
