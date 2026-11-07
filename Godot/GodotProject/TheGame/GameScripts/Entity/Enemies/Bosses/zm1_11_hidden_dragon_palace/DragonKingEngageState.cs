using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 龙王近身优先水柱，中距离在冰环与冰冻中选招，冷却中按原版节拍尝试水波。
/// </summary>
internal sealed class DragonKingEngageState : AbstractEnemyAiState
{
    private readonly int _attackDesire; // 本次入场选定的普通水波出手欲望，战斗内升级不改变。
    private readonly float _waterColumnDistance; // 当前场景装配的近身水柱距离，不持有原生形状。

    /// <summary>
    /// 由龙王实体传入本次入场的出手欲望，状态不引用实体节点。
    /// </summary>
    /// <param name="attackDesire">0..100的普通水波出手概率。</param>
    /// <param name="waterColumnDistance">近身水柱水平距离，像素。</param>
    public DragonKingEngageState(int attackDesire, float waterColumnDistance)
    {
        _attackDesire = attackDesire;
        _waterColumnDistance = waterColumnDistance;
    }

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
        double period = Math.Abs(agent.TargetOffset.X) <= _waterColumnDistance ? 1.5 : 1;
        Engage(agent, agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, period), delta);
    }

    /// <summary>
    /// 官方按水平距离选水柱或冰系招式，不限制目标高度。
    /// </summary>
    /// <param name="agent">本首领实例。</param>
    /// <param name="decision">是否跨过当前区间的普攻节拍。</param>
    /// <param name="delta">本次游戏秒数。</param>
    private void Engage(IEnemyAiAgent agent, bool decision, float delta)
    {
        float distance = Math.Abs(agent.TargetOffset.X);
        if (distance <= _waterColumnDistance)
        {
            if (agent.AiData.Cooldowns[3] <= 0)
            {
                agent.RequestAttack(3);
                return;
            }
        }
        else if (distance <= agent.EngageBounds.End.X)
        {
            bool iceRingReady = agent.AiData.Cooldowns[1] <= 0;
            bool freezeReady = agent.AiData.Cooldowns[2] <= 0;
            if (iceRingReady || freezeReady)
            {
                int attackIndex;
                if (iceRingReady && freezeReady)
                {
                    attackIndex = agent.NextRandom() < .5f ? 1 : 2;
                }
                else
                {
                    attackIndex = iceRingReady ? 1 : 2;
                }
                agent.RequestAttack(attackIndex);
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
            if (agent.NextRandom() * 100 < _attackDesire)
            {
                agent.RequestAttack(0);
            }
            else
            {
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
            }
        }
        else if (distance > _waterColumnDistance)
        {
            agent.Move(Math.Sign(agent.TargetOffset.X));
        }
        else
        {
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
        }
    }
}
