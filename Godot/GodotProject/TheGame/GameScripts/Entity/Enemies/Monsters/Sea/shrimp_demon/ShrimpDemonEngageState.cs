using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 虾兵在场景的交战外界与退避内界之间保持距离，按自身欲望发射可击散魔法弹。
/// </summary>
/// <param name="retreatDistance">场景退避内界的水平距离，像素。</param>
internal sealed class ShrimpDemonEngageState(float retreatDistance) : AbstractEnemyAiState
{
    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
    {
        if (agent.IsAttacking)
        {
            return;
        }

        if (!agent.HasTarget)
        {
            ChangeState<PatrolState>(fsm);
            return;
        }
        if (agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, agent.Config.AttackInterval))
        {
            if (Chance(agent, agent.Config.AttackDesire))
            {
                agent.RequestAttack(0);
            }
            else
            {
                agent.Move(0);
            }

            return;
        }
        int direction = Math.Sign(agent.TargetOffset.X);
        if (Math.Abs(agent.TargetOffset.X) <= retreatDistance)
        {
            agent.Move(-direction);
        }
        else if (Math.Abs(agent.TargetOffset.X) >= agent.EngageBounds.End.X)
        {
            agent.Move(direction);
        }
    }
}
