using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using Godot;

namespace GameLogic.Enemies.AI;

/// <summary>
/// 普通交战：目标高度超限则巡逻，范围外追击，范围内在决策节拍提交攻击；范围只读场景。
/// </summary>
/// <param name="cooldownAttackIndex">可选的优先冷却招式；指定时每个决策节拍出招，不再抽普通物种欲望。</param>
public sealed class EngageState(int? cooldownAttackIndex = null) : AbstractEnemyAiState
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
            ChangeState(fsm, agent.AiPreset.IdleStateType);
            return;
        }
        Rect2 bounds = agent.EngageBounds;
        Vector2 target = agent.TargetOffset;
        if (target.Y < bounds.Position.Y || target.Y > bounds.End.Y)
        {
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
        }
        else if (!agent.IsInEngageRange)
        {
            agent.Move(Math.Sign(target.X));
        }
        else if (agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, agent.Config.AttackInterval))
        {
            Attack(agent);
        }
    }

    /// <summary>
    /// 普通物种按欲望发起第一招；双招物种优先已冷却的指定招式，否则使用普攻。
    /// </summary>
    /// <param name="agent">当前实例的请求与数值。</param>
    private void Attack(IEnemyAiAgent agent)
    {
        if (cooldownAttackIndex is int index)
        {
            agent.RequestAttack(agent.AiData.Cooldowns[index] <= 0f ? index : 0);
        }
        else if (Chance(agent, agent.Config.AttackDesire))
        {
            agent.RequestAttack(0);
        }
        else
        {
            agent.Move(0);
        }
    }
}
